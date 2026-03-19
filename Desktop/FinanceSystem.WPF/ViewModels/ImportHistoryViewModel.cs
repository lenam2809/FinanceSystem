// ViewModel tab Lịch sử Import
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinanceSystem.Contracts.V1.Imports;
using FinanceSystem.WPF.Services;
using System.Collections.ObjectModel;

namespace FinanceSystem.WPF.ViewModels;

public partial class ImportHistoryViewModel : ObservableObject
{
    private readonly IImportService _importService;

    public ObservableCollection<ImportHistoryDto> HistoryItems { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    private bool _isLoading = false;

    [ObservableProperty]
    private string _pageInfo = "Trang 1 / 1";

    private int _currentPage = 1;
    private int _totalPages  = 1;
    private const int PageSize = 10;

    public ImportHistoryViewModel(IImportService importService)
    {
        _importService = importService;
    }

    // Gọi từ MainWindow.xaml.cs khi user chọn tab Lịch sử
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var result = await _importService.GetHistoryAsync(_currentPage, PageSize);
            HistoryItems.Clear();
            if (result?.Items != null)
                foreach (var item in result.Items)
                    HistoryItems.Add(item);

            _totalPages = result?.TotalPages ?? 1;
            PageInfo    = $"Trang {_currentPage} / {_totalPages}";
        }
        catch (Exception ex)
        {
            PageInfo = $"Lỗi: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    // Tên method phải là PreviousPage → CommunityToolkit tạo PreviousPageCommand
    [RelayCommand(CanExecute = nameof(CanPreviousPage))]
    private async Task PreviousPage()
    {
        _currentPage--;
        await LoadAsync();
    }

    // Tên method phải là NextPage → CommunityToolkit tạo NextPageCommand
    [RelayCommand(CanExecute = nameof(CanNextPage))]
    private async Task NextPage()
    {
        _currentPage++;
        await LoadAsync();
    }

    private bool CanPreviousPage() => _currentPage > 1        && !IsLoading;
    private bool CanNextPage()     => _currentPage < _totalPages && !IsLoading;
}
