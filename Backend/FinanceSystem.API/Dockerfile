# Multi-stage build để tối ưu kích thước image cuối
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution và csproj files trước để tận dụng Docker cache
COPY FinanceSystem.sln ./
COPY Backend/FinanceSystem.API/FinanceSystem.API.csproj Backend/FinanceSystem.API/
COPY Backend/FinanceSystem.Application/FinanceSystem.Application.csproj Backend/FinanceSystem.Application/
COPY Backend/FinanceSystem.Domain/FinanceSystem.Domain.csproj Backend/FinanceSystem.Domain/
COPY Backend/FinanceSystem.Infrastructure/FinanceSystem.Infrastructure.csproj Backend/FinanceSystem.Infrastructure/
COPY Shared/FinanceSystem.Contracts/FinanceSystem.Contracts.csproj Shared/FinanceSystem.Contracts/

# Restore packages
RUN dotnet restore Backend/FinanceSystem.API/FinanceSystem.API.csproj

# Copy toàn bộ source code
COPY . .

# Build và publish
RUN dotnet publish Backend/FinanceSystem.API/FinanceSystem.API.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Stage 2: Runtime (image nhỏ hơn, không có SDK)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Tạo thư mục logs
RUN mkdir -p /app/logs

# Copy published output từ stage build
COPY --from=build /app/publish .

# Expose port
EXPOSE 80
EXPOSE 443

# Chạy ứng dụng
ENTRYPOINT ["dotnet", "FinanceSystem.API.dll"]
