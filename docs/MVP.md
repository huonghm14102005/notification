# MVP

Phạm vi tài liệu: phiên bản nhỏ nhất của **notify-api** nhưng mang lại giá trị trọn vẹn.
Bối cảnh sản phẩm và ranh giới: [PRODUCT.md](PRODUCT.md).

MVP không phải một danh sách tính năng. Nó là một hành trình mà một actor đi được từ đầu đến cuối.
Mọi thứ bên dưới tồn tại để hành trình đó chạy được, hoặc bị loại trừ một cách rõ ràng.

## Hành trình chính

Hai actor phối hợp trong cùng một hành trình; tách riêng nửa nào cũng vô nghĩa.

**Hành trình thiết lập — quản trị viên (người)**

```
Đăng ký tổ chức và tài khoản quản trị
  → đăng nhập
  → cấu hình tài khoản gửi email (máy chủ SMTP, thông tin đăng nhập, địa chỉ gửi)
  → gửi thư thử và thấy nó tới nơi
  → tạo API key cho một hệ thống nguồn
  → (tuỳ chọn) soạn sẵn mẫu nội dung có chỗ điền
```

**Hành trình gửi — ứng dụng gửi (máy)**

```
Gọi dịch vụ kèm API key, danh sách người nhận, tiêu đề và nội dung
  → nhận phản hồi ngay lập tức (đã tiếp nhận, kèm mã thông điệp)
  → dịch vụ gửi thư qua tài khoản đã cấu hình
  → người nhận nhận được email
  → quản trị viên tra lại và thấy thông điệp đã gửi
  → nếu hỏng, quản trị viên thấy lý do và gửi lại
```

Hành trình hoàn tất khi một hệ thống nguồn không viết một dòng mã gửi mail nào vẫn khiến một email
thật tới một hộp thư thật, và sau đó một con người xác nhận được điều đó đã xảy ra.

## Must have

Thiếu bất kỳ mục nào thì hành trình trên không chạy.

| # | Khả năng | Vì sao hành trình cần |
|---|----------|----------------------|
| M-01 | Đăng ký tổ chức, đăng nhập quản trị (phiên làm việc) | Không có chủ sở hữu thì không cấu hình được gì |
| M-02 | Cô lập theo tổ chức trên mọi thao tác đọc và ghi | Cấu hình, thông điệp và lịch sử thuộc về một tổ chức |
| M-03 | Cấp và thu hồi API key, gắn với tổ chức và hệ thống nguồn | Ứng dụng gửi phải xác thực được như một máy |
| M-04 | Cấu hình tài khoản gửi email: máy chủ, cổng, thông tin đăng nhập, địa chỉ gửi; mật khẩu mã hoá, không đọc ngược | Không có tài khoản gửi thì không gửi được |
| M-05 | Gửi thư thử từ cấu hình đã lưu | Quản trị viên phải xác nhận cấu hình đúng trước khi nối hệ thống vào |
| M-06 | Nội dung: nhận tiêu đề và nội dung do ứng dụng gửi cung cấp; mẫu nội dung có biến `{{...}}` là tuỳ chọn | Đây là cách các hệ thống nguồn thực sự gửi |
| M-07 | Tiếp nhận: kiểm tra, lưu, phản hồi ngay | Nửa hành trình của ứng dụng gửi |
| M-08 | Tiến trình gửi bất đồng bộ: gửi qua tài khoản đã cấu hình, ghi lại kết quả | Nhận mà không gửi thì không có giá trị |
| M-09 | Thử lại có giãn cách khi lỗi tạm thời; lỗi vĩnh viễn ghi nhận và không thử lại | Sự cố không được làm mất thông điệp (M1/M5 trong PRODUCT.md) |
| M-10 | Tra cứu trạng thái theo mã thông điệp, gồm các lần thử, thời điểm và lý do hỏng | Trả lời "đã tới chưa" mà không cần log |
| M-11 | Danh sách lịch sử của tổ chức, lọc theo trạng thái và theo hệ thống nguồn | Bước chẩn đoán của quản trị viên |
| M-12 | Gửi lại thủ công một thông điệp đã hỏng | Khép vòng sau sự cố |
| M-13 | Kiểm tra dữ liệu vào với thông báo lỗi rõ ràng (thiếu nội dung, sai địa chỉ, mẫu không tồn tại) | Hệ thống nguồn phải tích hợp được mà không phải đoán |
| M-14 | Giới hạn tần suất theo tổ chức và theo API key | Một hệ thống nguồn không được làm sập dịch vụ dùng chung |
| M-15 | Nhiều người nhận trong một lần gọi (tối đa 500), mỗi người là một thông điệp riêng có trạng thái riêng | Hệ thống điểm gửi cho cả lớp, không gọi từng sinh viên |
| M-16 | Nhiều tài khoản gửi trong một tổ chức, hệ thống nguồn chỉ định hoặc dùng mặc định | Các phòng ban dùng hòm thư khác nhau |
| M-17 | Email cảnh báo tổng hợp gửi cho quản trị viên khi có thông điệp hỏng vĩnh viễn | Hỏng mà không ai biết thì lịch sử vô nghĩa |

## Should have

Giá trị thật, nhưng chủ động phát hành sau MVP.

| # | Khả năng | Vì sao có thể chờ |
|---|----------|-------------------|
| S-01 | Khoá chống trùng (idempotency key) khi tiếp nhận | MVP chấp nhận at-least-once (giả định A5) |
| S-02 | Tiếp nhận theo lô nhiều **nội dung khác nhau** trong một lần gọi | Một nội dung gửi cho nhiều người (M-15) đã đủ cho các tích hợp đầu tiên |
| S-03 | Gửi cc/bcc trong cùng một thư | Mỗi người nhận một thư riêng là đủ, và tra cứu rõ hơn |
| S-04 | Nội dung HTML bên cạnh văn bản thuần | Văn bản thuần đủ để chứng minh việc gửi; định dạng làm sau |
| S-05 | Nhà cung cấp email dạng API (SES/SendGrid) bên cạnh SMTP | SMTP chạy được ở mọi nơi; adapter thứ hai để kiểm chứng lớp trừu tượng |
| S-06 | Nhận phản hồi từ nhà cung cấp (đã tới / bị trả về) | Phải có nhà cung cấp dạng API trước |
| S-07 | Tệp đính kèm | Các thông báo đầu tiên chưa cần |
| S-09 | Phiên bản hoá và xem trước mẫu nội dung | Sửa trực tiếp vẫn ổn khi lưu lượng còn thấp |
| S-10 | Tác vụ dọn dữ liệu theo thời hạn lưu (10 năm) | Không có gì hết hạn trong những năm đầu vận hành |

## Could have

Cải thiện trải nghiệm, không quyết định MVP.

| # | Khả năng |
|---|----------|
| C-01 | Giao diện web cho cấu hình và lịch sử (MVP chỉ có API) |
| C-02 | Gửi lại hàng loạt theo khoảng thời gian |
| C-03 | Bảng theo dõi và biểu đồ tỉ lệ gửi thành công |
| C-04 | Xuất/nhập mẫu nội dung |
| C-05 | Cảnh báo khi tỉ lệ hỏng vượt ngưỡng |
| C-06 | Thư viện client cho các hệ thống nguồn |
| C-07 | Đặc tả OpenAPI sinh ra từ dịch vụ đang chạy |

## Not now

Chủ động loại khỏi phiên bản này, kế thừa phần loại trừ ở [PRODUCT.md](PRODUCT.md). Không được đưa
lại với lý do "sau này có thể cần".

| # | Loại trừ |
|---|----------|
| N-01 | Kênh khác ngoài email (SMS, push, chat, webhook chung) |
| N-02 | Trang tuỳ chọn nhận tin, huỷ đăng ký, giờ im lặng, gộp tin |
| N-03 | Gửi quảng bá, phân nhóm đối tượng, thống kê mở/nhấp |
| N-04 | Hộp thư thông báo trong ứng dụng cho người dùng cuối |
| N-05 | Hẹn giờ hoặc gửi lặp lại |
| N-06 | Dịch vụ tự quyết *khi nào* cần thông báo (nghe sự kiện, quy tắc nghiệp vụ) |
| N-07 | Danh bạ người nhận do dịch vụ sở hữu |
| N-08 | Mọi kết nối tới cơ sở dữ liệu, tenant hay tài khoản của dịch vụ CDN |
| N-09 | Triển khai đa vùng, kiến trúc sẵn sàng cao, tự co giãn |
| N-10 | Giao diện dành cho người nhận thư |

## Điều kiện hoàn tất MVP

MVP xong khi tất cả các điều sau đúng.

**Hành trình**

- [ ] Quản trị viên đi hết hành trình thiết lập chỉ bằng API, không cần can thiệp cơ sở dữ liệu hay
      máy chủ.
- [ ] Một hệ thống nguồn chỉ có API key và tài liệu khiến được một email thật tới hộp thư thật.
- [ ] Sau khi chủ động gây sự cố tài khoản gửi, các thông điệp nhận trong lúc sự cố vẫn được gửi khi
      khôi phục, không cần thao tác tay.
- [ ] Thông điệp hỏng vĩnh viễn hiển thị kèm lý do và quản trị viên gửi lại được.

**Phân quyền và cô lập**

- [ ] Mọi endpoint đều yêu cầu xác thực; không endpoint nào trả dữ liệu tổ chức khi chưa xác thực.
- [ ] Đã kiểm chứng không thể truy cập chéo tổ chức với mọi tài nguyên (cấu hình gửi, mẫu nội dung,
      thông điệp, lịch sử, API key) — mỗi loại có một kiểm thử dùng danh tính của tổ chức khác.
- [ ] Mật khẩu tài khoản gửi mã hoá khi lưu và không xuất hiện ở bất kỳ endpoint, log hay thông báo
      lỗi nào.
- [ ] API key bị thu hồi ngừng hoạt động ngay lập tức.

**Dữ liệu**

- [ ] Quy trình sao lưu và phục hồi cơ sở dữ liệu đã có tài liệu và đã thực hiện thành công ít nhất
      một lần trên bản dữ liệu giống thật.
- [ ] Mọi thay đổi lược đồ đều là migration, mỗi migration đã chạy tiến và lùi một lần.
- [ ] Phục hồi từ bản sao lưu không làm gửi lại các thông điệp đã gửi.

**Vận hành**

- [ ] Endpoint health báo cả dịch vụ lẫn kết nối cơ sở dữ liệu và hàng đợi.
- [ ] Log có cấu trúc, kèm mã tương quan theo yêu cầu/thông điệp; lỗi 5xx không lộ thông tin nội bộ
      ra ngoài.
- [ ] Thông điệp hỏng và sự cố tiến trình gửi hiện ra ở nơi có người theo dõi, không chỉ nằm trong
      log.
- [ ] Có tối thiểu các chỉ số: số tiếp nhận, số đã gửi, số hỏng, độ dài hàng đợi.

**Triển khai và quay lui**

- [ ] Dịch vụ API và tiến trình gửi triển khai độc lập với dịch vụ CDN.
- [ ] Quay lui được về phiên bản trước, và mỗi migration có đường quay lui đã ghi tài liệu.
- [ ] Cấu hình hoàn toàn bằng biến môi trường; không bí mật nào nằm trong image.
- [ ] Khởi động lại tiến trình gửi giữa chừng không làm mất thông điệp đã tiếp nhận.

**Tài liệu**

- [ ] Có hướng dẫn tích hợp cho hệ thống nguồn: xác thực, gửi, tra trạng thái, hiểu lỗi.
- [ ] Mọi mã lỗi trong tài liệu đều được cài đặt trả về, và mọi mã lỗi trả về đều có trong tài liệu.
