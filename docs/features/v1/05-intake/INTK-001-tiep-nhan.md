# INTK-001 — Tiếp nhận yêu cầu gửi cho một người nhận

Status: Planned

Đặc tả đầy đủ (Business rules, Public contract, Data impact, Acceptance criteria, Planned files) được
viết khi có lệnh `SELECT INTK-001`; xem [README.md](../README.md).

## Outcome

Hệ thống nguồn gọi một lần là xong việc: dịch vụ nhận trách nhiệm gửi và trả lại mã tra cứu.

## Actor

Hệ thống nguồn (xác thực bằng API key).

## Trigger

Hệ thống nguồn gọi endpoint tiếp nhận kèm người nhận, tiêu đề và nội dung.

## In scope

- Kiểm tra dữ liệu vào bằng FluentValidation, có biên trên độ dài
- Phân giải tài khoản gửi
- Lưu thông báo trạng thái `accepted`, nội dung mã hoá
- Trả `202` kèm mã thông báo sau khi đã commit
- Đẩy việc vào hàng đợi sau khi commit
- Lỗi dữ liệu trả mã lỗi dùng được bằng máy

## Out of scope

- Nhiều người nhận (INTK-002)
- Dùng mẫu (INTK-003)
- Giới hạn tần suất (INTK-004)
- Việc gửi thật (DLVR-001)
- Chống trùng bằng idempotency key

## Preconditions

- PRE-01: API key hợp lệ, chưa thu hồi
- PRE-02: tổ chức có tài khoản gửi khả dụng

## Dependencies

AUTH-003, SEND-002

## Tham chiếu

- Must-have: M-07, M-13 ([MVP.md](../../../MVP.md))
- Dữ liệu: `notifications`, `notification_batches` (ghi); `senders`, `api_keys` (đọc) — SPECS.md §6
- Contract: `POST /v1/notifications` — SPECS.md §7, §8

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
