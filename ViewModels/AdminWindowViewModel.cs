using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using fuji_barcode.Models;
using fuji_barcode.Services;

namespace fuji_barcode.ViewModels;

public partial class AdminWindowViewModel : ObservableObject
{
    private readonly BarcodeLookupService _lookupService;

    public ObservableCollection<BarcodeRecipeMapping> Mappings { get; } = [];

    [ObservableProperty]
    private string _objectIdInput = "";

    [ObservableProperty]
    private string _recipeNameInput = "";

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private BarcodeRecipeMapping? _selectedMapping;

    public IAsyncRelayCommand LoadCommand { get; }
    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand DeleteCommand { get; }
    public IRelayCommand ClearCommand { get; }

    public AdminWindowViewModel(BarcodeLookupService lookupService)
    {
        _lookupService = lookupService;
        LoadCommand = new AsyncRelayCommand(LoadAsync, () => !IsBusy);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, CanDelete);
        ClearCommand = new RelayCommand(ClearForm);
    }

    partial void OnIsBusyChanged(bool value)
    {
        LoadCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
    }

    partial void OnObjectIdInputChanged(string value)
    {
        DeleteCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedMappingChanged(BarcodeRecipeMapping? value)
    {
        if (value is null)
        {
            DeleteCommand.NotifyCanExecuteChanged();
            return;
        }

        ObjectIdInput = value.ObjectId;
        RecipeNameInput = value.RecipeName;
        DeleteCommand.NotifyCanExecuteChanged();
    }

    public async Task LoadAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;

        try
        {
            await ReloadMappingsAsync();
            StatusText = $"Loaded {Mappings.Count} mapping(s)";
        }
        catch (Exception ex)
        {
            StatusText = $"Load failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveAsync()
    {
        if (IsBusy)
            return;

        var objectId = ObjectIdInput.Trim();
        var recipeName = RecipeNameInput.Trim();

        if (string.IsNullOrEmpty(objectId) || string.IsNullOrEmpty(recipeName))
        {
            StatusText = "Object ID and recipe name are required";
            return;
        }

        IsBusy = true;

        try
        {
            await _lookupService.UpsertMappingAsync(objectId, recipeName);
            await ReloadMappingsAsync();
            ClearForm();
            StatusText = $"Saved mapping for '{objectId}'";
        }
        catch (Exception ex)
        {
            StatusText = $"Save failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DeleteAsync()
    {
        if (IsBusy)
            return;

        var objectId = ObjectIdInput.Trim();
        if (string.IsNullOrEmpty(objectId))
        {
            StatusText = "Select or enter an Object ID to delete";
            return;
        }

        IsBusy = true;

        try
        {
            var deleted = await _lookupService.DeleteMappingAsync(objectId);
            await ReloadMappingsAsync();
            ClearForm();
            StatusText = deleted
                ? $"Deleted mapping for '{objectId}'"
                : $"No mapping found for '{objectId}'";
        }
        catch (Exception ex)
        {
            StatusText = $"Delete failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanDelete()
    {
        return !IsBusy && !string.IsNullOrWhiteSpace(ObjectIdInput);
    }

    private void ClearForm()
    {
        SelectedMapping = null;
        ObjectIdInput = "";
        RecipeNameInput = "";
    }

    private async Task ReloadMappingsAsync()
    {
        var mappings = await _lookupService.ListMappingsAsync();
        Mappings.Clear();

        foreach (var mapping in mappings)
        {
            Mappings.Add(mapping);
        }
    }
}
