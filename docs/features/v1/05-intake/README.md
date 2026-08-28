# Intake

Bề mặt máy-gọi: xác thực API key, rate limit, validation, chọn sender và lưu notification vào PostgreSQL.
Phiên bản cơ bản không dùng Redis queue; worker polling PostgreSQL. Batch chỉ xuất hiện khi INTK-002 mở nhiều recipient.

INTK-003 đã hoàn tất. INTK-004 được hoãn trong local nhưng vẫn là cổng bắt buộc trước staging/production và trước khi
mở rộng INTK-002 lên 500 recipient. Trong thời gian hoãn, intake chỉ được chạy trong môi trường phát triển có kiểm soát.
