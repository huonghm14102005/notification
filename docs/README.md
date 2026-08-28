# Hệ Thống Thông Báo Đa Tổ Chức (Notification Server)

Hệ thống dịch vụ thông báo (Notification Server) độc lập, cô lập đa tổ chức (Multi-Tenant), hỗ trợ gửi thông báo đa kênh (**Email/SMTP**, **Telegram Bot**, **Discord Webhooks**), quản lý thiết bị nguồn, kho mẫu nội dung có phiên bản, cơ chế retry phân tán thông minh và bảng điều khiển quản trị Web Admin.

---

## 🧭 Bản Đồ Tài Liệu Kỹ Thuật (Documentation Sitemap)

| Tài liệu | Nội dung & Mục đích |
|---|---|
| [PRODUCT.md](PRODUCT.md) | Tầm nhìn sản phẩm, bài toán nghiệp vụ, người dùng mục tiêu và phạm vi |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Kiến trúc Clean Architecture, ranh giới module, luồng dữ liệu và thiết kế hệ thống |
| [SPECS.md](SPECS.md) | **Đặc tả kỹ thuật chuẩn**: Schema cơ sở dữ liệu, Public API Contracts, bảng mã lỗi và tham số vận hành |
| [IMPLEMENTATION-ROADMAP.md](IMPLEMENTATION-ROADMAP.md) | **Lộ trình phát triển**: Trạng thái các giai đoạn từ Core Engine đến Giao diện & Kênh mở rộng |
| [TARGET-DESIGN.md](TARGET-DESIGN.md) | Thiết kế mục tiêu tương lai (Multi-device, Multi-channel, Callback, Webhook HMAC) |
| [CONVENTIONS.md](CONVENTIONS.md) | Quy chuẩn mã nguồn bắt buộc (.NET, Clean Code, TypeScript, CSS, Git) |
| [WORKFLOW.md](WORKFLOW.md) | Quy trình phát triển Feature (Selected → Review → Approved → Verified) |
| [PRODUCTION-READINESS.md](PRODUCTION-READINESS.md) | Tiêu chí bảo đảm an toàn, bảo mật và hiệu năng trước khi Go-Live |
| [features/v1/README.md](features/v1/README.md) | **Danh mục 15 Feature Specs chi tiết** và trạng thái kiểm thử nghiệm thu |

---

## 🏗 Kiến Trúc Công Nghệ (Technology Stack)

```text
┌────────────────────────────────────────────────────────────────────────┐
│                        WEB ADMIN CONSOLE (SPA)                         │
│             React 19 + TypeScript + Vite + TanStack Query              │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │ HTTP / JSON
┌───────────────────────────────────▼────────────────────────────────────┐
│                    NOTIFICATION API (.NET 10 LTS)                      │
│        JWT Auth · API Key HMAC · FluentValidation · Rate Limiter       │
└──────────────────┬─────────────────────────────────┬───────────────────┘
                   │                                 │
                   ▼                                 ▼
       ┌───────────────────────┐         ┌───────────────────────┐
       │   PostgreSQL 16 DB    │         │     Redis 7 Cache     │
       │  (EF Core / Npgsql)   │         │ (Rate Limit / Health) │
       └───────────▲───────────┘         └───────────────────────┘
                   │ Polling (FOR UPDATE SKIP LOCKED)
┌──────────────────┴─────────────────────────────────────────────────────┐
│                    NOTIFICATION WORKER (.NET 10)                       │
│    Exponential Backoff · Failure Classification · HMAC Webhook Push    │
└──────────┬────────────────────────┬────────────────────────┬───────────┘
           │                        │                        │
           ▼                        ▼                        ▼
  ┌─────────────────┐      ┌─────────────────┐      ┌─────────────────┐
  │   Email / SMTP  │      │  Telegram Bot   │      │ Discord Webhook │
  │    (MailKit)    │      │   (HTTP REST)   │      │   (HTTP REST)   │
  └─────────────────┘      └─────────────────┘      └─────────────────┘
```

---

## ⚡ Các Tính Năng Đã Triển Khai & Kiểm Thử (Verified Features)

1. **Cô lập Đa tổ chức & Người dùng (`AUTH-001` → `AUTH-004`)**:
   * Đăng ký tổ chức, đăng nhập quản trị viên, quản lý tài khoản thành viên trong Tenant (`Admin` / `Member`).
   * Cấp phát và xoay vòng Refresh Token an toàn.
2. **Quản lý Thiết bị Nguồn & API Keys (`DEVICE-001`)**:
   * Quản lý thiết bị nguồn (Source Devices), cấp phát nhiều API Key theo tiền tố bí mật (`notify_...`), cấu hình Callback URL và Secret HMAC.
3. **Quản lý Tài khoản Gửi (`SEND-001` → `SEND-003`)**:
   * Cấu hình máy chủ SMTP, bảo mật mật khẩu bằng AES-256-GCM, chỉ định sender mặc định, gửi email kiểm tra kết nối trực tiếp.
4. **Mẫu Nội dung Có Phiên bản (`TMPL-001`, `TMPL-002`)**:
   * Quản lý Template theo scope (Tenant / Device), vòng đời phiên bản (`draft` → `active` → `retired`).
   * Hỗ trợ đồng thời Plain-text và Rich HTML với cú pháp biến `{{variable}}`.
5. **Tiếp nhận & Gửi Đa kênh (`INTK-001` → `INTK-003`, `CHAN-001`, `CHAN-002`)**:
   * Hỗ trợ 3 kênh gửi: **Email (SMTP)**, **Telegram (Bot API)**, **Discord (Webhooks)**.
   * Tiếp nhận Payload đa kênh với kiểm tra định dạng chặt chẽ.
6. **Độ Bền & Khắc Phục Sự Cố (`DLVR-001` → `DLVR-003`)**:
   * Worker xử lý bất đồng bộ, tự động thử lại tối đa 4 lần theo chu kỳ giãn cách lũy thừa (1 phút → 5 phút → 25 phút).
   * Tự động phục hồi các thông báo bị kẹt (`stuck recovery`).
7. **Callback Trạng Thái Có Chữ Ký (`CBACK-001`)**:
   * Đẩy kết quả gửi về hệ thống nguồn qua HTTP Webhook kèm chữ ký số `X-Notification-Signature` (HMAC-SHA256).
8. **Bảng Điều Khiển Web Admin Hiện Đại (`WEB-001`)**:
   * Toàn bộ màn hình: Đăng nhập, Lịch sử gửi, Chi tiết lần thử, Thiết bị nguồn & API Keys, Cấu hình Senders, Trình soạn thảo & Kiểm thử Template trực tiếp, Quản lý người dùng và Playground gửi thông báo nhanh.

---

## 🧪 Chất Lượng & Kiểm Thử

* **Toàn bộ Test Suite**: `dotnet test Notification.slnx` đạt **120/120 tests passed**.
* **Frontend Web Admin**: `npm --prefix web/admin test` đạt **100% passed**, build bundle tối ưu không lỗi.
* **Quy chuẩn Code**: `dotnet format --verify-no-changes` đạt **0 lỗi**.
