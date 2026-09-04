# Ma Trận Chức Năng CRUD & Thiết Kế Kiến Trúc Hệ Thống

Tài liệu này tổng hợp toàn bộ năng lực **CRUD (Create - Read - Update - Delete)** của từng phân hệ trong `notification-server`, đồng thời giải trình chi tiết về kiến trúc thiết bị (Device Roles), cơ chế định danh trên Mobile, và ứng dụng thực tế của Push Endpoint & Webhook Callback (HMAC).

---

## 1. Ma Trận Chức Năng CRUD Theo Từng Phân Hệ

Hệ thống được thiết kế theo chuẩn **Soft-delete / Immutable Audit** (không xoá vật lý làm mất vết dữ liệu liên kết mà chuyển trạng thái `disabled`, `retired`, `revoked`).

| Phân hệ | Create (Tạo mới) | Read (Xem / Lọc) | Update (Cập nhật) | Delete (Xoá / Vô hiệu) | Thao tác Nghiệp vụ Bổ sung |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **1. SMTP Senders**<br>`/v1/senders` | `POST /v1/senders`<br>Tạo máy chủ gửi mail | `GET /v1/senders`<br>Danh sách có phân trang cursor | `PATCH /v1/senders/{id}`<br>Sửa Host, Port, Creds, Name | `DELETE /v1/senders/{id}`<br>Vô hiệu hóa (Status: disabled) | - Đặt làm mặc định (`isDefault: true`)<br>- Gửi thử nghiệm (`POST /v1/senders/{id}/test`) |
| **2. Devices**<br>`/v1/devices` | `POST /v1/devices`<br>Đăng ký thiết bị (name, role) | `GET /v1/devices`<br>Lọc theo `scope=mine\|tenant`, `status` | `PATCH /v1/devices/{id}`<br>Đổi tên thiết bị | `POST /v1/devices/{id}/disable`<br>Vô hiệu hoá toàn bộ key | - Đăng ký Push Endpoint (FCM/APNs)<br>- Cấu hình Webhook Callback |
| **2.1. API Keys**<br>`/devices/{id}/api-keys` | `POST .../api-keys`<br>Sinh key mới (`notify_...`) | `GET .../api-keys`<br>Xem danh sách tiền tố `keyPrefix` | *(Không cho phép sửa key để đảm bảo an toàn)* | `DELETE .../api-keys/{keyId}`<br>Thu hồi khoá (Revoke key) | - Khóa máy đa năng xoay vòng (Rotate)<br>- Tự động chặn ngay khi Device bị disable |
| **2.2. Webhook Callback**<br>`/devices/{id}/callback` | `PUT .../callback`<br>Gán URL và sinh Secret HMAC | `GET /v1/devices/{id}`<br>Xem cờ `hasCallbackSecret` | `PUT .../callback`<br>Cập nhật URL mới và xoay Secret | *(Tắt callback bằng cách xoá URL hoặc disable device)* | - Tự động ký chữ ký số HMAC-SHA256<br>- Đẩy kết quả bất đồng bộ về nguồn |
| **2.3. Push Endpoints**<br>`/devices/{id}/push-endpoint` | `POST .../push-endpoint`<br>Đăng ký FCM / APNs token | `GET .../push-endpoint`<br>Xem platform, ngày tạo, trạng thái | `POST .../push-endpoint`<br>Ghi đè/xoay vòng token mới | `DELETE .../push-endpoint`<br>Vô hiệu hoá token | - Tự động mã hoá token bằng AES-256-GCM<br>- Tự động disable khi Provider báo token chết |
| **3. Templates**<br>`/v1/templates` | `POST /v1/templates`<br>Tạo mẫu nháp (Status: draft) | `GET /v1/templates`<br>Lọc theo status, scope, audience | `PATCH /v1/templates/{id}`<br>Chỉnh sửa nội dung khi còn Draft | *(Không xoá cứng để bảo tồn lịch sử render)* | - **Xuất bản (`/publish`)**: Khóa bất biến v1<br>- **Tạo version mới (`/versions`)**: Nhân bản v2 nháp |
| **4. Users / Identity**<br>`/v1/users` | `POST /v1/users`<br>Tạo tài khoản Member | `GET /v1/users`<br>Lọc theo status, xem `/users/me` | *(Cập nhật profile trong tương lai)* | `POST /v1/users/{id}/disable`<br>Khoá tài khoản thành viên | - Đăng ký tổ chức (`/tenants/register`)<br>- Đăng nhập / Refresh / Đăng xuất JWT |
| **5. Notifications**<br>`/v1/notifications` | `POST /v1/notifications`<br>Tiếp nhận gửi qua API Key / Admin | `GET /v1/notifications`<br>Lọc Kênh, Status, Device, Key | *(Nội dung thông báo đã gửi là bất biến)* | `POST /v1/notifications/{id}/cancel`<br>Hủy thông báo đang chờ (`accepted`) | - **Gửi lại thủ công (`/retry`)** khi thất bại<br>- Xem chi tiết lần gửi (Delivery Attempts) |

---

## 2. Giải Trình Kiến Trúc Thiết Bị: Tại Sao Lại Chia Ra Các Vai Trò (`source`, `recipient`, `both`)?

Trong tài liệu đặc tả [DEVICE-001](features/v1/08-devices/DEVICE-001-thiet-bi-va-api-key.md) và [DEVICE-002](features/v1/08-devices/DEVICE-002-push-endpoint.md), hệ thống đã quy định rõ ràng việc phân chia 3 vai trò này:

```text
                               ┌─────────────┐
                               │   TENANT    │
                               └──────┬──────┘
                                      │
                               ┌──────┴──────┐
                               │    USER     │
                               └──────┬──────┘
                                      │
                 ┌────────────────────┴────────────────────┐
                 ▼                                         ▼
         ┌───────────────┐                         ┌───────────────┐
         │ Role: source  │                         │Role: recipient│
         │ (Backend Sys) │                         │ (Mobile App)  │
         └───────┬───────┘                         └───────┬───────┘
                 │                                         │
        ┌────────┴────────┐                                │
        ▼                 ▼                                ▼
   [API Keys]     [Webhook Callback]              [Push FCM / APNs]
(Gọi API phát tin) (Nhận báo cáo kết quả)       (Nhận pop-up thông báo)
```

### Lý do cốt lõi:
1. **Khác biệt về luồng dữ liệu (Data Direction)**:
   - `source` (Nguồn): Là hệ thống **PHÁT TIN** (đi từ trong ra ngoài). Ví dụ: Backend NodeJS xử lý đơn hàng, Backend Java quản lý chấm công. Chúng cần **API Key** để đẩy tin vào hàng đợi, và cần **Webhook Callback** để nhận kết quả sau khi Worker gửi xong.
   - `recipient` (Đích nhận): Là thiết bị **NHẬN TIN** (đi từ ngoài vào trong). Ví dụ: Điện thoại iPhone của khách hàng. Khách hàng không bao giờ được cấp API Key để tự ý bắn tin đi khắp nơi, mà điện thoại của họ chỉ cần đăng ký **Push Token (FCM/APNs)** để server bắn thông báo về.
2. **Vai trò hỗn hợp `both`**:
   - Dành cho các thiết bị vừa có quyền phát tin, vừa có quyền nhận tin. Ví dụ: Máy POS thu ngân, hoặc ứng dụng điện thoại của Nhân viên giao hàng (Shipper vừa bấm cập nhật đơn hàng, vừa nhận thông báo có đơn mới cần giao).
3. **Nguyên tắc bảo mật tối thiểu (Principle of Least Privilege)**:
   - Nếu không phân role, một thiết bị di động của người dùng cuối có thể bị hacker dịch ngược (reverse-engineer) để lấy API Key và lợi dụng hệ thống làm công cụ spam email / SMS hàng loạt.

---

## 3. Ứng Dụng Thực Tế Của "Mobile Push Endpoint" & "Webhook Callback (HMAC)"

### 3.1. Mobile Push Endpoint (FCM / APNs) dùng để làm gì?
* **Mục đích thực tế**: Đẩy thông báo tức thì hiển thị trên màn hình khóa điện thoại (Push Notification) của người dùng kể cả khi họ đang tắt ứng dụng.
* **Quy trình hoạt động**:
  1. Khi người dùng mở App, Apple cấp mã định danh tạm thời gọi là `deviceToken` (APNs) hoặc Google cấp `registrationToken` (FCM).
  2. Ứng dụng gửi token này lên `notification-server` lưu vào bảng `device_push_endpoints` dưới dạng **mã hoá bảo mật `AES-256-GCM`**.
  3. Khi cần gửi thông báo đẩy (ví dụ: *"Tài khoản của bạn vừa được cộng 500.000đ"*), hệ thống chỉ cần gọi gửi tin tới `target: "<deviceId>"`. Worker sẽ tự giải mã token và gọi Apple/Google để làm rung chuông điện thoại của người dùng.

### 3.2. Webhook Callback (HMAC) dùng để làm gì?
* **Mục đích thực tế**: Báo cáo kết quả gửi tin về cho hệ thống nguồn theo cơ chế Bất đồng bộ (Async).
* **Bài toán thực tế**: 
  - Khi website của bạn gửi 1.000 email vé máy bay cho khách hàng, website không thể đứng chờ 1.000 email này gửi xong (sẽ bị đơ web). Thay vào đó, website gọi sang `notification-server` và nhận ngay mã `202 Accepted` trong 30ms.
  - Khi Worker chạy ngầm gửi xong email (sau 3 - 5 giây), nó sẽ tự động gửi một gói tin HTTP POST ngược lại URL của website bạn (Webhook) kèm nội dung:
    ```json
    {
      "eventId": "evt_123",
      "type": "notification.completed",
      "notificationId": "notif_456",
      "status": "delivered",
      "finishedAt": "2026-09-04T10:00:00Z"
    }
    ```
* **Chữ ký HMAC-SHA256 để làm gì?**:
  - Gói tin callback được gửi kèm Header: `X-Signature-SHA256: 7f8a9b...`.
  - Website của bạn dùng Secret đã lưu để băm thử nội dung gói tin. Nếu trùng khớp, website của bạn chắc chắn 100% gói tin này là do `notification-server` gửi đến, không sợ bị hacker gửi kết quả giả mạo.

---

## 4. Cơ Chế Xác Định Device ID Trên Mobile Trong Thực Tế

Nhiều người thắc mắc: *"Khi các thiết bị đăng nhập trên mobile thì làm sao xác định được ID của thiết bị đó?"*

### Quy trình chuẩn trong một ứng dụng Mobile thực tế:
1. **Sinh ID thiết bị duy nhất trên máy**:
   - Khi ứng dụng di động (React Native, Flutter, Swift, Kotlin) được cài đặt và mở lần đầu tiên, ứng dụng sẽ gọi hàm hệ thống lấy định danh phần cứng an toàn:
     - iOS: `identifierForVendor` (lưu vào iOS Keychain để không bị mất khi xoá app cài lại).
     - Android: `Settings.Secure.ANDROID_ID` hoặc sinh 1 chuỗi UUID lưu vào `EncryptedSharedPreferences`.
2. **Đăng ký thiết bị với Notification Server**:
   - Sau khi người dùng đăng nhập tài khoản thành công, ứng dụng gửi request:
     ```http
     POST /v1/devices
     Authorization: Bearer <user-jwt>
     Content-Type: application/json

     {
       "name": "iPhone 15 Pro Max của Nguyễn Văn A",
       "role": "recipient"
     }
     ```
   - Server sinh ra một mã UUID cố định (ví dụ: `9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d`) và trả về cho App.
   - App lưu mã này vào bộ nhớ máy làm `deviceId`.
3. **Liên kết Push Token**:
   - App lấy FCM / APNs token từ hệ điều hành và gửi lên:
     ```http
     POST /v1/devices/9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d/push-endpoint
     {
       "platform": "fcm",
       "token": "fcm_token_day_du_tu_google..."
     }
     ```
4. **Kể từ đó**: Khi backend muốn gửi tin cho Nguyễn Văn A, backend chỉ cần chỉ định: `target: "9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d"`.

---

## 5. Hướng Dẫn Kiểm Thử (Test) Các Chức Năng Này Mà Không Cần Viết App Mobile

Bạn **hoàn toàn kiểm thử được 100%** các tính năng này ngay trên giao diện Web Admin hoặc Postman/PowerShell mà không cần phải cài app di động thật:

### 5.1. Kiểm thử Webhook Callback (HMAC) bằng Webhook.site
1. Truy cập trang web miễn phí: **[https://webhook.site](https://webhook.site)**.
2. Trang web sẽ cấp cho bạn một đường dẫn URL tạm thời dạng: `https://webhook.site/xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`.
3. Mở Web Admin Console của bạn:
   - Vào **Thiết bị & Keys** (`/devices`) → Bấm vào thiết bị của bạn.
   - Tại mục **Callback webhook**, dán URL của `webhook.site` vào.
   - Bấm **Cấu hình Callback**. Hệ thống sinh ra mã `HMAC Secret`.
4. Bắn một thông báo bất kỳ sử dụng API Key của thiết bị đó.
5. Quay lại trang `webhook.site`:
   - Bạn sẽ thấy ngay một HTTP POST request vừa nổ về từ notification-server.
   - Kiểm tra Header: Có `X-Signature-SHA256` và `X-Event-ID`.
   - Kiểm tra Body: Chứa đầy đủ trạng thái `notification.completed`, `delivered`!

### 5.2. Kiểm thử Mobile Push Endpoint
1. Vào **Thiết bị & Keys** (`/devices`) → Bấm **Thêm thiết bị** với Role là `recipient` (hoặc `both`).
2. Mở chi tiết thiết bị, tại mục **Cấu hình Push Notification (FCM / APNs)**:
   - Chọn Platform: `fcm`.
   - Nhập một Mock Push Token thử nghiệm: `fcm_test_token_abc123xyz`.
   - Bấm **Đăng ký Push Token**.
3. **Xác nhận**:
   - Giao diện báo thành công, trạng thái hiển thị `Đang hoạt động`.
   - Token thực tế được mã hóa AES-256-GCM trong database và không bao giờ bị lộ ra ngoài màn hình.
   - Bấm **Hủy Push Token** để kiểm tra tính năng xóa/thu hồi endpoint.
