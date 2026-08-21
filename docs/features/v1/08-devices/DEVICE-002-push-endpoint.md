# DEVICE-002 — Push endpoint iOS và Android

Status: Planned
Dependencies: AUTH-004, DEVICE-001, CHAN-001

## Đọc nhanh

User đăng ký thiết bị nhận bằng UUID ổn định. Mỗi device có thể có push endpoint Android/FCM hoặc iOS/APNs; hệ thống
nguồn gửi tới `deviceId`, không gửi trực tiếp raw push token.

## Phạm vi dự kiến

- Device role `recipient` hoặc `both`.
- Đăng ký/rotate/revoke push endpoint theo platform `android` hoặc `ios`.
- Push token mã hóa trong PostgreSQL, không trả trong GET/log/history.
- Target public của push delivery là `deviceId`; server resolve endpoint active lúc tạo/gửi delivery.
- Một user có nhiều device; gửi theo user có thể fan-out tới mọi device active ở feature riêng.
- Token invalid/permanent sẽ disable endpoint; timeout/provider unavailable được retry theo delivery.
- FCM/APNs chỉ là provider, không phải database thứ hai.

## Ngoài phạm vi

- Device đăng ký ẩn danh, pairing code, device attestation và gửi theo topic.

