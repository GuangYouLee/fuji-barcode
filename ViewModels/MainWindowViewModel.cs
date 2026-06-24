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

    [ObservableProperty]
    private string _scanText = "";

    [ObservableProperty]
    private ScanMode _scanMode = ScanMode.ObjectId;

    public bool CanScan => !IsBusy;

    public bool IsObjectIdMode
    {
        get => ScanMode == ScanMode.ObjectId;
        set { if (value) ScanMode = ScanMode.ObjectId; }
    }

    public bool IsRecipeMode
    {
        get => ScanMode == ScanMode.Recipe;
        set { if (value) ScanMode = ScanMode.Recipe; }
    }

    partial void OnScanModeChanged(ScanMode value)
    {
        OnPropertyChanged(nameof(IsObjectIdMode));
        OnPropertyChanged(nameof(IsRecipeMode));
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
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanScan));
        SubmitCommand.NotifyCanExecuteChanged();
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

            if (ScanMode == ScanMode.ObjectId)
            {
                var recipe = await _lookupService.LookupRecipeAsync(input);
                if (recipe == null)
                {
                    StatusText = $"Object ID '{input}' not found";
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
