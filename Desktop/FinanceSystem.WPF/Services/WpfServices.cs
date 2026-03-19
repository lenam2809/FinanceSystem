// Các service giao tiếp với API backend qua HttpClient
// Interfaces và implementations cho WPF Admin Tool
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.IO;
using System;
using FinanceSystem.Contracts.V1.Auth;
using FinanceSystem.Contracts.V1.Common;
using FinanceSystem.Contracts.V1.Imports;
using FinanceSystem.Contracts.V1.Transactions;

namespace FinanceSystem.WPF.Services;

// ─────────────────────────────────────────────
// AUTH SERVICE
// ─────────────────────────────────────────────

/// <summary>
/// Interface dịch vụ xác thực
/// </summary>
public interface IAuthService
{
    Task<AuthResponseDto?> LoginAsync(string email, string password);
    Task LogoutAsync();
    string? AccessToken { get; }
    string? RefreshToken { get; }
    bool IsLoggedIn { get; }
    string? Role { get; }      // MainViewModel dùng Role, không phải UserRole
    string? UserName { get; }
}

/// <summary>
/// Triển khai dịch vụ xác thực - gọi API /auth/login và /auth/revoke
/// </summary>
public class AuthService : IAuthService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public bool IsLoggedIn => !string.IsNullOrEmpty(AccessToken);
    public string? Role { get; private set; }       // đổi từ UserRole → Role
    public string? UserName { get; private set; }

    public AuthService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Đăng nhập và lưu token vào memory
    /// </summary>
    public async Task<AuthResponseDto?> LoginAsync(string email, string password)
    {
        var client = _httpClientFactory.CreateClient("FinanceApi");
        var request = new LoginRequestDto { Email = email, Password = password };

        var response = await client.PostAsJsonAsync("api/auth/login", request);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception(ParseErrorMessage(errorContent));
        }

        var result = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        if (result != null)
        {
            AccessToken = result.AccessToken;
            RefreshToken = result.RefreshToken;
            Role = result.Role;          // đổi từ UserRole → Role
            UserName = result.UserName;
        }

        return result;
    }

    /// <summary>
    /// Đăng xuất - thu hồi refresh token
    /// </summary>
    public async Task LogoutAsync()
    {
        if (string.IsNullOrEmpty(RefreshToken)) return;

        try
        {
            var client = CreateAuthenticatedClient();
            await client.PostAsJsonAsync("api/auth/revoke", new RevokeTokenRequestDto { RefreshToken = RefreshToken! });
        }
        finally
        {
            AccessToken = null;
            RefreshToken = null;
            Role = null;            // đổi từ UserRole → Role
            UserName = null;
        }
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _httpClientFactory.CreateClient("FinanceApi");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", AccessToken);
        return client;
    }

    private static string ParseErrorMessage(string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("detail", out var detail))
                return detail.GetString() ?? "Đã xảy ra lỗi không xác định.";
        }
        catch { }
        return "Không thể kết nối đến máy chủ. Vui lòng kiểm tra kết nối mạng.";
    }
}

// ─────────────────────────────────────────────
// TEMPLATE SERVICE
// ─────────────────────────────────────────────

/// <summary>
/// Interface tải file Excel template từ API
/// </summary>
public interface ITemplateService
{
    Task<byte[]> DownloadTemplateAsync();
}

/// <summary>
/// Gọi GET /api/imports/template và trả về mảng byte file .xlsx
/// </summary>
public class TemplateService : ITemplateService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAuthService       _authService;

    public TemplateService(IHttpClientFactory httpClientFactory, IAuthService authService)
    {
        _httpClientFactory = httpClientFactory;
        _authService       = authService;
    }

    public async Task<byte[]> DownloadTemplateAsync()
    {
        var client = _httpClientFactory.CreateClient("FinanceApi");
        // Gắn token nếu đã đăng nhập (endpoint AllowAnonymous nên không bắt buộc)
        if (!string.IsNullOrEmpty(_authService.AccessToken))
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authService.AccessToken);

        var response = await client.GetAsync("api/imports/template");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync();
    }
}

// ─────────────────────────────────────────────
// IMPORT SERVICE
// ─────────────────────────────────────────────

/// <summary>
/// Interface dịch vụ import file Excel
/// </summary>
public interface IImportService
{
    Task<ImportResultDto?> ImportFileAsync(string filePath);
    Task<PagedResultDto<ImportHistoryDto>?> GetHistoryAsync(int page = 1, int pageSize = 10);
    Task<List<ImportErrorDto>?> GetErrorsAsync(int importId);
}

/// <summary>
/// Triển khai dịch vụ import - upload file và xem lịch sử
/// </summary>
public class ImportService : IImportService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAuthService _authService;

    public ImportService(IHttpClientFactory httpClientFactory, IAuthService authService)
    {
        _httpClientFactory = httpClientFactory;
        _authService = authService;
    }

    /// <summary>
    /// Upload file Excel lên API để xử lý
    /// </summary>
    public async Task<ImportResultDto?> ImportFileAsync(string filePath)
    {
        var client = CreateAuthenticatedClient();

        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var content = new MultipartFormDataContent();
        using var fileContent = new StreamContent(fileStream);

        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(fileContent, "file", Path.GetFileName(filePath));

        var response = await client.PostAsync("api/imports", content);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception(ParseErrorMessage(errorContent));
        }

        return await response.Content.ReadFromJsonAsync<ImportResultDto>();
    }

    /// <summary>
    /// Lấy lịch sử import có phân trang
    /// </summary>
    public async Task<PagedResultDto<ImportHistoryDto>?> GetHistoryAsync(int page = 1, int pageSize = 10)
    {
        var client = CreateAuthenticatedClient();
        var response = await client.GetAsync($"api/imports/history?page={page}&pageSize={pageSize}&sortDesc=true");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PagedResultDto<ImportHistoryDto>>();
    }

    /// <summary>
    /// Lấy danh sách lỗi chi tiết của một lần import
    /// </summary>
    public async Task<List<ImportErrorDto>?> GetErrorsAsync(int importId)
    {
        var client = CreateAuthenticatedClient();
        var response = await client.GetAsync($"api/imports/{importId}/errors");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ImportErrorDto>>();
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _httpClientFactory.CreateClient("FinanceApi");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _authService.AccessToken);
        return client;
    }

    private static string ParseErrorMessage(string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("detail", out var detail))
                return detail.GetString() ?? "Lỗi không xác định.";
        }
        catch { }
        return "Lỗi kết nối máy chủ.";
    }
}

// ─────────────────────────────────────────────
// TRANSACTION SERVICE
// ─────────────────────────────────────────────

/// <summary>
/// Interface dịch vụ quản lý giao dịch
/// </summary>
public interface ITransactionService
{
    Task<PagedResultDto<TransactionDto>?> GetTransactionsAsync(TransactionFilterDto filter);
    Task<TransactionSummaryDto?> GetSummaryAsync();
}

/// <summary>
/// Triển khai dịch vụ giao dịch
/// </summary>
public class TransactionService : ITransactionService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAuthService _authService;

    public TransactionService(IHttpClientFactory httpClientFactory, IAuthService authService)
    {
        _httpClientFactory = httpClientFactory;
        _authService = authService;
    }

    public async Task<PagedResultDto<TransactionDto>?> GetTransactionsAsync(TransactionFilterDto filter)
    {
        var client = CreateAuthenticatedClient();
        var qs = $"page={filter.Page}&pageSize={filter.PageSize}&sortBy={filter.SortBy}&sortDesc={filter.SortDesc}";
        if (filter.CategoryId.HasValue) qs += $"&categoryId={filter.CategoryId}";
        if (!string.IsNullOrEmpty(filter.Type)) qs += $"&type={filter.Type}";

        var response = await client.GetAsync($"api/transactions?{qs}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PagedResultDto<TransactionDto>>();
    }

    public async Task<TransactionSummaryDto?> GetSummaryAsync()
    {
        var client = CreateAuthenticatedClient();
        var response = await client.GetAsync("api/transactions/summary");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TransactionSummaryDto>();
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _httpClientFactory.CreateClient("FinanceApi");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _authService.AccessToken);
        return client;
    }
}
