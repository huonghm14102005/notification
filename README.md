# notification-server (notify-api)

Dịch vụ thông báo độc lập, đa tổ chức. Các ứng dụng gửi cho nó một thông điệp cần đến tay một người;
dịch vụ chuyển thông điệp đó tới tài khoản gửi mà tổ chức đã cấu hình. Việc gửi diễn ra bất đồng bộ,
có thử lại và có thể truy vết.

Phiên bản đầu chỉ hỗ trợ kênh email.

## Trạng thái

Giai đoạn định nghĩa — chưa có mã nguồn.

## Tài liệu

- [Product Brief](docs/PRODUCT.md) — vấn đề, người dùng, giá trị, chỉ số thành công, ràng buộc, giả
  định, rủi ro, phạm vi loại trừ.
- [MVP](docs/MVP.md) — hành trình đầu-cuối, phân loại Must/Should/Could/Not now và điều kiện hoàn tất.
- [Domain Map](docs/domain-map.md) — các vùng trách nhiệm nghiệp vụ, vòng đời, invariant, quyền sở
  hữu dữ liệu.
- [Feature Map](docs/feature-map.md) — bóc hành trình thành các capability theo từng domain.
- [Architecture](docs/ARCHITECTURE.md) — hình hài kỹ thuật và các quyết định kèm lý do.
- [Conventions](docs/CONVENTIONS.md) — quy tắc triển khai suy ra từ kiến trúc.
- [Workflow](docs/WORKFLOW.md) — vòng đời feature, quyền của AI theo trạng thái, release và rollback.

## Quyết định đã chốt

- Dịch vụ độc lập, không phải một module của dịch vụ CDN/Media hiện có.
- Làm mới hoàn toàn: cơ sở dữ liệu riêng, cơ chế định danh và khoá riêng; không dùng lại tenant,
  người dùng hay API key của dịch vụ CDN.
- Tenant là tổ chức sở hữu (trường đại học). Mỗi hệ thống nguồn — điểm, điểm rèn luyện, sau này là
  log lỗi — là một ứng dụng gửi, có API key riêng.
- Ứng dụng gửi tự cung cấp tiêu đề và nội dung; template là tuỳ chọn.
- Mỗi yêu cầu đi đúng một kênh. Phiên bản đầu chỉ có email, mở rộng kênh khác ở phiên bản sau.
