# DLVR-004 — Sự cố tổng hợp thay cho log lỗi liên tục

Status: Planned

Đặc tả đầy đủ (Business rules, Public contract, Data impact, Acceptance criteria, Planned files) được
viết khi có lệnh `SELECT DLVR-004`; xem [README.md](../README.md).

## Outcome

Exception/lỗi lặp lại được gom thành một incident bền vững và một cảnh báo tổng hợp, thay vì ghi/gửi cùng một lỗi liên
tục. Log vẫn giữ một bản ghi đầu, các lần sau tăng counter; không nuốt exception và không gửi exception thô cho user.

## Actor

Tiến trình worker (định kỳ).

## Trigger

Hết cửa sổ gộp và trong cửa sổ có thông báo hỏng.

## In scope

- Gộp các thông báo hỏng theo cửa sổ 15 phút cho từng tổ chức
- Fingerprint theo tenant, component và error code; cùng fingerprint trong cửa sổ chỉ tăng count
- Lưu firstSeen/lastSeen/count và sample message đã làm sạch
- Một thư liệt kê số lượng, lý do phổ biến và cách tra cứu
- Gửi qua tài khoản gửi mặc định
- Cửa sổ không có lỗi thì không gửi thư
- Thư cảnh báo hỏng thì chỉ ghi log `error`, không tự cảnh báo về chính nó
- Exception ngoài dự kiến chuyển thành error code an toàn; stack trace chỉ ở telemetry nội bộ, không vào callback/API

## Out of scope

- Webhook báo ngược về hệ thống nguồn
- Cảnh báo theo ngưỡng tỉ lệ hỏng
- Cảnh báo qua Slack hay Telegram

## Preconditions

- PRE-01: tổ chức có tài khoản gửi mặc định và có ít nhất một quản trị viên

## Dependencies

DLVR-002, SEND-002

## Tham chiếu

- Phạm vi sản phẩm: [PRODUCT.md](../../../PRODUCT.md)
- Dữ liệu: `failure_alerts` — SPECS.md §6, §10
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

- Gửi cảnh báo cho tất cả quản trị viên của tổ chức, hay chỉ một địa chỉ khai báo riêng?
