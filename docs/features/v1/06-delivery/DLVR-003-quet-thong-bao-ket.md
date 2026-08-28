# DLVR-003 — Cứu notification bị kẹt

Status: Verified
Selected: 2026-08-21
Approved: 2026-08-21
Verified: 2026-08-21
Dependencies: DLVR-001, DLVR-002

## Mục tiêu

Notification không được nằm mãi ở `sending` khi worker bị tắt hoặc crash giữa lúc gửi.

Worker sẽ định kỳ tìm notification bị kẹt, ghi nhận attempt bị gián đoạn và:

- gửi lại nếu vẫn còn lượt;
- chuyển sang `failed` nếu đã hết 4 attempts.

## Vấn đề cần giải quyết

Luồng bình thường:

```text
accepted → sending → sent
```

Khi worker chết giữa chừng:

```text
accepted → sending → worker chết
                    → notification kẹt ở sending
```

Khi chuyển sang `sending`, hệ thống đã tăng `attempt_count`. Vì vậy recovery không được giảm hoặc dùng lại attempt
number. Attempt bị gián đoạn phải được ghi vào lịch sử trước khi gửi lần tiếp theo.

## Phạm vi

### Có trong feature

- Quét notification `sending` quá lâu.
- Nhiều worker có thể quét đồng thời mà không xử lý trùng.
- Ghi attempt bị gián đoạn với mã lỗi ổn định.
- Retry ngay nếu còn lượt.
- Kết thúc sau attempt thứ tư.
- Cấu hình chu kỳ quét và ngưỡng xác định item bị kẹt.
- Log, metric và Docker integration test.

### Chưa làm

- Manual retry hoặc cancel.
- Cảnh báo tổng hợp và callback.
- Dead-letter queue hoặc Redis delivery queue.
- Heartbeat cho từng attempt.
- Bảo đảm exactly-once.

## Quy tắc xử lý

### 1. Xác định notification bị kẹt

Notification được coi là bị kẹt khi:

```text
status = sending
updated_at <= now - STUCK_AFTER_SECONDS
```

Mặc định:

- quét mỗi 5 phút;
- `sending` từ 10 phút trở lên được coi là bị kẹt;
- xử lý tối đa 100 notification mỗi vòng.

Lần quét đầu tiên chạy sau một chu kỳ, không chạy ngay khi worker khởi động.

Notification `accepted` không cần recovery. Worker bình thường đã lấy trực tiếp các item tới hạn từ PostgreSQL.
Feature này không dùng Redis.

### 2. Ghi nhận attempt bị gián đoạn

Mỗi item được recovery tạo đúng một `delivery_attempts`:

```text
result: transient_failure
errorCode: WORKER_INTERRUPTED
errorMessage: Delivery worker did not complete the attempt.
startedAt: updated_at của notification trước recovery
finishedAt: thời điểm recovery
```

Attempt và trạng thái notification phải được lưu trong cùng một PostgreSQL transaction.

### 3. Chọn retry hoặc failed

| Attempt bị kẹt | Trạng thái sau recovery | Lần tiếp theo |
|---:|---|---|
| 1, 2 hoặc 3 | `accepted` | Retry ngay với attempt kế tiếp |
| 4 | `failed` | Không có attempt 5 |

Khi còn lượt retry:

- giữ nguyên `attempt_count`;
- đặt `next_attempt_at` bằng thời điểm recovery;
- xóa `failure_reason`;
- lần claim kế tiếp tự tăng attempt number.

Khi attempt 4 bị kẹt:

- đặt `next_attempt_at=null`;
- đặt `failure_reason=WORKER_INTERRUPTED`;
- không cho polling claim lại.

Ví dụ worker chết ở attempt 2:

```text
attempt 1: transient_failure
attempt 2: transient_failure / WORKER_INTERRUPTED
attempt 3: lần gửi tiếp theo
```

### 4. Chống xử lý trùng

- Query recovery dùng `FOR UPDATE SKIP LOCKED`.
- Chỉ commit nếu notification vẫn là `sending` với cùng `attempt_count` và `updated_at`.
- Nếu delivery handler đã hoàn tất trước, recovery bỏ qua.
- Nếu recovery hoàn tất trước, completion cũ của handler bị bỏ qua.
- Unique `(notification_id, attempt_no)` là lớp bảo vệ cuối.
- Attempt count ngoài 1..4 là dữ liệu bất thường: log error, bỏ qua item đó và tiếp tục item khác.

## Giới hạn bảo đảm

Delivery vẫn là at-least-once. Email có thể bị gửi trùng trong tình huống:

```text
SMTP đã nhận email
→ worker chết trước khi ghi sent
→ recovery cho gửi lại
```

Không thể bảo đảm exactly-once vì SMTP và PostgreSQL không dùng chung transaction. Feature ưu tiên không làm mất
notification.

## Cấu hình

| Biến | Mặc định | Giá trị hợp lệ |
|---|---:|---:|
| `SWEEP_INTERVAL_SECONDS` | 300 | 5..3600 |
| `STUCK_AFTER_SECONDS` | 600 | 180..86400 |

`STUCK_AFTER_SECONDS` phải lớn hơn SMTP timeout. Cấu hình sai làm Worker fail khi khởi động.

## Log và metric

Sau mỗi recovery đã commit:

- tăng `delivery.attempts{result=transient_failure}`;
- tăng `deliveries.recovered`;
- tăng `deliveries.failed` nếu attempt 4 trở thành terminal.

Log warning gồm tenant ID, notification ID, sender ID, attempt number và trạng thái terminal. Không log email,
subject, body, ciphertext, SMTP response hoặc credential.

Cancellation hoặc lỗi database không được biến notification thành `failed`. Worker log lỗi và thử lại ở vòng sau.

## Contract và dữ liệu

Không có endpoint hoặc migration mới.

HIST-001 hiển thị recovery attempt giống attempt bình thường với:

```text
transient_failure / WORKER_INTERRUPTED
```

Repository dự kiến cung cấp một thao tác:

```text
RecoverStuck(now, staleBefore, limit)
    → danh sách item đã recovery thành công
```

Danh sách kết quả chỉ dùng để ghi log và metric sau commit. Cách tổ chức class/method nội bộ có thể refactor miễn giữ
đúng các quy tắc và acceptance criteria trong tài liệu này.

## Acceptance criteria

- AC-01: chỉ `sending` đúng hoặc quá ngưỡng mới được recovery.
- AC-02: nhiều worker quét đồng thời chỉ một worker recovery mỗi notification.
- AC-03: recovery tạo đúng một attempt `transient_failure/WORKER_INTERRUPTED`.
- AC-04: attempt và state transition được commit nguyên tử.
- AC-05: attempt 1..3 trở về `accepted` và được retry ngay với attempt number kế tiếp.
- AC-06: attempt 4 chuyển `failed`; không tạo attempt 5.
- AC-07: lịch sử attempt không có gap hoặc duplicate.
- AC-08: recovery và delivery completion chạy đua không ghi hai kết quả cho cùng attempt.
- AC-09: dữ liệu có attempt count ngoài 1..4 được bỏ qua mà không chặn item hợp lệ.
- AC-10: cancellation/lỗi DB không tạo attempt hoặc terminal state sai.
- AC-11: cấu hình ngoài giới hạn hoặc ngưỡng stale không lớn hơn SMTP timeout làm startup fail.
- AC-12: log và metric chỉ phát sau commit, không chứa dữ liệu nhạy cảm.
- AC-13: Docker test được luồng `stale sending → recovery → retry → sent`.
- AC-14: Docker test attempt 4 stale chuyển `failed` và không có attempt 5.
- AC-15: format, build và toàn bộ test pass; không thêm migration hoặc Redis dependency.

## File dự kiến thay đổi

```text
src/Notification.Application/Abstractions/Observability/NotificationMetrics.cs
src/Notification.Application/Notifications/Delivery/*
src/Notification.Infrastructure/Configuration/DeliveryWorkerOptions*.cs
src/Notification.Infrastructure/Persistence/DeliveryRepository.cs
src/Notification.Worker/NotificationDeliveryWorker.cs
src/Notification.Worker/Program.cs
tests/Notification.Application.Tests/Notifications/Delivery/*
scripts/test-integration.ps1
deploy/docker/compose.yml
.env.example
docs/SPECS.md
```

## Các quyết định cần duyệt

- Chỉ recovery `sending`; `accepted` đã được polling bình thường xử lý.
- Attempt bị gián đoạn là transient `WORKER_INTERRUPTED`.
- Attempt 1..3 retry ngay; attempt 4 failed.
- Sweep mặc định 5 phút, stale threshold 10 phút, batch 100.
- Chấp nhận at-least-once; không migration, Redis queue hoặc endpoint mới.

## Open questions

Không còn câu hỏi chặn triển khai.

## Verification evidence

- Build pass, 0 warning/error; 62 .NET tests pass; format check pass.
- Docker Compose pass các luồng recovery attempt 1 → success attempt 2 và attempt 4 → failed, không attempt 5.
- Migration rollback/re-apply pass; feature không thêm migration mới.
