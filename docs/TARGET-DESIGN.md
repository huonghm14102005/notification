# Thiết kế đích — notification service cho user và thiết bị

Đây là nguồn sự thật cho hệ thống đích. `SPECS.md` và các feature `Verified` vẫn mô tả code hiện tại;
thiết kế này chỉ có hiệu lực qua feature/migration được duyệt.

## 1. Luồng sản phẩm

```text
User đăng ký email/password; email đầy đủ là định danh đăng nhập
  → user đăng nhập và đăng ký một hoặc nhiều device
  → mỗi device được cấp credential riêng nếu có quyền gửi
  → device chọn một hoặc nhiều kênh
  → gửi plaintext hoặc template cùng target theo kênh
  → server lưu notification và tạo delivery độc lập
  → lần gửi đầu + tối đa 3 retry cho delivery thất bại tạm thời
  → khi hoàn tất, server callback kết quả thành công hoặc thất bại về device/hệ thống nguồn
```

Phiên bản đầu chỉ thực thi email SMTP/Gmail. SMS, Discord, webhook delivery và mobile push được thêm
sau bằng adapter, không thay đổi domain lõi.

## 2. User và device

Mô hình sở hữu là `tenant → users → devices`: một tenant có nhiều user, một user quản lý nhiều
device và mỗi device thuộc đúng một user trong tenant. Device có một trong ba vai trò:

| Role | Ví dụ | Credential/dữ liệu |
|---|---|---|
| `source` | DRL server, cảm biến, IoT gateway | API key để gọi notification API; callback URL/secret |
| `recipient` | Điện thoại, trình duyệt, desktop | FCM/APNs/Web Push endpoint để nhận push |
| `both` | Thiết bị vừa phát sự kiện vừa nhận cảnh báo | Có cả API key và push endpoint |

`device_id` là định danh công khai, ổn định. API key là bí mật xác thực. Push token là địa chỉ do
push provider cấp, không phải API key. Firebase/APNs là dịch vụ chuyển tiếp, không phải database thứ
hai; một PostgreSQL lưu toàn bộ user, device, credential, endpoint và lịch sử.

Giai đoạn đầu, user đã đăng nhập tự tạo/quản lý device của mình; tenant owner được xem, disable và
thu hồi device của mọi user trong tenant. Device không được tự đăng ký ẩn danh. Với server/IoT không
có màn hình, user/owner tạo device qua API, nhận API key đúng một lần rồi cài key vào thiết bị. Pairing
code tự động nằm ngoài DEVICE-001 và được dành cho feature sau.

### Định danh đăng nhập

- Email đầy đủ sau khi trim và normalize lowercase là định danh đăng nhập duy nhất.
- Phần trước ký tự `@` được dùng làm `displayName` mặc định, ví dụ `an@gmail.com` → `an`.
- `displayName` chỉ để hiển thị, không dùng xác thực và không bắt buộc duy nhất.
- Không có trường username riêng trong contract đăng ký giai đoạn đầu.

## 3. Bảo mật

- Password lưu bằng password hash; không mã hóa có thể giải ngược.
- Raw API key chỉ trả một lần; DB lưu prefix/hash. Một device có thể có nhiều key để xoay khóa.
- JWT của user dùng quản trị; API key của device dùng gửi tự động.
- Principal từ API key chứa `user_id`, `device_id`, `api_key_id`; không tin các ID này trong body.
- Push token và callback secret được mã hóa khi lưu; không xuất hiện trong log/history response.
- Thu hồi một key hoặc một device không làm mất notification/lịch sử cũ.
- Callback URL cấu hình trên device nguồn, không nhận tùy ý trong notification để giảm SSRF.

## 4. Notification, channel, target

Một notification chọn một hoặc nhiều channel. Mỗi cặp channel-target tạo delivery độc lập:

```text
Notification N1
  ├─ Delivery email   → student@example.com
  ├─ Delivery sms     → +84901234567
  ├─ Delivery discord → webhook/channel
  └─ Delivery push    → recipient device/push endpoint
```

Target theo kênh: email address, phone number, Discord destination, webhook URL hoặc recipient
device. Không coi `target` là một content mode.

Content có hai mode:

- `plaintext`: request gửi subject/body trực tiếp.
- `template`: request gửi `templateKey` và variables; server lưu snapshot nội dung đã render.

## 5. Contract intake đích

```http
POST /v1/notifications
X-API-Key: notify_device_...
Idempotency-Key: drl-request-2026-0001
```

```json
{
  "externalId": "DRL-REQ-2026-0001",
  "channels": [
    { "type": "email", "targets": ["student@example.com"] }
  ],
  "content": {
    "mode": "plaintext",
    "subject": "Cập nhật điểm rèn luyện",
    "body": "Điểm của bạn vừa được cập nhật"
  }
}
```

Template thay `subject/body` bằng `templateKey/variables`. Trong giai đoạn email-only, kênh khác trả
`422 CHANNEL_NOT_SUPPORTED` và không tạo dữ liệu nửa vời. `202` chỉ xác nhận notification và các
delivery đã được commit vào PostgreSQL.

`Idempotency-Key` có thể tùy chọn khi local test, nhưng bắt buộc ở production. Cùng device và cùng
key trả notification cũ, không gửi lặp.

## 6. Delivery và retry

```text
pending → processing → delivered
                 └──→ retry_scheduled → processing
                 └──→ failed
```

- Tổng tối đa 4 attempt: một lần đầu và ba retry.
- Lỗi permanent kết thúc ngay; lỗi transient dùng backoff rồi retry.
- Mỗi delivery độc lập: email thành công không bị gửi lại khi SMS thất bại.
- Notification tổng hợp thành `accepted`, `processing`, `delivered`, `partially_delivered`, `failed`
  hoặc `cancelled`.
- Email `delivered` chỉ có nghĩa SMTP/provider chấp nhận, không có nghĩa người nhận đã đọc.

## 7. Callback về nguồn

Giai đoạn đầu callback đúng một loại kết quả cuối cùng: `notification.completed`, cho cả thành công
và thất bại. Payload mang trạng thái tổng hợp `delivered`, `partially_delivered`, `failed` hoặc
`cancelled` cùng kết quả từng delivery. Khi cần quan sát chi tiết có thể bổ sung
`notification.accepted` và event theo delivery mà không đổi cơ chế ký.

```http
POST {device.callback_url}
X-NTS-Event-Id: evt_...
X-NTS-Timestamp: 1787202600
X-NTS-Signature: v1=<hmac-sha256(timestamp.rawBody)>
```

Callback là at-least-once; nguồn deduplicate bằng `eventId`. Callback retry độc lập, không thay đổi
kết quả delivery. API tra cứu vẫn là đường đối soát dự phòng.

## 8. Schema đích

| Bảng | Trách nhiệm |
|---|---|
| `users` | email unique, display name mặc định từ phần trước `@`, password hash, trạng thái |
| `devices` | owner user, public device key, type/role, trạng thái, callback config |
| `device_api_keys` | credential hash/prefix để device gửi request |
| `push_endpoints` | provider/platform và push token mã hóa để device nhận push |
| `channel_configurations` | SMTP trước; provider config cho các kênh sau |
| `templates` | template và variables |
| `notifications` | device nguồn, external/idempotency key, content snapshot, trạng thái tổng hợp |
| `deliveries` | notification, channel, target snapshot, trạng thái và lịch retry |
| `delivery_attempts` | lịch sử bất biến của từng lần gọi provider |
| `status_events` | payload kết quả cần callback |
| `callback_attempts` | lịch sử bất biến của callback |

## 9. Tương thích và thứ tự phát triển

Code hiện tại dùng tenant/admin và API key mang `producer_name`. Migration DEVICE-001 sẽ:

1. Giữ tenant/admin hiện tại; coi admin account là user quản trị trong tenant.
2. Backfill mỗi `(tenant, producer_name)` thành một device role `source`.
3. Gắn API key hiện có vào device, không làm key cũ ngừng hoạt động.
4. Backfill notification email cũ thành một delivery email khi CHAN-001 được triển khai.

Thứ tự ưu tiên:

```text
DEVICE-001 → DLVR-002 → CBACK-001 → CHAN-001 → INTK-003
           → DEVICE-002 (push endpoint) → CHAN-004 (mobile push)
```

Discord/webhook và SMS chỉ bắt đầu sau khi email, retry và callback đã Verified.
