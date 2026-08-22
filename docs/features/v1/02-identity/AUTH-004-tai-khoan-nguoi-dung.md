# AUTH-004 — Tài khoản người dùng trong tenant

Status: Review
Selected: 2026-08-22

Dependencies: AUTH-002, DEVICE-001

## Kết quả

Tenant có nhiều tài khoản đăng nhập. Owner tạo và quản lý user; mỗi user đăng nhập bằng email/password rồi tự quản lý
device và API key của mình. Owner vẫn quản lý được mọi user/device trong tenant.

## Phạm vi

- Hỗ trợ hai role: `owner` và `member`.
- Giữ nguyên dữ liệu, ID, password hash, refresh token và liên kết hiện có trong bảng `admins`; đổi tên trong code/API theo
  hướng `user` mà không rename bảng ở migration này để giảm rủi ro cho các foreign key cũ.
- Owner tạo, xem, liệt kê và disable member cùng tenant.
- User xem hồ sơ của chính mình.
- List/detail trả `deviceCount` và `activeDeviceCount`, không trả credential hay push token.
- Member tự quản lý device/key thuộc mình; owner quản lý mọi device/key cùng tenant.
- Không hỗ trợ self-registration, invitation email, reset password, đổi role, re-enable hoặc hard-delete trong feature này.

## Quy tắc nghiệp vụ

1. Email được trim, lowercase và là định danh đăng nhập duy nhất toàn hệ thống.
2. `displayName` mặc định là phần trước `@`; cho phép trùng, dài 1–100 ký tự và owner có thể truyền khi tạo.
3. Owner tạo member bằng mật khẩu ban đầu trong request. Server chỉ lưu hash và không trả lại mật khẩu.
4. Mật khẩu tuân theo validation AUTH-002 hiện có. Việc truyền mật khẩu cho member là trách nhiệm của owner qua kênh riêng
   trong local v1; production phải thay bằng invitation token một lần.
5. Chỉ tạo role `member`; không thể tạo thêm owner hoặc tự nâng quyền qua API này.
6. Không áp quota user mới trong local. Quota/rate limit phải được chốt trước production.
7. Disable là idempotent. User disabled không đăng nhập/refresh được; mọi refresh token đang hoạt động bị revoke.
8. Disable user đồng thời disable toàn bộ device của user. API key của các device đó lập tức ngừng xác thực; notification và
   lịch sử cũ vẫn được giữ nguyên.
9. Owner không được disable chính mình. User/tenant khác luôn nhận `404`, không được biết resource có tồn tại.
10. Số lượng device được tính tại thời điểm query; `activeDeviceCount` chỉ đếm device trạng thái `active`.

## Phân quyền

| Hành động | Owner | Member |
|---|---:|---:|
| Xem hồ sơ chính mình | Có | Có |
| List/detail user trong tenant | Có | Không |
| Tạo/disable member | Có | Không |
| Quản lý device/key của mình | Có | Có |
| Quản lý device/key của user khác cùng tenant | Có | Không |

JWT tiếp tục dùng claim `sub`, `tenant_id`, `role`. Không tin `tenantId` hoặc owner user ID từ request body.

## Public API

```http
GET    /v1/users/me
POST   /v1/users
GET    /v1/users?status=active|disabled&limit=50&cursor=...
GET    /v1/users/{id}
POST   /v1/users/{id}/disable
```

Tạo user nhận `email`, `password`, `displayName` (tùy chọn). Response `201` trả user item gồm `id`, `email`,
`displayName`, `role`, `status`, `deviceCount`, `activeDeviceCount`, `createdAt`, `updatedAt`, `disabledAt`; không trả password.
List dùng cursor ổn định `(createdAt,id)`, mặc định 50 và tối đa 100. Disable trả `204`, kể cả khi đã disabled.

Mã lỗi an toàn: `VALIDATION_FAILED`, `EMAIL_ALREADY_EXISTS`, `USER_NOT_FOUND`, `CANNOT_DISABLE_SELF`,
`UNAUTHORIZED`, `FORBIDDEN`. Không trả exception message, password hash hoặc thông tin tenant khác.

## Thay đổi dữ liệu và tương thích

- Mở check constraint role từ chỉ `owner` thành `owner|member`.
- Bổ sung `display_name`, `status`, `disabled_at` vào bảng `admins`; backfill owner hiện tại là `active` và display name từ email.
- Giữ tên bảng `admins` và các foreign key hiện tại. Migration `Down` chỉ rollback được khi không còn row member.
- Login/refresh kiểm tra user `active`. Device/API-key authentication kiểm tra cả user và device `active`.
- Contract AUTH-001/AUTH-002 cũ vẫn hoạt động; tên claim và token hiện tại không đổi.

## Acceptance criteria

1. Owner tạo member; email được chuẩn hóa và mật khẩu không xuất hiện trong response/log/database dạng thô.
2. Email trùng khác hoa/thường trả `409 EMAIL_ALREADY_EXISTS` và không rò rỉ tenant sở hữu email.
3. Owner list/detail chỉ thấy user cùng tenant, đúng cursor/filter và đúng hai device count.
4. Member và owner lấy được `/v1/users/me`; member không gọi được API quản lý user.
5. Member quản lý device/key của chính mình theo DEVICE-001.
6. Member không truy cập device/key của user khác; owner cùng tenant vẫn truy cập được.
7. Disable member revoke refresh token, disable mọi device và làm JWT/API key không còn được chấp nhận ở request kế tiếp.
8. Disable lặp lại trả `204`; disable chính owner trả `409 CANNOT_DISABLE_SELF`.
9. User/tenant khác nhận `404`; list và count không lẫn dữ liệu tenant.
10. Owner/dữ liệu/token/device/key hiện có được backfill mà không đổi ID, hash hoặc lịch sử.
11. Migration chạy trên database sạch và phiên bản trước; kiểm tra down/up theo điều kiện rollback.
12. Unit, authorization, tenant-isolation, authentication và Docker integration tests đều pass.

## File dự kiến

```text
src/Notification.Domain/Identity/*
src/Notification.Application/Identity/Users/*
src/Notification.Infrastructure/Persistence/*
src/Notification.Infrastructure/Persistence/Migrations/*
src/Notification.Api/Contracts/Users/*
src/Notification.Api/Endpoints/Users/*
src/Notification.Api/Program.cs
tests/*
scripts/test-integration.ps1
docs/SPECS.md
docs/features/v1/README.md
```

## Open questions

Không còn câu hỏi chặn local. Trước production bắt buộc thiết kế invitation/reset-password, xác minh email, quota/rate
limit và quy trình chuyển/thay owner; các phần đó không được ngầm mở rộng vào AUTH-004.
