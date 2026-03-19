// Lớp cơ sở cho các entity cần theo dõi lịch sử thay đổi
namespace FinanceSystem.Domain.Common;

/// <summary>
/// Lớp cơ sở cung cấp các trường audit (theo dõi ai tạo/sửa và khi nào)
/// Kế thừa lớp này cho các entity cần audit trail
/// </summary>
public abstract class AuditableEntity
{
    // Thời điểm tạo bản ghi
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Người tạo bản ghi (userId)
    public int? CreatedBy { get; set; }

    // Thời điểm cập nhật lần cuối
    public DateTime? UpdatedAt { get; set; }

    // Người cập nhật lần cuối (userId)
    public int? UpdatedBy { get; set; }
}

/// <summary>
/// Kết quả phân trang chung dùng cho tất cả API trả về danh sách
/// </summary>
public class PagedResult<T>
{
    // Danh sách dữ liệu của trang hiện tại
    public List<T> Items { get; set; } = new();

    // Trang hiện tại (bắt đầu từ 1)
    public int Page { get; set; }

    // Số bản ghi mỗi trang
    public int PageSize { get; set; }

    // Tổng số bản ghi
    public int TotalItems { get; set; }

    // Tổng số trang
    public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);

    // Trường sắp xếp
    public string? SortBy { get; set; }

    // Sắp xếp giảm dần
    public bool SortDesc { get; set; }

    // Có trang trước không
    public bool HasPreviousPage => Page > 1;

    // Có trang tiếp theo không
    public bool HasNextPage => Page < TotalPages;
}
