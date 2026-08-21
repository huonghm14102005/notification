# TMPL-002 — Template theo hệ thống gửi và định dạng

Status: Planned
Dependencies: TMPL-001, DEVICE-001, CHAN-001

## Đọc nhanh

Mỗi hệ thống nguồn đăng ký template riêng, dùng mã template ổn định (ví dụ `templateCode=score-result-v2`) rồi gửi dữ
liệu biến. Server render snapshot trước khi tạo delivery.

## Phạm vi dự kiến

- Template thuộc tenant và một source device; owner tenant có thể tạo template dùng chung nếu được chọn rõ.
- Định danh bằng `templateCode`/version, không dùng số thứ tự database dễ thay đổi.
- Audience: `user` hoặc `system`; channel/format phải tương thích delivery.
- Format email: `plain_text`, `html`, hoặc cả hai phần MIME alternative.
- Request gửi `templateCode` và `data`; không gửi template body tùy ý cùng lúc.
- Escape biến mặc định trong HTML để chống injection; chỉ field được khai báo trusted mới cho raw HTML.
- Notification lưu snapshot đã render; sửa template không đổi notification cũ.
- Template cross-device/cross-tenant bị che thành not found.

## Ngoài phạm vi

- Rich editor/UI, attachment, loop/expression tùy ý và thực thi code trong template.

