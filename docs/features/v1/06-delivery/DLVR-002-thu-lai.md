# DLVR-002 — Thử lại có giãn cách, phân loại lỗi, từ bỏ

Status: Planned

Đặc tả đầy đủ (Business rules, Public contract, Data impact, Acceptance criteria, Planned files) được
viết khi có lệnh `SELECT DLVR-002`; xem [README.md](../README.md).

## Outcome

Sự cố tạm thời của máy chủ thư không làm mất thông báo, và lỗi vĩnh viễn không bị thử lại vô ích.

## Actor

Tiến trình worker.

## Trigger

Một lần gửi trả về lỗi.

## In scope

- Phân loại lỗi SMTP thành `transient_failure` hoặc `permanent_failure`
- Lỗi tạm thời: hẹn lại theo 1 phút → 5 phút → 25 phút, tối đa 4 lần thử
- Lỗi vĩnh viễn: `failed` ngay, không thử lại
- Hết số lần: `failed`, ghi `failure_reason` đọc được
- Việc phân loại nằm trong adapter, không rò chi tiết SMTP ra ngoài

## Out of scope

- Gửi lại thủ công (HIST-003)
- Cảnh báo (DLVR-004)
- Hạn mức thử lại riêng theo tổ chức

## Preconditions

- PRE-01: thông báo chưa ở trạng thái kết thúc

## Dependencies

DLVR-001

## Tham chiếu

- Must-have: M-09 ([MVP.md](../../../MVP.md))
- Dữ liệu: `notifications.attempt_count`, `next_attempt_at`, `failure_reason` — SPECS.md §6, §9
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
