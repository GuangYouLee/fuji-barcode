using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using fuji_barcode.Services;

namespace fuji_barcode.ViewModels;

public partial class OperatorsWindowViewModel(TraceabilityService traceability) : ObservableObject
{
    public ObservableCollection<string> Operators { get; } = [];
    [ObservableProperty] private string? _selectedOperator;
    [ObservableProperty] private string _statusText = "Loading operators…";
    [ObservableProperty] private bool _isBusy;
    public bool CanInteract => !IsBusy;
    public bool CanChangeSelected => !IsBusy && SelectedOperator is not null;
    partial void OnIsBusyChanged(bool value) { OnPropertyChanged(nameof(CanInteract)); OnPropertyChanged(nameof(CanChangeSelected)); }
    partial void OnSelectedOperatorChanged(string? value) => OnPropertyChanged(nameof(CanChangeSelected));
    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var ids = await traceability.ListOperatorsAsync();
            Operators.Clear();
            foreach (var id in ids) Operators.Add(id);
            StatusText = ids.Count == 0 ? "No operators. Tap Add operator to create one." : $"{ids.Count} operators. PINs cannot be displayed.";
        }
        catch (Exception) { StatusText = "Could not load operators. Check the database."; }
        finally { IsBusy = false; }
    }
    public async Task DeleteAsync(string id)
    {
        IsBusy = true;
        try { await traceability.DeleteOperatorAsync(id); await LoadAsync(); StatusText = "Operator deleted. Existing history is retained."; }
        catch (Exception) { StatusText = "Could not delete operator. Check the database."; }
        finally { IsBusy = false; }
    }
}
