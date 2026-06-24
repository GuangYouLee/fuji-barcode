using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using fuji_barcode.Services;
using fuji_barcode.ViewModels;
using fuji_barcode.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace fuji_barcode;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        var serviceProvider = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.DataContext = serviceProvider.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<BarcodeLookupService>();
        services.AddSingleton<RpaEngineClient>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<AdminWindowViewModel>();
        services.AddTransient<MainWindow>();
        services.AddTransient<AdminWindow>();
    }
}
