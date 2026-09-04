# TMPL-001 — Quản Trị Mẫu Thông Báo & Phiên Bản Đa Định Dạng (Template Management)

Status: Verified  
Module: `04-template`  
Dependencies: `AUTH-001`, `DEVICE-001`  
Subsumes: `TMPL-002`

---

## 1. Mô Tả Tổng Quan

Module Template cung cấp khả năng soạn thảo, quản lý nội dung email/tin nhắn động với:
* **Hỗ trợ Đa định dạng**: Plain-text và HTML Body (tự động HTML escaping biến nội suy `{{var}}` để phòng chống tấn công XSS).
* **Phân cấp Phạm vi (Scope Hierarchy)**: 
  - `tenant`: Mẫu áp dụng chung cho toàn tổ chức.
  - `source`: Mẫu chuyên biệt chỉ dành riêng cho một thiết bị nguồn (`sourceDeviceId`), có quyền ghi đè mẫu tenant nếu trùng mã `templateCode`.
* **Phân loại Đối tượng (Audience)**: `user` (người dùng cuối) hoặc `system` (cảnh báo nội bộ hệ thống).
* **Vòng đời Phiên bản Bất biến (Immutable Versioning)**:
  - Một gia đình mẫu (`template family`) bắt đầu từ Version 1 với trạng thái `draft`.
  - Khi được **Xuất bản (`publish`)**, phiên bản đó chuyển sang `active` và **nội dung bị khóa bất biến vĩnh viễn**.
  - Mọi thay đổi nội dung tiếp theo bắt buộc phải tạo **Phiên bản mới (`/versions`)** để sinh ra Version 2 (draft), giúp không bao giờ làm gián đoạn các luồng gửi thư đang chạy.

---

## 2. Toàn Bộ Đặc Tả CRUD & Versioning Chi Tiết

### [CREATE] Tạo Mẫu Mới (Bản nháp v1)
* **Endpoint**: `POST /v1/templates`
* **Quyền**: Bearer Admin JWT
* **Request Body**:
  ```json
  {
    "templateCode": "order-success",
    "scope": "source",
    "sourceDeviceId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "audience": "user",
    "subject": "Xác nhận đơn hàng #{{orderId}}",
    "textBody": "Xin chào {{name}}, đơn hàng {{orderId}} của bạn đã thành công.",
    "htmlBody": "<p>Xin chào <strong>{{name}}</strong>, đơn hàng <strong>#{{orderId}}</strong> đã thành công.</p>",
    "variables": ["name", "orderId"]
  }
  ```
* **Validation**:
  - `templateCode`: 2 - 50 ký tự, tự động chuyển chữ thường chuẩn hóa.
  - `scope`: `tenant` (không cần `sourceDeviceId`) hoặc `source` (bắt buộc có `sourceDeviceId`).
  - `variables`: Danh sách tên biến không dấu, không trùng lặp.
* **Response (201 Created)**:
  ```json
  {
    "id": "7b8a9c0d-1e2f-3a4b-5c6d-7e8f9a0b1c2d",
    "templateCode": "order-success",
    "version": 1,
    "scope": "source",
    "sourceDeviceId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "audience": "user",
    "subject": "Xác nhận đơn hàng #{{orderId}}",
    "textBody": "Xin chào {{name}}, đơn hàng {{orderId}} của bạn đã thành công.",
    "htmlBody": "<p>Xin chào <strong>{{name}}</strong>, đơn hàng <strong>#{{orderId}}</strong> đã thành công.</p>",
    "variables": ["name", "orderId"],
    "status": "draft",
    "createdAt": "2026-09-04T10:00:00Z",
    "updatedAt": "2026-09-04T10:00:00Z"
  }
  ```

---

### [READ] Danh Sách & Chi Tiết Template

#### 1. Danh sách Template có bộ lọc
* **Endpoint**: `GET /v1/templates?status=active&scope=source&audience=user&limit=20&cursor=<token>`
* **Response (200 OK)**:
  ```json
  {
    "items": [
      {
        "id": "7b8a9c0d-1e2f-3a4b-5c6d-7e8f9a0b1c2d",
        "templateCode": "order-success",
        "version": 1,
        "scope": "source",
        "status": "active",
        "subject": "Xác nhận đơn hàng #{{orderId}}",
        "publishedAt": "2026-09-04T10:05:00Z"
      }
    ],
    "nextCursor": null
  }
  ```

#### 2. Chi tiết Template theo ID hoặc Code
* **Endpoint**: `GET /v1/templates/{id}` hoặc `GET /v1/templates/{templateCode}`
* **Response (200 OK)**: Trả về đối tượng template đầy đủ bao gồm cả `textBody`, `htmlBody` và danh sách biến.

---

### [UPDATE] Chỉnh sửa bản nháp (Draft)
* **Endpoint**: `PATCH /v1/templates/{id}`
* **Request Body**:
  ```json
  {
    "subject": "Cập nhật: Đơn hàng #{{orderId}} đã được đóng gói",
    "variables": ["orderId"]
  }
  ```
* **Quy tắc nghiệp vụ**:
  - **Chỉ được phép sửa khi `status: "draft"`**.
  - Nếu template đã `active` hoặc `retired`, API trả về ngay lỗi `409 Conflict` (bảo vệ tính bất biến của phiên bản).

---

### [LIFECYCLE & VERSIONING] Xuất bản & Nâng cấp Phiên bản

#### 1. Xuất bản phiên bản (`Publish`)
* **Endpoint**: `POST /v1/templates/{id}/publish`
* **Hành vi nguyên tử (Atomic Publish)**:
  - Phiên bản được chọn chuyển từ `draft` sang **`active`**.
  - Nếu trong gia đình mẫu này đã có một phiên bản cũ đang `active`, phiên bản cũ đó sẽ tự động chuyển sang **`retired`** một cách nguyên tử.
  - Kể từ thời điểm này, mọi request gửi thông báo dùng `templateCode` này sẽ tự động áp dụng phiên bản mới nhất vừa xuất bản.

#### 2. Tạo phiên bản mới (`Versions`)
* **Endpoint**: `POST /v1/templates/{id}/versions`
* **Hành vi**:
  - Hệ thống tự động sao chép toàn bộ tiêu đề, body, biến từ phiên bản hiện tại sang một bản ghi mới với `version = version_hiện_tại + 1` và `status = "draft"`.
  - Mỗi gia đình mẫu tại một thời điểm chỉ cho phép tồn tại **tối đa 1 bản nháp (draft)** để tránh xung đột nhánh chỉnh sửa.

#### 3. Ngừng sử dụng (`Retire`)
* Khi chuyển trạng thái sang `retired`, mẫu sẽ không còn được nhận diện cho các notification mới tạo, nhưng các thông báo cũ trong quá khứ đã render bằng snapshot của mẫu này vẫn giữ nguyên vẹn 100% nội dung để đối soát.
