using Avalonia.Controls;
using Avalonia.Interactivity;

namespace fuji_barcode.Views;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog()
    {
        InitializeComponent();
    }

    public ConfirmDialog(string title, string message, string confirmText)
        : this()
    {
        Title = title;
        MessageTextBlock.Text = message;
        ConfirmButton.Content = confirmText;
    }

    private void ConfirmClick(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }

    private void CancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
