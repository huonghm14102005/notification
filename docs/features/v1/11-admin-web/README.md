# Admin Web

Ứng dụng React dành cho admin quản lý notification-server. Frontend chỉ gọi public `/v1` API, không truy cập
PostgreSQL hoặc secret trực tiếp.

Thứ tự: `WEB-001` (khung, auth, history và thao tác vận hành) trước các dashboard incident chuyên sâu.
