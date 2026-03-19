// ViewModel cho màn hình import Excel
// Xử lý chọn file, xem trước dữ liệu, ghép cột, và gửi lên API
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinanceSystem.Contracts.V1.Imports;
using FinanceSystem.WPF.Services;
using Microsoft.Win32;
using OfficeOpenXml;
using System.Collections.ObjectModel;
using System.IO;

namespace FinanceSystem.WPF.ViewModels;

/// <summary>
/// Đại diện một dòng dữ liệu xem trước từ file Excel
/// </summary>
public partial class PreviewRow : ObservableObject
{
    public int    RowNumber         { get; set; }
    public string NgayStr           { get; set; } = string.Empty;
    public string SoTienStr         { get; set; } = string.Empty;
    public string DanhMuc           { get; set; } = string.Empty;
    public string MoTa              { get; set; } = string.Empty;
    public string Loai              { get; set; } = string.Empty;  // Income / Expense

    [ObservableProperty] private bool   _isValid           = true;
    [ObservableProperty] private string _validationMessage = string.Empty;
}

/// <summary>
/// ViewModel màn hình import Excel
/// </summary>
public partial class ImportViewModel : ObservableObject
{
    private readonly IImportService  _importService;
    private readonly ITemplateService _templateService;

    [ObservableProperty] private string _selectedFilePath  = string.Empty;
    [ObservableProperty] private string _selectedFileName  = "Chưa chọn file";
    [ObservableProperty] private bool   _isFileSelected    = false;
    [ObservableProperty] private bool   _isImporting       = false;
    [ObservableProperty] private bool   _isDownloading     = false;
    [ObservableProperty] private bool   _hasResult         = false;
    [ObservableProperty] private string _statusMessage     = string.Empty;
    [ObservableProperty] private bool   _isSuccess         = false;
    [ObservableProperty] private ImportResultDto? _importResult;

    public ObservableCollection<PreviewRow>     PreviewRows   { get; } = new();
    public ObservableCollection<ImportErrorDto> ImportErrors  { get; } = new();

    public ImportViewModel(IImportService importService, ITemplateService templateService)
    {
        _importService   = importService;
        _templateService = templateService;
    }

    /// <summary>
    /// Tải file template Excel về máy người dùng
    /// Mở hộp thoại SaveFileDialog để người dùng chọn nơi lưu
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDownload))]
    private async Task DownloadTemplate()
    {
        IsDownloading = true;
        StatusMessage = string.Empty;
        try
        {
            // Hộp thoại chọn nơi lưu file
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title      = "Lưu file template Excel",
                FileName   = $"Template_Import_GiaoDich_{DateTime.Now:yyyyMMdd}.xlsx",
                DefaultExt = ".xlsx",
                Filter     = "Excel Workbook (*.xlsx)|*.xlsx"
            };

            if (dialog.ShowDialog() != true) return;

            StatusMessage = "Đang tải template từ máy chủ...";
            var bytes = await _templateService.DownloadTemplateAsync();
            await File.WriteAllBytesAsync(dialog.FileName, bytes);

            StatusMessage = $"✅ Đã lưu template tại: {dialog.FileName}";

            // Hỏi người dùng có muốn mở file ngay không
            var open = System.Windows.MessageBox.Show(
                "Tải template thành công!\n\nBạn có muốn mở file ngay để điền dữ liệu không?",
                "Tải thành công",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (open == System.Windows.MessageBoxResult.Yes)
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName        = dialog.FileName,
                    UseShellExecute = true   // Mở bằng Excel mặc định
                });
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Lỗi tải template: {ex.Message}";
        }
        finally
        {
            IsDownloading = false;
        }
    }

    private bool CanDownload() => !IsDownloading && !IsImporting;

    /// <summary>
    /// Mở hộp thoại chọn file .xlsx
    /// </summary>
    [RelayCommand]
    private void SelectFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Chọn file Excel để import",
            Filter = "File Excel (*.xlsx)|*.xlsx",
            FilterIndex = 1
        };

        if (dialog.ShowDialog() == true)
        {
            SelectedFilePath = dialog.FileName;
            SelectedFileName = Path.GetFileName(dialog.FileName);
            IsFileSelected = true;
            HasResult = false;
            ImportErrors.Clear();

            // Load xem trước dữ liệu
            LoadPreviewAsync(dialog.FileName);
        }
    }

    /// <summary>
    /// Đọc file Excel để xem trước dữ liệu.
    /// Cấu trúc template mới:
    ///   Dòng 1 : tiêu đề (merge)
    ///   Dòng 2 : cảnh báo (merge)
    ///   Dòng 3 : hint row (Format: dd/MM/yyyy, So duong > 0 ...)
    ///   Dòng 4 : header cột  ← đọc tên cột từ đây
    ///   Dòng 5+: dữ liệu    ← đọc data từ đây
    /// Nếu file không dùng template chuẩn, tự động dò tìm dòng header.
    /// </summary>
    private void LoadPreviewAsync(string filePath)
    {
        PreviewRows.Clear();
        StatusMessage = string.Empty;

        try
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var package = new ExcelPackage(new FileInfo(filePath));

            // Lấy sheet đầu tiên (tên "Giao Dich" hoặc bất kỳ)
            var ws = package.Workbook.Worksheets.FirstOrDefault();
            if (ws == null || ws.Dimension == null)
            {
                StatusMessage = "File Excel không có dữ liệu.";
                return;
            }

            int totalRows = ws.Dimension.Rows;
            int totalCols = ws.Dimension.Columns;

            // ── Xác định dòng header ──────────────────────────
            // Template chuẩn: header ở dòng 4, data từ dòng 5
            // File tự tạo:    header ở dòng 1, data từ dòng 2
            int headerRow = DetectHeaderRow(ws, totalRows);
            int dataStart = headerRow + 1;

            if (dataStart > totalRows)
            {
                StatusMessage = "File không có dòng dữ liệu nào.";
                return;
            }

            // ── Đọc tên cột từ dòng header ───────────────────
            var headers = Enumerable.Range(1, totalCols)
                .Select(col => ws.Cells[headerRow, col].Text.Trim())
                .ToList();

            // Map tên cột → chỉ số (hỗ trợ cả tiếng Việt có dấu, ASCII và tiếng Anh)
            int colNgay    = FindColumnIndex(headers, "Ngay",    "Ngày",    "Date",        "NGAY");
            int colSoTien  = FindColumnIndex(headers, "So tien", "Số tiền", "Amount",      "SO TIEN");
            int colDanhMuc = FindColumnIndex(headers, "Danh muc","Danh mục","Category",    "DANH MUC");
            int colMoTa    = FindColumnIndex(headers, "Mo ta",   "Mô tả",   "Description", "MO TA");
            int colLoai    = FindColumnIndex(headers, "Loai",    "Loại",    "Type",        "LOAI");

            // Kiểm tra có đủ cột bắt buộc không
            var missingCols = new List<string>();
            if (colNgay    < 0) missingCols.Add("Ngay");
            if (colSoTien  < 0) missingCols.Add("So tien");
            if (colDanhMuc < 0) missingCols.Add("Danh muc");
            if (missingCols.Any())
            {
                StatusMessage = $"Không tìm thấy cột: {string.Join(", ", missingCols)}. " +
                                $"Hãy dùng template chuẩn hoặc đặt đúng tên cột.";
                return;
            }

            // ── Đọc tối đa 20 dòng dữ liệu ──────────────────
            int maxPreviewRow = Math.Min(totalRows, dataStart + 19);

            for (int row = dataStart; row <= maxPreviewRow; row++)
            {
                // Bỏ qua dòng hoàn toàn trống
                var allEmpty = Enumerable.Range(1, totalCols)
                    .All(c => string.IsNullOrWhiteSpace(ws.Cells[row, c].Text));
                if (allEmpty) continue;

                var ngay    = colNgay    >= 0 ? ws.Cells[row, colNgay    + 1].Text.Trim() : string.Empty;
                var soTien  = colSoTien  >= 0 ? ws.Cells[row, colSoTien  + 1].Text.Trim() : string.Empty;
                var danhMuc = colDanhMuc >= 0 ? ws.Cells[row, colDanhMuc + 1].Text.Trim() : string.Empty;
                var moTa    = colMoTa    >= 0 ? ws.Cells[row, colMoTa    + 1].Text.Trim() : string.Empty;
                var loai    = colLoai    >= 0 ? ws.Cells[row, colLoai    + 1].Text.Trim() : string.Empty;

                // ── Validate sơ bộ client-side ────────────────
                var errors = new List<string>();

                // Validate ngày
                if (string.IsNullOrWhiteSpace(ngay))
                    errors.Add("Thiếu ngày");
                else if (!TryParseDate(ngay, out var parsedDate))
                    errors.Add("Ngày không đúng định dạng (dd/MM/yyyy)");
                else if (parsedDate.Date > DateTime.Today)
                    errors.Add("Ngày không được là ngày tương lai");

                // Validate số tiền — xử lý cả dấu chấm và phẩy phân cách
                var soTienClean = soTien.Replace(",", "").Replace(" ", "");
                if (string.IsNullOrWhiteSpace(soTienClean))
                    errors.Add("Thiếu số tiền");
                else if (!decimal.TryParse(soTienClean,
                             System.Globalization.NumberStyles.Any,
                             System.Globalization.CultureInfo.InvariantCulture,
                             out var amount))
                    errors.Add("Số tiền không phải là số");
                else if (amount <= 0)
                    errors.Add("Số tiền phải > 0");

                // Validate danh mục
                if (string.IsNullOrWhiteSpace(danhMuc))
                    errors.Add("Thiếu danh mục");

                // Validate loại (nếu có)
                if (!string.IsNullOrWhiteSpace(loai)
                    && loai != "Income" && loai != "Expense")
                    errors.Add($"Loại '{loai}' không hợp lệ (Income/Expense)");

                PreviewRows.Add(new PreviewRow
                {
                    RowNumber         = row,
                    NgayStr           = ngay,
                    SoTienStr         = soTien,
                    DanhMuc           = danhMuc,
                    MoTa              = moTa,
                    Loai              = loai,
                    IsValid           = !errors.Any(),
                    ValidationMessage = errors.Any()
                                        ? string.Join(" | ", errors)
                                        : "OK"
                });
            }

            // Thống kê nhanh
            int okCount  = PreviewRows.Count(r => r.IsValid);
            int errCount = PreviewRows.Count(r => !r.IsValid);
            StatusMessage = PreviewRows.Count == 0
                ? "Không tìm thấy dòng dữ liệu nào (dữ liệu bắt đầu từ dòng 5)."
                : $"Xem trước: {PreviewRows.Count} dòng — {okCount} hợp lệ, {errCount} có lỗi.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Không thể đọc file: {ex.Message}";
        }
    }

    // ── Helpers ──────────────────────────────────────────────

    /// <summary>
    /// Tự động dò tìm dòng header.
    /// Ưu tiên dòng 4 (template chuẩn), fallback về dòng 1 (file tự tạo).
    /// Cách nhận biết: dòng header chứa ít nhất 2 từ khóa cột quen thuộc.
    /// </summary>
    private static int DetectHeaderRow(ExcelWorksheet ws, int totalRows)
    {
        // Các từ khóa nhận dạng dòng header (case-insensitive)
        var keywords = new[] { "ngay", "so tien", "danh muc", "mo ta", "loai",
                               "date", "amount", "category", "description", "type" };

        // Kiểm tra dòng 4 trước (template chuẩn)
        for (int checkRow = 4; checkRow >= 1; checkRow--)
        {
            if (checkRow > totalRows) continue;
            var rowText = Enumerable.Range(1, Math.Min(ws.Dimension.Columns, 10))
                .Select(c => ws.Cells[checkRow, c].Text.ToLower().Trim())
                .ToList();

            int matches = keywords.Count(kw =>
                rowText.Any(cell => cell.Contains(kw)));

            if (matches >= 2)
                return checkRow; // Tìm thấy header
        }

        // Fallback: dòng 1
        return 1;
    }

    /// <summary>
    /// Tìm chỉ số cột (0-based) trong danh sách header, khớp bất kỳ tên nào trong aliases.
    /// So sánh case-insensitive và trim.
    /// </summary>
    private static int FindColumnIndex(List<string> headers, params string[] aliases)
    {
        for (int i = 0; i < headers.Count; i++)
        {
            var h = headers[i].ToLower().Trim();
            foreach (var alias in aliases)
            {
                if (h == alias.ToLower().Trim())
                    return i;
            }
        }
        return -1; // Không tìm thấy
    }

    /// <summary>
    /// Parse ngày theo nhiều định dạng: dd/MM/yyyy, yyyy-MM-dd, d/M/yyyy
    /// </summary>
    private static bool TryParseDate(string text, out DateTime result)
    {
        var formats = new[]
        {
            "dd/MM/yyyy", "d/M/yyyy", "d/MM/yyyy", "dd/M/yyyy",
            "yyyy-MM-dd", "MM/dd/yyyy", "dd-MM-yyyy"
        };
        return DateTime.TryParseExact(text, formats,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out result);
    }
    [RelayCommand(CanExecute = nameof(CanImport))]
    private async Task Import()    // CommunityToolkit tạo ImportCommand (không phải ImportAsyncCommand)
    {
        IsImporting = true;
        HasResult = false;
        StatusMessage = "Đang gửi file lên máy chủ...";

        try
        {
            var result = await _importService.ImportFileAsync(SelectedFilePath);
            ImportResult = result;
            IsSuccess = result?.ErrorCount == 0;
            HasResult = true;
            StatusMessage = result?.Message ?? "Import hoàn thành.";

            // Nếu có lỗi, tải danh sách lỗi
            if (result?.ErrorCount > 0)
                await LoadErrorsAsync(result.ImportId);
        }
        catch (Exception ex)
        {
            IsSuccess = false;
            HasResult = true;
            StatusMessage = $"Import thất bại: {ex.Message}";
        }
        finally
        {
            IsImporting = false;
        }
    }

    private bool CanImport() => IsFileSelected && !IsImporting;

    private async Task LoadErrorsAsync(int importId)
    {
        ImportErrors.Clear();
        var errors = await _importService.GetErrorsAsync(importId);
        if (errors != null)
            foreach (var err in errors)
                ImportErrors.Add(err);
    }

}
