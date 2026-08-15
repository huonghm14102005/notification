# Intake

Bề mặt máy-gọi: xác thực API key, rate limit, validation, chọn sender, tạo batch/notification trong
transaction và enqueue sau commit. Intake không gửi SMTP.

Thứ tự: `INTK-001 → INTK-004 → INTK-002`; INTK-003 làm sau TMPL-001. Rate limit đi trước mở rộng
500 recipients để bảo vệ đường ghi tải lớn.
