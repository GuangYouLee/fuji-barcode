using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using fuji_barcode.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;

namespace fuji_barcode.Views;

public partial class MainWindow : Window
{
    private readonly IServiceProvider _serviceProvider;
    private int _openAdminWindowCount;

    public MainWindow(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
    }

    private void OpenAdminWindow(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_openAdminWindowCount > 0)
            return;

        var adminWindow = _serviceProvider.GetRequiredService<AdminWindow>();
        adminWindow.DataContext = _serviceProvider.GetRequiredService<AdminWindowViewModel>();
        _openAdminWindowCount++;
        if (DataContext is MainWindowViewModel mainVm) mainVm.IsAdminOpen = true;
        adminWindow.Closed += (_, _) =>
        {
            _openAdminWindowCount = Math.Max(0, _openAdminWindowCount - 1);
            if (DataContext is MainWindowViewModel ownerVm) ownerVm.IsAdminOpen = _openAdminWindowCount > 0;
            if (_openAdminWindowCount > 0)
                return;

            Dispatcher.UIThread.Post(() => { Activate(); ScanInput?.Focus(); });
        };

        adminWindow.Show(this);

        if (adminWindow.DataContext is AdminWindowViewModel vm)
        {
            _ = vm.LoadAsync();
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.AuthenticateOperatorAsync = async recipeId =>
            {
                var dialog = _serviceProvider.GetRequiredService<OperatorKeypadDialog>();
                ((OperatorKeypadViewModel)dialog.DataContext!).ConfigureScan(recipeId);
                return await dialog.ShowDialog<Models.OperatorAuthentication?>(this);
            };
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private async void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ScanInput?.Focus();

        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.InitializeAsync();
        }
    }

    private void RefocusScanInput(object? sender, RoutedEventArgs e)
    {
        Dispatcher.UIThread.Post(() => ScanInput?.Focus());
    }

    private async void ConfirmStopScript(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        var dialog = new ConfirmDialog(
            "Stop Script",
            "Stop the running script and clear its sessions?",
            "Stop Script");

        if (await dialog.ShowDialog<bool>(this))
        {
            await vm.StopCommand.ExecuteAsync(null);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.IsBusy) &&
            sender is MainWindowViewModel { IsBusy: false })
        {
            Dispatcher.UIThread.Post(() => ScanInput?.Focus());
        }
    }
}
