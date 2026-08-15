# HIST-001 — Tra cứu một thông báo kèm các lần gửi

Status: Planned

Đặc tả đầy đủ (Business rules, Public contract, Data impact, Acceptance criteria, Planned files) được
viết khi có lệnh `SELECT HIST-001`; xem [README.md](../README.md).

## Outcome

Trả lời được câu "thư đã tới chưa" mà không cần đọc log máy chủ.

## Actor

Quản trị viên, và hệ thống nguồn với thông báo do chính nó tạo.

## Trigger

Gọi endpoint chi tiết với mã thông báo.

## In scope

- Trả trạng thái, thời điểm, người nhận, `recipient_ref`, lý do hỏng
- Kèm danh sách các lần gửi theo thứ tự
- Quản trị viên đọc được nội dung thư; mỗi lần đọc ghi vết `adminId` và `notificationId`
- Khoá API chỉ thấy siêu dữ liệu, và chỉ với thông báo do chính nó tạo
- Thông báo của tổ chức khác trả `404`, không trả `403`

## Out of scope

- Danh sách và bộ lọc (HIST-002)
- Gửi lại (HIST-003)
- Xuất báo cáo

## Preconditions

- PRE-01: thông báo tồn tại trong tổ chức của người gọi

## Dependencies

DLVR-001

## Tham chiếu

- Must-have: M-10 ([MVP.md](../../../MVP.md))
- Dữ liệu: `notifications`, `delivery_attempts` (đọc) — SPECS.md §6
- Contract: `GET /v1/notifications/:id` — SPECS.md §7

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
