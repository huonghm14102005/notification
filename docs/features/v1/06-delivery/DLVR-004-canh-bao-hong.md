# DLVR-004 — Email cảnh báo tổng hợp cho quản trị viên

Status: Planned

Đặc tả đầy đủ (Business rules, Public contract, Data impact, Acceptance criteria, Planned files) được
viết khi có lệnh `SELECT DLVR-004`; xem [README.md](../README.md).

## Outcome

Thông báo hỏng vĩnh viễn được con người biết tới ngay, không phải chờ ai đó mở danh sách.

## Actor

Tiến trình worker (định kỳ).

## Trigger

Hết cửa sổ gộp và trong cửa sổ có thông báo hỏng.

## In scope

- Gộp các thông báo hỏng theo cửa sổ 15 phút cho từng tổ chức
- Một thư liệt kê số lượng, lý do phổ biến và cách tra cứu
- Gửi qua tài khoản gửi mặc định
- Cửa sổ không có lỗi thì không gửi thư
- Thư cảnh báo hỏng thì chỉ ghi log `error`, không tự cảnh báo về chính nó

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
