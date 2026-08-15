# SEND-003 — Gửi thư thử từ một tài khoản gửi

Status: Verified
Selected: 2026-08-15
Approved: 2026-08-15
Verified: 2026-08-15

## Outcome

Admin gửi được một email kiểm tra bằng chính cấu hình SMTP đã lưu. Khi SMTP chấp nhận thư và cấu hình chưa thay đổi
trong lúc gửi, hệ thống ghi `verified_at` để admin biết cấu hình kết nối/xác thực đã hoạt động.

## Actor

Quản trị viên `owner` đã xác thực bằng JWT.

## Trigger

Admin gọi `POST /v1/senders/{id}/test` với địa chỉ nhận thư thử.

## In scope

- Gửi đồng bộ đúng một email plain-text tới địa chỉ admin cung cấp.
- Giải mã SMTP password chỉ trong Infrastructure, sát thời điểm xác thực SMTP.
- Bắt buộc TLS, giới hạn thời gian toàn bộ thao tác và phân loại lỗi an toàn.
- Chỉ cập nhật `verified_at` khi SMTP đã chấp nhận thư và cấu hình vẫn là phiên bản vừa kiểm tra.
- Adapter `IEmailSender` dùng lại được bởi Delivery nhưng chưa triển khai retry/job.
- Docker Compose integration test với SMTP fixture cục bộ; không cần tài khoản Gmail thật trong CI.

## Out of scope

- Queue, retry tự động, delivery attempt hoặc notification history.
- HTML, attachment, CC/BCC, subject/body do caller tùy biến.
- OAuth2, Gmail API, SES/SendGrid API.
- Kiểm tra inbox, open/bounce/delivery sau khi SMTP đã nhận thư.
- Cho phép bỏ qua certificate validation ở production.

## Preconditions

- PRE-01: sender tồn tại trong tenant của admin và có `status=active`.
- PRE-02: `ENCRYPTION_KEY` hợp lệ và ciphertext giải mã đúng AAD tenant/sender.
- PRE-03: deployment cho phép egress tới SMTP host/port đã cấu hình.

## Dependencies

SEND-001. SEND-002 không bắt buộc vì endpoint chọn sender trực tiếp bằng ID.

## Tham chiếu

- Must-have: M-05 ([MVP.md](../../../MVP.md)).
- Dữ liệu: `senders.verified_at` — SPECS.md §6.
- Contract: `POST /v1/senders/:id/test` — SPECS.md §7.
- Adapter boundary: ARCHITECTURE.md D7–D8, CONVENTIONS.md §10 và §14.

## Business rules

- BR-01: request chỉ nhận `recipientEmail`; email được trim/lowercase, tối đa 254 ký tự và phải hợp lệ. Field lạ,
  null hoặc body rỗng trả `400 VALIDATION_FAILED`.
- BR-02: mỗi request gửi đúng một email plain-text. Subject cố định `[notification-server] SMTP test: {senderKey}`;
  body cố định, có sender key và thời điểm UTC nhưng không có credential, token hoặc thông tin nhạy cảm.
- BR-03: envelope dùng `fromEmail/fromName` đã lưu và recipient đã validate. Không nhận header tùy ý để tránh header injection.
- BR-04: `secure=true` dùng TLS ngay khi connect. `secure=false` bắt buộc nâng cấp STARTTLS; server không hỗ trợ
  STARTTLS thì thất bại, không fallback plaintext.
- BR-05: luôn kiểm tra certificate chain, hostname và thời hạn. Cấu hình bỏ qua TLS validation chỉ được phép trong
  SMTP fixture của test bằng CA test được trust, không có flag tắt validation trong runtime API.
- BR-06: connect, TLS handshake, authenticate và send dùng chung timeout `SMTP_TIMEOUT_MS`, mặc định 30000 ms.
  Giá trị cấu hình phải nằm trong 1000..120000 và fail-fast khi khởi động nếu sai.
- BR-07: xác thực bằng username/password đã lưu. Password chỉ tồn tại dạng string trong phạm vi lời gọi adapter,
  không cache, không trả về và không log; reference được thả sau khi hoàn tất.
- BR-08: thành công nghĩa là SMTP server trả kết quả chấp nhận cho lệnh gửi thư. Không đồng nghĩa thư đã vào inbox.
- BR-09: SEND-003 không retry. Mỗi HTTP request thực hiện tối đa một SMTP send để tránh gửi lặp.
- BR-10: không giữ database transaction hoặc row lock trong khi DNS/network/SMTP đang chạy.
- BR-11: trước khi gọi SMTP, repository đọc snapshot sender đúng tenant. Sau thành công, update có điều kiện xác nhận
  sender vẫn active và các trường `host`, `port`, `secure`, `username`, `password_encrypted` vẫn giống snapshot.
- BR-12: nếu snapshot kết nối/auth thay đổi hoặc sender bị disable trong lúc gửi, không ghi `verified_at` và trả
  `409 SENDER_CHANGED`; email thử có thể đã được SMTP chấp nhận nên response nói rõ trạng thái này.
- BR-13: khi update có điều kiện thành công, đặt `verified_at=now` và `updated_at=now`. Gửi thử thành công lần nữa
  thay timestamp bằng thời điểm mới.
- BR-14: SMTP/DNS/TLS/auth/recipient rejection hoặc timeout không thay đổi `verified_at` hiện tại. Việc thất bại một
  lần không tự xóa bằng chứng thành công trước đó.
- BR-15: lỗi trả về chỉ thuộc nhóm an toàn; không chuyển nguyên văn exception/server response ra API. Log có
  `tenantId`, `senderId`, nhóm lỗi, duration và correlation ID, nhưng không có recipient, username/password,
  ciphertext, SMTP transcript hoặc message body.
- BR-16: endpoint chịu rate limit riêng 5 request/admin/phút; vượt trả `429 RATE_LIMITED` và `Retry-After`.
- BR-17: host DNS hoặc IP vẫn được hỗ trợ cho SMTP relay nội bộ. Kiểm soát dải mạng/egress thuộc deployment policy;
  endpoint chỉ dành cho admin và không mở redirect/proxy protocol khác SMTP.

## Authorization

- Chỉ JWT policy `Admin`; API key machine bị từ chối.
- `tenantId` lấy từ principal, không nhận từ body/query/path.
- Sender tenant khác và ID giả cùng trả `404 NOT_FOUND` để không lộ sự tồn tại.
- Sender disabled trả `409 SENDER_DISABLED` trước khi mở kết nối.

## Public contract

### `POST /v1/senders/{id}/test`

```http
Authorization: Bearer <admin-access-token>
Content-Type: application/json
```

```json
{ "recipientEmail": "admin@example.edu" }
```

Thành công — `200 OK`:

```json
{
  "sent": true,
  "senderId": "0198...",
  "recipientEmail": "admin@example.edu",
  "verifiedAt": "2026-08-15T06:00:00Z"
}
```

`recipientEmail` trong response là giá trị đã normalize. Không trả SMTP response, message ID hay cấu hình bí mật.

### Mã lỗi

| Trường hợp | HTTP | Code | Thông báo an toàn |
|---|---:|---|---|
| Body/email không hợp lệ hoặc field lạ | 400 | `VALIDATION_FAILED` | Dữ liệu gửi thử không hợp lệ |
| Sender không tồn tại/cross-tenant | 404 | `NOT_FOUND` | Không tìm thấy sender |
| Sender disabled | 409 | `SENDER_DISABLED` | Sender đã bị vô hiệu hóa |
| Sender đổi/disable sau khi SMTP chấp nhận | 409 | `SENDER_CHANGED` | Thư đã được SMTP chấp nhận nhưng cấu hình không được đánh dấu verified |
| DNS/connect/TLS/auth/rejected | 502 | `SMTP_TEST_FAILED` | SMTP test thất bại; kèm `reason` thuộc enum an toàn |
| Hết `SMTP_TIMEOUT_MS` | 504 | `SMTP_TEST_TIMEOUT` | SMTP test quá thời gian chờ |
| Quá 5 request/admin/phút | 429 | `RATE_LIMITED` | Quá nhiều yêu cầu gửi thử |
| Lỗi ngoài dự kiến/giải mã thất bại | 500 | `INTERNAL_ERROR` | Lỗi nội bộ |

`reason` của `SMTP_TEST_FAILED` chỉ nhận một trong: `dns`, `connection`, `tls`, `authentication`, `recipient_rejected`,
`provider`. Không kèm host, username, server text, exception type hoặc stack trace.

## Application/adapter contract

Application khai báo cổng hẹp, không tham chiếu MailKit:

```text
IEmailSender.SendTestAsync(SmtpEnvelope, recipientEmail, cancellationToken)
  -> success
  -> EmailSendException(reason: dns|connection|tls|authentication|recipient_rejected|provider|timeout)
```

Infrastructure adapter dùng MailKit. Handler chịu trách nhiệm lookup tenant, gọi adapter ngoài transaction và cập nhật
verification có điều kiện. Cancellation của client và timeout nội bộ đều hủy network I/O; chỉ timeout nội bộ ánh xạ
`SMTP_TEST_TIMEOUT`.

## Data impact

- Không tạo bảng/cột/index và không cần migration mới.
- Thành công ghi `senders.verified_at` và `updated_at` bằng một conditional update theo tenant, sender, status và snapshot
  connection/auth fields.
- Thất bại trước SMTP acceptance không ghi database.

## Configuration impact

| Biến | Mặc định | Validation |
|---|---:|---|
| `SMTP_TIMEOUT_MS` | `30000` | integer 1000..120000 |

Compose/local khai báo rõ giá trị mặc định. Production có thể override nhưng không thể tắt TLS verification.

## Acceptance criteria

- AC-01: request hợp lệ gửi đúng một plain-text email với from/recipient/subject cố định đúng contract.
- AC-02: `secure=true` dùng implicit TLS; `secure=false` bắt buộc STARTTLS và không plaintext fallback.
- AC-03: credential đúng được giải mã trong Infrastructure, SMTP accept trả `200` và cập nhật `verifiedAt` UTC.
- AC-04: gửi thành công lần nữa cập nhật `verifiedAt` mới.
- AC-05: DNS/connect/TLS/auth/recipient/provider failure trả `502 SMTP_TEST_FAILED` với reason allow-list và không đổi
  `verifiedAt`.
- AC-06: timeout trả `504 SMTP_TEST_TIMEOUT`, hủy I/O và không retry/không đổi `verifiedAt`.
- AC-07: sender disabled trả `409` mà không mở socket; sender cross-tenant/ID giả trả cùng `404`.
- AC-08: nếu cấu hình connection/auth đổi hoặc sender bị disable trong lúc gửi, conditional update không ghi verification
  và trả `409 SENDER_CHANGED` dù SMTP có thể đã nhận thư.
- AC-09: API key machine bị từ chối; rate limit thứ sáu trong một phút trả `429` với `Retry-After`.
- AC-10: mọi response/log/exception không chứa password, ciphertext, username, SMTP transcript hoặc body; failure
  response/log không chứa recipient, còn success response chỉ trả recipient đã normalize theo public contract.
- AC-11: cấu hình `SMTP_TIMEOUT_MS` ngoài biên làm API fail-fast; giá trị hợp lệ được adapter dùng cho toàn thao tác.
- AC-12: Docker Compose test dùng SMTP fixture cục bộ có TLS/STARTTLS và auth để xác nhận thư nhận được, nội dung an toàn,
  failure mapping và migration hiện hành vẫn rollback/reapply sạch.
- AC-13: SMTP call diễn ra ngoài database transaction; DB unavailable khi cập nhật sau send không gây gửi lại lần hai.

## Test mapping

| AC | Test dự kiến |
|---|---|
| AC-01..06 | Adapter/integration tests với SMTP fixture: TLS modes, auth, recipient, timeout và đúng một message |
| AC-07..10 | API integration tests với hai tenant, API key, disabled sender, concurrent config change và log capture |
| AC-11 | Options validator tests |
| AC-12 | `scripts/test-integration.ps1` + Docker Compose SMTP fixture và PostgreSQL thật |
| AC-13 | Handler test dùng fake adapter/repository, xác nhận thứ tự và không retry sau acceptance |

## Planned files

```text
src/Notification.Application/Abstractions/Email/
  IEmailSender.cs
  EmailSendException.cs
src/Notification.Application/Senders/
  SendTestEmailHandler.cs
  SenderModels.cs
  ISenderRepository.cs

src/Notification.Infrastructure/Configuration/SmtpOptions.cs
src/Notification.Infrastructure/Configuration/SmtpOptionsValidator.cs
src/Notification.Infrastructure/Email/MailKitEmailSender.cs
src/Notification.Infrastructure/Persistence/SenderRepository.cs
src/Notification.Infrastructure/DependencyInjection.cs
src/Notification.Infrastructure/Notification.Infrastructure.csproj

src/Notification.Api/Contracts/Senders/SendTestEmailRequest.cs
src/Notification.Api/Endpoints/Senders/SenderEndpoints.cs
src/Notification.Api/Program.cs
src/Notification.Api/appsettings.Development.json

tests/Notification.Application.Tests/Senders/*
tests/Notification.IntegrationTests/Senders/*
deploy/docker/compose.yml
scripts/test-integration.ps1
.env.example

docs/features/v1/03-sender/SEND-003-thu-thu.md
docs/features/v1/README.md
README.md
```

Không dự kiến EF Core migration. Nếu cần thay schema, feature phải quay lại Review trước khi triển khai.

## Security review

- SR-01: tenant lookup trước network call; cross-tenant không tạo DNS/SMTP traffic.
- SR-02: TLS/STARTTLS và certificate validation bắt buộc; không có plaintext/downgrade mode.
- SR-03: plaintext password chỉ tồn tại trong Infrastructure adapter, không log/cache/trả về.
- SR-04: fixed content, một recipient, validation và rate limit hạn chế lạm dụng endpoint làm mail relay.
- SR-05: lỗi dùng allow-list; không lộ SMTP banner/response, host, username, recipient hoặc secret.
- SR-06: host nội bộ được hỗ trợ có chủ đích; production phải áp network egress policy cho API container.

## Open questions

Không có. Đề xuất duyệt contract `200/502/504`, nội dung thư cố định và rate limit 5 lần/admin/phút như trên.

## Approval gate

Chưa được phép triển khai code. Duyệt bằng `APPROVE SEND-003` hoặc yêu cầu sửa bằng `CHANGE SEND-003: ...`.
