# CHAN-002 — Kênh gửi thông báo Telegram và Discord

Status: Verified
Selected: 2026-08-27
Approved: 2026-08-27
Verified: 2026-08-27
Dependencies: CHAN-001, AUTH-003, DLVR-002

## 1. Kết quả đạt được

Hệ thống mở rộng khả năng gửi thông báo tức thời qua hai nền tảng chat phổ biến:
1. **Telegram (Telegram Bot API)**: Gửi tin nhắn định dạng HTML tới User hoặc Channel qua `chat_id`.
2. **Discord (Discord Webhooks)**: Gửi tin nhắn định dạng Markdown và Rich Embed tới các kênh Discord.

Mỗi kênh hoạt động như một Delivery Adapter độc lập trong kiến trúc đa kênh của `notification-server`, hỗ trợ cơ chế retry tự động khi gặp lỗi tạm thời (Rate Limit / Network) và phân loại lỗi vĩnh viễn chính xác.

---

## 2. Đặc tả giao thức và Adapter

### A. Telegram Channel Adapter (`telegram`)
* **Endpoint gọi ngoài**: `POST https://api.telegram.org/bot<bot_token>/sendMessage`
* **Content-Type**: `application/json`
* **Payload JSON**:
  ```json
  {
    "chat_id": "123456789",
    "text": "<b>[Tiêu đề thông báo]</b>\n\nNội dung thông báo",
    "parse_mode": "HTML"
  }
  ```
* **Cách xác định Bot Token & Chat ID**:
  - Ưu tiên: Lấy `bot_token` từ cấu hình Sender mã hóa của tenant.
  - Hỗ trợ cú pháp Target trực tiếp: `<bot_token>:<chat_id>` hoặc `@channel_username`.
* **Phân loại lỗi**:
  - `200 OK`: Thành công, lưu `message_id` vào `delivery_attempts.provider_message_id`.
  - `429 Too Many Requests` hoặc `5xx`: Lỗi tạm thời (`TELEGRAM_RATE_LIMITED`, `TELEGRAM_SERVER_ERROR`), tự động lập lịch retry theo chu kỳ backoff (1m, 5m, 25m).
  - `400 Bad Request` / `401 Unauthorized` / `404 Not Found`: Lỗi vĩnh viễn (`TELEGRAM_UNAUTHORIZED`, `TELEGRAM_NOT_FOUND`), đánh dấu delivery `failed`.

---

### B. Discord Channel Adapter (`discord`)
* **Endpoint gọi ngoài**: `POST https://discord.com/api/webhooks/<webhook_id>/<webhook_token>`
* **Content-Type**: `application/json`
* **Payload JSON**:
  ```json
  {
    "content": "**[Tiêu đề thông báo]**\n\nNội dung thông báo",
    "embeds": [
      {
        "title": "Tiêu đề thông báo",
        "description": "Nội dung thông báo",
        "color": 5793266
      }
    ]
  }
  ```
* **Cách xác định Webhook URL**:
  - Lấy trực tiếp từ trường `target` (URL hợp lệ `https://discord.com/api/webhooks/...` hoặc `https://discordapp.com/api/webhooks/...`).
  - Hoặc lấy từ cấu hình Sender (`Sender.PasswordEncrypted` hoặc `Sender.Host`).
* **Phân loại lỗi**:
  - `200 OK` / `204 No Content`: Thành công.
  - `429 Too Many Requests` hoặc `5xx`: Lỗi tạm thời (`DISCORD_RATE_LIMITED`, `DISCORD_SERVER_ERROR`), worker retry.
  - `400 Bad Request` / `404 Not Found`: Lỗi vĩnh viễn (`DISCORD_WEBHOOK_NOT_FOUND`), không thử lại.

---

## 3. Public Contract API

### Intake Multi-Channel (`POST /v1/notifications`)

```http
POST /v1/notifications
Authorization: Bearer <device-api-key>
Content-Type: application/json
```

**Ví dụ gửi Telegram**:
```json
{
  "channels": [
    {
      "type": "telegram",
      "targets": [{ "address": "123456789", "ref": "user-chat-01" }]
    }
  ],
  "content": {
    "mode": "plaintext",
    "subject": "Cảnh báo bảo mật",
    "body": "Phát hiện đăng nhập lạ từ IP 192.168.1.1."
  }
}
```

**Ví dụ gửi Discord**:
```json
{
  "channels": [
    {
      "type": "discord",
      "targets": [{ "address": "https://discord.com/api/webhooks/123456/abcdef" }]
    }
  ],
  "content": {
    "mode": "template",
    "templateCode": "server-alert",
    "data": { "server": "DB-Primary", "cpu": "95%" }
  }
}
```

**Response `202 Accepted`**:
```json
{
  "id": "8fa1b439-d3e2-4913-9fcf-b6ad90c9103c",
  "status": "accepted",
  "deliveries": [
    {
      "id": "1e9f4561-2244-4822-b5e7-a9a3b9059f12",
      "channel": "telegram",
      "target": "123456789",
      "targetRef": "user-chat-01",
      "status": "pending"
    }
  ]
}
```

---

## 4. Quản trị và Quan sát

* **Tra cứu & Lọc**: `GET /v1/notifications?channel=telegram` hoặc `GET /v1/notifications?channel=discord`.
* **Giao diện Web Admin**: Bảng lịch sử và chi tiết hiển thị trực quan biểu tượng kênh (`✈️ telegram`, `🎮 discord`, `✉️ email`).
* **Bảo mật**: Không ghi Bot Token thô, Webhook URL nhạy cảm hoặc nội dung notification chưa giải mã vào Log JSON.
