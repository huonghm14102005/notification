# Delivery

Sở hữu vòng đời gửi, delivery attempt bất biến, retry/recovery và cảnh báo lỗi. PostgreSQL là nguồn
sự thật; Redis chỉ giữ lịch/job có thể dựng lại.

Thứ tự: `DLVR-001 → DLVR-002 → DLVR-003`; DLVR-004 sau DLVR-002 và SEND-002.
