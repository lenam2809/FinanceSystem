// Điểm khởi động Blazor Server Application
// Chọn Blazor Server vì yêu cầu real-time SignalR notifications
using FinanceSystem.Blazor.Services;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// Thêm Razor Components với Blazor Server
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// HttpClient kết nối đến API backend
builder.Services.AddHttpClient("FinanceApi", client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5000/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Cookie authentication cho Blazor Server
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/dang-nhap";
        options.LogoutPath = "/dang-xuat";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

builder.Services.AddAuthorization();

// Đăng ký các dịch vụ Blazor
builder.Services.AddScoped<IBlazorAuthService, BlazorAuthService>();
builder.Services.AddScoped<IBlazorTransactionService, BlazorTransactionService>();
builder.Services.AddScoped<IBlazorImportService, BlazorImportService>();
builder.Services.AddScoped<IBlazorCategoryService, BlazorCategoryService>();

// Session để lưu token
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
