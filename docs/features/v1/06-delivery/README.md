# Delivery

Sở hữu vòng đời gửi, delivery attempt bất biến, retry/recovery và cảnh báo lỗi. PostgreSQL vừa là nguồn
sự thật vừa là hàng đợi bền vững; worker polling trực tiếp, Redis không nằm trên đường gửi cơ bản.

Thứ tự: `DLVR-001 → DLVR-002 → DLVR-003`; DLVR-004 sau DLVR-002 và SEND-002.
