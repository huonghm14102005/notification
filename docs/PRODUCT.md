# Product Brief

## Tuyên bố sản phẩm hiện tại

Một user có thể quản lý nhiều thiết bị/hệ thống nguồn. Mỗi thiết bị nguồn có credential riêng để yêu
cầu gửi notification qua một hoặc nhiều kênh. Nội dung có thể là plaintext hoặc template; target phụ
thuộc kênh. Mỗi delivery được gửi một lần đầu và retry tối đa ba lần. Khi notification hoàn tất,
server chủ động trả cả kết quả thành công hoặc thất bại về hệ thống nguồn. Một PostgreSQL lưu user, device, credential, notification và lịch
sử; push provider như Firebase là dịch vụ chuyển tiếp, không phải database thứ hai.

User đăng nhập bằng email đầy đủ và password. Phần trước `@` chỉ là tên hiển thị mặc định, không phải
định danh đăng nhập và không cần duy nhất.

Phiên bản hiện tại thử nghiệm email SMTP/Gmail. Thiết kế đầy đủ nằm tại
[TARGET-DESIGN.md](TARGET-DESIGN.md).

Sản phẩm: **notify-api** — máy chủ thông báo độc lập, làm mới hoàn toàn, không dùng chung mã nguồn
hay dữ liệu với dịch vụ CDN/Media hiện có.

## Vấn đề

Khi có việc xảy ra trong một hệ thống của trường — có điểm mới, thay đổi điểm rèn luyện, sau này là
lỗi hệ thống — người cần biết lại không được báo một cách tin cậy. Mỗi hệ thống muốn báo cho một
người đều phải tự giải quyết cùng một loạt vấn đề: giữ thông tin tài khoản gửi ở đâu, soạn nội dung
thế nào, làm gì khi máy chủ mail chết, và sau đó ai biết được thư đã tới hay chưa.

Hệ quả quan sát được:

- Logic gửi bị viết lại ở từng hệ thống, nên đổi nhà cung cấp mail hay đổi địa chỉ gửi là phải sửa
  mọi hệ thống.
- Việc gửi nằm ngay trong luồng xử lý của người dùng, nên nhà cung cấp chậm hoặc chết sẽ kéo tụt
  chính hệ thống đó.
- Một lần gửi thất bại thường chỉ là một dòng log trong một hệ thống, không ai khác nhìn thấy. Không
  ai trả lời được câu "sinh viên đã nhận được chưa" nếu không đọc log máy chủ.
- Thông tin đăng nhập tài khoản gửi bị sao chép vào mọi hệ thống có gửi mail.

## Đối tượng sử dụng

| Đối tượng | Vai trò | Nhu cầu chính |
|-----------|---------|---------------|
| **Ứng dụng gửi** (máy — đối tượng chính) | Hệ thống nội bộ vừa xử lý xong một sự kiện và cần báo cho một người | Một lời gọi duy nhất, và dịch vụ nhận trách nhiệm về thông điệp đó |
| Quản trị viên (người) | Sở hữu cấu hình gửi của tổ chức | Cấu hình tài khoản gửi và nội dung; xem được cái gì đã gửi, cái gì hỏng |
| Người vận hành nền tảng (người) | Vận hành dịch vụ | Nhìn được lưu lượng và tỉ lệ lỗi; biết khi nhà cung cấp có vấn đề |

Người nhận thông báo chịu ảnh hưởng của sản phẩm nhưng không tương tác với sản phẩm ở phiên bản đầu.

## Giá trị mang lại

Các hệ thống chỉ cần giao việc "báo cho người này chuyện này" bằng một lời gọi rồi thôi. Việc gửi,
thử lại, giữ thông tin đăng nhập, quản lý nội dung và lưu lịch sử trở thành một trách nhiệm dùng
chung thay vì bị nhân bản. Nhờ đó một nhóm phát triển có thể thêm loại thông báo mới mà không phải
nuôi hạ tầng gửi, còn người phụ trách nghiệp vụ đổi được tài khoản gửi hoặc câu chữ mà không cần
triển khai lại mã nguồn.

## Kết quả người dùng đạt được

1. Ứng dụng gửi báo được cho một người mà không nhúng logic gửi hay thông tin đăng nhập nào, và độ
   trễ cũng như độ sẵn sàng của chính nó không bị ảnh hưởng bởi đường gửi mail.
2. Thông điệp chưa gửi được ngay vẫn sẽ được gửi sau, ứng dụng không phải làm gì thêm, và sau đó vẫn
   tra được kết quả.
3. Quản trị viên tự sửa câu chữ hoặc đổi tài khoản gửi, không cần sửa mã nguồn của bất kỳ hệ thống
   nào.
4. Bất kỳ ai có quyền đều trả lời được, cho một thông điệp cụ thể: đã gửi chưa, lúc nào, qua đường
   nào, nếu chưa thì vì sao — không cần đọc log.
5. Gửi lại hàng loạt sau một sự cố là một hành động có chủ đích, không phải việc viết lại lịch sử.

## Chỉ số thành công

| # | Chỉ số | Định nghĩa | Mục tiêu |
|---|--------|-----------|----------|
| M1 | Mất thông điệp đã nhận | Thông điệp dịch vụ đã nhận nhưng không đạt trạng thái kết thúc nào | 0 |
| M2 | Tỉ lệ gửi được | Thông điệp đã nhận và cuối cùng gửi được, không tính địa chỉ sai bị từ chối vĩnh viễn | > 99% |
| M3 | Độ trễ mà ứng dụng gửi thấy | p95 thời gian của lời gọi tiếp nhận | < 100 ms |
| M4 | Thời gian tới lần gửi đầu tiên | p95 từ lúc tiếp nhận đến lần gửi đầu | < 5 s |
| M5 | Phục hồi sau sự cố nhà cung cấp | Tỉ lệ thông điệp xếp hàng trong 30 phút sự cố được gửi trong 15 phút sau khi khôi phục | 100% |
| M6 | Tự chủ nội dung | Tỉ lệ thay đổi câu chữ thực hiện được mà không cần triển khai lại mã nguồn | > 90% |
| M7 | Mức độ áp dụng | Số hệ thống nguồn tích hợp trong một quý sau khi ra mắt | ≥ 3 |
| M8 | Công sức chẩn đoán | Số thao tác trung vị để quản trị viên biết số phận một thông điệp | 1 truy vấn, không cần log |

## Ràng buộc

**Kỹ thuật**

- Dịch vụ độc lập, có kho dữ liệu riêng và cơ chế định danh riêng.
- Chạy trên cùng nền tảng vận hành hiện có (Docker Compose, PostgreSQL, Redis, Nginx) và theo các
  quy ước đã có của nhóm.
- Phiên bản đầu chỉ gửi email; thiết kế không được khiến việc thêm kênh thứ hai thành viết lại.
- Thông tin đăng nhập tài khoản gửi thuộc về tổ chức, mã hoá khi lưu, không bao giờ đọc ngược ra.
- Hệ thống phải chịu được nhà cung cấp chết vài chục phút mà không mất dữ liệu.

**Thời gian và ngân sách**

- Không có ngân sách hạ tầng riêng: tái sử dụng PostgreSQL/Redis/Compose sẵn có.
- Chi phí nhà cung cấp email phải nằm trong mức trường đang trả cho lượng gửi hiện tại.
- Nhóm nhỏ, làm tăng dần: phải có phiên bản dùng được trước khi bàn tới kênh thứ hai.

**Pháp lý và tuân thủ**

- Nội dung thông điệp và địa chỉ người nhận là dữ liệu cá nhân: thời gian lưu phải có giới hạn và
  nội dung phải xoá được khi có yêu cầu.
- Chỉ thông báo giao dịch/nghiệp vụ trong phiên bản đầu. Gửi quảng bá hàng loạt sẽ kéo theo nghĩa vụ
  xin đồng ý và huỷ đăng ký mà dự án chưa nhận.
- Dữ liệu giữa các tổ chức phải cô lập: không tổ chức nào thấy thông điệp, người nhận hay thông tin
  đăng nhập của tổ chức khác.

## Giả định

Chưa được kiểm chứng — mỗi giả định cần xác nhận trước khi dựa vào nó.

| # | Giả định | Cách kiểm chứng |
|---|----------|-----------------|
| A1 | Các nhóm hệ thống nguồn sẽ dùng dịch vụ chung thay vì giữ mã gửi mail của mình | Cam kết từ hai hệ thống đầu tiên trước khi làm |
| A2 | Email đáp ứng phần lớn nhu cầu thông báo trước mắt | Thống kê các loại thông báo mà các hệ thống muốn gửi |
| A3 | Quản trị viên thực sự muốn và sẽ tự sửa nội dung | Phỏng vấn người sẽ giữ vai trò này |
| A4 | Lưu lượng dự kiến chạy thoải mái trên PostgreSQL/Redis dùng chung | Ước lượng cùng các nhóm hệ thống; kiểm tải đường tiếp nhận |
| A5 | Chấp nhận at-least-once — thà trùng một email hiếm gặp còn hơn mất | Xác nhận theo từng loại thông báo với các nhóm |
| A6 | Một tài khoản gửi cho cả tổ chức là đủ; chưa cần địa chỉ gửi riêng theo hệ thống | Hỏi quản trị viên |
| A7 | Người nhận do ứng dụng gửi cung cấp; dịch vụ không cần danh bạ riêng | Rà lại các tình huống sử dụng |
| A8 | Chưa cần hẹn giờ gửi trong phiên bản đầu | Rà lại các tình huống sử dụng |

## Rủi ro

| # | Rủi ro | Tác động | Giảm thiểu |
|---|--------|----------|-----------|
| R1 | Các hệ thống vẫn tự gửi, dịch vụ bị bỏ qua | Sản phẩm không tạo ra giá trị | Đưa hệ thống đầu tiên vào ngay trong phiên bản đầu; làm cho việc tích hợp rẻ hơn tự làm |
| R2 | Khả năng vào hộp thư kém hơn cách cũ (bị coi là spam) | Người dùng không nhận được thư, mất niềm tin | Chủ động xác thực tên miền gửi (SPF/DKIM/DMARC); theo dõi tỉ lệ bị trả về ngay từ đầu |
| R3 | Dịch vụ tập trung trở thành điểm chết chung cho mọi hệ thống | Sự cố diện rộng | Thiết kế nhận-rồi-xếp-hàng để ứng dụng gửi không bao giờ bị chặn; triển khai độc lập; định rõ cách suy giảm |
| R4 | Lộ hoặc lạm dụng thông tin đăng nhập tài khoản gửi | Sự cố an toàn, bị mạo danh gửi thư | Mã hoá khi lưu, chỉ ghi không đọc, cô lập theo tổ chức, ghi vết mọi thay đổi cấu hình |
| R5 | Trở thành cổng gửi mở: gửi nội dung bất kỳ tới bất kỳ ai | Bị lạm dụng, hại uy tín tên miền | Bắt buộc xác thực ứng dụng gửi, giới hạn tần suất theo từng khoá, quy trách nhiệm từng thông điệp về khoá tạo ra nó |
| R6 | Lưu nội dung và địa chỉ người nhận tạo ra trách nhiệm về dữ liệu cá nhân | Rủi ro tuân thủ | Giới hạn thời gian lưu, xoá theo yêu cầu, lưu tối thiểu |
| R7 | Phạm vi trôi thành nền tảng gửi quảng bá | Không bao giờ xong | Giữ nghiêm phần loại trừ bên dưới; phiên bản đầu chỉ thông báo nghiệp vụ |
| R8 | "Làm mới hoàn toàn" khiến phải làm lại phần định danh đã có nơi khác | Tốn công, vận hành thiếu nhất quán | Đây là quyết định có chủ đích, ghi lại ở đây; chỉ xem lại nếu vận hành hai mô hình định danh trở nên nặng nề |

## Phạm vi sản phẩm chưa giải quyết

Sản phẩm chủ động **không** làm những việc sau. Có thể xem lại về sau, nhưng không quyết định nào của
phiên bản đầu được viện dẫn chúng.

1. **Quản lý tuỳ chọn nhận tin của người nhận** — không có trang tuỳ chọn, không xử lý huỷ đăng ký,
   không gộp tin hay giờ im lặng.
2. **Gửi quảng bá, chiến dịch** — không phân nhóm đối tượng, không gửi hàng loạt theo chiến dịch,
   không A/B testing, không thống kê mở/nhấp.
3. **Kênh khác ngoài email ở phiên bản đầu** — chưa SMS, push, nền tảng chat hay webhook chung.
4. **Hộp thư thông báo trong ứng dụng** — sản phẩm không lưu và không phục vụ dòng thông báo để
   người dùng cuối đọc bên trong một ứng dụng.
5. **Quyết định khi nào cần thông báo** — sản phẩm không theo dõi sự kiện và không áp quy tắc nghiệp
   vụ để tự quyết; hệ thống nguồn quyết định, sản phẩm chuyển đi.
6. **Danh bạ người nhận** — sản phẩm không sở hữu cơ sở dữ liệu liên hệ; người nhận đi kèm yêu cầu.
7. **Giao diện cho người nhận** — phiên bản đầu phục vụ ứng dụng và quản trị viên, không phục vụ
   người nhận thư.
8. **Hẹn giờ hoặc gửi lặp lại** — chưa có gửi theo lịch trong phiên bản đầu.
