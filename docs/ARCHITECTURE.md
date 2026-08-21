# Kiến trúc

Hình hài kỹ thuật của notify-api, rút ra từ [PRODUCT.md](PRODUCT.md) và
[TARGET-DESIGN.md](TARGET-DESIGN.md). Quy tắc viết code bắt buộc nằm ở
[CONVENTIONS.md](CONVENTIONS.md).

Tài liệu này quyết định cấu trúc, ranh giới và cơ chế. Nó không định nghĩa endpoint, thân yêu cầu
hay cột dữ liệu — những thứ đó thuộc SPECS.md, viết ngay sau đây.

## 1. Bối cảnh

```
   Hệ thống nguồn của trường            notify-api                    Bên ngoài
 ┌──────────────────────┐        ┌───────────────────────────┐
 │ Hệ thống điểm        │        │  api        (HTTP)        │
 │ Điểm rèn luyện       │─HTTP──▶│    tiếp nhận, cấu hình,   │       ┌──────────────┐
 │ (sau) log lỗi        │ API key│    tra cứu                │       │ Tài khoản    │
 └──────────────────────┘        │        │                  │──────▶│ SMTP của     │
                                 │        ▼ giao việc        │       │ nhà trường   │
 ┌──────────────────────┐        │  worker     (không nhận HTTP)     └──────┬───────┘
 │ Quản trị viên        │─HTTP──▶│    dựng nội dung, gửi,    │              │
 │ (người)              │  phiên └────────────┬──────────────┘              ▼
 └──────────────────────┘                     │                        người nhận
                                    PostgreSQL │ Redis
```

Hai đơn vị triển khai, một mã nguồn, một cơ sở dữ liệu. Không dùng chung gì với dịch vụ CDN/Media:
repository riêng, cơ sở dữ liệu riêng, định danh riêng.

## 2. Các quyết định

| # | Quyết định | Lý do | Phương án bị loại |
|---|-----------|-------|-------------------|
| D1 | Dịch vụ độc lập, không phải module của cdn-api | Bên gọi là các hệ thống khác; đường gửi thông báo không được dính vào nhịp triển khai của dịch vụ media | Thêm module `notification` vào cdn-api — nhanh hơn nhưng buộc chung bản phát hành và mô hình tenant |
| D2 | Tenancy riêng, thông tin xác thực riêng, cơ sở dữ liệu riêng | Quyết định của người phụ trách sản phẩm; tenant của dịch vụ CDN không phải các hệ thống nguồn của trường | Dùng lại tenants/api_keys của cdn-api |
| D3 | Tách `api` và `worker`; worker polling PostgreSQL | Nhận bền vững, gửi sau mà không thêm Redis queue ở phiên bản đầu | Gửi ngay trong request — vi phạm M3 và phụ thuộc nhà cung cấp |
| D4 | PostgreSQL vừa là nguồn sự thật vừa là hàng đợi bền vững | Giảm thành phần và loại bỏ lỗi lệch trạng thái DB/queue; Redis chỉ dùng cho cache/rate limit khi cần | Dùng thêm queue trước khi lưu lượng thực tế yêu cầu |
| D5 | Job chỉ mang mã thông báo | Nội dung có thể lớn và có thể đổi; worker đọc lại chính bản ghi nó sắp xử lý | Nhét toàn bộ nội dung vào job |
| D6 | Mỗi lần gửi là một dòng bất biến (I12) | Chẩn đoán và kiểm toán cần mọi lần thử, không chỉ lần cuối | Ghi đè một trường trạng thái |
| D7 | Truy cập nhà cung cấp qua một cổng hẹp `EmailSender` | Nhà cung cấp thứ hai (S-05) và kênh sau này không được chạm vào intake | Gọi thẳng thư viện SMTP trong logic gửi |
| D8 | Bí mật của tài khoản gửi mã hoá bằng khoá ứng dụng, không bao giờ trả ra (I4) | Dịch vụ giữ thông tin đăng nhập mail thật của trường | Lưu dạng thô và trông vào phân quyền cơ sở dữ liệu |
| D9 | Nội dung đi kèm yêu cầu; mẫu nội dung là tuỳ chọn | Quyết định sản phẩm: hệ thống nguồn tự gửi tiêu đề và nội dung | Bắt buộc dùng mẫu |
| D10 | ASP.NET Core API + .NET Worker, EF Core/Npgsql, PostgreSQL và Redis; đóng gói Docker, vẫn dùng Compose/Nginx hiện có | Phù hợp hệ thống backend/worker dài hạn; type system, DI, hosted service và observability thống nhất; dịch vụ vốn độc lập nên không cần chung runtime với CDN | Node/Fastify cho phép chung toolchain với CDN nhưng không tạo lợi ích chia sẻ dữ liệu hay deployment |

## 3. Các tiến trình

### `api` — bề mặt vào duy nhất

HTTP không giữ trạng thái. Trách nhiệm: xác thực, phân quyền trong phạm vi một tổ chức, kiểm tra dữ
liệu và đọc ghi Postgres. Nó không nói chuyện với nhà cung cấp mail, trừ một
ngoại lệ: thư thử (M-05) gửi đồng bộ, vì quản trị viên đang chờ câu trả lời.

### `worker` — mọi thứ có thể chậm hoặc hỏng

Polling và claim notification tới hạn trong PostgreSQL. Trách nhiệm: nạp thông báo, dựng nội dung nếu có gọi tên mẫu, gọi cổng gửi,
ghi dòng kết quả lần gửi, quyết định thử lại hay từ bỏ. Không mở HTTP ngoài endpoint health. Mở rộng
theo chiều ngang là thêm worker; mức đồng thời mỗi worker có giới hạn để không dội vào nhà cung cấp.

An toàn khi sập: job chỉ được xác nhận sau khi dòng kết quả đã commit. Worker bị giết giữa lúc gửi
có thể khiến một thông điệp được gửi hai lần — chấp nhận theo giả định A5 (at-least-once), sẽ giảm
đi khi làm phần chống trùng (S-01).

## 4. Ranh giới module trong mã nguồn

Solution là modular monolith theo Clean Architecture; cấu trúc đầy đủ nằm tại
Module mới phải được ghi vào phần ranh giới module của tài liệu này trước khi triển khai.

```
src/
  Notification.Domain/          entity, value object, invariant
  Notification.Application/     use case và các interface hạ tầng
  Notification.Infrastructure/  EF Core, Redis, crypto, MailKit, observability
  Notification.Api/             HTTP endpoints và middleware
  Notification.Worker/          consumer và scheduled recovery jobs
```

Những quy tắc giữ cho ranh giới domain là thật:

- Endpoint không gọi thẳng DbContext/repository; use case không gọi repository của module khác.
- `template` là thuần tuý: cho câu chữ và dữ liệu thì trả về văn bản. Không I/O, không biết gì về
  tài khoản gửi hay thông báo.
- `sender` chỉ trả lời "cho tôi một tài khoản gửi dùng được của tổ chức này", không biết gì về thông báo.
- Chỉ Infrastructure cài đặt email adapter; Delivery chỉ phụ thuộc `IEmailSender` của Application.
- Mọi hàm repository đều nhận mã tổ chức và lọc theo nó — cô lập được ép ở tầng thấp nhất, không phải
  ở route (I1, I2).

## 5. Quyền sở hữu dữ liệu và độ bền

| Kho | Chứa | Yêu cầu về độ bền |
|-----|------|-------------------|
| PostgreSQL | Tổ chức, quản trị viên, API key, tài khoản gửi (bí mật đã mã hoá), mẫu nội dung, thông báo kèm nội dung và người nhận, các lần gửi | Nguồn sự thật; sao lưu và phục hồi là điều kiện hoàn tất MVP |
| Redis | Cache và bộ đếm giới hạn tần suất | Không nằm trên đường gửi cơ bản; mất Redis không làm mất notification |

Worker polling trực tiếp các notification chưa kết thúc theo `status` và `next_attempt_at`. Cơ chế claim và quét
notification kẹt ở DLVR-003 bảo đảm I6 mà không cần đồng bộ thêm một hàng đợi.

## 6. Đường đi của một thông báo

```
hệ thống nguồn ─▶ api: xác thực (API key → tổ chức + hệ thống nguồn)
              ├─ kiểm tra dữ liệu vào                  ─┐ từ chối ở đây không để lại bản ghi (I8)
              ├─ xác định tài khoản gửi của tổ chức     │
              ├─ dựng nội dung nếu có gọi tên mẫu       ├─ trong một giao dịch
              ├─ lưu thông báo (trạng thái: đã tiếp nhận)│
              └─ commit notification accepted          ─┘
            └▶ 202 kèm mã thông báo

worker ─▶ polling/claim notification accepted đã tới hạn
            ├─ đánh dấu đang xử lý
            ├─ mở tài khoản gửi, gửi
            ├─ ghi dòng lần gửi (thành công | thất bại + phân loại)
            └─ nếu hỏng: tạm thời → hẹn lại với giãn cách tăng dần, tới giới hạn
                         vĩnh viễn → kết thúc ở trạng thái hỏng, kèm lý do (I13, I14)
```

Việc phân loại lỗi thuộc về adapter của nhà cung cấp, không thuộc logic gửi: adapter ánh xạ phản hồi
SMTP thành `tạm thời` hoặc `vĩnh viễn`, nhờ vậy thêm nhà cung cấp mới không phải sửa logic thử lại.

Gửi lại thủ công (M-12) tạo một lần gửi mới trên cùng thông báo; không bao giờ viết lại lịch sử
(I16, I17).

## 7. An toàn

- Hai loại thông tin xác thực: phiên của quản trị viên (token ngắn hạn) và API key của máy
  (`notify_` + chuỗi ngẫu nhiên, lưu dạng băm, dùng tiền tố để tra cứu). Thu hồi có hiệu lực ngay (I3).
- Mọi yêu cầu đều xác định tổ chức trước tiên; yêu cầu không quy được về tổ chức nào thì bị từ chối
  trước cả bước kiểm tra dữ liệu.
- Bí mật tài khoản gửi mã hoá khi lưu bằng khoá lấy từ biến môi trường, chỉ giải mã trong worker lúc
  gửi, và bị loại khỏi mọi bộ tuần tự hoá, log và thông báo lỗi.
- Vì hệ thống nguồn tự cung cấp nội dung (D9), một khoá bị lộ có thể gửi văn bản bất kỳ từ địa chỉ
  của trường. Biện pháp ở MVP là giới hạn tần suất theo từng khoá cộng với việc quy trách nhiệm mọi
  thông báo về khoá đã tạo ra nó; ràng buộc chặt hơn phải được chốt trong feature bảo mật tương ứng.
- Giới hạn tần suất theo tổ chức và theo khoá, đếm trong Redis, áp dụng trước mọi thao tác ghi.

## 8. Vận hành

- **Health**: `api` và `worker` mỗi bên tự báo sống, kèm khả năng kết nối cơ sở dữ liệu và hàng đợi.
- **Log**: có cấu trúc, một mã tương quan cho mỗi yêu cầu, mang theo vào job để lần được một thông
  báo từ lúc tiếp nhận tới từng lần gửi. Thân lỗi 5xx không bao giờ mang thông tin nội bộ.
- **Chỉ số**: số tiếp nhận, số đã gửi, số hỏng, độ dài hàng đợi, số lần thử theo loại kết quả.
- **Triển khai**: Docker Compose cạnh hệ thống hiện có; `api` sau Nginx, `worker` không có đường vào.
  Hai image là cùng một bản build với entrypoint khác nhau nên không thể lệch phiên bản.
- **Ra bản mới**: migration chạy trước khi phiên bản mới khởi động; mỗi migration có bước lùi thành
  văn. `api` và `worker` quay lui độc lập được — đây chính là lý do job chỉ mang một mã (D5): worker
  phiên bản cũ vẫn xử lý được job mới.

## 9. Chủ động không có trong kiến trúc này

Hẹn giờ, danh bạ người nhận, hộp thư
trong ứng dụng, giao diện quản trị, triển khai đa vùng hay kiến trúc sẵn sàng cao. Mỗi thứ đều nằm
trong phần loại trừ ở mức sản phẩm và không được đưa lại vì lý do tiện tay về kỹ thuật.

## 10. Quy ước triển khai

Các rule kiểm tra được về C#, API, auth, database, delivery, callback, test, Docker và Git chỉ được
định nghĩa tại [CONVENTIONS.md](CONVENTIONS.md), không lặp lại trong tài liệu kiến trúc.

## 11. Những gì đặc tả phải chốt tiếp

Danh sách endpoint và contract; tên trạng thái được lưu; cột và chỉ mục của từng bảng; giới hạn số
lần thử và các mốc giãn cách; con số giới hạn tần suất; mã lỗi; quy trình kiểm chứng tài khoản gửi.
