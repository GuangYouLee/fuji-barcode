using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using fuji_barcode.Models;
using fuji_barcode.Services;

namespace fuji_barcode.ViewModels;

public partial class AdminWindowViewModel : ObservableObject
{
    private readonly BarcodeLookupService _lookupService;
    private readonly RpaEngineClient _rpaClient;

    public ObservableCollection<BarcodeRecipeMapping> Mappings { get; } = [];
    public ObservableCollection<string> AvailableScripts { get; } = [];

    [ObservableProperty]
    private string _logIdInput = "";

    [ObservableProperty]
    private string _recipeNameInput = "";

    [ObservableProperty]
    private string _globalScriptInput = "";

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private BarcodeRecipeMapping? _selectedMapping;

    public IAsyncRelayCommand LoadCommand { get; }
    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand DeleteCommand { get; }
    public IAsyncRelayCommand SaveGlobalScriptCommand { get; }
    public IRelayCommand ClearCommand { get; }

    public AdminWindowViewModel(BarcodeLookupService lookupService, RpaEngineClient rpaClient)
    {
        _lookupService = lookupService;
        _rpaClient = rpaClient;
        LoadCommand = new AsyncRelayCommand(LoadAsync, () => !IsBusy);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, CanDelete);
        SaveGlobalScriptCommand = new AsyncRelayCommand(SaveGlobalScriptAsync, () => !IsBusy);
        ClearCommand = new RelayCommand(ClearForm);
    }

    partial void OnIsBusyChanged(bool value)
    {
        LoadCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
        SaveGlobalScriptCommand.NotifyCanExecuteChanged();
    }

    partial void OnLogIdInputChanged(string value)
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

        LogIdInput = value.LogId;
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
            await _lookupService.EnsureDatabaseReadyAsync();
            await ReloadMappingsAsync();

            var globalScript = await _lookupService.GetSettingAsync("rpa_script_override");
            GlobalScriptInput = globalScript ?? "";

            AvailableScripts.Clear();
            try
            {
                var scripts = await _rpaClient.ListScriptsAsync();
                foreach (var script in scripts)
                {
                    AvailableScripts.Add(script.Name);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load scripts from engine: {ex.Message}");
            }

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

        var logId = LogIdInput.Trim();
        var recipeName = RecipeNameInput.Trim();

        if (string.IsNullOrEmpty(logId) || string.IsNullOrEmpty(recipeName))
        {
            StatusText = "Log ID and recipe name are required";
            return;
        }

        IsBusy = true;

        try
        {
            await _lookupService.UpsertMappingAsync(logId, recipeName);
            await ReloadMappingsAsync();
            ClearForm();
            StatusText = $"Saved mapping for '{logId}'";
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

    private async Task SaveGlobalScriptAsync()
    {
        if (IsBusy)
            return;

        var script = GlobalScriptInput.Trim();
        var savedScript = string.IsNullOrEmpty(script) ? null : script;

        IsBusy = true;

        try
        {
            await _lookupService.SetSettingAsync("rpa_script_override", savedScript);
            StatusText = string.IsNullOrEmpty(script)
                ? "Cleared global RPA script override (will auto-resolve by recipe)"
                : $"Saved global RPA script override: '{script}'";
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

        var logId = LogIdInput.Trim();
        if (string.IsNullOrEmpty(logId))
        {
            StatusText = "Select or enter a Log ID to delete";
            return;
        }

        IsBusy = true;

        try
        {
            var deleted = await _lookupService.DeleteMappingAsync(logId);
            await ReloadMappingsAsync();
            ClearForm();
            StatusText = deleted
                ? $"Deleted mapping for '{logId}'"
                : $"No mapping found for '{logId}'";
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
        return !IsBusy && !string.IsNullOrWhiteSpace(LogIdInput);
    }

    private void ClearForm()
    {
        SelectedMapping = null;
        LogIdInput = "";
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
