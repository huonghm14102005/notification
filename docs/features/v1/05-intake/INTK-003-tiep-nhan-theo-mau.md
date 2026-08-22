# INTK-003 — Gửi notification bằng template

Status: Verified
Selected: 2026-08-22
Approved: 2026-08-22
Verified: 2026-08-22
Dependencies: INTK-001, DEVICE-001, CHAN-001, TMPL-002

## Đọc nhanh

Hiện source phải tự ghép `subject/body`. Sau feature này source có thể gửi:

```json
{
  "content": {
    "mode": "template",
    "templateCode": "score-result",
    "data": { "studentName": "An", "score": "9.5" }
  }
}
```

Server tìm template phù hợp với source device, render text/HTML, mã hoá snapshot rồi mới tạo notification và delivery.
Template được sửa hoặc publish version mới sau đó không làm notification cũ thay đổi.

## Luồng xử lý

```text
API key → tenantId + sourceDeviceId
        → validate request/target/sender
        → tìm active source template
             └─ không có thì fallback tenant template
        → render subject + text/html
        → mã hoá snapshot
        → commit Notification + Delivery
        → 202 Accepted
        → Worker gửi đúng MIME format
```

Mọi bước lookup/render/validate phải hoàn tất trước lần ghi đầu tiên. Có lỗi thì không để lại notification, delivery
hoặc attempt.

## Phạm vi

- Thêm `content.mode=template` vào contract đa kênh của `POST /v1/notifications`.
- Chọn active template theo tenant, source device và `templateCode`.
- Validate map dữ liệu biến và render bằng renderer của TMPL-002.
- Lưu template ID và snapshot subject/text/HTML đã mã hoá.
- Worker gửi email plain text, HTML hoặc `multipart/alternative` đúng snapshot.
- Giữ nguyên `content.mode=plaintext` và contract inline cũ trong chu kỳ local hiện tại.

Chưa làm nhiều target/batch, render riêng từng recipient, idempotency key, preview/test-send template, attachment,
schedule hoặc raw HTML variable.

## Business rules

1. Chỉ contract đa kênh hỗ trợ template. Contract legacy `subject/body/recipients` tiếp tục chỉ nhận plaintext.
2. `content` là union nghiêm ngặt:
   - `mode=plaintext`: chỉ có `mode`, `subject`, `body`;
   - `mode=template`: chỉ có `mode`, `templateCode`, `data`.
3. Trộn field của hai mode hoặc gửi field lạ trả lỗi; không dùng nội dung inline làm fallback khi template lỗi.
4. `templateCode` trim/lowercase và dùng cùng regex 3..63 ký tự của TMPL-002.
5. Tenant ID, API key ID và source device ID chỉ lấy từ authenticated principal, không nhận từ request.
6. Lookup active template theo thứ tự: source template của device → tenant template. Draft/retired không được dùng.
7. Template không tồn tại, sai tenant, sai source hoặc không active cùng trả `404 TEMPLATE_NOT_FOUND`.
8. `data` là JSON object string→string, tối đa 50 key. Tên/value tuân giới hạn renderer TMPL-002.
9. Thiếu biến trả `TEMPLATE_VARIABLE_MISSING`; thừa biến trả `TEMPLATE_VARIABLE_UNKNOWN`. Không bỏ qua và không tự
   thay bằng chuỗi rỗng.
10. HTML variable luôn được escape bởi renderer; intake không có đường tắt raw/trusted HTML.
11. Render subject tối đa 998, mỗi text/HTML body tối đa 100000 và tổng body tối đa 150000 ký tự.
12. Resolve sender, template và render xong trước khi tạo ID/ciphertext. Bất kỳ lỗi nào cũng không ghi dữ liệu.
13. Subject, text body và HTML body được mã hoá riêng bằng tenant ID + notification ID làm AAD.
14. Notification lưu `templateId` trỏ đúng immutable version đã dùng. Content delivery luôn đọc snapshot, không render
    lại và không đọc template khi retry/recovery.
15. Plain-text template gửi MIME `text/plain`; HTML-only gửi `text/html`; có cả hai gửi
    `multipart/alternative` với plain text trước HTML.
16. Provider/retry xử lý một email như một delivery duy nhất, không tạo attempt riêng cho từng MIME part.
17. `202` chỉ trả sau khi Notification + Delivery commit atomically; nó không có nghĩa provider đã nhận email.
18. Không log `templateData`, rendered content, target email, raw API key hoặc ciphertext.

## Authorization

- Endpoint vẫn yêu cầu policy `ApiKey`; JWT admin không gửi thay source.
- Principal bắt buộc có `tenant_id`, API key ID và `device_id` hợp lệ.
- Template source chỉ khớp đúng device đang xác thực; không cho request chọn `sourceDeviceId` khác.
- Mọi lookup bắt đầu bằng tenant. Cross-tenant được che bằng mã not-found chung.

## Public contract

### Template mode

```http
POST /v1/notifications
Authorization: Bearer notify_<secret>
Content-Type: application/json
```

```json
{
  "senderKey": "greenmail-smtp",
  "channels": [{
    "type": "email",
    "targets": [{ "address": "student@example.test", "ref": "SV001" }]
  }],
  "content": {
    "mode": "template",
    "templateCode": "score-result",
    "data": {
      "studentName": "An",
      "score": "9.5"
    }
  }
}
```

Thành công giữ response CHAN-001:

```json
{
  "id": "0198...",
  "status": "accepted",
  "deliveries": [{
    "id": "0198...",
    "channel": "email",
    "target": "student@example.test",
    "targetRef": "SV001",
    "status": "pending"
  }]
}
```

### Plaintext mode không đổi

```json
{
  "content": {
    "mode": "plaintext",
    "subject": "Kết quả mới",
    "body": "Bạn đã có kết quả."
  }
}
```

### Error contract

| Trường hợp | HTTP | Code |
|---|---:|---|
| JSON/data/code/field sai | 400 | `VALIDATION_FAILED` |
| Trộn plaintext và template | 422 | `CONTENT_CONTRACT_AMBIGUOUS` |
| Content mode chưa hỗ trợ | 422 | `CONTENT_MODE_NOT_SUPPORTED` |
| Template không active/không thuộc scope | 404 | `TEMPLATE_NOT_FOUND` |
| Thiếu biến | 400 | `TEMPLATE_VARIABLE_MISSING` |
| Thừa biến | 400 | `TEMPLATE_VARIABLE_UNKNOWN` |
| Render vượt giới hạn | 400 | `TEMPLATE_RENDER_TOO_LARGE` |
| Sender không tồn tại/disabled | 409 | `SENDER_NOT_FOUND` |
| Database không commit được | 503 | `SERVICE_UNAVAILABLE` |

Lỗi biến có thể trả danh sách tên biến nhưng không trả value.

## Data impact

Mở rộng snapshot notification:

```text
notifications
  template_id uuid null                  -- FK immutable template version
  subject_encrypted bytea not null
  text_body_encrypted bytea null
  html_body_encrypted bytea null
```

- Rename/backfill `body_encrypted` hiện tại thành `text_body_encrypted`; inline notification cũ vẫn là plaintext.
- Check yêu cầu ít nhất một trong text/html ciphertext có dữ liệu.
- Template FK `ON DELETE RESTRICT`; template version không hard-delete.
- History admin trả đúng text/HTML snapshot khi contract history được mở rộng; API key tiếp tục không đọc content.
- Migration phải chạy clean và trên database phiên bản hiện tại có notification plaintext.

## Internal contracts

```text
ITemplateRepository.FindActiveAsync(tenantId, sourceDeviceId, normalizedCode)
  → source active trước, tenant active sau, hoặc null

ITemplateRenderer.Render(template, data)
  → subject + textBody? + htmlBody?

IEmailSender.SendAsync(sender, target, subject, textBody?, htmlBody?)
```

Endpoint không gọi EF Core, cipher hoặc SMTP trực tiếp. Worker không gọi template repository.

## Acceptance criteria

1. Source gửi template plaintext nhận `202`; PostgreSQL lưu đúng template version và ciphertext snapshot.
2. HTML-only tạo email `text/html`; text+HTML tạo đúng `multipart/alternative`.
3. Source template được ưu tiên trước tenant fallback cùng code.
4. Draft/retired/missing/cross-source/cross-tenant cùng trả `TEMPLATE_NOT_FOUND` và không ghi dữ liệu.
5. Thiếu/thừa biến trả code và danh sách tên ổn định, không echo value, không ghi dữ liệu.
6. HTML variable được escape; payload như `<script>` không trở thành markup thực thi.
7. Field của hai mode không được trộn; plaintext hiện tại vẫn hoạt động không đổi.
8. Snapshot notification cũ không đổi sau khi publish template version mới.
9. Retry/recovery dùng snapshot, không render lại và không đổi template version.
10. API key device A không dùng được source template của device B; JWT không gửi notification.
11. Sender/template/render/database failure không tạo notification hoặc delivery rác.
12. Migration giữ notification plaintext hiện tại và chạy up/down/up trong Docker.
13. Log/error/callback không lộ template data hoặc rendered content.

## Planned files

```text
src/Notification.Api/Contracts/Notifications/NotificationRequests.cs
src/Notification.Api/Endpoints/Notifications/NotificationEndpoints.cs
src/Notification.Application/Notifications/*
src/Notification.Application/Notifications/Delivery/*
src/Notification.Application/Abstractions/Email/IEmailSender.cs
src/Notification.Domain/Notifications/OutboundNotification.cs
src/Notification.Infrastructure/Email/MailKitEmailSender.cs
src/Notification.Infrastructure/Persistence/*
src/Notification.Infrastructure/Persistence/Migrations/*_AddNotificationHtmlSnapshot.cs
tests/*/Notifications/*
tests/*/Templates/*
scripts/test-integration.ps1
docs/SPECS.md
```

## Điểm cần xác nhận khi duyệt

- Template chỉ đi qua contract đa kênh `content.mode=template`; contract legacy vẫn plaintext.
- Source không chọn version; server luôn lấy active source template rồi fallback active tenant template.
- Thiếu và thừa biến đều bị từ chối, không bỏ qua biến dư.
- HTML-only được gửi HTML thật; multipart giữ cả text và HTML snapshot đã mã hoá.
