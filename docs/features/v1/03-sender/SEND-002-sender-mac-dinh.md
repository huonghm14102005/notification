# SEND-002 — Tài khoản gửi mặc định và chọn theo senderKey

Status: Verified
Selected: 2026-08-15
Approved: 2026-08-15
Verified: 2026-08-15

## Đọc nhanh

Mỗi tenant có tối đa một email sender mặc định:

```text
đặt sender B mặc định
→ bỏ mặc định sender A
→ đặt B mặc định
→ commit cùng transaction
```

- Chỉ sender active mới được đặt làm mặc định.
- Đặt lại cùng sender hoặc xóa mặc định nhiều lần đều idempotent.
- Việc thay thế mặc định phải nguyên tử, không có lúc hai sender cùng default.
- Tenant khác nhận `404`; disabled sender nhận state conflict.

Có thể refactor transaction/repository nhưng phải giữ unique invariant “0 hoặc 1 default sender mỗi tenant” và không
được để intake tự đoán một sender khi chưa cấu hình default.

## Outcome

Một tenant có thể chọn một tài khoản SMTP active làm mặc định. Các feature tiếp nhận thông báo có thể phân giải
`senderKey` thành sender cụ thể, hoặc dùng sender mặc định khi không truyền key.

## Actor

- Quản trị viên `owner` đã xác thực bằng JWT đặt hoặc gỡ sender mặc định.
- Application handler của các feature intake gọi bộ phân giải sender; SEND-002 chưa mở endpoint intake.

## Trigger

- Admin gọi `PATCH /v1/senders/{id}` với trường `isDefault`.
- Một application handler gọi sender resolver với `tenantId` tin cậy và `senderKey` tùy chọn.

## In scope

- Mỗi tenant có tối đa một sender active mặc định.
- Đặt mặc định mới và tự động gỡ mặc định cũ trong cùng transaction.
- Cho phép gỡ mặc định hiện tại mà chưa chọn sender thay thế.
- Phân giải `senderKey` tới sender active đúng tenant; bỏ trống thì dùng mặc định.
- Hành vi ổn định khi hai yêu cầu đặt mặc định chạy đồng thời.

## Out of scope

- Endpoint intake và public payload chứa `senderKey` — INTK-001.
- Giải mã mật khẩu hoặc tạo SMTP client — SEND-003/DLVR-001.
- Chọn theo loại thông báo/người nhận, cân bằng tải hoặc tự chọn sender đầu tiên.
- Tự động đổi mặc định khi sender bị disable; SEND-001 đã gỡ cờ mặc định khi disable.

## Preconditions

- PRE-01: sender đích tồn tại trong tenant hiện tại và có `status=active`.
- PRE-02: endpoint quản trị nhận `tenantId` từ JWT, không nhận tenant từ path/body.

## Dependencies

SEND-001

## Tham chiếu

- Phạm vi sản phẩm: [PRODUCT.md](../../../PRODUCT.md).
- Dữ liệu: `senders.is_default`, partial unique index `ux_senders_tenant_default` — SPECS.md §6.
- Contract: `PATCH /v1/senders/:id` với `isDefault`; trường `senderKey` của intake — SPECS.md §7, §8.

## Business rules

- BR-01: `isDefault=true` chỉ hợp lệ với sender `active`; sender không tồn tại hoặc thuộc tenant khác được che thành
  `404 NOT_FOUND`, sender disabled trả `409 SENDER_DISABLED` như SEND-001.
- BR-02: khi đặt mặc định, repository khóa phạm vi tenant, gỡ cờ sender cũ rồi đặt sender đích trong cùng database
  transaction. Không có trạng thái commit nhìn thấy hai sender mặc định.
- BR-03: đặt lại chính sender đang mặc định là idempotent, vẫn trả `200`.
- BR-04: `isDefault=false` gỡ cờ nếu sender đích đang mặc định; nếu không thì là no-op idempotent.
- BR-05: gỡ mặc định không tự chọn sender thay thế; tenant được phép tạm thời không có mặc định.
- BR-06: PATCH không có `isDefault` giữ nguyên hành vi SEND-001. Thay cấu hình SMTP và mặc định trong cùng request
  phải commit nguyên tử.
- BR-07: resolver chỉ trả sender `active` và đúng `tenant_id`.
- BR-08: `senderKey` được trim và đổi chữ thường giống quy tắc tạo sender. Null, rỗng hoặc toàn khoảng trắng nghĩa là
  dùng sender mặc định.
- BR-09: key chỉ định nhưng không tồn tại, thuộc tenant khác hoặc sender disabled đều trả `SENDER_NOT_FOUND`; không
  fallback sang mặc định.
- BR-10: bỏ `senderKey` nhưng không có mặc định active cũng trả `SENDER_NOT_FOUND`.
- BR-11: resolver trả cấu hình mã hóa cho application nội bộ, không giải mã `password_encrypted`.
- BR-12: đặt/gỡ mặc định cập nhật `updated_at` của sender bị tác động nhưng không thay đổi `verified_at`.
- BR-13: partial unique index là lớp bảo vệ cuối. Xung đột serialization/deadlock do request đồng thời được retry hữu
  hạn trong Infrastructure; lỗi cuối cùng không lộ chi tiết SQL.

## Authorization

- PATCH tiếp tục yêu cầu JWT policy `Admin`; API key machine không được đặt mặc định.
- Resolver không nhận tenant từ payload công khai; caller truyền tenant từ principal/API key đã xác thực.
- Mọi lookup theo ID, key hoặc default đều bắt đầu bằng `tenant_id`.
- Sender tenant khác được che thành `404`; PATCH giữ rate-limit policy `sender-mutation`.

## Public contract

### `PATCH /v1/senders/{id}`

Contract SEND-001 được mở rộng không phá vỡ tương thích bằng trường tùy chọn:

```json
{ "isDefault": true }
```

`isDefault` phải là JSON boolean khi có mặt; `null` không hợp lệ. Có thể gửi cùng các trường SMTP đã được SEND-001
duyệt. Ít nhất một trường PATCH phải có mặt.

Thành công trả `200 OK` với sender response hiện hành; `isDefault` phản ánh giá trị sau commit. Response không chứa
password hoặc ciphertext.

### Application contract: sender resolver

```text
Resolve(tenantId, senderKey?) -> ResolvedSender | SENDER_NOT_FOUND
```

Đây là contract nội bộ dùng lại bởi INTK-001 và các intake handler sau này, không phải HTTP endpoint của SEND-002.
`ResolvedSender` chứa `id`, `tenantId`, `key`, `channel` và cấu hình SMTP mã hóa; không chứa plaintext password.

### Mã lỗi

| Trường hợp | HTTP ở PATCH | Code |
|---|---:|---|
| Body sai kiểu, `isDefault: null`, body rỗng hoặc field lạ | 400 | `VALIDATION_FAILED` |
| Sender không tồn tại hoặc thuộc tenant khác | 404 | `NOT_FOUND` |
| Sender disabled | 409 | `SENDER_DISABLED` |
| Resolver không tìm thấy key/default active | do endpoint gọi resolver quyết định | `SENDER_NOT_FOUND` |

## Data impact

- Không tạo migration: cột, check constraint và partial unique index đã có từ migration `AddSenders` của SEND-001.
- Giao dịch đặt mặc định khóa tenant và cập nhật tối đa sender mặc định cũ cùng sender đích.
- Resolver theo key dùng `ux_senders_tenant_key`; theo default dùng `ux_senders_tenant_default`.
- Không đọc/ghi plaintext secret và không đổi `verified_at` chỉ vì đổi `is_default`.

## Acceptance criteria

- AC-01: PATCH `isDefault=true` đặt sender active làm mặc định và trả `isDefault=true`.
- AC-02: đặt B làm mặc định tự gỡ A trong cùng tenant; sau commit chỉ B là mặc định.
- AC-03: mặc định tenant A không làm thay đổi sender tenant B.
- AC-04: PATCH `isDefault=false` gỡ mặc định; gọi lặp lại vẫn thành công và tenant có thể không còn mặc định.
- AC-05: đặt lại chính sender mặc định là idempotent.
- AC-06: sender disabled không thể làm mặc định; sender khác tenant/ID giả cùng trả `404`.
- AC-07: hai PATCH đồng thời cùng tenant kết thúc với đúng một sender mặc định.
- AC-08: resolver với key hợp lệ trả đúng sender active sau trim/normalize.
- AC-09: key không tồn tại, disabled hoặc cross-tenant trả `SENDER_NOT_FOUND` và không fallback.
- AC-10: key null/rỗng/trắng trả sender mặc định active.
- AC-11: không có mặc định active thì resolver trả `SENDER_NOT_FOUND`.
- AC-12: API key machine không gọi được PATCH; response/log không chứa secret/ciphertext.
- AC-13: PATCH kết hợp cấu hình và `isDefault` commit nguyên tử; validation lỗi không đổi sender nào.
- AC-14: đổi mặc định cập nhật `updated_at` nhưng không xóa `verified_at`.
- AC-15: Docker Compose test chạy luồng trên PostgreSQL thật và migration hiện hành rollback/reapply sạch.

## Test mapping

| AC | Test dự kiến |
|---|---|
| AC-01..06, AC-12..14 | Sender endpoint integration tests qua JWT/API key và hai tenant |
| AC-07 | PostgreSQL concurrency integration test với hai transaction đặt default |
| AC-08..11 | Application/repository resolver tests gồm active/disabled/cross-tenant/default |
| AC-15 | `scripts/test-integration.ps1` dùng Docker Compose |

## Planned files

```text
src/Notification.Domain/Senders/Sender.cs
src/Notification.Application/Senders/ISenderRepository.cs
src/Notification.Application/Senders/ISenderResolver.cs
src/Notification.Application/Senders/SenderHandlers.cs
src/Notification.Application/Senders/SenderModels.cs
src/Notification.Infrastructure/Persistence/SenderRepository.cs
src/Notification.Api/Contracts/Senders/SenderRequests.cs
src/Notification.Api/Endpoints/Senders/SenderEndpoints.cs
tests/Notification.Application.Tests/Senders/*
tests/Notification.IntegrationTests/Senders/*
scripts/test-integration.ps1
docs/features/v1/03-sender/SEND-002-sender-mac-dinh.md
docs/features/v1/README.md
```

Không dự kiến migration. Nếu triển khai phát hiện schema SEND-001 không bảo đảm transaction/concurrency đã duyệt,
feature phải quay lại Review trước khi thêm migration.

## Security review

- SR-01: mọi lookup sender lọc `tenant_id` lấy từ identity đã xác thực.
- SR-02: key không tồn tại, disabled và cross-tenant không làm lộ sender tenant khác.
- SR-03: resolver không giải mã secret; API response/log không chứa ciphertext/plaintext.
- SR-04: thao tác admin giữ rate limit; lỗi database được ánh xạ an toàn.

## Open questions

Không có. Quyết định đề xuất để duyệt: cho phép gỡ mặc định bằng `isDefault=false`; intake sau đó trả
`SENDER_NOT_FOUND` cho tới khi chọn mặc định mới hoặc truyền `senderKey` hợp lệ.

## Approval gate

Chưa được phép triển khai code. Duyệt bằng `APPROVE SEND-002` hoặc yêu cầu sửa bằng `CHANGE SEND-002: ...`.
