# Quy trình phát triển

Quy định một thay đổi đi từ ý tưởng đến bản phát hành như thế nào, và AI được phép hành động tới đâu
ở mỗi giai đoạn.

Tài liệu này nói về **quy trình**. Quy tắc viết mã nằm ở [CONVENTIONS.md](CONVENTIONS.md); phạm vi
sản phẩm nằm ở [MVP.md](MVP.md).

## 1. Vòng đời của một feature

```
Planned  ──▶  Draft  ──▶  Review  ──▶  Approved  ──▶  Implementing  ──▶  Verified  ──▶  Released
                 ▲           │              │               │
                 └───────────┴──────────────┴───────────────┘
                        quay lại Review khi mã không theo được đặc tả
```

| Trạng thái | Nghĩa là gì | Bằng chứng |
|-----------|-------------|------------|
| Planned | Đã nằm trong phạm vi, chưa viết đặc tả | có một dòng trong bảng feature của SPECS.md |
| Draft | Đặc tả đang viết, còn chỗ trống | tệp trong `docs/features/v1/`, đầu tệp ghi `Status: Draft` |
| Review | Đặc tả đủ đầy, đang chờ chốt | `Status: Review` |
| Approved | Người phụ trách đã duyệt đặc tả | `Status: Approved` kèm ngày duyệt |
| Implementing | Đang viết mã theo đặc tả đã duyệt | nhánh đang mở, đặc tả không đổi |
| Verified | Mã chạy đúng mọi acceptance criteria, test xanh | PR xanh và được duyệt |
| Released | Đã chạy trên môi trường thật | có mục trong `docs/changelog/` |

Trạng thái ghi ngay ở đầu tệp đặc tả, không lưu ở nơi khác:

```md
# 03 — Notification intake

Status: Approved
Approved: 2026-08-20
```

## 2. Quyền của AI theo trạng thái

| Trạng thái | AI được làm | AI không được làm |
|-----------|-------------|-------------------|
| Planned | Phân tích sơ bộ, đặt câu hỏi, khảo sát mã hiện có | Viết đặc tả như thể đã chốt |
| Draft | Đọc lại, chỉ ra chỗ thiếu, chỗ mâu thuẫn, chỗ mập mờ | **Không viết mã triển khai** |
| Review | Đề xuất phương án kèm đánh đổi, viết acceptance criteria | Viết mã; tự chọn giúp phương án |
| Approved | Lập kế hoạch triển khai, chia bước, chuẩn bị migration | Đổi nội dung đặc tả trong lúc làm |
| Implementing | Viết mã, viết test, chạy typecheck, báo blocker | Âm thầm lệch khỏi đặc tả |
| Verified | Sửa lỗi đã được xác nhận | Thêm chức năng mới nhân tiện |
| Released | Không tự ý đổi gì | Sửa hành vi khi chưa có đặc tả mới |

Hai luật cứng suy ra từ bảng trên:

- **AI không bao giờ triển khai một đặc tả ở trạng thái Draft.** Nếu được yêu cầu viết mã cho một
  đặc tả Draft, phải trả lời rằng đặc tả chưa được duyệt và hỏi xem có đưa lên Review không.
- **Mọi lệch khỏi đặc tả đều phải nói ra.** Phát hiện đặc tả sai hoặc không làm được thì dừng, đưa
  đặc tả về Review, không "tự sửa cho hợp lý".

## 3. Quy tắc bắt buộc

| # | Quy tắc | Kiểm tra ở đâu |
|---|---------|----------------|
| R1 | Đặc tả phải được cập nhật **trước khi** hành vi thay đổi | review PR: PR đổi hành vi mà không đổi `docs/` thì bị chặn |
| R2 | Không triển khai đặc tả Draft | review PR: kiểm tra `Status:` của tệp đặc tả liên quan |
| R3 | Contract công khai (endpoint, thân yêu cầu/phản hồi, mã lỗi, hình dạng payload job) cần được duyệt tường minh | trạng thái đặc tả phải là Approved trước khi mở PR |
| R4 | Thay đổi cơ sở dữ liệu phải có migration | review PR: đổi lược đồ mà không có tệp trong `migrations/` thì bị chặn |
| R5 | Mỗi acceptance criterion ánh xạ tới ít nhất một test | review PR: mỗi tiêu chí trong đặc tả có một test mang tên tương ứng |
| R6 | Mã không theo được đặc tả thì đặc tả quay về Review | tác giả PR chuyển trạng thái, không tự ý sửa hành vi |

## 4. Thay đổi feature

```
1. Đọc đặc tả hiện tại          docs/features/v1/{feature}.md
2. Sửa đặc tả                   yêu cầu, contract, quy tắc nghiệp vụ, acceptance criteria
3. Chuyển Draft → Review        yêu cầu người phụ trách duyệt
4. Được duyệt → Implementing    viết mã theo thứ tự schema → repository → service → route
5. Viết test theo từng criterion
6. Typecheck và test xanh → mở PR
7. PR được duyệt và gộp → Verified
8. Chạy thật → Released, ghi changelog
```

Feature mới thì thêm một tệp `docs/features/v1/{số}-{tên}.md` và thêm một dòng vào bảng feature của
SPECS.md. Sửa feature cũ thì chỉ sửa tệp của chính nó và chỉ chạm vào module tương ứng.

## 5. Thay đổi API

Contract công khai là thứ bên ngoài phụ thuộc vào: đường dẫn, thân yêu cầu và phản hồi, mã lỗi, ý
nghĩa mã trạng thái, và hình dạng payload job giữa `api` và `worker`.

Thêm endpoint:

```
1. Mô tả trong đặc tả feature: method, path, xác thực, yêu cầu, phản hồi, mã lỗi
2. Được duyệt (R3)
3. Thêm schema Zod trong {module}.schema.ts
4. Cài đặt route, service, repository
5. Thêm test, trong đó bắt buộc có test cô lập tenant
```

Sửa endpoint đã có: sửa đặc tả trước, đánh giá xem có phá vỡ tương thích không, rồi mới sửa mã.

Việc **không** phá vỡ tương thích: thêm trường tuỳ chọn vào yêu cầu, thêm trường vào phản hồi, thêm
endpoint mới, nới lỏng kiểm tra dữ liệu.

Việc **có** phá vỡ tương thích: bỏ hoặc đổi tên trường, thêm trường bắt buộc, đổi kiểu, đổi mã trạng
thái hoặc mã lỗi, siết kiểm tra dữ liệu, đổi ý nghĩa một trạng thái. Xem mục 7.

## 6. Thay đổi cơ sở dữ liệu

```
1. Mô tả thay đổi lược đồ trong đặc tả feature
2. Tạo migrations/{số}_{mô_tả}.ts, có cả up() và down()
3. Cột thuộc tổ chức đi kèm chỉ mục bắt đầu bằng tenant_id
4. Chạy thử up rồi down trên cơ sở dữ liệu sạch trước khi mở PR
```

Migration phải chạy được **trước khi** phiên bản mới khởi động và không được làm hỏng phiên bản đang
chạy. Vì vậy việc bỏ một cột luôn tách thành hai bản phát hành:

```
Bản 1: mã ngừng đọc và ngừng ghi cột đó (cột vẫn còn)
Bản 2: migration xoá cột
```

Đổi tên cột làm tương tự: thêm cột mới → ghi cả hai → chuyển dữ liệu → ngừng dùng cột cũ → xoá.

Không bao giờ sửa một migration đã gộp; sai thì viết migration mới.

## 7. Breaking change

```
1. Đặc tả nêu rõ: cái gì hỏng, ai bị ảnh hưởng, đường di trú
2. Được duyệt tường minh (R3) — không tự quyết
3. Ghi vào docs/changelog/v{phiên bản}.md
4. Báo cho đội của các hệ thống nguồn trước khi phát hành
```

Với contract HTTP, mặc định là **không phá vỡ**: mở `/v2` bên cạnh `/v1` và giữ `/v1` sống cho tới
khi mọi hệ thống nguồn đã chuyển. Với payload job, luôn tăng số phiên bản `v` và để worker hiểu cả
phiên bản cũ trong ít nhất một chu kỳ phát hành, vì `api` và `worker` quay lui độc lập.

## 8. Security review

Bắt buộc rà soát an toàn khi thay đổi chạm vào: xác thực, khoá API, bí mật của tài khoản gửi, ranh
giới tổ chức, giới hạn tần suất, hoặc nội dung do hệ thống nguồn cung cấp.

- [ ] Mọi endpoint mới đều xác định tổ chức trước khi làm việc khác
- [ ] Mọi truy vấn mới đều lọc theo `tenant_id`, lấy từ thông tin xác thực chứ không từ yêu cầu
- [ ] Có test khẳng định không đọc được dữ liệu của tổ chức khác
- [ ] Không bí mật nào xuất hiện trong phản hồi, log hay thông báo lỗi
- [ ] Dữ liệu vào được kiểm tra bằng Zod, có biên trên độ dài
- [ ] Thao tác ghi có giới hạn tần suất
- [ ] Lỗi 5xx không lộ thông tin nội bộ

## 9. Yêu cầu về test

- Mỗi acceptance criterion ↔ ít nhất một test (R5).
- Mọi endpoint chạm dữ liệu thuộc tổ chức ↔ một test cô lập tenant.
- Mọi hàm xử lý job ↔ một test chạy hai lần và khẳng định kết quả không đổi.
- Sửa lỗi ↔ một test tái hiện lỗi đó, viết trước khi sửa.
- Test đặt tên theo tiêu chí mà nó kiểm chứng, để đối chiếu ngược lại đặc tả được.

## 10. Code review

Trước khi gộp:

- [ ] Đặc tả đã cập nhật và ở trạng thái Approved (R1, R2, R3)
- [ ] Mã làm đúng những gì đặc tả nói, không nhiều hơn
- [ ] Có migration nếu lược đồ đổi, và `down()` đã chạy thử (R4)
- [ ] Mỗi acceptance criterion có test tương ứng (R5)
- [ ] Theo đúng CONVENTIONS.md, đặc biệt là chiều phụ thuộc và lọc theo tổ chức
- [ ] `npm run typecheck` và test xanh
- [ ] Đã ghi changelog nếu là breaking change

PR chỉ chứa một feature hoặc một bản sửa lỗi. Commit theo dạng `{type}({scope}): {mô tả}`, ví dụ
`feat(notification): accept inline subject and body`.

## 11. Phát hành

```
1. Gộp vào main            → CI chạy typecheck và test
2. Build một image duy nhất → api và worker cùng image, khác entrypoint
3. Chạy migration
4. Triển khai api và worker
5. Kiểm tra health của cả hai
6. Theo dõi 15 phút: tỉ lệ hỏng, độ dài hàng đợi, log lỗi
7. Ghi mục changelog
```

`api` và `worker` luôn phát hành cùng một bản build để không lệch phiên bản.

## 12. Rollback

Điều kiện quay lui: tỉ lệ hỏng tăng, hàng đợi tăng không giảm, health đỏ, hoặc lỗi làm mất thông báo.

```
1. Triển khai lại image của bản trước (api và worker)
2. Chỉ chạy migration down() khi bản mới có migration phá vỡ tương thích
   → nếu theo đúng mục 6 thì thường không cần: lược đồ mới vẫn hợp với mã cũ
3. Kiểm tra health, xem hàng đợi có tiêu thụ trở lại không
4. Đẩy lại các thông báo còn kẹt ở trạng thái chưa kết thúc
5. Ghi lại sự cố và nguyên nhân
```

Không có thông báo nào được mất khi quay lui: chúng nằm trong Postgres, không nằm trong hàng đợi.

## 13. Hotfix

Chỉ dùng cho lỗi đang gây thiệt hại trên môi trường thật.

```
1. Nhánh từ main: fix/{mô tả}
2. Sửa nhỏ nhất có thể — không kèm dọn dẹp, không kèm chức năng mới
3. Test tái hiện lỗi
4. Duyệt nhanh nhưng vẫn phải có người duyệt
5. Phát hành và theo dõi
6. Trong vòng một ngày làm việc: cập nhật đặc tả cho khớp hành vi mới
```

Hotfix là ngoại lệ duy nhất của R1 — đặc tả được sửa **sau**, nhưng bắt buộc phải sửa. Hotfix mà
chưa cập nhật đặc tả là một khoản nợ còn treo, không phải việc đã xong.

## 14. Tài liệu cần sửa theo loại thay đổi

| Loại thay đổi | Tệp phải cập nhật |
|---------------|-------------------|
| Feature mới | `docs/features/v1/` + `SPECS.md` |
| Đổi API | `docs/features/v1/` (+ `ARCHITECTURE.md` nếu đổi ranh giới) |
| Đổi lược đồ | `docs/features/v1/` + tệp migration |
| Breaking change | `docs/changelog/v{phiên bản}.md` |
| Đổi khái niệm nghiệp vụ | `domain-map.md` |
| Đổi quyết định kỹ thuật | `ARCHITECTURE.md` |
| Đổi khuôn mẫu viết mã | `CONVENTIONS.md` |
