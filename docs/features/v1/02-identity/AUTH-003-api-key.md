# AUTH-003 — Cấp, liệt kê và thu hồi API key

Status: Planned

Đặc tả đầy đủ (Business rules, Public contract, Data impact, Acceptance criteria, Planned files) được
viết khi có lệnh `SELECT AUTH-003`; xem [README.md](../README.md).

## Outcome

Mỗi hệ thống nguồn có một khoá riêng để gọi dịch vụ, thu hồi được mà không ảnh hưởng hệ thống khác.

## Actor

Quản trị viên.

## Trigger

Quản trị viên cấp khoá cho một hệ thống nguồn, hoặc thu hồi một khoá.

## In scope

- Cấp khoá: sinh khoá thô, lưu tiền tố và bản băm
- Khoá thô chỉ hiện đúng một lần lúc cấp
- Liệt kê khoá kèm tiền tố, trạng thái, lần dùng cuối
- Thu hồi khoá, hiệu lực ngay lập tức
- Xác thực bên gọi bằng khoá: tra theo tiền tố rồi so băm

## Out of scope

- Phân quyền chi tiết theo từng khoá
- Hạn dùng tự động
- Xoay khoá tự động

## Preconditions

- PRE-01: người gọi là quản trị viên của tổ chức

## Dependencies

AUTH-002

## Tham chiếu

- Must-have: M-03 ([MVP.md](../../../MVP.md))
- Dữ liệu: `api_keys` — SPECS.md §6
- Contract: `GET/POST /v1/api-keys`, `DELETE /v1/api-keys/:id` — SPECS.md §7

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
