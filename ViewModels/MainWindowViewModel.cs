using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using fuji_barcode.Models;
using fuji_barcode.Services;
using Microsoft.Data.Sqlite;

namespace fuji_barcode.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly BarcodeLookupService _lookupService;
    private readonly RpaEngineClient _rpaClient;
    private readonly CurrentRecipeApi _currentRecipeApi;
    private readonly TraceabilityService _traceability;
    public Func<string, Task<OperatorAuthentication?>>? AuthenticateOperatorAsync { get; set; }
    [ObservableProperty] private bool _isAdminOpen;
    partial void OnIsAdminOpenChanged(bool value) { OnPropertyChanged(nameof(CanScan)); SubmitCommand.NotifyCanExecuteChanged(); }
    private readonly string _scanModePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "fuji-barcode",
        "user-preferences.json");

    [ObservableProperty]
    private string _scanText = "";

    [ObservableProperty]
    private ScanMode _scanMode = ScanMode.LogId;

    public bool CanScan => !IsBusy && !IsAdminOpen;

    public bool IsLogIdMode
    {
        get => ScanMode == ScanMode.LogId;
        set { if (value) ScanMode = ScanMode.LogId; }
    }

    public bool IsRecipeMode
    {
        get => ScanMode == ScanMode.Recipe;
        set { if (value) ScanMode = ScanMode.Recipe; }
    }

    partial void OnScanModeChanged(ScanMode value)
    {
        OnPropertyChanged(nameof(IsLogIdMode));
        OnPropertyChanged(nameof(IsRecipeMode));

        if (!TrySaveScanMode(value))
        {
            StatusText = "Warning: could not save last mode selection";
        }
    }

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private bool _isBusy;

    public IAsyncRelayCommand SubmitCommand { get; }
    public IAsyncRelayCommand StopCommand { get; }

    public MainWindowViewModel(BarcodeLookupService lookupService, RpaEngineClient rpaClient, CurrentRecipeApi currentRecipeApi, TraceabilityService traceability)
    {
        _lookupService = lookupService;
        _rpaClient = rpaClient;
        _currentRecipeApi = currentRecipeApi;
        _traceability = traceability;
        SubmitCommand = new AsyncRelayCommand(ProcessScanAsync, () => CanScan);
        StopCommand = new AsyncRelayCommand(StopScriptAsync);
        _scanMode = LoadScanMode();
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanScan));
        SubmitCommand.NotifyCanExecuteChanged();
    }

    public async Task InitializeAsync()
    {
        IsBusy = true;
        StatusText = "Checking database...";

        try
        {
            await _lookupService.EnsureDatabaseReadyAsync();
            StatusText = "Ready";
        }
        catch (Exception ex)
        {
            StatusText = $"Database init failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private ScanMode LoadScanMode()
    {
        if (!File.Exists(_scanModePath))
            return ScanMode.LogId;

        try
        {
            var text = File.ReadAllText(_scanModePath);
            return Enum.TryParse<ScanMode>(text, ignoreCase: true, out var mode) && Enum.IsDefined(mode)
                ? mode
                : ScanMode.LogId;
        }
        catch
        {
            return ScanMode.LogId;
        }
    }

    private bool TrySaveScanMode(ScanMode scanMode)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_scanModePath)!);
            File.WriteAllText(_scanModePath, scanMode.ToString());
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private async Task ProcessScanAsync()
    {
        if (!CanScan)
            return;

        var input = ScanText.Trim();
        var mode = ScanMode;
        if (string.IsNullOrEmpty(input))
        {
            StatusText = "Scan text is empty";
            return;
        }

        IsBusy = true;
        ScanText = "";

        try
        {
            string recipeName;
            string? resolvedLogId = null;

            if (mode == ScanMode.LogId)
            {
                var mapping = await _lookupService.LookupMappingAsync(input);
                if (mapping == null)
                {
                    StatusText = $"Log ID '{input}' not found";
                    return;
                }
                if (string.IsNullOrWhiteSpace(mapping.RecipeName))
                {
                    StatusText = $"Log ID '{input}' has no recipe configured";
                    return;
                }
                recipeName = mapping.RecipeName;
                resolvedLogId = mapping.LogId;
                StatusText = $"Resolved recipe: {recipeName}";
            }
            else
            {
                if (!await _lookupService.RecipeExistsAsync(input))
                {
                    StatusText = $"Recipe '{input}' not found in saved mappings";
                    return;
                }
                recipeName = input;
                StatusText = $"Recipe: {recipeName}";
            }

            if (AuthenticateOperatorAsync is null)
            {
                StatusText = "Operator keypad is unavailable. Scan stopped.";
                return;
            }
            var authentication = await AuthenticateOperatorAsync(recipeName);
            if (authentication is null)
            {
                StatusText = "Scan cancelled. Nothing recorded.";
                return;
            }

            string matchedScript;
            var globalScriptOverride = await _lookupService.GetSettingAsync("rpa_script_override");

            if (!string.IsNullOrWhiteSpace(globalScriptOverride))
            {
                matchedScript = globalScriptOverride;
                StatusText = $"Using global override script: {matchedScript}";
            }
            else
            {
                matchedScript = recipeName;
                StatusText = $"Using recipe script: {matchedScript}";
            }

            var scanId = await _traceability.RecordAsync(authentication, mode, input, recipeName, resolvedLogId);
            string outcome;
            try
            {
                _currentRecipeApi.SetRecipe(recipeName);
                var result = await _rpaClient.RunScriptAsync(matchedScript);
                outcome = result.Success ? "run_ok" : "run_failed";
                StatusText = result.Success
                    ? $"Launch accepted: {result.Message ?? matchedScript} (completion is not tracked)"
                    : $"Launch failed: {result.Message ?? "Unknown error"}";
            }
            catch (HttpRequestException ex) when (ex.StatusCode.HasValue)
            {
                outcome = "run_failed";
                StatusText = $"Launch rejected by RPA engine: HTTP {(int)ex.StatusCode.Value}.";
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                outcome = "connection_error";
                StatusText = "Connection error: launch status may be uncertain. Do not rescan without checking the RPA engine.";
            }
            catch (Exception ex)
            {
                outcome = "run_failed";
                StatusText = $"Launch failed: {ex.Message}";
            }
            try { await _traceability.RecordOutcomeAsync(scanId, outcome); }
            catch (Exception)
            {
                StatusText += $" History result could not be saved (scan {scanId}, result {outcome}); history remains pending. Do not repeat the launch.";
            }
        }
        catch (SqliteException)
        {
            StatusText = "Database operation failed. Nothing recorded and no script launched.";
        }
        catch (HttpRequestException ex)
        {
            StatusText = $"Connection error: {ex.Message}";
        }
        catch (InvalidOperationException ex)
        {
            StatusText = ex.Message;
        }
        catch (Exception ex)
        {
            StatusText = $"Unexpected error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task StopScriptAsync()
    {
        StatusText = "Stopping script...";

        try
        {
            await _rpaClient.StopScriptAsync();
            await _rpaClient.ClearSessionsAsync();
            StatusText = "Stop requested and sessions cleared";
        }
        catch (HttpRequestException ex)
        {
            StatusText = $"Connection error: {ex.Message}";
        }
        catch (InvalidOperationException ex)
        {
            StatusText = ex.Message;
        }
        catch (Exception ex)
        {
            StatusText = $"Stop failed: {ex.Message}";
        }
    }
}
