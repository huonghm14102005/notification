# SEND-001 — Quản Trị Máy Chủ Gửi Thư (Sender Management & Testing)

Status: Verified  
Module: `03-sender`  
Dependencies: `AUTH-001`  
Subsumes: `SEND-002`, `SEND-003`

---

## 1. Mô Tả Tổng Quan

Module Sender quản lý các tài khoản máy chủ gửi email (SMTP Server / Dịch vụ Transactional Email như Resend, SendGrid, Gmail) của tổ chức. 

* Mật khẩu/API Key được **mã hóa bảo mật `AES-256-GCM`** và tuyệt đối không bao giờ trả về qua API.
* Hỗ trợ **cơ chế Hoán đổi Sender Mặc định Nguyên tử (Atomic Default Swap)**: Mỗi tổ chức chỉ có duy nhất một máy chủ mặc định tại một thời điểm.
* Hỗ trợ **Kiểm thử gửi thư trực tiếp (SMTP Testing)**: Tự động phân loại lỗi (DNS, kết nối, xác thực) và tự động nhận diện bypass qua **Resend HTTPS REST API (Cổng 443)** khi chạy trên các môi trường Cloud (như Render Free Tier) bị chặn cổng SMTP.

---

## 2. Toàn Bộ Đặc Tả CRUD Chi Tiết

### [CREATE] Thêm máy chủ gửi thư mới
* **Endpoint**: `POST /v1/senders`
* **Quyền**: Bearer Admin JWT
* **Request Body**:
  ```json
  {
    "key": "primary-mailer",
    "host": "smtp.resend.com",
    "port": 587,
    "secure": false,
    "username": "resend",
    "password": "re_WCqhjhu5_...",
    "fromEmail": "onboarding@resend.dev",
    "fromName": "Notification Service"
  }
  ```
* **Validation**:
  - `key`: Bắt buộc, duy nhất trong tenant, chỉ gồm chữ thường, số, dấu gạch ngang (2 - 50 ký tự).
  - `host`: Tên miền hoặc IP máy chủ SMTP hợp lệ.
  - `port`: Cổng TCP (1 - 65535, phổ biến: `465` SSL, `587` STARTTLS).
  - `secure`: `true` (SSL on connect) hoặc `false` (STARTTLS / Plain).
  - `fromEmail`: Địa chỉ email người gửi hợp lệ (RFC 5322).
* **Response (201 Created)**:
  ```json
  {
    "id": "40a80d41-5a70-427e-8a3e-99ea734b1d34",
    "key": "primary-mailer",
    "channel": "email",
    "host": "smtp.resend.com",
    "port": 587,
    "secure": false,
    "username": "resend",
    "fromEmail": "onboarding@resend.dev",
    "fromName": "Notification Service",
    "isDefault": false,
    "status": "active",
    "verifiedAt": null,
    "createdAt": "2026-09-04T10:00:00Z",
    "updatedAt": "2026-09-04T10:00:00Z"
  }
  ```
  *(Tuyệt đối không lộ trường `password` hoặc `passwordEncrypted`)*.

---

### [READ] Xem danh sách và chi tiết Sender

#### 1. Danh sách Sender
* **Endpoint**: `GET /v1/senders?limit=20&cursor=<token>`
* **Response (200 OK)**:
  ```json
  {
    "items": [
      {
        "id": "40a80d41-5a70-427e-8a3e-99ea734b1d34",
        "key": "primary-mailer",
        "host": "smtp.resend.com",
        "port": 587,
        "secure": false,
        "username": "resend",
        "fromEmail": "onboarding@resend.dev",
        "fromName": "Notification Service",
        "isDefault": true,
        "status": "active",
        "verifiedAt": "2026-09-04T10:05:00Z",
        "createdAt": "2026-09-04T10:00:00Z",
        "updatedAt": "2026-09-04T10:05:00Z"
      }
    ],
    "nextCursor": null
  }
  ```

#### 2. Chi tiết một Sender
* **Endpoint**: `GET /v1/senders/{id}`
* **Response (200 OK)**: Trả về chi tiết `SenderItem` tương ứng.

---

### [UPDATE] Cập nhật thông số Sender & Đặt làm mặc định
* **Endpoint**: `PATCH /v1/senders/{id}`
* **Request Body** (cho phép cập nhật một phần):
  ```json
  {
    "fromName": "Notification Center",
    "isDefault": true,
    "password": "new_secret_password_if_changed"
  }
  ```
* **Quy tắc nghiệp vụ**:
  - Nếu `password` bị bỏ trống hoặc null: Giữ nguyên mật khẩu đã mã hóa trước đó trong CSDL.
  - Nếu `isDefault: true`: Hệ thống tự động chuyển `isDefault = false` ở tất cả các sender khác trong cùng tenant một cách nguyên tử (Atomic). Không bao giờ xảy ra tình trạng có 2 sender cùng là mặc định.
  - Sender đang bị `disabled` không được phép sửa đổi hoặc đặt làm mặc định (trả về lỗi `409 Conflict`).
* **Response (200 OK)**: Trả về đối tượng `SenderItem` sau khi cập nhật.

---

### [DELETE] Vô hiệu hóa Sender
* **Endpoint**: `DELETE /v1/senders/{id}`
* **Response (204 No Content)**
* **Hành vi nghiệp vụ**:
  - Thao tác là Soft-delete: Trạng thái chuyển thành `disabled`.
  - Nếu sender này đang là `isDefault`: Tự động hủy cờ mặc định.
  - Không xóa cứng khỏi database để bảo vệ tính toàn vẹn của lịch sử các thông báo đã gửi trong quá khứ.

---

### [ACTION] Gửi thư kiểm tra kết nối (SMTP Test)
* **Endpoint**: `POST /v1/senders/{id}/test`
* **Request Body**:
  ```json
  {
    "recipientEmail": "recipient@example.com"
  }
  ```
* **Cơ chế xử lý & Resilience**:
  1. **Tự động nhận diện Resend**: Nếu `host` chứa `resend.com` hoặc password bắt đầu bằng `re_`, hệ thống tự động gọi qua **Resend HTTPS REST API (Cổng 443)** thay vì SMTP TCP để không bị firewall trên Cloud chặn.
  2. **Rate Limit**: Giới hạn tối đa 5 lần test / phút / tenant để chống cạn kiệt tài nguyên. Lần thứ 6 sẽ nhận `429 Too Many Requests` kèm header `Retry-After`.
* **Response Thành công (200 OK)**:
  ```json
  {
    "sent": true,
    "verifiedAt": "2026-09-04T10:05:00Z",
    "recipientEmail": "recipient@example.com"
  }
  ```
  *(Cột `verifiedAt` của sender sẽ tự động được cập nhật mốc thời gian này)*.
* **Xử lý lỗi**:
  - Sai mật khẩu / API Key: Trả về `502 Bad Gateway` với `reason: "SMTP_AUTHENTICATION"`.
  - Lỗi DNS máy chủ: `502 Bad Gateway` với `reason: "SMTP_DNS"`.
  - Hết thời gian chờ: `504 Gateway Timeout` với mã `SMTP_TEST_TIMEOUT`.
