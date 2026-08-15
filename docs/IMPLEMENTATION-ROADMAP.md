# Lộ trình triển khai tuần tự

Mục tiêu của lộ trình là tạo các lát cắt có thể kiểm chứng, không xây toàn bộ hạ tầng trước rồi mới
có hành trình chạy được. Thứ tự thư mục feature biểu thị nhóm năng lực; thứ tự dưới đây mới là thứ
tự triển khai bắt buộc.

## 1. Nguyên tắc

1. Chỉ triển khai một feature đang `Approved`.
2. Mỗi feature hoàn tất theo thứ tự: spec → migration → domain/application → infrastructure →
   transport → test → tài liệu vận hành.
3. Feature sau chỉ bắt đầu khi dependency trực tiếp ở trạng thái `Verified`.
4. Mỗi giai đoạn kết thúc bằng một demo/checkpoint có thể chạy độc lập.
5. Foundation được làm vừa đủ theo nhu cầu; không dựng trước framework dùng cho feature tương lai.

## 2. Các giai đoạn

| Giai đoạn | Feature theo thứ tự | Kết quả kiểm chứng |
|---|---|---|
| 0 — Walking skeleton | OPS-001 (phần bootstrap, health, correlation, test containers) | API và Worker khởi động trong Compose; health kiểm tra PostgreSQL/Redis; log JSON không lộ bí mật |
| 1 — Chủ sở hữu | AUTH-001 → AUTH-002 → AUTH-003 | Tạo tổ chức, đăng nhập và cấp/thu hồi API key; test cô lập tenant chạy được |
| 2 — Đường gửi thật | SEND-001 → SEND-002 → SEND-003 | Lưu bí mật mã hoá, chọn sender mặc định và gửi được email thử |
| 3 — Lát cắt đầu-cuối | INTK-001 → DLVR-001 → HIST-001 | API key tiếp nhận một thông báo, worker gửi, quản trị viên/hệ thống nguồn tra được kết quả |
| 4 — Độ bền | DLVR-002 → DLVR-003 | Lỗi tạm thời được retry, lỗi vĩnh viễn kết thúc rõ ràng, job mất/kẹt được phục hồi |
| 5 — Hoàn thiện intake | INTK-004 → INTK-002 → TMPL-001 → INTK-003 | Có rate limit trước khi mở rộng tải; hỗ trợ 500 người nhận và nội dung theo mẫu |
| 6 — Vận hành nghiệp vụ | HIST-002 → HIST-003 → DLVR-004 | Có danh sách/lô, retry-cancel thủ công và cảnh báo lỗi tổng hợp |
| 7 — Hardening/release | Hoàn thiện OPS-001 xuyên suốt | Load test intake, restore drill, security review, rollback rehearsal và release checklist đạt |

INTK-004 đứng trước INTK-002 có chủ đích: không mở endpoint nhận tối đa 500 người trước khi có lớp
bảo vệ tải. TMPL-001 có thể phát triển song song với giai đoạn 3 nếu có người độc lập, nhưng khi làm
tuần tự thì đặt sau đường gửi cơ bản để giảm thời gian tới demo đầu-cuối.

## 3. Dependency chuẩn

```text
OPS-001 bootstrap
  └─ AUTH-001 → AUTH-002 ┬→ AUTH-003 ───────────────┐
                         ├→ SEND-001 → SEND-002 ────┤
                         └→ TMPL-001 ───────────────┼─────────────┐
                                                   ▼             │
                                               INTK-001          │
                                                   ├→ INTK-004   │
                                                   ├→ INTK-002   │
                                                   ├→ DLVR-001 → DLVR-002 → DLVR-003
                                                   │      │           └→ DLVR-004
                                                   │      └→ HIST-001 → HIST-002 → HIST-003
                                                   └──────────────→ INTK-003
```

Điều chỉnh dependency cần ghi vào cả feature spec và bảng danh mục; không được chỉ sửa sơ đồ.

## 4. Quy trình code của một feature

### Bước 1 — Khoá phạm vi

- Chọn đúng một ID.
- Điền đầy đủ business rules, authorization, contract, data impact, acceptance criteria và planned
  files trong tệp feature.
- Giải quyết toàn bộ open question ảnh hưởng hành vi; chuyển `Review`, sau đó chờ `Approved`.

### Bước 2 — Thiết kế thay đổi

- Nêu migration và khả năng tương thích lùi.
- Nêu command/query, aggregate/invariant và interface hạ tầng cần dùng.
- Ánh xạ từng acceptance criterion tới loại test.
- Security review bắt buộc nếu feature chạm tenant, auth, secret hoặc nội dung.

### Bước 3 — Triển khai từ lõi ra biên

1. Migration và model persistence.
2. Domain rule/value object thuần.
3. Application command/query và interface.
4. Infrastructure adapter/repository.
5. API endpoint hoặc Worker consumer/job.
6. Unit, integration, contract và tenant-isolation tests.

Không gọi SMTP/Redis/EF Core trực tiếp từ endpoint hoặc Domain.

### Bước 4 — Xác minh

- Chạy format, build, architecture tests, unit tests và integration tests.
- Với job: chạy cùng payload hai lần và kiểm tra idempotency.
- Với migration: kiểm tra upgrade trên database sạch và database phiên bản trước.
- Với endpoint: kiểm tra success, validation, authentication, authorization, tenant isolation và
  error contract.
- Ghi bằng chứng vào PR rồi chuyển `Verified`.

### Bước 5 — Phát hành

- Build một image cho cùng phiên bản API/Worker.
- Chạy migration một lần, rollout API và Worker, smoke-test health và hành trình bị ảnh hưởng.
- Theo dõi lỗi, độ dài hàng đợi và latency; rollback theo WORKFLOW nếu vượt ngưỡng.

## 5. Definition of Done theo giai đoạn

Một giai đoạn chỉ hoàn tất khi tất cả feature trong giai đoạn đã `Verified`, demo checkpoint chạy
trên Docker Compose, tài liệu/contract khớp hành vi, không còn open question chặn giai đoạn kế tiếp
và không có migration chưa được kiểm chứng.
