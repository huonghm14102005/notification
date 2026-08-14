# Quy ước kỹ thuật

Biến các quyết định trong [ARCHITECTURE.md](ARCHITECTURE.md) thành quy tắc triển khai nhất quán cho
cả người và AI.

Một quy tắc chỉ được nằm ở đây khi thoả cả ba điều: **lặp lại trên nhiều feature**, **bảo vệ một
quyết định kiến trúc**, và **kiểm tra được bằng review hoặc công cụ**. Quy tắc nghiệp vụ của từng
feature thuộc về đặc tả feature đó, không thuộc tài liệu này.

Cột "Bảo vệ" tham chiếu tới quyết định D1–D10 và invariant I1–I18.

## 1. Thuật ngữ và cách đặt tên

Ngôn ngữ trong mã nguồn phải trùng với ngôn ngữ trong [domain-map.md](domain-map.md). Mã nguồn viết
bằng tiếng Anh, tài liệu viết bằng tiếng Việt.

| Khái niệm nghiệp vụ | Định danh trong mã | Cấm dùng |
|---------------------|--------------------|----------|
| Tổ chức | `tenant` | `org`, `company`, `school` |
| Hệ thống nguồn | `producer` | `client`, `app`, `system` |
| Khoá của máy | `apiKey` | `token`, `secret` |
| Tài khoản gửi | `sender` | `provider`, `smtp`, `account` |
| Thông báo (lời hứa) | `notification` | `message`, `mail` |
| Một lần gửi | `deliveryAttempt` | `delivery`, `try`, `send` |
| Mẫu nội dung | `template` | `layout` |

- Không dùng `sent` cho ý "người nhận đã đọc". `sent` có đúng một nghĩa: tài khoản gửi đã nhận
  thông điệp.
- Không dùng `failed` cho một lần gửi hỏng. Lần gửi hỏng là `attempt.result = "failure"`; `failed`
  là trạng thái kết thúc của thông báo.

Quy ước hình thức, giống dịch vụ CDN:

| Loại | Cách viết | Ví dụ |
|------|-----------|-------|
| Thư mục module | kebab-case | `notification/` |
| Tệp | kebab-case, có hậu tố vai trò | `notification.service.ts` |
| Kiểu, interface | PascalCase | `EmailSender`, `NotificationRow` |
| Biến, hàm | camelCase | `tenantId`, `acceptNotification` |
| Hằng | SCREAMING_SNAKE_CASE | `MAX_DELIVERY_ATTEMPTS` |
| Cột cơ sở dữ liệu | snake_case | `tenant_id`, `created_at` |
| Trường trong phản hồi API | camelCase | `notificationId`, `createdAt` |

## 2. Cấu trúc thư mục

```
notify-api/
├── src/
│   ├── config/         nạp và kiểm tra biến môi trường, xuất ra một đối tượng đã đóng băng
│   ├── lib/            db, queue, crypto, logger, errors, pagination
│   ├── middleware/     xác thực, xử lý lỗi, giới hạn tần suất
│   ├── modules/
│   │   └── {domain}/
│   │       ├── {domain}.route.ts
│   │       ├── {domain}.service.ts
│   │       ├── {domain}.repository.ts
│   │       └── {domain}.schema.ts
│   ├── providers/
│   │   └── email/      smtp.ts và index.ts (chọn cài đặt)
│   ├── worker/         tiến trình tiêu thụ hàng đợi
│   └── index.ts        điểm vào của api
└── migrations/         đánh số tăng dần
```

Tên module lấy đúng từ domain map: `identity`, `sender`, `template`, `notification`, `delivery`,
`history`. Thêm module mới đồng nghĩa với thêm một domain — phải sửa domain map trước.

## 3. Ranh giới module

| Quy tắc | Bảo vệ |
|---------|--------|
| Route không truy vấn cơ sở dữ liệu; chỉ gọi service của chính module mình | ranh giới domain |
| Service không import repository của module khác; muốn dùng thì gọi service của module sở hữu | quyền sở hữu dữ liệu |
| Cấm phụ thuộc vòng. Nếu hai module cần nhau, tách phần dùng chung ra `lib/` | D3 |
| `template` là thuần tuý: vào là câu chữ và dữ liệu, ra là văn bản. Không I/O, không import `db`, `queue` hay `providers/` | quyền sở hữu dữ liệu |
| `sender` không import `notification` hay `delivery` | chiều phụ thuộc |
| Chỉ `delivery` được import `providers/` | D7 |
| Chỉ `worker/` được đăng ký hàm xử lý job; `api` chỉ đẩy job | D3 |

Chiều phụ thuộc cho phép: `route → service → repository → lib`, và `delivery → providers`.

## 4. Quy ước API

- Tất cả đường dẫn có tiền tố phiên bản: `/v1/...`. Thay đổi phá vỡ tương thích thì tăng tiền tố,
  không sửa tại chỗ.
- Danh từ số nhiều, kebab-case: `/v1/notifications`, `/v1/api-keys`.
- Thân yêu cầu và phản hồi dùng camelCase; ngày giờ là chuỗi ISO 8601, múi giờ UTC; chuyển đổi từ
  `Date` thực hiện ở tầng service.
- Tiếp nhận thành công trả `202` kèm mã thông báo, không phải `200` — phản hồi là lời hứa, không
  phải kết quả (I5).
- Danh sách luôn phân trang và luôn có cùng khung bao: `{ items, total, page, limit }`.
- Không endpoint nào trả về bí mật của tài khoản gửi hay khoá API dạng thô. Khoá API chỉ hiện đúng
  một lần, ngay tại phản hồi tạo khoá (I4).

## 5. Contract lỗi

Mọi lỗi ra khỏi API đều có cùng khung bao:

```json
{ "error": "Notification not found", "statusCode": 404 }
```

Lỗi kiểm tra dữ liệu có thêm `details`:

```json
{ "error": "Validation failed", "statusCode": 400,
  "details": [{ "path": "recipient", "message": "Invalid email address" }] }
```

Quy tắc:

- Service ném lỗi có kiểu từ `lib/errors.ts` (`ValidationError`, `UnauthorizedError`,
  `ForbiddenError`, `NotFoundError`, `RateLimitError`, `ProviderError`). Route không tự dựng phản hồi lỗi.
- Một bộ xử lý lỗi toàn cục ánh xạ lỗi có kiểu sang mã trạng thái. Route không có `try/catch` chỉ để
  đổi mã trạng thái.
- Nội dung lỗi 5xx không bao giờ ra tới bên gọi; ghi log kèm mã tương quan rồi trả một câu chung.
- Không bao giờ nuốt lỗi. Việc "cố gắng hết sức" (đẩy hàng đợi, đếm chỉ số) được phép bỏ qua nhưng
  bắt buộc ghi log kèm ngữ cảnh.
- Lỗi từ nhà cung cấp luôn được adapter phân loại thành `transient` hoặc `permanent` trước khi rời
  khỏi `providers/` (D7, I13).

## 6. Kiểm tra dữ liệu

- Zod, khai báo trong `{domain}.schema.ts`, áp ở biên route. Service nhận đầu vào đã có kiểu và giả
  định nó hợp lệ.
- Kiểm tra dữ liệu là điều kiện tiên quyết của việc tiếp nhận: yêu cầu sai bị từ chối đồng bộ và
  không để lại bản ghi nào (I8).
- Biên trên của độ dài (tiêu đề, nội dung, số biến) thuộc schema, không nằm rải trong service.
- Payload của job cũng được kiểm tra bằng schema khi worker nhận, đúng như dữ liệu HTTP.

## 7. Phân quyền và cô lập tenant

| Quy tắc | Bảo vệ |
|---------|--------|
| Mọi yêu cầu xác định tổ chức trước khi làm bất cứ việc gì; không xác định được thì `401` trước cả bước kiểm tra dữ liệu | I2 |
| Mọi hàm repository chạm vào dữ liệu thuộc tổ chức đều nhận `tenantId` làm tham số đầu và lọc theo nó. Cấm truy vấn "toàn cục" ngoài các tác vụ vận hành | I1, I2 |
| Không lấy `tenantId` từ thân yêu cầu, tham số truy vấn hay đường dẫn — chỉ lấy từ thông tin xác thực | I2 |
| Không tìm thấy vì khác tổ chức thì trả `404`, không trả `403` | rò rỉ thông tin |
| Xác thực khoá của máy tra theo tiền tố rồi so băm; khoá đã thu hồi hỏng ngay lập tức, không có bộ nhớ đệm | I3 |

Định dạng khoá của máy: `notify_` + 64 ký tự hex. Lưu dạng băm cùng tiền tố dùng để tra cứu.

## 8. Cơ sở dữ liệu và migration

- Mọi thay đổi lược đồ đi kèm một migration đánh số tăng dần, có cả `up` và `down`; sửa tay lược đồ
  là vi phạm.
- Migration chạy trước khi phiên bản mới khởi động và phải tương thích với phiên bản đang chạy —
  không xoá cột trong cùng bản phát hành với việc ngừng dùng cột đó.
- Mọi bảng thuộc tổ chức đều có `tenant_id` và một chỉ mục bắt đầu bằng `tenant_id`.
- Mọi bảng đều có `created_at`; bảng có thể sửa thì thêm `updated_at`.
- Bảng lần gửi chỉ được ghi thêm: không `UPDATE`, không `DELETE` (I12, I17). Vi phạm quy tắc này
  phát hiện được khi review vì trong `delivery.repository.ts` sẽ xuất hiện câu lệnh cập nhật.
- Cấu hình (tổ chức, tài khoản gửi, mẫu, khoá) dùng xoá mềm bằng `deleted_at`; bản ghi lịch sử không
  bao giờ bị xoá mềm, chỉ bị xoá theo thời hạn lưu.
- Truy vấn qua Kysely; không ghép chuỗi SQL.

## 9. Giao dịch

- Một thao tác ghi thay đổi nhiều bảng thì nằm trong một giao dịch, mở ở tầng service và truyền
  xuống repository.
- Không gọi mạng bên trong giao dịch: không SMTP, không đẩy hàng đợi. Đẩy hàng đợi luôn xảy ra
  **sau khi commit** (D4) — nếu đẩy hỏng, tác vụ quét sẽ bù (I6).
- Giao dịch không bao giờ bao trọn một lần gửi. Ghi dòng lần gửi là một thao tác riêng, sau khi nhà
  cung cấp trả lời.

## 10. Tích hợp bên ngoài

- Mọi hệ thống bên ngoài đều được truy cập qua một cổng khai báo trong `providers/{loại}/index.ts`.
  Logic gửi phụ thuộc vào cổng, không phụ thuộc thư viện.
- Cổng chỉ nhận và trả kiểu của riêng ta; không để kiểu của thư viện SMTP rò ra ngoài `providers/`.
- Adapter chịu trách nhiệm: đặt thời gian chờ, ánh xạ lỗi sang `transient`/`permanent`, và trả về mã
  tham chiếu của nhà cung cấp nếu có.
- Adapter không tự thử lại, không ghi cơ sở dữ liệu, không quyết định số lần thử — đó là việc của
  `delivery`.
- Thêm một nhà cung cấp là thêm một tệp trong `providers/email/` và một nhánh chọn trong `index.ts`;
  không được sửa `notification` hay `delivery` (D7).

## 11. Job nền

| Quy tắc | Bảo vệ |
|---------|--------|
| Payload của job chỉ chứa định danh và một số phiên bản: `{ v: 1, notificationId }`. Không nhét nội dung | D5 |
| Số phiên bản payload tăng khi hình dạng đổi; hàm xử lý phải nhận được cả phiên bản cũ trong suốt một chu kỳ phát hành, để `api` và `worker` quay lui độc lập được | ra bản mới |
| Hàm xử lý job phải idempotent: nạp lại trạng thái hiện tại và tự thoát nếu thông báo đã ở trạng thái kết thúc | I6, A5 |
| Hàm xử lý xác nhận job chỉ sau khi kết quả đã commit | mất việc |
| Retry có giới hạn cứng bằng hằng số cấu hình; không có vòng lặp thử vô hạn | I15 |
| Hỏng vĩnh viễn được lưu kèm lý do và không tự thử lại; xử lý tiếp là việc của con người | I13, I14 |
| Job không bao giờ là nguồn sự thật — mất Redis chỉ mất lịch, tác vụ quét dựng lại được | D4 |
| Mỗi job mang theo mã tương quan của yêu cầu đã sinh ra nó | truy vết |

## 12. Ghi log

- Log có cấu trúc dạng JSON qua `lib/logger.ts`; cấm `console.log`.
- Mọi dòng log của một yêu cầu và của các job nó sinh ra đều mang cùng một `correlationId`.
- Trường bắt buộc khi có: `correlationId`, `tenantId`, `notificationId`.
- Cấm ghi log: bí mật tài khoản gửi, khoá API dạng thô, mật khẩu, và toàn văn nội dung thư. Ghi độ
  dài và mã băm khi cần chẩn đoán.
- Log ở mức `info` cho các mốc vòng đời (đã tiếp nhận, đã gửi, đã từ bỏ), `warn` cho hỏng tạm thời,
  `error` cho hỏng vĩnh viễn và lỗi ngoài dự kiến.

## 13. Kiểm thử

- Logic thuần tuý (dựng nội dung, phân loại lỗi, tính giãn cách thử lại) có kiểm thử đơn vị, không mock.
- Service có kiểm thử tích hợp chạy trên cơ sở dữ liệu thật; nhà cung cấp bên ngoài được thay bằng
  một cài đặt giả của cổng, không phải bằng mock thư viện.
- Mỗi module thuộc tổ chức có ít nhất một kiểm thử khẳng định dữ liệu của tổ chức khác không đọc
  được (I2). Đây là yêu cầu bắt buộc khi thêm bất kỳ endpoint mới nào.
- Mỗi hàm xử lý job có một kiểm thử chạy nó hai lần trên cùng đầu vào và khẳng định kết quả không
  đổi (idempotent).
- Kiểm thử không phụ thuộc thứ tự chạy và tự dọn dữ liệu của mình.

## 14. Cấu hình và bí mật

- Toàn bộ cấu hình đến từ biến môi trường, nạp và kiểm tra một lần lúc khởi động trong `config/`;
  ứng dụng không khởi động nếu thiếu biến bắt buộc. Cấm đọc `process.env` ngoài `config/`.
- `.env.example` liệt kê mọi biến; `.env` không bao giờ được commit.
- Bí mật của tài khoản gửi mã hoá khi lưu bằng khoá lấy từ môi trường, chỉ giải mã tại điểm gửi, và
  bị loại khỏi mọi bộ tuần tự hoá (I4, D8).
- Không hằng số bí mật nào nằm trong mã nguồn — kể cả giá trị mặc định cho môi trường phát triển.
- Giới hạn tần suất, số lần thử và các mốc giãn cách là hằng số cấu hình, không phải số rải trong mã.

## 15. Tài liệu

- Đặc tả trước, mã sau: một feature phải có tài liệu trong `docs/features/` trước khi viết mã.
- Đổi quyết định kiến trúc thì sửa `ARCHITECTURE.md` trong cùng PR; đổi khái niệm nghiệp vụ thì sửa
  `domain-map.md`.
- Tài liệu trong repository này viết bằng tiếng Việt; mã nguồn, tên định danh và commit viết bằng
  tiếng Anh.
- Commit theo dạng `feat:`, `fix:`, `refactor:`, `docs:`, `chore:`; nhánh theo dạng
  `feature/…`, `fix/…`.
- PR phải chạy qua kiểm tra kiểu (typecheck) và kiểm thử trước khi gộp.
