# AUTH-002 — Đăng nhập, làm mới phiên, đăng xuất

Status: Verified
Selected: 2026-08-15
Approved: 2026-08-15
Verified: 2026-08-15

## Đọc nhanh

Feature cung cấp ba thao tác session:

```text
login → access token ngắn hạn + refresh token
refresh → thu hồi token cũ + cấp cặp token mới
logout → thu hồi refresh token được gửi
```

- Đăng nhập duy nhất bằng email đầy đủ đã normalize.
- Access token là JWT HS256; refresh token opaque và database chỉ lưu SHA-256 hash.
- Refresh token dùng một lần; hai request đồng thời chỉ một request thành công.
- Logout idempotent và không dùng deny-list cho access token.
- Token/password không được xuất hiện trong log hoặc database dưới dạng thô.

Có thể refactor issuer/generator/repository nhưng phải giữ rotation nguyên tử, claim tenant/admin/role, validation JWT,
cache headers và hành vi lỗi không tiết lộ tài khoản có tồn tại hay không.

## Outcome

Quản trị viên đăng nhập bằng email/mật khẩu, nhận access token ngắn hạn và refresh token có thể thu hồi;
mọi API quản trị về sau lấy `adminId`, `tenantId` và role từ identity đã xác thực.

## Actor

- Quản trị viên đang hoạt động, được tạo bởi AUTH-001.
- HTTP client giữ access token trong bộ nhớ an toàn và refresh token ngoài log/URL.

## Trigger

- `POST /v1/auth/login` với email và mật khẩu.
- `POST /v1/auth/refresh` với refresh token còn hiệu lực.
- `POST /v1/auth/logout` với Bearer access token và refresh token cần thu hồi.

## In scope

- Xác minh email/mật khẩu bằng password hasher của AUTH-001.
- Phát hành JWT access token HS256 và opaque refresh token sinh bằng CSPRNG.
- Lưu duy nhất SHA-256 hash của refresh token.
- Rotate refresh token sau mỗi lần refresh; mỗi token chỉ dùng thành công một lần.
- Thu hồi một refresh token khi logout.
- Authentication middleware và identity dùng chung cho các endpoint admin tiếp theo.
- Rate limit login theo IP; error contract không cho phép dò email.
- Migration `AddRefreshTokens` và Docker Compose integration test PostgreSQL thật.

## Out of scope

- Quên/đổi mật khẩu, xác minh email, MFA, SSO/OAuth.
- Quản lý nhiều admin hoặc khóa/mở khóa tài khoản.
- Danh sách thiết bị/phiên, đăng xuất tất cả thiết bị.
- Phân quyền ngoài role `owner`.
- Cookie/browser session; v1 trả token trong JSON cho API client.
- Thu hồi access token trước khi hết hạn.

## Preconditions

- PRE-01: AUTH-001 ở trạng thái Verified.
- PRE-02: admin và tenant chưa soft-delete.
- PRE-03: PostgreSQL sẵn sàng và migration đã chạy.

## Dependencies

AUTH-001.

## Tham chiếu

- Phạm vi sản phẩm: [PRODUCT.md](../../../PRODUCT.md).
- Dữ liệu: `admins` (đọc), `refresh_tokens` (ghi) — SPECS.md §6.
- Contract: `/v1/auth/login`, `/v1/auth/refresh`, `/v1/auth/logout` — SPECS.md §7.
- Error envelope, log và tenant boundary — CONVENTIONS.md §5–6, §11.

## Business rules

- BR-01: email được trim và lowercase invariant trước khi tra cứu; mật khẩu giữ nguyên ký tự caller gửi.
- BR-02: login sai email và sai mật khẩu đều trả cùng `401 INVALID_CREDENTIALS`, cùng hình dạng response;
  không tiết lộ tài khoản có tồn tại hay không.
- BR-03: password được kiểm tra bằng `IPasswordHasher`; không so sánh hash hoặc mật khẩu bằng logic tự viết.
- BR-04: access token là JWT ký HS256 bằng `JWT_SECRET` tối thiểu 32 byte, không chứa bí mật hoặc email.
- BR-05: JWT chứa `sub=adminId`, `tenant_id`, `role`, `jti`, `iss`, `aud`, `iat`, `nbf`, `exp`;
  thuật toán được allow-list cố định là HS256 khi validate.
- BR-06: access TTL mặc định 3600 giây; refresh TTL mặc định 604800 giây. Cấu hình phải là số dương,
  access TTL không vượt refresh TTL.
- BR-07: refresh token là 32 byte ngẫu nhiên từ CSPRNG, mã hóa base64url không padding; database chỉ lưu
  SHA-256 hash dạng byte, không lưu token thô.
- BR-08: login thành công tạo một refresh-token family mới. Refresh thành công thực hiện atomically:
  khóa bản ghi hiện tại, thu hồi nó, tạo token kế nhiệm cùng family và trả cặp token mới.
- BR-09: hai request đồng thời dùng cùng refresh token chỉ một request thành công. Request còn lại trả
  `401 INVALID_REFRESH_TOKEN`; không phát hành thêm token.
- BR-10: refresh token hết hạn, đã thu hồi, không tồn tại, hoặc thuộc admin/tenant đã xóa đều trả cùng
  `401 INVALID_REFRESH_TOKEN`.
- BR-11: logout idempotent: token hợp lệ được thu hồi; token đã thu hồi vẫn trả `204`. Token không đúng
  identity đang đăng nhập trả `401 INVALID_REFRESH_TOKEN`.
- BR-12: logout chỉ thu hồi refresh token được gửi; access token hiện tại tiếp tục có hiệu lực tới `exp`.
- BR-13: login áp fixed-window 10 request/IP/phút. Request thứ 11 trả `429 RATE_LIMITED` và `Retry-After`;
  IP khác có bucket độc lập. Refresh/logout không dùng bucket login.
- BR-14: password, access token, refresh token và token hash không xuất hiện trong structured log,
  exception public, metrics label hoặc correlation data.
- BR-15: authentication đọc tenant từ claim đã ký, sau đó xác nhận admin/tenant vẫn hoạt động; endpoint
  nghiệp vụ không nhận `tenantId` từ body/path/query để quyết định quyền.
- BR-16: mọi thời điểm lưu ở UTC; kiểm tra hết hạn dùng clock abstraction để test không phụ thuộc thời gian thật.

## Authorization

- Login và refresh là public vì caller chưa có access token hợp lệ.
- Logout yêu cầu `Authorization: Bearer <accessToken>` và refresh token trong JSON body.
- Authentication thành công tạo principal với `adminId`, `tenantId`, role `owner`; thiếu/sai/hết hạn trả
  `401 UNAUTHORIZED` và header `WWW-Authenticate: Bearer`.
- Không chấp nhận `adminId`, `tenantId` hoặc role do caller tự gửi.
- Refresh không cần access token; quyền được xác định từ bản ghi refresh token và admin liên quan.

## Public contract

### `POST /v1/auth/login`

Request:

```json
{
  "email": "admin@local.test",
  "password": "12345678"
}
```

Thành công: `200 OK`, `Cache-Control: no-store`, `Pragma: no-cache`.

```json
{
  "tokenType": "Bearer",
  "accessToken": "<jwt>",
  "accessTokenExpiresIn": 3600,
  "refreshToken": "<opaque-token>",
  "refreshTokenExpiresIn": 604800,
  "admin": {
    "id": "00000000-0000-0000-0000-000000000000",
    "tenantId": "00000000-0000-0000-0000-000000000000",
    "role": "owner"
  }
}
```

### `POST /v1/auth/refresh`

Request:

```json
{ "refreshToken": "<opaque-token>" }
```

Thành công: `200 OK`, cùng token envelope của login và bắt buộc trả refresh token mới. Refresh token
cũ mất hiệu lực trước khi response thành công được gửi.

### `POST /v1/auth/logout`

```http
Authorization: Bearer <access-token>
Content-Type: application/json
```

```json
{ "refreshToken": "<opaque-token>" }
```

Thành công hoặc đã logout trước đó: `204 No Content`.

| Trường hợp | HTTP | Mã lỗi |
|---|---:|---|
| Request sai schema/độ dài | 400 | `VALIDATION_FAILED` |
| Email/mật khẩu sai | 401 | `INVALID_CREDENTIALS` |
| Access token thiếu/sai/hết hạn | 401 | `UNAUTHORIZED` |
| Refresh token sai/hết hạn/đã dùng | 401 | `INVALID_REFRESH_TOKEN` |
| Vượt rate limit login | 429 | `RATE_LIMITED` |
| PostgreSQL tạm thời không dùng được | 503 | `SERVICE_UNAVAILABLE` |
| Lỗi ngoài dự kiến | 500 | `INTERNAL_ERROR` |

Mọi error dùng envelope chung và correlation ID. Response chứa token luôn có cache headers nêu trên.

## Data impact

Migration `AddRefreshTokens` tạo bảng:

```text
refresh_tokens
  id uuid primary key
  admin_id uuid not null references admins(id) on delete restrict
  family_id uuid not null
  token_hash bytea not null
  expires_at timestamptz not null
  revoked_at timestamptz null
  replaced_by_id uuid null references refresh_tokens(id) on delete restrict
  created_at timestamptz not null
```

Indexes/constraints:

- Unique index `ux_refresh_tokens_token_hash` trên `token_hash`.
- Index `ix_refresh_tokens_admin_family` trên `(admin_id, family_id)`.
- Index `ix_refresh_tokens_expires_at_active` trên `expires_at` khi `revoked_at is null` để hỗ trợ cleanup sau này.
- Check `expires_at > created_at`.
- Check `revoked_at is null or revoked_at >= created_at`.
- `Down()` xóa `refresh_tokens`; integration test phải rollback rồi apply lại trên PostgreSQL sạch.

Không thêm token thô vào entity, seed, fixture snapshot hoặc migration data.

## Acceptance criteria

- AC-01: credential seed AUTH-001 đăng nhập được; response đúng contract và JWT signature/issuer/audience/claims hợp lệ.
- AC-02: sai email và sai password trả response `401 INVALID_CREDENTIALS` không thể phân biệt.
- AC-03: email được normalize; password không bị trim; validation biên email/password trả `400` và không ghi DB.
- AC-04: database chỉ chứa SHA-256 refresh hash; token/hash/password không xuất hiện trong response ngoài
  hai token thô được phát hành, hoặc trong captured log/error.
- AC-05: refresh hợp lệ trả access và refresh token mới; token cũ bị thu hồi và không dùng lại được.
- AC-06: hai refresh đồng thời với cùng token có đúng một thành công và một `401`; database chỉ có một successor.
- AC-07: refresh hết hạn, giả, đã thu hồi hoặc của admin/tenant đã xóa đều trả `401 INVALID_REFRESH_TOKEN`.
- AC-08: logout đúng identity trả `204`, chạy lại vẫn `204`; refresh sau logout thất bại.
- AC-09: logout token thuộc admin khác trả `401` và không thu hồi token của admin đó.
- AC-10: access token hợp lệ tạo đúng principal; token sai signature, algorithm, issuer, audience hoặc hết hạn
  đều bị từ chối với `WWW-Authenticate: Bearer`.
- AC-11: request login thứ 11 cùng IP/phút trả `429`, có `Retry-After`; bucket IP khác độc lập.
- AC-12: cấu hình JWT thiếu/yếu hoặc TTL không hợp lệ làm API fail-fast mà không mở cổng HTTP.
- AC-13: migration apply, rollback và re-apply được trên PostgreSQL thật; unique/FK/check constraints hoạt động.
- AC-14: format, build, unit, architecture, HTTP integration và Docker Compose test xanh; dependency audit sạch.

## Planned files

```text
src/Notification.Domain/Identity/RefreshToken.cs
src/Notification.Application/Abstractions/Security/IAccessTokenIssuer.cs
src/Notification.Application/Abstractions/Security/IRefreshTokenGenerator.cs
src/Notification.Application/Abstractions/Time/IClock.cs
src/Notification.Application/Identity/Login/*
src/Notification.Application/Identity/RefreshSession/*
src/Notification.Application/Identity/Logout/*
src/Notification.Application/Identity/Abstractions/IIdentityRepository.cs
src/Notification.Infrastructure/Configuration/AuthOptions.cs
src/Notification.Infrastructure/Persistence/Configurations/RefreshTokenConfiguration.cs
src/Notification.Infrastructure/Persistence/Migrations/*_AddRefreshTokens.cs
src/Notification.Infrastructure/Persistence/IdentityRepository.cs
src/Notification.Infrastructure/Security/JwtAccessTokenIssuer.cs
src/Notification.Infrastructure/Security/SecureRefreshTokenGenerator.cs
src/Notification.Api/Contracts/Identity/*
src/Notification.Api/Endpoints/Identity/AuthEndpoints.cs
src/Notification.Api/Program.cs
deploy/docker/compose.yml
.env.example
README.md
tests/Notification.Application.Tests/Identity/*
tests/Notification.IntegrationTests/Identity/*
scripts/test-integration.ps1
docs/SPECS.md
docs/features/v1/README.md
docs/features/v1/02-identity/AUTH-002-dang-nhap.md
```

## Security review decisions requiring approval

- SR-01: access token HS256, TTL 3600 giây; refresh token TTL 7 ngày.
- SR-02: refresh token opaque, lưu SHA-256, rotate một lần dùng; logout chỉ thu hồi token được gửi.
- SR-03: logout yêu cầu cả access token và refresh token; access token không có deny-list và hết hiệu lực tự nhiên.
- SR-04: không tự động thu hồi cả token family khi phát hiện reuse ở v1; reuse luôn thất bại và được ghi security event
  không chứa token. Chức năng “logout all devices” để ngoài scope.

## Open questions

Không còn câu hỏi kỹ thuật chặn Review. Bốn quyết định security ở trên cần được duyệt tường minh bằng
`APPROVE AUTH-002` hoặc thay đổi bằng `CHANGE AUTH-002: ...`.
