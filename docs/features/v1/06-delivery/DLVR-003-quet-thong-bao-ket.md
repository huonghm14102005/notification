# DLVR-003 — Quét và cứu thông báo kẹt

Status: Planned

Đặc tả đầy đủ (Business rules, Public contract, Data impact, Acceptance criteria, Planned files) được
viết khi có lệnh `SELECT DLVR-003`; xem [README.md](../README.md).

## Outcome

Mất việc trong hàng đợi hoặc worker chết giữa chừng không làm thông báo nằm im mãi mãi.

## Actor

Tiến trình worker (định kỳ).

## Trigger

Hết chu kỳ quét.

## In scope

- Định kỳ tìm thông báo `accepted` quá hạn `next_attempt_at` mà không có việc trong hàng đợi
- Tìm thông báo kẹt ở `sending` quá ngưỡng và đưa về hàng đợi
- Không tạo bản ghi trùng, không vượt số lần thử tối đa

## Out of scope

- Giao diện theo dõi
- Cảnh báo khi số thông báo kẹt tăng

## Preconditions

- PRE-01: Postgres là nguồn sự thật, Redis chỉ giữ lịch

## Dependencies

DLVR-001, DLVR-002

## Tham chiếu

- Must-have: M-08 ([MVP.md](../../../MVP.md))
- Dữ liệu: `notifications` (đọc, đẩy lại hàng đợi) — SPECS.md §9
- Contract: Không có endpoint mới

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
