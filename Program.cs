using Avalonia;
using System;
using System.Threading;

namespace fuji_barcode;

sealed class Program
{
    private const string SingleInstanceMutexName = "Local\\fuji-barcode";

    [STAThread]
    public static void Main(string[] args)
    {
        using var mutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
            return;

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
