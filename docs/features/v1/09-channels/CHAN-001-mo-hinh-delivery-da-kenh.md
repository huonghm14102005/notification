# CHAN-001 — Nền tảng delivery đa kênh

Status: Review
Selected: 2026-08-21
Dependencies: DEVICE-001, DLVR-002, DLVR-003, CBACK-001

## Đọc nhanh

Hiện tại một bản ghi `notification` vừa chứa nội dung vừa kiêm luôn việc gửi email. Feature này tách hai trách nhiệm:

```text
Notification: yêu cầu nghiệp vụ và nội dung
  └─ Delivery email: đích nhận, trạng thái và lịch retry riêng
```

Model mới sẵn sàng để sau này một notification có email, SMS, Discord hoặc push độc lập. Trong CHAN-001 chỉ
`email` được bật và mỗi request vẫn có đúng một email target; gửi nhiều người nhận thuộc INTK-002.

## 1. Mục tiêu

- Một notification sở hữu một hoặc nhiều delivery.
- Mỗi delivery có channel, target, sender, trạng thái, attempt và lịch retry riêng.
- Trạng thái notification được tổng hợp từ delivery, không trực tiếp điều khiển SMTP nữa.
- Dữ liệu email và lịch sử attempt hiện có được backfill, không gửi lại.
- Callback hoàn tất chỉ được tạo khi mọi delivery đã kết thúc.

## 2. Không làm trong feature này

- Không gửi SMS, Discord, webhook hoặc mobile push.
- Không cho một request gửi nhiều target; INTK-002 thực hiện phần này.
- Không hỗ trợ template contract mới; INTK-003 thực hiện sau.
- Không đổi số lần thử, backoff hoặc phân loại lỗi của DLVR-002/DLVR-003.
- Không thêm retry/cancel thủ công hoặc API quản trị delivery riêng.

## 3. Public contract

Endpoint giữ nguyên:

```http
POST /v1/notifications
Authorization: Bearer <device-api-key>
Content-Type: application/json
```

Contract chuẩn mới:

```json
{
  "senderKey": "gmail-main",
  "channels": [{
    "type": "email",
    "targets": [{ "address": "student@example.com", "ref": "student-123" }]
  }],
  "content": {
    "mode": "plaintext",
    "subject": "Cập nhật điểm rèn luyện",
    "body": "Điểm của bạn vừa được cập nhật"
  }
}
```

Response `202`:

```json
{
  "id": "notification-uuid",
  "status": "accepted",
  "deliveries": [{
    "id": "delivery-uuid",
    "channel": "email",
    "target": "student@example.com",
    "targetRef": "student-123",
    "status": "pending"
  }]
}
```

Không trả ciphertext, provider credential hoặc dữ liệu attempt nội bộ.

### Tương thích contract cũ

Payload cũ `{ senderKey, subject, body, recipients:[{email,ref}] }` tiếp tục được nhận trong một chu kỳ chuyển đổi và
được ánh xạ thành plaintext cùng một email delivery. Không được trộn field cũ với `channels/content`; nếu trộn trả
`422 CONTRACT_AMBIGUOUS`. Response payload cũ giữ shape cũ để không làm hỏng client.

API ghi warning `legacy_notification_contract_used`, không log nội dung hoặc email. Contract cũ được đánh dấu deprecated.

## 4. Validation

- `channels` có đúng một phần tử trong CHAN-001.
- `type` chuẩn hóa lowercase và phải là `email`; giá trị khác trả `422 CHANNEL_NOT_SUPPORTED`.
- Channel trùng trả `422 DUPLICATE_CHANNEL`.
- Email channel có đúng một target; nhiều hơn trả `422 MULTIPLE_TARGETS_NOT_ENABLED`.
- Email trim/lowercase, tối đa 254 ký tự, không chứa dấu phẩy/chấm phẩy và phải hợp lệ.
- `ref` trim, nullable, tối đa 200 ký tự, không chứa control character.
- `content.mode` chỉ nhận `plaintext`; mode khác trả `422 CONTENT_MODE_NOT_SUPPORTED`.
- Subject sau trim dài 1..998, không chứa control character.
- Body dài 1..100000; chỉ cho phép tab, CR và LF trong nhóm control character.
- `senderKey` giữ rule SEND-002.
- Validate toàn bộ trước khi ghi; lỗi không tạo dữ liệu nửa vời.

## 5. Authorization và tenant

- Chỉ API key active của device active role `source` hoặc `both` được gọi.
- Notification, delivery, sender, API key và device nguồn phải cùng tenant.
- Mọi query/update delivery mang `tenant_id`; không dựa riêng vào UUID.
- Quyền đọc hiện tại và tenant isolation không thay đổi.

## 6. Trạng thái

Delivery:

```text
pending → sending → delivered
                  → pending       (lỗi tạm thời, còn lượt retry)
                  → failed        (lỗi vĩnh viễn hoặc hết lượt)
```

- Mỗi delivery tối đa bốn attempt.
- Worker claim delivery `pending` tới hạn bằng `FOR UPDATE SKIP LOCKED`.
- `attempt_count` tăng khi claim; mỗi provider call có một `delivery_attempt` bất biến.
- Recovery `sending` stale áp dụng trên delivery và không tạo attempt thứ năm.

Notification tổng hợp:

| Delivery con | Notification |
|---|---|
| còn `pending`, chưa có `sending` | `accepted` |
| có ít nhất một `sending` | `processing` |
| tất cả `delivered` | `delivered` |
| terminal, có cả `delivered` và `failed` | `partially_delivered` |
| tất cả `failed` | `failed` |
| tất cả `cancelled` | `cancelled` |

`partially_delivered` chưa phát sinh từ intake email-only nhưng domain phải hỗ trợ để kênh sau không đổi model.
Notification terminal không được quay lại non-terminal.

## 7. Callback

- Chỉ tạo `notification.completed` khi mọi delivery terminal.
- Tạo event cùng transaction với lần cập nhật cuối làm notification terminal.
- Unique `(notification_id,event_type)` ngăn event trùng khi worker/recovery chạy đồng thời.
- Payload dùng status tổng hợp và mảng delivery có `deliveryId`, `channel`, `target`, `status`, `attemptCount`, `errorCode`.
- Callback failure không đổi notification/delivery và không gọi lại SMTP.
- Callback event đã tạo trước migration giữ encrypted payload cũ và tiếp tục được gửi.

## 8. Data model và migration

Thêm `deliveries`:

```text
id, tenant_id, notification_id
channel, target, target_ref
sender_id nullable
status, attempt_count, next_attempt_at
failure_code, delivered_at
created_at, updated_at
```

Thay đổi:

- `delivery_attempts` tham chiếu `delivery_id` thay cho `notification_id`.
- Notification giữ source API key/device, content snapshot, status tổng hợp và timestamps.
- Sender/target/retry/provider-call state chuyển xuống delivery.
- Index claim `(status,next_attempt_at,created_at,id)` và `(tenant_id,notification_id)`.
- Check channel v1 chỉ nhận `email`; migration sau mở rộng cùng lúc adapter mới được bật.
- Unique `(notification_id,channel,target)` ngăn delivery trùng.

### Backfill

Trong một transaction migration:

1. Tạo đúng một email delivery cho mỗi notification cũ.
2. Sao chép sender, recipient/ref, status, attempt count, retry time, failure và sent time.
3. Ánh xạ `accepted→pending`, `sending→sending`, `sent→delivered`, `failed→failed`.
4. Gắn mọi attempt cũ vào delivery vừa tạo, giữ ID, attempt number và timestamps.
5. Notification đổi `sent→delivered`, `sending→processing`; trạng thái terminal khác giữ nghĩa.
6. Kiểm tra số delivery bằng số notification và không có attempt mồ côi trước khi bỏ cột cũ.

Rollback chỉ hỗ trợ khi database vẫn chỉ có email và mỗi notification có đúng một delivery. Nếu invariant sai, rollback
fail rõ ràng thay vì làm mất dữ liệu.

## 9. Concurrency và tính nhất quán

- Tạo notification và toàn bộ delivery trong một transaction.
- Claim khóa delivery, không giữ khóa notification qua provider call.
- Sau mỗi transition delivery, repository khóa notification và tính aggregate trong cùng transaction.
- Concurrent completion không được lost update hoặc tạo callback sớm.
- Worker crash sau provider success nhưng trước commit vẫn có thể gửi lặp; semantics giữ at-least-once.

## 10. Quan sát và cấu hình

- Log dùng `notificationId`, `deliveryId`, `channel`, `attemptNo`, `result`, `errorCode`; không log target/content/secret.
- Metrics delivery thêm dimension channel với cardinality giới hạn bởi adapter đã đăng ký.
- Health/readiness không phụ thuộc provider ngoài.
- Max attempts/backoff/timeout giữ cấu hình hiện tại.

## 11. Acceptance criteria

- AC-01: contract mới atomically tạo một notification và một email delivery, trả `202` đúng shape.
- AC-02: contract cũ tiếp tục hoạt động; mixed contract bị từ chối.
- AC-03: channel chưa bật, trùng, nhiều target và content mode chưa bật bị từ chối trước khi ghi.
- AC-04: authorization device/API key và tenant isolation giữ đúng DEVICE-001.
- AC-05: worker claim/send/retry theo delivery; tối đa bốn attempt và email thành công không gửi lại.
- AC-06: permanent/transient failure và backoff giữ đúng DLVR-002.
- AC-07: delivery stale được recovery theo DLVR-003, không vượt attempt bốn.
- AC-08: notification aggregate đúng mọi tổ hợp, gồm `partially_delivered`.
- AC-09: delivery transition và notification aggregate atomic dưới concurrent worker.
- AC-10: callback chỉ tạo một lần khi mọi delivery terminal và mang kết quả từng delivery.
- AC-11: callback retry không thay đổi delivery và không gọi lại SMTP.
- AC-12: backfill tạo đúng một delivery cho mọi notification cũ và giữ attempt/history.
- AC-13: database sạch, upgrade bản trước, rollback hợp lệ và apply lại đều pass.
- AC-14: API tra cứu đọc từ delivery nhưng giữ quyền và không lộ nội dung cho machine client.
- AC-15: log/metric không lộ target, content, callback secret hoặc provider credential.
- AC-16: Docker chứng minh intake mới → email delivery → aggregate terminal → signed callback.

## 12. Kế hoạch test

- Domain: delivery transitions, aggregate matrix, terminal invariant.
- Application: validation mapping, legacy adapter, callback completion condition.
- PostgreSQL: atomic intake, tenant isolation, concurrent claim, aggregate lock, backfill và constraints.
- Worker: success, permanent failure, transient retry, exhausted retry và stale recovery theo delivery.
- Docker: contract mới/cũ, SMTP thật, callback HMAC, migration down/up.

## 13. Planned files

```text
src/Notification.Domain/Notifications/*
src/Notification.Domain/Deliveries/*
src/Notification.Application/Notifications/*
src/Notification.Application/Notifications/Delivery/*
src/Notification.Infrastructure/Persistence/*
src/Notification.Infrastructure/Persistence/Migrations/*_AddDeliveries.cs
src/Notification.Api/Contracts/Notifications/*
src/Notification.Api/Endpoints/Notifications/*
src/Notification.Worker/*
tests/Notification.Domain.Tests/Deliveries/*
tests/Notification.Application.Tests/Notifications/*
tests/Notification.IntegrationTests/Notifications/*
deploy/docker/compose.yml
scripts/test-integration.ps1
```

## 14. Quyết định cần duyệt

- CHAN-001 chỉ bật một email target để không chiếm phạm vi INTK-002.
- Contract cũ được hỗ trợ thêm một chu kỳ và giữ response cũ.
- Delivery sở hữu sender, target, retry và attempt; notification giữ content/source/status tổng hợp.
- Migration backfill toàn bộ lịch sử, không gửi lại dữ liệu cũ.
- Callback chỉ phát sau khi aggregate terminal.

## Open questions

Không còn câu hỏi chặn Review. Duyệt bằng `APPROVE CHAN-001` hoặc sửa bằng `CHANGE CHAN-001: ...`.
