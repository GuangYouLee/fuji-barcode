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

    private async void ConfirmGlobalScriptSaveClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AdminWindowViewModel vm)
            return;

        var dialog = new PasswordDialog();
        var password = await dialog.ShowDialog<string?>(this);
        if (password is not null)
            await vm.SaveGlobalScriptAsync(password);
    }

    private async void ConfirmSaveClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AdminWindowViewModel vm)
            return;

        var logIds = vm.GetLogIds();
        var recipeName = vm.RecipeNameInput.Trim();
        if (logIds.Count == 0 || string.IsNullOrEmpty(recipeName))
        {
            await vm.SaveCommand.ExecuteAsync(null);
            return;
        }

        var isUpdate = logIds.Count == 1 && vm.SelectedMapping?.LogId == logIds[0];
        var action = isUpdate ? "Update" : logIds.Count == 1 ? "Create" : "Save";
        var logDescription = logIds.Count == 1 ? $"'{logIds[0]}'" : $"{logIds.Count} Log IDs";
        var dialog = new ConfirmDialog(
            $"{action} Mapping{(logIds.Count == 1 ? "" : "s")}",
            $"{action} mapping{(logIds.Count == 1 ? "" : "s")} for {logDescription} to recipe '{recipeName}'?",
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

        var logIds = vm.GetLogIds();
        if (logIds.Count == 0)
        {
            await vm.DeleteCommand.ExecuteAsync(null);
            return;
        }

        var logDescription = logIds.Count == 1 ? $"'{logIds[0]}'" : $"{logIds.Count} Log IDs";
        var dialog = new ConfirmDialog(
            $"Delete Mapping{(logIds.Count == 1 ? "" : "s")}",
            $"Delete mapping{(logIds.Count == 1 ? "" : "s")} for {logDescription}?",
            "Delete");

        if (await dialog.ShowDialog<bool>(this))
        {
            await vm.DeleteCommand.ExecuteAsync(null);
        }
    }
}
