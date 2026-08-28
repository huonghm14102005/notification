# INTK-004 — Giới hạn tần suất theo khoá và theo tổ chức

Status: Planned
Local decision: Deferred
Production gate: Required

## Quyết định hiện tại

Không triển khai INTK-004 trong giai đoạn chạy local có kiểm soát. Local chỉ dùng để phát triển và integration test,
không được mở endpoint intake trực tiếp ra Internet hoặc dùng cho tải thật.

Trước staging/production phải `SELECT INTK-004`, chốt fail-open/fail-closed khi Redis lỗi, triển khai đầy đủ và chạy
load/abuse test. Không được đánh dấu hệ thống production-ready nếu feature này chưa `Verified`.

Đặc tả đầy đủ (Business rules, Public contract, Data impact, Acceptance criteria, Planned files) được
viết khi có lệnh `SELECT INTK-004`; xem [README.md](../README.md).

## Outcome

Một hệ thống nguồn lỗi hoặc một khoá bị lộ không làm sập dịch vụ dùng chung.

## Actor

Hệ thống nguồn (bị giới hạn) và người đăng nhập (giới hạn đăng nhập).

## Trigger

Mỗi yêu cầu ghi đi qua bộ đếm trước khi làm việc.

## In scope

- Đếm theo khoá: số yêu cầu/phút và số người nhận/giờ
- Đếm theo tổ chức: số người nhận/giờ
- Đếm theo IP cho đăng nhập
- Vượt thì `429` kèm `retryAfter`
- Bộ đếm nằm trong Redis, hết hạn tự động

## Out of scope

- Hạn mức riêng theo từng khoá
- Mua thêm hạn mức
- Xếp hàng thay vì từ chối

## Preconditions

- PRE-01: Redis khả dụng; Redis hỏng thì quyết định fail-open hay fail-closed phải được chốt

## Dependencies

INTK-001

## Tham chiếu

- Phạm vi sản phẩm: [PRODUCT.md](../../../PRODUCT.md)
- Dữ liệu: Không có bảng mới; bộ đếm trong Redis — SPECS.md §11
- Contract: Mã lỗi `RATE_LIMITED` trên các endpoint ghi — SPECS.md §11, §12

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

- Redis hỏng thì tiếp nhận vẫn chạy (fail-open) hay từ chối (fail-closed)?
