# AUTH-001 — Định Danh, Quản Trị Tài Khoản & Phân Quyền Tổ Chức (Identity & Access Management)

Status: Verified  
Module: `02-identity`  
Dependencies: `OPS-001`  
Subsumes: `AUTH-002`, `AUTH-003`, `AUTH-004`

---

## 1. Mô Tả Tổng Quan

Module Identity đảm bảo an toàn truy cập, xác thực và cô lập dữ liệu đa người thuê (**Multi-tenant Data Isolation**) cho toàn bộ hệ thống:

```text
┌─────────────────────────────────────────────────────────────┐
│                     TENANT (Tổ chức)                         │
│                                                             │
│   ┌─────────────────────┐         ┌─────────────────────┐   │
│   │   User: OWNER       │         │    User: MEMBER     │   │
│   │ (Quản trị tối cao)  │         │  (Nhân viên tác vụ) │   │
│   └──────────┬──────────┘         └──────────┬──────────┘   │
│              │                               │              │
│       Quản lý Member                   Quản lý Device       │
│       Quản lý Sender                   & API Key của mình   │
│       Quản lý Mọi Device                                    │
└──────────────┼───────────────────────────────┼──────────────┘
               ▼                               ▼
       PostgreSQL Row-level isolation by `tenant_id` (Bắt buộc)
```

* **Cô lập Tenant (Tenant Isolation)**: Mọi bảng dữ liệu nghiệp vụ đều chứa cột `tenant_id`. API tự động trích xuất `tenant_id` từ claims của Access Token đã xác thực, tuyệt đối không tin ID truyền từ client.
* **Xác thực JWT & Token Rotation**: Refresh token chỉ được dùng một lần (Single-use rotation) và bị thu hồi ngay khi cấp cặp token mới nhằm chống tấn công replay.
* **Phân quyền (RBAC)**:
  - `owner`: Quản trị viên cao nhất của tổ chức, có quyền quản lý thành viên, cấu hình máy chủ gửi thư (Sender), mẫu thông báo và toàn bộ thiết bị.
  - `member`: Thành viên thông thường, chỉ quản lý các thiết bị nguồn và API key do chính mình tạo ra.

---

## 2. Toàn Bộ Đặc Tả CRUD & Phiên Xác Thực Chi Tiết

### 2.1. Đăng Ký Tổ Chức (Tenant Registration)
* **Endpoint**: `POST /v1/tenants/register`
* **Quyền**: Public (Mở tự do)
* **Request Body**:
  ```json
  {
    "tenantName": "Acme Corporation",
    "tenantSlug": "acme-corp",
    "adminEmail": "owner@acme.com",
    "adminPassword": "Password123@"
  }
  ```
* **Validation**:
  - `tenantSlug`: Duy nhất toàn hệ thống, chỉ gồm chữ thường, số, dấu gạch ngang (2 - 50 ký tự).
  - `adminEmail`: Email hợp lệ, duy nhất trong tenant.
  - `adminPassword`: Tối thiểu 8 ký tự.
* **Response (201 Created)**:
  ```json
  {
    "tenantId": "1a2b3c4d-5e6f-7a8b-9c0d-1e2f3a4b5c6d",
    "tenantName": "Acme Corporation",
    "tenantSlug": "acme-corp",
    "adminId": "9f8e7d6c-5b4a-3e2f-1a0b-9c8d7e6f5a4b",
    "adminEmail": "owner@acme.com",
    "role": "owner",
    "createdAt": "2026-09-04T10:00:00Z"
  }
  ```
  *(Tuyệt đối không rò rỉ `passwordHash`)*.

---

### 2.2. Quản Lý Phiên (Login, Refresh, Logout)

#### 1. Đăng nhập (`Login`)
* **Endpoint**: `POST /v1/auth/login`
* **Request Body**:
  ```json
  {
    "email": "owner@acme.com",
    "password": "Password123@"
  }
  ```
* **Response (200 OK)**:
  ```json
  {
    "accessToken": "eyJhbGciOiJIUzI1NiIsIn...",
    "refreshToken": "dGhpcy1pcy1hLXNlY3VyZS1yZWZyZXNoLXRva2Vu...",
    "expiresIn": 3600
  }
  ```

#### 2. Làm mới Token (`Refresh Token Rotation`)
* **Endpoint**: `POST /v1/auth/refresh`
* **Request Body**:
  ```json
  {
    "refreshToken": "dGhpcy1pcy1hLXNlY3VyZS1yZWZyZXNoLXRva2Vu..."
  }
  ```
* **Bảo mật**:
  - Cấp một cặp `accessToken` và `refreshToken` hoàn toàn mới.
  - Refresh token cũ lập tức bị vô hiệu hóa. Nếu phát hiện token cũ được gọi lại lần 2, hệ thống từ chối ngay với mã `401 Unauthorized` (chống lộ token).

#### 3. Đăng xuất (`Logout`)
* **Endpoint**: `POST /v1/auth/logout`
* **Quyền**: Bearer User JWT
* **Request Body**:
  ```json
  {
    "refreshToken": "dGhpcy1pcy1hLXNlY3VyZS1yZWZyZXNoLXRva2Vu..."
  }
  ```
* **Response (204 No Content)**: Thu hồi vĩnh viễn refresh token.

---

### 2.3. CRUD Quản Trị Người Dùng (User Management)

#### [CREATE] Thêm thành viên mới (Member)
* **Endpoint**: `POST /v1/users`
* **Quyền**: Bearer Owner JWT
* **Request Body**:
  ```json
  {
    "email": "staff@acme.com",
    "password": "TemporaryPassword123@",
    "displayName": "Nguyễn Văn B"
  }
  ```
* **Response (201 Created)**:
  ```json
  {
    "id": "2c3d4e5f-6a7b-8c9d-0e1f-2a3b4c5d6e7f",
    "email": "staff@acme.com",
    "displayName": "Nguyễn Văn B",
    "role": "member",
    "status": "active",
    "deviceCount": 0,
    "createdAt": "2026-09-04T10:00:00Z"
  }
  ```

#### [READ] Xem danh sách và hồ sơ cá nhân
* **Danh sách User**: `GET /v1/users?status=active&limit=50` (Chỉ dành cho Owner).
* **Hồ sơ của tôi**: `GET /v1/users/me` (Dành cho mọi user đã đăng nhập để lấy thông tin cá nhân và vai trò).

#### [DELETE / DISABLE] Vô hiệu hóa tài khoản
* **Endpoint**: `POST /v1/users/{id}/disable`
* **Quyền**: Bearer Owner JWT
* **Hành vi nghiệp vụ**:
  - Chuyển trạng thái user sang `disabled`.
  - Toàn bộ JWT và Refresh Token của user này mất hiệu lực ngay lập tức.
  - Toàn bộ các thiết bị (`Device`) và khóa (`API Key`) do user này sở hữu đều lập tức bị vô hiệu hóa không thể gọi API.
