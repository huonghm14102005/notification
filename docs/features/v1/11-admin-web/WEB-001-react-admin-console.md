# WEB-001 — React admin console

Status: Verified
Selected: 2026-08-22
Approved: 2026-08-22
Verified: 2026-08-22

Dependencies: AUTH-002, HIST-001, HIST-002, HIST-003

## Outcome

Admin có giao diện web dễ dùng để đăng nhập, xem lịch sử notification, lọc trạng thái, xem chi tiết attempts và thực
hiện retry/cancel mà không phải gọi API thủ công.

## Kiến trúc đề xuất

- React + TypeScript + Vite, một SPA riêng trong `web/admin/`.
- React Router cho route; TanStack Query quản lý server state/cache.
- React Hook Form + Zod cho form/validation; CSS variables và CSS Modules, chưa phụ thuộc UI framework lớn.
- API client sinh kiểu hoặc viết typed client bám đúng `/v1`; không gọi DB và không chứa backend secret.
- Local Docker chạy SPA qua Nginx và proxy `/v1` tới API để tránh cấu hình CORS tùy tiện.
- Access token giữ trong memory. Refresh token dùng `sessionStorage` trong local v1 vì backend hiện trả token trong
  JSON; trước production cần chuyển sang BFF/HttpOnly Secure cookie bằng feature security riêng.

## Sitemap

```text
/login
/
├── /notifications
│   └── /notifications/:id
├── /devices
├── /templates
├── /senders
└── /settings
```

WEB-001 code trước ba màn hình: login, notification list và notification detail. Devices/templates/senders chỉ có
navigation placeholder cho feature UI sau, không dựng form giả.

## Luồng chính

1. Login bằng email/password; lỗi không tiết lộ tài khoản tồn tại.
2. Danh sách mới nhất, filter status/channel/time/device/API key, cursor “Tải thêm”.
3. Click một dòng mở detail: trạng thái, delivery, attempts, nội dung chỉ admin được phép xem.
4. `failed/partially_delivered` hiện nút Retry; `accepted` hiện Cancel.
5. Dialog xác nhận mô tả rõ retry chỉ gửi lại delivery failed và tạo ID mới.
6. Thành công invalidate list/detail; retry điều hướng sang notification mới. Lỗi `409` yêu cầu reload trạng thái.

## UX states bắt buộc

- Loading skeleton, empty state, inline validation, lỗi mất mạng và nút thử lại.
- Không dùng màu làm tín hiệu duy nhất; status có text/icon và đạt contrast WCAG AA.
- Keyboard focus rõ, dialog trap focus/ESC, label cho input và bảng responsive thành cards trên mobile.
- Thời gian hiển thị timezone trình duyệt, tooltip giữ UTC gốc.
- Không render raw HTML notification; HTML preview bị sandbox và tắt script ở feature sau. WEB-001 hiển thị text an toàn.

## Security

- Không log token/content; không lưu access token trong localStorage.
- CSP chặt, không `dangerouslySetInnerHTML`, escape mọi dữ liệu server.
- Khi refresh thất bại: xóa session và về login.
- Mutation chống double-click và xử lý response idempotent.
- Production không phát hành cho tới khi refresh token chuyển HttpOnly cookie/BFF và có CSRF protection.

## Public API sử dụng

- `POST /v1/auth/login`, `POST /v1/auth/refresh`, `POST /v1/auth/logout`.
- `GET /v1/notifications`, `GET /v1/notifications/{id}`.
- `POST /v1/notifications/{id}/retry`, `POST /v1/notifications/{id}/cancel`.

Không thêm/sửa backend API trong WEB-001. Incident dashboard DLVR-004 cần API đọc riêng ở feature sau.

## Acceptance criteria

1. Login/logout/refresh hoạt động; reload tab trong cùng session khôi phục phiên an toàn theo giới hạn local.
2. List filter và cursor khớp API, không request trùng do re-render.
3. Detail hiển thị attempts đúng thứ tự và không render content dưới dạng HTML thực thi.
4. Retry/cancel chỉ hiện theo state, có confirm và xử lý `201/200/204/409` đúng.
5. Unauthorized tự về login; lỗi mạng không làm mất filter hiện tại.
6. Không có token/secret trong console, URL, analytics hoặc localStorage.
7. Keyboard navigation và contrast đạt kiểm tra accessibility tự động.
8. Unit/component tests và Playwright smoke test chạy trong CI/Docker.

## Planned files

```text
web/admin/package.json
web/admin/src/app/*
web/admin/src/auth/*
web/admin/src/notifications/*
web/admin/src/shared/api/*
web/admin/src/styles/*
web/admin/tests/*
deploy/docker/admin-web.nginx.conf
deploy/docker/compose.yml
.github/workflows/ci.yml
README.md
```

## Open questions

Không còn câu hỏi chặn cho thiết kế local. Trước khi approve cần xác nhận: dùng React/TypeScript/Vite, giao diện
đầu tiên chỉ gồm auth + notification operations, và production auth sẽ được harden bằng HttpOnly cookie/BFF.

## Verification

- React production build và unit/accessibility tests pass.
- Playwright Chromium smoke test pass cho luồng login và notification list.
- Docker Compose phục vụ SPA qua Nginx, proxy cùng origin và phát CSP/security headers.
- Toàn bộ .NET format/build, 109 tests và Docker integration/migration down-up pass.
