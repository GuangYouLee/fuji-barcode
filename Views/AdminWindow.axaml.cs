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

        var objectId = vm.ObjectIdInput.Trim();
        var recipeName = vm.RecipeNameInput.Trim();
        if (string.IsNullOrEmpty(objectId) || string.IsNullOrEmpty(recipeName))
        {
            await vm.SaveCommand.ExecuteAsync(null);
            return;
        }

        var isUpdate = vm.SelectedMapping?.ObjectId == objectId;
        var action = isUpdate ? "Update" : "Create";
        var dialog = new ConfirmDialog(
            $"{action} Mapping",
            $"{action} mapping for '{objectId}' to recipe '{recipeName}'?",
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

        var objectId = vm.ObjectIdInput.Trim();
        if (string.IsNullOrEmpty(objectId))
        {
            await vm.DeleteCommand.ExecuteAsync(null);
            return;
        }

        var dialog = new ConfirmDialog(
            "Delete Mapping",
            $"Delete mapping for '{objectId}'?",
            "Delete");

        if (await dialog.ShowDialog<bool>(this))
        {
            await vm.DeleteCommand.ExecuteAsync(null);
        }
    }
}
