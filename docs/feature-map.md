# Feature Map sơ bộ

Mục đích: bóc hành trình MVP thành những khả năng cụ thể mà sản phẩm cần cung cấp — đủ chi tiết để
quyết định kiến trúc, không hơn.

Đầu vào: [MVP.md](MVP.md) (hành trình và danh sách Must-have M-01…M-14), [domain-map.md](domain-map.md)
(các domain và invariant).

Chủ động **chưa** chốt ở đây: toàn bộ endpoint, trường dữ liệu, acceptance criteria, thiết kế màn
hình. Những thứ đó thuộc SPECS.md, viết sau khi có kiến trúc.

## Phân biệt các mức

| Mức | Ví dụ | Quyết định ở đâu |
|-----|-------|------------------|
| Domain | Notification Intake | domain-map.md |
| **Feature** | **Tiếp nhận một yêu cầu gửi thông báo** | **tài liệu này** |
| Contract | `POST /v1/notifications` | SPECS.md |
| Implementation | `NotificationService.accept()` | mã nguồn |

Chỉ mức feature được chốt bên dưới. Không có endpoint nào xuất hiện trong tài liệu này.

## Bản đồ tính năng

Chú thích: **[MVP]** cần cho hành trình đầu-cuối · **[sau]** chủ động làm sau MVP ·
**[không]** loại trừ khỏi phiên bản này.

```
Identity & Access
├── Đăng ký tổ chức kèm quản trị viên đầu tiên          [MVP]  M-01
├── Đăng nhập / làm mới phiên                           [MVP]  M-01
├── Cấp API key cho một hệ thống nguồn                  [MVP]  M-03
├── Liệt kê API key (không bao giờ lộ khoá)             [MVP]  M-03
├── Thu hồi API key                                     [MVP]  M-03
├── Xác thực bên gọi (người hoặc máy)                   [MVP]  M-01/M-03
├── Ép ranh giới tổ chức trên mọi thao tác              [MVP]  M-02
├── Quản lý thêm quản trị viên                          [sau]
└── Ghi vết thay đổi cấu hình                           [sau]

Sender Configuration
├── Cấu hình tài khoản gửi email (máy chủ, tài khoản, địa chỉ gửi)  [MVP]  M-04
├── Lưu bí mật theo kiểu chỉ ghi                        [MVP]  M-04
├── Xem cấu hình mà không thấy bí mật                   [MVP]  M-04
├── Kiểm chứng bằng một thư thử                         [MVP]  M-05
├── Sửa / tắt một tài khoản gửi                         [MVP]  M-04
├── Nhiều tài khoản gửi mỗi tổ chức, có một mặc định    [sau]  S-08
└── Nhà cung cấp dạng API bên cạnh SMTP                 [sau]  S-05

Message Content  (hỗ trợ, không phải cửa kiểm soát — hệ thống nguồn tự cung cấp câu chữ)
├── Tạo mẫu nội dung (khoá, tiêu đề, nội dung văn bản)  [MVP]  M-06
├── Đọc / liệt kê mẫu                                   [MVP]  M-06
├── Sửa mẫu                                             [MVP]  M-06
├── Khai báo và kiểm tra biến của mẫu                   [MVP]  M-06/M-13
├── Dựng nội dung từ mẫu và dữ liệu                     [MVP]  M-06
├── Nội dung HTML                                       [sau]  S-04
├── Phiên bản hoá và xem trước mẫu                      [sau]  S-09
└── Rút một mẫu khỏi sử dụng                            [sau]

Notification Intake
├── Tiếp nhận yêu cầu: kiểm tra, lưu, phản hồi          [MVP]  M-07
├── Nhận câu chữ đi kèm ngay trong yêu cầu              [MVP]  M-07
├── Từ chối yêu cầu sai với lỗi dùng được               [MVP]  M-13
├── Xác định tài khoản gửi (và mẫu, nếu có gọi tên)     [MVP]  M-07
├── Lưu nội dung hoàn chỉnh cùng yêu cầu                [MVP]  M-07/M-10
├── Giới hạn tần suất theo tổ chức và theo khoá         [MVP]  M-14
├── Chống trùng bằng idempotency key                    [sau]  S-01
├── Tiếp nhận theo lô                                   [sau]  S-02
├── Nhiều người nhận trong một yêu cầu                  [sau]  S-03
├── Tệp đính kèm                                        [sau]  S-07
└── Hẹn giờ gửi                                         [không] N-05

Delivery
├── Lấy việc đã tiếp nhận theo cách bất đồng bộ         [MVP]  M-08
├── Giao thông điệp cho tài khoản gửi                   [MVP]  M-08
├── Ghi kết quả của từng lần gửi                        [MVP]  M-08
├── Thử lại lỗi tạm thời với giãn cách tăng dần         [MVP]  M-09
├── Không thử lại khi bị từ chối vĩnh viễn              [MVP]  M-09
├── Từ bỏ sau khi hết số lần thử, có ghi lý do          [MVP]  M-09
├── Gửi lại khi con người yêu cầu                       [MVP]  M-12
├── Nhận phản hồi từ nhà cung cấp (trả về / đã tới)     [sau]  S-06
└── Kênh khác ngoài email                               [không] N-01

History & Audit
├── Tra một thông báo kèm các lần gửi                   [MVP]  M-10
├── Liệt kê thông báo của tổ chức, lọc theo trạng thái  [MVP]  M-11
├── Cung cấp health và các bộ đếm cơ bản                [MVP]  điều kiện hoàn tất
├── Xoá dữ liệu theo thời hạn lưu                       [sau]  S-10
├── Cảnh báo khi tỉ lệ hỏng vượt ngưỡng                 [sau]  C-05
└── Bảng theo dõi                                       [sau]  C-03
```

Mỗi mục Must-have M-01…M-14 xuất hiện đúng một lần ở trên, và không có tính năng MVP nào mà không
Must-have nào yêu cầu.

## Phụ thuộc

```
Identity & Access ──────────────────────────────┐
        │                                       │ (xác thực và giới hạn phạm vi cho tất cả)
        ▼                                       ▼
Sender Configuration            Message Content
        │                                │
        │  (tài khoản nào gửi)           │ (câu chữ và biến, khi có dùng mẫu)
        │                                ▼
        │                        Notification Intake ◀── hệ thống nguồn
        │                                │
        │                                │ (việc đã tiếp nhận)
        └───────────────▶  Delivery ◀────┘
                                │
                                ▼
                        History & Audit ◀── quản trị viên
```

Thứ tự xây dựng suy ra từ sơ đồ: Identity & Access không phụ thuộc ai và phải có trước. Sender
Configuration và Message Content độc lập với nhau, làm song song được. Intake cần Sender
Configuration, và cần Message Content chỉ khi yêu cầu gọi tên một mẫu. Delivery cần Intake và Sender
Configuration. History dựa trên bản ghi do các phần khác tạo ra nên hoàn thiện sau cùng.

Hai chiều phụ thuộc cần nói rõ vì rất dễ làm sai:

- Sender Configuration không được biết là có thông báo tồn tại. Nó được hỏi xin một tài khoản gửi,
  chứ không với tay vào intake hay delivery.
- Message Content không được biết thông điệp rời hệ thống bằng cách nào. Việc dựng nội dung là thuần
  tuý: vào là câu chữ và dữ liệu, ra là văn bản.

## Lát cắt dọc đầu tiên

Hành trình chạy được đầu-cuối chỉ với một phần của Must-have. Làm lát cắt này trước để kiểm chứng
kiến trúc trước khi lấp phần còn lại:

```
Đăng ký tổ chức → đăng nhập → cấu hình tài khoản SMTP
    → cấp API key → tiếp nhận một yêu cầu (kèm tiêu đề, nội dung)
    → gửi một lần → tra kết quả
```

Vẫn thuộc MVP nhưng làm ngay sau lát cắt: thư thử, thử lại và giãn cách, gửi lại thủ công, giới hạn
tần suất, danh sách và bộ lọc lịch sử, mẫu nội dung.

## Giả định đang dùng

| # | Giả định | Bản đồ đổi thế nào nếu sai |
|---|----------|---------------------------|
| F1 | Mỗi tổ chức một tài khoản gửi trong MVP | Intake phải thêm khả năng "chọn tài khoản gửi" |
| F4 | Chỉ con người mới được gửi lại | Delivery phải thêm khả năng gửi lại cho hệ thống nguồn kèm quy tắc phân quyền riêng |

Đã chốt (xem [domain-map.md](domain-map.md)) và đã phản ánh ở trên:

| Quyết định | Ảnh hưởng lên bản đồ này |
|------------|--------------------------|
| Tổ chức là đơn vị sở hữu; mỗi hệ thống nguồn có khoá riêng | Không thêm tầng cô lập; lọc lịch sử và giới hạn tần suất theo từng khoá |
| Hệ thống nguồn tự cung cấp tiêu đề và nội dung | Mẫu nội dung vẫn còn nhưng rời khỏi đường bắt buộc; Intake có thêm "nhận câu chữ đi kèm yêu cầu" |
| Một yêu cầu một kênh; phiên bản đầu chỉ email | Không có tính năng phát tán nhiều kênh; kênh mới sau này là thêm một loại tài khoản gửi |

## Đủ để làm gì

Kiến trúc bây giờ quyết định được: ranh giới giữa tiếp nhận và gửi nằm ở đâu, phần nào phải bền
vững, phần nào chạy ngoài luồng request, và lớp trừu tượng nhà cung cấp phải che giấu những gì. Vẫn
chưa quyết định được contract — đó là việc của SPECS.md, sau khi kiến trúc được duyệt.
