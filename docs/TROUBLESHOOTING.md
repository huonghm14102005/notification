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
