using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MechanicalCataphract.Data;
using MechanicalCataphract.Services;
using MechanicalCataphract.Discord;
using GUI.ViewModels;

namespace GUI;

public partial class App : Application
{
    public static IServiceProvider? Services { get; private set; }

    /// <summary>
    /// Reference to the main window, used for showing dialogs.
    /// </summary>
    public static MainWindow? MainWindow { get; private set; }

    /// <summary>
    /// Gets a service from the DI container.
    /// </summary>
    public static T GetService<T>() where T : class
    {
        return Services?.GetRequiredService<T>() ?? throw new InvalidOperationException($"Service {typeof(T).Name} not found");
    }

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

        // Ensure database is created and seed terrain types from assets
        using (var scope = Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<WargameDbContext>();
            dbContext.Database.EnsureCreated();

            // Load terrain types from properties file if not already loaded
            if (!dbContext.TerrainTypes.Any())
            {
                var propertiesPath = System.IO.Path.Combine(
                    AppContext.BaseDirectory, "Assets", "classic-icons", "classic.properties");

                if (System.IO.File.Exists(propertiesPath))
                {
                    var terrainTypes = TerrainTypeLoader.LoadFromPropertiesFile(propertiesPath);
                    dbContext.TerrainTypes.AddRange(terrainTypes);
                    dbContext.SaveChanges();
                }
            }
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindowViewModel = Services.GetRequiredService<MainWindowViewModel>();
            var mainWindow = new MainWindow(mainWindowViewModel);
            MainWindow = mainWindow;
            desktop.MainWindow = mainWindow;

            // Auto-start Discord bot if previously configured (fire-and-forget)
            var botService = Services.GetRequiredService<IDiscordBotService>();
            var channelManager = Services.GetRequiredService<IDiscordChannelManager>();
            _ = Task.Run(async () =>
            {
                await botService.TryAutoStartAsync();
                if (botService.IsConnected)
                {
                    await channelManager.EnsureSentinelFactionResourcesAsync();
                    await channelManager.EnsureCoLocationCategoryAsync();
                }
            });

            desktop.ShutdownRequested += (_, e) =>
            {
                // Block shutdown until the gateway connection is cleanly closed.
                botService.StopBotAsync().GetAwaiter().GetResult();
            };
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
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IMessageService, MessageService>();
        services.AddScoped<ICoLocationChannelService, CoLocationChannelService>();
        services.AddScoped<ITimeAdvanceService, TimeAdvanceService>();
        services.AddScoped<IPathfindingService, PathfindingService>();

        // Discord (singletons — long-lived gateway connection)
        services.AddSingleton<IDiscordBotService, DiscordBotService>();
        services.AddSingleton<IDiscordChannelManager, DiscordChannelManager>();

        // ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<HexMapViewModel>();
    }
}