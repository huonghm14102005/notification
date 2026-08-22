# HIST-002 — Danh sách notification có bộ lọc

Status: Review
Selected: 2026-08-22
Dependencies: HIST-001, CHAN-001, DEVICE-001

## Đọc nhanh

Feature này thêm endpoint chỉ đọc:

```http
GET /v1/notifications?status=failed&channel=email&limit=50
```

- Admin xem notification trong tenant.
- API key chỉ xem notification do chính key đó tạo.
- Danh sách không giải mã subject/body và không trả ciphertext.
- Phân trang bằng cursor ổn định, mới nhất trước.
- Chưa có batch summary vì `INTK-002` đang bị hoãn cùng `INTK-004`.

## Phạm vi

- Danh sách notification và delivery summary.
- Lọc theo aggregate status, channel, khoảng thời gian.
- Admin lọc thêm theo source device hoặc API key.
- Cursor pagination theo `createdAt DESC, id DESC`.
- Giữ nguyên endpoint chi tiết `GET /v1/notifications/{id}` của HIST-001.

Không làm batch summary, total count, tìm theo email/ref, full-text search, CSV, dashboard, retry hoặc cancel.

## Business rules

1. Mọi query bắt đầu bằng `tenant_id` lấy từ authenticated principal.
2. Admin JWT thấy mọi notification trong tenant; API key bắt buộc thêm `api_key_id` của chính principal vào query.
3. Sắp xếp cố định `created_at DESC, id DESC`; client không chọn sort.
4. `limit` mặc định 50, nhỏ nhất 1, lớn nhất 100.
5. Cursor là token opaque chứa version, `createdAt` và `id`; cursor sai trả `400 INVALID_CURSOR`.
6. Trang kế tiếp lấy bản ghi nhỏ hơn cặp `(createdAt,id)` trong cursor, không lặp khi nhiều notification cùng thời điểm.
7. `status` chỉ nhận `accepted`, `processing`, `delivered`, `partially_delivered`, `failed`, `cancelled`.
8. `channel` hiện nhận `email`; có thể bổ sung channel đã triển khai sau này mà không đổi hình response.
9. `from` inclusive, `to` exclusive, ISO-8601 có timezone; `from < to` và khoảng thời gian tối đa 31 ngày.
10. Không truyền thời gian thì không tự thêm khoảng mặc định; cursor/limit vẫn chặn kích thước response.
11. `sourceDeviceId` và `apiKeyId` chỉ dành cho admin. API key truyền hai filter này nhận `400 FILTER_NOT_ALLOWED`.
12. `sourceDeviceId` lọc qua device gắn với API key đã tạo notification; không nhận device khác tenant.
13. `channel` lọc notification có ít nhất một delivery thuộc channel đó; response vẫn trả mọi delivery của notification.
14. Mỗi item trả delivery summary nhưng không trả attempts; muốn xem attempts dùng HIST-001.
15. Không giải mã hoặc load subject/text/html snapshot. Không trả callback URL, secret, ciphertext hay provider message ID.
16. Query read-only, `AsNoTracking`, không giữ database lock trong lúc serialize response.
17. Không trả total count; `nextCursor=null` nghĩa là hết dữ liệu.

## Authorization

- Policy: `AdminOrApiKey`.
- Admin: tenant isolation, được dùng tất cả filter.
- API key: tenant + đúng `api_key_id`, không được mở rộng scope sang key/device khác.
- Thiếu/sai credential trả `401`; filter hợp lệ nhưng không có dữ liệu trả `200` với `items=[]`.
- ID filter thuộc tenant khác cho kết quả rỗng như ID không tồn tại.

## Public contract

```http
GET /v1/notifications?status=failed&channel=email&from=2026-08-01T00:00:00Z&to=2026-09-01T00:00:00Z&sourceDeviceId=<uuid>&apiKeyId=<uuid>&limit=50&cursor=<opaque>
Authorization: Bearer <admin-jwt-or-api-key>
```

Thành công:

```json
{
  "items": [{
    "id": "0198...",
    "sourceDeviceId": "0198...",
    "apiKeyId": "0198...",
    "producerName": "drl-server",
    "status": "partially_delivered",
    "createdAt": "2026-08-22T02:00:00Z",
    "updatedAt": "2026-08-22T02:00:05Z",
    "completedAt": "2026-08-22T02:00:05Z",
    "deliveries": [{
      "id": "0198...",
      "channel": "email",
      "target": "student@example.test",
      "targetRef": "SV001",
      "status": "delivered",
      "attemptCount": 1,
      "errorCode": null
    }]
  }],
  "nextCursor": "eyJ2IjoxLC4uLn0"
}
```

Với API key, response bỏ `apiKeyId`, `sourceDeviceId` và `targetRef`; metadata còn lại giữ nguyên. Nội dung
template/plaintext không xuất hiện trong cả hai view.

| Trường hợp | HTTP | Code |
|---|---:|---|
| Query/UUID/time/status/channel/limit sai | 400 | `VALIDATION_FAILED` |
| Cursor sai version/hình dạng | 400 | `INVALID_CURSOR` |
| API key dùng filter admin | 400 | `FILTER_NOT_ALLOWED` |
| Thiếu/sai auth | 401 | `UNAUTHORIZED` |
| Database lỗi | 503 | `SERVICE_UNAVAILABLE` |

## Data impact

Không thêm bảng và không ghi dữ liệu. Dùng `notifications`, `deliveries`, `api_keys`, `devices` hiện có.

Chỉ thêm migration nếu `EXPLAIN` trong integration test chứng minh index hiện tại không phục vụ query tenant + cursor.
Nếu cần, index mới phải bắt đầu bằng `tenant_id` và migration phải qua down/up.

## Internal contracts

```text
ListNotificationsQuery(tenantId, caller, filters, cursor, limit)
  → NotificationListPage(items, nextCursor)

INotificationRepository.ListAsync(query)
  → limit + 1 projection; không load entity graph, không load ciphertext
```

Endpoint chỉ parse/validate contract và map lỗi. Repository thực hiện tenant/caller filter ngay trong SQL.

## Acceptance criteria

1. Admin nhận trang mới nhất trước, tối đa `limit`, và `nextCursor` đúng khi còn dữ liệu.
2. Hai notification cùng `createdAt` phân trang không lặp/không mất nhờ tie-breaker `id`.
3. API key chỉ thấy notification của chính key; key khác cùng device và key/device/tenant khác không thấy dữ liệu.
4. Admin lọc đúng theo status, channel, source device, API key và khoảng thời gian.
5. API key truyền filter admin nhận `FILTER_NOT_ALLOWED` và repository không chạy.
6. Status/channel/UUID/time/limit sai trả lỗi ổn định; cursor hỏng trả `INVALID_CURSOR`.
7. Filter hợp lệ không có kết quả trả `200`, mảng rỗng, `nextCursor=null`.
8. Channel filter chọn notification phù hợp nhưng item trả đủ mọi delivery của notification đó.
9. Admin thấy targetRef; API key không thấy targetRef/sourceDeviceId/apiKeyId.
10. Response/log không chứa subject, text/html, ciphertext, callback secret hoặc provider message ID.
11. List không load delivery attempts và không thay đổi notification/delivery.
12. Database failure trả `503` không lộ SQL/connection detail.
13. Docker integration xác minh query, tenant isolation, cursor và migration down/up hiện tại tiếp tục pass.

## Planned files

```text
src/Notification.Api/Contracts/Notifications/NotificationListContracts.cs
src/Notification.Api/Endpoints/Notifications/NotificationEndpoints.cs
src/Notification.Application/Notifications/ListNotificationsHandler.cs
src/Notification.Application/Notifications/NotificationListModels.cs
src/Notification.Application/Notifications/INotificationRepository.cs
src/Notification.Infrastructure/Persistence/NotificationRepository.cs
tests/Notification.Application.Tests/Notifications/ListNotificationsHandlerTests.cs
tests/Notification.IntegrationTests/Notifications/*
scripts/test-integration.ps1
docs/SPECS.md
```

## Điểm cần xác nhận khi duyệt

- Local chỉ triển khai danh sách notification; batch summary chờ `INTK-002`.
- List không trả nội dung, attempts hoặc total count.
- API key chỉ xem dữ liệu của đúng key, không gộp mọi key trên cùng device.
- Khoảng thời gian nếu truyền bị giới hạn tối đa 31 ngày; nếu không truyền thì không áp khoảng mặc định.
