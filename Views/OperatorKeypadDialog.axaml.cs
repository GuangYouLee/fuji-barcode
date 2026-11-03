using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using fuji_barcode.Models;
using fuji_barcode.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace fuji_barcode.Views;

public partial class OperatorKeypadDialog : Window
{
    private readonly OperatorKeypadViewModel _viewModel;
    public OperatorKeypadDialog(IServiceProvider services)
    {
        InitializeComponent();
        var viewModel = services.GetRequiredService<OperatorKeypadViewModel>();
        DataContext = _viewModel = viewModel;
        AddHandler(KeyDownEvent, SwallowKey, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, SwallowKey, RoutingStrategies.Tunnel);
        AddHandler(TextInputEvent, SwallowText, RoutingStrategies.Tunnel);
        viewModel.CloseRequested += CloseWithResult;
        Closing += (_, e) => { if (viewModel.IsBusy) e.Cancel = true; };
        Closed += (_, _) => { viewModel.CloseRequested -= CloseWithResult; viewModel.ClearCredentials(); };
        Opened += (_, _) =>
        {
            var screen = Screens.ScreenFromWindow(this);
            if (screen is not null)
            {
                MaxHeight = Math.Max(200, screen.WorkingArea.Height / screen.Scaling - 48);
                MaxWidth = Math.Max(260, screen.WorkingArea.Width / screen.Scaling - 32);
            }
        };
    }
    private static void SwallowKey(object? sender, KeyEventArgs e) => e.Handled = true;
    private static void SwallowText(object? sender, TextInputEventArgs e) => e.Handled = true;
    private void CloseWithResult(OperatorAuthentication? result)
    {
        // Allow the command to close the modal while ordinary window closing remains blocked during I/O.
        _viewModel.IsBusy = false;
        Close(result);
    }
}
