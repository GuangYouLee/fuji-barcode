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

    private async void OpenAdminWindow(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var adminWindow = _serviceProvider.GetRequiredService<AdminWindow>();
        adminWindow.DataContext = _serviceProvider.GetRequiredService<AdminWindowViewModel>();
        _openAdminWindowCount++;
        adminWindow.Closed += (_, _) =>
        {
            _openAdminWindowCount = Math.Max(0, _openAdminWindowCount - 1);
            if (_openAdminWindowCount > 0)
                return;

            Dispatcher.UIThread.Post(() => { Activate(); ScanInput?.Focus(); });
        };

        if (adminWindow.DataContext is AdminWindowViewModel vm)
        {
            await vm.LoadAsync();
        }

        adminWindow.Show(this);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
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

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.IsBusy) &&
            sender is MainWindowViewModel { IsBusy: false })
        {
            Dispatcher.UIThread.Post(() => ScanInput?.Focus());
        }
    }
}
