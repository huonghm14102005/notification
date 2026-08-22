# Đặc tả kỹ thuật — notify-api

Tài liệu này mô tả contract và schema hiện đang triển khai. Hướng chuyển sang source ổn định, nhiều
API key, delivery đa kênh và callback trạng thái nằm tại [TARGET-DESIGN.md](TARGET-DESIGN.md); mỗi
thay đổi chỉ có hiệu lực sau khi feature tương ứng được Approved và Verified.

Tài liệu chốt các con số và contract mà [ARCHITECTURE.md](ARCHITECTURE.md) cố tình để ngỏ: tên
trạng thái, bảng dữ liệu, danh sách endpoint, mã lỗi, số lần thử lại, giới hạn.

Trạng thái: **Review** — chưa được duyệt, nên chưa viết mã (xem [WORKFLOW.md](WORKFLOW.md) §2).

## 1. Tổng quan

Dịch vụ thông báo độc lập, đa tổ chức. Các hệ thống của trường (điểm, điểm rèn luyện, sau này là log
lỗi) gửi sang một yêu cầu đã có sẵn tiêu đề và nội dung; dịch vụ tiếp nhận, xếp hàng, gửi qua tài
khoản email của trường, thử lại khi hỏng và lưu lại toàn bộ vết.

## 2. Nền tảng

| Thành phần | Công nghệ | Phiên bản |
|-----------|-----------|-----------|
| Dịch vụ API | ASP.NET Core Web API | .NET 10 LTS |
| Tiến trình worker | .NET Worker Service | .NET 10 LTS |
| Ngôn ngữ | C# | theo SDK .NET 10 |
| Cơ sở dữ liệu | PostgreSQL | 16 |
| Hàng đợi | Redis | 7 |
| Persistence | EF Core + Npgsql | tương thích runtime đã chọn |
| Xác thực | ASP.NET Core Authentication + JWT | built-in |
| Kiểm tra dữ liệu | FluentValidation | chốt khi khởi tạo |
| Gửi email | MailKit (SMTP) | chốt khi khởi tạo |
| Reverse proxy | Nginx | alpine |

API và Worker dùng cùng solution, image và version. DLVR-001 dùng PostgreSQL polling, không thêm thư viện queue;
Redis vẫn dùng cho cache/rate limit khi các feature cần; đường gửi cơ bản polling PostgreSQL và không phụ thuộc Redis.

## 3. Các quyết định sản phẩm đã chốt

| # | Quyết định |
|---|-----------|
| P1 | Một tổ chức có nhiều tài khoản gửi; hệ thống nguồn chỉ định `senderKey`, bỏ trống thì dùng tài khoản mặc định |
| P2 | "Gửi thành công" = máy chủ SMTP đã nhận thư cho địa chỉ đó. Không theo dõi việc mở thư hay vào hộp thư |
| P3 | Tự thử lại tối đa 4 lần; hết lần vẫn hỏng thì gửi email cảnh báo cho quản trị viên **và** hiện trong danh sách lỗi |
| P4 | Lưu lịch sử kèm nguyên văn nội dung trong 10 năm; nội dung được mã hoá khi lưu |
| P5 | Một yêu cầu gửi được cho nhiều người nhận, tối đa 500; mỗi người nhận là một thông báo riêng, có trạng thái riêng và gửi lại riêng được |
| P6 | Hệ thống nguồn tự tra ra email; mã sinh viên chỉ đi kèm để ghi lịch sử, dịch vụ không giữ danh sách sinh viên |

P5 thay đổi so với MVP ban đầu (trước đây một yêu cầu một người nhận). P6 giữ nguyên phần loại trừ:
không có danh bạ người nhận.

## 4. Nhóm chức năng (v1)

| Nhóm | Chức năng | Đặc tả |
|------|-----------|--------|
| Tổ chức & định danh | Đăng ký, đăng nhập, quản trị viên | `features/v1/01-identity.md` |
| Khoá API | Cấp, liệt kê, thu hồi | `features/v1/02-api-keys.md` |
| Tài khoản gửi | Cấu hình SMTP, mặc định, thư thử | `features/v1/03-senders.md` |
| Mẫu nội dung | CRUD, biến, dựng nội dung | `features/v1/04-templates.md` |
| Tiếp nhận | Nhận yêu cầu một hoặc nhiều người nhận | `features/v1/05-intake.md` |
| Gửi | Hàng đợi, thử lại, cảnh báo hỏng | `features/v1/06-delivery.md` |
| Lịch sử | Tra cứu, lọc, gửi lại thủ công | `features/v1/07-history.md` |

## 5. Trạng thái

Tên trạng thái lưu trong cơ sở dữ liệu, viết snake_case, không đổi tuỳ tiện vì bên ngoài đọc được.

**`notifications.status`** sau CHAN-001

| Giá trị | Nghĩa | Kết thúc? |
|---------|-------|-----------|
| `accepted` | Đã nhận và lưu, chờ gửi | không |
| `processing` | Có delivery đang được worker xử lý | không |
| `delivered` | Mọi delivery đã được provider nhận | có |
| `partially_delivered` | Có delivery thành công và delivery thất bại | có |
| `failed` | Hết số lần thử hoặc bị từ chối vĩnh viễn | có |
| `cancelled` | Người huỷ trước khi gửi | có |

Không có `rejected`: yêu cầu sai bị từ chối đồng bộ và không sinh bản ghi (I8).

**`deliveries.status`**: `pending`, `sending`, `delivered`, `failed`, `cancelled`.
**`delivery_attempts.result`**: `success`, `transient_failure`, `permanent_failure`.

**`senders.status`**: `active`, `disabled`. **`api_keys.status`**: `active`, `revoked`.
**`templates.status`**: `draft`, `active`, `retired`. Mỗi family template có tối đa một draft và một active;
version đã publish là bất biến.

## 6. Mô hình dữ liệu

Mọi bảng có `id uuid` mặc định `gen_random_uuid()` và `created_at timestamptz` mặc định `now()`.
Bảng có thể sửa thì thêm `updated_at`. Bảng cấu hình dùng xoá mềm `deleted_at`.

| Bảng | Cột chính | Chỉ mục |
|------|-----------|---------|
| `tenants` | `name`, `slug` unique, `deleted_at` | `slug` |
| `admins` | `tenant_id`, `email`, `password_hash`, `role` | unique `email`; `(tenant_id, email)` |
| `refresh_tokens` | `admin_id`, `family_id`, `token_hash`, `expires_at`, `revoked_at`, `replaced_by_id` | unique `token_hash`; `(admin_id, family_id)`; active `expires_at` |
| `api_keys` | `tenant_id`, `created_by_admin_id`, `producer_name`, `key_prefix`, `key_hash`, `status`, `last_used_at`, `revoked_at` | unique `key_prefix`; unique `key_hash`; `(tenant_id, status)`; `(tenant_id, created_at desc)` |
| `senders` | `tenant_id`, `key`, `channel`, `host`, `port`, `secure`, `username`, `password_encrypted`, `from_email`, `from_name`, `is_default`, `status`, `verified_at` | unique `(tenant_id, key)`; unique một phần `(tenant_id) where is_default` |
| `templates` | `tenant_id`, `template_code`, `scope`, `source_device_id`, `audience`, `version`, `subject`, `text_body`, `html_body`, `variables jsonb`, `status` | unique family/version; unique một draft và một active/family |
| `notification_batches` | Được bổ sung ở INTK-002: `tenant_id`, `api_key_id`, `recipient_count`, `idempotency_key` | unique `(tenant_id, idempotency_key)` |
| `notifications` | `tenant_id`, `api_key_id`, `template_id`, `subject_encrypted`, `text_body_encrypted`, `html_body_encrypted`, trạng thái tổng hợp, `completed_at` | `(tenant_id, created_at desc)`; `(tenant_id, status)` |
| `deliveries` | `tenant_id`, `notification_id`, `channel`, `target`, `target_ref`, `sender_id`, trạng thái/retry/failure/delivered timestamps | `(status,next_attempt_at,created_at,id)`; unique `(notification_id,channel,target)` |
| `delivery_attempts` | `tenant_id`, `delivery_id`, `sender_id`, `attempt_no`, `result`, `provider_message_id`, `error_code`, `error_message`, `started_at`, `finished_at` | unique `(delivery_id, attempt_no)` |
| `notification_manual_actions` | `tenant_id`, `admin_id`, notification nguồn/kết quả, `action`, `created_at` | unique `(tenant_id,source_notification_id,action)`; `(tenant_id,created_at,id)` |
| `failure_alerts` | `tenant_id`, `window_start`, `window_end`, `notification_count`, `sent_at` | `(tenant_id, window_start)` |

Ghi chú:

- `recipient_ref` là mã sinh viên hoặc mã tuỳ ý do hệ thống nguồn gửi kèm, chỉ để tra cứu (P6).
  Dịch vụ không diễn giải, không tra ngược ra email.
- `subject_encrypted`, các body snapshot và `password_encrypted` mã hoá bằng AES-256-GCM với khoá lấy
  từ `ENCRYPTION_KEY` (P4, D8).
- `delivery_attempts` chỉ ghi thêm: không `UPDATE`, không `DELETE`.
- `notification_batches` chỉ xuất hiện khi INTK-002 mở nhiều người nhận; INTK-001 không tạo batch.

## 7. Danh sách endpoint

Tiền tố `/v1`. Cột "Auth": `admin` = JWT của quản trị viên, `key` = khoá API của hệ thống nguồn,
`—` = công khai.

| Method | Path | Auth | Mô tả |
|--------|------|------|-------|
| POST | `/v1/tenants/register` | — | Tạo tổ chức kèm quản trị viên đầu tiên |
| POST | `/v1/auth/login` | — | Đăng nhập, trả access + refresh token |
| POST | `/v1/auth/refresh` | — | Đổi refresh token lấy access token mới |
| POST | `/v1/auth/logout` | admin | Thu hồi refresh token |
| GET | `/v1/api-keys` | admin | Liệt kê khoá (chỉ tiền tố, không bao giờ trả khoá thô) |
| POST | `/v1/api-keys` | admin | Cấp khoá cho một hệ thống nguồn — khoá thô chỉ hiện ở đây, một lần |
| DELETE | `/v1/api-keys/:id` | admin | Thu hồi khoá, hiệu lực ngay |
| GET | `/v1/senders` | admin | Liệt kê tài khoản gửi, không kèm bí mật |
| POST | `/v1/senders` | admin | Tạo tài khoản gửi |
| PATCH | `/v1/senders/:id` | admin | Sửa; đặt `isDefault` sẽ gỡ mặc định của tài khoản khác |
| DELETE | `/v1/senders/:id` | admin | Tắt tài khoản gửi |
| POST | `/v1/senders/:id/test` | admin | Gửi thư thử đồng bộ, cập nhật `verified_at` |
| GET | `/v1/templates` | admin | Liệt kê/filter các version template |
| POST | `/v1/templates` | admin | Tạo family và draft version 1 |
| GET | `/v1/templates/:id` | admin | Xem một version |
| PATCH | `/v1/templates/:id` | admin | Chỉ sửa draft |
| POST | `/v1/templates/:id/versions` | admin | Clone active thành draft version kế tiếp |
| POST | `/v1/templates/:id/publish` | admin | Publish draft, retire active cũ atomically |
| POST | `/v1/templates/:id/retire` | admin | Retire active hiện tại |
| POST | `/v1/notifications` | key | **Tiếp nhận**: 1–500 người nhận, trả `202` |
| GET | `/v1/notifications` | admin, key | Danh sách theo cursor; lọc trạng thái, channel, thời gian; admin lọc source/key |
| GET | `/v1/notifications/:id` | admin, key | Một thông báo kèm các lần gửi |
| POST | `/v1/notifications/:id/retry` | admin | Gửi lại thủ công, tạo lần gửi mới |
| POST | `/v1/notifications/:id/cancel` | admin | Huỷ khi còn `accepted` |
| GET | `/v1/batches/:id` | admin, key | Hoãn tới INTK-002; chưa có trong local flow |
| GET | `/health` | — | Readiness, kiểm tra PostgreSQL và Redis |
| GET | `/health/live` | — | Liveness của riêng tiến trình API |

Khoá API chỉ đọc được thông báo do chính nó tạo ra; quản trị viên đọc được toàn bộ tổ chức.

## 8. Contract tiếp nhận

```http
POST /v1/notifications
Authorization: Bearer notify_<64 hex>
Content-Type: application/json
```

```jsonc
{
  "senderKey": "dao-tao",
  "channels": [{
    "type": "email",
    "targets": [{ "address": "sv1@st.edu.vn", "ref": "2021600123" }]
  }],
  "content": {
    "mode": "template",
    "templateCode": "diem-hoc-ky",
    "data": { "name": "An" }
  }
}
```

```jsonc
// 202 Accepted
{ "id": "9ab1…", "status": "accepted", "deliveries": [{ "id": "…", "channel": "email", "status": "pending" }] }
```

Quy tắc:

- `content.mode=plaintext` chỉ nhận `subject/body`; `content.mode=template` chỉ nhận `templateCode/data`.
- Source không chọn version: server ưu tiên active template của source device rồi fallback active tenant template.
- Render và mã hoá snapshot hoàn tất trước lần ghi đầu tiên; retry không đọc hoặc render lại template.
- Một người nhận sai định dạng làm **hỏng cả yêu cầu** (`400`, kèm chỉ số phần tử sai) — không tiếp
  nhận một phần, để hệ thống nguồn không phải đoán ai đã được nhận.
- Phản hồi trả về sau khi mọi bản ghi đã commit (I5); worker polling PostgreSQL, không có bước đẩy Redis queue.

## 9. Gửi và thử lại

| Tham số | Giá trị | Biến môi trường |
|---------|---------|-----------------|
| Tổng số lần thử | 4 (một lần đầu + 3 lần thử lại) | Hằng số ứng dụng |
| Giãn cách | 1 phút → 5 phút → 25 phút, tính từ lúc attempt kết thúc | Hằng số ứng dụng |
| Thời gian chờ SMTP | 30 giây | `SMTP_TIMEOUT_MS` |
| Số job đồng thời mỗi worker | 5 | `WORKER_CONCURRENCY` |
| Chu kỳ quét thông báo kẹt | 5 phút | `SWEEP_INTERVAL_SECONDS` |
| Ngưỡng coi là kẹt | `sending` quá 10 phút | `STUCK_AFTER_SECONDS` |

Phân loại lỗi do adapter quyết (D7):

| Phản hồi SMTP | Phân loại | Hành vi |
|---------------|-----------|---------|
| 2xx | `success` | `sent`, ghi `provider_message_id` nếu có |
| 4xx, timeout, mất kết nối | `transient_failure` | hẹn lại theo giãn cách, tới khi hết lần |
| 5xx, hòm thư không tồn tại, sai thông tin đăng nhập | `permanent_failure` | `failed` ngay, không thử lại (I13) |

Attempt transient thứ tư vẫn ghi `transient_failure` nhưng notification chuyển `failed`; không sinh attempt thứ năm.
Hết lần thử hoặc hỏng vĩnh viễn: `status = failed`, `failure_reason` là thông báo an toàn, đọc được
(I14), và thông báo đi vào cảnh báo ở mục 10.

Worker định kỳ recovery notification `sending` quá `STUCK_AFTER_SECONDS`. Attempt bị gián đoạn được ghi
`transient_failure/WORKER_INTERRUPTED`: attempt 1..3 trở về `accepted` để retry ngay, attempt 4 chuyển `failed`.
Recovery dùng PostgreSQL `FOR UPDATE SKIP LOCKED`, không dùng Redis và vẫn có thể gửi trùng theo at-least-once nếu
SMTP đã nhận email trước khi worker chết.

## 10. Cảnh báo hỏng

Theo P3, hỏng vĩnh viễn được báo bằng **hai** đường:

1. Hiện ngay trong `GET /v1/notifications?status=failed` để quản trị viên vào xem và bấm gửi lại.
2. Một email tổng hợp gửi tới các quản trị viên của tổ chức, gộp theo cửa sổ 15 phút
   (`ALERT_WINDOW_SECONDS`) để không spam: một thư liệt kê số lượng hỏng, lý do phổ biến và liên kết
   tra cứu. Không gửi thư khi cửa sổ không có lỗi nào.

Thư cảnh báo gửi qua tài khoản gửi mặc định. Nếu chính tài khoản đó hỏng thì chỉ ghi log ở mức
`error` và không thử lại vô hạn — tránh vòng lặp cảnh báo.

## 11. Giới hạn tần suất

| Phạm vi | Giới hạn | Biến môi trường |
|---------|----------|-----------------|
| Mỗi khoá API | 60 yêu cầu/phút | `RATE_LIMIT_PER_KEY` |
| Mỗi khoá API | 5.000 người nhận/giờ | `RATE_LIMIT_RECIPIENTS_PER_HOUR` |
| Mỗi tổ chức | 20.000 người nhận/giờ | `RATE_LIMIT_TENANT_PER_HOUR` |
| Đăng nhập | 10 lần/phút mỗi IP | `RATE_LIMIT_LOGIN` |

Đếm trong Redis, kiểm tra trước mọi thao tác ghi. Vượt giới hạn trả `429` kèm `retryAfter` (giây).

## 12. Mã lỗi

Khung bao theo ARCHITECTURE.md, thêm trường `code` để hệ thống nguồn xử lý bằng máy:

```json
{ "error": "Sender not found", "code": "SENDER_NOT_FOUND", "statusCode": 404 }
```

| `code` | HTTP | Khi nào |
|--------|------|---------|
| `VALIDATION_FAILED` | 400 | Dữ liệu vào sai; kèm `details` |
| `CONTENT_CONTRACT_AMBIGUOUS` | 422 | Trộn field plaintext và template |
| `TOO_MANY_RECIPIENTS` | 400 | Quá 500 người nhận |
| `TEMPLATE_VARIABLE_MISSING` | 400 | Thiếu biến mà mẫu khai báo (I9) |
| `TEMPLATE_VARIABLE_UNKNOWN` | 400 | Gửi biến không được mẫu khai báo |
| `TEMPLATE_RENDER_TOO_LARGE` | 400 | Nội dung sau render vượt giới hạn |
| `UNAUTHORIZED` | 401 | Thiếu thông tin xác thực, sai, hoặc khoá đã thu hồi |
| `FORBIDDEN` | 403 | Đúng tổ chức nhưng không đủ quyền |
| `NOT_FOUND` | 404 | Không có, hoặc thuộc tổ chức khác |
| `SENDER_NOT_FOUND` | 409 | `senderKey` không tồn tại, disabled, hoặc chưa có tài khoản mặc định |
| `TEMPLATE_NOT_FOUND` | 404 | Không có active template đúng tenant/source |
| `INVALID_STATE` | 409 | Gửi lại một thông báo chưa kết thúc, huỷ một thông báo đã gửi |
| `RATE_LIMITED` | 429 | Vượt giới hạn ở mục 11 |
| `INTERNAL_ERROR` | 500 | Lỗi ngoài dự kiến; không kèm chi tiết |

## 13. Lưu trữ và xoá dữ liệu

- Giữ thông báo và các lần gửi trong **10 năm** (`RETENTION_YEARS`), kèm nguyên văn nội dung đã mã
  hoá (P4).
- Một tác vụ dọn dẹp chạy hằng ngày xoá bản ghi quá hạn theo lô.
- Chỉ quản trị viên đọc được nội dung thư; khoá API chỉ thấy siêu dữ liệu và trạng thái của thông
  báo do chính nó tạo.
- Mọi lần quản trị viên đọc nội dung thư đều ghi log kèm `adminId` và `notificationId`.

## 14. Biến môi trường

| Biến | Bắt buộc | Mặc định | Mô tả |
|------|----------|----------|-------|
| `PORT` | Không | 3100 | Cổng API |
| `HOST` | Không | 0.0.0.0 | Địa chỉ lắng nghe |
| `DATABASE_URL` | Có | — | Chuỗi kết nối PostgreSQL |
| `REDIS_URL` | Không | redis://localhost:6379 | Chuỗi kết nối Redis |
| `JWT_SECRET` | Có | — | Khoá HS256, tối thiểu 32 byte UTF-8 |
| `JWT_ISSUER` | Không | notification-server | JWT issuer bắt buộc khi xác thực |
| `JWT_AUDIENCE` | Không | notification-admin | JWT audience bắt buộc khi xác thực |
| `JWT_EXPIRES_IN` | Không | 3600 | TTL access token (giây) |
| `JWT_REFRESH_EXPIRES_IN` | Không | 604800 | TTL refresh token (giây) |
| `API_KEY_SALT` | Có | — | Khóa HMAC-SHA256, tối thiểu 16 byte UTF-8 |
| `ENCRYPTION_KEY` | Có | — | 32 byte dạng base64, dùng cho AES-256-GCM |
| `MAX_RECIPIENTS_PER_REQUEST` | Không | 500 | Trần người nhận mỗi lần gọi |
| `SMTP_TIMEOUT_MS` | Không | 30000 | Thời gian chờ SMTP |
| `WORKER_CONCURRENCY` | Không | 5 | Số job đồng thời mỗi worker |
| `SWEEP_INTERVAL_SECONDS` | Không | 300 | Chu kỳ quét thông báo kẹt |
| `STUCK_AFTER_SECONDS` | Không | 600 | Tuổi tối thiểu của trạng thái `sending` trước recovery |
| `ALERT_WINDOW_SECONDS` | Không | 900 | Cửa sổ gộp email cảnh báo |
| `RETENTION_YEARS` | Không | 10 | Thời hạn lưu lịch sử |
| `LOG_LEVEL` | Không | info | Mức log |

## 15. Điểm còn bỏ ngỏ

1. Chống trùng bằng `Idempotency-Key` đã có cột trong `notification_batches` nhưng chưa bật ở v1;
   cần chốt cửa sổ thời gian trước khi bật.
2. Chưa có giao diện quản trị; v1 chỉ có API. Quản trị viên thao tác bằng công cụ HTTP.
3. Ràng buộc nội dung theo từng khoá (giới hạn tên miền người nhận, tiền tố tiêu đề) vẫn để ngỏ như
   trong domain map.
