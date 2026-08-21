# DLVR-002 — Thử lại có giãn cách, phân loại lỗi, từ bỏ

Status: Verified
Selected: 2026-08-21
Approved: 2026-08-21
Verified: 2026-08-21

## Đọc nhanh

Lỗi SMTP được chia thành hai nhóm:

```text
transient → retry sau 1m, 5m, 25m
permanent → failed ngay
```

- Tối đa 4 attempts: một lần đầu và ba retry.
- Mỗi attempt được ghi riêng; success không gửi lại.
- Transient attempt 4 vẫn ghi `transient_failure` nhưng notification thành `failed`.
- Retry trở về `accepted`; worker không sleep chờ trong handler.
- Shutdown cancellation không tạo failure; lỗi ngoài dự kiến là permanent.

Có thể refactor policy/classifier/repository nhưng phải giữ mã lỗi ổn định, lịch cố định, transaction attempt+state,
giới hạn 4 attempts, safe error và metric chỉ phát sau commit.
Dependencies: DLVR-001

## Outcome

Lỗi email tạm thời được ghi nhận và tự retry có giới hạn; lỗi vĩnh viễn hoặc attempt cuối kết thúc rõ
ràng, không mất notification và không lặp vô hạn.

## Actor và trigger

Worker xử lý notification `sending`; email adapter trả lỗi cho attempt hiện tại.

## In scope

- Typed provider failure: `transient_failure` hoặc `permanent_failure`.
- Một lần đầu và tối đa ba retry, lịch `+1 phút → +5 phút → +25 phút`.
- Ghi attempt bất biến và cập nhật notification nguyên tử.
- Metrics/log cho retry, terminal failure và exhausted retry.

## Out of scope

- Recovery item kẹt `sending` (DLVR-003), manual retry (HIST-003), alert (DLVR-004).
- Jitter, policy riêng theo tenant/sender, dead-letter/Redis queue, retry trong adapter.
- Delivery đa kênh (CHAN-001).

## Preconditions

- DLVR-001 Verified; claim dùng PostgreSQL `FOR UPDATE SKIP LOCKED`.
- Item khớp `status=sending`, `attempt_count=attemptNo`.
- `(notification_id, attempt_no)` là unique.

## Tham chiếu

- [PRODUCT.md](../../../PRODUCT.md), [SPECS.md](../../../SPECS.md) §3, §6, §9.
- [ARCHITECTURE.md](../../../ARCHITECTURE.md) §5, [CONVENTIONS.md](../../../CONVENTIONS.md) §8, §12.

## Business rules

### Attempt và backoff

- BR-01: claim `accepted → sending` tăng `attempt_count` đúng một; attempt đầu là 1.
- BR-02: `MaxDeliveryAttempts = 4`, gồm lần đầu và ba retry.
- BR-03: transient attempt 1/2/3 đặt `next_attempt_at = finished_at + 1m/5m/25m`. Không dùng
  started time hoặc một clock read khác làm mốc.
- BR-04: lịch cố định, không jitter; chưa đến hạn thì worker không claim.
- BR-05: transient attempt 4 vẫn ghi `transient_failure`, nhưng notification thành `failed`, không có
  attempt 5 và `next_attempt_at=null`.
- BR-06: permanent ở bất kỳ attempt nào failed ngay. Success thành sent; attempts cũ không đổi.

### Phân loại

- BR-07: adapter trả typed failure gồm classification, stable code và safe message. Application không
  phụ thuộc MailKit type/enum hoặc raw SMTP response.
- BR-08: transient: client timeout, DNS tạm thời, connection reset/refused/unreachable, transport I/O,
  SMTP `4xx`, protocol disconnect trước kết quả cuối.
- BR-09: permanent: credential sai, TLS/certificate/configuration, sender/message không hợp lệ,
  recipient/provider command SMTP `5xx`.
- BR-10: recipient/provider `4xx` là transient; raw provider response không vào DB/API/log.
- BR-11: sender missing/disabled và decrypt lỗi là permanent application failure, không gọi SMTP.
- BR-12: shutdown cancellation được propagate và không ghi failure. Unexpected provider exception sau
  khi gọi provider ghi permanent `UNEXPECTED_ERROR`; repository exception được propagate cho DLVR-003.

### Atomicity, idempotency, observability

- BR-13: attempt và notification state commit trong cùng PostgreSQL transaction.
- BR-14: transient còn lượt ghi attempt, chuyển `sending → accepted`, giữ count, xóa failure reason và đặt lịch.
- BR-15: terminal failure ghi attempt, chuyển `sending → failed`, đặt safe human-readable
  `failure_reason` tối đa 1000 ký tự, không chứa target/content/secret/raw response.
- BR-16: completion chỉ commit khi khớp tenant/id/status/attempt. Mất race hoặc lặp trả `skipped`, không
  ghi row; unique attempt là lớp bảo vệ cuối.
- BR-17: delivery vẫn at-least-once; crash sau provider success nhưng trước DB commit không được tuyên bố
  exactly-once.
- BR-18: sau commit tăng `delivery.attempts{result}`. `deliveries.failed` chỉ tăng ở terminal failure.
- BR-19: retry log warning kèm tenant/notification/sender/attempt, code, nextAttemptAt; không log PII/secret.

## Authorization

Không có endpoint mới. Chỉ worker nội bộ gọi handler/repository. HIST-001 giữ authorization hiện tại.

## Public contract

Không đổi route/payload. Contract hiện có được sử dụng đầy đủ:

- State: `accepted → sending → accepted` khi retry; cuối cùng `sent` hoặc `failed`.
- Attempt result: `success`, `transient_failure`, `permanent_failure`.
- `attemptCount` tối đa 4 cho luồng tự động.
- `failureReason=null` khi còn retry/thành công; safe readable reason khi failed.

| Stable code | Classification |
|---|---|
| `SMTP_TIMEOUT`, `SMTP_DNS`, `SMTP_CONNECTION`, `SMTP_TRANSIENT` | transient |
| `SMTP_AUTHENTICATION`, `SMTP_TLS`, `RECIPIENT_REJECTED`, `SMTP_PROVIDER` | permanent |
| `SENDER_UNAVAILABLE`, `CONTENT_DECRYPTION_FAILED`, `UNEXPECTED_ERROR` | permanent |

Recipient/provider `4xx` dùng `SMTP_TRANSIENT`; `RECIPIENT_REJECTED`/`SMTP_PROVIDER` dành cho `5xx`.

## State transitions

| Outcome | Điều kiện | Notification | Attempt result |
|---|---|---|---|
| success | attempt 1..4 | `sent`, không lịch | `success` |
| transient | attempt 1..3 | `accepted`, lịch +1m/+5m/+25m | `transient_failure` |
| transient | attempt 4 | `failed`, không lịch | `transient_failure` |
| permanent | attempt 1..4 | `failed`, không lịch | `permanent_failure` |

## Data impact và configuration

Không migration; dùng `notifications.status/attempt_count/next_attempt_at/failure_reason` và
`delivery_attempts.attempt_no/result/error_*`, unique `(notification_id, attempt_no)` hiện có. Nếu cần đổi
schema/constraint, feature quay lại Review trước khi tạo migration.

Delay và max attempt là application constants đã duyệt, chưa mở configuration. `SMTP_TIMEOUT_MS` chỉ là
timeout một attempt; handler không sleep chờ retry. Poll interval/concurrency giữ nguyên.

## Rollout và rollback

1. Deploy API/Worker cùng build; không migration.
2. Smoke test success, transient→success, permanent và transient đủ bốn attempt.
3. Theo dõi attempt result, terminal failed, queue age và retry log.
4. Rollback image trước; item `accepted` có lịch vẫn tương thích schema cũ.

## Acceptance criteria

- AC-01: success attempt 1 ghi đúng một success, sent, không retry.
- AC-02: transient attempt 1 ghi transient, về accepted, count=1, hẹn `finishedAt+1m`.
- AC-03: transient attempt 2/3 hẹn `+5m`/`+25m`; worker không claim trước hạn.
- AC-04: transient attempt 4 ghi attempt rồi failed; polling lặp không tạo attempt 5.
- AC-05: permanent ở attempt bất kỳ failed ngay, không retry.
- AC-06: timeout, DNS, connection/I/O, SMTP `4xx` được phân loại transient với stable code.
- AC-07: auth, TLS/config, recipient/provider `5xx` được phân loại permanent với stable code.
- AC-08: sender unavailable/decrypt lỗi permanent, không gọi SMTP.
- AC-09: shutdown cancellation propagate, không ghi failure.
- AC-10: attempt/state atomic; lỗi commit không để dữ liệu nửa vời.
- AC-11: completion đồng thời/lặp chỉ một commit, không rò unique violation.
- AC-12: attempts 1..4 liên tục, bất biến; HIST-001 trả tăng dần với safe error.
- AC-13: retry không tăng failed metric; terminal tăng một lần; attempt metric tăng sau commit.
- AC-14: DB/API/log không chứa raw SMTP response, credential, recipient, subject/body trong error.
- AC-15: Docker Compose fault test transient→success, permanent, exhausted; format/build/test xanh.

## Planned files

```text
src/Notification.Application/Abstractions/Email/IEmailSender.cs
src/Notification.Application/Notifications/Delivery/*
src/Notification.Domain/Notifications/OutboundNotification.cs
src/Notification.Infrastructure/Email/MailKitEmailSender.cs
src/Notification.Infrastructure/Persistence/DeliveryRepository.cs
src/Notification.Worker/NotificationDeliveryWorker.cs
tests/Notification.Application.Tests/Notifications/Delivery/DeliverNotificationHandlerTests.cs
tests/Notification.IntegrationTests/Notifications/DeliveryRetryTests.cs
scripts/test-integration.ps1
docs/SPECS.md
docs/features/v1/README.md
```

## Decisions requiring approval

- DR-01: tối đa 4 attempt; fixed backoff `1m, 5m, 25m`, không jitter/config.
- DR-02: SMTP `4xx` transient; `5xx`, auth và TLS/config permanent.
- DR-03: attempt 4 transient giữ result transient nhưng notification failed.
- DR-04: unexpected provider exception permanent; shutdown cancellation không tạo attempt.
- DR-05: không migration và không endpoint mới.

## Open questions

Không còn câu hỏi chặn triển khai.
