# AUTH-001 — Đăng ký tổ chức kèm quản trị viên đầu tiên

Status: Verified
Selected: 2026-08-15
Approved: 2026-08-15
Verified: 2026-08-15

## Outcome

Một tổ chức mới được tạo cùng quản trị viên đầu tiên trong một giao dịch nguyên tử; môi trường
Development/Test có một tài khoản cố định để kiểm thử các feature quản trị tiếp theo.

## Actor

- Người khai trương một tổ chức, chưa có tài khoản.
- Development seed runner khi khởi động môi trường local/test.

## Trigger

- Gọi `POST /v1/tenants/register` với tên tổ chức, slug, email và mật khẩu quản trị viên.
- Khởi động API trong Development/Test với `SEED_TEST_ADMIN=true`.

## In scope

- Entity và bảng `tenants`, `admins`.
- Tạo tenant và admin đầu tiên trong cùng PostgreSQL transaction.
- Chuẩn hóa email và slug trước khi kiểm tra trùng.
- Băm mật khẩu bằng `PasswordHasher<TUser>`; không tự cài thuật toán mật mã.
- Role duy nhất ở feature này là `owner`.
- Migration đầu tiên cho identity và EF Core design-time factory.
- Public registration endpoint có giới hạn 5 yêu cầu/IP/giờ.
- Seed idempotent cho tài khoản local/test cố định.
- Ghi tài khoản test trong README theo yêu cầu của chủ dự án.
- Integration test trên PostgreSQL thật qua Docker Compose.

## Out of scope

- Đăng nhập, access/refresh token và logout (AUTH-002).
- API key (AUTH-003).
- Thêm, sửa, khóa hoặc xoá quản trị viên.
- Xác minh email, quên/đổi mật khẩu và MFA.
- Giao diện web.
- Cho phép caller tự chỉ định role.
- Chạy seed ở Staging/Production.

## Preconditions

- PRE-01: OPS-001 ở trạng thái Verified.
- PRE-02: PostgreSQL sẵn sàng và migration đã chạy.
- PRE-03: slug và email chuẩn hóa chưa tồn tại.

## Dependencies

OPS-001.

## Tham chiếu

- Must-have: M-01, M-02 ([MVP.md](../../../MVP.md)).
- Dữ liệu: `tenants`, `admins` — SPECS.md §6.
- Contract: `POST /v1/tenants/register` — SPECS.md §7.
- Quy ước tenant và transaction: CONVENTIONS.md §7–9.

## Business rules

- BR-01: `name` được trim, dài 2..200 ký tự và không được chỉ gồm khoảng trắng.
- BR-02: `slug` được trim, chuyển lowercase, dài 3..63 ký tự; chỉ gồm `a-z`, `0-9` và dấu `-`;
  không bắt đầu/kết thúc bằng `-`, không có hai dấu `-` liên tiếp.
- BR-03: `slug` là duy nhất không phân biệt hoa thường; xung đột trả `TENANT_SLUG_EXISTS`.
- BR-04: email được trim và lowercase invariant, dài tối đa 254 ký tự và phải đúng định dạng email.
- BR-05: email quản trị viên là duy nhất toàn hệ thống để AUTH-002 đăng nhập chỉ bằng email; xung đột
  trả `ADMIN_EMAIL_EXISTS`.
- BR-06: mật khẩu dài 8..128 ký tự. Không trim hoặc tự thay đổi mật khẩu; khoảng trắng là một phần
  của mật khẩu nếu caller gửi.
- BR-07: chỉ lưu password hash; request, validation error, structured log và database không chứa
  mật khẩu thô.
- BR-08: tenant và admin `owner` được insert trong cùng transaction. Một insert thất bại phải rollback
  cả hai, không để tenant mồ côi.
- BR-09: client không được gửi `id`, `role`, `tenantId`, timestamp hoặc password hash.
- BR-10: lỗi unique constraint được ánh xạ về mã lỗi nghiệp vụ ổn định, không trả tên constraint/SQL.
- BR-11: seed gọi cùng application use case với registration; không ghi trực tiếp bằng SQL và không
  tạo bản ghi trùng khi chạy lại.
- BR-12: seed test chỉ được chạy khi environment là `Development` hoặc `Test`. Nếu
  `SEED_TEST_ADMIN=true` ở environment khác, ứng dụng fail-fast trước khi mở cổng HTTP.
- BR-13: dữ liệu seed cố định: tenant `Test Organization`, slug `test-organization`, email
  `admin@local.test`, mật khẩu `12345678`, role `owner`.
- BR-14: do credential seed được công khai trong repository, deployment ngoài local/test phải đặt
  `SEED_TEST_ADMIN=false` hoặc bỏ biến; không được đổi cơ chế kiểm tra environment bằng cấu hình.
- BR-15: endpoint registration áp fixed-window rate limit 5 request/IP/giờ. Vượt giới hạn trả `429`
  và `Retry-After`; request bị giới hạn không chạm database.

## Authorization

- Endpoint registration công khai vì actor chưa có identity.
- Caller không thể chọn tenant ID, admin ID hoặc role.
- Mọi endpoint đọc tenant/admin vẫn nằm ngoài AUTH-001; feature này chưa cấp session.
- Seed là bootstrap nội bộ, không có HTTP endpoint và không chạy ngoài Development/Test.

## Public contract

### `POST /v1/tenants/register`

Request:

```json
{
  "tenantName": "Trường Đại học Hàng hải Việt Nam",
  "tenantSlug": "vimaru",
  "adminEmail": "admin@example.edu.vn",
  "adminPassword": "a-strong-password"
}
```

Thành công: HTTP `201 Created`.

```json
{
  "tenant": {
    "id": "00000000-0000-0000-0000-000000000000",
    "name": "Trường Đại học Hàng hải Việt Nam",
    "slug": "vimaru"
  },
  "admin": {
    "id": "00000000-0000-0000-0000-000000000000",
    "email": "admin@example.edu.vn",
    "role": "owner"
  }
}
```

Response có header `Location: /v1/tenants/{tenantId}` dù endpoint đọc tenant chưa thuộc feature này.
Không trả password hoặc password hash.

| Trường hợp | HTTP | Mã lỗi |
|---|---:|---|
| Request sai schema/rule | 400 | `VALIDATION_FAILED` |
| Slug đã tồn tại | 409 | `TENANT_SLUG_EXISTS` |
| Email đã tồn tại | 409 | `ADMIN_EMAIL_EXISTS` |
| Vượt rate limit | 429 | `RATE_LIMITED` |
| PostgreSQL tạm thời không dùng được | 503 | `SERVICE_UNAVAILABLE` |
| Lỗi ngoài dự kiến | 500 | `INTERNAL_ERROR` |

Error envelope tuân theo CONVENTIONS §5 và luôn kèm correlation ID ở header.

## Data impact

Migration `InitialIdentity` tạo:

```text
tenants
  id uuid primary key
  name varchar(200) not null
  slug varchar(63) not null
  created_at timestamptz not null
  updated_at timestamptz not null
  deleted_at timestamptz null

admins
  id uuid primary key
  tenant_id uuid not null references tenants(id)
  email varchar(254) not null
  password_hash text not null
  role varchar(32) not null check (role in ('owner'))
  created_at timestamptz not null
  updated_at timestamptz not null
  deleted_at timestamptz null
```

Indexes/constraints:

- Unique index trên `tenants.slug` khi `deleted_at is null`; application luôn lưu slug đã lowercase.
- Unique index trên `admins.email` khi `deleted_at is null`; application luôn lưu email đã lowercase.
- Index `admins(tenant_id, email)` để ép access path theo tenant cho feature sau.
- Không cascade delete tenant trong MVP; soft delete được xử lý ở feature tương lai.

EF entity không được rò ra API/Application. Migration chạy trước API/Worker, không tự động chạy khi
mọi replica khởi động.

## Acceptance criteria

- AC-01: request hợp lệ tạo đúng một tenant và một admin `owner`, trả `201`, `Location` và response
  không có password/hash.
- AC-02: tên, slug và email được chuẩn hóa đúng BR-01..05 trước khi lưu/trả.
- AC-03: input sai ở từng biên name/slug/email/password trả `400` và không tạo bản ghi.
- AC-04: slug trùng khác casing trả `409 TENANT_SLUG_EXISTS`; không tạo admin/tenant thừa.
- AC-05: email trùng khác casing trả `409 ADMIN_EMAIL_EXISTS`; không tạo tenant mồ côi.
- AC-06: gây lỗi insert admin trong integration test rollback cả transaction.
- AC-07: database chỉ chứa password hash; verify `12345678` thành công qua password hasher nhưng
  hash không bằng mật khẩu và hai lần hash không cần giống nhau.
- AC-08: response, captured JSON log và exception public không chứa password request/hash, SQL hoặc
  tên constraint.
- AC-09: request thứ 6 từ cùng IP trong một giờ trả `429`, có `Retry-After` và không chạm use case;
  IP khác có bucket độc lập.
- AC-10: seed Development/Test tạo đúng credential BR-13 và chạy hai lần vẫn chỉ có một tenant/admin.
- AC-11: `SEED_TEST_ADMIN=true` ở Production làm API fail-fast; tắt seed thì Production khởi động
  bình thường và không có test account.
- AC-12: migration áp dụng được trên PostgreSQL sạch; rollback/forward-fix procedure được kiểm tra;
  index unique và foreign key hoạt động thật.
- AC-13: repository methods nhận tenant ID ở vị trí đầu với mọi truy vấn tenant-scoped; architecture
  test chặn API dùng DbContext trực tiếp.
- AC-14: README ghi rõ credential chỉ dành cho local/test và cảnh báo không bật seed ở production.
- AC-15: format, build, unit, architecture, integration và Docker Compose test đều xanh; dependency
  audit không có vulnerability đã biết.

## Planned files

```text
Directory.Packages.props
src/Notification.Domain/Identity/Tenant.cs
src/Notification.Domain/Identity/Admin.cs
src/Notification.Domain/Identity/AdminRole.cs
src/Notification.Application/Identity/RegisterTenant/*
src/Notification.Application/Identity/Abstractions/IIdentityRepository.cs
src/Notification.Application/Abstractions/Security/IPasswordHasher.cs
src/Notification.Infrastructure/Persistence/NotificationDbContext.cs
src/Notification.Infrastructure/Persistence/Configurations/TenantConfiguration.cs
src/Notification.Infrastructure/Persistence/Configurations/AdminConfiguration.cs
src/Notification.Infrastructure/Persistence/Migrations/*_InitialIdentity.cs
src/Notification.Infrastructure/Persistence/IdentityRepository.cs
src/Notification.Infrastructure/Security/AspNetPasswordHasher.cs
src/Notification.Infrastructure/Bootstrap/TestAdminSeeder.cs
src/Notification.Api/Endpoints/Identity/RegisterTenantEndpoint.cs
src/Notification.Api/Contracts/Identity/RegisterTenantRequest.cs
src/Notification.Api/Contracts/Identity/RegisterTenantResponse.cs
src/Notification.Api/Errors/*
src/Notification.Api/Program.cs
src/Notification.Api/appsettings.Development.json
tests/Notification.Domain.Tests/Identity/*
tests/Notification.Application.Tests/Identity/*
tests/Notification.IntegrationTests/Identity/*
tests/Notification.ArchitectureTests/*
deploy/docker/compose.yml
scripts/test-integration.ps1
.env.example
README.md
docs/SPECS.md
docs/features/v1/02-identity/AUTH-001-dang-ky-to-chuc.md
```

## Open questions

Không có. Credential local/test công khai đã được chủ dự án chấp nhận rõ ràng; cơ chế chặn seed ở
Production là điều kiện bắt buộc và không được nới lỏng trong implementation.
