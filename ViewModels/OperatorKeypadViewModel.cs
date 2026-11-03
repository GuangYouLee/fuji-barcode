using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using fuji_barcode.Models;
using fuji_barcode.Services;

namespace fuji_barcode.ViewModels;

public partial class OperatorKeypadViewModel(TraceabilityService traceability) : ObservableObject
{
    private string _pin = "";
    private bool _editingPin;
    private bool _saving;
    private bool _replace;
    [ObservableProperty] private string _operatorId = "";
    [ObservableProperty] private string _pinDisplay = "– – – –";
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private string _titleText = "Operator Login";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isCredentialRejected;
    [ObservableProperty] private string _scanContext = "Credentials apply to this scan only.";
    public void ConfigureScan(string recipeId)
        => ScanContext = $"Recipe ID: {recipeId}";
    public bool CanInteract => !IsBusy;
    public bool CanEditId => CanInteract && !_replace;
    public bool HasStatusText => !string.IsNullOrWhiteSpace(StatusText);
    public string OperatorDisplay => FormatDigits(OperatorId);
    public string ActiveFieldText => _editingPin ? "Entering PIN • 4 digits" : "Entering Operator ID • 4 digits";
    public string IdBorder => _editingPin ? "#CBD5E1" : "#15803D";
    public string PinBorder => _editingPin ? "#15803D" : "#CBD5E1";
    public event Action<OperatorAuthentication?>? CloseRequested;
    partial void OnOperatorIdChanged(string value) => OnPropertyChanged(nameof(OperatorDisplay));
    partial void OnStatusTextChanged(string value)
    {
        IsCredentialRejected = false;
        OnPropertyChanged(nameof(HasStatusText));
    }
    partial void OnIsBusyChanged(bool value) { OnPropertyChanged(nameof(CanInteract)); OnPropertyChanged(nameof(CanEditId)); }

    public void ConfigureSave(string? id)
    {
        _saving = true;
        _replace = id is not null;
        OperatorId = id ?? "";
        _editingPin = _replace;
        TitleText = _replace ? "Reset operator PIN" : "Create operator";
        ScanContext = _replace ? "Choose a new 4-digit PIN for this operator." : "Choose a 4-digit Operator ID and a 4-digit PIN.";
        StatusText = "PINs are stored securely and cannot be displayed.";
        NotifyField();
    }

    [RelayCommand] private void SelectId() { if (!_replace && !IsBusy) { _editingPin = false; NotifyField(); } }
    [RelayCommand] private void SelectPin() { if (!IsBusy) { _editingPin = true; NotifyField(); } }
    [RelayCommand] private async Task DigitAsync(string digit)
    {
        if (IsBusy || digit.Length != 1 || digit[0] is < '0' or > '9') return;
        if (_editingPin)
        {
            if (_pin.Length >= TraceabilityService.CredentialLength) return;
            _pin += digit;
        }
        else
        {
            if (_replace || OperatorId.Length >= TraceabilityService.CredentialLength) return;
            OperatorId += digit;
            if (OperatorId.Length == TraceabilityService.CredentialLength)
            {
                _editingPin = true;
                NotifyField();
            }
        }
        RefreshPin();
        if (!_saving && TraceabilityService.ValidId(OperatorId) && TraceabilityService.ValidPin(_pin))
            await ConfirmCommand.ExecuteAsync(null);
    }
    [RelayCommand] private void Clear()
    {
        if (IsBusy) return;
        if (_editingPin) _pin = ""; else if (!_replace) OperatorId = "";
        RefreshPin();
    }
    [RelayCommand] private void Delete()
    {
        if (IsBusy) return;
        if (_editingPin && _pin.Length > 0) _pin = _pin[..^1];
        else if (!_editingPin && !_replace && OperatorId.Length > 0) OperatorId = OperatorId[..^1];
        RefreshPin();
    }
    [RelayCommand] private void Cancel() { if (!IsBusy) CloseRequested?.Invoke(null); }
    [RelayCommand] private async Task ConfirmAsync()
    {
        if (IsBusy) return;
        if (!TraceabilityService.ValidId(OperatorId) || !TraceabilityService.ValidPin(_pin))
        { StatusText = "Complete both four-digit fields."; return; }
        IsBusy = true;
        StatusText = _saving ? "Saving operator…" : "Checking credentials…";
        try
        {
            if (_saving)
            {
                await traceability.SaveOperatorAsync(OperatorId, _pin, _replace);
                CloseRequested?.Invoke(new OperatorAuthentication(OperatorId, DateTimeOffset.UtcNow));
            }
            else
            {
                var authentication = await traceability.AuthenticateAsync(OperatorId, _pin);
                if (authentication is not null)
                    CloseRequested?.Invoke(authentication);
                else
                {
                    _pin = "";
                    _editingPin = true;
                    RefreshPin();
                    NotifyField();
                    StatusText = "Incorrect Operator ID or PIN. Please try again.";
                    IsCredentialRejected = true;
                }
            }
        }
        catch (Exception)
        {
            StatusText = _saving ? "Could not save operator. The ID may already exist or the database is unavailable." : "Authentication unavailable. Cancel and check the database.";
            _pin = "";
            RefreshPin();
        }
        finally { IsBusy = false; }
    }
    public void ClearCredentials() { _pin = ""; OperatorId = ""; RefreshPin(); }
    private void RefreshPin() => PinDisplay = FormatDigits(new string('●', _pin.Length));
    private static string FormatDigits(string value)
        => string.Join(" ", value.PadRight(TraceabilityService.CredentialLength, '–').ToCharArray());
    private void NotifyField()
    {
        OnPropertyChanged(nameof(ActiveFieldText)); OnPropertyChanged(nameof(IdBorder));
        OnPropertyChanged(nameof(PinBorder)); OnPropertyChanged(nameof(CanEditId));
    }
}
