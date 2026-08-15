# Sender

Sở hữu cấu hình SMTP và bí mật mã hoá. Chỉ Infrastructure được giải mã bí mật tại điểm gửi;
response, log và exception không chứa bí mật.

Thứ tự: `SEND-001 → SEND-002`; SEND-003 hoàn tất trước checkpoint đường gửi thật.
