# DLVR-001 — Worker gửi email bất đồng bộ và ghi nhận lần gửi

Status: Verified
Selected: 2026-08-15
Approved: 2026-08-15
Verified: 2026-08-15

## Outcome

Notification `accepted` do hệ thống nguồn tạo được worker lấy từ PostgreSQL, gửi qua SMTP và chuyển thành `sent` hoặc
`failed`. Đây là lát cắt đầu-cuối tối thiểu để thử tích hợp thật với hệ thống ĐRL.

## Actor và trigger

- Actor: tiến trình `.NET Worker Service`.
- Trigger: polling thấy notification `accepted` có `next_attempt_at <= now`.

## In scope

- Poll và claim notification trực tiếp từ PostgreSQL, không Redis queue.
- Nạp sender đã lưu, giải mã subject/body và mật khẩu SMTP ở sát adapter gửi.
- Gửi đúng một email plain-text qua MailKit.
- Ghi một `delivery_attempts` bất biến cho mỗi lần gửi.
- Chuyển `accepted → sending → sent|failed` và phát metrics an toàn.
- Chống hai worker claim cùng một notification bằng khóa hàng PostgreSQL.
- Docker Compose test end-to-end qua GreenMail; hỗ trợ smoke test với sender Gmail đã cấu hình.

## Out of scope

- Retry/backoff và phân loại transient để hẹn lại — DLVR-002.
- Phục hồi notification bị kẹt `sending` sau crash — DLVR-003.
- Cảnh báo lỗi tổng hợp — DLVR-004.
- API đọc lịch sử — HIST-001; template intake — INTK-003; intake rate limit — INTK-004.
- HTML/attachment/CC/BCC và kênh ngoài email.

## Preconditions

- PRE-01: INTK-001 và SEND-001 Verified; migration `AddNotificationIntake` đã chạy.
- PRE-02: notification `accepted`, tới hạn và trỏ tới sender cùng tenant.
- PRE-03: `ENCRYPTION_KEY`, `DATABASE_URL`, `SMTP_TIMEOUT_MS` hợp lệ.

## Dependencies

INTK-001, SEND-001.

## Business rules

- BR-01: worker polling mỗi `DELIVERY_POLL_INTERVAL_MS` (mặc định 2000, cho phép 250..60000) và xử lý tối đa
  `WORKER_CONCURRENCY` notification đồng thời (mặc định 5, cho phép 1..50).
- BR-02: mỗi lần claim lấy tối đa số slot còn trống, chỉ dòng `status=accepted AND next_attempt_at<=now`, sắp theo
  `next_attempt_at`, `created_at`, `id`; dùng transaction và `FOR UPDATE SKIP LOCKED` để nhiều worker không claim trùng.
- BR-03: claim nguyên tử đổi trạng thái sang `sending`, tăng `attempt_count` từ 0 lên 1, đặt `updated_at=now`; transaction
  commit trước khi mở kết nối SMTP. DLVR-001 không claim lại `sending`.
- BR-04: trước khi gửi, worker nạp notification và sender bằng `(tenant_id,id)`. Notification không còn `sending` hoặc
  attempt hiện tại không phải 1 thì handler tự thoát, không gửi.
- BR-05: sender phải còn `active`. Sender thiếu/disabled/cross-tenant không mở SMTP; tạo attempt
  `permanent_failure`, code `SENDER_UNAVAILABLE`, rồi chuyển notification `failed`.
- BR-06: subject/body chỉ được giải mã trong application handler ngay trước khi gọi email port. Ciphertext dùng đúng
  tenant ID và notification ID làm AAD; lỗi giải mã tạo `permanent_failure` code `CONTENT_DECRYPTION_FAILED`.
- BR-07: email adapter được mở rộng bằng `SendAsync(sender, recipientEmail, subject, body, ct)`. Message dùng đúng
  `fromEmail/fromName`, một `To`, subject/body plain-text; bắt buộc implicit TLS hoặc STARTTLS như SEND-003.
- BR-08: SMTP/provider chấp nhận thư thì trong một transaction: thêm attempt `success`, đặt notification `sent`,
  `sent_at=finished_at`, `next_attempt_at=null`, `failure_reason=null`.
- BR-09: trong DLVR-001, mọi `EmailSendException` đều kết thúc `failed` ngay vì retry chưa được triển khai. Thêm attempt
  `permanent_failure`, lưu error code chuẩn hóa và `failure_reason` an toàn; DLVR-002 sẽ thay đổi riêng lỗi transient.
- BR-10: error code cho lần gửi: `SMTP_TIMEOUT`, `SMTP_AUTHENTICATION`, `SMTP_TLS`, `SMTP_DNS`, `SMTP_CONNECTION`,
  `RECIPIENT_REJECTED`, `SMTP_PROVIDER`, `SENDER_UNAVAILABLE`, `CONTENT_DECRYPTION_FAILED`, `UNEXPECTED_ERROR`.
  `error_message` tối đa 1000 ký tự, là thông điệp chuẩn hóa, không chứa exception/host/user/password/content.
- BR-11: mỗi notification trong DLVR-001 có tối đa một attempt (`attempt_no=1`), được bảo vệ bởi unique
  `(notification_id,attempt_no)`. Gọi handler lại sau `sent|failed` không gửi và không thêm attempt.
- BR-12: kết quả SMTP và commit database không thể là một transaction phân tán. Nếu process chết sau SMTP accept nhưng
  trước commit, notification có thể còn `sending`; DLVR-003 xử lý recovery sau. Hệ thống không tuyên bố exactly-once.
- BR-13: lỗi của một notification không làm dừng worker. Cancellation khi shutdown không bị đổi thành `failed`; worker
  ngừng nhận claim mới và chờ các tác vụ đang chạy trong giới hạn host shutdown.
- BR-14: log mang notificationId/tenantId/senderId/attemptNo nhưng không email/ref/subject/body/ciphertext/SMTP secret.
  `delivery.attempts` tăng theo result; `deliveries.sent` hoặc `deliveries.failed` tăng chỉ sau commit kết quả.

## Authorization và ranh giới dữ liệu

- Không có public HTTP endpoint mới; chỉ Worker đăng ký hosted service.
- Worker dùng service identity/config nội bộ, không dùng JWT/API key.
- Query/claim có thể tìm theo status toàn hệ thống để làm việc, nhưng mọi lần nạp sender và ghi attempt đều giữ
  `tenant_id` lấy từ notification; FK/check và repository ngăn liên kết cross-tenant.
- Nội dung và SMTP secret không xuất hiện trong health, logs, metrics hoặc delivery attempt.

## Internal contracts

```text
ClaimDue(now, limit) -> ClaimedNotification[]
Deliver(notificationId, claimedAttemptNo=1) -> sent | failed | skipped
SendAsync(resolvedSender, recipientEmail, subject, body, cancellationToken)
```

Claimed item chỉ chứa `notificationId`, `tenantId`, `senderId`, `attemptNo`; handler nạp lại dữ liệu từ PostgreSQL.

Không có thay đổi contract `POST /v1/notifications`: response `202` vẫn chỉ nghĩa là đã lưu bền, không bảo đảm đã gửi.

## Data impact

Migration `AddDeliveryAttempts` tạo bảng:

- `id uuid` PK, `tenant_id uuid` FK restrict, `notification_id uuid` FK restrict, `sender_id uuid` FK restrict.
- `attempt_no int`, `result varchar(32)`, `provider_message_id varchar(500) null`, `error_code varchar(64) null`,
  `error_message varchar(1000) null`, `started_at`, `finished_at`, `created_at` timestamptz.
- Check `attempt_no >= 1`; result thuộc `success|transient_failure|permanent_failure`; success không có error code và
  failure phải có error code; `finished_at >= started_at`.
- Unique `(notification_id,attempt_no)`; index `(tenant_id,created_at)` và `(tenant_id,notification_id)`.
- `Down()` chỉ xóa `delivery_attempts`; không xóa hoặc sửa notification đã tồn tại.

Không thêm queue/bảng lease. Trạng thái `sending` trên notification là claim/lease tối thiểu; recovery thuộc DLVR-003.

## Acceptance criteria

- AC-01: notification `accepted` tới hạn được claim nguyên tử thành `sending`, attempt_count=1 trước SMTP.
- AC-02: hai worker polling đồng thời chỉ một worker claim/gửi cùng notification.
- AC-03: happy path gửi đúng from/to/subject/body plain-text tới GreenMail, thêm attempt success và chuyển `sent` với
  `sent_at`, không còn `next_attempt_at`.
- AC-04: ciphertext subject/body giải mã bằng đúng AAD; plaintext không xuất hiện trong DB ngoài tiến trình gửi, log,
  metric hoặc attempt.
- AC-05: sender disabled/missing/cross-tenant không mở SMTP, tạo failure `SENDER_UNAVAILABLE` và notification `failed`.
- AC-06: từng nhóm lỗi adapter ánh xạ đúng code an toàn, tạo một permanent_failure và chuyển `failed`.
- AC-07: gọi handler lại cho notification `sent` hoặc `failed` không gửi lại và không thêm attempt.
- AC-08: unique attempt ngăn ghi trùng khi hai completion cạnh tranh; trạng thái cuối và metric chỉ ghi một lần.
- AC-09: một notification lỗi không dừng polling; notification hợp lệ tiếp theo vẫn được gửi.
- AC-10: cancellation khi shutdown không bị ghi thành provider failure.
- AC-11: log/metric không chứa email/ref/content/ciphertext/password/SMTP username; metrics tăng sau commit.
- AC-12: Worker options sai giới hạn làm process fail-fast; poll interval/concurrency hợp lệ được áp dụng.
- AC-13: migration up/down/up thành công trên Docker Compose và không làm mất bảng notifications.
- AC-14: Docker Compose end-to-end: gọi intake bằng API key, worker tự gửi vào GreenMail và database chuyển `sent`.

## Test mapping

| AC | Test dự kiến |
|---|---|
| AC-01..02 | PostgreSQL repository concurrency tests với `SKIP LOCKED` |
| AC-03..04 | GreenMail/cipher integration tests |
| AC-05..07 | Application handler tests cho sender/error/idempotency |
| AC-08..10 | Completion race, failure isolation và cancellation tests |
| AC-11..12 | Log/metric safety và options validation tests |
| AC-13..14 | `scripts/test-integration.ps1` bằng Docker Compose |

## Planned files

```text
src/Notification.Domain/Notifications/DeliveryAttempt.cs
src/Notification.Domain/Notifications/OutboundNotification.cs
src/Notification.Application/Abstractions/Email/IEmailSender.cs
src/Notification.Application/Notifications/Delivery/*
src/Notification.Infrastructure/Email/MailKitEmailSender.cs
src/Notification.Infrastructure/Persistence/Configurations/DeliveryAttemptConfiguration.cs
src/Notification.Infrastructure/Persistence/DeliveryRepository.cs
src/Notification.Infrastructure/Persistence/Migrations/*_AddDeliveryAttempts.cs
src/Notification.Infrastructure/Configuration/DeliveryWorkerOptions*.cs
src/Notification.Worker/NotificationDeliveryWorker.cs
src/Notification.Worker/Program.cs
tests/Notification.Application.Tests/Notifications/Delivery/*
tests/Notification.IntegrationTests/Notifications/Delivery/*
scripts/test-integration.ps1
README.md
docs/features/v1/06-delivery/DLVR-001-gui-bat-dong-bo.md
docs/features/v1/README.md
```

## Security review

- SR-01: sender lookup và attempt giữ tenant boundary từ notification; không nhận tenant từ ngoại vi.
- SR-02: plaintext/secret chỉ sống trong bộ nhớ lúc gửi, không log/metric/persist lại.
- SR-03: SMTP luôn TLS bắt buộc, timeout hữu hạn; lỗi được chuẩn hóa.
- SR-04: polling có giới hạn concurrency; `SKIP LOCKED` tránh giữ/chờ khóa dây chuyền.
- SR-05: rate limit intake đang tạm hoãn chỉ phù hợp tích hợp thử có kiểm soát, chưa đủ điều kiện production.

## Open questions

Không có. Đề xuất duyệt: DLVR-001 gửi một lần; mọi lỗi kết thúc `failed`. Retry/backoff và recovery crash được giữ lại
rõ ràng cho DLVR-002/003 để có đường thử ĐRL sớm mà không che giấu giới hạn hiện tại.

## Verification evidence

- `dotnet build Notification.slnx --no-restore`: pass, 0 warning/error.
- `dotnet test Notification.slnx --no-build --no-restore`: pass 42/42 test.
- `scripts/test-integration.ps1`: pass bằng Docker Compose; API key tiếp nhận notification, Worker claim từ PostgreSQL,
  gửi SMTP TLS tới GreenMail, chuyển `sent`, ghi attempt `success`, rồi migration down/up thành công.
