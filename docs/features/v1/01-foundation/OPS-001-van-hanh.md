# OPS-001 — Walking skeleton, health, log và metrics nền tảng

Status: Verified
Selected: 2026-08-15
Approved: 2026-08-15
Re-approved: 2026-08-15 — Docker Compose integration test thay Testcontainers
Verified: 2026-08-15

## Đọc nhanh

Feature này dựng bộ khung chạy chung cho API và Worker:

```text
Docker Compose → PostgreSQL + Redis + API + Worker
                         ↓
                health, log, metrics
```

- `/health/live` chỉ kiểm tra API còn sống.
- `/health` chỉ healthy khi PostgreSQL và Redis đều dùng được.
- Mỗi request có correlation ID an toàn.
- API và Worker dùng cùng image/build; Worker không mở HTTP public.
- Không log secret, token, nội dung notification hoặc connection string.

Có thể refactor cách tổ chức health/log/metrics nhưng phải giữ nguyên endpoint, response tối thiểu, fail-fast config,
chiều phụ thuộc project và Docker integration test.

## Outcome

API và Worker có thể build, test và chạy bằng Docker Compose trên cùng một phiên bản; người vận
hành phân biệt được tiến trình còn sống với tiến trình đã sẵn sàng phục vụ, đồng thời có log và
metrics nền tảng an toàn để các feature sau sử dụng.

## Actor

- Người vận hành.
- Docker/orchestrator và hệ thống giám sát.
- Lập trình viên chạy môi trường local và integration test.

## Trigger

- Khởi động API hoặc Worker.
- Docker/orchestrator gọi health probe.
- Một HTTP request đi qua API.
- API/Worker ghi một sự kiện hoặc cập nhật một metric nghiệp vụ.

## In scope

- Khởi tạo solution .NET 10 LTS và năm project runtime: Domain, Application, Infrastructure, API,
  Worker; bốn project test theo ARCHITECTURE.
- Một Docker image/version dùng cho API và Worker, với hai command khởi động khác nhau.
- Docker Compose local gồm API, Worker, PostgreSQL và Redis; không thêm Nginx ở walking skeleton.
- Bind cấu hình bằng Options, validate khi khởi động; hỗ trợ biến môi trường theo SPECS §14.
- API liveness và readiness; readiness kiểm tra PostgreSQL và Redis với timeout độc lập.
- Worker tự chạy health check PostgreSQL/Redis định kỳ và phát trạng thái cho Docker bằng health
  command/process check; không mở cổng HTTP public.
- Middleware nhận hoặc sinh `correlationId`, trả lại trong header và đưa vào logging scope.
- JSON console logging qua `ILogger`, có redaction/allow-list để không ghi secret hoặc nội dung.
- Khai báo các instruments nền tảng và nghiệp vụ qua `System.Diagnostics.Metrics`; ở OPS-001 chỉ
  wiring/export bằng OpenTelemetry và kiểm chứng instrument tồn tại, chưa có dashboard.
- Integration test dùng Docker Compose để điều phối PostgreSQL/Redis thật; không phụ thuộc thư viện
  Testcontainers trong test project.
- Architecture tests bảo vệ chiều phụ thuộc giữa các project.

## Out of scope

- Domain table, EF Core migration nghiệp vụ và repository nghiệp vụ.
- Queue consumer, enqueue, retry và queue-depth collector thật; thuộc DLVR/INTK.
- Counter nghiệp vụ tăng thật; OPS-001 chỉ cung cấp meter/instrument contract cho feature sở hữu.
- SMTP, authentication, tenant resolution và API key.
- Dashboard, alert theo ngưỡng, distributed tracing exporter và log collector tập trung.
- Nginx/production Compose, backup/restore drill, load test và release hardening giai đoạn 7.

## Preconditions

- PRE-01: Máy phát triển/CI phải có .NET 10 SDK và Docker Engine/Compose.
- PRE-02: Cổng local và credential phát triển được cấu hình ngoài source.
- PRE-03: Không sử dụng tài khoản hoặc secret production trong integration test.

## Dependencies

None.

## Tham chiếu

- Điều kiện hoàn tất: [PRODUCT.md](../../../PRODUCT.md).
- Kiến trúc đích: [ARCHITECTURE.md](../../../ARCHITECTURE.md).
- Lộ trình: [IMPLEMENTATION-ROADMAP.md](../../../IMPLEMENTATION-ROADMAP.md), giai đoạn 0 và 7.
- Contract: SPECS.md §7 và §14.

## Business rules

- BR-01: API chỉ được xem là ready khi kết nối được cả PostgreSQL và Redis trong timeout cấu hình;
  một dependency hỏng làm readiness `Unhealthy`, không làm liveness thất bại.
- BR-02: Liveness không gọi dependency ngoài; nó chỉ chứng minh process và HTTP pipeline phản hồi.
- BR-03: Health response không trả connection string, hostname nội bộ, exception, stack trace hoặc
  credential. Tên dependency và trạng thái tổng quát được phép trả.
- BR-04: Request có `X-Correlation-ID` hợp lệ thì giữ nguyên; thiếu hoặc không hợp lệ thì sinh UUID.
  Giá trị hợp lệ dài 1..128 ký tự và chỉ gồm chữ, số, `.`, `_`, `-`.
- BR-05: API luôn trả `X-Correlation-ID` trên response, kể cả response lỗi do middleware xử lý.
- BR-06: Worker/job sau này nhận correlation ID từ job payload; OPS-001 cung cấp scope helper nhưng
  không định nghĩa lại payload của DLVR-001.
- BR-07: Log JSON bắt buộc có timestamp UTC, level, category, message và `correlationId` khi có.
  `tenantId`/`notificationId` chỉ xuất hiện khi context đã xác định được, không ghi giá trị giả.
- BR-08: Cấm log password, token/JWT, API key thô, encryption key, connection string, SMTP secret,
  recipient address và subject/body. Exception được log theo allow-list; response 5xx không lộ chi tiết.
- BR-09: Options bắt buộc không hợp lệ làm process fail-fast với thông báo chỉ nêu tên cấu hình,
  không in giá trị bí mật.
- BR-10: Metric name ổn định, lowercase dot notation; không dùng email, API key, notification ID,
  correlation ID hoặc tenant ID làm label để tránh dữ liệu cá nhân/cardinality cao.
- BR-11: Meter contract dự trữ `notifications.accepted`, `deliveries.sent`, `deliveries.failed`,
  `delivery.attempts` và `queue.depth`. Feature sở hữu chỉ được tăng/cập nhật metric sau khi thay đổi
  nghiệp vụ tương ứng đã commit.
- BR-12: API và Worker cùng version/build metadata; health response API trả version nhưng không trả
  commit/source path nếu chưa được cung cấp an toàn ở build time.

## Authorization

- `GET /health` và `GET /health/live` không yêu cầu xác thực để Docker/orchestrator gọi được.
- Endpoint chỉ trả dữ liệu tối thiểu theo BR-03; không liệt kê cấu hình.
- Worker không có public HTTP endpoint.

## Public contract

### `GET /health/live`

Thành công: HTTP `200`.

```json
{
  "status": "healthy",
  "service": "notification-api",
  "version": "0.1.0"
}
```

Endpoint không kiểm tra PostgreSQL hoặc Redis.

### `GET /health`

Ready: HTTP `200`; không ready: HTTP `503`.

```json
{
  "status": "healthy",
  "service": "notification-api",
  "version": "0.1.0",
  "checks": {
    "postgresql": "healthy",
    "redis": "healthy"
  }
}
```

`status` nhận `healthy` hoặc `unhealthy`; từng check nhận `healthy` hoặc `unhealthy`. Không trả
duration/exception trong public response. Cả hai endpoint trả `Content-Type: application/json` và
`Cache-Control: no-store`.

### Correlation header

- Request header: `X-Correlation-ID` — optional.
- Response header: `X-Correlation-ID` — required.
- Header không hợp lệ không làm request thất bại; API thay bằng UUID mới để tránh log injection.

## Data impact

- Không thêm bảng hoặc migration.
- PostgreSQL chỉ được mở kết nối/probe read-only cho readiness.
- Redis chỉ thực hiện lệnh ping/health tương đương; không tạo key bền vững.
- Không lưu log/metric vào PostgreSQL trong feature này.

## Acceptance criteria

- AC-01: `dotnet build` build toàn solution và architecture tests xác nhận Domain không tham chiếu
  project khác, Application chỉ tham chiếu Domain, API/Worker không truy cập trực tiếp persistence.
- AC-02: `docker compose up` khởi động được API, Worker, PostgreSQL và Redis từ repository sạch;
  API/Worker dùng cùng image tag/build. Integration-test script dùng chính Compose stack này và
  luôn dọn tài nguyên trong khối `finally`/trap kể cả khi test thất bại.
- AC-03: `/health/live` trả `200` khi API chạy dù PostgreSQL hoặc Redis không khả dụng.
- AC-04: `/health` trả `200` và hai check `healthy` khi dependency sẵn sàng; trả `503` và check tương
  ứng `unhealthy` khi dừng PostgreSQL hoặc Redis; response không chứa exception/connection string.
- AC-05: cấu hình bắt buộc thiếu/sai làm đúng process fail-fast và log không chứa giá trị secret.
- AC-06: correlation ID hợp lệ được giữ và trả lại; thiếu/không hợp lệ được thay bằng UUID; log trong
  cùng request chứa cùng ID.
- AC-07: automated redaction test ghi một payload chứa mọi nhóm secret/nội dung bị cấm và khẳng
  định output JSON không chứa giá trị gốc.
- AC-08: log là JSON parse được, có các field bắt buộc; tenant/notification context được thêm bằng
  scope mà không cần thay logger API.
- AC-09: meter test quan sát được năm instrument chuẩn, đồng thời xác nhận label contract không cho
  phép dữ liệu cá nhân hoặc định danh cardinality cao.
- AC-10: health endpoints không yêu cầu auth, có `Cache-Control: no-store`, đúng content type và
  không trả chi tiết nội bộ.
- AC-11: Worker kết thúc non-zero khi cấu hình bắt buộc sai; khi dependency tạm hỏng sau khởi động,
  worker tiếp tục sống và health state chuyển unhealthy để Docker/orchestrator quan sát.
- AC-12: `dotnet format --verify-no-changes`, unit tests và integration tests đều xanh trong CI.

## Planned files

```text
Notification.slnx
Directory.Build.props
Directory.Packages.props
.editorconfig
.env.example
src/Notification.Domain/Notification.Domain.csproj
src/Notification.Application/Notification.Application.csproj
src/Notification.Application/Abstractions/Observability/NotificationMetrics.cs
src/Notification.Infrastructure/Notification.Infrastructure.csproj
src/Notification.Infrastructure/Configuration/*
src/Notification.Infrastructure/Health/*
src/Notification.Infrastructure/Observability/*
src/Notification.Api/Notification.Api.csproj
src/Notification.Api/Program.cs
src/Notification.Api/Middleware/CorrelationIdMiddleware.cs
src/Notification.Api/Health/*
src/Notification.Worker/Notification.Worker.csproj
src/Notification.Worker/Program.cs
src/Notification.Worker/Health/*
tests/Notification.Domain.Tests/Notification.Domain.Tests.csproj
tests/Notification.Application.Tests/Notification.Application.Tests.csproj
tests/Notification.IntegrationTests/Notification.IntegrationTests.csproj
tests/Notification.IntegrationTests/Foundation/*
tests/Notification.ArchitectureTests/Notification.ArchitectureTests.csproj
deploy/docker/Dockerfile
deploy/docker/compose.yml
scripts/test-integration.ps1
.github/workflows/ci.yml
docs/features/v1/01-foundation/OPS-001-van-hanh.md
```

Tên tệp con có thể được làm rõ trong implementation plan nhưng không được thêm project/module hoặc
đổi public contract ngoài danh sách trên nếu chưa đưa spec về Review.

## Open questions

Không có. Testcontainers đã bị loại khỏi phạm vi sau security audit; Docker Compose integration
test được chọn để vẫn kiểm thử dependency thật mà không nhận dependency `SSH.NET` có lỗ hổng.
