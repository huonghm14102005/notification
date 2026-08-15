# SEND-001 — Cấu hình tài khoản gửi SMTP

Status: Planned

Đặc tả đầy đủ (Business rules, Public contract, Data impact, Acceptance criteria, Planned files) được
viết khi có lệnh `SELECT SEND-001`; xem [README.md](../README.md).

## Outcome

Tổ chức khai báo được hòm thư dùng để gửi, mật khẩu không bao giờ đọc ngược ra được.

## Actor

Quản trị viên.

## Trigger

Quản trị viên tạo hoặc sửa một tài khoản gửi.

## In scope

- Tạo, sửa, tắt tài khoản gửi: khoá, host, port, secure, username, mật khẩu, địa chỉ và tên người gửi
- Mật khẩu mã hoá AES-256-GCM khi lưu
- Liệt kê và xem chi tiết không kèm bí mật

## Out of scope

- Chọn tài khoản gửi khi tiếp nhận (SEND-002)
- Gửi thư thử (SEND-003)
- Nhà cung cấp dạng API ngoài SMTP
- OAuth2 với Gmail API

## Preconditions

- PRE-01: người gọi là quản trị viên của tổ chức

## Dependencies

AUTH-002

## Tham chiếu

- Must-have: M-04 ([MVP.md](../../../MVP.md))
- Dữ liệu: `senders` — SPECS.md §6
- Contract: `GET/POST /v1/senders`, `PATCH/DELETE /v1/senders/:id` — SPECS.md §7

## Ghi chú cấu hình Gmail

Tài khoản dùng để thử nghiệm: `huong102145@st.vimaru.edu.vn` (Google Workspace của trường).

| Trường | Giá trị |
|-------|--------|
| `host` | `smtp.gmail.com` |
| `port` | `587` |
| `secure` | `false` (nâng cấp bằng STARTTLS) |
| `username` | địa chỉ đầy đủ |
| `password` | App Password 16 ký tự, không phải mật khẩu đăng nhập |

App Password chỉ tạo được khi tài khoản đã bật xác thực hai bước, và quản trị Workspace của
trường có quyền tắt tính năng này. Mật khẩu do người vận hành tự nhập vào hệ thống khi chạy,
không nằm trong mã nguồn hay trong tài liệu.

Hạn mức cần biết: Gmail thường khoảng 500 người nhận/ngày, Workspace khoảng 2.000 người
nhận/ngày. Đủ để thử, không đủ để gửi cho cả trường — khi chạy thật phải dùng SMTP relay của
Workspace hoặc một dịch vụ gửi thư chuyên dụng.

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
