# notification-server

Dịch vụ notification đa tenant viết bằng .NET, PostgreSQL và Docker. Hệ thống nguồn gửi một yêu cầu rồi tiếp tục
công việc của nó; API lưu yêu cầu, Worker chuyển tiếp bất đồng bộ, retry lỗi tạm thời và callback kết quả về nguồn.

Phiên bản hiện tại chỉ gửi email qua SMTP. Nền tảng delivery đã tách theo kênh để bổ sung push, webhook, Discord và
SMS sau này mà không phải viết lại notification core.

## Cách hệ thống hoạt động

```text
Admin đăng nhập
  ├─ tạo source device và API key
  ├─ cấu hình SMTP sender
  ├─ cấu hình callback có HMAC
  └─ quản lý template theo tenant/source và version

Source device dùng API key
  → POST /v1/notifications
  → API kiểm tra tenant, sender và nội dung
  → PostgreSQL lưu Notification + Delivery(pending)
  → trả 202 Accepted

Worker
  → claim Delivery bằng transaction/locking
  → gửi SMTP
  → thành công: delivered
  → lỗi tạm thời: retry tối đa 3 lần sau lần đầu
  → lỗi vĩnh viễn hoặc hết retry: failed
  → cập nhật trạng thái tổng hợp của Notification
  → gửi callback notification.completed về source device
```

Mỗi delivery xử lý độc lập. Một kênh đã thành công không bị gửi lại khi kênh khác thất bại. PostgreSQL là nguồn dữ
liệu chính; Redis không phải nơi duy nhất giữ trạng thái notification.

Hệ thống có semantics at-least-once: callback có thể đến lặp và trong một số tình huống worker chết đúng thời điểm,
provider có thể nhận lại cùng message. Consumer callback phải chống trùng bằng `eventId`.

## Thành phần

| Thành phần | Trách nhiệm |
|---|---|
| `Notification.Api` | Auth, quản trị device/key/sender/template, tiếp nhận và tra cứu notification |
| `Notification.Worker` | Claim delivery, gửi SMTP, retry, recovery job kẹt và gửi callback |
| PostgreSQL 16 | Lưu tenant, user, device, credential metadata, notification, delivery và lịch sử attempt |
| Redis 7 | Health/rate-limit và năng lực hỗ trợ; delivery core vẫn dựa vào PostgreSQL |
| GreenMail | SMTP fixture local; không gửi email ra Internet |
| callback receiver | Fixture local để kiểm tra callback và chữ ký HMAC |

API và Worker được build từ cùng một image/version nhưng có thể scale độc lập khi triển khai thật.

## Yêu cầu chạy local

- Docker Desktop có Docker Compose.
- PowerShell 7 hoặc Windows PowerShell để chạy script kiểm thử/demo.
- .NET SDK 10 nếu muốn build và test trực tiếp ngoài Docker.

## Khởi động bằng Docker

Từ thư mục gốc repository:

```powershell
docker compose -f deploy/docker/compose.yml up --build --detach --wait
```

Compose khởi động PostgreSQL, Redis, GreenMail, callback receiver, chạy migration rồi mới mở API và Worker.

| Dịch vụ local | Địa chỉ |
|---|---|
| API | `http://localhost:3100` |
| Liveness | `http://localhost:3100/health/live` |
| Readiness | `http://localhost:3100/health` |
| Callback fixture | `http://localhost:3101` |

Tài khoản seed chỉ dùng local/test:

| Trường | Giá trị |
|---|---|
| Email | `admin@local.test` |
| Password | `12345678` |
| Tenant | `Test Organization` |

Seed bị chặn trong Production. Không sao chép credential hoặc secret mặc định của Compose sang môi trường thật.

Xem log:

```powershell
docker compose -f deploy/docker/compose.yml logs -f api worker
```

Dừng và giữ volume dữ liệu:

```powershell
docker compose -f deploy/docker/compose.yml down
```

Dừng và xoá dữ liệu local:

```powershell
docker compose -f deploy/docker/compose.yml down --volumes
```

Lệnh cuối xoá PostgreSQL volume của Compose và chỉ phù hợp với dữ liệu local có thể tạo lại.

## Chạy demo đầu-cuối

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\demo-notification-flow.ps1
```

Demo reset project/volume dùng riêng tên `notification-demo`, sau đó khởi động Compose, đăng nhập admin local, tạo API
key và SMTP sender, gửi một notification, chờ Worker rồi xác nhận delivery `success|1|`. Nó không xoá volume của môi
trường Compose mặc định. Email được GreenMail nhận bên trong Docker, không gửi ra Internet.

Có thể đổi địa chỉ nhận giả và timeout:

```powershell
.\scripts\demo-notification-flow.ps1 -RecipientEmail "recipient@local.test" -TimeoutSeconds 45
```

## Build và kiểm thử

Các lệnh dưới đây phải pass trước khi push:

```powershell
dotnet restore Notification.slnx
dotnet format Notification.slnx --verify-no-changes --no-restore
dotnet build Notification.slnx -c Release --no-restore
dotnet test Notification.slnx -c Release --no-build
powershell -ExecutionPolicy Bypass -File .\scripts\test-integration.ps1
```

Integration script build image mới, chạy API/Worker với PostgreSQL/Redis/GreenMail thật, kiểm tra retry, recovery,
callback và migration down/up, sau đó tự dọn container/volume test.

Nếu chỉ muốn chạy lại integration bằng image local vừa build:

```powershell
.\scripts\test-integration.ps1 -SkipBuild
```

CI dùng đúng chuỗi lệnh trong [`.github/workflows/ci.yml`](.github/workflows/ci.yml).

## Chức năng hiện có

- Đăng ký tenant, đăng nhập/refresh/logout và cô lập dữ liệu theo tenant.
- User/admin quản lý nhiều source device và xoay nhiều API key trên từng device.
- Cấu hình/test SMTP sender; secret được mã hoá và không trả lại qua API.
- Tiếp nhận email bất đồng bộ, delivery attempt, retry tối đa ba lần và recovery job kẹt.
- Callback `notification.completed` có HMAC, retry độc lập và `eventId` chống xử lý trùng.
- Delivery entity đa kênh và trạng thái tổng hợp `delivered`, `partially_delivered`, `failed`.
- Template theo tenant/source, audience, text/HTML, version bất biến và HTML escaping.
- Tra cứu notification và lịch sử attempt theo quyền admin/source.

Đã hỗ trợ gửi notification bằng template (`INTK-003`). Chưa có trong production flow: rate limit intake (`INTK-004`),
batch nhiều người nhận, push mobile, webhook/Discord/SMS, cảnh báo sự cố tổng hợp và hardening production đầy đủ.

## Lưu ý trước production

Local hiện cho phép reset dữ liệu để chỉnh schema. Trước staging phải tạo baseline migration sạch và kiểm thử nâng cấp
trên dữ liệu gần thực tế. Secret phải chuyển sang secret manager; SMTP thử nghiệm phải thay bằng provider transactional
email và cấu hình SPF/DKIM/DMARC.

INTK-004 được hoãn khi chạy local, nhưng bắt buộc phải triển khai và load-test trước khi mở intake ra Internet hoặc
đưa hệ thống lên staging/production.

Checklist đầy đủ nằm tại [Production readiness](docs/PRODUCTION-READINESS.md).

## Tài liệu

| Tài liệu | Nội dung |
|---|---|
| [Mục lục](docs/README.md) | Điểm bắt đầu của bộ tài liệu |
| [Product](docs/PRODUCT.md) | Mục tiêu, người dùng và phạm vi sản phẩm |
| [Architecture](docs/ARCHITECTURE.md) | Ranh giới API/Application/Domain/Infrastructure/Worker |
| [Current specs](docs/SPECS.md) | Contract và schema đang triển khai |
| [Roadmap](docs/IMPLEMENTATION-ROADMAP.md) | Thứ tự feature tiếp theo |
| [Feature catalog](docs/features/v1/README.md) | Trạng thái và acceptance criteria từng feature |
| [Workflow](docs/WORKFLOW.md) | Quy trình SELECT/APPROVE/VERIFY |
