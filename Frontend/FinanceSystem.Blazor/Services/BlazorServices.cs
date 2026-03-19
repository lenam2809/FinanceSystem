// Các dịch vụ Blazor gọi API backend
// Quản lý token trong memory/session
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FinanceSystem.Contracts.V1.Auth;
using FinanceSystem.Contracts.V1.Common;
using FinanceSystem.Contracts.V1.Imports;
using FinanceSystem.Contracts.V1.Transactions;

namespace FinanceSystem.Blazor.Services;

// ─── Auth State ──────────────────────────────────────────

/// <summary>
/// Trạng thái xác thực lưu trong memory (Blazor Server scope)
/// </summary>
public class AuthState
{
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public string? UserName { get; set; }
    public string? UserEmail { get; set; }
    public string? Role { get; set; }
    public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken);
}

// ─── Blazor Auth Service ─────────────────────────────────

public interface IBlazorAuthService
{
    AuthState State { get; }
    Task<bool> LoginAsync(string email, string password);
    Task LogoutAsync();
}

public class BlazorAuthService : IBlazorAuthService
{
    private readonly IHttpClientFactory _httpClientFactory;
    public AuthState State { get; } = new();

    public BlazorAuthService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<bool> LoginAsync(string email, string password)
    {
        var client = _httpClientFactory.CreateClient("FinanceApi");
        var response = await client.PostAsJsonAsync("api/auth/login",
            new LoginRequestDto { Email = email, Password = password });

        if (!response.IsSuccessStatusCode) return false;

        var result = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        if (result == null) return false;

        State.AccessToken = result.AccessToken;
        State.RefreshToken = result.RefreshToken;
        State.UserName = result.UserName;
        State.UserEmail = result.UserEmail;
        State.Role = result.Role;
        return true;
    }

    public async Task LogoutAsync()
    {
        if (!string.IsNullOrEmpty(State.RefreshToken))
        {
            try
            {
                var client = CreateAuthenticatedClient();
                await client.PostAsJsonAsync("api/auth/revoke",
                    new RevokeTokenRequestDto { RefreshToken = State.RefreshToken });
            }
            catch { /* bỏ qua lỗi khi logout */ }
        }

        State.AccessToken = null;
        State.RefreshToken = null;
        State.UserName = null;
        State.UserEmail = null;
        State.Role = null;
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _httpClientFactory.CreateClient("FinanceApi");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", State.AccessToken);
        return client;
    }
}

// ─── Blazor Transaction Service ──────────────────────────

public interface IBlazorTransactionService
{
    Task<PagedResultDto<TransactionDto>?> GetTransactionsAsync(TransactionFilterDto filter);
    Task<TransactionSummaryDto?> GetSummaryAsync(DateTime? from = null, DateTime? to = null);
    Task<TransactionDto?> CreateAsync(CreateTransactionDto dto);
    Task DeleteAsync(int id);
}

public class BlazorTransactionService : IBlazorTransactionService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IBlazorAuthService _authService;

    public BlazorTransactionService(IHttpClientFactory httpClientFactory, IBlazorAuthService authService)
    {
        _httpClientFactory = httpClientFactory;
        _authService = authService;
    }

    public async Task<PagedResultDto<TransactionDto>?> GetTransactionsAsync(TransactionFilterDto filter)
    {
        var client = CreateClient();
        var qs = BuildQueryString(filter);
        var response = await client.GetAsync($"api/transactions?{qs}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PagedResultDto<TransactionDto>>();
    }

    public async Task<TransactionSummaryDto?> GetSummaryAsync(DateTime? from = null, DateTime? to = null)
    {
        var client = CreateClient();
        var qs = string.Empty;
        if (from.HasValue) qs += $"?dateFrom={from.Value:yyyy-MM-dd}";
        if (to.HasValue) qs += (qs.Length > 0 ? "&" : "?") + $"dateTo={to.Value:yyyy-MM-dd}";

        var response = await client.GetAsync($"api/transactions/summary{qs}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TransactionSummaryDto>();
    }

    public async Task<TransactionDto?> CreateAsync(CreateTransactionDto dto)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("api/transactions", dto);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TransactionDto>();
    }

    public async Task DeleteAsync(int id)
    {
        var client = CreateClient();
        var response = await client.DeleteAsync($"api/transactions/{id}");
        response.EnsureSuccessStatusCode();
    }

    private static string BuildQueryString(TransactionFilterDto f)
    {
        var parts = new List<string>
        {
            $"page={f.Page}",
            $"pageSize={f.PageSize}",
            $"sortBy={f.SortBy ?? "date"}",
            $"sortDesc={f.SortDesc}"
        };
        if (f.DateFrom.HasValue) parts.Add($"dateFrom={f.DateFrom.Value:yyyy-MM-dd}");
        if (f.DateTo.HasValue)   parts.Add($"dateTo={f.DateTo.Value:yyyy-MM-dd}");
        if (f.CategoryId.HasValue) parts.Add($"categoryId={f.CategoryId}");
        if (!string.IsNullOrEmpty(f.Type)) parts.Add($"type={f.Type}");
        if (!string.IsNullOrEmpty(f.SearchText)) parts.Add($"searchText={Uri.EscapeDataString(f.SearchText)}");
        return string.Join("&", parts);
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient("FinanceApi");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _authService.State.AccessToken);
        return client;
    }
}

// ─── Blazor Import Service ────────────────────────────────

public interface IBlazorImportService
{
    Task<PagedResultDto<ImportHistoryDto>?> GetHistoryAsync(int page = 1, int pageSize = 10);
    Task<List<ImportErrorDto>?> GetErrorsAsync(int importId);
    Task<ImportResultDto?> UploadAsync(byte[] fileBytes, string fileName);
}

public class BlazorImportService : IBlazorImportService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IBlazorAuthService _authService;

    public BlazorImportService(IHttpClientFactory httpClientFactory, IBlazorAuthService authService)
    {
        _httpClientFactory = httpClientFactory;
        _authService = authService;
    }

    /// <summary>
    /// Upload file Excel lên API để import giao dịch
    /// </summary>
    public async Task<ImportResultDto?> UploadAsync(byte[] fileBytes, string fileName)
    {
        var client = CreateClient();
        using var content     = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue(
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(fileContent, "file", fileName);

        var response = await client.PostAsync("api/imports", content);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ImportResultDto>();
    }

    public async Task<PagedResultDto<ImportHistoryDto>?> GetHistoryAsync(int page = 1, int pageSize = 10)
    {
        var client = CreateClient();
        var response = await client.GetAsync($"api/imports/history?page={page}&pageSize={pageSize}&sortDesc=true");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PagedResultDto<ImportHistoryDto>>();
    }

    public async Task<List<ImportErrorDto>?> GetErrorsAsync(int importId)
    {
        var client = CreateClient();
        var response = await client.GetAsync($"api/imports/{importId}/errors");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ImportErrorDto>>();
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient("FinanceApi");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _authService.State.AccessToken);
        return client;
    }
}

// ─── Blazor Category Service ──────────────────────────────

public interface IBlazorCategoryService
{
    Task<List<CategoryDto>?> GetCategoriesAsync();
}

public class BlazorCategoryService : IBlazorCategoryService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IBlazorAuthService _authService;

    public BlazorCategoryService(IHttpClientFactory httpClientFactory, IBlazorAuthService authService)
    {
        _httpClientFactory = httpClientFactory;
        _authService = authService;
    }

    public async Task<List<CategoryDto>?> GetCategoriesAsync()
    {
        var client = _httpClientFactory.CreateClient("FinanceApi");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _authService.State.AccessToken);
        var response = await client.GetAsync("api/categories");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<CategoryDto>>();
    }
}
