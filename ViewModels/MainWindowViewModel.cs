using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using fuji_barcode.Helpers;
using fuji_barcode.Models;
using fuji_barcode.Services;

namespace fuji_barcode.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly BarcodeLookupService _lookupService;
    private readonly RpaEngineClient _rpaClient;
    private readonly string _scanModePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "fuji-barcode",
        "user-preferences.json");

    [ObservableProperty]
    private string _scanText = "";

    [ObservableProperty]
    private ScanMode _scanMode = ScanMode.LogId;

    public bool CanScan => !IsBusy;

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

    public MainWindowViewModel(BarcodeLookupService lookupService, RpaEngineClient rpaClient)
    {
        _lookupService = lookupService;
        _rpaClient = rpaClient;
        SubmitCommand = new AsyncRelayCommand(ProcessScanAsync, () => !IsBusy);
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
        if (IsBusy)
            return;

        var input = ScanText.Trim();
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

            if (ScanMode == ScanMode.LogId)
            {
                var recipe = await _lookupService.LookupRecipeAsync(input);
                if (recipe == null)
                {
                    StatusText = $"Log ID '{input}' not found";
                    return;
                }
                recipeName = recipe;
                StatusText = $"Resolved recipe: {recipeName}";
            }
            else
            {
                recipeName = input;
                StatusText = $"Recipe: {recipeName}";
            }

            var scripts = await _rpaClient.ListScriptsAsync();
            var matchedScript = RecipeScriptResolver.Resolve(scripts, recipeName);

            StatusText = $"Matched script: {matchedScript}";

            var result = await _rpaClient.RunScriptAsync(matchedScript);

            StatusText = result.Success
                ? $"Run OK: {result.Message ?? matchedScript}"
                : $"Run failed: {result.Message ?? "Unknown error"}";
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
}
