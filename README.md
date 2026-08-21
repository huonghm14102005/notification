# notification-server (notify-api)

Dịch vụ thông báo độc lập, đa tổ chức. Các ứng dụng gửi cho nó một thông điệp cần đến tay một người;
dịch vụ chuyển thông điệp đó tới tài khoản gửi mà tổ chức đã cấu hình. Việc gửi diễn ra bất đồng bộ,
có thử lại và có thể truy vết.

Phiên bản đầu chỉ hỗ trợ kênh email.

## Trạng thái

Đang phát triển theo feature; OPS-001, module Identity (AUTH-001..003), module Sender (SEND-001..003), TMPL-001, INTK-001, DLVR-001 và HIST-001 đã Verified.

## Chạy local

```powershell
docker compose -f deploy/docker/compose.yml up --build --wait
```

### Demo luồng trung chuyển hoàn chỉnh

Sau khi clone repository, máy chỉ cần Docker Desktop và PowerShell. Từ thư mục gốc repository chạy:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\demo-notification-flow.ps1
```

Script tự động:

1. Build và khởi động PostgreSQL, Redis, GreenMail, API và Worker bằng Docker Compose.
2. Đăng nhập tài khoản local seed.
3. Tạo API key mô phỏng một hệ thống nguồn và sender SMTP GreenMail tạm.
4. Gọi `POST /v1/notifications` với một người nhận.
5. Chờ Worker polling PostgreSQL và gửi SMTP.
6. Xác nhận notification là `sent` và delivery attempt là `success`.

Kết quả thành công có dạng:

```text
[6/6] Demo passed.
status          : sent
deliveryAttempt : success|1|
```

Có thể đổi người nhận hoặc thời gian chờ:

```powershell
.\scripts\demo-notification-flow.ps1 -RecipientEmail "recipient@local.test" -TimeoutSeconds 45
```

Đây là test local: email được GreenMail nhận bên trong Docker, không gửi ra Internet. Container được giữ lại để xem
log; dừng mà vẫn giữ dữ liệu bằng `docker compose -f deploy/docker/compose.yml down`.

Compose chạy migration trước API/Worker và tạo tài khoản thử nghiệm idempotent:

| Trường | Giá trị |
|---|---|
| URL API | `http://localhost:3100` |
| Tenant | `Test Organization` (`test-organization`) |
| Email | `admin@local.test` |
| Mật khẩu | `12345678` |

Tài khoản trên chỉ dành cho local/test. Seed bị chặn ở Production kể cả khi
`SEED_TEST_ADMIN=true`; không dùng credential này cho môi trường thật.

Đăng nhập qua `POST /v1/auth/login`; dùng `POST /v1/auth/refresh` để rotate refresh token và
`POST /v1/auth/logout` kèm Bearer access token để thu hồi phiên. Refresh token chỉ dùng được một lần.

Admin dùng JWT để cấp, liệt kê và thu hồi khóa máy qua `POST/GET/DELETE /v1/api-keys`. Khóa thô
`notify_<64-hex>` chỉ xuất hiện trong response tạo khóa; hãy lưu ngay vì dịch vụ không thể khôi phục lại.


Admin cấu hình tài khoản SMTP qua `POST/GET/PATCH/DELETE /v1/senders`. Mật khẩu SMTP được mã hóa

AES-256-GCM bằng `ENCRYPTION_KEY` (base64 của đúng 32 byte), không được trả lại qua API. Giá trị mặc định
trong Compose chỉ dành cho local/test; production phải cung cấp khóa riêng qua secret manager.

Trường `isDefault` trong PATCH chọn hoặc gỡ tài khoản mặc định; khi tiếp nhận

thông báo, `senderKey` sẽ chọn
tài khoản active tương ứng và nếu bỏ trống thì dùng tài khoản mặc định.
Admin dùng `POST /v1/senders/{id}/test` với `recipientEmail` để gửi thư kiểm tra đồng bộ. Kết nối luôn dùng
implicit TLS hoặc STARTTLS bắt buộc; thành công cập nhật `verifiedAt`. Timeout cấu hình bằng `SMTP_TIMEOUT_MS`.

Admin quản lý mẫu plain-text theo tenant qua `POST/GET /v1/templates` và `GET/PATCH /v1/templates/{key}`. Mẫu đi theo
vòng đời `draft → active → retired`; key không đổi và không được tái sử dụng. Placeholder có dạng `{{variableName}}`,
được kiểm tra khớp chính xác với danh sách `variables` và render một lần để dữ liệu biến không bị diễn giải lại.

Hệ thống nguồn dùng API key gọi `POST /v1/notifications` để tiếp nhận một email inline. API chọn sender active, mã hóa
subject/body và lưu notification `accepted` trong PostgreSQL trước khi trả `202`. Luồng cơ bản không dùng Redis queue
hoặc batch. Worker polling PostgreSQL, claim notification tới hạn, gửi SMTP plain-text rồi lưu trạng thái `sent` hoặc
`failed` cùng delivery attempt. Phiên bản hiện tại gửi một lần; retry được giữ cho DLVR-002.

Admin hoặc hệ thống nguồn tra cứu kết quả bằng `GET /v1/notifications/{id}`. Admin trong cùng tenant thấy nội dung đã
giải mã và recipient ref; API key chỉ thấy metadata của notification do chính key đó tạo, không thấy subject/body/ref.

## Phạm vi đang ưu tiên và tạm hoãn

Ưu tiên hiện tại là hoàn thành đường gửi thật để tích hợp thử với hệ thống ĐRL: `INTK-001 → DLVR-001 → HIST-001`.
Các phần sau vẫn nằm trong roadmap nhưng tạm hoãn, chưa được bỏ khỏi sản phẩm:

- `INTK-003`: tiếp nhận notification theo template; hiện hệ thống ĐRL gửi trực tiếp `subject` và `body`.
- `INTK-004`: rate limit riêng cho intake; chỉ mở tích hợp thử bằng API key được kiểm soát, chưa mở tải công khai.
- `INTK-002`: nhiều người nhận/batch; hiện mỗi request có đúng một recipient.
- `DLVR-002..004`: retry nâng cao, khôi phục notification kẹt và cảnh báo tổng hợp.
- `HIST-002..003`: danh sách, hủy và gửi lại thủ công.

Trước khi mở rộng lưu lượng hoặc đưa vào production phải review lại danh sách này, tối thiểu hoàn thành rate limit,
retry/recovery và API tra cứu kết quả.

## Tài liệu

Đọc từ [mục lục tài liệu](docs/README.md). Bộ tài liệu phân biệt rõ contract đang chạy, thiết kế đích,
kiến trúc, roadmap, workflow và spec của từng feature.

## Quyết định đã chốt

- Dịch vụ độc lập, không phải một module của dịch vụ CDN/Media hiện có.
- Làm mới hoàn toàn: cơ sở dữ liệu riêng, cơ chế định danh và khoá riêng; không dùng lại tenant,
  người dùng hay API key của dịch vụ CDN.
- Tenant là tổ chức sở hữu. Mỗi hệ thống nguồn có `source_id` công khai, ổn định và một hoặc nhiều
  API key bí mật để xoay/thu hồi độc lập; server suy ra source từ API key.
- Ứng dụng gửi tự cung cấp tiêu đề và nội dung; template là tuỳ chọn.
- Thiết kế đích cho phép một notification chọn nhiều kênh và tạo delivery độc lập; bản thử nghiệm
  hiện chỉ thực thi email qua SMTP/Gmail.
- Server chủ động callback trạng thái có chữ ký về source; API tra cứu vẫn là đường đối soát.
- Nền tảng đích là ASP.NET Core API + .NET Worker Service, PostgreSQL và Redis, đóng gói bằng Docker.
