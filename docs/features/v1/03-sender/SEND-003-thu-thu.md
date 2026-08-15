# SEND-003 — Gửi thư thử từ một tài khoản gửi

Status: Planned

Đặc tả đầy đủ (Business rules, Public contract, Data impact, Acceptance criteria, Planned files) được
viết khi có lệnh `SELECT SEND-003`; xem [README.md](../README.md).

## Outcome

Quản trị viên biết chắc cấu hình đúng trước khi nối hệ thống nguồn vào.

## Actor

Quản trị viên.

## Trigger

Quản trị viên bấm gửi thư thử cho một tài khoản gửi.

## In scope

- Gửi đồng bộ một thư tới địa chỉ do quản trị viên nhập
- Thành công thì ghi `verified_at`
- Hỏng thì trả lý do đọc được, không lộ mật khẩu

## Out of scope

- Gửi qua hàng đợi
- Thử lại tự động
- Ghi vào lịch sử thông báo

## Preconditions

- PRE-01: tài khoản gửi tồn tại, thuộc tổ chức của người gọi và đang `active`

## Dependencies

SEND-001

## Tham chiếu

- Must-have: M-05 ([MVP.md](../../../MVP.md))
- Dữ liệu: `senders.verified_at` (cập nhật) — SPECS.md §6
- Contract: `POST /v1/senders/:id/test` — SPECS.md §7

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
