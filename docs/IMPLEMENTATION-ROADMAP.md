# Lộ trình triển khai tuần tự (Implementation Roadmap)

Tài liệu này xác định thứ tự ưu tiên và trạng thái triển khai thực tế của từng giai đoạn trong hệ thống `notification-server`.

---

## 1. Trạng Thái Tổng Thể Các Giai Đoạn

| Giai đoạn | Nội dung | Các Feature | Trạng thái |
|---|---|---|---|
| **0 — Walking Skeleton** | Hạ tầng ban đầu, Health check, Logging bảo mật, Docker Compose | `OPS-001` | ✅ **Verified** |
| **1 — Định danh & Tenant** | Đăng ký tổ chức, Đăng nhập Admin, Refresh token, Phân quyền người dùng | `AUTH-001`, `AUTH-002`, `AUTH-004` | ✅ **Verified** |
| **2 — Thiết bị & API Key** | Quản lý thiết bị nguồn (Source Devices), cấp/thu hồi nhiều API Key | `AUTH-003`, `DEVICE-001` | ✅ **Verified** |
| **3 — Tài khoản gửi** | Cấu hình máy chủ SMTP, mã hóa AES-256-GCM, sender mặc định, gửi thử | `SEND-001`, `SEND-002`, `SEND-003` | ✅ **Verified** |
| **4 — Mẫu nội dung** | Template engine, Versioning (Draft/Active/Retired), Plain-text & Rich HTML | `TMPL-001`, `TMPL-002` | ✅ **Verified** |
| **5 — Tiếp nhận & Vận hành** | Tiếp nhận đa kênh, Tra cứu lịch sử, Lọc, Gửi lại / Hủy thủ công | `INTK-001`, `INTK-003`, `HIST-001`, `HIST-002`, `HIST-003` | ✅ **Verified** |
| **6 — Độ bền & Gửi tin** | Worker polling, Retry giãn cách (1m-5m-25m), Stuck recovery, Cảnh báo sự cố | `DLVR-001`, `DLVR-002`, `DLVR-003`, `DLVR-004` | ✅ **Verified** |
| **7 — Callback về nguồn** | Webhook callback có chữ ký HMAC-SHA256, worker đẩy trạng thái | `CBACK-001` | ✅ **Verified** |
| **8 — Kênh Chat & Push Mobile** | Tích hợp **Telegram Bot**, **Discord Webhooks** và **Mobile Push (FCM / APNs)** | `CHAN-001`, `CHAN-002`, `DEVICE-002` | ✅ **Verified** |
| **9 — Web Admin Console** | Giao diện React 19 SPA hoàn chỉnh (Lịch sử, Devices, Senders, Templates, Users, Dispatch Playground) | `WEB-001` | ✅ **Verified** |
| **10 — Gửi theo lô lớn & Idempotency** | Tiếp nhận mảng lớn người nhận (`notification_batches`), Chống trùng `Idempotency-Key` | `INTK-002`, `INTK-004` | ⏳ **Kế hoạch tiếp theo** |
| **11 — Kênh SMS Gateway** | Tích hợp gửi tin nhắn SMS thương hiệu (eSMS, Twilio...) | `CHAN-003` | ⏳ **Kế hoạch tiếp theo** |

---

## 2. Dependency Graph Hiện Tại

```text
OPS-001 (Bootstrap & Health)
  └─ AUTH-001 → AUTH-002 → AUTH-004 (Tenant Users)
       ├─ DEVICE-001 → DEVICE-002 (Source/Recipient Devices, API Keys & Push Endpoints)
       ├─ SEND-001 → SEND-002 (SMTP Senders)
       ├─ TMPL-001 → TMPL-002 (Multi-format Templates)
       │    │
       │    ▼
       ├─ INTK-001 → CHAN-001 → CHAN-002 & DEVICE-002 (Email + Telegram + Discord + Push Mobile)
       │    │
       │    ├─ DLVR-001 → DLVR-002 → DLVR-003 (Worker & Resiliency)
       │    ├─ HIST-001 → HIST-002 → HIST-003 (History & Manual Ops)
       │    ├─ CBACK-001 (Signed Webhook Callback)
       │    └─ WEB-001 (Full Admin Web Console + Dispatch Playground)
       │
       ▼ (Giai đoạn tiếp theo)
  INTK-004 (Rate Limit & Idempotency) → INTK-002 (Batch Recipients) → CHAN-003 (SMS Gateway)
```

---

## 3. Các Hạng Mục Đề Xuất Triển Khai Tiếp Theo

Dựa trên hệ thống đã hoàn thiện vững chắc 4 kênh gửi (Email, Telegram, Discord, Push Mobile), các bước tiếp theo được đề xuất theo thứ tự giá trị:

### Ưu tiên 1: `INTK-002` & `INTK-004` — Gửi thông báo Hàng loạt (Batch Intake) & Chống trùng (Idempotency)
* Mở rộng endpoint `POST /v1/notifications` chấp nhận mảng tới 500 người nhận cùng lúc trong 1 request.
* Bật header `Idempotency-Key` để bảo đảm hệ sinh thái microservices gọi lại nhiều lần không bị gửi trùng lặp thông báo.

### Ưu tiên 2: `CHAN-003` — SMS Gateway Delivery
* Tích hợp nhà cung cấp dịch vụ SMS OTP/Thông báo qua cổng API (eSMS, Twilio, Viettel...)
* Quản lý Brandname và template đăng ký trước.
