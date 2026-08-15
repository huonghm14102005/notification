# notification-server (notify-api)

Dịch vụ thông báo độc lập, đa tổ chức. Các ứng dụng gửi cho nó một thông điệp cần đến tay một người;
dịch vụ chuyển thông điệp đó tới tài khoản gửi mà tổ chức đã cấu hình. Việc gửi diễn ra bất đồng bộ,
có thử lại và có thể truy vết.

Phiên bản đầu chỉ hỗ trợ kênh email.

## Trạng thái

Đang phát triển theo feature; OPS-001, AUTH-001 và AUTH-002 đã Verified.

## Chạy local

```powershell
docker compose -f deploy/docker/compose.yml up --build --wait
```

Compose chạy migration trước API/Worker và tạo tài khoản thử nghiệm idempotent:

| Trường | Giá trị |
|---|---|
| URL API | `http://localhost:3100` |
| Tenant | `Test Organization` (`test-organization`) |
| Email | `admin@local.test` |
| Mật khẩu | `12345678` |

Tài khoản trên chỉ dành cho local/test. Seed bị chặn ở Production kể cả khi
`SEED_TEST_ADMIN=true`; không dùng credential này cho môi trường thật.

Đăng nhập qua `POST /v1/auth/login`; dùng `POST /v1/auth/refresh` để rotate refresh token và
`POST /v1/auth/logout` kèm Bearer access token để thu hồi phiên. Refresh token chỉ dùng được một lần.

## Tài liệu

- [Product Brief](docs/PRODUCT.md) — vấn đề, người dùng, giá trị, chỉ số thành công, ràng buộc, giả
  định, rủi ro, phạm vi loại trừ.
- [MVP](docs/MVP.md) — hành trình đầu-cuối, phân loại Must/Should/Could/Not now và điều kiện hoàn tất.
- [Domain Map](docs/domain-map.md) — các vùng trách nhiệm nghiệp vụ, vòng đời, invariant, quyền sở
  hữu dữ liệu.
- [Feature Map](docs/feature-map.md) — bóc hành trình thành các capability theo từng domain.
- [Architecture](docs/ARCHITECTURE.md) — hình hài kỹ thuật và các quyết định kèm lý do.
- [Conventions](docs/CONVENTIONS.md) — quy tắc triển khai suy ra từ kiến trúc.
- [Specs](docs/SPECS.md) — endpoint, mô hình dữ liệu, trạng thái, mã lỗi, giới hạn, biến môi trường.
- [Workflow](docs/WORKFLOW.md) — vòng đời feature, quyền của AI theo trạng thái, release và rollback.
- [Thiết kế solution .NET](docs/DOTNET-SOLUTION.md) — project, module, chiều phụ thuộc và ranh giới Docker.
- [Lộ trình triển khai](docs/IMPLEMENTATION-ROADMAP.md) — thứ tự feature và quy trình code tuần tự.
- [Danh mục feature v1](docs/features/v1/README.md) — feature được nhóm theo module phát triển.

## Quyết định đã chốt

- Dịch vụ độc lập, không phải một module của dịch vụ CDN/Media hiện có.
- Làm mới hoàn toàn: cơ sở dữ liệu riêng, cơ chế định danh và khoá riêng; không dùng lại tenant,
  người dùng hay API key của dịch vụ CDN.
- Tenant là tổ chức sở hữu (trường đại học). Mỗi hệ thống nguồn — điểm, điểm rèn luyện, sau này là
  log lỗi — là một ứng dụng gửi, có API key riêng.
- Ứng dụng gửi tự cung cấp tiêu đề và nội dung; template là tuỳ chọn.
- Mỗi yêu cầu đi đúng một kênh. Phiên bản đầu chỉ có email, mở rộng kênh khác ở phiên bản sau.
- Nền tảng đích là ASP.NET Core API + .NET Worker Service, PostgreSQL và Redis, đóng gói bằng Docker.
