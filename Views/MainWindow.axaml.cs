using Avalonia.Controls;
using fuji_barcode.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace fuji_barcode.Views;

public partial class MainWindow : Window
{
    private readonly IServiceProvider _serviceProvider;

    public MainWindow(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        InitializeComponent();
        Loaded += (_, _) => ScanInput?.Focus();
    }

    private async void OpenAdminWindow(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var adminWindow = _serviceProvider.GetRequiredService<AdminWindow>();
        adminWindow.DataContext = _serviceProvider.GetRequiredService<AdminWindowViewModel>();

        if (adminWindow.DataContext is AdminWindowViewModel vm)
        {
            await vm.LoadAsync();
        }

        adminWindow.Show(this);
    }
}
