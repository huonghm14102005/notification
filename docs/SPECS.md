# Đặc tả kỹ thuật — notify-api

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

API và Worker dùng cùng solution, image và version. Thư viện queue cụ thể được chốt trong DLVR-001;
Redis vẫn là hạ tầng hàng đợi/lịch có thể dựng lại theo D4 và D10.

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

**`notifications.status`**

| Giá trị | Nghĩa | Kết thúc? |
|---------|-------|-----------|
| `accepted` | Đã nhận và lưu, chờ gửi | không |
| `sending` | Worker đang xử lý | không |
| `sent` | SMTP đã nhận thư (P2) | có |
| `failed` | Hết số lần thử hoặc bị từ chối vĩnh viễn | có |
| `cancelled` | Người huỷ trước khi gửi | có |

Không có `rejected`: yêu cầu sai bị từ chối đồng bộ và không sinh bản ghi (I8).

**`delivery_attempts.result`**: `success`, `transient_failure`, `permanent_failure`.

**`senders.status`**: `active`, `disabled`. **`api_keys.status`**: `active`, `revoked`.
**`templates.status`**: `draft`, `active`, `retired`.

## 6. Mô hình dữ liệu

Mọi bảng có `id uuid` mặc định `gen_random_uuid()` và `created_at timestamptz` mặc định `now()`.
Bảng có thể sửa thì thêm `updated_at`. Bảng cấu hình dùng xoá mềm `deleted_at`.

| Bảng | Cột chính | Chỉ mục |
|------|-----------|---------|
| `tenants` | `name`, `slug` unique, `deleted_at` | `slug` |
| `admins` | `tenant_id`, `email`, `password_hash`, `role` | unique `email`; `(tenant_id, email)` |
| `refresh_tokens` | `admin_id`, `token_hash`, `expires_at`, `revoked_at` | `token_hash` |
| `api_keys` | `tenant_id`, `producer_name`, `key_prefix`, `key_hash`, `status`, `last_used_at`, `revoked_at` | unique `key_prefix`; `(tenant_id, status)` |
| `senders` | `tenant_id`, `key`, `channel`, `host`, `port`, `secure`, `username`, `password_encrypted`, `from_email`, `from_name`, `is_default`, `status`, `verified_at` | unique `(tenant_id, key)`; unique một phần `(tenant_id) where is_default` |
| `templates` | `tenant_id`, `key`, `subject`, `body`, `variables jsonb`, `status` | unique `(tenant_id, key, status='active')` |
| `notification_batches` | `tenant_id`, `api_key_id`, `recipient_count`, `idempotency_key` | unique `(tenant_id, idempotency_key)` |
| `notifications` | `tenant_id`, `batch_id`, `api_key_id`, `sender_id`, `template_id`, `recipient_email`, `recipient_ref`, `subject_encrypted`, `body_encrypted`, `status`, `attempt_count`, `next_attempt_at`, `failure_reason`, `sent_at` | `(tenant_id, created_at desc)`; `(tenant_id, status)`; `(status, next_attempt_at)` |
| `delivery_attempts` | `tenant_id`, `notification_id`, `sender_id`, `attempt_no`, `result`, `provider_message_id`, `error_code`, `error_message`, `started_at`, `finished_at` | `(notification_id, attempt_no)` |
| `failure_alerts` | `tenant_id`, `window_start`, `window_end`, `notification_count`, `sent_at` | `(tenant_id, window_start)` |

Ghi chú:

- `recipient_ref` là mã sinh viên hoặc mã tuỳ ý do hệ thống nguồn gửi kèm, chỉ để tra cứu (P6).
  Dịch vụ không diễn giải, không tra ngược ra email.
- `subject_encrypted`, `body_encrypted`, `password_encrypted` mã hoá bằng AES-256-GCM với khoá lấy
  từ `ENCRYPTION_KEY` (P4, D8).
- `delivery_attempts` chỉ ghi thêm: không `UPDATE`, không `DELETE`.
- `notification_batches` nhóm các người nhận của cùng một lần gọi (P5), phục vụ tra cứu và
  idempotency về sau.

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
| GET | `/v1/templates` | admin | Liệt kê mẫu |
| POST | `/v1/templates` | admin | Tạo mẫu |
| GET | `/v1/templates/:key` | admin | Xem một mẫu |
| PATCH | `/v1/templates/:key` | admin | Sửa mẫu |
| POST | `/v1/notifications` | key | **Tiếp nhận**: 1–500 người nhận, trả `202` |
| GET | `/v1/notifications` | admin, key | Danh sách, lọc theo trạng thái, thời gian, khoá, batch |
| GET | `/v1/notifications/:id` | admin, key | Một thông báo kèm các lần gửi |
| POST | `/v1/notifications/:id/retry` | admin | Gửi lại thủ công, tạo lần gửi mới |
| POST | `/v1/notifications/:id/cancel` | admin | Huỷ khi còn `accepted` |
| GET | `/v1/batches/:id` | admin, key | Tóm tắt một lần gọi: số đã gửi, đang chờ, hỏng |
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
  "senderKey": "dao-tao",              // tuỳ chọn, bỏ trống thì dùng tài khoản mặc định (P1)
  "subject": "Kết quả học kỳ 1",       // bắt buộc nếu không dùng template
  "body": "Chào {{name}}, ...",        // bắt buộc nếu không dùng template
  "templateKey": "diem-hoc-ky",        // tuỳ chọn; nếu có thì subject/body lấy từ mẫu
  "recipients": [                       // 1..500 (P5)
    { "email": "sv1@st.edu.vn", "ref": "2021600123", "variables": { "name": "An" } }
  ]
}
```

```jsonc
// 202 Accepted
{
  "batchId": "0f2c…",
  "accepted": 300,
  "notifications": [ { "id": "9ab1…", "email": "sv1@st.edu.vn", "ref": "2021600123" } ]
}
```

Quy tắc:

- `subject`/`body` và `templateKey` loại trừ nhau; không có cái nào thì `400`.
- Một người nhận sai định dạng làm **hỏng cả yêu cầu** (`400`, kèm chỉ số phần tử sai) — không tiếp
  nhận một phần, để hệ thống nguồn không phải đoán ai đã được nhận.
- Phản hồi trả về sau khi mọi bản ghi đã commit (I5); việc đẩy hàng đợi diễn ra sau đó.

## 9. Gửi và thử lại

| Tham số | Giá trị | Biến môi trường |
|---------|---------|-----------------|
| Tổng số lần thử | 4 (một lần đầu + 3 lần thử lại) | `MAX_DELIVERY_ATTEMPTS` |
| Giãn cách | 1 phút → 5 phút → 25 phút | `RETRY_BACKOFF_SECONDS` |
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

Hết lần thử hoặc hỏng vĩnh viễn: `status = failed`, `failure_reason` là câu tiếng Việt đọc được
(I14), và thông báo đi vào cảnh báo ở mục 10.

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

Khung bao theo CONVENTIONS.md §5, thêm trường `code` để hệ thống nguồn xử lý bằng máy:

```json
{ "error": "Sender not found", "code": "SENDER_NOT_FOUND", "statusCode": 404 }
```

| `code` | HTTP | Khi nào |
|--------|------|---------|
| `VALIDATION_FAILED` | 400 | Dữ liệu vào sai; kèm `details` |
| `CONTENT_REQUIRED` | 400 | Không có `subject`/`body` lẫn `templateKey` |
| `CONTENT_CONFLICT` | 400 | Có cả nội dung trực tiếp lẫn `templateKey` |
| `TOO_MANY_RECIPIENTS` | 400 | Quá 500 người nhận |
| `TEMPLATE_VARIABLE_MISSING` | 400 | Thiếu biến mà mẫu khai báo (I9) |
| `UNAUTHORIZED` | 401 | Thiếu thông tin xác thực, sai, hoặc khoá đã thu hồi |
| `FORBIDDEN` | 403 | Đúng tổ chức nhưng không đủ quyền |
| `NOT_FOUND` | 404 | Không có, hoặc thuộc tổ chức khác |
| `SENDER_NOT_FOUND` | 404 | `senderKey` không tồn tại, hoặc chưa có tài khoản mặc định |
| `TEMPLATE_NOT_FOUND` | 404 | `templateKey` không tồn tại hoặc đã rút |
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
| `JWT_SECRET` | Có | — | Tối thiểu 32 ký tự |
| `JWT_EXPIRES_IN` | Không | 3600 | TTL access token (giây) |
| `JWT_REFRESH_EXPIRES_IN` | Không | 604800 | TTL refresh token (giây) |
| `API_KEY_SALT` | Có | — | Tối thiểu 16 ký tự |
| `ENCRYPTION_KEY` | Có | — | 32 byte dạng base64, dùng cho AES-256-GCM |
| `MAX_RECIPIENTS_PER_REQUEST` | Không | 500 | Trần người nhận mỗi lần gọi |
| `MAX_DELIVERY_ATTEMPTS` | Không | 4 | Tổng số lần thử |
| `RETRY_BACKOFF_SECONDS` | Không | 60,300,1500 | Giãn cách giữa các lần thử |
| `SMTP_TIMEOUT_MS` | Không | 30000 | Thời gian chờ SMTP |
| `WORKER_CONCURRENCY` | Không | 5 | Số job đồng thời mỗi worker |
| `SWEEP_INTERVAL_SECONDS` | Không | 300 | Chu kỳ quét thông báo kẹt |
| `ALERT_WINDOW_SECONDS` | Không | 900 | Cửa sổ gộp email cảnh báo |
| `RETENTION_YEARS` | Không | 10 | Thời hạn lưu lịch sử |
| `LOG_LEVEL` | Không | info | Mức log |

## 15. Điểm còn bỏ ngỏ

1. Chống trùng bằng `Idempotency-Key` đã có cột trong `notification_batches` nhưng chưa bật ở v1;
   cần chốt cửa sổ thời gian trước khi bật.
2. Chưa có giao diện quản trị; v1 chỉ có API. Quản trị viên thao tác bằng công cụ HTTP.
3. Ràng buộc nội dung theo từng khoá (giới hạn tên miền người nhận, tiền tố tiêu đề) vẫn để ngỏ như
   trong domain map.
