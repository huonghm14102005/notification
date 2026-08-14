# Domain Map

Mục đích: nhận diện các vùng trách nhiệm nghiệp vụ trước khi chia hệ thống thành component kỹ thuật.
Domain ở đây là ranh giới khái niệm, không phải microservice và cũng không phải bảng dữ liệu.

Rút ra từ hành trình trong [MVP.md](MVP.md). Những gì đã chốt nằm ở [Quyết định đã chốt](#quyết-định-đã-chốt),
những gì còn treo nằm ở [Điểm còn bỏ ngỏ](#điểm-còn-bỏ-ngỏ).

## 1. Bóc tách từ hành trình

Hành trình, viết lại theo ngôn ngữ nghiệp vụ:

```
Quản trị viên nhận quyền sở hữu một không gian làm việc
  → mô tả cách thông điệp rời khỏi hệ thống (một tài khoản gửi)
  → chứng minh tài khoản đó dùng được
  → cấp cho một hệ thống nguồn quyền yêu cầu gửi
Hệ thống nguồn yêu cầu gửi một thông điệp, tự cung cấp câu chữ
  (hoặc gọi tên một mẫu nội dung và điền vào chỗ trống)
  → yêu cầu được tiếp nhận, kèm một lời hứa sẽ gửi
  → câu chữ trở thành một thông điệp hoàn chỉnh
  → thông điệp hoàn chỉnh được giao cho tài khoản gửi
  → người nhận nhận được
  → sau đó có người hỏi yêu cầu ấy đã ra sao
  → và hỏi lại lần nữa, sau khi có lỗi
```

### Danh từ nghiệp vụ

| Khái niệm | Hành vi |
|-----------|---------|
| Tổ chức (tenant) | Sở hữu mọi thứ còn lại; là ranh giới nhìn thấy dữ liệu |
| Quản trị viên | Người cấu hình tổ chức và xem lịch sử |
| Hệ thống nguồn (producer) | Bên gọi là máy, hành động thay cho tổ chức |
| Thông tin xác thực | Chứng minh ai đang gọi — phiên của người, hoặc khoá của máy |
| Tài khoản gửi (sender) | Một cách để thông điệp rời hệ thống (máy chủ, tài khoản, địa chỉ gửi) |
| Mẫu nội dung (template) | Câu chữ dùng lại được, có chỗ trống đặt tên |
| Người nhận | Người mà thông điệp hướng tới; đi kèm từng yêu cầu, không lưu thành danh bạ |
| Thông báo (notification) | Một yêu cầu đã được tiếp nhận — chính là lời hứa |
| Thông điệp hoàn chỉnh | Tiêu đề và nội dung sau khi đã điền đầy đủ |
| Lần gửi (delivery attempt) | Một lần giao thông điệp cho tài khoản gửi |
| Kết quả | Điều tài khoản gửi trả lời: nhận, từ chối, không liên lạc được |
| Lịch sử | Bản ghi tra cứu được về các thông báo và các lần gửi |

### Hành động nghiệp vụ

Nhận quyền sở hữu tổ chức · xác thực · cấu hình tài khoản gửi · chứng minh tài khoản gửi ·
soạn mẫu nội dung · cấp quyền cho máy · thu hồi quyền của máy · yêu cầu gửi thông báo ·
tiếp nhận yêu cầu · dựng nội dung · thực hiện lần gửi · ghi kết quả · gửi lại · từ bỏ · tra lịch sử.

### Quy tắc nghiệp vụ (quan sát được, chưa hình thức hoá)

1. Không thể yêu cầu gửi trước khi tổ chức có một tài khoản gửi dùng được. Câu chữ có thể đi kèm yêu
   cầu hoặc lấy từ mẫu nội dung.
2. Tiếp nhận là một lời hứa: đã nhận thì phải đi tới một trạng thái kết thúc.
3. Yêu cầu không bao giờ chờ tài khoản gửi — tiếp nhận và gửi là hai thời điểm khác nhau.
4. Bị từ chối vì lý do vĩnh viễn thì không thử lại; không liên lạc được thì phải thử lại.
5. Từ bỏ là một quyết định có ghi lý do, không bao giờ im lặng.
6. Con người có quyền yêu cầu thử lại sau khi hệ thống đã từ bỏ.
7. Mọi đọc và ghi đều nằm trong một tổ chức; không có gì đi xuyên qua.
8. Khoá của máy bị thu hồi thì ngừng tác dụng ngay, nhưng các thông báo nó đã tạo vẫn còn.
9. Thông tin đăng nhập của tài khoản gửi chỉ ghi và dùng, không đọc ngược ra được.

### Trạng thái và vòng đời

**Thông báo** — lời hứa với hệ thống nguồn:

```
đã tiếp nhận ──▶ đang xử lý ──▶ đã gửi ──▶ (đã tới | bị trả về)
      │              │
      │              ├──▶ hỏng  (hết số lần thử, hoặc bị từ chối vĩnh viễn)
      │              └──▶ người gửi lại ──▶ đang xử lý
      └──▶ bị từ chối (không tiếp nhận: thiếu nội dung, sai địa chỉ, mẫu không tồn tại)
```

"Bị từ chối" xảy ra đồng bộ và không tạo ra lời hứa nào. "Đã tới" và "bị trả về" cần nhà cung cấp
phản hồi nên nằm ngoài phạm vi MVP; "đã gửi" là trạng thái thành công của MVP, nghĩa là *tài khoản
gửi đã nhận*, không phải *người đã đọc*.

**Lần gửi** — một lần thử, bất biến khi đã kết thúc:

```
bắt đầu ──▶ thành công
        └─▶ thất bại (tạm thời → được thử tiếp)
                     (vĩnh viễn → không thử nữa)
```

**Tài khoản gửi** — `đã cấu hình → đã kiểm chứng → đang dùng → đã tắt`. Tài khoản chưa kiểm chứng
vẫn dùng được, nhưng quản trị viên chưa được chứng minh là nó chạy.

**Khoá của máy** — `đang hoạt động → đã thu hồi`. Không có trạng thái trung gian.

**Mẫu nội dung** — `nháp → đang dùng → đã rút`. Mẫu đã rút không dùng cho thông báo mới; thông báo đã
tiếp nhận giữ nguyên câu chữ mà nó đã dựng.

## 2. Các domain sơ bộ

| Domain | Trách nhiệm | Không phải trách nhiệm |
|--------|-------------|------------------------|
| **Identity & Access** | Tổ chức, quản trị viên, phiên đăng nhập, khoá của máy, ai được làm gì | Gửi cái gì và cho ai |
| **Sender Configuration** | Các đường ra của thông điệp: bản ghi tài khoản gửi, bí mật của nó, việc kiểm chứng | Nội dung; thời điểm gửi |
| **Message Content** | Mẫu nội dung, biến của mẫu, dựng thông điệp hoàn chỉnh từ mẫu và dữ liệu | Ai được dùng mẫu; thông điệp rời hệ thống bằng cách nào |
| **Notification Intake** | Tiếp nhận hoặc từ chối yêu cầu, giữ lời hứa, chống trùng | Câu chữ trông thế nào; gửi bằng đường nào |
| **Delivery** | Các lần gửi, chính sách thử lại, quyết định từ bỏ, đặc thù từng nhà cung cấp | Yêu cầu có hợp lệ không; câu chữ |
| **History & Audit** | Bản ghi bền vững và các câu hỏi đặt lên nó | Làm cho việc gì đó xảy ra |

Quan hệ:

```
Identity & Access ─── sở hữu ──▶ tất cả bên dưới (ranh giới tổ chức)

Notification Intake ──nhờ──▶ Message Content  (dựng câu chữ này với dữ liệu này, khi có dùng mẫu)
        │                             ▲
        │                             │
        └──giao việc──▶ Delivery ─────┘
                             │
                             └──dùng──▶ Sender Configuration (tài khoản nào, bí mật nào)

Tất cả ──ghi vào──▶ History & Audit
```

Chú ý chiều phụ thuộc: Delivery biết Sender Configuration nhưng Sender Configuration không biết gì về
thông báo; Message Content hoàn toàn không biết việc gửi. Intake là domain duy nhất mà hệ thống nguồn
nói chuyện trực tiếp.

## 3. Invariant

Luôn đúng, ở mọi thời điểm:

**Identity & Access**

- I1. Mỗi bản ghi thuộc về đúng một tổ chức.
- I2. Không thao tác đọc hay ghi nào chạm được dữ liệu của tổ chức khác, dù nhận vào định danh nào.
- I3. Khoá bị thu hồi không xác thực được gì kể từ thời điểm thu hồi.
- I4. Bí mật của tài khoản gửi chỉ ghi và dùng, không endpoint nào trả ra.

**Intake**

- I5. Thông báo đã tiếp nhận phải được ghi bền vững trước khi trả lời "đã nhận" cho bên gọi.
- I6. Thông báo đã tiếp nhận luôn đi tới trạng thái kết thúc — đã gửi, hỏng hoặc bị huỷ; không bao
  giờ mắc kẹt vô hạn ở trạng thái đang xử lý.
- I7. Thông báo tham chiếu tới tài khoản gửi (và mẫu nội dung, nếu có) tồn tại tại thời điểm tiếp nhận.
- I8. Yêu cầu bị từ chối không để lại thông báo nào.

**Content**

- I9. Nếu dùng mẫu mà thiếu biến thì đó là từ chối, không phải điền chỗ trống rỗng.
- I10. Cái đã gửi phải tái dựng được: thông báo giữ chính câu chữ đã gửi, kể cả khi mẫu bị sửa sau đó.

**Delivery**

- I11. Mỗi lần gửi thuộc về đúng một thông báo và một tài khoản gửi.
- I12. Lần gửi đã kết thúc không bao giờ bị sửa; thử tiếp thì tạo lần gửi mới.
- I13. Thông báo bị từ chối vĩnh viễn không bao giờ được thử lại tự động.
- I14. Thông báo hỏng luôn có lý do; hỏng mà không có lý do là điều không thể xảy ra.
- I15. Số lần thử tự động của một thông báo không bao giờ vượt giới hạn đã đặt.
- I16. Người gửi lại tạo ra một lần gửi mới; không bao giờ xoá các lần trước.

**History**

- I17. Lịch sử chỉ ghi thêm: kết quả đã ghi thì không bị viết đè.
- I18. Mọi thông báo trong lịch sử đều truy được về bên gọi đã tạo ra nó.

## 4. Quyền sở hữu dữ liệu sơ bộ

| Dữ liệu | Domain sở hữu |
|---------|---------------|
| Tổ chức, tài khoản quản trị, phiên đăng nhập | Identity & Access |
| Khoá của máy và quyền của nó | Identity & Access |
| Bản ghi tài khoản gửi, bí mật, kết quả kiểm chứng | Sender Configuration |
| Mẫu nội dung, định nghĩa biến, phiên bản mẫu | Message Content |
| Tiêu đề và nội dung hoàn chỉnh | Message Content (dựng ra), Notification Intake (lưu cùng lời hứa) |
| Bản ghi thông báo, dấu chống trùng, người nhận của yêu cầu đó | Notification Intake |
| Lần gửi, mã tham chiếu của nhà cung cấp, lý do hỏng | Delivery |
| Chính sách thử lại và giới hạn | Delivery |
| Lịch sử tra cứu được, vết thay đổi cấu hình | History & Audit |

Nội dung hoàn chỉnh là thứ duy nhất dùng chung: Message Content dựng ra, nhưng nó được lưu cùng thông
báo để cái đã gửi luôn tái dựng được (I10).

## 5. Thuật ngữ đa nghĩa

Những từ mà mỗi người hiểu một kiểu. Mỗi từ cần một nghĩa thống nhất.

| Thuật ngữ | Nghĩa A | Nghĩa B | Rủi ro nếu để mập mờ |
|-----------|---------|---------|----------------------|
| **Thông báo** | Yêu cầu mà hệ thống nguồn đã gửi | Email đã tới nơi | Lẫn lộn lời hứa với kết quả; một yêu cầu có thể sinh nhiều lần gửi |
| **Đã gửi** | Đã giao cho tài khoản gửi | Đã vào hộp thư người nhận | Báo cáo một tỉ lệ thành công mà ta không quan sát được |
| **Đã tới** | Nhà cung cấp xác nhận đã nhận ở phía sau | Người đã đọc | Hứa một bảo đảm mà MVP không làm được |
| **Hỏng** | Lần gửi này hỏng | Đã từ bỏ thông báo | Quản trị viên gửi lại nhầm thứ |
| **Người nhận** | Một địa chỉ trong một yêu cầu | Một người đã biết, có tuỳ chọn nhận tin | Trôi dần sang sở hữu danh bạ — điều đã loại trừ |
| **Kênh** | Loại đường truyền (email) | Một tài khoản đã cấu hình (máy chủ SMTP này) | Rối khi một tổ chức có hai tài khoản email |
| **Mẫu nội dung** | Câu chữ dùng lại | Đúng đoạn văn bản đã gửi đi | Sửa mẫu trông như viết lại lịch sử |
| **Tổ chức** | Một tổ chức khách hàng | Một ứng dụng có gửi thông báo | Quyết định việc một khách hàng có tách được các ứng dụng của mình hay không |
| **Gửi lại** | Hệ thống tự thử lại | Con người chủ động gửi lại | Giới hạn số lần thử và vết kiểm toán mất ý nghĩa |

Định nghĩa thống nhất:

- **Thông báo** = yêu cầu đã tiếp nhận (lời hứa). Thứ tới nơi người nhận là một *lần gửi*.
- **Đã gửi** = tài khoản gửi đã nhận thông điệp. Phiên bản đầu không khẳng định gì mạnh hơn.
- **Đã tới** = nhà cung cấp báo đã nhận ở phía sau; chưa có ở phiên bản đầu.
- **Hỏng** = đã từ bỏ thông báo. Một lần thử không thành công gọi là *lần gửi thất bại*.
- **Kênh** = loại đường truyền (email). **Tài khoản gửi** = một tài khoản cụ thể của kênh đó.
- **Tổ chức** = đơn vị sở hữu và cô lập; một ứng dụng bên trong tổ chức được nhận diện bằng khoá của
  nó, không phải bằng một tổ chức riêng.

## Quyết định đã chốt

Do người phụ trách sản phẩm quyết; phần còn lại của tài liệu đọc theo các quyết định này.

1. **Tổ chức là gì** — tổ chức là đơn vị sở hữu (trường đại học), không phải một ứng dụng. Các hệ
   thống nguồn bên trong (điểm, điểm rèn luyện, sau này là log lỗi) là các *ứng dụng gửi*, mỗi cái
   một khoá riêng. Hệ quả: lịch sử, giới hạn tần suất và thu hồi phải diễn đạt được theo từng ứng
   dụng gửi, mà không cần thêm một tầng cô lập nữa.
2. **Nội dung đến từ đâu** — hệ thống nguồn cung cấp tiêu đề và nội dung hoàn chỉnh, dịch vụ chuyển
   tiếp. Hệ quả: **Message Content là công cụ hỗ trợ, không phải cửa kiểm soát**. Việc dựng nội dung
   là tuỳ chọn và nằm ngoài đường tiếp nhận bắt buộc; thông báo tự mang nội dung của nó. I9 chỉ áp
   dụng khi có dùng mẫu; I10 vẫn giữ nguyên, vì nội dung đã gửi luôn được lưu cùng thông báo.
3. **Hình dạng kênh** — một yêu cầu đi đúng một kênh, và phiên bản đầu chỉ có email. Kênh thêm về sau
   là thêm một *loại tài khoản gửi*, không phải thêm khái niệm phát tán nhiều kênh trong một thông báo.

## Điểm còn bỏ ngỏ

Chưa trả lời; không điểm nào chặn phần kiến trúc, nhưng đều sẽ quay lại ở bước đặc tả:

1. **Chọn tài khoản gửi** — với một tài khoản mỗi tổ chức thì Delivery không phải chọn. Nếu một tổ
   chức có nhiều tài khoản, hệ thống nguồn chọn hay đánh dấu một cái mặc định?
2. **Thế nào là thành công về mặt nghiệp vụ** — báo cho tổ chức rằng "tài khoản gửi đã nhận" là đủ,
   hay sản phẩm phải khẳng định thư đã vào hộp thư (kéo theo phản hồi từ nhà cung cấp vào phiên bản
   đầu)?
3. **Ai được gửi lại** — hệ thống nguồn có được kích hoạt gửi lại không, hay chỉ quản trị viên?
4. **Thời hạn lưu lịch sử** — giữ nội dung và địa chỉ người nhận bao lâu, và ai được đọc nội dung
   thư sau khi đã gửi?
5. **Tin cậy nội dung** — vì hệ thống nguồn tự cung cấp câu chữ, cái gì ngăn một khoá bị lộ gửi nội
   dung bất kỳ từ địa chỉ của trường? Phương án: chỉ giới hạn tần suất theo khoá, hoặc ràng buộc
   danh sách địa chỉ nhận / tiền tố tiêu đề cho từng khoá.
