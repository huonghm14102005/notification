# Tổng hợp Lỗi Thường Gặp & Hướng Dẫn Khắc Phục (Troubleshooting Guide)

Tài liệu này ghi lại toàn bộ các sự cố kỹ thuật đã gặp phải trong quá trình phát triển, cấu hình môi trường cục bộ và cách xử lý triệt để cho hệ thống `notification-server`.

---

## 1. Lỗi thiếu chuỗi kết nối Database (`ArgumentNullException: uriString`)

### Hiện tượng
Khi chạy `dotnet run --project src/Notification.Api` hoặc `dotnet ef database update`:
```text
Unhandled exception. System.ArgumentNullException: Value cannot be null. (Parameter 'uriString')
   at Notification.Infrastructure.DependencyInjection.ToConnectionString(String url)
```

### Nguyên nhân
- .NET runtime khi chạy trực tiếp ngoài terminal (PowerShell/CMD) không tự động nạp các biến từ file `.env` nếu biến môi trường chưa được export vào OS session.
- EF Core Design-time tooling (`dotnet ef`) không có sẵn context factory để đọc cấu hình độc lập.

### Hướng giải quyết
1. **Tạo bộ nạp môi trường tự động**: Viết class `EnvFile.cs` trong `Notification.Infrastructure.Bootstrap` để tự động quét và load file `.env` ngay khi `Program.cs` khởi động.
2. **Hỗ trợ EF Tooling**: Tạo `NotificationDbContextFactory.cs` cài đặt `IDesignTimeDbContextFactory<NotificationDbContext>` để các lệnh migration luôn lấy được chuỗi kết nối hợp lệ.

---

## 2. Lỗi sai định dạng Encryption Key (`OptionsValidationException`)

### Hiện tượng
Khi chạy integration test hoặc khởi động worker:
```text
Microsoft.Extensions.Options.OptionsValidationException: ENCRYPTION_KEY must be valid base64 encoding exactly 32 bytes.
```

### Nguyên nhân
File `.env.example` ban đầu chứa chuỗi placeholder chữ (`base64-encoded-exactly-32-random-bytes`), không phải là một chuỗi Base64 hợp lệ đại diện cho đúng 32 bytes (256-bit AES key).

### Hướng giải quyết
Cập nhật `.env` với chuỗi Base64 32 bytes chuẩn xác:
```properties
ENCRYPTION_KEY=MDEyMzQ1Njc4OTAxMjM0NTY3ODkwMTIzNDU2Nzg5MDE=
```
*(Trong production, sinh bằng lệnh: `openssl rand -base64 32`)*

---

## 3. Lỗi xác thực mật khẩu PostgreSQL (`password authentication failed for user "notify"`)

### Hiện tượng
Khi chạy `dotnet ef database update`:
```text
Npgsql.PostgresException (0x80004005): 28P01: password authentication failed for user "notify"
```

### Nguyên nhân
1. Mật khẩu mặc định trong Docker container là `notify-local`, nhưng chuỗi kết nối trong file `.env` trước đó để là `change-me`.
2. Trên máy tính của lập trình viên có sẵn một dịch vụ PostgreSQL cài trực tiếp trên Windows (PID 6600) đang chiếm cổng `5432`. Lệnh `dotnet ef` kết nối nhầm vào PostgreSQL của Windows thay vì PostgreSQL trong Docker.

### Hướng giải quyết
1. Đổi cổng ánh xạ của Docker PostgreSQL sang `5433` (`5433:5432`) trong `deploy/docker/compose.yml`.
2. Cập nhật chuỗi kết nối trong `.env`:
   ```properties
   DATABASE_URL=postgresql://notify:notify-local@localhost:5433/notification
   ```

---

## 4. Lỗi dữ liệu cũ khi áp dụng Migration (`check constraint violated` & `foreign key violation`)

### Hiện tượng
Khi chạy migration `AddDeliveries`:
```text
23514: check constraint "ck_notifications_status" of relation "notifications" is violated by some row
23503: insert or update on table "delivery_attempts" violates foreign key constraint "FK_delivery_attempts_deliveries_delivery_id"
```

### Nguyên nhân
Dữ liệu test cũ còn lưu trong CSDL từ các phiên bản sơ khởi có chứa trạng thái `sending`, `retrying` hoặc các bản ghi `delivery_attempts` mồ côi (chưa có liên kết với bảng `deliveries` mới).

### Hướng giải quyết
Bổ sung các câu lệnh SQL chuẩn hóa dữ liệu vào file migration `20260821093207_AddDeliveries.cs` trước khi áp dụng ràng buộc khóa:
```csharp
migrationBuilder.Sql("UPDATE notifications SET status = 'delivered' WHERE status = 'sent';");
migrationBuilder.Sql("UPDATE notifications SET status = 'processing' WHERE status IN ('sending', 'retrying');");
migrationBuilder.Sql("UPDATE notifications SET status = 'accepted' WHERE status NOT IN ('accepted','processing','delivered','partially_delivered','failed','cancelled');");
migrationBuilder.Sql("DELETE FROM delivery_attempts WHERE delivery_id NOT IN (SELECT id FROM deliveries);");
```

---

## 5. Lỗi không đăng nhập được trên Web Admin Console (`admin@local.test`)

### Hiện tượng
Đăng nhập tại `http://localhost:5173` bằng tài khoản mặc định `admin@local.test` / `12345678` bị báo lỗi mạng hoặc không phản hồi.

### Nguyên nhân
1. Cấu hình Vite Proxy trong `web/admin/vite.config.ts` trỏ về cổng `http://localhost:3100` (cổng API trong Docker) thay vì `http://localhost:5000` (cổng khi chạy `dotnet run` ở local).
2. API Backend chưa bật CORS để nhận request trực tiếp từ frontend.

### Hướng giải quyết
1. Cập nhật `vite.config.ts`:
   ```typescript
   server: {
     port: 5173,
     proxy: {
       '/v1': process.env.VITE_API_URL || 'http://localhost:5000'
     }
   }
   ```
2. Thêm middleware `app.UseCors()` trong `src/Notification.Api/Program.cs`.

---

## 6. Lỗi xung đột tiến trình khi Build (`file is locked by Notification.Api.exe`)

### Hiện tượng
Khi chạy `dotnet build` hoặc `dotnet test`:
```text
error MSB3027: Could not copy "obj\Debug\net10.0\apphost.exe" to "bin\Debug\net10.0\Notification.Api.exe". The file is locked by: "Notification.Api (PID)"
```

### Nguyên nhân
Tiến trình `Notification.Api.exe` đang chạy ngầm trên Windows và khóa file binary.

### Hướng giải quyết
Dừng tiến trình API đang chạy bằng PowerShell:
```powershell
Stop-Process -Name "Notification.Api" -Force -ErrorAction SilentlyContinue
```
Sau đó tiến hành build lại bình thường.

---

## 7. Lỗi Frontend trên Vercel gọi nhầm URL Vercel thay vì Backend (Render)

### Hiện tượng
Khi deploy Web Admin lên Vercel (`https://notification-xxx.vercel.app`) và Backend lên Render (`https://notification-len1.onrender.com`), khi bấm Đăng nhập thì trình duyệt gửi request tới `https://notification-xxx.vercel.app/v1/auth/login` và bị 302/404 Redirect thay vì gửi sang Render.

### Nguyên nhân
1. `AuthContext.tsx` dùng đường dẫn tương đối `/v1/...` mà không ghép biến `VITE_API_URL`.
2. File `vercel.json` trước đó chưa cấu hình rewrite trỏ sang URL Render thật.

### Hướng giải quyết
1. Cập nhật `AuthContext.tsx` tự động nối biến môi trường `import.meta.env.VITE_API_URL`.
2. Cập nhật `web/admin/vercel.json` rewrite toàn bộ `/v1/:path*` sang `https://notification-len1.onrender.com/v1/:path*`.
3. Cài đặt biến môi trường trên Vercel: `VITE_API_URL = https://notification-len1.onrender.com`.

---

## 8. Lỗi Backend trên Render Crash khi khởi động (`Exited with status 139` / `OptionsValidationException`)

### Hiện tượng
Khi deploy Backend lên Render, log hiển thị:
```text
at Microsoft.Extensions.Options.StartupValidator.Validate()
at Microsoft.Extensions.Hosting.Internal.Host.StartAsync()
==> Exited with status 139
```

### Nguyên nhân
1. Render cung cấp chuỗi `DATABASE_URL` dạng `postgres://...` (không phải `postgresql://`), khiến bộ kiểm tra `FoundationOptionsValidator` từ chối URL và ném ngoại lệ dừng ứng dụng.
2. Render Redis URL có thể dùng scheme `rediss://` (kết nối Redis bảo mật qua SSL) chưa nằm trong danh sách scheme hợp lệ.
3. Chuỗi kết nối Database từ các dịch vụ đám mây (Render, Supabase, Neon) yêu cầu SSL mode (`sslmode=require`) khi kết nối.

### Hướng giải quyết
1. Cập nhật `FoundationOptionsValidator.cs` chấp nhận cả `postgres://` và `postgresql://`, cũng như `redis://` và `rediss://`.
2. Cập nhật `DependencyInjection.ToConnectionString()` tự động nhận diện cổng mặc định 5432 và kích hoạt `SslMode.Require` khi kết nối tới các dịch vụ Cloud Database.

---

## 9. Lỗi Parse URL do dán nhầm cú pháp Markdown vào `VITE_API_URL`

### Hiện tượng
Khi gửi request API từ trình duyệt:
```text
Failed to execute 'fetch' on 'Window': Failed to parse URL from https://notification-len1.onrender.com](https://notification-len1.onrender.com)/v1/tenants/register
```

### Nguyên nhân
Khi copy đường link từ trình soạn thảo markdown vào trang cài đặt biến môi trường trên Vercel (`VITE_API_URL`), chuỗi URL bị dính cú pháp markdown `[url](url)`.

### Hướng giải quyết
1. Trên Vercel Settings: Sửa giá trị `VITE_API_URL` thành URL chuẩn xác: `https://notification-len1.onrender.com` (chỉ duy nhất link, không có ngoặc vuông hay ngoặc tròn).
2. Trong mã nguồn `AuthContext.tsx`: Đã bổ sung hàm `cleanUrl()` tự động lọc và trích xuất URL sạch nếu bị dính ký tự thừa.

---

## 10. Lỗi 401 UNAUTHORIZED khi gửi tin trực tiếp từ Web Admin Console

### Hiện tượng
Khi dùng tính năng **Gửi thông báo thử nghiệm (Dispatch Playground)** trên Web Admin Console, API trả về:
```json
{ "error": "Unauthorized", "code": "UNAUTHORIZED", "statusCode": 401 }
```

### Nguyên nhân
Endpoint `POST /v1/notifications` trước đó chỉ cấu hình chính sách xác thực `.RequireAuthorization("ApiKey")` (dành riêng cho máy móc / backend bên ngoài gọi bằng API Key). Khi Quản trị viên đăng nhập vào Web Console và gửi request với JWT Bearer Token, API từ chối xác thực.

### Hướng giải quyết
1. Cấu hình policy `AdminOrApiKey` trong `Program.cs` chấp nhận cả JWT Token của Admin và API Key của máy chủ client.
2. Bổ sung phương thức `EnsureAdminDispatchContextAsync` trong `NotificationRepository` để tự động gán hoặc tạo thiết bị ảo (`Device`) và khóa nội bộ (`ApiKey`) cho Quản trị viên khi thực hiện gửi thử từ giao diện Web.

---

## 11. Lỗi 409 SENDER_NOT_FOUND khi gửi qua kênh Discord / Telegram / Push

### Hiện tượng
Khi chọn kênh **Discord (Webhook)** hoặc **Telegram (Chat ID)** và bấm gửi, API trả về:
```json
{ "error": "Sender not found", "code": "SENDER_NOT_FOUND", "statusCode": 409 }
```

### Nguyên nhân
Logic ban đầu trong `AcceptNotificationHandler` quy định mọi thông báo đều phải tìm thấy một cấu hình `Sender` (SMTP Sender) trong CSDL. Đối với các tài khoản mới trên Cloud chưa kịp tạo cấu hình SMTP, logic này ném lỗi `SENDER_NOT_FOUND` ngay cả khi người dùng chỉ muốn gửi qua Discord hoặc Telegram.

### Hướng giải quyết
Cập nhật `AcceptNotificationHandler.cs`:
- Đối với các kênh gửi trực tiếp qua Webhook/Token như `discord`, `telegram`, `push`: Cho phép `SenderId` mang giá trị `null` (không bắt buộc phải có máy chủ SMTP).
- Chỉ riêng kênh `email` mới bắt buộc phải cấu hình máy chủ SMTP Sender.

---

## 12. Lỗi 503 SERVICE_UNAVAILABLE do vi phạm Check Constraint CSDL PostgreSQL

### Hiện tượng
Khi gửi thông báo Telegram hoặc Discord lên PostgreSQL thật trên Render:
```json
{ "error": "Service unavailable", "code": "SERVICE_UNAVAILABLE", "statusCode": 503 }
```
*(Trong khi chạy unit test và in-memory test ở local vẫn pass)*.

### Nguyên nhân
- Trong bảng `deliveries` trên PostgreSQL, migration ban đầu đặt ràng buộc kiểm tra toàn vẹn:
  ```sql
  CONSTRAINT ck_deliveries_channel CHECK (channel = 'email')
  ```
- Trình giả lập Test In-Memory không kiểm tra luật SQL Check Constraint nên test vẫn xanh. Nhưng PostgreSQL thật trên Render sẽ chặn thao tác INSERT khi `channel` mang giá trị `'telegram'`, `'discord'` hoặc `'push'` với mã lỗi SQL `23514`.

### Hướng giải quyết
1. Cập nhật `DeliveryConfiguration.cs` và `DeviceConfiguration.cs` hỗ trợ đầy đủ các kênh:
   ```sql
   CONSTRAINT ck_deliveries_channel CHECK (channel IN ('email', 'telegram', 'discord', 'push'))
   CONSTRAINT ck_devices_role CHECK (role IN ('source', 'both', 'recipient'))
   ```
2. Tạo và áp dụng Migration `20260828071811_UpdateChannelAndDeviceRoleConstraints.cs` lên CSDL PostgreSQL.

---

## 13. Lỗi thông báo ở trạng thái "Đã tiếp nhận" (Accepted) nhưng không gửi đi

### Hiện tượng
Thông báo đã tạo thành công và xuất hiện trong bảng Lịch sử với trạng thái **"Đã tiếp nhận"**, nhưng không có tin nhắn nào nổ về Discord / Telegram / Email.

### Nguyên nhân
Hệ thống được thiết kế theo kiến trúc Microservices tách rời giữa `Notification.Api` (nhận tin) và `Notification.Worker` (tiến trình nền quét hàng đợi và gửi tin). Khi triển khai bản gọn (Monolith/Single Service) trên Render với chỉ 1 Web Service, chỉ có `Notification.Api` hoạt động, dẫn đến không có tiến trình worker nào kích hoạt việc gửi tin ra ngoài.

### Hướng giải quyết
Đăng ký các Background Worker (`NotificationDeliveryWorker`, `CallbackDeliveryWorker`, `FailureAlertWorker`) chạy trực tiếp dưới dạng `IHostedService` bên trong `Notification.Api/Program.cs`. Nhờ đó, một Web Service duy nhất có thể vừa phục vụ API, vừa tự động gửi thông báo ngầm theo thời gian thực (chu kỳ poll 500ms).

---

## 14. Lỗi gửi Email báo SENDER_NOT_FOUND dù đã thêm máy chủ SMTP

### Hiện tượng
Đã thêm thành công máy chủ SMTP trong mục **Cấu hình SMTP**, nhưng khi gửi email thử nghiệm vẫn bị báo lỗi `SENDER_NOT_FOUND`.

### Nguyên nhân
Máy chủ SMTP được tạo ra có `Sender Key` là một chuỗi UUID tự sinh (ví dụ: `74785606-3536-4122-...`) và chưa được gạt cờ "Đặt làm mặc định". Khi người dùng gửi mail và để trống ô `Sender Key`, hệ thống mặc định tìm bản ghi có `IsDefault = true` nên không khớp.

### Hướng giải quyết
Bổ sung cơ chế **Smart Sender Fallback** trong `SenderRepository.ResolveAsync`: Nếu người dùng không nhập `Sender Key` hoặc nhập `default`, hệ thống sẽ tự động lấy máy chủ SMTP đang hoạt động đầu tiên của tổ chức để gửi thư mà không cần người dùng phải copy-paste chuỗi UUID thủ công.

---

## 15. Lỗi `NETSDK1152: Found multiple publish output files with the same relative path` khi Docker Build

### Hiện tượng
Khi Render build Docker image ở bước `dotnet publish src/Notification.Api/Notification.Api.csproj -c Release`:
```text
error NETSDK1152: Found multiple publish output files with the same relative path:
/source/src/Notification.Worker/appsettings.Development.json, /source/src/Notification.Api/appsettings.Development.json,
/source/src/Notification.Worker/appsettings.json, /source/src/Notification.Api/appsettings.json.
```

### Nguyên nhân
Khi `Notification.Api` tham chiếu tới `Notification.Worker`, trình biên dịch .NET SDK phát hiện cả hai project đều có file cấu hình trùng tên `appsettings.json` và `appsettings.Development.json` copy vào thư mục publish, dẫn đến xung đột theo luật kiểm tra mặc định của SDK.

### Hướng giải quyết
Bổ sung cấu hình bỏ qua cảnh báo trùng file cấu hình phụ trong `src/Notification.Api/Notification.Api.csproj`:
```xml
<PropertyGroup>
  <ErrorOnDuplicatePublishOutputFiles>false</ErrorOnDuplicatePublishOutputFiles>
</PropertyGroup>
```
File `appsettings.json` chính của Web Service API sẽ được ưu tiên sử dụng.

---

## 16. Lỗi Telegram nhận nhầm Password của Gmail làm Bot Token

### Hiện tượng
Gửi tin nhắn Telegram bằng cú pháp `botToken:chatId` ở ô Target, nhưng tin nhắn không đến nhóm Telegram.

### Nguyên nhân
Khi cơ chế Smart Sender Fallback tìm thấy tài khoản Gmail SMTP trong CSDL, hàm `ResolveCredentials` của kênh Telegram giải mã mật khẩu ứng dụng Gmail gán vào `botToken` do chưa kiểm tra điều kiện `sender.Channel == "telegram"`.

### Hướng giải quyết
1. Đặt ưu tiên cao nhất cho định dạng kết hợp `botToken:chatId` truyền trực tiếp qua trường Target.
2. Chỉ lấy thông tin xác thực từ Sender trong CSDL khi cấu hình đó có `Channel == "telegram"`.

---

## 18. Lỗi SMTP_TIMEOUT / SMTP_TEST_TIMEOUT trên hạ tầng Cloud (Render Free Tier)

### Hiện tượng
Gửi email hoặc bấm kiểm tra cấu hình SMTP trên Render Cloud bị quay vô tận và báo lỗi sau 30 giây:
```json
{ "error": "SMTP test timed out", "code": "SMTP_TEST_TIMEOUT", "statusCode": 504 }
```

### Nguyên nhân
Render (và hầu hết các nền tảng PaaS/Serverless miễn phí như Vercel, AWS Sandbox) áp dụng chính sách bảo mật mạng chặn toàn bộ lưu lượng Outbound TCP trên các cổng SMTP truyền thống (`25`, `465`, `587`, `2525`) nhằm ngăn chặn nguy cơ máy chủ bị lợi dụng phát tán thư rác (Spam botnet).

### Hướng giải quyết
1. **Môi trường Cloud miễn phí (Render Free Tier)**:
   - Khuyến nghị sử dụng các dịch vụ gửi Email thông qua **HTTP REST API trên Cổng HTTPS 443** (như Resend API, SendGrid Web API, Brevo API). Do chạy trên cổng Web 443, các dịch vụ này không bao giờ bị tường lửa chặn và gửi thư tức thì (< 500ms).
   - Hoặc nâng cấp lên gói trả phí của Render và gửi yêu cầu mở cổng SMTP Outbound.
2. **Môi trường Cục bộ (Localhost) hoặc Máy chủ Riêng (VPS / Docker Dedicated)**:
   - Không bị giới hạn bởi tường lửa PaaS, máy chủ kết nối trực tiếp tới `smtp.gmail.com:465` (SSL) và gửi thư thành công 100%.







