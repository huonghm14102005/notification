# Thiết kế solution .NET

Tài liệu này ánh xạ kiến trúc nghiệp vụ của `notify-api` sang ASP.NET Core. Đây là cấu trúc đích để
lập kế hoạch; chưa phải sự cho phép tạo mã nguồn.

## 1. Nền tảng đích

| Thành phần | Lựa chọn |
|---|---|
| Runtime | .NET 10 LTS |
| HTTP API | ASP.NET Core Web API |
| Worker | .NET Worker Service (`BackgroundService`) |
| Cơ sở dữ liệu | PostgreSQL qua Npgsql và EF Core |
| Hàng đợi, lịch retry, rate limit | Redis; adapter hàng đợi nằm sau interface của Application |
| SMTP | MailKit, chỉ được dùng trong Infrastructure |
| Validation | FluentValidation tại biên HTTP/Application |
| Log/metrics | `ILogger`, JSON console, OpenTelemetry metrics |
| Test | xUnit, Testcontainers cho PostgreSQL/Redis và SMTP giả |
| Đóng gói | Một Docker image, hai entrypoint API/Worker; Docker Compose cho môi trường local |

Không chốt thư viện queue ở giai đoạn thiết kế. Feature DLVR-001 phải đánh giá MassTransit,
Hangfire và một adapter Redis tối giản dựa trên yêu cầu retry, delayed job và khả năng dựng lại job
từ PostgreSQL. Domain/Application không phụ thuộc lựa chọn này.

## 2. Cấu trúc solution

```text
notification-server/
├── src/
│   ├── Notification.Api/
│   │   ├── Endpoints/
│   │   ├── Middleware/
│   │   └── Program.cs
│   ├── Notification.Worker/
│   │   ├── Consumers/
│   │   ├── ScheduledJobs/
│   │   └── Program.cs
│   ├── Notification.Domain/
│   │   ├── Identity/
│   │   ├── Senders/
│   │   ├── Templates/
│   │   ├── Notifications/
│   │   ├── Delivery/
│   │   └── Shared/
│   ├── Notification.Application/
│   │   ├── Identity/
│   │   ├── Senders/
│   │   ├── Templates/
│   │   ├── Intake/
│   │   ├── Delivery/
│   │   ├── History/
│   │   └── Abstractions/
│   └── Notification.Infrastructure/
│       ├── Persistence/
│       ├── Queueing/
│       ├── Email/
│       ├── Security/
│       └── Observability/
├── tests/
│   ├── Notification.Domain.Tests/
│   ├── Notification.Application.Tests/
│   ├── Notification.IntegrationTests/
│   └── Notification.ArchitectureTests/
├── deploy/
│   ├── docker/
│   └── nginx/
└── docs/
```

## 3. Chiều phụ thuộc

```text
Api ───────────┐
               ├──> Application ──> Domain
Worker ────────┘          ▲
                          │ implements ports
Infrastructure ───────────┘
```

- Domain không tham chiếu ASP.NET Core, EF Core, Redis, SMTP hoặc Infrastructure.
- Application sở hữu use case, transaction boundary và các interface cho persistence, queue,
  clock, crypto và email.
- Infrastructure cài đặt các interface; kiểu của thư viện ngoài không được rò ra Application.
- API và Worker chỉ là composition root/transport. Endpoint và consumer không chứa nghiệp vụ.
- Module chỉ gọi public use case của module khác; không dùng trực tiếp repository của module khác.

## 4. Ánh xạ module nghiệp vụ

| Module tài liệu | Application | Domain/data sở hữu | Điểm vào |
|---|---|---|---|
| Foundation | health, correlation, cấu hình | Không sở hữu nghiệp vụ | API + Worker |
| Identity | đăng ký, phiên, API key | tenant, admin, refresh token, API key | API |
| Sender | quản lý/chọn sender, gửi thử | sender và bí mật mã hoá | API |
| Template | CRUD và render mẫu | template | API, được Intake gọi |
| Intake | kiểm tra và tiếp nhận | batch, notification | API |
| Delivery | gửi, retry, recovery, alert | delivery attempt, failure alert | Worker |
| History | tra cứu, huỷ, gửi lại | Không sở hữu bảng; thao tác qua public use case | API |

History là read/operation module, không được sở hữu hoặc sửa trực tiếp aggregate của Delivery.
Lệnh retry/cancel đi qua application interface của Notifications/Delivery để giữ invariant.

## 5. Ranh giới triển khai Docker

- `notification-api` và `notification-worker` dùng cùng image/tag nhưng chạy command khác nhau.
- PostgreSQL là nguồn sự thật; Redis có thể mất và được dựng lại bằng DLVR-003.
- API stateless và không gửi SMTP, ngoại trừ SEND-003 là thao tác kiểm tra đồng bộ đã được giới hạn.
- Worker không mở public API; health được cung cấp cho orchestrator bằng health endpoint nội bộ hoặc
  health command.
- Migration chạy bằng entrypoint riêng trước khi rollout API/Worker, không chạy cạnh tranh khi nhiều
  replica khởi động.

## 6. Quy tắc chưa được phép tự chốt lúc code

- Thư viện queue cụ thể và cơ chế distributed lock.
- Chính sách Redis fail-open/fail-closed của INTK-004.
- Người nhận cảnh báo của DLVR-004.
- Đổi major .NET khỏi .NET 10 LTS.

Các mục trên phải được giải quyết trong feature spec liên quan trước trạng thái `Approved`.
