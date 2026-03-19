// Các Value Converter dùng trong XAML binding
// Tất cả converter được đăng ký trong App.xaml Resources
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FinanceSystem.WPF.Helpers;

/// <summary>
/// Chuyển bool → Visibility (true = Visible, false = Collapsed)
/// </summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Visible;
}

/// <summary>
/// Đảo ngược bool (true → false, false → true)
/// Dùng để disable control khi IsLoading = true
/// </summary>
[ValueConversion(typeof(bool), typeof(bool))]
public class InvertBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && !b;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && !b;
}

/// <summary>
/// Chuyển bool → text loading/normal
/// ConverterParameter format: "TextKhiTrue|TextKhiFalse"
/// Ví dụ: ConverterParameter='Đang tải...|Xác Nhận'
/// </summary>
[ValueConversion(typeof(bool), typeof(string))]
public class LoadingTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var parts = parameter?.ToString()?.Split('|');
        if (parts?.Length != 2) return value?.ToString() ?? string.Empty;
        return value is true ? parts[0] : parts[1];
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Chuyển int (count) hoặc string → Visibility
/// - int:    > 0 = Visible, = 0 = Collapsed
/// - string: không rỗng = Visible, rỗng = Collapsed
/// </summary>
[ValueConversion(typeof(object), typeof(Visibility))]
public class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int count)
            return count > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (value is string str)
            return !string.IsNullOrWhiteSpace(str) ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
