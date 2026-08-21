# AUTH-003 — Cấp, liệt kê, xác thực và thu hồi API key

Status: Verified
Selected: 2026-08-15
Approved: 2026-08-15
Verified: 2026-08-15

## Đọc nhanh

Admin tạo API key cho hệ thống nguồn; raw key chỉ được trả đúng một lần:

```text
raw key: notify_<64 hex> → trả một lần cho admin
                         → DB lưu prefix + HMAC hash
```

- Admin JWT tạo/list/revoke key; API key không có quyền admin.
- Key active tạo machine principal chứa tenant/key identity.
- Revoke là soft delete, idempotent và có hiệu lực ở request kế tiếp.
- Cho phép nhiều key cùng producer để xoay khóa; tối đa 50 active key/tenant.
- Danh sách dùng cursor và tuyệt đối không trả raw key/hash.

Có thể refactor authentication/repository nhưng phải giữ constant-time verification, tenant isolation, one-time secret,
giới hạn key và hành vi revoke không phụ thuộc positive cache.

## Outcome

Mỗi hệ thống nguồn có API key riêng, gắn cố định với một tenant, có thể được cấp và thu hồi độc lập;
khóa thô chỉ xuất hiện một lần và không thể khôi phục từ database.

## Actor

- Quản trị viên `owner` đã xác thực bằng JWT của AUTH-002 quản lý khóa.
- Hệ thống nguồn sử dụng API key để gọi các endpoint machine-to-machine từ INTK-001 trở đi.

## Trigger

- Admin gọi `POST /v1/api-keys` để cấp khóa.
- Admin gọi `GET /v1/api-keys` để kiểm kê.
- Admin gọi `DELETE /v1/api-keys/{id}` để thu hồi.
- Request nghiệp vụ gửi `Authorization: Bearer notify_<64-hex>`.

## In scope

- Sinh khóa bằng CSPRNG, trả khóa thô đúng một lần.
- Lưu prefix và HMAC-SHA256, không lưu khóa thô.
- Liệt kê metadata khóa trong tenant của admin.
- Thu hồi idempotent, có hiệu lực ở request API-key tiếp theo.
- Authentication scheme cho API key và machine principal chứa tenant/key identity.
- Cập nhật `last_used_at` có giới hạn write amplification.
- Migration `AddApiKeys`; Docker Compose test cô lập tenant và rollback/apply PostgreSQL thật.

## Out of scope

- Scope/quyền chi tiết trên từng khóa; mọi khóa active có cùng quyền machine của tenant.
- Ngày hết hạn tự động, tự động rotate, endpoint đổi khóa tại chỗ.
- Sửa tên producer sau khi cấp.
- Rate limit gửi thông báo theo key/tenant; INTK-004 thực hiện bằng identity tạo ở đây.
- Dùng API key để gọi endpoint quản trị.
- Hiển thị hoặc khôi phục lại khóa thô sau response tạo khóa.

## Preconditions

- PRE-01: AUTH-002 ở trạng thái Verified.
- PRE-02: admin và tenant trong JWT vẫn hoạt động.
- PRE-03: `API_KEY_SALT` có ít nhất 16 byte UTF-8 và migration đã chạy.

## Dependencies

AUTH-002.

## Tham chiếu

- Phạm vi sản phẩm: [PRODUCT.md](../../../PRODUCT.md).
- Dữ liệu: `api_keys` — SPECS.md §6.
- Contract: `GET/POST /v1/api-keys`, `DELETE /v1/api-keys/{id}` — SPECS.md §7.
- Định dạng khóa máy và tenant boundary — CONVENTIONS.md §6.

## Business rules

- BR-01: khóa thô có dạng `notify_` + đúng 64 ký tự hex lowercase, được sinh từ 32 byte CSPRNG.
- BR-02: `key_prefix` là `notify_` cộng 12 ký tự hex đầu tiên; prefix dùng để tra cứu và hiển thị,
  không đủ để xác thực.
- BR-03: `key_hash` là HMAC-SHA256 của toàn bộ khóa thô với `API_KEY_SALT`; so sánh hash constant-time.
- BR-04: khóa thô chỉ có trong response `201` của thao tác tạo. Database, danh sách, log, metrics, error,
  migration và test snapshot không chứa khóa thô.
- BR-05: `producerName` được trim, dài 2..100 ký tự, cho phép chữ Unicode, số, khoảng trắng và `._-`;
  không cho control character. Giá trị sau trim được lưu nguyên casing.
- BR-06: cho phép nhiều khóa active cùng `producerName` để admin xoay thủ công theo quy trình tạo mới →
  đổi cấu hình hệ thống nguồn → thu hồi khóa cũ.
- BR-07: mỗi tenant có tối đa 50 khóa active. Vượt giới hạn trả `409 API_KEY_LIMIT_REACHED` và không tạo khóa.
- BR-08: tạo khóa và kiểm tra giới hạn diễn ra trong cùng transaction; request đồng thời không được vượt trần.
- BR-09: danh sách chỉ trả khóa của tenant trong JWT, sắp xếp `createdAt` giảm dần rồi `id` giảm dần;
  không nhận tenant ID từ caller. Cursor mã hóa cặp `(createdAt,id)`, opaque với client, giới hạn mặc định 50
  và tối đa 100.
- BR-10: thu hồi đặt `status=revoked`, `revoked_at=now`; không hard-delete. Thu hồi lại cùng khóa trả `204`.
- BR-11: ID không tồn tại hoặc thuộc tenant khác cùng trả `404 NOT_FOUND`; không tiết lộ cross-tenant resource.
- BR-12: API-key authentication tra prefix, tính HMAC và constant-time compare, sau đó kiểm tra `status=active`
  và tenant chưa soft-delete. Thiếu/sai/thu hồi trả `401 UNAUTHORIZED` cùng hình dạng response.
- BR-13: machine principal chứa `tenantId`, `apiKeyId`, `producerName`, authentication type `ApiKey`;
  không chứa raw key/hash và không có admin role.
- BR-14: request xác thực thành công cập nhật `last_used_at` tối đa một lần mỗi 5 phút cho mỗi khóa;
  lỗi cập nhật telemetry không được biến khóa hợp lệ thành lỗi auth nhưng phải ghi warning an toàn.
- BR-15: thu hồi không dùng cache dương vượt qua request; request bắt đầu sau khi transaction thu hồi commit
  phải bị từ chối. Request đã xác thực trước commit không bị dừng giữa chừng.
- BR-16: endpoint tạo khóa áp fixed-window 10 request/admin/phút; vượt giới hạn trả `429 RATE_LIMITED`
  và `Retry-After`. GET/DELETE vẫn chịu authorization nhưng không dùng bucket tạo khóa.
- BR-17: mọi thời điểm là UTC; `created_by_admin_id` lấy từ JWT để audit, không lấy từ request.

## Authorization

- `GET/POST/DELETE /v1/api-keys` chỉ chấp nhận JWT admin `owner`; API key không gọi được các endpoint này.
- Tenant và admin ID lấy từ principal AUTH-002.
- API-key scheme chỉ áp cho endpoint machine được khai báo rõ; không tự cấp quyền admin.
- Khi endpoint tương lai cho phép cả admin và key, policy phải phân biệt principal type và áp tenant boundary.

## Public contract

### `POST /v1/api-keys`

```http
Authorization: Bearer <admin-access-token>
Content-Type: application/json
```

```json
{ "producerName": "Hệ thống điểm" }
```

Thành công: `201 Created`, `Location: /v1/api-keys/{id}`, `Cache-Control: no-store`.

```json
{
  "id": "00000000-0000-0000-0000-000000000000",
  "producerName": "Hệ thống điểm",
  "keyPrefix": "notify_a1b2c3d4e5f6",
  "key": "notify_<64-hex>",
  "status": "active",
  "createdAt": "2026-08-15T00:00:00Z"
}
```

### `GET /v1/api-keys?limit=50&cursor=<opaque>`

Thành công: `200 OK`.

```json
{
  "items": [
    {
      "id": "00000000-0000-0000-0000-000000000000",
      "producerName": "Hệ thống điểm",
      "keyPrefix": "notify_a1b2c3d4e5f6",
      "status": "active",
      "lastUsedAt": null,
      "createdAt": "2026-08-15T00:00:00Z",
      "revokedAt": null
    }
  ],
  "nextCursor": null
}
```

Response không có `key` hoặc `keyHash`. Cursor sai cấu trúc trả `400 VALIDATION_FAILED`; cursor không chứa
tenant ID và repository vẫn luôn lọc tenant từ principal.

### `DELETE /v1/api-keys/{id}`

Thành công hoặc đã thu hồi: `204 No Content`.

| Trường hợp | HTTP | Mã lỗi |
|---|---:|---|
| Request/UUID/producerName sai | 400 | `VALIDATION_FAILED` |
| JWT hoặc API key thiếu/sai/thu hồi | 401 | `UNAUTHORIZED` |
| Principal đúng nhưng không phải admin owner | 403 | `FORBIDDEN` |
| Key ID không có hoặc khác tenant | 404 | `NOT_FOUND` |
| Tenant đã có 50 khóa active | 409 | `API_KEY_LIMIT_REACHED` |
| Vượt rate limit tạo khóa | 429 | `RATE_LIMITED` |
| PostgreSQL tạm thời không dùng được | 503 | `SERVICE_UNAVAILABLE` |
| Lỗi ngoài dự kiến | 500 | `INTERNAL_ERROR` |

## Data impact

Migration `AddApiKeys` tạo:

```text
api_keys
  id uuid primary key
  tenant_id uuid not null references tenants(id) on delete restrict
  created_by_admin_id uuid not null references admins(id) on delete restrict
  producer_name varchar(100) not null
  key_prefix varchar(19) not null
  key_hash bytea not null
  status varchar(16) not null
  last_used_at timestamptz null
  created_at timestamptz not null
  revoked_at timestamptz null
```

Indexes/constraints:

- Unique `ux_api_keys_prefix` trên `key_prefix`.
- Unique `ux_api_keys_hash` trên `key_hash` để chống collision ngoài dự kiến.
- Index `ix_api_keys_tenant_status` trên `(tenant_id, status)`.
- Index `ix_api_keys_tenant_created` trên `(tenant_id, created_at desc)`.
- Check `status in ('active','revoked')`.
- Check trạng thái/thời điểm: active có `revoked_at is null`; revoked có `revoked_at is not null` và
  `revoked_at >= created_at`.
- `Down()` xóa `api_keys`; integration test rollback rồi apply lại trên PostgreSQL sạch.

## Acceptance criteria

- AC-01: admin owner tạo khóa nhận `201`, Location và khóa đúng format/entropy; database chỉ có HMAC/prefix.
- AC-02: response tạo là nơi duy nhất trả khóa thô; GET, DELETE, captured log/error không lộ raw key/hash/salt.
- AC-03: validation producerName kiểm tra biên độ dài/control characters và không ghi DB khi lỗi.
- AC-04: GET sắp xếp và phân trang cursor ổn định, trả metadata active/revoked của đúng tenant, không lặp/bỏ
  bản ghi khi đi hết trang và không có secret fields.
- AC-05: admin tenant A không xem/thu hồi khóa tenant B; cả ID giả và cross-tenant đều trả cùng `404`.
- AC-06: thu hồi active key trả `204`, chạy lại vẫn `204`, metadata chuyển revoked và không hard-delete.
- AC-07: API key active tạo đúng machine principal; key giả, sửa một ký tự, sai prefix hoặc revoked đều `401`.
- AC-08: request bắt đầu sau commit thu hồi bị từ chối ngay, không phụ thuộc process/cache cũ.
- AC-09: cho phép hai key cùng producerName hoạt động đồng thời để xoay thủ công.
- AC-10: request tạo khóa thứ 51 trả `409`; hai request đồng thời ở ngưỡng 49 chỉ một request được tạo.
- AC-11: `last_used_at` cập nhật sau auth thành công nhưng không quá một lần/5 phút; auth thất bại không cập nhật.
- AC-12: API key không gọi được endpoint admin; JWT admin không bị nhận nhầm thành machine key.
- AC-13: request tạo thứ 11 cùng admin/phút trả `429` có `Retry-After`; admin khác có bucket độc lập.
- AC-14: `API_KEY_SALT` thiếu/yếu làm API fail-fast trước khi mở cổng HTTP và message không chứa salt.
- AC-15: migration apply/rollback/re-apply được; FK, unique và check constraints hoạt động trên PostgreSQL thật.
- AC-16: format, build, unit, architecture, HTTP integration, Docker Compose test xanh; dependency audit sạch.

## Planned files

```text
src/Notification.Domain/Identity/ApiKey.cs
src/Notification.Domain/Identity/ApiKeyStatus.cs
src/Notification.Application/Abstractions/Security/IApiKeySecretService.cs
src/Notification.Application/Identity/ApiKeys/*
src/Notification.Application/Identity/Abstractions/IIdentityRepository.cs
src/Notification.Infrastructure/Configuration/ApiKeyOptions.cs
src/Notification.Infrastructure/Persistence/Configurations/ApiKeyConfiguration.cs
src/Notification.Infrastructure/Persistence/Migrations/*_AddApiKeys.cs
src/Notification.Infrastructure/Persistence/IdentityRepository.cs
src/Notification.Infrastructure/Security/ApiKeySecretService.cs
src/Notification.Api/Authentication/ApiKeyAuthenticationHandler.cs
src/Notification.Api/Contracts/Identity/ApiKeyRequests.cs
src/Notification.Api/Endpoints/Identity/ApiKeyEndpoints.cs
src/Notification.Api/Program.cs
deploy/docker/compose.yml
.env.example
README.md
tests/Notification.Application.Tests/Identity/ApiKeys/*
tests/Notification.IntegrationTests/Identity/ApiKeys/*
scripts/test-integration.ps1
docs/SPECS.md
docs/features/v1/README.md
docs/features/v1/02-identity/AUTH-003-api-key.md
```

## Security review decisions requiring approval

- SR-01: khóa `notify_` + 64 hex; prefix gồm 12 hex đầu, hash bằng HMAC-SHA256 với salt tối thiểu 16 byte.
- SR-02: thu hồi kiểm tra PostgreSQL ở request tiếp theo, không positive-cache key active.
- SR-03: cho phép nhiều khóa cùng producer để xoay thủ công; giới hạn 50 active key mỗi tenant.
- SR-04: `last_used_at` được write-throttle 5 phút; không làm auth thất bại nếu telemetry update lỗi.
- SR-05: danh sách dùng cursor `(createdAt,id)`, mặc định 50 và tối đa 100; không dùng offset pagination.

## Open questions

Không còn câu hỏi kỹ thuật chặn Review. Năm quyết định security trên cần được duyệt bằng
`APPROVE AUTH-003` hoặc thay đổi bằng `CHANGE AUTH-003: ...`.
