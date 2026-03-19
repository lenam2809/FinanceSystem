# 💰 Personal Finance Ecosystem

Hệ thống quản lý tài chính cá nhân – ASP.NET Core API + WPF Admin Tool + Blazor Dashboard.

## 🏗️ Kiến Trúc

```
Backend/
  FinanceSystem.API          → ASP.NET Core 8 Web API
  FinanceSystem.Application  → CQRS (MediatR), FluentValidation
  FinanceSystem.Domain       → Entities, Exceptions
  FinanceSystem.Infrastructure → EF Core + PostgreSQL, JWT, Excel
Frontend/
  FinanceSystem.Blazor       → Blazor Server Dashboard
Desktop/
  FinanceSystem.WPF          → WPF Admin Tool (MVVM / CommunityToolkit)
Shared/
  FinanceSystem.Contracts    → DTOs dùng chung (V1 namespace)
Tests/
  FinanceSystem.Tests        → xUnit + Moq + FluentAssertions
```

## 🚀 Chạy Với Docker

```bash
docker-compose up --build
```

- API: http://localhost:5000
- Swagger: http://localhost:5000/swagger

## 🔑 Tài Khoản Mẫu

| Vai trò | Email | Mật khẩu |
|---|---|---|
| Admin | admin@finance.com | Admin@123 |
| User  | user@finance.com  | User@123  |

## 🛠️ Công Nghệ

.NET 8, EF Core, PostgreSQL, MediatR, FluentValidation, JWT + Refresh Token, Serilog, EPPlus, SignalR, Hangfire, xUnit, Moq, CommunityToolkit.Mvvm, Blazor Server, Docker

## 📊 Format File Excel Import

| Cột | Bắt buộc | Ghi chú |
|---|---|---|
| Ngày | ✅ | dd/MM/yyyy, không tương lai |
| Số tiền | ✅ | > 0, tối đa 2 chữ số thập phân |
| Danh mục | ✅ | Phải tồn tại trong DB |
| Mô tả | ❌ | Tùy chọn |

## 🧪 Chạy Unit Tests

```bash
cd Tests/FinanceSystem.Tests
dotnet test
```

## 📡 API Chính

```
POST /api/auth/login          Đăng nhập
POST /api/auth/refresh         Làm mới token
POST /api/auth/revoke          Đăng xuất
GET  /api/transactions         Danh sách giao dịch (phân trang)
GET  /api/transactions/summary Tổng thu/chi
POST /api/imports              Upload file Excel
GET  /api/imports/history      Lịch sử import
GET  /api/imports/{id}/errors  Lỗi chi tiết
```

## 🔒 Bảo Mật

- JWT Access Token: hết hạn sau 15 phút
- Refresh Token: 7 ngày, token rotation, phát hiện reuse attack
- Rate Limiting: 5 req/phút cho auth, 100 req/phút cho API chung
- Toàn bộ error messages trả về bằng tiếng Việt
