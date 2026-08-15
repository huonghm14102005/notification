# Identity

Sở hữu tenant, quản trị viên, refresh token và API key. Tenant luôn lấy từ identity đã xác thực,
không lấy từ body/path/query. Thứ tự: `AUTH-001 → AUTH-002 → AUTH-003`.
