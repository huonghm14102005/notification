# Cẩm Nang Kiến Thức & Kiến Trúc Dự Án (Knowledge Base)

> **Dự án**: `notification-server` — Hệ thống Phân Phối Thông Báo Đa Kênh Tập Trung (Email, Mobile Push, Webhook Callback)  
> **Kiến trúc**: .NET 10 Clean Architecture, PostgreSQL, React 19 Admin SPA, Multi-Tenancy  
> **Mục đích tài liệu**: Lưu trữ toàn bộ kiến thức nghiệp vụ, quyết định kiến trúc cốt lõi, kinh nghiệm xử lý hạ tầng Cloud và cẩm nang kiểm thử thực chiến.

---

## 1. Tổng Quan Kiến Trúc Hệ Thống (High-Level Architecture)

`notification-server` được thiết kế theo mô hình **Bất đồng bộ hướng sự kiện (Asynchronous Event-Driven Architecture)** nhằm phục vụ hàng triệu thông báo mà không làm nghẽn hệ thống nguồn:

```mermaid
graph TD
    Client[Client / Backend / Microservices] -->|1. POST /v1/notifications kèm X-API-Key| API[Notification API Engine]
    API -->|2. Lưu Outbox & Phản hồi 202 Accepted trong 30ms| DB[(PostgreSQL Database)]
    
    subgraph "Background Processing (Worker)"
        Worker[Notification Background Worker] -->|3. Polling / Nhận việc từ Outbox| DB
        Worker -->|4a. Gửi Email qua HTTPS 443 / SMTP| MailProvider[Resend / Gmail / SendGrid]
        Worker -->|4b. Bắn Push Notification| MobilePush[Google FCM / Apple APNs]
        Worker -->|5. Đẩy kết quả qua Webhook Callback HMAC| ClientWebhook[Client Webhook Endpoint]
    end
```

### Tại sao API phải trả về `202 Accepted` thay vì chờ gửi xong?
- **Tránh Timeout**: Nếu một đơn hàng cần gửi 5.000 email, việc chờ gửi xong mới trả về HTTP response sẽ khiến trình duyệt của khách hàng bị quay đơ (timeout 30s).
- **Phân tách trách nhiệm (Decoupling)**: Hệ thống nguồn chỉ cần đảm bảo tin nhắn đã được ghi nhận an toàn vào hàng đợi (`202 Accepted`). Việc kết nối với nhà mạng, thử lại khi rớt mạng (Exponential Backoff Retry) là nhiệm vụ của Worker chạy ngầm.

---

## 2. Kiến Trúc Thiết Bị & Phân Quyền Bảo Mật (Device Roles)

Một trong những thiết kế quan trọng nhất của hệ thống là thực thể **Device (Thiết bị)**. Thiết bị đại diện cho cả **hệ thống phát tin** lẫn **thiết bị nhận tin**.

```mermaid
graph LR
    Device[Thiết bị - Device UUID] -->|Role: source| SourceCaps[Máy chủ Backend: API Key + Webhook Callback]
    Device -->|Role: recipient| RecipientCaps[App Di Động: Push Token FCM/APNs]
    Device -->|Role: both| BothCaps[Máy POS / App Shipper: Cả hai quyền]
```

### 2.1. Tại sao phải phân chia 3 vai trò (`source`, `recipient`, `both`)?
Tuân thủ tuyệt đối **Nguyên tắc phân quyền tối thiểu (Principle of Least Privilege)**:

1. **`source` (Hệ thống Nguồn - Máy phát tin)**:
   - **Thực tế**: Là các cụm máy chủ Backend, Microservices (như `Order-Service`, `Billing-Service`, `Website-Checkout`).
   - **Khả năng**: Được cấp **API Key** để đẩy thông báo vào hàng đợi (`POST /v1/notifications`), và được cấu hình **Webhook Callback** để nhận báo cáo trạng thái hoàn thành.
   - **Bảo mật**: Tuyệt đối **không nhận Push Notification** và không đăng ký push token.

2. **`recipient` (Thiết bị Đích - Người nhận tin)**:
   - **Thực tế**: Là điện thoại cá nhân (iPhone / Android) của người dùng cuối hoặc nhân viên.
   - **Khả năng**: Đăng ký **Push Token (FCM / APNs)** với server để nhận thông báo nổi trên màn hình khóa.
   - **Bảo mật tối quan trọng**: **TUYỆT ĐỐI KHÔNG CẤP API KEY CHO APP DI ĐỘNG**. Nếu cấp API Key gắn vào app mobile, tin tặc có thể decompile (dịch ngược file APK/IPA), lấy cắp API Key và biến server thành công cụ spam tin nhắn rác.

3. **`both` (Cả hai vai trò - Thiết bị hai chiều)**:
   - **Thực tế**: Dành cho thiết bị chuyên dụng vừa nhận việc vừa báo cáo kết quả: ví dụ **Máy POS bán hàng**, **Ứng dụng của Shipper / Tài xế** (vừa nhận cuốc xe mới qua Push, vừa bấm hoàn thành đơn để gọi API báo hệ thống gửi tin cho khách).

---

## 3. Bản Chất Của Mobile Push Endpoint & Webhook Callback (HMAC)

### 3.1. Mobile Push Endpoint (FCM / APNs) Hoạt Động Như Thế Nào?
- **Bản chất**: Cho phép server đánh thức điện thoại, làm rung chuông và hiện pop-up ngay cả khi người dùng đã tắt hoàn toàn ứng dụng (killed app).
- **Luồng dữ liệu**:
  1. Điện thoại khởi động -> Xin Apple (APNs) hoặc Google (FCM) cấp một chuỗi `Push Token`.
  2. App gửi token này lên server: `POST /v1/devices/{deviceId}/push-endpoint`.
  3. Server mã hóa token bằng chuẩn quân sự **`AES-256-GCM`** trước khi lưu vào CSDL.
  4. Khi gửi thông báo, người gửi chỉ cần chỉ định `target: "<deviceId>"`. Worker tự động giải mã token và gọi Google/Apple chuyển tiếp tin tới điện thoại.

### 3.2. Webhook Callback (HMAC) Dùng Để Làm Gì?
- **Bản chất**: Báo cáo kết quả gửi tin ngược về cho hệ thống nguồn theo cơ chế bất đồng bộ (Asynchronous Callback).
- **Chữ ký số chống giả mạo (HMAC-SHA256)**:
  - Khi gửi callback về URL của bạn, server đính kèm Header: `X-Signature-SHA256: <hex-hash>`.
  - Hệ thống nguồn dùng mã `HMAC Secret` đã lưu để tính hash payload và so sánh với header này.
  - **Lợi ích**: Đảm bảo 100% gói tin này xuất phát từ chính `notification-server`, loại trừ hoàn toàn nguy cơ kẻ xấu tấn công giả mạo trạng thái (Man-in-the-middle / Tampering).

---

## 4. Kinh Nghiệm Thực Chiến Hạ Tầng & Cloud Deploy (Gotchas & Workarounds)

### 4.1. Lỗi Render Free Tier Chặn Cổng Mạng SMTP Outbound (504 Gateway Timeout)
- **Vấn đề thực tế**:
  - Khi deploy backend lên các nền tảng Cloud Free Tier (tiêu biểu là **Render Free Tier**), nhà cung cấp khóa hoàn toàn lưu lượng mạng TCP đi ra ngoài (Outbound Traffic) trên các cổng SMTP: **`Port 25`**, **`Port 465`**, **`Port 587`** nhằm chống spam bot.
  - Hậu quả: Kết nối SMTP trực tiếp tới Gmail, Mailgun hay SendGrid sẽ bị treo và trả về lỗi **`504 Gateway Timeout (SMTP_TEST_TIMEOUT)`**.
- **Giải pháp đột phá — Resend Native HTTPS Port 443**:
  - `notification-server` xây dựng cơ chế tự động nhận diện thông minh trong [MailKitEmailSender.cs](file:///d:/Workspace/StartUp/notification-server/src/Notification.Infrastructure/Email/MailKitEmailSender.cs):
    - Khi phát hiện cấu hình máy chủ là `smtp.resend.com` hoặc mật khẩu có tiền tố `re_`, hệ thống **không mở kết nối SMTP TCP** mà tự động chuyển hướng gửi qua **Resend REST API (`https://api.resend.com/emails`)**.
    - REST API chạy trên **Cổng 443 (HTTPS chuẩn)**, cổng này mở 100% trên mọi nền tảng Cloud, giúp ứng dụng gửi thư ổn định tuyệt đối mà không sợ bị firewall chặn.

### 4.2. Giới Hạn Của Tài Khoản Resend Free Tier
- **Đặc điểm**: Tài khoản Resend miễn phí cấp sẵn tên miền gửi mặc định là `onboarding@resend.dev`.
- **Ràng buộc**: Với tên miền dùng thử này, Resend **CHỈ CHO PHÉP** gửi email đến **chính địa chỉ email bạn đã dùng để đăng ký tài khoản Resend** (ví dụ: `huong102145@st.vimaru.edu.vn`).
- **Hiện tượng lỗi**: Nếu thử gửi tới email khác (ví dụ: `test@gmail.com`), Resend sẽ trả về lỗi **`403 Forbidden`**:  
  *"You can only send testing emails to your own email address..."*
- **Khắc phục**: Khi test, bắt buộc nhập đúng email tài khoản Resend. Muốn gửi tự do cho bất kỳ ai, cần vào trang chủ Resend thêm và xác minh tên miền riêng (Custom Domain).

### 4.3. Cơ Chế Hoán Đổi Mặc Định Nguyên Tử (Atomic Default Swap)
- Trong một tổ chức (tenant), tại một thời điểm **chỉ được phép có duy nhất 1 Sender là Mặc định (`isDefault: true`)**.
- Khi người dùng đặt Sender B làm mặc định, database sử dụng câu lệnh update nguyên tử trong một Transaction để chuyển tất cả các Sender khác thành `isDefault = false`, loại trừ triệt để lỗi xung đột cấu hình (race condition).

---

## 5. Quy Trình Xác Định ID Thiết Bị Trên Mobile Trong Thực Tế

Nhiều nhà phát triển thắc mắc: *"Khi người dùng đăng nhập trên điện thoại thì làm sao backend biết ID thiết bị đó là gì?"*

Quy trình chuẩn 4 bước được áp dụng trong mọi ứng dụng di động thực tế (React Native, Flutter, Swift, Kotlin):

```mermaid
sequenceDiagram
    autonumber
    actor User as Người dùng
    participant App as Mobile App
    participant OS as Hệ điều hành (iOS / Android)
    participant Server as Notification Server
    
    User->>App: Mở App & Đăng nhập
    App->>OS: Lấy Hardware ID hoặc sinh UUID duy nhất
    Note over App: Lưu Hardware UUID vào Keychain / Keystore an toàn
    App->>Server: POST /v1/devices { name: "iPhone 15 của Hưởng", role: "recipient" }
    Server-->>App: Trả về { id: "9b1deb4d-..." } (deviceId cố định)
    Note over App: Lưu deviceId vào bộ nhớ máy
    
    App->>OS: Xin cấp quyền Push & nhận FCM/APNs Token
    OS-->>App: Trả về chuỗi token thiết bị
    App->>Server: POST /v1/devices/{deviceId}/push-endpoint { platform: "fcm", token: "..." }
    Server-->>App: 200 OK (Kích hoạt push thành công)
```

- **Bước 1**: Khi ứng dụng được mở lần đầu, App dùng thư viện lấy mã phần cứng không đổi (`identifierForVendor` trên iOS hoặc `ANDROID_ID` trên Android) hoặc tự sinh 1 chuỗi UUID v4 ngẫu nhiên, lưu vào vùng nhớ bảo mật không bị xoá khi update (**iOS Keychain** hoặc **Android EncryptedSharedPreferences**).
- **Bước 2**: Sau khi user đăng nhập, App gọi `POST /v1/devices` với tên máy và nhận về mã `deviceId` cố định.
- **Bước 3**: App nhận Push Token từ Google/Apple và gọi `POST /v1/devices/{deviceId}/push-endpoint` để liên kết token với thiết bị.
- **Bước 4**: Về sau, bất cứ khi nào hệ thống muốn gửi thông báo đến chiếc máy này, chỉ cần chỉ định đích đến là `target: "<deviceId>"`.

---

## 6. Cẩm Nang Kiểm Thử Thực Chiến (Testing Playbook)

Dưới đây là các phương pháp kiểm thử đầy đủ toàn bộ hệ thống ngay trên trình duyệt và terminal:

### 6.1. Kiểm thử Gửi Email (Sender Test)
1. Truy cập Web Admin -> **Kênh Email** (`/senders`).
2. Chọn **Thêm máy chủ gửi thư** -> Chọn mẫu nhanh **`⚡ Resend (Khuyên dùng trên Cloud / Render)`**.
3. Điền API Key (`re_...`) -> Bấm **Tạo Máy Chủ Gửi Thư**.
4. Bấm **✉ Gửi thư thử nghiệm**:
   - Nhập email người nhận (lưu ý tài khoản Resend Free phải nhập chính email đăng ký Resend).
   - Bấm **Gửi thử ngay** -> Nhận thông báo thành công và huy hiệu chuyển màu xanh `✓ Đã kiểm thử`.

### 6.2. Kiểm thử API Key của Thiết Bị
1. Vào **Thiết bị & Keys** (`/devices`) -> Tạo thiết bị vai trò `source`.
2. Mở chi tiết thiết bị -> Mục **Danh sách API Keys** -> Bấm **+ Tạo API Key mới** -> Sao chép chuỗi `notify_...`.
3. Mở Terminal / PowerShell, chạy lệnh cURL:
   ```bash
   curl -X POST "https://notification-len1.onrender.com/v1/notifications" \
     -H "Content-Type: application/json" \
     -H "X-API-Key: <DÁN_API_KEY_TẠI_ĐÂY>" \
     -d '{
       "recipientEmail": "huong102145@st.vimaru.edu.vn",
       "subject": "Test API Key",
       "body": "Thông báo kiểm thử gửi bằng API Key của thiết bị."
     }'
   ```
   *Kết quả*: Trả về `202 Accepted` kèm `id` thông báo.
4. Quay lại Web Admin, bấm **Thu hồi (Revoke)** key đó -> Chạy lại lệnh cURL trên -> Server lập tức từ chối với mã **`401 Unauthorized`**.

### 6.3. Kiểm thử Webhook Callback HMAC bằng `webhook.site`
1. Truy cập **[https://webhook.site](https://webhook.site)** -> Sao chép URL tạm thời được cấp.
2. Vào chi tiết thiết bị trên Web Admin -> **Webhook Callback (HMAC)** -> Bấm **Cấu hình Callback** -> Dán URL vào -> Bấm lưu và nhận mã **HMAC Secret**.
3. Gửi 1 thông báo bằng API Key.
4. Quay lại màn hình `webhook.site`: Gói tin callback `notification.completed` sẽ nổ về ngay lập tức:
   - **Tab Headers**: Có `X-Signature-SHA256` và `X-Event-ID`.
   - **Tab Body**: Có payload trạng thái hoàn tất (`status: "delivered"`).

### 6.4. Kiểm thử Push Endpoint (FCM / APNs)
1. Tạo thiết bị vai trò `recipient` (ví dụ: `iPhone 15 Test`).
2. Mở chi tiết thiết bị -> **Mobile Push Endpoint** -> Bấm **+ Đăng ký Push Token**.
3. Điền Mock Token (ví dụ: `fcm_mock_test_token_xyz123`) -> Bấm **Lưu Token**.
4. Xác nhận trạng thái chuyển sang `Đang hoạt động (active)` và token được mã hóa `AES-256-GCM` trong database.
5. Bấm **Hủy Push Token** để kiểm tra tính năng thu hồi.

---

## 7. Tổng Hợp Các Mã Lỗi Thường Gặp & Cách Khắc Phục

| Mã Lỗi | HTTP Code | Nguyên Nhân Thực Tế | Cách Khắc Phục |
| :--- | :---: | :--- | :--- |
| `SMTP_TEST_TIMEOUT` | **504** | Hạ tầng Cloud (Render Free Tier) chặn cổng kết nối SMTP TCP (587, 465, 25). | Chuyển cấu hình máy chủ sang dùng **Resend** để tự động bypass qua cổng HTTPS 443. |
| `SMTP_TEST_FAILED` | **502** | Sai mật khẩu/API Key hoặc tài khoản Resend Free gửi tới email khác email chủ tài khoản. | Kiểm tra thông điệp chi tiết trả về: nhập đúng email đăng ký Resend hoặc kiểm tra lại App Password Google. |
| `SENDER_NOT_FOUND` | **409** | Gửi email nhưng trong Tenant chưa có Sender nào hoạt động (`status: active`). | Vào mục "Cấu hình SMTP", tạo 1 Sender (Resend/Gmail), tích chọn làm Sender mặc định. |
| `DISCORD_RATE_LIMITED` | **429** | Discord Cloudflare áp dụng giới hạn tần suất toàn cục (mã 0) lên dải IP chung của Render. | Tự động chuyển hướng sang `canary.discord.com` hoặc dùng nút "Bắn test ngay" từ trình duyệt. |
| `DEVICE_DISABLED` | **409** | Thiết bị đã bị vô hiệu hóa nên không thể tạo key mới hoặc sửa thông số. | Bật lại thiết bị hoặc tạo thiết bị mới. |
| `FORBIDDEN` | **403** | Tài khoản không có quyền thực hiện thao tác (ví dụ xem toàn bộ tenant khi không phải Owner). | Đăng nhập bằng tài khoản có vai trò Owner hoặc Admin. |
| `API_KEY_LIMIT_REACHED` | **409** | Thiết bị đã đạt giới hạn tối đa số lượng API Key hoạt động đồng thời (10 keys/device). | Thu hồi bớt các API Key cũ không còn sử dụng trước khi tạo mới. |

---

## 8. Chuyên Đề Toàn Diện Về Webhook Trong Dự Án (Webhook Deep-Dive)

Nhiều lập trình viên thường nhầm lẫn giữa **API thông thường** và **Webhook**. Trong dự án này, Webhook xuất hiện ở 2 vị thế hoàn toàn khác nhau:

```text
┌─────────────────────────────────────────────────────────────────────────────┐
│                          HAI HÌNH THÁI WEBHOOK                              │
├──────────────────────────────────────┬──────────────────────────────────────┤
│  1. WEBHOOK CALLBACK (Báo cáo ngược) │  2. INCOMING ADAPTER (Bắn tin ra)    │
├──────────────────────────────────────┼──────────────────────────────────────┤
│  • Chiều: Notification-Server        │  • Chiều: Notification-Server        │
│          ──► Backend của Khách hàng  │          ──► Discord / Slack API     │
│  • Mục đích: "Tôi đã gửi xong thư,   │  • Mục đích: "Hãy hiển thị tin nhắn  │
│    đây là kết quả cho bạn."          │    này lên kênh chat của nhóm."      │
│  • Bảo mật: Chữ ký số HMAC-SHA256    │  • Bảo mật: Token nằm trong Webhook  │
│    chống giả mạo gói tin.            │    URL của Discord.                  │
└──────────────────────────────────────┴──────────────────────────────────────┘
```

### 8.1. Tại sao cần Webhook Callback? (Mô hình "Đừng gọi hỏi tôi, tôi sẽ tự báo cho bạn")
- **Vấn đề Polling**: Nếu không có Webhook Callback, sau khi gửi đơn hàng, Backend của khách hàng sẽ phải liên tục chạy vòng lặp `GET /v1/notifications/{id}` mỗi 2 giây để xem email đã gửi tới khách chưa. Nếu có 100.000 đơn hàng, server sẽ chịu hàng triệu request hỏi thăm vô ích làm nghẽn CPU và mạng.
- **Giải pháp Webhook**: 
  - Backend của bạn gửi tin xong thì rảnh tay làm việc khác (`202 Accepted`).
  - Khi Worker hoàn tất việc gửi thư (dù thành công hay thất bại), hệ thống sẽ **chủ động gửi một HTTP POST** chứa toàn bộ kết quả về địa chỉ Webhook mà bạn đã đăng ký trên thiết bị nguồn (`source`).

### 8.2. Cơ Chế Chữ Ký Số HMAC SHA-256 (Tối quan trọng trong Bảo mật)
Làm thế nào để Backend của bạn biết chắc chắn gói tin POST gửi đến thực sự là từ `notification-server` chứ không phải một hacker đang giả mạo gửi báo cáo lừa đảo?

1. **Sinh khóa bí mật (Secret)**: Khi bạn cấu hình Webhook trên giao diện, hệ thống sinh ra một chuỗi ngẫu nhiên dài 64 ký tự gọi là `HMAC Secret` (chỉ hệ thống và bạn biết).
2. **Ký số gói tin (Signing)**: Trước khi bắn callback, server lấy toàn bộ nội dung JSON Body, dùng thuật toán `HMAC-SHA256` cùng `Secret` để tính ra một chuỗi băm (Hash), sau đó gắn vào Header:
   ```http
   X-Signature-SHA256: 3b1a8d... (chuỗi hex 64 ký tự)
   X-Event-ID: 7b8a9c0d-1e2f-...
   Content-Type: application/json
   ```
3. **Xác thực tại đầu nhận (Verification)**:
   - Backend của bạn nhận gói tin, lấy nguyên văn chuỗi Body và dùng chính `Secret` đã lưu để tính lại mã băm.
   - Nếu mã băm tính được **khớp 100%** với `X-Signature-SHA256` trong header -> Tin nhắn là thật và chưa hề bị sửa đổi dọc đường.

---

## 9. Sáu Trụ Cột Kiến Thức Chủ Đạo Của Toàn Bộ Dự Án

Toàn bộ `notification-server` được xây dựng dựa trên 6 nguyên lý kiến trúc bất biến sau:

### Trụ cột 1: Bất Đồng Bộ Hướng Sự Kiện & Outbox Pattern (Async Engine)
- **Tách rời Tốc độ và Vận chuyển**: Tầng API chỉ làm 1 việc duy nhất là kiểm tra tính hợp lệ dữ liệu và ghi vào bảng CSDL trong 30ms, trả về ngay `202 Accepted`.
- **Worker chuyên trách**: Toàn bộ việc kết nối SMTP, gọi API mạng, thử lại khi rớt mạng (Exponential Backoff: 1 phút, 5 phút, 25 phút) được giao cho Worker tiến hành ngầm.
- **Không bao giờ gửi trùng**: Cơ chế khóa dòng `FOR UPDATE SKIP LOCKED` trong PostgreSQL cho phép nhiều Worker chạy song song cùng quét 1 bảng hàng đợi mà không bao giờ tranh chấp hay gửi trùng tin nhắn.

### Trụ cột 2: Cô Lập Dữ Liệu Đa Người Thuê Tuyệt Đối (Multi-Tenancy Isolation)
- Hệ thống được thiết kế theo chuẩn SaaS B2B phục vụ hàng ngàn công ty (Tenant) trên cùng 1 CSDL.
- **Quy tắc cốt tử**: Mọi bảng dữ liệu nghiệp vụ (`notifications`, `deliveries`, `senders`, `templates`, `devices`) đều bắt buộc có cột `tenant_id`. API tự động trích xuất `tenant_id` từ Token đã xác thực, tuyệt đối không tin ID client truyền lên URL.

### Trụ cột 3: Ma Trận Phân Quyền Thiết Bị (Device Role Matrix)
- **`source`**: Chỉ dành cho máy chủ Backend (được cấp API Key, cấu hình Webhook Callback, tuyệt đối không nhận push notification).
- **`recipient`**: Chỉ dành cho App Di Động (đăng ký Push Token, **tuyệt đối không cấp API Key** để chống tin tặc decompile app lấy trộm key đi spam).
- **`both`**: Dành cho máy POS, app shipper vừa nhận lệnh vừa báo cáo.

### Trụ cột 4: Tính Bất Biến & Vết Kiểm Toán (Immutable Versioning & Audit Trail)
- **Notification là bất biến**: Thông báo khi đã lưu trữ thì không bao giờ sửa nội dung (Subject, Body, Người nhận), chỉ cập nhật tiến trình và ghi vết từng lần gửi (`Delivery Attempts`).
- **Template Versioning**: Mẫu khi đã xuất bản (`active`) sẽ bị khóa cứng vĩnh viễn để bảo vệ tính toàn vẹn. Muốn đổi nội dung bắt buộc phải tạo bản nháp mới (`v2 draft`).

### Trụ cột 5: Khả Năng Thích Ứng Hạ Tầng Mạng Cloud (Cloud Resilient Networking)
- Tự động nhận diện Render chặn cổng SMTP 587/465 để chuyển sang **Native HTTPS Port 443** qua Resend REST API.
- Tự động nhận diện Discord Cloudflare Rate Limit IP Render để **Failover sang `canary.discord.com`** và hỗ trợ **Bắn test trực tiếp từ Trình duyệt (CORS bypass)**.

### Trụ cột 6: Quản Lý Mẫu Đa Định Dạng & Phòng Vệ XSS (Security First)
- Mẫu thông báo hỗ trợ đồng thời cả Plaintext và HTML.
- Toàn bộ biến số nội suy `{{var}}` đều được mã hóa an toàn (HTML Entity Escaping) trước khi đưa vào template HTML, triệt tiêu 100% lỗ hổng bảo mật tấn công Cross-Site Scripting (XSS).

