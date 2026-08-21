# INTK-003 — Dựng nội dung từ `templateKey` khi tiếp nhận

Status: Planned

## Outcome

Hệ thống nguồn gửi dữ liệu nghiệp vụ thay vì tự ghép câu chữ, còn thông báo lưu đúng nội dung đã
dùng tại thời điểm tiếp nhận.

## Actor

Hệ thống nguồn, xác thực bằng API key.

## Trigger

Yêu cầu chứa `templateKey` và `templateData` thay cho `subject` và `body` trực tiếp.

## In scope

- Tìm mẫu active theo `tenantId` và `templateKey`.
- Kiểm tra đủ biến khai báo; bỏ qua biến dư.
- Render tiêu đề/nội dung trước transaction ghi notification.
- Lưu snapshot đã render; sửa mẫu không đổi lịch sử.
- Áp dụng cho một hoặc nhiều người; cùng batch dùng cùng snapshot.
- Lỗi máy-đọc được cho mẫu không tồn tại hoặc thiếu biến.

## Out of scope

- HTML, version, preview và biểu thức điều kiện.
- Vừa truyền template vừa truyền nội dung trực tiếp để fallback.
- Render riêng cho từng recipient trong một batch.

## Preconditions

- PRE-01: API key hợp lệ và template cùng tenant.
- PRE-02: template đang active.

## Dependencies

INTK-001, TMPL-001

## Tham chiếu

- Phạm vi sản phẩm: [PRODUCT.md](../../../PRODUCT.md)
- Dữ liệu: `templates` (đọc), `notifications` (snapshot) — SPECS.md §6
- Contract: `POST /v1/notifications` dạng template — SPECS.md §8

## Business rules

Hoàn thiện ở bước `SELECT INTK-003`. Quy tắc cố định: render lỗi không để lại batch, notification
hoặc job; lịch sử luôn đọc snapshot.

## Authorization

API key chỉ dùng template cùng tenant. Không tồn tại và khác tenant cùng trả `TEMPLATE_NOT_FOUND`.

## Public contract

Chốt ở bước `SELECT`; request không được đồng thời có `subject/body` và `templateKey/templateData`.

## Data impact

Không thêm bảng. Đọc `templates`, ghi snapshot vào `notifications` trong flow INTK-001/002.

## Acceptance criteria

Khi `SELECT` phải bao phủ: thành công, thiếu biến, mẫu không tồn tại, tenant isolation, hai chế độ
input và snapshot không đổi sau khi sửa template.

## Planned files

Xác định theo [ARCHITECTURE.md](../../../ARCHITECTURE.md) ở bước `SELECT`.

## Open questions

Không có.
