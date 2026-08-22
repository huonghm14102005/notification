# HIST-003 — Gửi lại hoặc hủy notification thủ công

Status: Review
Selected: 2026-08-22

Dependencies: HIST-001, HIST-002, DLVR-001, CHAN-001

## Đọc nhanh

Admin có hai thao tác vận hành:

```text
Notification failed → retry → tạo notification mới từ snapshot cũ
                            → chỉ sao chép delivery failed

Notification accepted → cancel → hủy trước khi worker claim
```

Notification và attempts cũ không bị sửa hoặc xóa. Mỗi thao tác lưu người thực hiện và thời điểm để đối soát.

## Outcome

Sau khi sửa nguyên nhân gây lỗi, admin có thể gửi lại phần thất bại; hoặc ngăn một notification chưa bắt đầu gửi
mà không cần hệ thống nguồn tạo request mới.

## Actor và trigger

- Actor duy nhất: admin JWT thuộc tenant.
- Retry khi notification đã kết thúc ở `failed` hoặc `partially_delivered`.
- Cancel khi notification vẫn ở `accepted` và mọi delivery còn `pending`, `attemptCount=0`.
- API key không được retry/cancel.

## In scope

- Retry một notification bằng cách tạo notification mới với ID mới.
- Sao chép content snapshot đã mã hóa và các delivery `failed` của notification nguồn.
- Không render lại template và không gửi lại delivery `delivered`.
- Cancel notification chưa có attempt.
- Transaction và row lock bảo vệ thao tác khỏi race với worker.
- Audit người thao tác, hành động, notification nguồn/kết quả và thời điểm.

## Out of scope

- Chọn từng delivery để retry/cancel; retry hàng loạt hoặc theo lịch.
- Sửa target/content/sender trong lúc retry.
- Recall email đã gửi hoặc hủy delivery đang `sending`.
- API key tự retry/cancel; rate limit riêng cho thao tác admin.

## Business rules

1. Tenant và admin ID chỉ lấy từ JWT; lookup luôn bắt đầu bằng `(tenant_id, notification_id)`.
2. ID không tồn tại hoặc cross-tenant trả `404 NOT_FOUND`.
3. Retry hợp lệ với notification `failed` hoặc `partially_delivered` và phải có delivery `failed`.
4. Retry tạo notification mới, giữ `apiKeyId`, `templateId` và ciphertext snapshot của notification nguồn.
5. Notification mới chỉ chứa bản sao delivery `failed`, giữ channel, target, targetRef và senderId; delivery mới bắt
   đầu `pending`, `attemptCount=0`, `nextAttemptAt=now`.
6. Delivery `delivered` hoặc `cancelled` không được sao chép. Email đã thành công không bị gửi lại.
7. Notification nguồn và attempts cũ là bất biến. Notification mới có tối đa bốn attempt mới cho mỗi delivery.
8. Mỗi notification nguồn chỉ tạo tối đa một manual retry trực tiếp. Gọi lại trả chính notification kết quả đã tạo;
   nếu kết quả đó thất bại, admin retry trên ID mới. Quy tắc này chống tạo trùng khi client timeout.
9. Retry không phụ thuộc API key nguồn còn active, nhưng sender phải tồn tại và active. Nếu không, trả
   `409 SENDER_UNAVAILABLE` và không ghi dữ liệu dở dang.
10. Cancel chỉ hợp lệ khi notification `accepted`, mọi delivery `pending` và chưa có attempt. Trong cùng transaction,
    mọi delivery chuyển `cancelled`, xóa `nextAttemptAt`, rồi aggregate notification chuyển `cancelled`.
11. Nếu worker đã claim (`sending`) hoặc đã có attempt, cancel trả `409 INVALID_STATE`; không cố dừng SMTP.
12. Gọi cancel lại notification đã được manual cancel là idempotent và trả `204`. Trạng thái terminal khác trả
    `409 INVALID_STATE`.
13. Retry/cancel khóa notification và deliveries liên quan. Chỉ worker hoặc admin thắng race; không có trạng thái
    nửa hủy hoặc hai notification retry.
14. Notification retry phát callback `notification.completed` khi kết thúc. Cancel thành công cũng phát callback
    completed với trạng thái `cancelled` theo CBACK-001.
15. Audit chỉ lưu ID/metadata an toàn, không lưu plaintext, target, API key, ciphertext hoặc secret.

## Authorization

- JWT admin hợp lệ: thao tác notification trong tenant của mình.
- Thiếu/sai auth hoặc dùng API key: `401 UNAUTHORIZED` theo policy `Admin`.
- Cross-tenant và ID giả: `404 NOT_FOUND`, không dùng `403` để tránh dò ID.

## Public contract

### Retry

```http
POST /v1/notifications/{id}/retry
Authorization: Bearer <admin-jwt>
```

Không có request body. Lần đầu thành công trả `201 Created` và header
`Location: /v1/notifications/{new-id}`:

```json
{
  "id": "new-notification-uuid",
  "sourceNotificationId": "old-notification-uuid",
  "status": "accepted",
  "createdAt": "2026-08-22T10:00:00Z"
}
```

Gọi lặp lại trả `200 OK` với cùng response và cùng ID.

### Cancel

```http
POST /v1/notifications/{id}/cancel
Authorization: Bearer <admin-jwt>
```

Không có request body. Thành công hoặc gọi lại sau manual cancel: `204 No Content`.

### Errors

| Trường hợp | HTTP | Code |
|---|---:|---|
| ID không tồn tại/cross-tenant | 404 | `NOT_FOUND` |
| Trạng thái không cho phép | 409 | `INVALID_STATE` |
| Retry nhưng sender không active | 409 | `SENDER_UNAVAILABLE` |
| Có body hoặc query parameter không hỗ trợ | 400 | `VALIDATION_FAILED` |
| Database lỗi | 503 | `SERVICE_UNAVAILABLE` |

## Internal contract

```text
RetryNotification(tenantId, adminId, sourceNotificationId, now)
  → RetryResult(created, newNotificationId, createdAt)

CancelNotification(tenantId, adminId, notificationId, now)
  → cancelled | already_cancelled
```

Endpoint chỉ xác thực contract và map lỗi. Repository thực hiện lock, state check, clone/cancel và audit atomically.

## Data impact

Thêm bảng `notification_manual_actions`:

| Cột | Ý nghĩa |
|---|---|
| `id` | UUID audit row |
| `tenant_id` | tenant sở hữu |
| `admin_id` | admin thực hiện |
| `source_notification_id` | notification được thao tác |
| `result_notification_id` | notification mới của retry; null với cancel |
| `action` | `retry` hoặc `cancel` |
| `created_at` | thời điểm UTC |

Ràng buộc/index:

- FK đều `RESTRICT`; check `action IN ('retry','cancel')`.
- Unique `(tenant_id, source_notification_id, action)` chống double-submit.
- Index `(tenant_id, created_at, id)` phục vụ audit sau này.

Migration chỉ thêm bảng/index, tương thích lùi với API/worker cũ. Phải kiểm tra up, down và up lại trên PostgreSQL.

## Acceptance criteria

1. Admin retry notification `failed` nhận `201`; notification mới chỉ có delivery thất bại ở trạng thái pending.
2. Retry `partially_delivered` không sao chép delivery đã delivered.
3. Notification mới giữ ciphertext snapshot/target/sender; không render template và không sửa notification cũ.
4. Gọi retry lặp lại cùng source nhận `200` và cùng result ID; không tạo thêm notification/delivery/audit.
5. Retry không có delivery failed hoặc sai trạng thái trả `409 INVALID_STATE`.
6. Retry với sender disabled/missing trả `409 SENDER_UNAVAILABLE` và không ghi một phần.
7. Cancel accepted chưa có attempt trả `204`; notification và mọi delivery thành cancelled, không còn due.
8. Gọi lại manual cancel vẫn trả `204` và không thêm audit.
9. Cancel khi processing/sending/đã có attempt hoặc terminal khác trả `409 INVALID_STATE`, không đổi dữ liệu.
10. Retry/cancel đồng thời với worker có đúng một state transition thắng; không gửi delivery đã cancel.
11. Thao tác thành công có audit đúng tenant/admin/source/result/time và không chứa nội dung hay secret.
12. API key không gọi được endpoint; thiếu/sai JWT trả `401`.
13. ID giả hoặc cross-tenant trả `404`; tenant khác không đọc/thay đổi được dữ liệu hoặc audit.
14. Notification retry đi qua worker, retry tối đa bốn attempt và callback khi hoàn tất như notification thường.
15. Cancel phát callback completed với trạng thái `cancelled`.
16. Request có body/query lạ trả `400 VALIDATION_FAILED`.
17. Database lỗi trả `503 SERVICE_UNAVAILABLE`, không lộ exception/schema/SQL.
18. Migration chạy up/down/up trong Docker; toàn bộ regression test vẫn pass.

## Test mapping

| AC | Test |
|---|---|
| 1..6 | Domain/application tests cho clone, state, sender và idempotency |
| 7..10 | Transaction/integration tests cho cancel và race với worker |
| 11..13 | Audit, authorization và tenant-isolation integration tests |
| 14..15 | Docker end-to-end worker + callback |
| 16..17 | API contract/error tests |
| 18 | Migration gate và toàn bộ CI/Docker suite |

## Planned files

```text
src/Notification.Domain/Notifications/NotificationManualAction.cs
src/Notification.Domain/Notifications/Delivery.cs
src/Notification.Application/Notifications/ManualNotificationHandlers.cs
src/Notification.Application/Notifications/INotificationRepository.cs
src/Notification.Infrastructure/Persistence/NotificationRepository.cs
src/Notification.Infrastructure/Persistence/NotificationDbContext.cs
src/Notification.Infrastructure/Persistence/Configurations/NotificationManualActionConfiguration.cs
src/Notification.Infrastructure/Persistence/Migrations/*AddNotificationManualActions.cs
src/Notification.Api/Contracts/Notifications/ManualNotificationContracts.cs
src/Notification.Api/Endpoints/Notifications/NotificationEndpoints.cs
tests/Notification.Domain.Tests/Notifications/*
tests/Notification.Application.Tests/Notifications/*
scripts/test-integration.ps1
docs/features/v1/07-history/HIST-003-gui-lai-thu-cong.md
```

## Security review

- Tenant/admin chỉ lấy từ JWT; repository filter tenant trước khi lock/update.
- Cross-tenant trả 404 và có test cô lập tenant.
- Chỉ admin policy được ghi; API key bị 401.
- Audit/response/log không chứa content, target, ciphertext, credential hoặc raw exception.
- Transaction và unique constraint chống double-submit/race.

## Open questions

Không còn câu hỏi chặn. Khi approve cần xác nhận bốn quyết định: retry tạo notification mới, chỉ sao chép delivery
failed, mỗi source chỉ retry trực tiếp một lần, và cancel chỉ trước attempt đầu tiên.
