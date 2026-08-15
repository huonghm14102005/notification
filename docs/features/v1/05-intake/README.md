# Intake

Bề mặt máy-gọi: xác thực API key, rate limit, validation, chọn sender và lưu notification vào PostgreSQL.
Phiên bản cơ bản không dùng Redis queue; worker polling PostgreSQL. Batch chỉ xuất hiện khi INTK-002 mở nhiều recipient.

Thứ tự: `INTK-001 → INTK-004 → INTK-002`; INTK-003 làm sau TMPL-001. Rate limit đi trước mở rộng
500 recipients để bảo vệ đường ghi tải lớn.
