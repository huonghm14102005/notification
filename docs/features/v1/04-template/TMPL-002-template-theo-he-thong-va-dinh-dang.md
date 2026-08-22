# TMPL-002 — Template theo hệ thống gửi và định dạng

Status: Review
Selected: 2026-08-22
Dependencies: TMPL-001, DEVICE-001, CHAN-001

## Đọc nhanh

Feature này xác định template của hệ thống nào, dùng cho ai và có định dạng gì:

```text
tenant/source device → templateCode + version → user/system → text/HTML
```

Source gửi `templateCode` và dữ liệu biến. Server chọn đúng template, render rồi lưu snapshot; sửa hoặc publish
template mới không làm lịch sử notification cũ thay đổi.

## Phạm vi

- Template scope `tenant` dùng chung hoặc scope `source` thuộc một source device.
- Định danh gửi bằng `templateCode`; admin quản lý chính xác từng version bằng UUID.
- Audience `user|system` để phân loại, không tự chọn recipient/channel hay cấp quyền.
- Email có plain text, HTML hoặc cả hai phần MIME alternative.
- Draft được sửa; version đã publish là bất biến. Muốn sửa phải clone thành version kế tiếp.
- HTML-escape mọi giá trị biến; không hỗ trợ raw/trusted HTML trong v1.
- Chưa dùng template trong API gửi notification; phần đó thuộc `INTK-003`.

Không làm rich editor, attachment, layout inheritance, Markdown, loop, điều kiện, expression, import/export hoặc
rollback version.

## Business rules

1. `templateCode` thay tên `key` ở contract mới nhưng giữ cách normalize/regex của TMPL-001.
2. Family được định danh bởi `(tenantId, scope, sourceDeviceId, templateCode)`.
3. Scope `tenant` bắt buộc `sourceDeviceId=null`. Scope `source` bắt buộc device cùng tenant, role `source|both`.
4. Hai source có thể dùng cùng code; template tenant và source cũng có thể trùng code.
5. Version bắt đầu từ 1, tăng liên tục trong family và do server cấp.
6. Mỗi family chỉ có tối đa một draft và một active version.
7. Publish draft và retire active cũ trong cùng transaction. Active/retired không được sửa hoặc tái kích hoạt.
8. Tạo version mới bằng cách clone active mới nhất thành draft kế tiếp.
9. Audience bắt buộc `user|system`, là metadata quản trị; cả hai đều dùng được text/HTML.
10. Không nhận field `format`. Có `textBody` là plain text, có `htmlBody` là HTML, có cả hai là multipart.
11. Phải có ít nhất một body. Mỗi body dài 1..100000; tổng kết quả render tối đa 150000 ký tự.
12. Placeholder/variables giữ cú pháp và giới hạn của TMPL-001. Tập biến dùng trong mọi phần phải đúng khai báo.
13. Biến trong HTML luôn encode `& < > " '`. Plain text thay nguyên văn nhưng vẫn chặn control character nguy hiểm.
14. Renderer chỉ thay một lượt; token nằm trong giá trị biến không được render lần hai.
15. List hỗ trợ filter `scope`, `sourceDeviceId`, `audience`, `status` và cursor như TMPL-001.
16. Lookup luôn lọc tenant trước. Device/template sai tenant trả cùng `404 NOT_FOUND`.
17. Không log subject, body hoặc giá trị biến.

## Cách chọn template khi gửi

`INTK-003` sẽ dùng quy tắc cố định:

1. Tìm active template scope `source` của device đang xác thực.
2. Nếu không có, tìm active template scope `tenant` cùng code.
3. Không có cả hai thì trả `TEMPLATE_NOT_FOUND`.

Source-specific được ưu tiên để một hệ thống tuỳ biến nội dung mà không ảnh hưởng hệ thống khác. Notification lưu
`templateId`, version và snapshot subject/text/html đã render.

## Authorization

- CRUD yêu cầu JWT policy `Admin`; tenant chỉ lấy từ JWT.
- Owner quản lý template tenant và template của mọi source device cùng tenant.
- API key machine không gọi CRUD template.
- UUID không thay thế kiểm tra tenant và device role.

## Public contract

### Tạo family và draft version 1

`POST /v1/templates`

```json
{
  "templateCode": "score-result",
  "scope": "source",
  "sourceDeviceId": "0198...",
  "audience": "user",
  "subject": "Kết quả của {{studentName}}",
  "textBody": "Điểm: {{score}}",
  "htmlBody": "<p>Điểm: <strong>{{score}}</strong></p>",
  "variables": ["studentName", "score"]
}
```

Trả `201`, status `draft`, version `1`.

### Quản lý version

- `POST /v1/templates/{templateId}/versions`: clone active thành draft version kế tiếp.
- `GET /v1/templates`: list/filter/cursor.
- `GET /v1/templates/{templateId}`: xem một version.
- `PATCH /v1/templates/{templateId}`: chỉ sửa draft.
- `POST /v1/templates/{templateId}/publish`: publish draft.
- `POST /v1/templates/{templateId}/retire`: retire active mà không tạo bản thay thế.

Source gửi notification bằng `templateCode`, không gửi database ID/version.

### Error contract

| Trường hợp | HTTP | Code |
|---|---:|---|
| Payload/format/placeholder sai | 400 | `VALIDATION_FAILED` / `TEMPLATE_SYNTAX_INVALID` |
| Device/template không tồn tại hoặc cross-tenant | 404 | `NOT_FOUND` |
| Family đã có draft | 409 | `TEMPLATE_DRAFT_EXISTS` |
| Sửa/publish/retire sai trạng thái | 409 | `TEMPLATE_INVALID_STATE` |
| Family trùng trong cùng scope | 409 | `TEMPLATE_CODE_EXISTS` |

## Data impact

```text
templates
  id, tenant_id, template_code
  scope, source_device_id, audience, version
  subject, text_body, html_body, variables
  status, created_at, updated_at, published_at, retired_at
```

- Unique `(tenant, family, version)`; partial unique tối đa một draft và một active/family.
- FK source device `ON DELETE RESTRICT`; check scope/device, version, status và ít nhất một body.
- Dữ liệu cũ nếu cần giữ được backfill thành scope `tenant`, audience `user`, version 1, `textBody=body`.
- Dự án đang local nên có thể reset dữ liệu. Trước staging phải squash/baseline; không áp migration phá dữ liệu lên
  staging/production.

## Tương thích chuyển tiếp

Contract cũ dùng `{key}` được giữ một chu kỳ local và phát deprecation warning. Nó không hỗ trợ đầy đủ
version/source/HTML. Trước staging phải xoá contract cũ để production chỉ có một contract rõ ràng.

## Acceptance criteria

1. Tạo tenant/source draft đúng scope, device role và tenant isolation.
2. Cho phép cùng code ở hai source; từ chối family trùng.
3. Publish atomic; mỗi family tối đa một active và một draft.
4. Active/retired bất biến; clone tạo đúng version kế tiếp.
5. Text, HTML và multipart validate/render đúng; biến HTML luôn được escape.
6. Thiếu/thừa biến, syntax sai hoặc output quá lớn không ghi dữ liệu.
7. Lookup ưu tiên source rồi fallback tenant và không rò cross-tenant.
8. List/filter/cursor ổn định, chỉ trả dữ liệu đúng tenant.
9. Contract cũ hoạt động trong chu kỳ chuyển tiếp và phát warning an toàn.
10. Migration/ràng buộc PostgreSQL chạy up/down/up trong Docker.
11. Log/error không chứa nội dung template hoặc giá trị biến.
12. Snapshot/version của notification cũ không đổi sau publish (kiểm chứng đầy đủ ở INTK-003).

## Planned files

```text
src/Notification.Domain/Templates/*
src/Notification.Application/Templates/*
src/Notification.Infrastructure/Persistence/Configurations/ContentTemplateConfiguration.cs
src/Notification.Infrastructure/Persistence/TemplateRepository.cs
src/Notification.Infrastructure/Persistence/Migrations/*_AddScopedTemplateVersions.cs
src/Notification.Api/Contracts/Templates/*
src/Notification.Api/Endpoints/Templates/TemplateEndpoints.cs
tests/*/Templates/*
scripts/test-integration.ps1
docs/SPECS.md
```

## Điểm cần xác nhận khi duyệt

- Không raw HTML trong v1; tất cả biến HTML đều escape.
- Source gửi bằng code; admin quản lý version bằng UUID.
- Source template được ưu tiên, tenant template là fallback.
- Version đã publish bất biến; sửa bằng cách clone version mới.

