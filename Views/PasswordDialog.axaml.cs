using Avalonia.Controls;
using Avalonia.Interactivity;

namespace fuji_barcode.Views;

public partial class PasswordDialog : Window
{
    public PasswordDialog()
    {
        InitializeComponent();
    }

    private void ConfirmClick(object? sender, RoutedEventArgs e)
    {
        Close(PasswordTextBox.Text ?? "");
    }

    private void CancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }
}
