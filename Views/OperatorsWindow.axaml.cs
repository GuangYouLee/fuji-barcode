using Avalonia.Controls;
using Avalonia.Interactivity;
using fuji_barcode.Models;
using fuji_barcode.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace fuji_barcode.Views;

public partial class OperatorsWindow : Window
{
    private readonly IServiceProvider _services;
    private readonly OperatorsWindowViewModel _viewModel;
    public OperatorsWindow(IServiceProvider services)
    {
        InitializeComponent();
        var viewModel = services.GetRequiredService<OperatorsWindowViewModel>();
        _services = services;
        DataContext = _viewModel = viewModel;
        Loaded += async (_, _) => await viewModel.LoadAsync();
    }
    private async void AddClick(object? sender, RoutedEventArgs e) => await ShowKeypadAsync(null);
    private async void ResetClick(object? sender, RoutedEventArgs e)
    {
        var id = _viewModel.SelectedOperator;
        if (id is null || !await new ConfirmDialog("Reset PIN", $"Replace the PIN for operator {id}?", "Reset PIN").ShowDialog<bool>(this)) return;
        await ShowKeypadAsync(id);
    }
    private async Task ShowKeypadAsync(string? id)
    {
        if (_viewModel.IsBusy) return;
        var dialog = _services.GetRequiredService<OperatorKeypadDialog>();
        ((OperatorKeypadViewModel)dialog.DataContext!).ConfigureSave(id);
        await dialog.ShowDialog<OperatorAuthentication?>(this);
        await _viewModel.LoadAsync();
    }
    private async void DeleteClick(object? sender, RoutedEventArgs e)
    {
        var id = _viewModel.SelectedOperator;
        if (id is not null && await new ConfirmDialog("Delete operator", $"Delete operator {id}? Existing scan history will remain.", "Delete").ShowDialog<bool>(this))
            await _viewModel.DeleteAsync(id);
    }
    private async void RefreshClick(object? sender, RoutedEventArgs e) => await _viewModel.LoadAsync();
}
