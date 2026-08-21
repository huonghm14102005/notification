# DEVICE-001 — User quản lý device và API key

Status: Verified
Selected: 2026-08-21
Approved: 2026-08-21
Verified: 2026-08-21

## Đọc nhanh

Tenant có nhiều user; mỗi user có thể quản lý nhiều device. Device là danh tính ổn định của hệ thống nguồn:

```text
Tenant → User → Device → nhiều API keys
```

- Device public ID là UUID; API key là secret riêng, không phải device ID hay push token.
- User quản lý device của mình; tenant owner quản lý mọi device cùng tenant.
- Device role ở v1 là `source` hoặc `both`.
- Disable device là idempotent và làm toàn bộ key của device ngừng xác thực ngay.
- Mỗi device có tối đa 10 active keys; mỗi tenant tối đa 50.
- Raw key chỉ trả một lần; list chỉ có prefix/hash-safe metadata.
- Key cũ được backfill từ `producerName` sang device mà không đổi key, hash, prefix hoặc history.
- `/v1/api-keys` được giữ một chu kỳ chuyển đổi bên cạnh nested device-key endpoints.

Có thể refactor endpoint/application/repository nhưng phải giữ ownership filter trong query, tenant isolation, one-time
secret, giới hạn khóa, disable semantics và tính tương thích của migration/backfill.
Dependencies: AUTH-003

## Outcome

User đã đăng nhập quản lý nhiều device thuộc mình. Mỗi device nguồn có định danh công khai ổn định
và một hoặc nhiều API key bí mật có thể xoay/thu hồi độc lập. Khi device gọi API, server suy ra
tenant, owner, device và API key từ credential; không tin các ID này trong request body.

## Actor

- User đã xác thực bằng JWT. Trong schema hiện tại actor này là `Admin`; DEVICE-001 không đổi contract
  đăng nhập hoặc đổi tên bảng `admins`.
- Tenant owner quản lý được mọi device trong tenant. Hiện mọi admin đều có role `owner`; member role
  được để cho feature Identity riêng.
- Device role `source` hoặc `both` dùng API key để gọi machine endpoint.

## Trigger

- User tạo, xem, liệt kê, đổi tên hoặc disable device.
- User cấp, liệt kê hoặc thu hồi API key của device.
- API-key authentication xác định device nguồn cho notification mới.

## In scope

- Device thuộc tenant và đúng một owner user/admin.
- Role `source` và `both`; role được chọn khi tạo và không đổi trong feature này.
- CRUD an toàn theo nghĩa: create/read/list/rename/disable; không hard-delete và chưa re-enable.
- API key lồng dưới device; raw key chỉ xuất hiện một lần khi cấp.
- Nhiều active key trên một device để rotate không gián đoạn.
- Disable device vô hiệu toàn bộ key ngay ở request tiếp theo.
- Backfill API key hiện có từ `producer_name` sang device mà không đổi raw key/hash/prefix.
- Principal máy bổ sung `device_id`, `owner_user_id`; giữ claim legacy `producer_name` trong v1.
- Giữ `/v1/api-keys` hiện tại hoạt động như compatibility API trong một chu kỳ chuyển đổi.
- Docker Compose integration test với PostgreSQL thật và migration rollback/re-apply.

## Out of scope

- Đổi bảng/type `Admin` thành `User`, thêm member role, mời user hoặc chuyển ownership device.
- Device tự đăng ký ẩn danh, pairing code, device attestation hoặc certificate/mTLS.
- Device role `recipient`, push endpoint, Firebase/APNs/Web Push — DEVICE-002.
- Callback URL/secret và gửi callback — CBACK-001.
- API-key scope chi tiết, expiry tự động hoặc quota riêng theo device.
- Re-enable device đã disabled; cần feature/audit policy riêng.
- Đổi intake sang contract đa kênh — CHAN-001.
- Xóa `api_keys.producer_name` hoặc xóa compatibility endpoint; việc contract cột cũ là migration sau.

## Preconditions

- AUTH-003 đã Verified; API key hiện tại xác thực ổn định.
- `admins`, `tenants`, `api_keys` và notification FK hiện tại tồn tại.
- Tất cả API key hiện có có `producer_name` hợp lệ theo AUTH-003.

## Dependencies

AUTH-003. CBACK-001, CHAN-001 và DEVICE-002 phụ thuộc DEVICE-001, không phải dependency ngược.

## Tham chiếu

- Domain đích: [TARGET-DESIGN.md](../../../TARGET-DESIGN.md) §2–3, §9.
- Quy tắc auth/ownership/migration: [CONVENTIONS.md](../../../CONVENTIONS.md) §6–7.
- Contract/schema hiện tại: [SPECS.md](../../../SPECS.md) §6–7.
- Compatibility API: [AUTH-003](../02-identity/AUTH-003-api-key.md).

## Business rules

### Device

- BR-01: `id` UUID là mã device công khai, ổn định. Không tạo thêm `deviceKey`; `name` chỉ để hiển thị
  và không dùng xác thực.
- BR-02: `name` được trim, dài 2..100 ký tự, cho phép Unicode, số, khoảng trắng và `._-`; cấm control
  character. Tên không cần unique vì UUID mới là định danh.
- BR-03: role nhận `source` hoặc `both`. DEVICE-001 chưa nhận `recipient`; role không thể PATCH.
- BR-04: device mới có status `active`, `owner_admin_id` và `tenant_id` lấy từ JWT, không nhận từ body.
- BR-05: user thường chỉ đọc/sửa device có `owner_admin_id` của mình. Tenant owner được thao tác mọi
  device cùng tenant. Khác tenant luôn `404`.
- BR-06: rename chỉ đổi `name`, cập nhật `updated_at`; không đổi lịch sử notification đã lưu.
- BR-07: disable chuyển `active → disabled`, đặt `disabled_at`; gọi lại idempotent trả `204`.
- BR-08: device disabled không được cấp key mới và mọi key của nó ngừng xác thực ngay sau commit.
  Các key không bị đổi sang `revoked`; trạng thái key và device là hai lớp độc lập để giữ audit.
- BR-09: không hard-delete device đã tồn tại. Device disabled vẫn đọc/list được và giữ liên kết lịch sử.
- BR-10: tenant owner list mặc định chỉ thấy device của chính mình; `scope=tenant` mới liệt kê toàn
  tenant. Actor không phải owner dùng `scope=tenant` nhận `403` khi member role được bổ sung sau.

### Device API key

- BR-11: format, CSPRNG, prefix, HMAC, constant-time comparison, raw-key-once, last-used throttle và
  giới hạn tạo key kế thừa nguyên vẹn AUTH-003.
- BR-12: mỗi device có tối đa 10 active key; mỗi tenant vẫn có tối đa 50 active key. Cả hai giới hạn
  được kiểm tra trong cùng transaction có khóa phù hợp để request đồng thời không vượt trần.
- BR-13: cấp key chỉ cho device active có role `source` hoặc `both`.
- BR-14: list key chỉ trả metadata và lọc đồng thời `(tenant_id, device_id)`; không trả hash/raw key.
- BR-15: revoke idempotent; ID giả, key thuộc device khác, owner khác không có quyền hoặc tenant khác
  đều `404`, trừ tenant owner hợp lệ trong cùng tenant.
- BR-16: một device có thể có nhiều active key. Rotate là tạo key mới, triển khai key mới vào thiết
  bị, rồi revoke key cũ; không có endpoint thay raw key tại chỗ.
- BR-17: machine principal sau xác thực gồm `tenant_id`, `owner_user_id`, `device_id`, `api_key_id`,
  `device_role`, `actor_type=machine`; không chứa raw/hash.
- BR-18: claim `producer_name` tiếp tục mang `device.name` cho code/consumer v1. Claim này deprecated,
  không dùng làm ownership hoặc authorization mới.
- BR-19: machine endpoint phải kiểm tra device active trong cùng query xác thực key; không positive-cache
  active device/key qua request.

### Backfill và compatibility

- BR-20: migration nhóm key hiện có theo `(tenant_id, lower(trim(producer_name)))`; mỗi nhóm tạo một
  device role `source`. Tên device lấy `producer_name` của key được tạo sớm nhất, tie-break bằng UUID.
- BR-21: owner của device backfill là `created_by_admin_id` của key sớm nhất theo cùng thứ tự. Tenant
  owner vẫn quản lý được device nếu các key trong nhóm từng do admin khác tạo.
- BR-22: migration gắn mọi `api_keys.device_id`; không thay `id`, prefix, hash, status, timestamps hoặc
  FK từ notifications tới API key. Sau backfill `device_id` là NOT NULL.
- BR-23: giữ `producer_name` và dual-write nó bằng device name khi tạo key mới trong v1. Rename device
  không cập nhật `producer_name` của key cũ để lịch sử/response legacy không bị viết lại.
- BR-24: `POST /v1/api-keys` legacy chỉ tìm device theo `normalized_legacy_name`; device tạo từ API mới
  có giá trị này null và không tham gia lookup. Nếu chưa có legacy device thì endpoint tạo device role
  `source`, owner là caller, rồi cấp key trong một transaction.
- BR-25: `GET /v1/api-keys` và `DELETE /v1/api-keys/{id}` giữ response/status hiện tại. Endpoint mới
  là contract ưu tiên; compatibility endpoint được đánh dấu deprecated trong docs/response header.

## Authorization

| Thao tác | JWT user sở hữu device | Tenant owner cùng tenant | API key | Khác tenant |
|---|---:|---:|---:|---:|
| Tạo device | Có | Có | Không | Không |
| Xem/list/rename/disable device | Có | Có | Không | 404 |
| Cấp/list/revoke key của device | Có | Có | Không | 404 |
| Dùng key gọi machine endpoint | N/A | N/A | Chỉ device active/key active | 401 |

- Tenant/user/role lấy từ JWT; device/key identity lấy từ route rồi repository lọc ownership.
- API key không được gọi endpoint quản trị device/key.
- Resource tồn tại nhưng owner không có quyền được xử lý như `404`; `403` chỉ dùng cho hành động
  `scope=tenant` mà role không cho phép.

## Public contract

### `POST /v1/devices`

JWT user. Rate limit 20 create/user/phút.

```json
{ "name": "DRL Production", "role": "source" }
```

Thành công: `201 Created`, `Location: /v1/devices/{id}`.

```json
{
  "id": "00000000-0000-0000-0000-000000000000",
  "name": "DRL Production",
  "role": "source",
  "status": "active",
  "ownerUserId": "00000000-0000-0000-0000-000000000000",
  "createdAt": "2026-08-21T00:00:00Z",
  "updatedAt": "2026-08-21T00:00:00Z",
  "disabledAt": null
}
```

### `GET /v1/devices/{id}`

Trả cùng device representation; không chứa key, hash hoặc callback config tương lai.

### `GET /v1/devices?scope=mine|tenant&status=active|disabled&limit=50&cursor=<opaque>`

- `scope` mặc định `mine`; `status` tùy chọn; limit 1..100, mặc định 50.
- Sắp xếp `createdAt desc, id desc`; cursor opaque chứa cặp này, không chứa tenant/user.

```json
{ "items": [{ "id": "...", "name": "DRL Production", "role": "source", "status": "active",
  "ownerUserId": "...", "createdAt": "...", "updatedAt": "...", "disabledAt": null }],
  "nextCursor": null }
```

### `PATCH /v1/devices/{id}`

Body phải chứa đúng trường `name`; unknown/null/empty body bị từ chối.

```json
{ "name": "DRL Production 01" }
```

Thành công `200 OK` với device representation mới.

### `POST /v1/devices/{id}/disable`

Không body. Thành công hoặc đã disabled: `204 No Content`.

### `POST /v1/devices/{deviceId}/api-keys`

Không body. JWT user; rate limit kế thừa bucket tạo API key của AUTH-003.

Thành công `201 Created`, `Location: /v1/devices/{deviceId}/api-keys/{id}`,
`Cache-Control: no-store`:

```json
{
  "id": "00000000-0000-0000-0000-000000000000",
  "deviceId": "00000000-0000-0000-0000-000000000000",
  "keyPrefix": "notify_a1b2c3d4e5f6",
  "key": "notify_<64-hex>",
  "status": "active",
  "createdAt": "2026-08-21T00:00:00Z"
}
```

### `GET /v1/devices/{deviceId}/api-keys?limit=50&cursor=<opaque>`

Trả `{ items, nextCursor }`, cùng metadata AUTH-003 cộng `deviceId`; không trả `key`, `keyHash` hoặc
`producerName`.

### `DELETE /v1/devices/{deviceId}/api-keys/{keyId}`

Thành công hoặc đã revoked: `204 No Content`.

### Compatibility `/v1/api-keys`

Giữ nguyên request/response/status của AUTH-003 trong v1. Mọi response thêm:

```http
Deprecation: true
Link: </v1/devices>; rel="successor-version"
```

## Error contract

| Trường hợp | HTTP | Code |
|---|---:|---|
| Body/query/cursor/UUID sai | 400 | `VALIDATION_FAILED` |
| JWT/API key thiếu, sai, revoked hoặc device disabled | 401 | `UNAUTHORIZED` |
| Non-owner dùng `scope=tenant` | 403 | `FORBIDDEN` |
| Device/key không có, không sở hữu hoặc cross-tenant | 404 | `NOT_FOUND` |
| Device đã disabled khi cấp key | 409 | `DEVICE_DISABLED` |
| Role không cho phép cấp API key | 409 | `DEVICE_ROLE_NOT_SOURCE` |
| Device đạt 10 active key | 409 | `DEVICE_API_KEY_LIMIT_REACHED` |
| Tenant đạt 50 active key | 409 | `API_KEY_LIMIT_REACHED` |
| Vượt rate limit | 429 | `RATE_LIMITED` |
| PostgreSQL tạm thời lỗi | 503 | `SERVICE_UNAVAILABLE` |
| Lỗi ngoài dự kiến | 500 | `INTERNAL_ERROR` |

## Data impact

Migration dự kiến `AddDevicesAndLinkApiKeys`:

```text
devices
  id uuid primary key
  tenant_id uuid not null references tenants(id) on delete restrict
  owner_admin_id uuid not null references admins(id) on delete restrict
  name varchar(100) not null
  normalized_legacy_name varchar(100) null
  role varchar(16) not null
  status varchar(16) not null
  created_at timestamptz not null
  updated_at timestamptz not null
  disabled_at timestamptz null

api_keys
  + device_id uuid references devices(id) on delete restrict
  producer_name giữ nguyên trong v1
```

Constraints/indexes:

- Check `role in ('source','both')`.
- Check `status in ('active','disabled')` và status/timestamp nhất quán.
- Index `(tenant_id, owner_admin_id, created_at desc)`.
- Index `(tenant_id, status, created_at desc)`.
- Unique partial `(tenant_id, normalized_legacy_name) where normalized_legacy_name is not null` để
  compatibility lookup không mơ hồ với device do legacy API quản lý; device API mới để null.
- Index `api_keys(device_id, status, created_at desc)` và `(tenant_id, device_id)`.
- Sau backfill, FK và `api_keys.device_id` chuyển NOT NULL trong cùng migration trước commit.

`Down()` xóa FK/index/cột `device_id`, sau đó xóa `devices`; không sửa/xóa API key, notification hoặc
delivery attempt cũ. Vì `producer_name` được giữ, phiên bản cũ đọc lại được sau rollback.

## Compatibility và rollout

1. Migration tạo/backfill device rồi thêm NOT NULL FK; không đổi contract cũ.
2. Rollout API/Worker cùng build; authentication đọc key kèm device status.
3. Smoke test key cũ gửi notification và HISTORY đọc được producer name cũ.
4. Client mới chuyển sang nested device API; client cũ tiếp tục `/v1/api-keys` trong ít nhất một chu
   kỳ release.
5. Feature sau mới ngừng dual-write/xóa `producer_name`; đó là breaking migration riêng.

## Acceptance criteria

- AC-01: user tạo device nhận `201`; tenant/owner lấy từ JWT, name/role validation lỗi không ghi DB.
- AC-02: user tạo nhiều device tên giống nhau được; UUID khác nhau và ownership đúng.
- AC-03: user chỉ get/list/rename/disable device mình; tenant owner dùng `scope=tenant` quản lý mọi
  device cùng tenant; cross-tenant/unauthorized owner nhận contract đúng.
- AC-04: list filter/status/cursor ổn định, không lặp/bỏ item và không rò key/secret.
- AC-05: rename chỉ đổi name/updatedAt; role, owner, key legacy producer name và history không đổi.
- AC-06: disable idempotent; device/key/history còn đọc được nhưng mọi key của device bị `401` ngay
  sau commit, không positive cache.
- AC-07: cấp key nested trả raw key đúng một lần; DB/list/log/error không chứa raw/hash/salt.
- AC-08: hai active key cùng device cùng xác thực ra một device ID; revoke một key không ảnh hưởng key kia.
- AC-09: nested list/revoke lọc cả tenant/device/ownership; key của device khác và cross-tenant trả 404.
- AC-10: request thứ 11 trên device hoặc thứ 51 trên tenant bị `409`; concurrent boundary không vượt trần.
- AC-11: machine principal có tenant/owner/device/key/role, giữ producer claim legacy và không có admin role.
- AC-12: migration backfill gom case-insensitive producer đúng BR-20, giữ nguyên mọi key byte/status/time
  và notification FK; mọi key cũ vẫn xác thực/gửi được sau upgrade.
- AC-13: compatibility POST/GET/DELETE giữ contract AUTH-003, có deprecation headers và ánh xạ
  normalized legacy device nhất quán, không tự chọn device được tạo từ API mới.
- AC-14: migration apply trên DB sạch và DB có nhiều key/notification; rollback/re-apply không mất dữ
  liệu và snapshot khớp model.
- AC-15: API key hoặc device không gọi được endpoint admin; JWT không bị nhận nhầm machine principal.
- AC-16: captured structured logs/metrics không chứa raw key, hash, password, notification content
  hoặc target.
- AC-17: format, build, unit, architecture và Docker Compose integration tests xanh.

## Planned files

```text
src/Notification.Domain/Devices/Device.cs
src/Notification.Domain/Devices/DeviceRole.cs
src/Notification.Domain/Devices/DeviceStatus.cs
src/Notification.Application/Devices/*
src/Notification.Application/Identity/Abstractions/IIdentityRepository.cs
src/Notification.Application/Identity/ApiKeys/*
src/Notification.Infrastructure/Persistence/Configurations/DeviceConfiguration.cs
src/Notification.Infrastructure/Persistence/Migrations/*_AddDevicesAndLinkApiKeys.cs
src/Notification.Infrastructure/Persistence/IdentityRepository.cs
src/Notification.Api/Authentication/ApiKeyAuthenticationHandler.cs
src/Notification.Api/Contracts/Devices/*
src/Notification.Api/Endpoints/Devices/*
src/Notification.Api/Endpoints/Identity/ApiKeyEndpoints.cs
src/Notification.Api/Program.cs
tests/Notification.Domain.Tests/Devices/*
tests/Notification.Application.Tests/Devices/*
tests/Notification.IntegrationTests/Devices/*
tests/Notification.IntegrationTests/Identity/ApiKeys/*
docs/SPECS.md
docs/features/v1/README.md
README.md
```

## Security review decisions requiring approval

- SR-01: UUID là public device ID; name không unique và không dùng xác thực.
- SR-02: device disabled làm key vô hiệu ở query auth tiếp theo nhưng không tự revoke key rows.
- SR-03: tối đa 10 active key/device và 50/tenant; kiểm tra concurrency trong transaction.
- SR-04: API legacy được giữ một chu kỳ, backfill theo normalized producer và giữ `producer_name`.
- SR-05: current `Admin` được dùng làm user owner; không đổi auth/schema user trong DEVICE-001.
- SR-06: disable là một chiều trong v1; không hard-delete hoặc re-enable.

## Open questions

Không còn câu hỏi chặn Review. Các quyết định contract, migration và security trên cần được duyệt bằng
`APPROVE DEVICE-001` hoặc sửa bằng `CHANGE DEVICE-001: ...`.
