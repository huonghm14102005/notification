# DLVR-004 — Gom lỗi và gửi cảnh báo tổng hợp

Status: Verified
Selected: 2026-08-22
Approved: 2026-08-22
Verified: 2026-08-22

Dependencies: DLVR-002, DLVR-003, SEND-002

## Đọc nhanh

```text
Delivery kết thúc failed
  → ghi/tăng incident theo tenant + cửa sổ 15 phút + loại lỗi
  → chỉ log warning/error chi tiết an toàn ở lần đầu

Cửa sổ đã đóng
  → worker gửi một email tổng hợp cho admin của tenant
  → nếu email cảnh báo hỏng: ghi error một lần, không tự tạo incident mới
```

Feature này không gửi exception hoặc stack trace cho người dùng. API lịch sử vẫn là nơi xem từng notification cụ thể.

## Outcome

Lỗi lặp lại không spam log hoặc tạo hàng loạt email. Người vận hành nhận một bản tóm tắt bền vững theo tenant và
cửa sổ thời gian, biết số lượng lỗi, nhóm nguyên nhân và cách tra cứu chi tiết.

## Actor và trigger

- Delivery worker ghi incident khi một delivery chuyển sang `failed` terminal.
- Alert worker claim các cửa sổ đã kết thúc và chưa xử lý.
- Người nhận: mọi admin chưa bị xóa của tenant, email được normalize và loại trùng.

## In scope

- Cửa sổ cố định 15 phút theo UTC, cấu hình bằng `ALERT_WINDOW_SECONDS`.
- Gom theo tenant, component, channel và safe error code.
- Lưu firstSeen, lastSeen, count và sample message đã làm sạch.
- Một email tổng hợp cho mỗi địa chỉ admin trong một tenant-window.
- Alert là best-effort, chỉ thử gửi một lần; không sinh alert về lỗi của chính alert.
- Claim/recovery an toàn khi chạy nhiều worker.

## Out of scope

- Dashboard/API đọc incident, acknowledgement, retention hoặc on-call escalation.
- Ngưỡng theo phần trăm, webhook/Slack/Discord/SMS/push.
- Gửi alert cho hệ thống nguồn hoặc địa chỉ tùy ý.
- Thay thế log collector/APM; stack trace vẫn chỉ ở telemetry nội bộ.

## Business rules

1. Chỉ delivery terminal `failed` mới tạo incident. Transient đang chờ retry không tạo incident.
2. Window là `[floor(occurredAt/window), windowStart+window)`, tính theo UTC; mặc định 900 giây.
3. Fingerprint logic gồm `component=delivery`, channel và safe error code. Không hash raw exception/message/target.
4. Cùng `(tenant, windowStart, component, channel, errorCode)` dùng atomic upsert: tăng count, cập nhật lastSeen;
   firstSeen và sample đầu tiên không đổi.
5. Sample message lấy từ allowlist theo error code, tối đa 300 ký tự; không lưu exception, stack trace, target,
   plaintext notification, credential hoặc provider response thô.
6. Lần xuất hiện đầu log `Error` với tenant/component/channel/code và incident ID. Lần lặp chỉ tăng metric/counter,
   không ghi lại cùng error log.
7. Mỗi tenant-window có đúng một alert dispatch. Chỉ claim khi `windowEnd <= now` và có incident.
8. Alert worker dùng `FOR UPDATE SKIP LOCKED`; dispatch chuyển `pending → sending` trước I/O.
9. Alert gửi qua sender mặc định active của tenant tới mọi admin active. Danh sách email normalize lowercase, distinct.
10. Subject không chứa PII: `[Notification] {count} delivery failures`. Body chứa window UTC, tổng count, tối đa 10
    nhóm lỗi theo count giảm dần và hướng dẫn dùng `GET /v1/notifications?status=failed&from=...&to=...`.
11. Không đưa target, subject/body notification, ciphertext, API key, SMTP secret, exception hoặc stack trace vào email.
12. Mỗi admin nhận một SMTP message riêng để không lộ danh sách email qua To/Cc/Bcc.
13. Nếu thiếu sender mặc định hoặc không có admin active, dispatch kết thúc `failed` với safe code
    `ALERT_SENDER_UNAVAILABLE` hoặc `ALERT_RECIPIENT_MISSING`; không retry.
14. Nếu một hoặc nhiều lần gửi alert lỗi, tiếp tục các recipient còn lại rồi kết thúc `partially_delivered` hoặc
    `failed`; lỗi chỉ log `Error` một lần cho dispatch và không tạo failure incident.
15. Alert SMTP chỉ thử đúng một lần cho mỗi recipient. Không dùng DLVR-002 và không tự gọi notification intake.
16. Worker chết khi dispatch đang `sending`: sau 2 phút recovery chuyển dispatch sang `failed` với
    `ALERT_WORKER_INTERRUPTED`; không gửi lại để tránh email trùng.
17. Hoàn tất dispatch phải idempotent theo `(dispatchId,status)`. Callback không được tạo cho internal alert.
18. Incident/dispatch là dữ liệu tenant; mọi query luôn có tenant ID dù không có public endpoint trong feature này.

## Authorization và public contract

Không có endpoint mới và không có thao tác từ client. Đây là job nội bộ của worker.

Email là contract vận hành, không phải notification của hệ thống nguồn:

```text
Subject: [Notification] 12 delivery failures

Window: 2026-08-22T10:00:00Z — 2026-08-22T10:15:00Z
Total: 12
email / SMTP_CONNECTION: 9
email / SMTP_AUTH: 3
Lookup: GET /v1/notifications?status=failed&from=...&to=...
```

## Internal contracts

```text
RecordFailure(tenantId, component, channel, safeErrorCode, occurredAt)
  → incidentId, isFirstOccurrence

ClaimClosedAlertWindows(now, limit)
  → AlertDispatch[]

CompleteAlert(dispatchId, result, recipientCount, successCount, safeErrorCode?, finishedAt)
  → bool

RecoverStuckAlerts(now, staleBefore, limit)
  → recoveredCount
```

Provider adapter nhận resolved sender + một recipient + subject/body an toàn. Không I/O trong transaction.

## Data impact

Thêm `failure_incidents`:

- `id`, `tenant_id`, `window_start`, `window_end`, `component`, `channel`, `error_code`.
- `sample_message`, `first_seen_at`, `last_seen_at`, `occurrence_count`, `created_at`, `updated_at`.
- Unique `(tenant_id,window_start,component,channel,error_code)`.
- Index `(tenant_id,window_end,id)`.

Thêm `failure_alerts`:

- `id`, `tenant_id`, `window_start`, `window_end`, `status`, `attempt_count`.
- `recipient_count`, `success_count`, `failure_code`, `started_at`, `finished_at`, `created_at`, `updated_at`.
- Status: `pending`, `sending`, `delivered`, `partially_delivered`, `failed`.
- Unique `(tenant_id,window_start)`; index `(status,window_end,created_at,id)` và `(tenant_id,created_at,id)`.

Incident upsert và tạo dispatch nằm trong transaction hoàn tất delivery. Migration chỉ thêm bảng/index, tương thích
lùi; kiểm tra up/down/up trên PostgreSQL.

## Configuration

| Biến | Mặc định | Validation |
|---|---:|---|
| `ALERT_WINDOW_SECONDS` | 900 | 60..86400 |
| `ALERT_POLL_INTERVAL_MS` | 5000 | 100..60000 |
| `ALERT_CLAIM_LIMIT` | 20 | 1..100 |
| `ALERT_STUCK_AFTER_SECONDS` | 120 | 30..3600 |

## Acceptance criteria

1. Failure terminal đầu tiên tạo incident đúng tenant/window/fingerprint và log một error an toàn.
2. 100 failure giống nhau trong cùng window tạo một incident count=100, không tạo 100 error log.
3. Khác tenant/channel/error code/window tạo incident riêng.
4. Transient còn retry và delivery thành công không tạo incident.
5. Sample/database/log không chứa target, content, credential, raw exception hoặc stack trace.
6. Window đóng tạo đúng một dispatch; nhiều worker không claim trùng.
7. Alert gửi riêng tới mọi admin active, loại trùng email, bằng sender mặc định active.
8. Email có tổng count, tối đa 10 nhóm lỗi và query tra cứu đúng window; không có PII/secret.
9. Window không có incident không tạo dispatch/email.
10. Thiếu sender/admin kết thúc failed với safe code và không retry.
11. Một recipient lỗi không chặn recipient khác; kết quả aggregate đúng.
12. Lỗi alert không tạo failure incident hoặc alert lồng nhau; chỉ log error một lần.
13. Worker chết khi sending được recovery thành failed, không gửi lại.
14. Complete/recovery gọi lặp không thay đổi terminal dispatch.
15. Incident và dispatch tenant khác không được đọc/claim/cập nhật chéo.
16. Config ngoài giới hạn làm worker fail-fast khi startup.
17. Migration up/down/up và toàn bộ Docker regression pass.

## Test mapping

| AC | Test |
|---|---|
| 1..5 | Unit/application tests cho window, fingerprint, sanitizer và atomic upsert |
| 6, 13..15 | PostgreSQL integration cho claim, concurrency, recovery và tenant isolation |
| 7..12 | Docker + GreenMail, nhiều recipient và failure path |
| 16 | Options validation tests |
| 17 | Migration gate và full regression suite |

## Planned files

```text
src/Notification.Domain/Alerts/*
src/Notification.Application/Alerts/*
src/Notification.Infrastructure/Persistence/AlertRepository.cs
src/Notification.Infrastructure/Persistence/Configurations/Failure*.cs
src/Notification.Infrastructure/Persistence/Migrations/*AddFailureAlerts.cs
src/Notification.Infrastructure/Configuration/AlertOptions.cs
src/Notification.Worker/FailureAlertWorker.cs
src/Notification.Worker/Program.cs
tests/Notification.Domain.Tests/Alerts/*
tests/Notification.Application.Tests/Alerts/*
scripts/test-integration.ps1
docs/features/v1/06-delivery/DLVR-004-canh-bao-hong.md
```

## Security review

- Tenant đến từ notification/delivery đã lưu, không từ payload ngoài.
- Chỉ safe code và allowlisted sample được persistence/email/log.
- Mỗi recipient nhận email riêng; không lộ danh sách admin.
- Alert failure bị chặn khỏi incident pipeline để không đệ quy.
- Claim/complete/recovery có state guard và tenant filter.

## Open questions

Không còn câu hỏi chặn. Đề xuất duyệt: gửi cho mọi admin active, một lần/recipient, không retry alert và recovery
`sending` thành failed để ưu tiên không spam trùng.
