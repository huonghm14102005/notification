# Bộ tài liệu notification-server

| Tài liệu | Trả lời câu hỏi |
|---|---|
| [PRODUCT.md](PRODUCT.md) | Xây sản phẩm gì, cho ai và phạm vi nào? |
| [TARGET-DESIGN.md](TARGET-DESIGN.md) | Mô hình đích user/device, đa kênh, retry và callback là gì? |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Code .NET, PostgreSQL, Docker và ranh giới module được tổ chức thế nào? |
| [CONVENTIONS.md](CONVENTIONS.md) | Người và AI bắt buộc tuân theo rule nào khi viết code? |
| [SPECS.md](SPECS.md) | API/schema hiện tại đã triển khai là gì? |
| [IMPLEMENTATION-ROADMAP.md](IMPLEMENTATION-ROADMAP.md) | Phát triển feature theo thứ tự nào? |
| [WORKFLOW.md](WORKFLOW.md) | SELECT/APPROVE/VERIFY vận hành ra sao? |
| [PRODUCTION-READINESS.md](PRODUCTION-READINESS.md) | Cần bảo đảm gì trước khi chạy dữ liệu và lưu lượng thật? |
| [features/v1/README.md](features/v1/README.md) | Trạng thái và spec chi tiết của từng feature |

`SPECS.md` và feature `Verified` phản ánh code hiện tại. `TARGET-DESIGN.md` là đích phát triển, không
được coi là đã có trong code. Migration/feature trong roadmap quyết định thời điểm thiết kế đích có
hiệu lực. Feature spec giữ chi tiết nghiệm thu; tài liệu tổng quan không lặp acceptance criteria.
