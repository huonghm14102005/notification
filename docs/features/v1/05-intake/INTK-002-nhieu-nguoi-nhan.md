# INTK-002 — Nhiều người nhận trong một yêu cầu (tối đa 500)

Status: Planned

Đặc tả đầy đủ (Business rules, Public contract, Data impact, Acceptance criteria, Planned files) được
viết khi có lệnh `SELECT INTK-002`; xem [README.md](../README.md).

## Outcome

Hệ thống điểm gửi cho cả lớp bằng một lần gọi, mà vẫn theo dõi và gửi lại được từng sinh viên.

## Actor

Hệ thống nguồn.

## Trigger

Yêu cầu tiếp nhận có mảng `recipients` nhiều phần tử.

## In scope

- 1..500 người nhận mỗi yêu cầu; quá thì `TOO_MANY_RECIPIENTS`
- Mỗi người nhận thành một thông báo riêng, trạng thái riêng
- Cùng một `notification_batches` để tra cứu theo lô
- Một người nhận sai định dạng làm hỏng cả yêu cầu, kèm chỉ số phần tử sai
- Ghi `recipient_ref` (mã sinh viên) nếu có, không diễn giải

## Out of scope

- cc/bcc trong cùng một thư
- Nhiều nội dung khác nhau trong một lần gọi
- Tóm tắt lô (HIST-002)

## Preconditions

- PRE-01: như INTK-001

## Dependencies

INTK-001, INTK-004

## Tham chiếu

- Must-have: M-15 ([MVP.md](../../../MVP.md))
- Dữ liệu: `notifications` (nhiều dòng), `notification_batches` — SPECS.md §6
- Contract: `POST /v1/notifications` với mảng `recipients` — SPECS.md §8

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
