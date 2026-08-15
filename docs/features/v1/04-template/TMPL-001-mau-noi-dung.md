# TMPL-001 — Mẫu nội dung: tạo, xem, sửa, khai báo biến

Status: Planned

Đặc tả đầy đủ (Business rules, Public contract, Data impact, Acceptance criteria, Planned files) được
viết khi có lệnh `SELECT TMPL-001`; xem [README.md](../README.md).

## Outcome

Tổ chức soạn sẵn câu chữ dùng lại được, thay vì mỗi hệ thống nguồn tự viết lại.

## Actor

Quản trị viên.

## Trigger

Quản trị viên tạo hoặc sửa một mẫu.

## In scope

- Tạo, xem, liệt kê, sửa mẫu: khoá, tiêu đề, nội dung, danh sách biến
- Dựng nội dung từ mẫu và dữ liệu, thay `{{bien}}`
- Thiếu biến đã khai báo thì báo lỗi, không gửi chuỗi rỗng

## Out of scope

- Dùng mẫu khi tiếp nhận (INTK-003)
- Nội dung HTML
- Phiên bản hoá mẫu
- Xem trước

## Preconditions

- PRE-01: người gọi là quản trị viên của tổ chức

## Dependencies

AUTH-002

## Tham chiếu

- Must-have: M-06 ([MVP.md](../../../MVP.md))
- Dữ liệu: `templates` — SPECS.md §6
- Contract: `GET/POST /v1/templates`, `GET/PATCH /v1/templates/:key` — SPECS.md §7

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
