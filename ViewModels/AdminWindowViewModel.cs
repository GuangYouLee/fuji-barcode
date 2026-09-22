using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using fuji_barcode.Models;
using fuji_barcode.Services;

namespace fuji_barcode.ViewModels;

public partial class AdminWindowViewModel : ObservableObject
{
    private const string GlobalScriptPassword = "bioenergy";
    private readonly BarcodeLookupService _lookupService;
    private readonly List<BarcodeRecipeMapping> _allMappings = [];

    public ObservableCollection<BarcodeRecipeMapping> Mappings { get; } = [];

    [ObservableProperty]
    private string _logIdInput = "";

    [ObservableProperty]
    private string _recipeNameInput = "";

    [ObservableProperty]
    private string _globalScriptInput = "";

    [ObservableProperty]
    private string _mappingFilterText = "";

    [ObservableProperty]
    private bool _hasNoMatchingMappings;

    [ObservableProperty]
    private bool _isMappingSearchVisible;

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
    public IRelayCommand ClearMappingFilterCommand { get; }
    public IRelayCommand ToggleMappingSearchCommand { get; }

    public string MappingSearchButtonText => IsMappingSearchVisible ? "Hide Search" : "Search Mappings";

    public AdminWindowViewModel(BarcodeLookupService lookupService)
    {
        _lookupService = lookupService;
        LoadCommand = new AsyncRelayCommand(LoadAsync, () => !IsBusy);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, CanDelete);
        ClearCommand = new RelayCommand(ClearForm);
        ClearMappingFilterCommand = new RelayCommand(() => MappingFilterText = "");
        ToggleMappingSearchCommand = new RelayCommand(() => IsMappingSearchVisible = !IsMappingSearchVisible);
    }

    partial void OnIsBusyChanged(bool value)
    {
        LoadCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
    }

    partial void OnLogIdInputChanged(string value)
    {
        DeleteCommand.NotifyCanExecuteChanged();
    }

    partial void OnMappingFilterTextChanged(string value)
    {
        ApplyMappingFilter();
    }

    partial void OnIsMappingSearchVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(MappingSearchButtonText));

        if (!value)
            MappingFilterText = "";
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

    public IReadOnlyList<string> GetLogIds() => ParseLogIds(LogIdInput);

    private static IReadOnlyList<string> ParseLogIds(string input) =>
        input
            .Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

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
            StatusText = $"Loaded {Mappings.Count} recipe group(s)";
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

        var logIds = GetLogIds();
        var recipeName = RecipeNameInput.Trim();

        if (logIds.Count == 0 || string.IsNullOrEmpty(recipeName))
        {
            StatusText = "At least one Log ID and a recipe name are required";
            return;
        }

        IsBusy = true;

        try
        {
            var selectedLogIds = SelectedMapping is null
                ? Array.Empty<string>()
                : ParseLogIds(SelectedMapping.LogId);
            await _lookupService.ReplaceMappingsAsync(selectedLogIds, logIds, recipeName);
            await ReloadMappingsAsync();
            ClearForm();
            StatusText = $"Saved {logIds.Count} mapping(s) for recipe '{recipeName}'";
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

    public async Task SaveGlobalScriptAsync(string password)
    {
        if (IsBusy)
            return;

        if (password != GlobalScriptPassword)
        {
            StatusText = "Incorrect password. Unysis script was not changed";
            return;
        }

        var script = GlobalScriptInput.Trim();
        var savedScript = string.IsNullOrEmpty(script) ? null : script;

        IsBusy = true;

        try
        {
            await _lookupService.SetSettingAsync("rpa_script_override", savedScript);
            StatusText = string.IsNullOrEmpty(script)
                ? "Cleared Unysis script override (will auto-resolve by recipe)"
                : $"Saved Unysis script override: '{script}'";
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

        var logIds = GetLogIds();
        if (logIds.Count == 0)
        {
            StatusText = "Select or enter a Log ID to delete";
            return;
        }

        IsBusy = true;

        try
        {
            var deleted = await _lookupService.DeleteMappingsAsync(logIds);
            await ReloadMappingsAsync();
            ClearForm();
            StatusText = deleted > 0
                ? $"Deleted {deleted} mapping(s)"
                : "No mapping found";
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
        _allMappings.Clear();
        _allMappings.AddRange(await _lookupService.ListMappingsAsync());
        ApplyMappingFilter();
    }

    private void ApplyMappingFilter()
    {
        var filter = MappingFilterText.Trim();
        var filteredMappings = string.IsNullOrEmpty(filter)
            ? _allMappings
            : _allMappings
                .Where(mapping => mapping.LogId.Contains(filter, StringComparison.OrdinalIgnoreCase)
                    || mapping.RecipeName.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();

        SelectedMapping = null;
        Mappings.Clear();

        foreach (var group in filteredMappings.GroupBy(mapping => mapping.RecipeName, StringComparer.Ordinal))
        {
            Mappings.Add(new BarcodeRecipeMapping
            {
                LogId = string.Join(", ", group.Select(mapping => mapping.LogId)),
                RecipeName = group.Key,
                UpdatedAt = group.Max(mapping => mapping.UpdatedAt)
            });
        }

        HasNoMatchingMappings = filteredMappings.Count == 0;
    }
}
