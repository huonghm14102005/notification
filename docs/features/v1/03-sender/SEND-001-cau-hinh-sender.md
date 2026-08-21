# SEND-001 — Cấu hình tài khoản gửi SMTP

Status: Verified
Selected: 2026-08-15
Approved: 2026-08-15
Verified: 2026-08-15

## Đọc nhanh

Admin quản lý cấu hình SMTP theo tenant:

```text
create/list/update/disable sender
                  ↓
       SMTP password được mã hóa
```

- `key` định danh sender trong tenant và không đổi sau khi tạo.
- Password SMTP được mã hóa bằng AES-GCM, không trả lại qua API.
- Sender thuộc tenant khác luôn được che bằng `404`.
- Disable là idempotent; sender disabled không được update hoặc sử dụng.
- Feature chỉ lưu cấu hình, chưa gửi email thử hoặc notification.

Có thể refactor cipher/repository/endpoint nhưng phải giữ tenant boundary, encryption AAD, secret redaction, optimistic
state checks và contract create/list/update/disable.

## Outcome

Admin cấu hình được một hoặc nhiều tài khoản SMTP cho tenant; mật khẩu được mã hóa có xác thực khi lưu,
không bao giờ xuất hiện trong API đọc, log hoặc lỗi.

## Actor

Quản trị viên `owner` đã xác thực bằng JWT của AUTH-002.

## Trigger

- `POST /v1/senders` tạo cấu hình SMTP.
- `GET /v1/senders` kiểm kê cấu hình trong tenant.
- `PATCH /v1/senders/{id}` sửa cấu hình hoặc thay mật khẩu.
- `DELETE /v1/senders/{id}` vô hiệu hóa sender.

## In scope

- Tạo, liệt kê, sửa và vô hiệu hóa cấu hình kênh `email` qua SMTP.
- Mã hóa password bằng AES-256-GCM với nonce riêng cho từng lần ghi.
- Tenant isolation, validation, secret-safe logging/error handling.
- Giữ password cũ khi PATCH không có trường password; mã hóa lại khi có password mới.
- Migration `AddSenders` và Docker Compose test PostgreSQL thật.

## Out of scope

- Gọi mạng SMTP, kiểm tra credential hoặc gửi email thử — SEND-003.
- Chọn sender mặc định và resolve `senderKey` — SEND-002.
- OAuth2, Gmail API, SES/SendGrid HTTP API.
- Tự động rotate password hoặc rotate `ENCRYPTION_KEY` đang lưu.
- Đọc/giải mã password qua HTTP.
- Hard-delete sender hoặc tái sử dụng key của sender đã disabled.

## Preconditions

- PRE-01: AUTH-002 ở trạng thái Verified.
- PRE-02: admin/tenant trong JWT vẫn hoạt động.
- PRE-03: `ENCRYPTION_KEY` là đúng 32 byte sau khi decode base64 và migration đã chạy.

## Dependencies

AUTH-002.

## Tham chiếu

- Phạm vi sản phẩm: [PRODUCT.md](../../../PRODUCT.md).
- Dữ liệu: `senders` — SPECS.md §6.
- Contract: `GET/POST/PATCH/DELETE /v1/senders` — SPECS.md §7.
- Secret boundary và adapter — ARCHITECTURE.md D7–D8; CONVENTIONS.md §9, §11.

## Cấu hình Gmail tham khảo

Tài khoản thử nghiệm dự kiến: `huong102145@st.vimaru.edu.vn` (Google Workspace của trường).

| Trường | Giá trị |
|---|---|
| `host` | `smtp.gmail.com` |
| `port` | `587` |
| `secure` | `false` — kết nối thường rồi bắt buộc nâng cấp STARTTLS |
| `username` | địa chỉ email đầy đủ |
| `password` | App Password 16 ký tự, không phải mật khẩu đăng nhập |

App Password do người vận hành nhập khi chạy, không ghi vào mã nguồn/tài liệu. Gmail phù hợp thử nghiệm;
sản lượng thật cần SMTP relay Workspace hoặc dịch vụ gửi chuyên dụng.

## Business rules

- BR-01: `key` được trim/lowercase, dài 3..63, chỉ gồm `a-z`, `0-9`, `-`; không bắt đầu/kết thúc bằng `-`
  và không có `--`. Key bất biến sau khi tạo.
- BR-02: key duy nhất trong toàn bộ lịch sử của một tenant, kể cả sender disabled; trùng trả
  `409 SENDER_KEY_EXISTS`.
- BR-03: `host` được trim/lowercase, dài 1..253; chấp nhận DNS hostname hợp lệ hoặc địa chỉ IPv4/IPv6,
  không chấp nhận URI, path, scheme hoặc ký tự control.
- BR-04: `port` nằm trong 1..65535. `secure=true` nghĩa là implicit TLS ngay khi kết nối; `secure=false`
  nghĩa là bắt buộc STARTTLS trước authentication, không bao giờ cho plaintext fallback.
- BR-05: `username` dài 1..254 sau trim; password dài 1..1024 và được giữ nguyên ký tự, không trim.
- BR-06: `fromEmail` được trim/lowercase, tối đa 254 và đúng email syntax; `fromName` được trim,
  dài 1..200, không có control character hoặc CR/LF để chống header injection.
- BR-07: v1 chỉ có `channel=email`; caller không gửi channel. Server tự lưu `email`.
- BR-08: create luôn đặt `status=active`, `is_default=false`, `verified_at=null`; caller không thể tự đặt các trường này.
- BR-09: password được mã hóa AES-256-GCM bằng key cấu hình. Mỗi lần mã hóa dùng nonce CSPRNG 12 byte,
  tag 16 byte; AAD là version + tenantId + senderId để ciphertext không thể hoán đổi giữa record/tenant.
- BR-10: envelope ciphertext có version rõ ràng và chứa version/nonce/tag/ciphertext; database không lưu
  password thô, nonce không tái sử dụng với cùng key.
- BR-11: PATCH dùng merge semantics: trường vắng mặt giữ nguyên; password vắng mặt giữ ciphertext cũ;
  password có mặt phải là chuỗi hợp lệ và được mã hóa lại với nonce mới. JSON `null` không dùng để xóa trường bắt buộc.
- BR-12: thay đổi `host`, `port`, `secure`, `username` hoặc `password` đặt `verified_at=null`. Thay đổi
  `fromEmail/fromName` không tự xóa verification của SMTP credential.
- BR-13: SEND-001 không mở socket, DNS lookup hoặc gửi mail trong transaction/request; lưu cấu hình không đồng nghĩa đã verified.
- BR-14: DELETE chuyển `status=disabled`, cập nhật `updated_at`; gọi lại vẫn `204`. Sender disabled vẫn xuất hiện
  trong danh sách để audit và không sửa được; PATCH disabled trả `409 SENDER_DISABLED`.
- BR-15: ID không tồn tại hoặc thuộc tenant khác cùng trả `404 NOT_FOUND`. Mọi repository query nhận tenantId đầu tiên.
- BR-16: danh sách sắp xếp `createdAt desc, id desc`, cursor pagination mặc định 50, tối đa 100; không trả
  password ciphertext, nonce, tag hoặc dấu hiệu suy ra độ dài password.
- BR-17: create/update/delete áp fixed-window tổng cộng 30 request/admin/phút; vượt trả `429 RATE_LIMITED`
  và `Retry-After`.
- BR-18: thiếu/sai `ENCRYPTION_KEY` làm API và Worker fail-fast trước khi mở cổng/chạy job; validation message
  không chứa key. Chỉ Infrastructure có quyền giải mã tại điểm gửi trong feature sau.
- BR-19: response/log/error không chứa password request, ciphertext, encryption key hoặc SMTP auth string.
- BR-20: mọi timestamp là UTC; update atomically thay toàn bộ trường được yêu cầu và ciphertext tương ứng.

## Authorization

- Tất cả endpoint SEND-001 chỉ chấp nhận JWT admin policy `Admin`; API key machine bị từ chối.
- `tenantId` và `adminId` lấy từ principal AUTH-002, không nhận trong body/path/query.
- Sender tenant khác được che thành `404`, không trả `403` để tránh dò ID.

## Public contract

### `POST /v1/senders`

```json
{
  "key": "dao-tao",
  "host": "smtp.gmail.com",
  "port": 587,
  "secure": false,
  "username": "huong102145@st.vimaru.edu.vn",
  "password": "<app-password>",
  "fromEmail": "huong102145@st.vimaru.edu.vn",
  "fromName": "Phòng Đào tạo"
}
```

Thành công: `201 Created`, `Location: /v1/senders/{id}`.

```json
{
  "id": "00000000-0000-0000-0000-000000000000",
  "key": "dao-tao",
  "channel": "email",
  "host": "smtp.gmail.com",
  "port": 587,
  "secure": false,
  "username": "huong102145@st.vimaru.edu.vn",
  "fromEmail": "huong102145@st.vimaru.edu.vn",
  "fromName": "Phòng Đào tạo",
  "isDefault": false,
  "status": "active",
  "verifiedAt": null,
  "createdAt": "2026-08-15T00:00:00Z",
  "updatedAt": "2026-08-15T00:00:00Z"
}
```

### `GET /v1/senders?limit=50&cursor=<opaque>`

```json
{
  "items": [
    {
      "id": "00000000-0000-0000-0000-000000000000",
      "key": "dao-tao",
      "channel": "email",
      "host": "smtp.gmail.com",
      "port": 587,
      "secure": false,
      "username": "huong102145@st.vimaru.edu.vn",
      "fromEmail": "huong102145@st.vimaru.edu.vn",
      "fromName": "Phòng Đào tạo",
      "isDefault": false,
      "status": "active",
      "verifiedAt": null,
      "createdAt": "2026-08-15T00:00:00Z",
      "updatedAt": "2026-08-15T00:00:00Z"
    }
  ],
  "nextCursor": null
}
```

### `PATCH /v1/senders/{id}`

Mọi trường đều tùy chọn trừ `key` không được phép gửi. Ví dụ đổi password:

```json
{ "password": "<new-app-password>" }
```

Ít nhất một trường phải có mặt. Thành công trả `200 OK` với sender response không có password.

### `DELETE /v1/senders/{id}`

Thành công hoặc đã disabled: `204 No Content`.

| Trường hợp | HTTP | Mã lỗi |
|---|---:|---|
| Payload/cursor/UUID sai | 400 | `VALIDATION_FAILED` |
| JWT thiếu/sai/hết hạn | 401 | `UNAUTHORIZED` |
| Không phải admin owner | 403 | `FORBIDDEN` |
| Sender không tồn tại/khác tenant | 404 | `NOT_FOUND` |
| Key đã tồn tại trong tenant | 409 | `SENDER_KEY_EXISTS` |
| PATCH sender disabled | 409 | `SENDER_DISABLED` |
| Vượt mutation rate limit | 429 | `RATE_LIMITED` |
| PostgreSQL tạm thời không dùng được | 503 | `SERVICE_UNAVAILABLE` |
| Lỗi ngoài dự kiến | 500 | `INTERNAL_ERROR` |

## Data impact

Migration `AddSenders` tạo:

```text
senders
  id uuid primary key
  tenant_id uuid not null references tenants(id) on delete restrict
  key varchar(63) not null
  channel varchar(16) not null
  host varchar(253) not null
  port integer not null
  secure boolean not null
  username varchar(254) not null
  password_encrypted bytea not null
  from_email varchar(254) not null
  from_name varchar(200) not null
  is_default boolean not null
  status varchar(16) not null
  verified_at timestamptz null
  created_at timestamptz not null
  updated_at timestamptz not null
```

Indexes/constraints:

- Unique `ux_senders_tenant_key` trên `(tenant_id,key)` — áp cả active/disabled.
- Index `ix_senders_tenant_status` trên `(tenant_id,status)`.
- Partial unique `ux_senders_tenant_default` trên `tenant_id where is_default=true and status='active'`;
  SEND-001 luôn false nhưng migration chuẩn bị invariant cho SEND-002.
- Check `channel='email'`, `port between 1 and 65535`, `status in ('active','disabled')`.
- Check sender disabled không được là default.
- `Down()` xóa bảng `senders`; rollback/apply được kiểm tra trên PostgreSQL sạch.

## Acceptance criteria

- AC-01: create hợp lệ trả `201`, Location và metadata chuẩn hóa; DB có ciphertext nhưng không có password thô.
- AC-02: cùng password mã hóa hai lần tạo ciphertext khác nhau; giải mã đúng khi dùng đúng key/AAD và thất bại
  nếu đổi tenantId, senderId, ciphertext hoặc tag.
- AC-03: mọi biên key/host/port/username/password/fromEmail/fromName được validate; lỗi không ghi record.
- AC-04: key trùng trong cùng tenant trả `409`; tenant khác dùng cùng key được; disabled key không tái sử dụng được.
- AC-05: GET phân trang ổn định, chỉ trả sender đúng tenant và tuyệt đối không có secret/ciphertext fields.
- AC-06: PATCH vắng password giữ nguyên ciphertext; có password tạo ciphertext mới và không trả secret.
- AC-07: đổi connection/auth field xóa `verifiedAt`; đổi from metadata giữ nguyên `verifiedAt`.
- AC-08: PATCH atomically cập nhật đúng trường, normalize đúng và từ chối body rỗng/null/key.
- AC-09: DELETE active trả `204`, gọi lại vẫn `204`, record còn lại với status disabled và không default.
- AC-10: tenant A không list/update/disable sender tenant B; ID giả và cross-tenant cùng `404`.
- AC-11: API key machine không gọi được endpoint sender admin.
- AC-12: SEND-001 không thực hiện DNS/socket/SMTP trong create/update; `verifiedAt` ban đầu luôn null.
- AC-13: mutation thứ 31 cùng admin/phút trả `429` có `Retry-After`; admin khác có bucket độc lập.
- AC-14: encryption key thiếu/sai độ dài/base64 làm API/Worker fail-fast và không lộ key.
- AC-15: captured response/log/error không chứa password, ciphertext hoặc encryption key.
- AC-16: migration apply/rollback/re-apply; FK, unique, partial unique và check constraint hoạt động thật.
- AC-17: format, build, unit, architecture, HTTP integration, Docker Compose test xanh; dependency audit sạch.

## Planned files

```text
src/Notification.Domain/Senders/Sender.cs
src/Notification.Domain/Senders/SenderStatus.cs
src/Notification.Application/Abstractions/Security/ISecretCipher.cs
src/Notification.Application/Senders/*
src/Notification.Infrastructure/Configuration/EncryptionOptions.cs
src/Notification.Infrastructure/Persistence/Configurations/SenderConfiguration.cs
src/Notification.Infrastructure/Persistence/Migrations/*_AddSenders.cs
src/Notification.Infrastructure/Persistence/SenderRepository.cs
src/Notification.Infrastructure/Security/AesGcmSecretCipher.cs
src/Notification.Api/Contracts/Senders/*
src/Notification.Api/Endpoints/Senders/SenderEndpoints.cs
src/Notification.Api/Program.cs
deploy/docker/compose.yml
.env.example
README.md
tests/Notification.Application.Tests/Senders/*
tests/Notification.IntegrationTests/Senders/*
scripts/test-integration.ps1
docs/SPECS.md
docs/features/v1/README.md
docs/features/v1/03-sender/SEND-001-cau-hinh-sender.md
```

## Security review decisions requiring approval

- SR-01: AES-256-GCM, nonce 12 byte ngẫu nhiên, tag 16 byte, AAD gắn version + tenantId + senderId.
- SR-02: `secure=false` bắt buộc STARTTLS, không cho plaintext fallback; `secure=true` là implicit TLS.
- SR-03: key sender bất biến và không tái sử dụng kể cả sau disable.
- SR-04: PATCH không có password giữ ciphertext; password mới mã hóa lại; đổi connection/auth xóa verification.
- SR-05: create/update không gọi mạng; SEND-003 chịu trách nhiệm xác minh credential.
- SR-06: host cho phép DNS hoặc IP để hỗ trợ SMTP relay nội bộ; kiểm soát egress thuộc deployment/network policy.

## Open questions

Không còn câu hỏi kỹ thuật chặn Review. Sáu quyết định security trên cần được duyệt bằng
`APPROVE SEND-001` hoặc thay đổi bằng `CHANGE SEND-001: ...`.
