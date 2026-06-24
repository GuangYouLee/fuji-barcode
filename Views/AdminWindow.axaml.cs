using Avalonia.Controls;
using Avalonia.Interactivity;
using fuji_barcode.ViewModels;

namespace fuji_barcode.Views;

public partial class AdminWindow : Window
{
    public AdminWindow()
    {
        InitializeComponent();
    }

    private async void ConfirmSaveClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AdminWindowViewModel vm)
            return;

        var logId = vm.LogIdInput.Trim();
        var recipeName = vm.RecipeNameInput.Trim();
        if (string.IsNullOrEmpty(logId) || string.IsNullOrEmpty(recipeName))
        {
            await vm.SaveCommand.ExecuteAsync(null);
            return;
        }

        var isUpdate = vm.SelectedMapping?.LogId == logId;
        var action = isUpdate ? "Update" : "Create";
        var dialog = new ConfirmDialog(
            $"{action} Mapping",
            $"{action} mapping for '{logId}' to recipe '{recipeName}'?",
            action);

        if (await dialog.ShowDialog<bool>(this))
        {
            await vm.SaveCommand.ExecuteAsync(null);
        }
    }

    private async void ConfirmDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AdminWindowViewModel vm)
            return;

        var logId = vm.LogIdInput.Trim();
        if (string.IsNullOrEmpty(logId))
        {
            await vm.DeleteCommand.ExecuteAsync(null);
            return;
        }

        var dialog = new ConfirmDialog(
            "Delete Mapping",
            $"Delete mapping for '{logId}'?",
            "Delete");

        if (await dialog.ShowDialog<bool>(this))
        {
            await vm.DeleteCommand.ExecuteAsync(null);
        }
    }
}
