# AUTH-004 — Tài khoản người dùng trong tenant

Status: Planned
Dependencies: AUTH-002, DEVICE-001

## Đọc nhanh

Tenant owner tạo hoặc mời user. User đăng nhập bằng email/password, sau đó tự tạo nhiều device và cấp API key cho
device nguồn của mình. API trả số lượng device nhưng không trả raw key cũ.

## Phạm vi dự kiến

- Tách khái niệm user khỏi tên kỹ thuật `Admin` hiện tại nhưng giữ tương thích JWT và dữ liệu cũ.
- Email đầy đủ lowercase là định danh đăng nhập; display name không duy nhất.
- Quan hệ `tenant → users → devices → api_keys`.
- Owner quản lý user cùng tenant; user quản lý device/key của chính mình.
- List/detail user có `deviceCount`, `activeDeviceCount`; không nhúng push token hoặc key secret.
- Disable user làm phiên, device và API key ngừng xác thực nhưng giữ lịch sử.

## Chưa chốt khi SELECT

- Chỉ owner mời/tạo user hay cho phép self-registration bằng tenant code/domain.
- Luồng đặt mật khẩu ban đầu và xác minh email.
- Giới hạn user/device theo tenant.

