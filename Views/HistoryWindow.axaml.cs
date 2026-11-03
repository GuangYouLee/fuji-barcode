using Avalonia.Controls;
using fuji_barcode.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace fuji_barcode.Views;

public partial class HistoryWindow : Window
{
    public HistoryWindow(IServiceProvider services)
    {
        InitializeComponent();
        var viewModel = services.GetRequiredService<HistoryWindowViewModel>();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.LoadAsync();
    }
}
