# HIST-002 — Danh sách có bộ lọc và tóm tắt một lần gọi

Status: Planned

Đặc tả đầy đủ (Business rules, Public contract, Data impact, Acceptance criteria, Planned files) được
viết khi có lệnh `SELECT HIST-002`; xem [README.md](../README.md).

## Outcome

Quản trị viên tìm ra việc gì đang hỏng, và hệ thống nguồn biết lô vừa gửi đi tới đâu.

## Actor

Quản trị viên, và hệ thống nguồn với thông báo do chính nó tạo.

## Trigger

Gọi endpoint danh sách hoặc endpoint tóm tắt lô.

## In scope

- Lọc theo trạng thái, khoảng thời gian, khoá API, lô
- Phân trang, sắp xếp mới nhất trước
- Tóm tắt lô: số đã gửi, đang chờ, hỏng
- `?status=failed` chính là danh sách lỗi mà cảnh báo trỏ tới

## Out of scope

- Bảng theo dõi
- Xuất CSV
- Thống kê theo thời gian

## Preconditions

- PRE-01: người gọi thuộc tổ chức

## Dependencies

HIST-001

## Tham chiếu

- Must-have: M-11 ([MVP.md](../../../MVP.md))
- Dữ liệu: `notifications`, `notification_batches` (đọc) — SPECS.md §6
- Contract: `GET /v1/notifications`, `GET /v1/batches/:id` — SPECS.md §7

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
