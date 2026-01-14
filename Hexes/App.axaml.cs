using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MechanicalCataphract.Data;
using MechanicalCataphract.Services;
using GUI.ViewModels;
using System;

namespace GUI;

public partial class App : Application
{
    public static IServiceProvider? Services { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Configure services
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        // Ensure database is created and migrations applied
        using (var scope = Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<WargameDbContext>();
            dbContext.Database.EnsureCreated();
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindowViewModel = Services.GetRequiredService<MainWindowViewModel>();
            var mainWindow = new MainWindow(mainWindowViewModel);
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Database
        services.AddDbContext<WargameDbContext>(options =>
            options.UseSqlite("Data Source=wargame.db"));

        // Services
        services.AddScoped<IMapService, MapService>();
        services.AddScoped<IFactionService, FactionService>();
        services.AddScoped<IArmyService, ArmyService>();
        services.AddScoped<ICommanderService, CommanderService>();
        services.AddScoped<IGameStateService, GameStateService>();

        // ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<HexMapViewModel>();
    }
}