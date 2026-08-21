# CHAN-001 — Mô hình delivery đa kênh

**Status:** Planned  
**Dependencies:** DEVICE-001, DLVR-002

## Outcome

Một notification chọn một hoặc nhiều kênh và sinh delivery độc lập cho mỗi kênh. Giai đoạn này chỉ
`email` được thực thi; kênh chưa hỗ trợ bị từ chối đồng bộ.

## In scope và rules

- Tạo bảng `deliveries`; chuyển trạng thái/retry từ notification xuống delivery.
- Intake nhận `channels[]`, recipients theo kênh và trả danh sách delivery.
- Worker claim từng delivery email; lịch sử tổng hợp trạng thái notification.
- Migration dữ liệu email cũ thành một delivery email.
- Request có ít nhất một kênh, không trùng; không tạo notification nếu có kênh chưa bật.
- Mỗi delivery retry độc lập; kết quả hỗn hợp là `partially_delivered`.

Contract chi tiết, state transition và rollback được hoàn thiện khi `SELECT CHAN-001`.
