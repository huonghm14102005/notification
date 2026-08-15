# TMPL-001 — Mẫu nội dung: tạo, xem, sửa và render biến

Status: Verified
Selected: 2026-08-15
Approved: 2026-08-15
Verified: 2026-08-15

## Outcome

Admin quản lý được mẫu email plain-text theo tenant. Application có một renderer thuần túy để dựng subject/body từ
mẫu active và dữ liệu biến; kết quả sau này được intake lưu thành snapshot độc lập với thay đổi của mẫu.

## Actor

- Quản trị viên `owner` quản lý mẫu bằng JWT.
- Intake application handler dùng template reader/renderer nội bộ ở INTK-003; TMPL-001 chưa mở endpoint gửi.

## Trigger

- Admin tạo, liệt kê, xem hoặc sửa mẫu.
- Application gọi renderer với một template active và map dữ liệu biến.

## In scope

- `GET/POST /v1/templates`, `GET/PATCH /v1/templates/{key}`.
- Vòng đời `draft → active → retired`.
- Khai báo biến và placeholder `{{variableName}}` trong subject/body.
- Render xác định, không I/O, báo rõ biến thiếu/thừa.
- Tenant isolation, validation, soft retirement và migration PostgreSQL.

## Out of scope

- Dùng template trong notification intake — INTK-003.
- HTML, Markdown conversion, attachment, layout/template inheritance.
- Preview endpoint hoặc gửi thử template.
- Version history, rollback template, clone/import/export.
- Conditional, loop, expression, function hoặc nested object traversal.
- Xóa cứng hoặc tái sử dụng key đã retired.

## Preconditions

- PRE-01: AUTH-002 Verified; admin/tenant lấy từ JWT hợp lệ.
- PRE-02: PostgreSQL sẵn sàng và migration trước đó đã áp dụng.

## Dependencies

AUTH-002.

## Tham chiếu

- Must-have: M-06 ([MVP.md](../../../MVP.md)).
- Invariant I9/I10: [domain-map.md](../../../domain-map.md).
- Dữ liệu/contract: `templates`, `/v1/templates` — SPECS.md §6–§8.

## Business rules

- BR-01: `key` được trim/lowercase, dài 3..63, theo regex `^[a-z0-9](?:[a-z0-9]|-(?!-)){1,61}[a-z0-9]$`;
  bất biến và duy nhất trong toàn bộ lịch sử tenant, kể cả retired.
- BR-02: create luôn tạo `status=draft`. Caller không đặt ID, tenant, timestamp hoặc status khi tạo.
- BR-03: `subject` dài 1..998 sau trim, không chứa CR/LF/NUL/control character. `body` dài 1..100000, giữ nguyên
  khoảng trắng/newline, không chứa NUL hoặc control character ngoài TAB/LF/CR.
- BR-04: mỗi tên biến dài 1..64, case-sensitive, theo `^[A-Za-z][A-Za-z0-9_]{0,63}$`; danh sách tối đa 50,
  không trùng và lưu theo thứ tự ordinal tăng dần để response/render ổn định.
- BR-05: placeholder có đúng cú pháp `{{variableName}}`; không cho khoảng trắng trong dấu ngoặc, escape, nested token
  hoặc delimiter chưa đóng.
- BR-06: tập placeholder xuất hiện trong subject/body phải bằng đúng tập `variables`: thiếu khai báo, khai báo nhưng
  không dùng hoặc token sai cú pháp đều làm create/PATCH trả `400 TEMPLATE_SYNTAX_INVALID`.
- BR-07: cùng một placeholder có thể xuất hiện nhiều lần và mọi vị trí được thay bằng cùng giá trị.
- BR-08: renderer yêu cầu data là map string→string có đúng các key đã khai báo. Thiếu key trả
  `TEMPLATE_VARIABLE_MISSING` cùng danh sách tên thiếu; key thừa trả `TEMPLATE_VARIABLE_UNKNOWN`.
- BR-09: giá trị biến tối đa 10000 ký tự. Khi biến xuất hiện trong subject, giá trị không được có CR/LF/NUL/control;
  trong body cho phép TAB/LF/CR nhưng không cho NUL/control khác. Render vượt subject 998 hoặc body 100000 trả
  `TEMPLATE_RENDER_TOO_LARGE`.
- BR-10: thay thế một lượt theo token của template; nội dung `{{...}}` nằm trong giá trị biến không được render tiếp.
- BR-11: renderer là logic thuần, không I/O, không truy cập clock/database và với cùng input luôn cho cùng output.
- BR-12: chỉ template `active` được application reader trả cho intake. Draft/retired/cross-tenant đều được che thành
  `TEMPLATE_NOT_FOUND` ở contract nội bộ.
- BR-13: chuyển trạng thái chỉ cho phép `draft→active` và `active→retired`. Retired là terminal và không sửa được;
  transition khác trả `409 TEMPLATE_INVALID_STATE`.
- BR-14: subject/body/variables được sửa khi draft hoặc active. Sửa active có hiệu lực cho intake mới; notification đã
  nhận phải giữ snapshot riêng ở INTK-003/I10.
- BR-15: PATCH merge semantics; field vắng giữ nguyên, JSON null không hợp lệ, body rỗng/field lạ trả validation error.
- BR-16: create/update/retire áp tổng rate limit 30 request/admin/phút; GET không tính vào quota.
- BR-17: list sắp xếp `createdAt desc, id desc`, cursor opaque; mặc định 50, tối đa 100; filter status tùy chọn.
- BR-18: timestamp UTC; mọi update thay `updated_at`, create đặt `created_at=updated_at`.


## Authorization

- Tất cả endpoint TMPL-001 yêu cầu JWT policy `Admin`; API key machine bị từ chối.
- `tenantId` lấy từ principal, không nhận từ body/path/query.
- Lookup/list/update luôn lọc tenant trước key/status; key tenant khác và key giả cùng trả `404 NOT_FOUND`.
- Nội dung template không được ghi vào log request/error; log mutation chỉ chứa tenantId, adminId, templateId/key và status.

## Public contract

### `POST /v1/templates`

```json
{
  "key": "diem-hoc-ky",
  "subject": "Kết quả học kỳ của {{studentName}}",
  "body": "Chào {{studentName}},\nĐiểm của bạn là {{score}}.",
  "variables": ["studentName", "score"]
}
```

Thành công: `201 Created`, `Location: /v1/templates/diem-hoc-ky`.

### Template response

```json
{
  "id": "0198...",
  "key": "diem-hoc-ky",
  "subject": "Kết quả học kỳ của {{studentName}}",
  "body": "Chào {{studentName}},\nĐiểm của bạn là {{score}}.",
  "variables": ["score", "studentName"],
  "status": "draft",
  "createdAt": "2026-08-15T07:00:00Z",
  "updatedAt": "2026-08-15T07:00:00Z"
}
```

### `GET /v1/templates?status=<draft|active|retired>&limit=50&cursor=<opaque>`

`status` tùy chọn; bỏ trống trả mọi trạng thái. Response:

```json
{ "items": [/* template response */], "nextCursor": null }
```

### `GET /v1/templates/{key}`

Key được normalize như BR-01. Trả `200` với template response hoặc `404 NOT_FOUND`.

### `PATCH /v1/templates/{key}`

```json
{
  "subject": "Kết quả mới của {{studentName}}",
  "body": "Chào {{studentName}},\nĐiểm: {{score}}.",
  "variables": ["studentName", "score"],
  "status": "active"
}
```

Mọi field đều tùy chọn nhưng ít nhất một field phải có. `key` không sửa được. Thành công trả `200` với state sau commit.

### Error contract

| Trường hợp | HTTP | Code |
|---|---:|---|
| Field sai kiểu/biên, null, body rỗng, field lạ | 400 | `VALIDATION_FAILED` |
| Placeholder và variables không khớp/cú pháp sai | 400 | `TEMPLATE_SYNTAX_INVALID` |
| Key trùng trong tenant | 409 | `TEMPLATE_KEY_EXISTS` |
| Transition sai hoặc sửa retired | 409 | `TEMPLATE_INVALID_STATE` |
| Key không tồn tại/cross-tenant | 404 | `NOT_FOUND` |
| Vượt rate limit mutation | 429 | `RATE_LIMITED` |

Các lỗi renderer nội bộ cho INTK-003: `TEMPLATE_NOT_FOUND`, `TEMPLATE_VARIABLE_MISSING`,
`TEMPLATE_VARIABLE_UNKNOWN`, `TEMPLATE_RENDER_TOO_LARGE`.

## Application contract

```text
ITemplateReader.FindActiveAsync(tenantId, normalizedKey) -> TemplateDefinition | TEMPLATE_NOT_FOUND
ITemplateRenderer.Render(template, IReadOnlyDictionary<string,string> data) -> RenderedContent(subject, body)
```

`TemplateDefinition`/`RenderedContent` là type Application/Domain, không rò EF/JSONB/PostgreSQL. Renderer không tự lookup.

## Data impact

Migration `AddTemplates` tạo:

```text
templates
  id          uuid pk
  tenant_id   uuid not null fk tenants(id) on delete restrict
  key         varchar(63) not null
  subject     varchar(998) not null
  body        text not null
  variables   jsonb not null
  status      varchar(16) not null
  created_at  timestamptz not null
  updated_at  timestamptz not null
```

- Unique `ux_templates_tenant_key` trên `(tenant_id,key)`, áp cả retired.
- Index `ix_templates_tenant_status_created` trên `(tenant_id,status,created_at desc)`.
- Check status trong `draft|active|retired`, body length 1..100000, `jsonb_typeof(variables)='array'`.
- Validation sâu của variables/placeholder nằm ở Domain/Application; database giữ invariant cấu trúc cơ bản.
- `Down()` xóa `templates`; phải rollback/reapply được trên PostgreSQL sạch.

## Acceptance criteria

- AC-01: admin tạo draft hợp lệ, key/variables được normalize và response/Location đúng contract.
- AC-02: key trùng trong cùng tenant trả `409`; tenant khác dùng cùng key được.
- AC-03: subject/body/variable vượt biên, control/header injection hoặc placeholder sai trả lỗi và không ghi dữ liệu.
- AC-04: list/filter/cursor ổn định, chỉ trả template đúng tenant; GET key normalize đúng.
- AC-05: PATCH merge atomically, không đổi key; body rỗng/null/field lạ không thay dữ liệu.
- AC-06: chỉ transition `draft→active→retired`; retired không sửa/tái kích hoạt.
- AC-07: renderer thay mọi occurrence, case-sensitive, một lượt và deterministic.
- AC-08: thiếu/thừa variable trả code/danh sách ổn định; không render chuỗi rỗng ngầm.
- AC-09: giá trị subject có CRLF/control và output vượt biên bị từ chối.
- AC-10: reader chỉ resolve active đúng tenant; draft/retired/cross-tenant trả `TEMPLATE_NOT_FOUND`.
- AC-11: API key machine không gọi CRUD; mutation thứ 31/admin/phút trả `429` và `Retry-After`.
- AC-12: response/log/error không lộ body/subject/variable values ngoài response GET/CRUD được admin yêu cầu.
- AC-13: migration `AddTemplates` up/down/up sạch trong Docker Compose PostgreSQL thật.

## Test mapping

| AC | Test dự kiến |
|---|---|
| AC-01..06 | Domain/application/API integration tests với hai tenant |
| AC-07..09 | Unit tests thuần cho renderer và syntax validator |
| AC-10 | Repository reader tests active/draft/retired/cross-tenant |
| AC-11..12 | Authorization/rate-limit/log safety integration tests |
| AC-13 | `scripts/test-integration.ps1` dùng Docker Compose |

## Planned files

```text
src/Notification.Domain/Templates/*
src/Notification.Application/Templates/*
src/Notification.Infrastructure/Persistence/Configurations/TemplateConfiguration.cs
src/Notification.Infrastructure/Persistence/TemplateRepository.cs
src/Notification.Infrastructure/Persistence/Migrations/*_AddTemplates.cs
src/Notification.Api/Contracts/Templates/*
src/Notification.Api/Endpoints/Templates/TemplateEndpoints.cs
src/Notification.Api/Program.cs
tests/Notification.Domain.Tests/Templates/*
tests/Notification.Application.Tests/Templates/*
tests/Notification.IntegrationTests/Templates/*
scripts/test-integration.ps1
docs/features/v1/04-template/TMPL-001-mau-noi-dung.md
docs/features/v1/README.md
README.md
```

## Security review

- SR-01: tenantId chỉ từ JWT; mọi query/index bắt đầu bằng tenant.
- SR-02: subject và biến dùng trong subject chặn CR/LF/control để chống header injection.
- SR-03: renderer không đánh giá expression/code và không render đệ quy giá trị biến.
- SR-04: giới hạn kích thước/count ngăn payload amplification; mutation có rate limit.
- SR-05: template content không được log tự động dù admin có quyền đọc qua endpoint.

## Open questions

Không có. Đề xuất duyệt: key không tái sử dụng sau retirement; create luôn draft; chỉ active mới dùng được cho intake.

## Verification evidence

- `dotnet build Notification.slnx --no-restore`: pass, 0 warning/error.
- `dotnet test Notification.slnx --no-build --no-restore`: pass 37/37 test.
- `scripts/test-integration.ps1`: pass với Docker Compose, gồm CRUD/vòng đời/cô lập tenant và rollback/apply migration.
