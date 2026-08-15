# DLVR-001 — Worker gửi bất đồng bộ và ghi lại từng lần gửi

Status: Planned

Đặc tả đầy đủ (Business rules, Public contract, Data impact, Acceptance criteria, Planned files) được
viết khi có lệnh `SELECT DLVR-001`; xem [README.md](../README.md).

## Outcome

Thông báo đã tiếp nhận thực sự tới hòm thư người nhận, và có vết chứng minh điều đó.

## Actor

Tiến trình worker (máy).

## Trigger

Có việc trong hàng đợi, hoặc có thông báo `accepted` tới hạn gửi.

## In scope

- Poll PostgreSQL để claim notification `accepted` đã tới `next_attempt_at`
- Đọc thông báo và tài khoản gửi từ Postgres, giải mã nội dung
- Gửi qua cổng `EmailSender` (SMTP)
- Ghi một dòng `delivery_attempts` cho mỗi lần thử, chỉ ghi thêm
- Cập nhật `sending` → `sent` hoặc để DLVR-002 xử lý khi hỏng
- Hàm xử lý idempotent: chạy lại không gửi trùng khi đã kết thúc

## Out of scope

- Thử lại (DLVR-002)
- Cảnh báo (DLVR-004)
- Kênh khác ngoài email

## Preconditions

- PRE-01: thông báo ở trạng thái `accepted`
- PRE-02: tài khoản gửi còn `active`

## Dependencies

INTK-001, SEND-001

## Tham chiếu

- Must-have: M-08 ([MVP.md](../../../MVP.md))
- Dữ liệu: `notifications` (cập nhật trạng thái), `delivery_attempts` (ghi thêm) — SPECS.md §6
- Contract: trạng thái và chỉ mục polling của `notifications` — SPECS.md §5, §6

## Business rules

Chưa viết (Planned).

## Authorization

Chưa viết (Planned).

## Public contract

Chưa viết (Planned).

## Data impact

Chưa viết (Planned).

## Acceptance criteria

Chưa viết (Planned).

## Planned files

Chưa viết (Planned).

## Open questions

Không có.
