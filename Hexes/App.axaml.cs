using System;
using System.Collections.Generic;
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

        // Resolve GameRulesService immediately so GameRules.Current is set
        // before any entity computed properties run (they use the static accessor).
        Services.GetRequiredService<IGameRulesService>();

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

            // Update weather type icon paths (runs every startup; handles existing databases)
            {
                var weatherIconMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Clear"]    = "avares://MechanicalCataphract/Assets/weather-icons/clear-day.svg",
                    ["Rain"]     = "avares://MechanicalCataphract/Assets/weather-icons/rain.svg",
                    ["Fog"]      = "avares://MechanicalCataphract/Assets/weather-icons/fog.svg",
                    ["Overcast"] = "avares://MechanicalCataphract/Assets/weather-icons/overcast.svg",
                };

                var existingWeatherTypes = dbContext.WeatherTypes.ToList();

                foreach (var kvp in weatherIconMap)
                {
                    var name = kvp.Key;
                    var iconPath = kvp.Value;
                    var weatherType = existingWeatherTypes.FirstOrDefault(
                        w => w.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                    if (weatherType != null)
                    {
                        weatherType.IconPath = iconPath;
                    }
                    else if (name.Equals("Overcast", StringComparison.OrdinalIgnoreCase))
                    {
                        dbContext.WeatherTypes.Add(new MechanicalCataphract.Data.Entities.Weather
                        {
                            Name = "Overcast",
                            IconPath = iconPath,
                            MovementModifier = 0.9,
                            CombatModifier = 0.95
                        });
                    }
                }

                dbContext.SaveChanges();
            }

            // Update location type icons from properties file (runs every startup)
            var iconPropertiesPath = System.IO.Path.Combine(
                AppContext.BaseDirectory, "Assets", "location-icons", "icon.properties");

            if (System.IO.File.Exists(iconPropertiesPath))
            {
                var locationIcons = TerrainTypeLoader.LoadLocationIconsFromPropertiesFile(iconPropertiesPath);
                var existingLocationTypes = dbContext.LocationTypes.ToList();

                foreach (var (name, iconFilename, scaleFactor) in locationIcons)
                {
                    var locType = existingLocationTypes.FirstOrDefault(
                        lt => lt.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                    if (locType != null)
                    {
                        locType.IconPath = $"avares://MechanicalCataphract/Assets/location-icons/{iconFilename}";
                        locType.ScaleFactor = scaleFactor;
                    }
                }

                dbContext.SaveChanges();
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
                    await channelManager.SyncExistingEntitiesAsync();
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
        // Game rules (singleton — loaded from game_rules.json, sets GameRules.Current)
        services.AddSingleton<IGameRulesService, GameRulesService>();

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
        services.AddScoped<IFactionRuleService, FactionRuleService>();
        services.AddScoped<INewsService, NewsService>();
        services.AddScoped<IWeatherService, WeatherService>();
        services.AddScoped<INavyService, NavyService>();

        // Discord (singletons — long-lived gateway connection)
        services.AddSingleton<IDiscordBotService, DiscordBotService>();
        services.AddSingleton<IDiscordChannelManager, DiscordChannelManager>();
        services.AddSingleton<IDiscordMessageHandler, DiscordMessageHandler>();

        // ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<HexMapViewModel>();
    }
}