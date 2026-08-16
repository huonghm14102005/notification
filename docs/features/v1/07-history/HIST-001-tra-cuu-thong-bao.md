# HIST-001 — Tra cứu một thông báo kèm các lần gửi

Status: Verified
Selected: 2026-08-16
Approved: 2026-08-16
Verified: 2026-08-16

## Outcome

Trả lời được câu "thư đã tới chưa" mà không cần đọc log máy chủ. Quản trị viên/hệ thống nguồn truy vấn
được trạng thái, thời điểm, tối đa 4 lần gửi với chi tiết lỗi của một thông báo.

## Actor

Quản trị viên (qua JWT), và hệ thống nguồn (qua API key) với thông báo do chính nó tạo.

## Trigger

Gọi `GET /v1/notifications/:id` với mã thông báo từ response tiếp nhận (INTK-001) hoặc danh sách (HIST-002).

## In scope

- Endpoint `GET /v1/notifications/:id` trả metadata thông báo: ID, tenant, producer, recipient (email/ref), status,
  thời tạo/gửi/cập nhật.
- Nội dung (subject/body): quản trị viên đọc được plaintext sau giải mã; hệ thống nguồn không thấy nội dung, chỉ siêu
  dữ liệu.
- Danh sách delivery_attempts: tối đa 4 dòng khi DLVR-001..003 chạy; mỗi dòng ghi attempt_no, kết quả, thời điểm,
  lỗi (code/message).
- Quản trị viên qua JWT thấy toàn bộ dữ liệu của tổ chức.
- API key chỉ thấy siêu dữ liệu (không nội dung), và chỉ thông báo do chính nó tạo (api_key_id trùng).
- Thông báo không tồn tại hoặc của tổ chức khác trả `404`; không trả `403` để tránh dò tìm.

## Out of scope

- Danh sách và bộ lọc — HIST-002.
- Gửi lại thủ công — HIST-003.
- Audit log mỗi lần đọc nội dung.
- Xuất báo cáo.

## Preconditions

- PRE-01: DLVR-001 Verified; migration `AddDeliveryAttempts` đã chạy.
- PRE-02: notification tồn tại trong tổ chức của người gọi.

## Dependencies

DLVR-001.

Không có phụ thuộc ngược từ feature khác. HIST-002 (danh sách) và HIST-003 (gửi lại) sẽ dùng endpoint này nếu cần chi tiết.

## Tham chiếu

- Must-have: M-10 ([MVP.md](../../../MVP.md)).
- Dữ liệu: `notifications`, `delivery_attempts` (đọc) — SPECS.md §6.
- Contract: `GET /v1/notifications/:id` — SPECS.md §7.

## Business rules

- BR-01: endpoint gọi bằng JWT admin hoặc API key; phân biệt quyền hạn khi trả response.
- BR-02: notification lookup bắt đầu bằng tenant từ principal, rồi ID; không tìm được trả `404`.
- BR-03: JWT admin đọc được toàn bộ trường: nội dung giải mã, email, ref, tất cả metadata.
- BR-04: API key chỉ đọc được nếu `api_key_id` trùng `notification.api_key_id`; không tìm được trả `404` (không phân
  biệt "không tồn tại" hay "không quyền").
- BR-05: API key chỉ thấy siêu dữ liệu: status, thời tạo/gửi, recipient email (không ref), lỗi; không thấy nội dung
  (subject/body).
- BR-06: delivery_attempts được sắp thứ tự tăng theo `attempt_no` (1..4), kèm `result`, thời điểm `started_at`/
  `finished_at`, và lỗi `(code,message)` nếu `result != success`.
- BR-07: lỗi `(code, message)` đã được chuẩn hóa trong adapter DLVR-001; đọc từ database mà không giải mã thêm.
- BR-08: response không lộ `recipient_ref` cho API key; quản trị viên thấy đầy đủ.

## Authorization

- JWT admin: đọc mọi notification thuộc tổ chức của admin, kèm nội dung.
- API key: đọc chỉ notification do chính nó tạo (lookup bằng `(tenant_id, id, api_key_id)` AND), không nội dung,
  không ref.
- Thiếu/sai auth trả `401`.
- Notification cross-tenant trả `404`.

## Public contract

```http
GET /v1/notifications/:id
Authorization: Bearer <jwt-admin> | Bearer notify_<64-hex>
```

Thành công: `200 OK`.

**Admin response:**

```json
{
  "id": "00000000-0000-0000-0000-000000000000",
  "tenantId": "10000000-0000-0000-0000-000000000000",
  "producerName": "dao-tao",
  "senderKey": "smtp-daoTao",
  "status": "sent",
  "recipientEmail": "sv1@st.edu.vn",
  "recipientRef": "2021600123",
  "subject": "Kết quả học kỳ 1",
  "body": "Chào bạn, kết quả của bạn là...",
  "createdAt": "2026-08-16T10:00:00Z",
  "sentAt": "2026-08-16T10:00:15Z",
  "updatedAt": "2026-08-16T10:00:15Z",
  "deliveryAttempts": [
    {
      "attemptNo": 1,
      "result": "success",
      "startedAt": "2026-08-16T10:00:01Z",
      "finishedAt": "2026-08-16T10:00:15Z",
      "providerMessageId": "greenmail-msg-123"
    }
  ]
}
```

**API key response (không nội dung, không ref):**

```json
{
  "id": "00000000-0000-0000-0000-000000000000",
  "tenantId": "10000000-0000-0000-0000-000000000000",
  "producerName": "dao-tao",
  "status": "sent",
  "recipientEmail": "sv1@st.edu.vn",
  "createdAt": "2026-08-16T10:00:00Z",
  "sentAt": "2026-08-16T10:00:15Z",
  "updatedAt": "2026-08-16T10:00:15Z",
  "deliveryAttempts": [
    {
      "attemptNo": 1,
      "result": "success",
      "startedAt": "2026-08-16T10:00:01Z",
      "finishedAt": "2026-08-16T10:00:15Z"
    }
  ]
}
```

| Trường hợp | HTTP | Code |
|---|---:|---|
| Notification không tồn tại hoặc cross-tenant | 404 | `NOT_FOUND` |
| API key không thấy notification (khác `api_key_id`) | 404 | `NOT_FOUND` |
| Auth thiếu/sai | 401 | `UNAUTHORIZED` |
| Database lỗi | 500 | `INTERNAL_SERVER_ERROR` |

Không có `403 Forbidden` để tránh dò tìm tenant/notification khác.

## Internal contracts

```text
GetNotificationWithAttempts(tenantId, notificationId, caller: principal)
  -> NotificationDetail (metadata, attempts[], content?)
```

Query riêng biệt:
- Notification lookup: `(tenant_id, id)`.
- Delivery attempts lookup: `(notification_id, attempt_no ASC)`.
- Content decryption: riêng trong application query handler (chỉ dùng khi caller là admin).

## Data impact

Không có migration mới. HIST-001 dùng bảng `notifications` và `delivery_attempts` được tạo ở DLVR-001.

Chỉ read; không ghi/sửa/xóa.

## Acceptance criteria

- AC-01: JWT admin gọi với notification ID hợp lệ nhận `200`, nội dung plaintext đã giải mã, recipient_ref, tất cả
  metadata.
- AC-02: API key gọi với notification ID của chính nó nhận `200`, siêu dữ liệu, không nội dung, không ref.
- AC-03: API key gọi với notification ID khác tạo (khác `api_key_id`) nhận `404`.
- AC-04: admin/key gọi notification cross-tenant nhận `404`.
- AC-05: notification `accepted` (chưa gửi) không có `deliveryAttempts` hoặc mảng rỗng.
- AC-06: notification `sent` có đúng một attempt `success` với `finishedAt`.
- AC-07: notification `failed` có tối đa 4 attempts; lần cuối là `permanent_failure` hoặc transient khi DLVR-002; mỗi
  attempt có `errorCode` và `errorMessage`.
- AC-08: attempts được sắp tăng `attemptNo`; không bỏ số (1,2,3,4 hoặc 1,2 chứ không 1,3).
- AC-09: admin đọc giải mã được; ciphertext không bao giờ rò rỉ trong response.
- AC-10: auth thiếu/sai trả `401`; JWT invalid trả `401`.
- AC-11: query chỉ đọc, không thay đổi trạng thái notification hay delivery_attempts.
- AC-12: concurrent đọc giống ID không gây lỗi; read không lock hoặc lock brief.
- AC-13: performance chấp nhận được với notification lớn (nội dung 100KB); giải mã không chặn.

## Test mapping

| AC | Test dự kiến |
|---|---|
| AC-01..02 | API contract tests với JWT admin và API key |
| AC-03..04 | Auth/tenant-isolation tests |
| AC-05..08 | Application query tests với attempts từ DLVR-001 |
| AC-09..10 | Decryption, auth và error tests |
| AC-11..13 | Read-only, concurrency và performance tests |

## Planned files

```text
src/Notification.Domain/Notifications/NotificationDetail.cs
src/Notification.Domain/Notifications/DeliveryAttemptDetail.cs
src/Notification.Application/Notifications/Queries/GetNotificationQuery.cs
src/Notification.Application/Notifications/Queries/GetNotificationHandler.cs
src/Notification.Infrastructure/Persistence/Queries/NotificationQueries.cs
src/Notification.Api/Contracts/Notifications/GetNotificationResponse.cs
src/Notification.Api/Contracts/Notifications/DeliveryAttemptResponse.cs
src/Notification.Api/Endpoints/Notifications/GetNotificationEndpoint.cs
src/Notification.Api/Program.cs
tests/Notification.Application.Tests/Notifications/Queries/GetNotificationHandlerTests.cs
tests/Notification.IntegrationTests/Notifications/GetNotificationEndpointTests.cs
docs/features/v1/07-history/HIST-001-tra-cuu-thong-bao.md
```

## Security review

- SR-01: tenant từ JWT/API key principal; không nhận từ request path hay query.
- SR-02: API key chỉ được dữ liệu do chính nó tạo; lookup có điều kiện `api_key_id` bắt buộc.
- SR-03: admin thấy nội dung giải mã; ciphertext không bao giờ lộ trong response JSON.
- SR-04: plaintext/ciphertext chỉ được gọi giải mã trong application layer, không ở infrastructure/API.
- SR-05: `404` cho cross-tenant/sai key để tránh dò tìm.
- SR-06: query có index hỗ trợ để không full scan; hiệu suất tốt với số lượng lớn delivery_attempts.

## Open questions

Không có. Đề xuất duyệt: endpoint đơn cho một thông báo, phân quyền admin/key, không nội dung cho key, không audit
mỗi lần đọc (có thể thêm sau).

## Verification evidence

- `dotnet build Notification.slnx --no-restore`: pass, 0 warning/error.
- `dotnet test Notification.slnx --no-build --no-restore`: pass 45/45 test.
- `scripts/test-integration.ps1`: pass bằng Docker Compose; admin đọc plaintext/ref/attempt, API key sở hữu chỉ đọc
  metadata, API key khác và admin cross-tenant nhận `404`; delivery và migration down/up tiếp tục pass.
