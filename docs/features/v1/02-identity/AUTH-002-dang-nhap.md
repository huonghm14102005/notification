# AUTH-002 — Đăng nhập, làm mới phiên, đăng xuất

Status: Planned

Đặc tả đầy đủ (Business rules, Public contract, Data impact, Acceptance criteria, Planned files) được
viết khi có lệnh `SELECT AUTH-002`; xem [README.md](../README.md).

## Outcome

Quản trị viên có access token để gọi các endpoint quản trị.

## Actor

Quản trị viên đã có tài khoản.

## Trigger

Gửi email và mật khẩu; hoặc gửi refresh token.

## In scope

- Đăng nhập trả access token và refresh token
- Làm mới access token bằng refresh token
- Đăng xuất thu hồi refresh token
- Giới hạn tần suất đăng nhập theo IP

## Out of scope

- Quên mật khẩu
- Đổi mật khẩu
- Xác thực hai bước
- Đăng nhập bằng Google

## Preconditions

- PRE-01: tài khoản tồn tại và chưa bị khoá

## Dependencies

AUTH-001

## Tham chiếu

- Must-have: M-01 ([MVP.md](../../../MVP.md))
- Dữ liệu: `admins` (đọc), `refresh_tokens` (ghi) — SPECS.md §6
- Contract: `POST /v1/auth/login`, `/v1/auth/refresh`, `/v1/auth/logout` — SPECS.md §7

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
