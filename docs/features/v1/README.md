# Danh Mục Feature — v1 (Đã Chuẩn Hóa & Hợp Nhất Module)

Hệ thống được tổ chức thành các Module tài nguyên hoàn chỉnh (Resource-Oriented Modules), tích hợp đầy đủ năng lực **CRUD (Create - Read - Update - Delete)** và các thao tác nghiệp vụ đặc thù.

---

## 1. Cấu Trúc Module Chuẩn Hóa

| Module | Tên Phân Hệ | Trách Nhiệm Nghiệp Vụ & Năng Lực CRUD | Mã Feature Chính |
| :--- | :--- | :--- | :--- |
| `01-foundation/` | **Foundation** | Bootstrap, Liveness/Readiness health check, correlation ID, metrics | `OPS-001` |
| `02-identity/` | **Identity & Access** | Đăng ký tổ chức, Session JWT/Refresh Token, Quản lý thành viên (Member/Owner), Cô lập Tenant | `AUTH-001` |
| `03-sender/` | **Sender Management** | CRUD máy chủ SMTP/Resend, Mật khẩu mã hóa AES-256, Đặt mặc định, Gửi thư thử nghiệm | `SEND-001` |
| `04-template/` | **Template Management** | CRUD mẫu thông báo (HTML & Text), XSS escaping, Scope hierarchy, Vòng đời phiên bản bất biến (v1, v2) | `TMPL-001` |
| `05-intake/` | **Notification Intake** | Tiếp nhận thông báo bất đồng bộ qua API Key hoặc Admin Web, Validation, Template snapshot | `INTK-001` |
| `06-delivery/` | **Delivery Engine** | Background Worker, Gửi đa kênh, Phân loại lỗi (Transient/Permanent), Retry backoff, Cứu job kẹt | `DLVR-001` |
| `07-history/` | **History & Operations** | Tra cứu thông báo, Lọc phân trang cursor, Thao tác Hủy (`Cancel`) và Gửi lại thủ công (`Retry`) | `HIST-001` |
| `08-devices/` | **Device & Endpoints** | Quản lý thiết bị (`source`/`recipient`/`both`), Khóa API, Push Endpoint (FCM/APNs), Webhook Callback (HMAC) | `DEVICE-001` |
| `09-channels/` | **Multi-Channels** | Trình gửi đa kênh: Email (SMTP/Resend), Telegram Bot, Discord Webhook, Mobile Push | `CHAN-001` |
| `11-admin-web/` | **Admin Web Console** | Giao diện React SPA điều hành toàn diện toàn bộ các chức năng CRUD và kiểm thử | `WEB-001` |

---

## 2. Bảng Danh Mục Feature Chính Thức

| ID | Tên Feature | Trạng Thái | File Đặc Tả Chi Tiết | Năng Lực Bao Gồm (Đã Hợp Nhất) |
| :--- | :--- | :--- | :--- | :--- |
| **OPS-001** | Vận hành & Giám sát | Verified | [spec](01-foundation/OPS-001-van-hanh.md) | Health check, Log, Metrics, Readiness |
| **AUTH-001** | Định danh, Tài khoản & Phân quyền | Verified | [spec](02-identity/AUTH-001-dang-ky-to-chuc.md) | Tenant Registration, Login, Token Rotation, User CRUD, Tenant Isolation *(Subsumes: AUTH-002, AUTH-003, AUTH-004)* |
| **SEND-001** | Quản trị Máy chủ Gửi thư & Test | Verified | [spec](03-sender/SEND-001-cau-hinh-sender.md) | SMTP CRUD, Default Sender, Test SMTP, Resend HTTPS Bypass *(Subsumes: SEND-002, SEND-003)* |
| **TMPL-001** | Quản trị Mẫu & Phiên bản Đa định dạng | Verified | [spec](04-template/TMPL-001-mau-noi-dung.md) | Template CRUD, HTML/Text, Publish, Immutable Versioning *(Subsumes: TMPL-002)* |
| **INTK-001** | Tiếp nhận Thông báo | Verified | [spec](05-intake/INTK-001-tiep-nhan.md) | Tiếp nhận Direct & Template, Multi-channel contract |
| **DLVR-001** | Delivery Worker & Khả năng Phục hồi | Verified | [spec](06-delivery/DLVR-001-gui-bat-dong-bo.md) | Background Dispatch, Retry transient, Stuck recovery, Incident alert |
| **HIST-001** | Lịch sử, Tra cứu & Vận hành Thông báo | Verified | [spec](07-history/HIST-001-tra-cuu-thong-bao.md) | Danh sách lọc cursor, Tra cứu Attempts, Hủy tin (`Cancel`), Gửi lại (`Retry`) *(Subsumes: HIST-002, HIST-003)* |
| **DEVICE-001**| Thiết bị, Khóa API, Push & Webhook | Verified | [spec](08-devices/DEVICE-001-thiet-bi-va-api-key.md) | Device CRUD, Role separation, API Key rotate, Push FCM/APNs, Webhook HMAC *(Subsumes: DEVICE-002, CBACK-001)* |
| **CHAN-001** | Mô hình Delivery Đa kênh | Verified | [spec](09-channels/CHAN-001-mo-hinh-delivery-da-kenh.md) | Dispatch Email, Telegram, Discord, Push |
| **WEB-001** | React Operations Console | Verified | [spec](11-admin-web/WEB-001-react-admin-console.md) | Giao diện quản trị SPA Vite/React điều hành các phân hệ |

---

## 3. Luồng Nghiệp Vụ Hoàn Chỉnh Đầu-Cuối

```text
1. Khởi tạo tổ chức & Xác thực:
   OPS-001 (Ready) → AUTH-001 (Đăng ký Tenant & Đăng nhập Owner)

2. Chuẩn bị Hạ tầng:
   SEND-001 (Thêm & Test SMTP/Resend) → DEVICE-001 (Tạo Source Device & Sinh API Key)

3. Soạn thảo Mẫu & Gửi tin:
   TMPL-001 (Tạo & Publish Template) → INTK-001 (Bắn thông báo qua API Key)

4. Xử lý Nền & Giám sát:
   DLVR-001 (Worker gửi qua CHAN-001) → DEVICE-001 (Đẩy Callback Webhook về nguồn)
   ↓
   HIST-001 (Giám sát trạng thái 'delivered', Thao tác Hủy hoặc Gửi lại khi lỗi)
```
