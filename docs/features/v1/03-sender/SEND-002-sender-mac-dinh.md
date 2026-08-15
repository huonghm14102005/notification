# SEND-002 — Tài khoản gửi mặc định và chọn theo senderKey

Status: Planned

Đặc tả đầy đủ (Business rules, Public contract, Data impact, Acceptance criteria, Planned files) được
viết khi có lệnh `SELECT SEND-002`; xem [README.md](../README.md).

## Outcome

Một tổ chức dùng được nhiều hòm thư; hệ thống nguồn chọn hòm thư hoặc để dịch vụ tự chọn.

## Actor

Quản trị viên (đặt mặc định) và hệ thống nguồn (chọn khi gửi).

## Trigger

Quản trị viên đánh dấu một tài khoản là mặc định, hoặc yêu cầu tiếp nhận có `senderKey`.

## In scope

- Mỗi tổ chức có tối đa một tài khoản mặc định
- Đặt mặc định mới thì tự gỡ mặc định cũ
- Phân giải `senderKey` → tài khoản gửi; bỏ trống thì lấy mặc định
- Không có tài khoản khả dụng thì lỗi rõ ràng

## Out of scope

- Chọn tài khoản theo luật (theo loại thông báo, theo người nhận)
- Cân bằng tải giữa nhiều tài khoản

## Preconditions

- PRE-01: tổ chức có ít nhất một tài khoản gửi `active`

## Dependencies

SEND-001

## Tham chiếu

- Must-have: M-16 ([MVP.md](../../../MVP.md))
- Dữ liệu: `senders` (đọc, cập nhật cờ mặc định) — SPECS.md §6
- Contract: `PATCH /v1/senders/:id` với `isDefault`; trường `senderKey` của intake — SPECS.md §7, §8

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
