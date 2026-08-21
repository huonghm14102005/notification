# INTK-001 — Tiếp nhận yêu cầu gửi cho một người nhận

Status: Verified
Selected: 2026-08-15
Approved: 2026-08-15
Verified: 2026-08-15

## Đọc nhanh

Hệ thống nguồn dùng API key để gửi notification email vào PostgreSQL:

```text
API key + sender + plaintext + recipients
                 ↓ validate và commit
              202 Accepted
                 ↓
            Worker gửi sau
```

- `202` chỉ có nghĩa notification đã được lưu bền, chưa có nghĩa email đã gửi.
- Tenant/API-key identity lấy từ principal, không lấy từ request.
- Sender phải active và thuộc cùng tenant.
- Email được normalize; subject/body được mã hóa trước khi lưu.
- Một recipient sai làm cả request thất bại; không lưu một phần.

Có thể refactor endpoint/handler/repository nhưng phải giữ validation trước write, transaction toàn request,
tenant isolation, encrypted snapshot và không gọi SMTP trong request API.

## Outcome

Hệ thống nguồn gọi một lần là hoàn tất trách nhiệm: dịch vụ xác thực API key, kiểm tra nội dung, chọn sender, lưu bền notification và trả mã tra cứu. Việc gửi email thật thuộc DLVR-001.

## Actor và trigger

Hệ thống nguồn dùng API key active gọi `POST /v1/notifications` với một người nhận, subject và body plain-text.

## In scope

- Nhận đúng một recipient bằng contract mảng có thể mở rộng ở INTK-002.
- Suy ra tenant/API key từ machine principal, phân giải sender active.
- Validate trước khi ghi; lưu trực tiếp một notification `accepted` trong PostgreSQL.
- Mã hóa snapshot subject/body bằng AES-256-GCM.
- Trả `202` cùng mã notification sau khi commit; DLVR-001 polling PostgreSQL để lấy việc.

## Out of scope

- SMTP/delivery attempt/retry/phục hồi — DLVR-001..003.
- Nhiều recipient — INTK-002; template/variables — INTK-003; intake rate limit — INTK-004.
- Batch và `Idempotency-Key`; MVP chấp nhận at-least-once. Batch chỉ được thêm khi INTK-002 thực sự cần nhiều recipient.
- Endpoint lịch sử/giải mã nội dung — HIST-001/002.

## Preconditions và dependencies

- AUTH-003 và SEND-002 đã Verified.
- API key và tenant active; tenant có sender active được chỉ định hoặc mặc định.
- `DATABASE_URL`, `REDIS_URL`, `ENCRYPTION_KEY` hợp lệ; migration đã chạy.

Dependencies: AUTH-003, SEND-002.

Tham chiếu: [PRODUCT](../../../PRODUCT.md); dữ liệu/contract tại SPECS.md §6–8; worker polling tại DLVR-001.

## Business rules

- BR-01: `recipients` là JSON array có đúng một phần tử. Số lượng khác trả `400 VALIDATION_FAILED`; INTK-002 sẽ chỉ nới giới hạn.
- BR-02: `subject` và `body` bắt buộc. `templateKey`, `variables` và field lạ bị từ chối để caller không hiểu nhầm dữ liệu đã được dùng.
- BR-03: `senderKey` null/rỗng/trắng dùng sender mặc định; giá trị khác được trim/lowercase. Không phân giải được sender active trả `409 SENDER_NOT_FOUND`, không ghi dữ liệu.
- BR-04: subject trim, dài 1..998 và không có control character. Body giữ whitespace, dài 1..100000; chỉ cho phép tab, CR, LF trong nhóm control.
- BR-05: email trim/lowercase, là đúng một địa chỉ hợp lệ, tối đa 254 ký tự; không nhận display name hay danh sách địa chỉ.
- BR-06: `ref` tùy chọn; rỗng/trắng thành null, còn lại trim, tối đa 200 ký tự, không control character. Dịch vụ không diễn giải ref.
- BR-07: validation và resolve sender hoàn tất trước khi ghi; request sai không để lại notification.
- BR-08: request hợp lệ tạo đúng một notification liên kết API key và sender; không tạo batch và không ghi Redis.
- BR-09: notification mới có `status=accepted`, `attempt_count=0`, `next_attempt_at=now`, các field failure/sent/template null; thời gian UTC.
- BR-10: subject/body mã hóa riêng bằng `ISecretCipher`, dùng tenant ID và notification ID làm AAD. Plaintext không vào database, log, metric hoặc exception.
- BR-11: PostgreSQL là hàng đợi bền vững. DLVR-001 polling các dòng `accepted` tới hạn theo index `(status,next_attempt_at)`; intake không gọi Redis và không có bước enqueue sau commit.
- BR-12: transaction chỉ chứa một lần insert notification. Commit thành công trả `202`; commit lỗi trả `503` và không có trạng thái nhận một phần.
- BR-13: response chỉ tạo sau commit. Hai request giống nhau tạo hai ID vì idempotency ngoài phạm vi.
- BR-14: `notifications.accepted` tăng đúng một sau commit; metric không dùng email/ref/API key/notification ID làm label.

## Authorization

- Chỉ policy `ApiKey`; JWT admin không thay thế API key.
- `tenantId`, `apiKeyId`, producer lấy từ principal, không nhận từ request.
- Lookup sender và bản ghi mới luôn ràng buộc tenant.
- API key thiếu/sai/revoked trả cùng `401 UNAUTHORIZED` trước validation nghiệp vụ.
- Sender cross-tenant được che bằng `SENDER_NOT_FOUND`; không trả cấu hình SMTP.

## Public contract

```http
POST /v1/notifications
Authorization: Bearer notify_<64-hex>
Content-Type: application/json
```

```json
{
  "senderKey": "dao-tao",
  "subject": "Kết quả học kỳ 1",
  "body": "Bạn đã có kết quả học kỳ mới.",
  "recipients": [{ "email": "sv1@st.edu.vn", "ref": "2021600123" }]
}
```

`senderKey` và `ref` tùy chọn; field khác bắt buộc; JSON camelCase và từ chối field lạ.

Thành công sau commit: `202 Accepted`.

```json
{
  "accepted": 1,
  "notifications": [{
    "id": "00000000-0000-0000-0000-000000000000",
    "email": "sv1@st.edu.vn",
    "ref": "2021600123"
  }]
}
```

`202` nghĩa là đã nhận trách nhiệm xử lý, không có nghĩa SMTP đã nhận email.

| Trường hợp | HTTP | Code |
|---|---:|---|
| API key thiếu/sai/revoked | 401 | `UNAUTHORIZED` |
| JSON/field/số recipient/email/nội dung/ref sai | 400 | `VALIDATION_FAILED` |
| Không có sender active phù hợp | 409 | `SENDER_NOT_FOUND` |
| Database lỗi trước commit | 503 | `SERVICE_UNAVAILABLE` |

Validation error dùng envelope chung, path như `recipients[0].email`, không echo nội dung hay secret.

## Internal contracts

```text
AcceptSingleNotification(tenantId, apiKeyId, request)
  -> AcceptedNotification(notificationId, normalizedEmail, recipientRef)
```

Endpoint không gọi EF Core hoặc cipher trực tiếp.

## Data impact

Migration `AddNotificationIntake` tạo:

- Chỉ tạo bảng `notifications`: ID, tenant/API-key/sender FK restrict, nullable template FK restrict, email/ref, subject/body `bytea`, status, attempt_count, next_attempt_at, failure_reason, sent_at, timestamps. Không có `batch_id` trong INTK-001.
- Check status thuộc `accepted|sending|sent|failed|cancelled`, attempt >= 0, ciphertext không rỗng.
- Index `(tenant_id,created_at)`, `(tenant_id,status)`, `(status,next_attempt_at)`.
- `Down()` xóa `notifications`; Docker Compose kiểm tra down/up. Đây là migration mở rộng, tương thích phiên bản hiện tại.

PostgreSQL vừa là nguồn sự thật vừa là hàng đợi. Redis không tham gia luồng intake/delivery cơ bản.

## Acceptance criteria

- AC-01: API key active gửi inline cho một recipient nhận `202`, accepted=1 và notification ID.
- AC-02: có đúng một notification liên kết tenant, API key, sender và trạng thái khởi tạo đúng; không có batch.
- AC-03: subject/body là ciphertext khác plaintext và giải mã đúng chỉ với AAD đúng.
- AC-04: bỏ senderKey chọn default; key chỉ định chọn đúng sender sau normalize.
- AC-05: sender thiếu/disabled/cross-tenant trả `409`, không ghi dữ liệu.
- AC-06: API key thiếu/sai/revoked trả `401`; JWT không gọi được endpoint.
- AC-07: mọi payload sai hoặc số recipient khác một trả `400` có field path và không ghi dữ liệu.
- AC-08: tenant A không chọn được sender B; tenant bản ghi chỉ từ principal.
- AC-09: intake không đọc/ghi Redis; notification `accepted` có `next_attempt_at` để worker polling trực tiếp.
- AC-10: worker có thể truy vấn notification tới hạn hiệu quả qua index đã duyệt mà không cần queue phụ.
- AC-11: database rollback trả `503`, không để lại notification và không lộ chi tiết nội bộ.
- AC-12: hai request giống nhau tạo hai ID, đúng semantics at-least-once.
- AC-13: accepted metric chỉ tăng sau commit; log/metric không chứa raw key/email/ref/content.
- AC-14: migration up/down/up thành công bằng Docker Compose.

## Test mapping

| AC | Test dự kiến |
|---|---|
| AC-01..03 | API/PostgreSQL/cipher integration tests |
| AC-04..05 | Application/API sender-resolution tests |
| AC-06..08 | Auth, validation, tenant-isolation tests |
| AC-09..11 | Repository polling-index và transaction rollback tests |
| AC-12..13 | Duplicate request, metrics, log-safety tests |
| AC-14 | `scripts/test-integration.ps1` với Docker Compose |

## Planned files

```text
src/Notification.Domain/Notifications/*
src/Notification.Application/Notifications/*
src/Notification.Infrastructure/Persistence/Configurations/Notification*.cs
src/Notification.Infrastructure/Persistence/NotificationRepository.cs
src/Notification.Infrastructure/Persistence/Migrations/*_AddNotificationIntake.cs
src/Notification.Api/Contracts/Notifications/*
src/Notification.Api/Endpoints/Notifications/NotificationEndpoints.cs
src/Notification.Api/Program.cs
tests/*/Notifications/*
scripts/test-integration.ps1
docs/features/v1/05-intake/INTK-001-tiep-nhan.md
docs/features/v1/README.md
README.md
```

## Security review

- Tenant/API key chỉ từ principal; lookup/index bắt đầu bằng tenant.
- Input có giới hạn trên; unknown field bị từ chối.
- Nội dung mã hóa trước persistence; log/metric không mang plaintext hoặc PII.
- Auth error đồng nhất; sender cross-tenant không thể dò tìm.
- Rate limit sẽ hoàn tất ở INTK-004 trước khi mở nhiều recipient.

## Open questions

Không có. Đề xuất duyệt: nhận đúng một phần tử trong `recipients`, chưa chống trùng; không tạo batch và không dùng Redis, worker DLVR-001 polling PostgreSQL.

## Verification evidence

- `dotnet build Notification.slnx --no-restore`: pass, 0 warning/error.
- `dotnet test Notification.slnx --no-build --no-restore`: pass 39/39 test.
- `scripts/test-integration.ps1`: pass bằng Docker Compose, gồm API-key intake, trạng thái `accepted`, không tạo
  `notification_batches`, JWT denial và migration down/up.
