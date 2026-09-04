# HIST-001 — Lịch Sử Thông Báo, Tra Cứu & Thao Tác Vận Hành (Notification History & Operations)

Status: Verified  
Module: `07-history`  
Dependencies: `AUTH-001`, `INTK-001`, `DLVR-001`  
Subsumes: `HIST-002`, `HIST-003`

---

## 1. Mô Tả Tổng Quan

Module Lịch sử & Vận hành chịu trách nhiệm theo dõi toàn bộ vòng đời của một thông báo từ khi được tiếp nhận (`accepted`), xử lý ngầm (`processing`), cho đến khi hoàn thành (`delivered`), thất bại (`failed`), hoặc bị hủy (`cancelled`).

### Các nguyên tắc vận hành:
* **Bảo toàn Dữ liệu & Tính Bất biến**: Nội dung thông báo đã lưu trữ là bất biến. Không bao giờ cập nhật ghi đè nội dung gốc.
* **Phân quyền & Quyền riêng tư (Privacy Scopes)**:
  - **Quản trị viên (Admin)**: Xem được toàn bộ nội dung rendered (subject, text, html), địa chỉ người nhận, và toàn bộ lịch sử các lần thử gửi (**Delivery Attempts**).
  - **Hệ thống Nguồn (Machine API Key)**: Chỉ tra cứu được các thông báo do chính API Key đó tạo ra; dữ liệu nhạy cảm của các hệ thống khác được bảo vệ tuyệt đối.
* **Thao tác Vận hành Thủ công (Manual Operations)**:
  - **Hủy bỏ (Cancel)**: Áp dụng cho các thông báo đang ở hàng chờ (`accepted`).
  - **Gửi lại thủ công (Manual Retry)**: Áp dụng cho các thông báo gặp lỗi vĩnh viễn (`failed`). Thao tác này tạo ra một notification mới độc lập và ghi vết kiểm toán vào bảng `notification_manual_actions`.

---

## 2. Toàn Bộ Đặc Tả CRUD & Vận Hành Chi Tiết

### [CREATE / INTAKE] Tiếp Nhận Thông Báo
* **Endpoint**: `POST /v1/notifications`
* **Quyền**: Bearer Machine API Key (`notify_...`) hoặc Bearer Admin JWT
* **Request Body (Chế độ Template hoặc Trực tiếp)**:
  ```json
  {
    "senderKey": "primary-mailer",
    "channels": [
      {
        "type": "email",
        "targets": [{ "address": "customer@example.com", "ref": "order-101" }]
      }
    ],
    "content": {
      "mode": "plaintext",
      "subject": "Thông báo đơn hàng",
      "body": "Đơn hàng của bạn đã được tiếp nhận."
    }
  }
  ```
* **Response (202 Accepted)**:
  ```json
  {
    "id": "5f6e7d8c-9b0a-1e2f-3a4b-5c6d7e8f9a0b",
    "status": "accepted",
    "deliveries": [
      {
        "id": "1a2b3c4d-5e6f-7a8b-9c0d-1e2f3a4b5c6d",
        "channel": "email",
        "target": "customer@example.com",
        "status": "pending"
      }
    ],
    "createdAt": "2026-09-04T10:00:00Z"
  }
  ```

---

### [READ] Danh Sách & Chi Tiết Thông Báo

#### 1. Danh sách thông báo có bộ lọc & phân trang
* **Endpoint**: `GET /v1/notifications?channel=email&status=delivered&limit=20&cursor=<token>`
* **Bộ lọc hỗ trợ**:
  - `channel`: `email`, `telegram`, `discord`, `push`.
  - `status`: `accepted`, `processing`, `delivered`, `partially_delivered`, `failed`, `cancelled`.
  - `sourceDeviceId`: Lọc theo thiết bị nguồn (chỉ dành cho Admin).
  - `apiKeyId`: Lọc theo khóa API đã gửi (chỉ dành cho Admin).
* **Phân trang bằng con trỏ (Cursor Pagination)**:
  - Đảm bảo hiệu năng ổn định kể cả khi bảng `notifications` đạt hàng triệu bản ghi.
  - Con trỏ mã hóa Base64URL bao gồm `(createdAt, id)`.
* **Response (200 OK)**:
  ```json
  {
    "items": [
      {
        "id": "5f6e7d8c-9b0a-1e2f-3a4b-5c6d7e8f9a0b",
        "status": "delivered",
        "channel": "email",
        "recipient": "customer@example.com",
        "createdAt": "2026-09-04T10:00:00Z",
        "sentAt": "2026-09-04T10:00:02Z"
      }
    ],
    "nextCursor": "ZXlKaGJHY2lPaUpTVX..."
  }
  ```

#### 2. Chi tiết thông báo & Lịch sử lần gửi (Attempts)
* **Endpoint**: `GET /v1/notifications/{id}`
* **Response Admin (200 OK)**:
  ```json
  {
    "id": "5f6e7d8c-9b0a-1e2f-3a4b-5c6d7e8f9a0b",
    "status": "delivered",
    "producerName": "Backend Order Service",
    "senderKey": "primary-mailer",
    "recipientEmail": "customer@example.com",
    "recipientRef": "order-101",
    "subject": "Thông báo đơn hàng",
    "body": "Đơn hàng của bạn đã được tiếp nhận.",
    "createdAt": "2026-09-04T10:00:00Z",
    "sentAt": "2026-09-04T10:00:02Z",
    "deliveryAttempts": [
      {
        "attemptNo": 1,
        "result": "success",
        "errorCode": null,
        "errorMessage": null,
        "startedAt": "2026-09-04T10:00:01Z",
        "finishedAt": "2026-09-04T10:00:02Z"
      }
    ]
  }
  ```

---

### [ACTION / OPERATIONS] Hủy & Gửi Lại Thủ Công

#### 1. Hủy thông báo đang chờ (`Cancel`)
* **Endpoint**: `POST /v1/notifications/{id}/cancel`
* **Quyền**: Bearer Admin JWT
* **Hành vi**:
  - Chuyển trạng thái notification và các delivery con từ `accepted` sang **`cancelled`**.
  - Worker khi quét delivery sẽ tự động bỏ qua các bản ghi `cancelled`.
  - **Idempotent**: Gọi hủy nhiều lần trên cùng 1 notification luôn trả về `204 No Content`.
  - Nếu thông báo đã gửi (`delivered`) hoặc đang gửi (`processing`): Trả về `409 Conflict`.

#### 2. Thử lại thủ công thông báo thất bại (`Manual Retry`)
* **Endpoint**: `POST /v1/notifications/{id}/retry`
* **Quyền**: Bearer Admin JWT
* **Hành vi**:
  - Áp dụng cho các thông báo có trạng thái `failed` hoặc `partially_delivered`.
  - Hệ thống tạo ra một **bản ghi Notification mới** sao chép đầy đủ nội dung, mẫu và người nhận của thông báo cũ.
  - Ghi nhận lịch sử kiểm toán vào bảng `notification_manual_actions` với `action_type = "retry"`, lưu rõ `admin_id` người thực hiện.
  - **Idempotent**: Nếu bấm Retry nhiều lần liên tiếp, hệ thống chỉ sinh ra 1 notification mới duy nhất (trả về mã `200 OK` với ID đã tạo thay vì sinh lặp vô hạn).
* **Response (201 Created / 200 OK)**:
  ```json
  {
    "id": "9a8b7c6d-5e4f-3a2b-1c0d-9e8f7a6b5c4d",
    "status": "accepted",
    "sourceNotificationId": "5f6e7d8c-9b0a-1e2f-3a4b-5c6d7e8f9a0b"
  }
  ```
