# CBACK-001 — Callback kết quả về hệ thống nguồn

Status: Verified
Selected: 2026-08-21
Approved: 2026-08-21
Verified: 2026-08-21
Dependencies: DEVICE-001, DLVR-002

## Mục tiêu

Khi notification kết thúc, notification-server chủ động gửi kết quả về device nguồn:

```text
notification sent/failed
        ↓ tạo event bền vững
notification.completed
        ↓ HTTP POST có chữ ký
device nguồn nhận kết quả
```

Nếu callback thất bại, kết quả delivery không thay đổi. Hệ thống nguồn vẫn có thể dùng API tra cứu để đối soát.

## Phạm vi

### Có trong feature

- Cấu hình hoặc tắt callback trên device nguồn.
- Secret do server sinh, chỉ trả một lần và mã hóa khi lưu.
- Tạo `notification.completed` cho cả thành công và thất bại.
- Payload không chứa subject, body, recipient hoặc credential.
- Ký HMAC-SHA256 trên timestamp và raw JSON body.
- Callback at-least-once; giữ nguyên event ID khi retry.
- Retry độc lập với email delivery.
- Timeout, chặn redirect và bảo vệ SSRF/DNS rebinding.
- Lưu event và callback attempts trong PostgreSQL.

### Chưa làm

- Callback URL tùy ý trong từng notification request.
- Event `notification.accepted` hoặc event riêng cho từng delivery.
- Nút retry callback thủ công và giao diện quản trị.
- Exactly-once callback.
- Delivery đa kênh; payload đã chuẩn bị dạng mảng để CHAN-001 mở rộng sau.

## 1. Cấu hình callback

Admin cấu hình callback trên device bằng:

```http
PUT /v1/devices/{deviceId}/callback
Authorization: Bearer <admin-jwt>
Content-Type: application/json

{ "url": "https://source.example.edu.vn/notification-callback" }
```

Server sinh 32 byte CSPRNG và trả secret dạng base64url đúng một lần:

```json
{
  "deviceId": "00000000-0000-0000-0000-000000000000",
  "url": "https://source.example.edu.vn/notification-callback",
  "secret": "<one-time-secret>",
  "configuredAt": "2026-08-21T00:00:00Z"
}
```

Quy tắc:

- User chỉ cấu hình device của mình; tenant owner cấu hình mọi device cùng tenant.
- Device phải active và có role `source` hoặc `both`.
- Gọi `PUT` lại thay URL và rotate secret; secret cũ không được trả lại. Mọi attempt bắt đầu sau commit dùng
  URL/secret mới, kể cả event được tạo trước lúc rotate.
- Database chỉ lưu secret đã mã hóa bằng tenant ID + device ID làm AAD.
- Response chứa secret có `Cache-Control: no-store`.
- `DELETE /v1/devices/{deviceId}/callback` tắt callback, idempotent và trả `204`. Cùng transaction, event `pending`
  của device chuyển `cancelled` với code `CALLBACK_DISABLED`.
- `GET /v1/devices/{deviceId}` thêm boolean `callbackConfigured`; không trả URL, secret hoặc ciphertext.
- Disable device ngừng tạo callback mới và chuyển event `pending` sang `cancelled` với code
  `DEVICE_DISABLED`.
- Event `sending` nghĩa là HTTP attempt đã bắt đầu nên không bị sửa trực tiếp. Attempt đó có thể vẫn tới receiver và
  luôn được ghi history: `2xx` hoàn tất `delivered`; kết quả lỗi chuyển event sang `cancelled` nếu callback/device đã
  bị tắt, không lên lịch retry. Request mới bắt đầu sau commit không được gửi.

## 2. URL an toàn

Callback URL phải:

- là URL tuyệt đối;
- dùng HTTPS;
- không chứa username/password hoặc fragment;
- có hostname hợp lệ và dài tối đa 2048 ký tự.

Development/Test có thể bật `CALLBACK_ALLOW_INSECURE_HTTP=true` để dùng HTTP receiver local. Production bật tùy chọn
này phải fail-fast.

Docker test có thể bật thêm `CALLBACK_ALLOW_PRIVATE_NETWORK=true` để gọi receiver trong network cô lập của Compose.
Tùy chọn này mặc định tắt và cấu hình nó trong Production phải fail-fast.

Server kiểm tra hostname ở hai thời điểm: lúc lưu cấu hình và ngay trước mỗi attempt. Mọi địa chỉ DNS trả về đều phải
là public; nếu có một địa chỉ loopback, private, link-local, multicast, unspecified hoặc reserved thì request bị chặn.

Trước khi gửi, adapter chọn một IP từ tập đã kiểm tra và pin socket vào đúng IP đó. TLS SNI và certificate validation
vẫn dùng hostname gốc. Adapter không được resolve hostname lần thứ hai khi mở kết nối; cách kiểm tra rồi để
`HttpClient` tự resolve lại không đạt yêu cầu vì còn lỗ hổng DNS rebinding/TOCTOU.

HTTP client không tự follow redirect. DNS/IP bị chặn lúc gửi là permanent failure `CALLBACK_TARGET_BLOCKED`.

## 3. Tạo event

Khi notification chuyển sang trạng thái cuối:

- `sent` ánh xạ thành callback status `delivered`;
- `failed` ánh xạ thành callback status `failed`.

Nếu source device đang active và đã cấu hình callback, transaction hoàn tất delivery đồng thời tạo đúng một
`status_events`. Unique `(notification_id,event_type)` ngăn tạo event trùng.

Event chỉ snapshot raw JSON payload chuẩn hóa và notification/device/tenant identity. URL và secret không được sao
chép sang event; worker đọc config hiện tại của device khi claim. Rotate làm attempt kế tiếp dùng config mới;
delete/disable chuyển event chưa hoàn tất thành `cancelled`.

Event ID có dạng `evt_` + 32 ký tự hex lowercase và không thay đổi giữa các lần retry.

## 4. Payload và chữ ký

Payload v1:

```json
{
  "schemaVersion": 1,
  "eventId": "evt_0123456789abcdef0123456789abcdef",
  "type": "notification.completed",
  "occurredAt": "2026-08-21T00:00:00Z",
  "notificationId": "00000000-0000-0000-0000-000000000000",
  "status": "delivered",
  "deliveries": [
    {
      "channel": "email",
      "status": "delivered",
      "attemptCount": 2,
      "errorCode": null
    }
  ]
}
```

Failed payload chỉ dùng stable safe `errorCode`; không có `failureReason`, provider response, email, recipient,
subject hoặc body.

Mỗi HTTP request gửi các header:

```http
X-NTS-Event-Id: evt_...
X-NTS-Timestamp: 1787202600
X-NTS-Signature: v1=<64 lowercase hex>
Content-Type: application/json
```

Chữ ký được tính chính xác như sau:

```text
signedPayload = timestamp + "." + rawUtf8JsonBody
signature = lowercase_hex(HMACSHA256(secret, signedPayload))
```

Receiver nên từ chối timestamp lệch quá 5 phút, constant-time compare chữ ký và deduplicate bằng `eventId`. Nếu
`eventId` đã được xử lý, receiver phải trả `2xx` như một success idempotent; không trả `409` cho duplicate hợp lệ.

## 5. Gửi và retry

Callback worker claim event tới hạn từ PostgreSQL bằng `FOR UPDATE SKIP LOCKED`; không dùng Redis. Mỗi provider call
tạo một immutable `callback_attempts`.

Tối đa 6 attempts: lần đầu và 5 retry với lịch:

```text
1 phút → 5 phút → 25 phút → 2 giờ → 12 giờ
```

Cửa sổ tự động khoảng 14 giờ là giới hạn có chủ ý của v1. Sau khi exhausted, event thành `failed`; hệ thống nguồn dùng
API tra cứu để đối soát. Retry thủ công hoặc cửa sổ 24–72 giờ thuộc feature sau.

| Kết quả HTTP/network | Hành vi |
|---|---|
| `200..299` | Event `delivered`, không retry |
| Timeout, DNS, connection, `408`, `425`, `429`, `500..599` | Transient, retry nếu còn lượt |
| Redirect `300..399` | Permanent, không follow |
| `400..499` khác, gồm `409` | Permanent; receiver đúng contract phải trả `2xx` cho duplicate |
| TLS/certificate hoặc target bị SSRF block | Permanent |

Response body không được lưu hoặc log. Chỉ lưu status code, result, safe error code và timestamps.

Callback attempt timeout mặc định 10 giây. Retry callback không thay đổi notification/delivery status, không gửi lại
email và không tăng delivery attempts.

## 6. Trạng thái và tính nhất quán

`status_events.status`:

```text
pending → sending → delivered
                  → pending   (transient còn lượt)
                  → failed    (permanent hoặc hết lượt)
pending           → cancelled (callback bị xóa hoặc device bị disable)
sending           → cancelled (attempt hiện tại lỗi và config/device đã bị tắt)
```

- Claim tăng `attempt_count` trước HTTP call.
- Attempt và state completion commit trong cùng transaction.
- Completion chỉ commit nếu event vẫn `sending` với cùng attempt number.
- Handler chạy lặp hoặc mất race trả skipped, không tạo duplicate attempt.
- Callback worker crash ở `sending` được recovery theo cùng nguyên tắc stale; attempt gián đoạn ghi
  `CALLBACK_WORKER_INTERRUPTED` và retry nếu còn lượt.
- Delete callback/disable device cancel event pending trong cùng transaction. Completion của event đang sending vẫn
  ghi attempt; nếu lỗi thì cancel thay vì retry. Claim/completion dùng state check nên không hồi sinh event cancelled.
- Callback là at-least-once: receiver có thể nhận cùng event ID nhiều lần nếu server crash sau HTTP `2xx` nhưng trước
  khi commit.

## 7. Cấu hình

| Biến | Mặc định | Giới hạn |
|---|---:|---:|
| `CALLBACK_TIMEOUT_MS` | 10000 | 1000..30000 |
| `CALLBACK_POLL_INTERVAL_MS` | 2000 | 250..60000 |
| `CALLBACK_CONCURRENCY` | 5 | 1..50 |
| `CALLBACK_STUCK_AFTER_SECONDS` | 120 | lớn hơn timeout, tối đa 86400 |
| `CALLBACK_ALLOW_INSECURE_HTTP` | false | chỉ Development/Test |
| `CALLBACK_ALLOW_PRIVATE_NETWORK` | false | chỉ Development/Test |

Max attempts và backoff là hằng số ứng dụng trong CBACK-001.

## 8. Dữ liệu

Migration mới:

```text
devices
  callback_url varchar(2048) null
  callback_secret_encrypted bytea null
  callback_configured_at timestamptz null

status_events
  id, tenant_id, device_id, notification_id, event_type
  payload_encrypted bytea
  status, attempt_count, next_attempt_at, failure_code
  occurred_at, created_at, updated_at

callback_attempts
  id, tenant_id, event_id, attempt_no, result
  http_status_code, error_code, started_at, finished_at, created_at
```

Ràng buộc quan trọng:

- callback config trên device hoặc cùng null, hoặc đủ URL/secret/configured timestamp;
- event status thuộc `pending|sending|delivered|failed|cancelled`;
- unique `(notification_id,event_type)`;
- unique `(event_id,attempt_no)`;
- result thuộc `success|transient_failure|permanent_failure`;
- event/attempt là lịch sử, không hard-delete.

Payload UTF-8 được mã hóa bằng tenant ID + event ID làm AAD. Sau khi giải mã, worker gửi lại đúng raw bytes đã
snapshot để chữ ký ổn định. Callback secret chỉ tồn tại một bản mã hóa trên device; API không trả ciphertext.

## 9. Acceptance criteria

- AC-01: owner cấu hình callback cho device hợp lệ; secret mới chỉ xuất hiện trong response của chính lần PUT đã sinh
  secret đó, bao gồm khi rotate.
- AC-02: user khác/cross-tenant không xem, rotate hoặc xóa config; cùng trả `404` khi cần che resource.
- AC-03: callback URL/secret mã hóa trong DB, không xuất hiện trong GET/log/error.
- AC-04: URL sai hoặc resolve tới IP bị chặn bị từ chối; mỗi attempt pin socket vào IP đã kiểm tra, giữ hostname cho
  TLS và không resolve lại; redirect không follow.
- AC-05: notification sent và failed đều tạo đúng một `notification.completed` trong transaction terminal.
- AC-06: payload có `schemaVersion=1`, đúng contract và không chứa recipient/content/secret/provider response.
- AC-07: chữ ký HMAC khớp chính xác timestamp + `.` + raw body; event ID không đổi khi retry.
- AC-08: HTTP 2xx ghi success và delivered; không retry.
- AC-09: timeout/network/429/5xx retry đúng lịch, tối đa 6 attempts trong khoảng 14 giờ rồi failed.
- AC-10: redirect và permanent 4xx/TLS/SSRF block failed ngay, không retry.
- AC-11: callback failure không đổi notification/delivery và không gửi lại email.
- AC-12: nhiều worker claim/completion đồng thời không tạo duplicate attempt/event.
- AC-13: callback `sending` stale được recovery có giới hạn; không tạo attempt 7.
- AC-14: rotate làm attempt sau dùng URL/secret mới; delete/disable atomically cancel pending event và request mới
  không bắt đầu sau commit. Attempt đang chạy vẫn được ghi; nếu lỗi thì cancelled, không retry.
- AC-15: receiver trả `2xx` cho event ID đã xử lý được coi success; `409` là permanent failure theo contract.
- AC-16: metrics/log phát sau commit, không chứa URL đầy đủ, body, secret, PII hoặc response body.
- AC-17: options sai fail-fast; Production không cho insecure HTTP.
- AC-18: migration up/down/up và unique/FK/check constraints pass trên PostgreSQL thật.
- AC-19: Docker receiver xác minh signature, transient→success, permanent, duplicate event ID và notification vẫn tra
  cứu được khi callback failed.
- AC-20: format, build, unit, architecture và Docker Compose tests pass; dependency audit sạch.

## File dự kiến thay đổi

```text
src/Notification.Domain/Devices/Device.cs
src/Notification.Domain/Callbacks/*
src/Notification.Application/Callbacks/*
src/Notification.Application/Devices/*
src/Notification.Infrastructure/Callbacks/*
src/Notification.Infrastructure/Persistence/*
src/Notification.Infrastructure/Persistence/Migrations/*_AddCallbacks.cs
src/Notification.Api/Contracts/Devices/*
src/Notification.Api/Endpoints/Devices/*
src/Notification.Worker/*
tests/Notification.Domain.Tests/Callbacks/*
tests/Notification.Application.Tests/Callbacks/*
tests/Notification.IntegrationTests/Callbacks/*
deploy/docker/compose.yml
scripts/test-integration.ps1
.env.example
docs/SPECS.md
```

## Các quyết định cần duyệt

- Callback config dùng `PUT/DELETE` nested dưới device; secret server sinh 32 byte và chỉ trả một lần.
- Event chỉ snapshot encrypted raw payload; attempt dùng URL/secret hiện tại, rotate có hiệu lực sau commit.
- Ký `timestamp.rawBody`, event ID ổn định; callback at-least-once.
- Tổng 6 attempts với backoff `1m,5m,25m,2h,12h`; callback retry độc lập delivery.
- HTTPS bắt buộc ngoài Development/Test; kiểm tra SSRF lúc cấu hình và trước mỗi attempt; không follow redirect.
- Adapter pin socket vào IP đã kiểm tra và giữ hostname gốc cho TLS; không chấp nhận check-then-resolve.
- Delete callback/disable device cancel event pending; attempt đang chạy vẫn ghi history nhưng không retry nếu lỗi.
- Duplicate hợp lệ phải được receiver trả `2xx`.
- Cửa sổ retry tự động v1 khoảng 14 giờ; sau đó nguồn đối soát bằng API.
- Thêm migration cho device callback config, `status_events` và `callback_attempts`.

## Verification

- 82 test .NET pass: application, domain, architecture và integration.
- Docker Compose end-to-end pass với PostgreSQL, Redis, SMTP, API, delivery worker và callback worker.
- Receiver thật trong Docker xác minh HMAC trên raw body, event ID, schema, payload và trạng thái `delivered`.
- Migration rollback về `0` và apply lại `latest` thành công.
