using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using fuji_barcode.Models;
using fuji_barcode.Services;

namespace fuji_barcode.ViewModels;

public partial class HistoryWindowViewModel(TraceabilityService traceability) : ObservableObject
{
    private int _page;
    private bool _hasNext;
    private long _maximumId = long.MaxValue;
    private DateTimeOffset? _appliedMonth;
    public ObservableCollection<ScanHistoryEntry> Rows { get; } = [];
    [ObservableProperty] private string _statusText = "Loading history…";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private DateTimeOffset? _selectedMonth;
    public bool CanInteract => !IsBusy;
    public bool CanPrevious => !IsBusy && _page > 0;
    public bool CanNext => !IsBusy && _hasNext;
    public string PageText => $"Page {_page + 1}";
    public string SelectedMonthText => SelectedMonth?.ToString("MMMM yyyy") ?? "Select month";
    partial void OnSelectedMonthChanged(DateTimeOffset? value) => OnPropertyChanged(nameof(SelectedMonthText));
    partial void OnIsBusyChanged(bool value) { OnPropertyChanged(nameof(CanInteract)); OnPropertyChanged(nameof(CanPrevious)); OnPropertyChanged(nameof(CanNext)); }
    [RelayCommand] private async Task RefreshAsync() { if (!IsBusy) { _page = 0; _maximumId = long.MaxValue; await LoadAsync(); } }
    [RelayCommand] private async Task PreviousAsync() { if (CanPrevious) { _page--; await LoadAsync(); } }
    [RelayCommand] private async Task NextAsync() { if (CanNext) { _page++; await LoadAsync(); } }
    [RelayCommand] private async Task ApplyMonthAsync()
    {
        if (IsBusy) return;
        _appliedMonth = SelectedMonth;
        await RefreshAsync();
    }
    [RelayCommand] private async Task AllMonthsAsync()
    {
        if (IsBusy) return;
        SelectedMonth = null;
        _appliedMonth = null;
        await RefreshAsync();
    }
    public async Task LoadAsync()
    {
        IsBusy = true;
        StatusText = "Loading history…";
        try
        {
            var rows = await traceability.ReadHistoryAsync(_page, _maximumId, _appliedMonth);
            if (_maximumId == long.MaxValue && rows.Count > 0) _maximumId = rows[0].Id;
            _hasNext = rows.Count > 50;
            Rows.Clear();
            foreach (var row in rows.Take(50)) Rows.Add(row);
            var period = _appliedMonth?.ToString("MMMM yyyy") ?? "All months";
            StatusText = Rows.Count == 0 ? $"No scan records. {period}." : $"{Rows.Count} records. {period}. Times are local with UTC offset.";
        }
        catch (Exception) { _hasNext = false; Rows.Clear(); StatusText = "Could not load history. Check the database."; }
        finally { IsBusy = false; OnPropertyChanged(nameof(PageText)); }
    }
}
