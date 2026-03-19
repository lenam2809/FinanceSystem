// Dịch vụ tạo file Excel template - phiên bản đã sửa lỗi XML
// Bỏ AddComment (bug EPPlus 7 với AutoFit + Unicode) - dùng row hướng dẫn thay thế
using FinanceSystem.Application.Common.Interfaces;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;

namespace FinanceSystem.Infrastructure.Services;

public class ExcelTemplateService : IExcelTemplateService
{
    public ExcelTemplateService()
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
    }

    public byte[] GenerateImportTemplate(IEnumerable<string> categoryNames)
    {
        using var package = new ExcelPackage();
        var catList = categoryNames.ToList();

        BuildDataSheet(package, catList);
        BuildCategorySheet(package, catList);
        BuildGuideSheet(package);

        package.Workbook.Worksheets[0].View.TabSelected = true;
        return package.GetAsByteArray();
    }

    // ─────────────────────────────────────────────────────────
    // Sheet 1: Giao Dich (tên ASCII để tránh encoding issues)
    // ─────────────────────────────────────────────────────────
    private static void BuildDataSheet(ExcelPackage package, List<string> catList)
    {
        // Dùng tên ASCII thuần để tránh bất kỳ vấn đề encoding nào
        var ws = package.Workbook.Worksheets.Add("Giao Dich");

        var colBlue   = Color.FromArgb(37,  99,  235);
        var colYellow = Color.FromArgb(254, 243, 199);
        var colBlueLight = Color.FromArgb(219, 234, 254);
        var colGreen  = Color.FromArgb(240, 253, 244);
        var colGray   = Color.FromArgb(249, 250, 251);
        var colBorder = Color.FromArgb(209, 213, 219);

        // Dòng 1: tiêu đề
        ws.Cells[1, 1, 1, 5].Merge = true;
        ws.Cells[1, 1].Value = "TEMPLATE IMPORT GIAO DICH TAI CHINH";
        StyleTitle(ws.Cells[1, 1], colBlue, 13, true);
        ws.Row(1).Height = 26;

        // Dòng 2: cảnh báo (chỉ ASCII + số, tránh tiếng Việt có dấu trong merged cell)
        ws.Cells[2, 1, 2, 5].Merge = true;
        ws.Cells[2, 1].Value = "LUU Y: Dien du lieu tu dong 5. Khong xoa dong 1-4. Ngay <= hom nay. So tien > 0.";
        ws.Cells[2, 1].Style.Font.Italic = true;
        ws.Cells[2, 1].Style.Font.Size   = 9;
        ws.Cells[2, 1].Style.Font.Color.SetColor(Color.FromArgb(146, 64, 14));
        ws.Cells[2, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
        ws.Cells[2, 1].Style.Fill.BackgroundColor.SetColor(colYellow);
        ws.Row(2).Height = 18;

        // Dòng 3: hướng dẫn cột (thay thế cho AddComment)
        var hints = new[] { "Format: dd/MM/yyyy", "So duong > 0", "Chon tu danh sach", "Ghi chu (tuy chon)", "Income / Expense" };
        for (int c = 1; c <= 5; c++)
        {
            ws.Cells[3, c].Value = hints[c - 1];
            ws.Cells[3, c].Style.Font.Size   = 8;
            ws.Cells[3, c].Style.Font.Italic = true;
            ws.Cells[3, c].Style.Font.Color.SetColor(Color.FromArgb(107, 114, 128));
            ws.Cells[3, c].Style.Fill.PatternType = ExcelFillStyle.Solid;
            ws.Cells[3, c].Style.Fill.BackgroundColor.SetColor(colGray);
        }
        ws.Row(3).Height = 16;

        // Dòng 4: header cột
        var headers = new[] { "Ngay", "So tien", "Danh muc", "Mo ta", "Loai" };
        var widths  = new[] { 18.0, 18.0, 22.0, 34.0, 16.0 };
        for (int c = 1; c <= 5; c++)
        {
            ws.Cells[4, c].Value = headers[c - 1];
            ws.Cells[4, c].Style.Font.Bold = true;
            ws.Cells[4, c].Style.Font.Size = 11;
            ws.Cells[4, c].Style.Font.Color.SetColor(Color.White);
            ws.Cells[4, c].Style.Fill.PatternType = ExcelFillStyle.Solid;
            ws.Cells[4, c].Style.Fill.BackgroundColor.SetColor(colBlue);
            ws.Cells[4, c].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            ws.Cells[4, c].Style.VerticalAlignment   = ExcelVerticalAlignment.Center;
            ws.Column(c).Width = widths[c - 1];
        }
        ws.Row(4).Height = 24;

        // Dòng 5-9: dữ liệu mẫu (dùng ASCII cho tên để tránh encoding)
        var samples = new[]
        {
            ("15/01/2024", "150000",  catList.Count > 0 ? catList[0] : "An uong",   "Bua trua van phong",  "Expense"),
            ("16/01/2024", "5000000", catList.Count > 2 ? catList[2] : "Luong",      "Luong thang 1/2024",  "Income"),
            ("17/01/2024", "80000",   catList.Count > 1 ? catList[1] : "Di chuyen",  "Grab di lam",         "Expense"),
            ("18/01/2024", "250000",  catList.Count > 3 ? catList[3] : "Mua sam",    "Mua sach",            "Expense"),
            ("20/01/2024", "1200000", catList.Count > 4 ? catList[4] : "Giai tri",   "Ve concert",          "Expense"),
        };

        for (int i = 0; i < samples.Length; i++)
        {
            int row = i + 5;
            var (ngay, soTien, danhMuc, moTa, loai) = samples[i];
            ws.Cells[row, 1].Value = ngay;
            ws.Cells[row, 2].Value = soTien;
            ws.Cells[row, 3].Value = danhMuc;
            ws.Cells[row, 4].Value = moTa;
            ws.Cells[row, 5].Value = loai;

            var bg = i % 2 == 0 ? colBlueLight : Color.White;
            ws.Cells[row, 1, row, 5].Style.Fill.PatternType = ExcelFillStyle.Solid;
            ws.Cells[row, 1, row, 5].Style.Fill.BackgroundColor.SetColor(bg);

            ws.Cells[row, 5].Style.Font.Bold = true;
            ws.Cells[row, 5].Style.Font.Color.SetColor(
                loai == "Income"
                    ? Color.FromArgb(21, 128, 61)
                    : Color.FromArgb(185, 28, 28));
        }

        // Border dòng 4-100
        ApplyBorder(ws.Cells[4, 1, 100, 5], colBorder);

        // Format số tiền
        ws.Cells[5, 2, 100, 2].Style.Numberformat.Format   = "#,##0.##";
        ws.Cells[5, 2, 100, 2].Style.HorizontalAlignment   = ExcelHorizontalAlignment.Right;

        // Dropdown danh mục — chỉ dùng khi catList không rỗng
        if (catList.Any())
        {
            // Dùng Formula1 string thay vì .Values.Add để tránh lỗi XML với Unicode
            // Format: "\"Cat1,Cat2,Cat3\"" — an toàn hơn với EPPlus 7
            var catCsv = string.Join(",", catList.Select(SanitizeForFormula));
            var catVal = ws.DataValidations.AddListValidation("C5:C100");
            catVal.ShowErrorMessage  = false; // tắt error popup để tránh lỗi XML
            catVal.ShowInputMessage  = false;
            catVal.Formula.Values.Clear();
            // Fallback: dùng formula string trực tiếp
            catVal.Formula.ExcelFormula = $"\"{catCsv}\"";
        }

        // Dropdown loại
        var typeVal = ws.DataValidations.AddListValidation("E5:E100");
        typeVal.ShowErrorMessage = false;
        typeVal.ShowInputMessage = false;
        typeVal.Formula.ExcelFormula = "\"Income,Expense\"";

        // Freeze header
        ws.View.FreezePanes(5, 1);

        // AutoFilter
        ws.Cells[4, 1, 4, 5].AutoFilter = true;
    }

    // ─────────────────────────────────────────────────────────
    // Sheet 2: DanhMuc
    // ─────────────────────────────────────────────────────────
    private static void BuildCategorySheet(ExcelPackage package, List<string> catList)
    {
        var ws = package.Workbook.Worksheets.Add("DanhMuc");
        var colBlue  = Color.FromArgb(37,  99,  235);
        var colLight = Color.FromArgb(239, 246, 255);

        ws.Cells[1, 1].Value = "Ten danh muc";
        ws.Cells[1, 2].Value = "Mo ta";
        foreach (var cell in new[] { ws.Cells[1, 1], ws.Cells[1, 2] })
        {
            cell.Style.Font.Bold = true;
            cell.Style.Font.Color.SetColor(Color.White);
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(colBlue);
        }
        ws.Column(1).Width = 25;
        ws.Column(2).Width = 40;

        // Ghi tên danh mục — đây là dữ liệu từ DB, có thể có tiếng Việt
        // Lưu nguyên vì đây là cell value (không phải XML attribute)
        for (int i = 0; i < catList.Count; i++)
        {
            int row = i + 2;
            ws.Cells[row, 1].Value = catList[i];
            ws.Cells[row, 2].Value = GetCategoryNote(catList[i]);
            if (i % 2 == 0)
            {
                ws.Cells[row, 1, row, 2].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[row, 1, row, 2].Style.Fill.BackgroundColor.SetColor(colLight);
            }
        }
    }

    // ─────────────────────────────────────────────────────────
    // Sheet 3: HuongDan
    // ─────────────────────────────────────────────────────────
    private static void BuildGuideSheet(ExcelPackage package)
    {
        var ws = package.Workbook.Worksheets.Add("HuongDan");
        ws.Column(1).Width = 80;

        var colBlue = Color.FromArgb(37,  99,  235);
        var colRed  = Color.FromArgb(185, 28,  28);

        // Chỉ dùng ASCII trong sheet này để đảm bảo 100% không lỗi XML
        var lines = new[]
        {
            ("HUONG DAN SU DUNG TEMPLATE IMPORT",                             true,  13, colBlue),
            ("",                                                               false, 11, Color.Black),
            ("1. DIEN DU LIEU",                                               true,  12, colBlue),
            ("   - Mo sheet 'Giao Dich'",                                     false, 11, Color.Black),
            ("   - Dien du lieu tu dong 5 tro di (khong sua dong 1-4)",       false, 11, Color.Black),
            ("   - Cac dong mau xanh la du lieu mau - co the xoa",           false, 11, Color.Black),
            ("",                                                               false, 11, Color.Black),
            ("2. QUY TAC NHAP LIEU",                                          true,  12, colBlue),
            ("   - Ngay:     dd/MM/yyyy, khong duoc la ngay tuong lai",       false, 11, Color.Black),
            ("   - So tien:  So duong > 0, toi da 2 chu so thap phan",        false, 11, Color.Black),
            ("   - Danh muc: Chon tu dropdown hoac xem sheet 'DanhMuc'",      false, 11, Color.Black),
            ("   - Mo ta:    Tuy chon, co the de trong",                      false, 11, Color.Black),
            ("   - Loai:     Income (thu nhap) hoac Expense (chi tieu)",      false, 11, Color.Black),
            ("",                                                               false, 11, Color.Black),
            ("3. IMPORT FILE",                                                 true,  12, colBlue),
            ("   - Luu file dinh dang .xlsx",                                 false, 11, Color.Black),
            ("   - Mo Admin Tool > tab 'Import Excel' > Chon file > Import",  false, 11, Color.Black),
            ("   - Hoac dung Blazor Web > menu 'Import Du Lieu'",             false, 11, Color.Black),
            ("",                                                               false, 11, Color.Black),
            ("4. LOI THUONG GAP",                                             true,  12, colRed),
            ("   - 'Danh muc khong ton tai': kiem tra lai ten voi sheet DanhMuc", false, 11, Color.Black),
            ("   - 'So tien khong hop le': dam bao la so, khong co ky tu dac biet", false, 11, Color.Black),
            ("   - 'Ngay khong hop le': dung dinh dang dd/MM/yyyy",          false, 11, Color.Black),
        };

        for (int i = 0; i < lines.Length; i++)
        {
            var (text, bold, size, color) = lines[i];
            var cell = ws.Cells[i + 1, 1];
            cell.Value = text;
            cell.Style.Font.Bold = bold;
            cell.Style.Font.Size = size;
            cell.Style.Font.Color.SetColor(color);
            ws.Row(i + 1).Height = 18;
        }
    }

    // ─────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────
    private static void StyleTitle(ExcelRange cell, Color color, int size, bool bold)
    {
        cell.Style.Font.Bold = bold;
        cell.Style.Font.Size = size;
        cell.Style.Font.Color.SetColor(color);
        cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
    }

    private static void ApplyBorder(ExcelRange range, Color color)
    {
        range.Style.Border.Top.Style    = ExcelBorderStyle.Thin;
        range.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
        range.Style.Border.Left.Style   = ExcelBorderStyle.Thin;
        range.Style.Border.Right.Style  = ExcelBorderStyle.Thin;
        range.Style.Border.Top.Color.SetColor(color);
        range.Style.Border.Bottom.Color.SetColor(color);
        range.Style.Border.Left.Color.SetColor(color);
        range.Style.Border.Right.Color.SetColor(color);
    }

    // Loại bỏ dấu phẩy và dấu nháy kép trong tên danh mục
    // vì chúng làm hỏng chuỗi formula "Cat1,Cat2"
    private static string SanitizeForFormula(string s)
        => s.Replace(",", " ").Replace("\"", "'").Trim();

    private static string GetCategoryNote(string name) => name switch
    {
        _ when name.Contains("n u") || name.ToLower().Contains("an uong")   => "Bua an, ca phe, do uong",
        _ when name.ToLower().Contains("di chuyen")                          => "Xang xe, taxi, xe om",
        _ when name.ToLower().Contains("luong")                              => "Thu nhap tu luong (Income)",
        _ when name.ToLower().Contains("mua sam")                            => "Quan ao, do dung",
        _ when name.ToLower().Contains("giai tri")                           => "Xem phim, du lich",
        _ when name.ToLower().Contains("y te")                               => "Thuoc, kham benh",
        _ when name.ToLower().Contains("giao duc")                           => "Hoc phi, sach vo",
        _ when name.ToLower().Contains("tiet kiem")                          => "Gui tiet kiem, dau tu",
        _                                                                     => ""
    };
}
