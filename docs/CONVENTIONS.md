# Notification Server — Conventions & Rules

Tài liệu này chứa các quy tắc **bắt buộc và kiểm tra được** khi người hoặc AI sửa code. Lý do kiến
trúc nằm ở [ARCHITECTURE.md](ARCHITECTURE.md); contract hiện tại nằm ở [SPECS.md](SPECS.md); thiết kế
chưa triển khai nằm ở [TARGET-DESIGN.md](TARGET-DESIGN.md).

## 1. Phạm vi và thứ tự ưu tiên

Khi có mâu thuẫn, áp dụng theo thứ tự:

1. Yêu cầu trực tiếp đã được người phụ trách sản phẩm xác nhận.
2. Feature spec có trạng thái `Approved`.
3. `SPECS.md` đối với code đang chạy và migration hiện có.
4. Tài liệu này.
5. `ARCHITECTURE.md` và `TARGET-DESIGN.md`.

`TARGET-DESIGN.md` không tự cấp quyền triển khai. Không thêm bảng, endpoint hoặc abstraction cho
feature tương lai trước khi feature đó được `Approved`.

## 2. Cấu trúc solution

```text
src/
  Notification.Domain/          entity, value object, invariant; không I/O
  Notification.Application/     use case, port/interface, validation
  Notification.Infrastructure/  EF Core, PostgreSQL, crypto, SMTP/provider adapter
  Notification.Api/             endpoint, auth, middleware, composition root
  Notification.Worker/          polling, delivery, retry, recovery, callback

tests/
  Notification.Domain.Tests/
  Notification.Application.Tests/
  Notification.ArchitectureTests/
  Notification.IntegrationTests/

deploy/docker/                   Docker Compose và container config
docs/features/                   feature spec và trạng thái
```

### Dependency flow

```text
Api/Worker → Application → Domain
Infrastructure ─implements→ Application ports
```

- Domain không tham chiếu ASP.NET Core, EF Core, MailKit, Redis hoặc Infrastructure.
- Application không tham chiếu Infrastructure hoặc provider SDK.
- Endpoint/BackgroundService không dùng `DbContext` trực tiếp; chỉ gọi application use case.
- Use case không truy cập repository nội bộ của module khác; dùng public application interface.
- Không tạo phụ thuộc vòng. Shared chỉ chứa primitive/value object thật sự dùng chung.
- Mỗi hệ thống ngoài đi qua interface trong Application và adapter trong Infrastructure.

## 3. Tên và thuật ngữ

Tài liệu viết tiếng Việt; code, schema identifier và commit viết tiếng Anh.

| Loại | Quy ước | Ví dụ |
|---|---|---|
| Namespace/folder | PascalCase | `Delivery/`, `DeviceApiKeys/` |
| C# type/public member | PascalCase | `Notification`, `ClaimDeliveriesAsync` |
| Local/parameter | camelCase | `deviceId`, `notificationId` |
| Private field | `_camelCase` | `_clock` |
| Constant | PascalCase | `MaxDeliveryAttempts` |
| Database table/column | snake_case, danh từ số nhiều cho bảng | `delivery_attempts`, `device_id` |
| JSON field | camelCase | `attemptCount`, `createdAt` |
| Route | plural, kebab-case | `/v1/api-keys`, `/v1/push-endpoints` |

Thuật ngữ chuẩn:

- `user`: tài khoản đăng nhập bằng email đầy đủ và password.
- `device`: server, hệ thống nguồn hoặc thiết bị vật lý thuộc user.
- `deviceId`: định danh công khai; không phải credential.
- `apiKey`: bí mật để device gửi request.
- `pushEndpoint`: địa chỉ/token để device nhận push; không phải API key.
- `notification`: yêu cầu đã được tiếp nhận bền vững.
- `delivery`: một channel-target của notification.
- `deliveryAttempt`: một lần gọi provider cho delivery.
- `sent/delivered`: provider đã chấp nhận; không có nghĩa người nhận đã đọc.

Không tạo thêm synonym như `producer`, `clientApp`, `message`, `sendJob` trong code mới. Tên legacy
chỉ tồn tại đến migration thay thế đã được duyệt.

## 4. Quy tắc C#/.NET

- Bật nullable reference types và implicit usings theo project hiện tại; warning mới không được bỏ qua.
- Public API/application interface phải có kiểu trả về rõ ràng và nhận `CancellationToken` khi có I/O.
- I/O bất đồng bộ dùng `async`/`await`; cấm `.Result`, `.Wait()` và fire-and-forget không được quản lý.
- Dùng constructor injection. Không service locator hoặc đọc container DI trong domain/application.
- Dùng `TimeProvider`/`IClock` thay vì gọi thời gian hệ thống trực tiếp trong logic kiểm thử được.
- Không bắt `Exception` chỉ để bỏ qua. Phải phân loại, rethrow có cause hoặc log đủ context.
- Không đưa provider type, EF entity tracking hoặc HTTP type qua ranh giới Application.
- Không thêm abstraction nếu chỉ phục vụ khả năng chưa được feature hiện tại yêu cầu.

## 5. API conventions

- Mọi endpoint nghiệp vụ có prefix `/v1`; breaking change dùng phiên bản mới.
- Request/response JSON camelCase; timestamp ISO 8601 UTC.
- Dữ liệu vào được validate tại biên bằng FluentValidation hoặc validator tương đương.
- Validation thất bại trước mọi ghi DB. Request bị từ chối không để lại notification nửa vời.
- Intake thành công trả `202 Accepted`, vì đây là lời hứa xử lý chứ không phải kết quả gửi.
- Tạo resource cấu hình trả `201 Created` và `Location` khi phù hợp.
- List endpoint phải phân trang; không trả danh sách không giới hạn.
- Không lấy `tenantId`, `userId`, `deviceId` hoặc `apiKeyId` đáng tin cậy từ request body; lấy từ principal.

Error envelope:

```json
{
  "error": "Validation failed",
  "code": "VALIDATION_FAILED",
  "statusCode": 400,
  "details": [{ "path": "channels", "message": "At least one channel is required" }]
}
```

- `400`: hình dạng/giá trị request sai; `401`: chưa xác thực; `403`: đã xác thực nhưng thiếu quyền.
- Truy cập resource của tenant/user khác trả `404` để không rò rỉ tồn tại.
- `409`: xung đột trạng thái/unique; `422`: contract hợp lệ nhưng channel/capability chưa hỗ trợ.
- `429`: rate limit và có `Retry-After`.
- 5xx không trả exception, stack trace, connection string hoặc provider secret.
- Global error handler ánh xạ typed error; endpoint không lặp `try/catch` để đổi status code.

## 6. Authentication và authorization

### Password và JWT

- Password dùng ASP.NET Core `PasswordHasher`; cấm lưu plaintext hoặc tự viết thuật toán hash.
- Email đăng nhập được trim, normalize lowercase và unique toàn hệ thống.
- Phần trước `@` chỉ tạo `displayName` mặc định; không dùng làm login và không bắt buộc unique.
- Không tạo trường `username` đăng nhập riêng nếu chưa có feature được duyệt thay đổi quyết định này.
- Access token ngắn hạn; refresh token lưu hash và rotate khi dùng.
- JWT user dùng cho quản trị. Không dùng JWT user cố định trong thiết bị server/IoT.

### Device API key

- Raw key sinh bằng CSPRNG, chỉ hiển thị một lần; DB lưu prefix và hash.
- Xác thực tra prefix trước rồi constant-time verify hash.
- Một device có thể có nhiều key để rotate; revoke một key không ảnh hưởng key khác.
- Disable device làm mọi key của device vô hiệu ngay.
- Principal device phải chứa tenant/user/device/API-key identity sau khi DEVICE-001 được triển khai.
- Không log raw key; log tối đa key ID hoặc prefix không nhạy cảm.

### Tenant và ownership

- Quan hệ đích là `tenant → users → devices`; một device có đúng một owner user trong tenant.
- User chỉ tạo/quản lý device của mình; tenant owner có thể xem, disable hoặc revoke device của mọi
  user cùng tenant. Device không được tự đăng ký ẩn danh trong DEVICE-001.
- Mọi repository chạm dữ liệu tenant nhận `tenantId` và lọc theo tenant ngay trong query.
- Dữ liệu device còn phải kiểm tra owner/scope phù hợp; không chỉ kiểm tra ở endpoint.
- Endpoint mới chạm dữ liệu tenant bắt buộc có integration test cross-tenant.

## 7. Database và migration

- PostgreSQL là nguồn sự thật duy nhất cho notification, delivery, attempt và callback event.
- Mọi thay đổi schema dùng EF Core migration đánh số/thời gian tăng dần; không sửa DB thủ công.
- Migration có `Up` và `Down`, chạy được trên DB sạch và DB ở phiên bản trước.
- Thay đổi phá vỡ dùng expand/migrate/contract; không xóa cột trong cùng release bắt đầu ngừng dùng nó.
- Mọi bảng tenant có `tenant_id`; index quan trọng bắt đầu bằng tenant khi truy vấn theo tenant.
- Dùng UUID và timestamp UTC; bảng sửa được có `created_at`, `updated_at` khi cần.
- Config/resource dùng soft delete hoặc disable. History/attempt/event chỉ ghi thêm.
- Query raw SQL phải parameterized, nằm trong Infrastructure và có test chứng minh nhu cầu.
- Nhiều bản ghi thay đổi cùng invariant phải commit trong một transaction.
- Cấm gọi SMTP, HTTP, SMS, Discord, FCM/APNs hoặc provider khác bên trong transaction.

## 8. Notification, delivery và retry

- API commit notification/delivery trước khi trả `202`; worker không phụ thuộc Redis queue.
- Worker polling PostgreSQL và claim bằng cơ chế an toàn cho nhiều worker, ví dụ `SKIP LOCKED`.
- Một delivery đại diện đúng một channel-target và có vòng đời riêng.
- Delivery thành công không được gửi lại chỉ vì delivery kênh khác thất bại.
- Mỗi delivery tối đa 4 attempt: lần đầu và tối đa 3 retry.
- Adapter provider chỉ phân loại `success`, `transientFailure`, `permanentFailure`; không tự retry.
- Lỗi permanent kết thúc ngay. Lỗi transient lên lịch backoff; không retry vòng lặp nóng.
- `delivery_attempts` bất biến; retry tạo dòng attempt mới với `attemptNo` liên tục.
- Worker handler phải idempotent theo trạng thái DB. Hệ thống chấp nhận at-least-once và phải giảm
  trùng bằng idempotency/provider key khi provider hỗ trợ.
- Nội dung và target dùng để gửi là snapshot; sửa template/device sau đó không viết lại lịch sử.

## 9. Template và channel adapter

- Template renderer là hàm thuần: không DB, HTTP, clock hoặc provider SDK.
- Thiếu variable là validation failure; không tự thay bằng chuỗi rỗng.
- `plaintext` và `template` là content mode; `target` không phải content mode.
- Application phụ thuộc interface theo capability; provider SDK chỉ xuất hiện trong Infrastructure.
- Thêm channel mới bằng adapter/config/validation riêng, không thêm `switch` provider rải rác.
- Channel chưa bật trả `422 CHANNEL_NOT_SUPPORTED`; không nhận rồi âm thầm bỏ qua.
- Mỗi adapter đặt timeout, hỗ trợ cancellation và loại bỏ secret khỏi exception/log.

## 10. Callback và outbound HTTP

- Callback URL lấy từ cấu hình device nguồn, không lấy tùy ý từ notification request.
- Callback secret mã hóa khi lưu và không trả lại sau khi cấu hình.
- Ký HMAC-SHA256 trên timestamp và raw body; gửi event ID để nguồn deduplicate.
- Callback là at-least-once; retry callback độc lập với delivery và không đổi trạng thái gửi.
- `notification.completed` phải được tạo cho cả kết quả thành công và thất bại cuối cùng; nguồn không
  được buộc phải polling để biết notification thành công.
- HTTP client dùng `IHttpClientFactory`, timeout hữu hạn và không tự follow redirect nếu chưa kiểm tra.
- Chặn scheme ngoài HTTPS ở production; kiểm tra DNS/IP để tránh loopback, link-local và private-network SSRF,
  trừ allowlist vận hành được duyệt.
- Không log raw callback body nếu chứa nội dung/target nhạy cảm.

## 11. Logging, metrics và secrets

- Dùng structured logging qua `ILogger`; cấm `Console.WriteLine` trong application code.
- Mỗi request/job có `correlationId`; khi có phải kèm tenant, device, notification và delivery ID.
- Không log password, JWT/refresh token, raw API key, SMTP secret, callback secret, push token,
  plaintext body hoặc toàn bộ target nhạy cảm.
- Ở `info`: lifecycle đã commit. `warn`: lỗi transient/retry. `error`: lỗi permanent hoặc ngoài dự kiến.
- Metric tối thiểu: accepted, pending, attempt theo result/channel, delivered, failed, callback result,
  processing latency và queue age.
- Secret đến từ configuration/secret manager; `.env` không commit. `.env.example` chỉ có placeholder.

## 12. Testing

### Unit/Application tests

- Test invariant, validation, rendering, phân loại lỗi, backoff và aggregate status không cần Docker.
- Fake port có hành vi rõ ràng; không mock implementation detail hoặc private method.
- Test dùng clock kiểm soát được, không phụ thuộc thời gian thực hoặc thứ tự chạy.

### Integration tests

- Dùng Docker Compose với PostgreSQL và provider giả theo quyết định OPS-001; không dùng Testcontainers.
- Test endpoint success, validation, authentication, authorization và tenant/device isolation.
- Test migration `Down → Up` khi feature có schema change và backfill trên dữ liệu phiên bản trước.
- Test worker chạy cùng item hai lần không tạo kết quả kết thúc sai hoặc attempt ngoài giới hạn.
- Test retry đủ transient/permanent và đúng tổng tối đa 4 attempt.
- Email test dùng GreenMail/local SMTP; không phụ thuộc Gmail/Internet trong CI.
- Callback test dùng HTTP receiver giả, kiểm tra chữ ký, duplicate event và timeout/retry.

### Kiểm tra trước khi hoàn tất

```powershell
dotnet format --verify-no-changes
dotnet build
dotnet test
docker compose -f deploy/docker/compose.yml up --build --wait
```

Chỉ chạy những lệnh phù hợp với feature, nhưng build và test liên quan phải có bằng chứng. Không tuyên
bố `Verified` nếu integration test bắt buộc chưa chạy.

## 13. Docker và configuration

- Dockerfile multi-stage; production chạy non-root nếu image/runtime cho phép.
- API và Worker build từ cùng revision; migration chạy một lần trước rollout.
- Compose local có health check và dependency readiness; không dùng delay cố định thay readiness.
- API mở HTTP; Worker không công khai business endpoint.
- Options bind từ ASP.NET Core configuration và validate khi startup; domain không đọc environment.
- PostgreSQL/SMTP/callback timeout, concurrency, retry/backoff và rate limit là config có validation.

## 14. Git, tài liệu và feature workflow

- Commit: `feat:`, `fix:`, `refactor:`, `docs:`, `test:`, `chore:`.
- Branch: `feature/...`, `fix/...`, `refactor/...`.
- Không commit secret, `.env`, token, Gmail password hoặc production credential.
- Không commit/push nếu người dùng chưa yêu cầu.
- Thay đổi domain sửa `TARGET-DESIGN.md`; thay đổi kiến trúc sửa `ARCHITECTURE.md`; thay đổi contract
  đang chạy sửa `SPECS.md`; thay đổi rule chung sửa chính file này.
- Feature phải có spec trước code và tuân thủ `SELECT → Review → APPROVE → Implementing → Verified`.
- Feature chưa `Approved`: chỉ được sửa tài liệu/spec, không viết implementation hoặc migration.
- Không sửa ngược spec `Verified` để mô tả thiết kế tương lai; tạo feature migration/chuyển đổi mới.

## 15. Security checklist bắt buộc

1. Không plaintext password hoặc secret trong DB/log/response.
2. Mọi query tenant/device có ownership filter và test cô lập.
3. Mọi input có giới hạn độ dài/số lượng; mọi list được phân trang.
4. Outbound SMTP/HTTP/provider có TLS phù hợp, timeout và cancellation.
5. Callback/webhook target được bảo vệ SSRF và ký xác thực.
6. API key/refresh token có thể revoke và raw value chỉ xuất hiện một lần.
7. Nội dung, recipient và push token được coi là dữ liệu cá nhân.
8. Rate limit phải hoàn tất trước khi mở intake công khai hoặc batch lớn.
9. Retry có giới hạn cứng; không có vòng lặp vô hạn.
10. Migration và rollback không làm mất lịch sử delivery/attempt.
