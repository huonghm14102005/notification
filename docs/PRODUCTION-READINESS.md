# Ghi chú đưa notification-server lên production

Tài liệu này là checklist thực tế trước khi hệ thống phục vụ dữ liệu và lưu lượng thật. Một feature ở trạng
thái `Verified` chỉ chứng minh chức năng đã đúng trong phạm vi test; không đồng nghĩa toàn hệ thống đã sẵn sàng
production.

## 1. Những nguyên tắc phải nhớ

1. PostgreSQL là nguồn dữ liệu chính. Redis, SMTP, Firebase/APNs và các nhà cung cấp khác chỉ là hạ tầng hỗ trợ.
2. `Notification` là yêu cầu nghiệp vụ; mỗi kênh/người nhận tạo một `Delivery` độc lập. Retry delivery lỗi, không
   gửi lại delivery đã thành công.
3. Gửi ra mạng không thể bảo đảm exactly-once. Hệ thống phải chấp nhận at-least-once và dùng idempotency để giảm
   nguy cơ gửi trùng.
4. Một lần gửi đầu tiên cộng tối đa ba retry, tức tối đa bốn attempt. Chỉ lỗi tạm thời mới được retry.
5. Callback cũng là at-least-once. Hệ thống nhận callback phải chống trùng bằng `eventId` và vẫn có API tra cứu để
   đối soát.
6. Không ghi password, raw API key, SMTP password, push token, callback secret hoặc nội dung nhạy cảm vào log.
7. Mọi truy vấn dữ liệu nghiệp vụ phải giới hạn theo `tenantId`; ID dạng UUID không thay thế kiểm tra quyền.

## 2. Dữ liệu và migration

- Local có thể xoá database để sửa thiết kế. Staging và production tuyệt đối không áp dụng cách này.
- Trước staging, tạo baseline migration sạch. Không mang lịch sử migration thử nghiệm hoặc migration chỉ hoạt động
  với database rỗng lên production.
- Mọi migration production phải được thử trên:
  - database sạch;
  - bản sao đã ẩn danh của schema/dữ liệu phiên bản đang chạy;
  - cả đường nâng cấp và phương án rollback/roll-forward.
- Migration lớn dùng chiến lược expand/contract: thêm cột/bảng tương thích trước, deploy code chuyển tiếp, backfill
  theo lô, chuyển traffic rồi mới xoá cấu trúc cũ ở bản phát hành sau.
- Backup PostgreSQL tự động, mã hoá, lưu khác vùng lỗi và phải diễn tập restore. Chỉ có file backup nhưng chưa từng
  restore thử thì chưa được xem là có khả năng khôi phục.
- Xác định retention cho notification, delivery, attempt, callback event và audit log; archive/xoá theo lô để tránh
  bảng và index tăng vô hạn.

## 3. Bảo mật

- Chỉ mở HTTPS; đặt API sau reverse proxy/load balancer và cấu hình trusted proxy chính xác.
- Password dùng thuật toán hash chậm có salt. API key chỉ lưu prefix và hash; raw key chỉ hiển thị một lần.
- Secret phải nằm trong secret manager của môi trường, không nằm trong Git, image, Compose file hoặc biến cấu hình
  được log ra. Có lịch xoay key và quy trình thu hồi khẩn cấp.
- Callback ký HMAC trên bytes chính xác của payload, kèm timestamp/event ID; kiểm tra constant-time và giới hạn cửa
  sổ thời gian để chống replay.
- Push token là dữ liệu nhạy cảm dù không phải API key: mã hoá hoặc hạn chế truy cập, không log và xoá khi provider
  báo token hết hiệu lực.
- Rate limit theo tenant, source device/API key và endpoint. Đặt giới hạn kích thước body, số recipient, độ dài nội
  dung và số biến template trước khi nhận vào database.
- Tách quyền quản trị user/device/sender/template khỏi quyền gửi notification. Audit các thao tác cấp/thu hồi key,
  đổi sender, callback URL và secret.

## 4. Tính đúng khi gửi và retry

- Intake nên nhận `Idempotency-Key` theo source để cùng một yêu cầu gửi lại không tạo notification mới. Ràng buộc
  unique phải nằm trong PostgreSQL, không chỉ kiểm tra ở code.
- Claim job phải có transaction/locking và lease timeout. Worker chết giữa lúc claim và gửi phải được recovery đưa
  về hàng đợi; nhiều worker không được đồng thời sở hữu cùng delivery.
- Có một cửa sổ không thể loại bỏ hoàn toàn: provider đã nhận email nhưng worker chết trước khi lưu `delivered`.
  Recovery có thể gửi lại. Cần ghi rõ cam kết at-least-once và dùng provider idempotency key khi provider hỗ trợ.
- Backoff nên có jitter để tránh hàng nghìn delivery retry cùng lúc. Tách giới hạn retry của delivery và callback.
- Phân loại lỗi theo từng provider: lỗi địa chỉ/credential/payload là vĩnh viễn; timeout, mất mạng, 429 và phần lớn
  5xx là tạm thời. Tôn trọng `Retry-After` khi có.
- Sau khi hết retry, giữ trạng thái và mã lỗi ổn định để tra cứu; không tự động retry vô hạn. Manual retry phải tạo
  audit trail và không sửa mất lịch sử attempt cũ.
- Trạng thái notification là kết quả tổng hợp từ deliveries. Không dùng trạng thái notification để quyết định gửi
  lại từng kênh.

## 5. Template và nội dung

- Template thuộc tenant và nên thuộc source system/device hoặc phạm vi được cấp rõ ràng. Người gửi tham chiếu
  `templateId`/version và truyền data, không được dùng template của tenant khác.
- Version template là bất biến sau khi phát hành. Notification phải lưu version đã render hoặc snapshot cần thiết để
  lịch sử không thay đổi khi template được sửa.
- Escape dữ liệu theo đúng ngữ cảnh HTML/text. Không cho phép biến template thực thi code, truy cập file/network hay
  chèn header email.
- Validate biến bắt buộc, kích thước kết quả render và subject. Có preview/test-send trước khi publish template.
- Plain text và HTML là hai phần nội dung riêng. Email HTML nên có plain-text fallback.

## 6. Callback và tích hợp bên ngoài

- Callback URL được cấu hình trên source device, không nhận URL tuỳ ý trong request gửi notification; cách này giảm
  nguy cơ SSRF.
- Khi cho phép sửa callback URL, chặn loopback, link-local, private network và metadata endpoint theo chính sách;
  kiểm tra lại sau DNS resolution và redirect.
- Callback có timeout ngắn, giới hạn redirect/body và connection pool. Một endpoint chậm không được chiếm hết worker.
- Payload callback không chứa secret hoặc nội dung notification nếu hệ thống nhận không thực sự cần.
- Lưu `eventId`, số attempt và kết quả HTTP. Cảnh báo khi callback backlog hoặc tuổi job vượt ngưỡng.

## 7. SMTP và các kênh

- Gmail phù hợp thử nghiệm, không phải lựa chọn mặc định cho tải production. Dùng nhà cung cấp transactional email có
  quota, bounce/complaint webhook, domain verification và khả năng quan sát.
- Cấu hình SPF, DKIM và DMARC; quản lý bounce, complaint, unsubscribe/suppression để bảo vệ uy tín domain.
- Mỗi channel adapter phải chuẩn hoá kết quả thành success, transient failure hoặc permanent failure nhưng vẫn giữ
  provider message ID và mã lỗi nội bộ để đối soát.
- Đặt concurrency/rate limit riêng cho từng provider và tenant. Có circuit breaker hoặc giảm tốc khi provider lỗi diện
  rộng; không để retry storm làm nghẽn PostgreSQL và outbound network.

## 8. Quan sát và xử lý sự cố

- Log JSON có `correlationId`, `tenantId`, `notificationId`, `deliveryId`, channel và error code; không log payload/secret.
- Metrics tối thiểu: intake rate, delivery success/failure/retry, latency theo channel, queue depth, oldest job age,
  stuck jobs, callback backlog, PostgreSQL pool/latency và provider 429/5xx.
- Cảnh báo dựa trên tỷ lệ và tuổi backlog, không gửi log lỗi liên tục cho từng bản ghi. Gom nhóm incident, chống lặp và
  gửi thông báo phục hồi khi hệ thống bình thường trở lại.
- Exception bất ngờ phải được log một lần tại boundary với stack trace và correlation ID; API trả mã lỗi an toàn,
  không trả stack trace. Lỗi nghiệp vụ dự kiến dùng error code, không dùng exception để điều khiển luồng bình thường.
- Có dashboard, runbook và người chịu trách nhiệm cho: provider down, credential hết hạn, queue tăng, database đầy,
  migration lỗi, callback lỗi hàng loạt và nghi ngờ lộ key.

## 9. Hạ tầng và phát hành

- Dùng cùng một immutable image cho API và Worker; chạy bằng non-root user, filesystem read-only nếu có thể và quét
  dependency/image định kỳ.
- API và Worker scale độc lập. Đặt request/CPU/memory limit, graceful shutdown và thời gian drain đủ để hoàn tất hoặc
  trả lease của job đang xử lý.
- Health check tách liveness và readiness. Provider bên ngoài lỗi không nên luôn làm process restart; readiness chỉ
  phản ánh dependency bắt buộc để instance nhận việc.
- PostgreSQL dùng managed service hoặc HA thực sự, TLS, connection pool có giới hạn và network policy. Redis không
  được trở thành nơi duy nhất giữ trạng thái delivery.
- Triển khai theo thứ tự tương thích: migration mở rộng → API/Worker mới → kiểm tra metrics → migration dọn dẹp ở
  release sau. Canary hoặc rolling deploy; rollback image không được yêu cầu rollback dữ liệu phá huỷ.
- Tách dev/staging/production bằng account, database, credentials và domain gửi riêng. Không dùng dữ liệu người dùng
  production trong môi trường thử nghiệm nếu chưa ẩn danh.

## 10. Cổng kiểm tra trước lần phát hành production đầu tiên

Chỉ phát hành khi toàn bộ mục sau có bằng chứng:

- Unit, architecture, integration và end-to-end test pass trên image sẽ phát hành.
- Test đồng thời nhiều worker, duplicate request, worker crash, provider timeout/429/5xx và callback trùng.
- Load test đạt lưu lượng dự kiến và có headroom; đo cả queue drain time sau sự cố.
- Migration được thử trên dữ liệu gần kích thước thật; backup và restore đã diễn tập.
- Tenant isolation và authorization được security review; dependency/container scan không còn lỗi nghiêm trọng.
- Secret production đã cấp qua secret manager và có người sở hữu quy trình rotation.
- Dashboard, alert, runbook, retention, SLO và on-call đã sẵn sàng.
- Có kế hoạch canary, rollback/roll-forward và tiêu chí dừng phát hành rõ ràng.

## 11. Việc còn thiếu cần ưu tiên trong roadmap hiện tại

Nền tảng delivery đa kênh đã có nhưng chưa nên xem là production-ready. Trước production cần ưu tiên tối thiểu:

1. Rate limit và giới hạn payload/recipient (`INTK-004`).
2. Idempotency cho intake và kiểm thử gửi trùng khi worker chết ở các thời điểm khác nhau.
3. Template versioning, escaping và quyền sở hữu template (`TMPL-002`, `INTK-003`).
4. Monitoring/cảnh báo tổng hợp, retention, backup/restore và runbook (`DLVR-004` cùng hardening vận hành).
5. Provider email production, SPF/DKIM/DMARC và xử lý bounce/complaint.
6. Baseline migration sạch cùng chiến lược nâng cấp dữ liệu trước staging.
7. Security/load/chaos test và diễn tập rollback trước khi nhận traffic thật.

