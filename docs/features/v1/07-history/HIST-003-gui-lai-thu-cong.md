# HIST-003 — Gửi lại thủ công và huỷ

Status: Planned

Đặc tả đầy đủ (Business rules, Public contract, Data impact, Acceptance criteria, Planned files) được
viết khi có lệnh `SELECT HIST-003`; xem [README.md](../README.md).

## Outcome

Sau khi sửa được nguyên nhân, quản trị viên khép vòng mà không cần nhờ hệ thống nguồn gọi lại.

## Actor

Quản trị viên.

## Trigger

Quản trị viên bấm gửi lại một thông báo hỏng, hoặc huỷ một thông báo chưa gửi.

## In scope

- Gửi lại chỉ áp dụng cho thông báo `failed`; trạng thái khác trả `INVALID_STATE`
- Gửi lại tạo lần gửi mới, giữ nguyên lịch sử cũ
- Huỷ chỉ áp dụng khi còn `accepted`
- Mọi thao tác ghi vết ai làm và khi nào

## Out of scope

- Gửi lại hàng loạt cả lô
- Hệ thống nguồn tự gọi gửi lại
- Tự động gửi lại theo lịch

## Preconditions

- PRE-01: thông báo thuộc tổ chức của người gọi và ở đúng trạng thái

## Dependencies

HIST-001, DLVR-001

## Tham chiếu

- Phạm vi sản phẩm: [PRODUCT.md](../../../PRODUCT.md)
- Dữ liệu: `notifications` (cập nhật trạng thái), `delivery_attempts` (thêm) — SPECS.md §6
- Contract: `POST /v1/notifications/:id/retry`, `POST /v1/notifications/:id/cancel` — SPECS.md §7

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
