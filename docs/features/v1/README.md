# Danh mục feature — v1

Mỗi feature là một lát cắt nhỏ nhất có thể duyệt, triển khai và kiểm chứng độc lập. Feature được
nhóm theo module; thứ tự code chính thức nằm trong [IMPLEMENTATION-ROADMAP.md](../../IMPLEMENTATION-ROADMAP.md).

## Cấu trúc module

| Thư mục | Module | Trách nhiệm | Feature |
|---|---|---|---|
| `01-foundation/` | Foundation | bootstrap, health, log, metrics, config, test infrastructure | OPS-001 |
| `02-identity/` | Identity | tenant, quản trị viên, phiên và API key | AUTH-001..003 |
| `03-sender/` | Sender | SMTP account, bí mật, sender mặc định và gửi thử | SEND-001..003 |
| `04-template/` | Template | mẫu văn bản thuần và render biến | TMPL-001 |
| `05-intake/` | Intake | validation, rate limit, batch và tiếp nhận | INTK-001..004 |
| `06-delivery/` | Delivery | worker, SMTP, retry, recovery và cảnh báo | DLVR-001..004 |
| `07-history/` | History | tra cứu, danh sách, huỷ và gửi lại | HIST-001..003 |

Mỗi thư mục có README mô tả ranh giới và thứ tự nội bộ. Data ownership và dependency code tuân
theo [DOTNET-SOLUTION.md](../../DOTNET-SOLUTION.md), không suy ra chỉ từ vị trí tệp.

## Danh mục

| ID | Feature | Status | Dependencies | Tệp |
|---|---|---|---|---|
| OPS-001 | Health, log và metrics | Verified | None | [spec](01-foundation/OPS-001-van-hanh.md) |
| AUTH-001 | Đăng ký tổ chức/admin đầu tiên | Verified | OPS-001 | [spec](02-identity/AUTH-001-dang-ky-to-chuc.md) |
| AUTH-002 | Đăng nhập, refresh, logout | Verified | AUTH-001 | [spec](02-identity/AUTH-002-dang-nhap.md) |
| AUTH-003 | Quản lý API key | Verified | AUTH-002 | [spec](02-identity/AUTH-003-api-key.md) |
| SEND-001 | Cấu hình SMTP | Verified | AUTH-002 | [spec](03-sender/SEND-001-cau-hinh-sender.md) |
| SEND-002 | Sender mặc định/`senderKey` | Verified | SEND-001 | [spec](03-sender/SEND-002-sender-mac-dinh.md) |
| SEND-003 | Gửi thư thử | Verified | SEND-001 | [spec](03-sender/SEND-003-thu-thu.md) |
| TMPL-001 | CRUD/render mẫu | Verified | AUTH-002 | [spec](04-template/TMPL-001-mau-noi-dung.md) |
| INTK-001 | Tiếp nhận một người nhận | Planned | AUTH-003, SEND-002 | [spec](05-intake/INTK-001-tiep-nhan.md) |
| INTK-002 | Tối đa 500 người nhận | Planned | INTK-001, INTK-004 | [spec](05-intake/INTK-002-nhieu-nguoi-nhan.md) |
| INTK-003 | Tiếp nhận theo mẫu | Planned | INTK-001, TMPL-001 | [spec](05-intake/INTK-003-tiep-nhan-theo-mau.md) |
| INTK-004 | Rate limit | Planned | INTK-001 | [spec](05-intake/INTK-004-gioi-han-tan-suat.md) |
| DLVR-001 | Worker gửi bất đồng bộ | Planned | INTK-001, SEND-001 | [spec](06-delivery/DLVR-001-gui-bat-dong-bo.md) |
| DLVR-002 | Retry/phân loại lỗi | Planned | DLVR-001 | [spec](06-delivery/DLVR-002-thu-lai.md) |
| DLVR-003 | Cứu thông báo kẹt | Planned | DLVR-001, DLVR-002 | [spec](06-delivery/DLVR-003-quet-thong-bao-ket.md) |
| DLVR-004 | Cảnh báo lỗi tổng hợp | Planned | DLVR-002, SEND-002 | [spec](06-delivery/DLVR-004-canh-bao-hong.md) |
| HIST-001 | Tra cứu thông báo/lần gửi | Planned | DLVR-001 | [spec](07-history/HIST-001-tra-cuu-thong-bao.md) |
| HIST-002 | Danh sách/tóm tắt lô | Planned | HIST-001 | [spec](07-history/HIST-002-danh-sach-lich-su.md) |
| HIST-003 | Gửi lại/huỷ thủ công | Planned | HIST-001, DLVR-001 | [spec](07-history/HIST-003-gui-lai-thu-cong.md) |

Cô lập tenant (M-02) là acceptance criterion bắt buộc của mọi feature chạm dữ liệu.

## Luồng tối thiểu đầu-cuối

```text
OPS-001 bootstrap
  → AUTH-001 → AUTH-002 → AUTH-003
  → SEND-001 → SEND-002 → SEND-003
  → INTK-001 → DLVR-001 → HIST-001
```

Sau checkpoint này mới tăng độ bền, tải và tiện ích quản trị theo roadmap.

## Trạng thái và lệnh

Vòng đời: `Planned → Draft → Review → Approved → Implementing → Verified → Released`.

| Lệnh | Nghĩa |
|---|---|
| `SELECT <ID>` | Khoá feature và hoàn thiện spec; chưa viết code |
| `APPROVE <ID>` | Cho phép triển khai đúng spec |
| `CHANGE <ID>: <nội dung>` | Sửa phạm vi, đưa về Review |
| `STATUS <ID>` | Báo trạng thái |
| `VERIFY <ID>` | Chạy kiểm tra và lấy bằng chứng |
| `RELEASE <ID>` | Phát hành feature Verified |
| `STOP` | Dừng ngay |

Chỉ `APPROVE <ID>` cho phép viết mã. Chi tiết xem [WORKFLOW.md](../../WORKFLOW.md).

## Khuôn feature spec

Mỗi spec theo thứ tự: Outcome, Actor, Trigger, In scope, Out of scope, Preconditions,
Dependencies, Tham chiếu, Business rules, Authorization, Public contract, Data impact, Acceptance
criteria, Planned files, Open questions. Spec chỉ được Approved khi không còn câu hỏi ảnh hưởng
contract, bảo mật, dữ liệu hoặc hành vi lỗi.
