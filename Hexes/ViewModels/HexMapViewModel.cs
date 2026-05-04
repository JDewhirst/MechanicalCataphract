using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GUI.ViewModels.HexMap;
using GUI.ViewModels.EntityViewModels;
using GUI.Windows;
using Hexes;
using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Data;
using MechanicalCataphract.Discord;
using Avalonia.Threading;
using MechanicalCataphract.Services;
using MechanicalCataphract.Services.Calendar;
using MechanicalCataphract.Services.Operations;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using System.Text.Json;

namespace GUI.ViewModels;

public partial class HexMapViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDiscordBotService _discordBotService;
    private readonly IDiscordChannelManager _discordChannelManager;
    private readonly IDiscordMessageHandler _discordMessageHandler;
    private readonly ICalendarDefinitionService _calendarDefinitionService;
    private readonly EntityViewModelFactory _entityViewModelFactory;

    public MapEditingCoordinator Editing { get; }
    public PathSelectionCoordinator PathSelection { get; }
    public DiscordConnectionViewModel Discord { get; }
    public NewsDropCoordinator News { get; }

    // Database-backed gamestate data
    [ObservableProperty]
    private long _currentWorldHour;

    [ObservableProperty]
    private string _currentCalendarDateText = string.Empty;

    // Time picker state
    [ObservableProperty]
    private decimal _pickerYear;

    [ObservableProperty]
    private int _pickerMonthIndex;

    [ObservableProperty]
    private int _pickerDayIndex;

    [ObservableProperty]
    private int _pickerHourIndex;

    [ObservableProperty]
    private List<CalendarMonthDefinition> _calendarMonths = new();

    [ObservableProperty]
    private ObservableCollection<int> _pickerDays = new();

    [ObservableProperty]
    private ObservableCollection<int> _pickerHours = new();

    // Database-backed hex data
    [ObservableProperty]
    private ObservableCollection<MapHex> _visibleHexes = new();

    [ObservableProperty]
    private ObservableCollection<TerrainType> _terrainTypes = new();

    [ObservableProperty]
    private ObservableCollection<LocationType> _locationTypes = new();

    [ObservableProperty]
    private ObservableCollection<MechanicalCataphract.Data.Entities.Weather> _weatherTypes = new();

    // Properties for forage mode
    [ObservableProperty]
    private bool _isForageModeActive;

    [ObservableProperty]
    private Army? _forageTargetArmy;

    [ObservableProperty]
    private ObservableCollection<Hex> _forageSelectedHexes = new();
    public int ForageSelectedCount => ForageSelectedHexes.Count;

    // Properties for muster mode
    [ObservableProperty]
    private bool _isMusterModeActive;

    [ObservableProperty]
    private Hex? _musterDestinationHex;

    [ObservableProperty]
    private Faction? _musterSelectedFaction;

    [ObservableProperty]
    private bool _musterIncludeCavalry;

    [ObservableProperty]
    private bool _musterIncludeWagons;

    [ObservableProperty]
    private ObservableCollection<Hex> _musterSelectedHexes = new();
    public int MusterSelectedCount => MusterSelectedHexes.Count;

    private string _musterPreviewText = "";
    public string MusterPreviewText
    {
        get => _musterPreviewText;
        private set => SetProperty(ref _musterPreviewText, value);
    }

    // Sync guard: prevents OnSelectedXxxChanged from recreating entity VMs
    // when we replace items in collections during Saved-event sync.
    private bool _isSyncingCollection;

    // Map dimensions â€” int.MaxValue means "unbounded" (no map loaded yet)
    private int _mapRows = int.MaxValue;
    private int _mapColumns = int.MaxValue;

    [ObservableProperty]
    private double _hexRadius = 20.0;

    [ObservableProperty]
    private Vector _panOffset = Vector.Zero;

    [ObservableProperty]
    private Hex? _selectedHex;

    [ObservableProperty]
    private MapHex? _selectedMapHex;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    // Entity collections for admin panels
    [ObservableProperty]
    private ObservableCollection<Faction> _factions = new();

    [ObservableProperty]
    private ObservableCollection<Army> _armies = new();

    [ObservableProperty]
    private ObservableCollection<Commander> _commanders = new();

    [ObservableProperty]
    private ObservableCollection<OrderViewModel> _orders = new();

    [ObservableProperty]
    private ObservableCollection<Message> _messages = new();

    [ObservableProperty]
    private ObservableCollection<CoLocationChannel> _coLocationChannels = new();

    // Selected entities for highlighting and left panel display
    [ObservableProperty]
    private object? _selectedEntity;

    /// <summary>
    /// The ViewModel for the currently selected entity, used by the left panel.
    /// </summary>
    [ObservableProperty]
    private IEntityViewModel? _selectedEntityViewModel;

    [ObservableProperty]
    private Faction? _selectedFaction;

    [ObservableProperty]
    private Army? _selectedArmy;

    [ObservableProperty]
    private Commander? _selectedCommander;

    [ObservableProperty]
    private OrderViewModel? _selectedOrder;

    [ObservableProperty]
    private Message? _selectedMessage;

    [ObservableProperty]
    private CoLocationChannel? _selectedCoLocationChannel;

    // News Items
    // Order filtering
    [ObservableProperty]
    private bool _hideProcessedOrders;

    private IList<OrderViewModel> _allOrders = new List<OrderViewModel>();

    // Message filtering
    [ObservableProperty]
    private bool _hideDeliveredMessages;

    private IList<Message> _allMessages = new List<Message>();

    // Navies
    [ObservableProperty]
    private ObservableCollection<Navy> _navies = new();

    [ObservableProperty]
    private Navy? _selectedNavy;

    public HexMapViewModel(
        IServiceScopeFactory scopeFactory,
        IDiscordBotService discordBotService,
        IDiscordChannelManager discordChannelManager,
        IDiscordMessageHandler discordMessageHandler,
        ICalendarDefinitionService calendarDefinitionService)
    {
        _scopeFactory = scopeFactory;
        _discordBotService = discordBotService;
        _discordChannelManager = discordChannelManager;
        _discordMessageHandler = discordMessageHandler;
        _calendarDefinitionService = calendarDefinitionService;

        _entityViewModelFactory = new EntityViewModelFactory(
            _scopeFactory,
            _discordChannelManager,
            () => _mapRows,
            () => _mapColumns,
            () => Commanders,
            () => Factions,
            () => Armies,
            () => LocationTypes,
            () => WeatherTypes);

        Editing = new MapEditingCoordinator(
            _scopeFactory,
            () => VisibleHexes,
            () => TerrainTypes,
            () => LocationTypes,
            () => SelectedHex,
            hex => SelectedMapHex = hex,
            message => StatusMessage = message);

        PathSelection = new PathSelectionCoordinator(
            _scopeFactory,
            () => SelectedEntityViewModel,
            vm => SelectedEntityViewModel = vm,
            () => SelectedMessage,
            () => SelectedArmy,
            () => SelectedCommander,
            CreateMessageViewModel,
            CreateArmyViewModel,
            CreateCommanderViewModel,
            RefreshMessagesAsync,
            message => StatusMessage = message,
            tool => Editing.CurrentTool = tool);

        Discord = new DiscordConnectionViewModel(
            _discordBotService,
            _discordChannelManager,
            _scopeFactory,
            async () =>
            {
                await RefreshCommandersAsync();
                await RefreshArmiesAsync();
            });

        News = new NewsDropCoordinator(
            _scopeFactory,
            () => Factions,
            () => CurrentWorldHour,
            tool => Editing.CurrentTool = tool,
            message => StatusMessage = message);

        _discordMessageHandler.EntitiesChanged += OnDiscordEntitiesChanged;
    }

    private Task InScopeAsync(Func<IServiceProvider, Task> operation)
        => _scopeFactory.InScopeAsync(operation);

    private Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> operation)
        => _scopeFactory.InScopeAsync(operation);

    private T InScope<T>(Func<IServiceProvider, T> operation)
        => _scopeFactory.InScope(operation);

    /// <summary>
    /// Replaces the entire ObservableCollection to force DataGrid re-binding.
    /// EF Core's identity map means the entity references are the same object,
    /// so RemoveAt+Insert doesn't trigger re-reads. A new collection reference
    /// forces the DataGrid to rebuild all rows with current property values.
    /// Wrapped in Dispatcher.UIThread.Post to eliminate threading ambiguity
    /// from fire-and-forget SaveAsync calls.
    /// </summary>
    private void SyncEntityInCollection<T>(
        Func<ObservableCollection<T>> getCollection,
        Action<ObservableCollection<T>> setCollection,
        Action<T>? setSelected,
        T entity)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _isSyncingCollection = true;
            try
            {
                setCollection(new ObservableCollection<T>(getCollection()));
                setSelected?.Invoke(entity);
            }
            finally
            {
                _isSyncingCollection = false;
            }
        });
    }

    private async void OnDiscordEntitiesChanged()
    {
        try
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await RefreshMessagesAsync();
                await RefreshOrdersAsync();
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HexMapVM] Discord entity refresh failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        // Load terrain types
        var terrainTypes = await InScopeAsync(sp =>
            sp.GetRequiredService<IMapService>().GetTerrainTypesAsync());
        TerrainTypes = new ObservableCollection<TerrainType>(terrainTypes);

        if (TerrainTypes.Count > 0)
            Editing.SelectedTerrainType = TerrainTypes[0];

        // Load location types
        var locationTypes = await InScopeAsync(sp =>
            sp.GetRequiredService<IMapService>().GetLocationTypesAsync());
        LocationTypes = new ObservableCollection<LocationType>(locationTypes);

        if (LocationTypes.Count > 0)
            Editing.SelectedLocationType = LocationTypes[0];

        // Load weather types
        var weatherTypes = await InScopeAsync(sp =>
            sp.GetRequiredService<IMapService>().GetWeatherTypesAsync());
        WeatherTypes = new ObservableCollection<MechanicalCataphract.Data.Entities.Weather>(weatherTypes);

        // Check if map exists, if not prompt for size and create one
        if (!await InScopeAsync(sp => sp.GetRequiredService<IMapService>().MapExistsAsync()))
        {
            var (newRows, newCols) = await PromptMapSizeAsync();
            await InScopeAsync(sp =>
                sp.GetRequiredService<IMapService>().InitializeMapAsync(newRows, newCols, TerrainTypes.Count > 0 ? TerrainTypes[0].Id : 1));
        }

        // Get map dimensions (0 means "no map yet" â€” keep int.MaxValue so bounds check is skipped)
        var (rows, cols) = await InScopeAsync(sp =>
            sp.GetRequiredService<IMapService>().GetMapDimensionsAsync());
        if (rows > 0) _mapRows = rows;
        if (cols > 0) _mapColumns = cols;

        // Load all hexes (for small maps; for large maps would use viewport-based loading)
        await LoadVisibleHexesAsync();

        // Seed water hexes with default population density
        await InScopeAsync(sp =>
            sp.GetRequiredService<IMapService>().EnsureWaterPopulationDefaultsAsync());

        // Load all entities for admin panels
        await LoadAllEntitiesAsync();

        StatusMessage = $"Loaded {VisibleHexes.Count} hexes, {Factions.Count} factions, {Armies.Count} armies";

        // Load Discord bot config
        await LoadDiscordConfigAsync();
    }

    private async Task LoadAllEntitiesAsync()
    {
        var factions = await InScopeAsync(sp =>
            sp.GetRequiredService<IFactionService>().GetAllAsync());
        Factions = new ObservableCollection<Faction>(factions);

        var armies = await InScopeAsync(sp =>
            sp.GetRequiredService<IArmyService>().GetAllAsync());
        Armies = new ObservableCollection<Army>(armies);

        var commanders = await InScopeAsync(sp =>
            sp.GetRequiredService<ICommanderService>().GetAllAsync());
        Commanders = new ObservableCollection<Commander>(commanders);

        var orders = await InScopeAsync(sp =>
            sp.GetRequiredService<IOrderService>().GetAllAsync());
        _allOrders = orders.Select(o =>
        {
            var vm = new OrderViewModel(o, _scopeFactory);
            vm.Saved += () => Dispatcher.UIThread.Post(ApplyOrderFilter);
            return vm;
        }).ToList();
        ApplyOrderFilter();

        var messages = await InScopeAsync(sp =>
            sp.GetRequiredService<IMessageService>().GetAllAsync());
        _allMessages = messages.ToList();
        ApplyMessageFilter();

        var coLocationChannels = await InScopeAsync(sp =>
            sp.GetRequiredService<ICoLocationChannelService>().GetAllAsync());
        CoLocationChannels = new ObservableCollection<CoLocationChannel>(coLocationChannels);

        var gameState = await InScopeAsync(sp =>
            sp.GetRequiredService<IGameStateService>().GetGameStateAsync());
        CurrentWorldHour = gameState.CurrentWorldHour;
        CurrentCalendarDateText = InScope(sp =>
            sp.GetRequiredService<ICalendarService>().FormatDateTime(CurrentWorldHour));

        // Initialize time picker
        var calDef = _calendarDefinitionService.GetCalendarDefinition();
        CalendarMonths = calDef.Months;
        PickerHours = new ObservableCollection<int>(Enumerable.Range(0, calDef.HoursPerDay));
        var currentDate = InScope(sp =>
            sp.GetRequiredService<ICalendarService>().GetDate(CurrentWorldHour));
        PickerYear = currentDate.Year;
        PickerMonthIndex = currentDate.MonthNumber - 1;
        RebuildPickerDays(calDef.Months[PickerMonthIndex].Days);
        PickerDayIndex = currentDate.DayOfMonth - 1;
        PickerHourIndex = currentDate.HourOfDay;

        await RefreshNewsItemsAsync();

        var navies = await InScopeAsync(sp =>
            sp.GetRequiredService<INavyService>().GetAllAsync());
        Navies = new ObservableCollection<Navy>(navies);
    }

    private async Task LoadDiscordConfigAsync()
    {
        await Discord.LoadAsync();
    }

    /// <summary>
    /// Reloads entity collections from the database.
    /// </summary>
    [RelayCommand]
    public async Task RefreshAllAsync()
    {
        // Reload all data from database
        //await RefreshHexesAsync();      // VisibleHexes
        await RefreshArmiesAsync();              // Armies
        await RefreshCommandersAsync();          // Commanders
        await RefreshMessagesAsync();            // Messages
        await RefreshFactionsAsync();            // Factions
        await RefreshOrdersAsync();              // Orders
        await RefreshCoLocationChannelsAsync();   // CoLocationChannels
        await RefreshNewsItemsAsync();             // NewsItems
        await RefreshNaviesAsync();               // Navies
        //await RefreshGameStateAsync();  // GameTime, etc.

        // Clear selections (optional - entities may have changed)
        SelectedArmy = null;
        SelectedCommander = null;
        SelectedMessage = null;
        SelectedCoLocationChannel = null;
        SelectedNavy = null;
        SelectedEntityViewModel = null;

        StatusMessage = "Data refreshed";
    }
    public async Task RefreshFactionsAsync()
    {
        var factions = await InScopeAsync(sp =>
            sp.GetRequiredService<IFactionService>().GetAllAsync());
        Factions = new ObservableCollection<Faction>(factions);
    }

    public async Task RefreshArmiesAsync()
    {
        var armies = await InScopeAsync(sp =>
            sp.GetRequiredService<IArmyService>().GetAllAsync());
        Armies = new ObservableCollection<Army>(armies);
    }

    public async Task RefreshCommandersAsync()
    {
        var commanders = await InScopeAsync(sp =>
            sp.GetRequiredService<ICommanderService>().GetAllAsync());
        Commanders = new ObservableCollection<Commander>(commanders);
    }

    public async Task RefreshOrdersAsync()
    {
        var orders = await InScopeAsync(sp =>
            sp.GetRequiredService<IOrderService>().GetAllAsync());
        _allOrders = orders.Select(o =>
        {
            var vm = new OrderViewModel(o, _scopeFactory);
            vm.Saved += () => Dispatcher.UIThread.Post(ApplyOrderFilter);
            return vm;
        }).ToList();
        ApplyOrderFilter();
    }

    partial void OnHideProcessedOrdersChanged(bool value) => ApplyOrderFilter();

    private void ApplyOrderFilter()
    {
        var filtered = HideProcessedOrders
            ? _allOrders.Where(o => !o.Processed)
            : _allOrders;
        Orders = new ObservableCollection<OrderViewModel>(filtered);
    }

    public async Task RefreshMessagesAsync()
    {
        var messages = await InScopeAsync(sp =>
            sp.GetRequiredService<IMessageService>().GetAllAsync());
        _allMessages = messages.ToList();
        ApplyMessageFilter();
    }

    partial void OnHideDeliveredMessagesChanged(bool value) => ApplyMessageFilter();

    private void ApplyMessageFilter()
    {
        var filtered = HideDeliveredMessages
            ? _allMessages.Where(m => !m.Delivered)
            : _allMessages;
        Messages = new ObservableCollection<Message>(filtered);
    }

    public async Task RefreshCoLocationChannelsAsync()
    {
        var channels = await InScopeAsync(sp =>
            sp.GetRequiredService<ICoLocationChannelService>().GetAllAsync());
        CoLocationChannels = new ObservableCollection<CoLocationChannel>(channels);
    }

    public async Task RefreshNewsItemsAsync()
    {
        await News.RefreshAsync();
    }

    public async Task RefreshNaviesAsync()
    {
        var navies = await InScopeAsync(sp =>
            sp.GetRequiredService<INavyService>().GetAllAsync());
        Navies = new ObservableCollection<Navy>(navies);
    }

    private async Task LoadVisibleHexesAsync()
    {
        var hexes = await InScopeAsync(sp =>
            sp.GetRequiredService<IMapService>().GetAllHexesAsync());
        VisibleHexes = new ObservableCollection<MapHex>(hexes);
    }

    partial void OnSelectedHexChanged(Hex? value)
    {
        if (value.HasValue)
        {
            var hex = value.Value;
            StatusMessage = $"Selected: ({hex.q}, {hex.r}, {hex.s})";

            // Find the MapHex for this hex
            foreach (var mapHex in VisibleHexes)
            {
                if (mapHex.Q == hex.q && mapHex.R == hex.r)
                {
                    SelectedMapHex = mapHex;
                    if (mapHex.TerrainType != null)
                    {
                        StatusMessage += $" - {mapHex.TerrainType.Name}";
                    }
                    break;
                }
            }
        }
        else
        {
            StatusMessage = string.Empty;
            SelectedMapHex = null;
        }
    }

    [RelayCommand]
    async Task AdvanceTimeAsync()
    {
        var result = await InScopeAsync(sp =>
            sp.GetRequiredService<ITimeAdvanceService>().AdvanceTimeAsync(1));
        if (result.Success)
        {
            CurrentWorldHour++;
            CurrentCalendarDateText = result.FormattedTime;
            StatusMessage = $"Advanced to {result.FormattedTime}. " +
                            $"{result.MessagesDelivered} messages delivered.";
        }
        else
        {
            StatusMessage = $"Failed to advance time: {result.Error}";
        }
        await RefreshAllAsync();  // Refresh everything

    }

    partial void OnPickerMonthIndexChanged(int value)
    {
        if (CalendarMonths.Count > 0 && value >= 0 && value < CalendarMonths.Count)
        {
            int maxDays = CalendarMonths[value].Days;
            RebuildPickerDays(maxDays);
            if (PickerDayIndex >= maxDays)
                PickerDayIndex = maxDays - 1;
        }
    }

    private void RebuildPickerDays(int count)
    {
        PickerDays = new ObservableCollection<int>(Enumerable.Range(1, count));
    }

    [RelayCommand]
    async Task SetTimeAsync()
    {
        long newWorldHour = InScope(sp =>
            sp.GetRequiredService<ICalendarService>().GetWorldHour(
                (int)PickerYear, PickerMonthIndex + 1, PickerDayIndex + 1, PickerHourIndex));

        await InScopeAsync(sp =>
            sp.GetRequiredService<IGameStateService>().SetCurrentWorldHourAsync(newWorldHour));
        CurrentWorldHour = newWorldHour;
        CurrentCalendarDateText = InScope(sp =>
            sp.GetRequiredService<ICalendarService>().FormatDateTime(newWorldHour));
        StatusMessage = $"Time set to {CurrentCalendarDateText}.";
        await RefreshAllAsync();
    }

    [RelayCommand]
    private void SelectHex(Hex hex)
    {
        SelectedHex = hex;
    }

    [RelayCommand]
    private void CompletePan(Vector delta)
    {
        PanOffset = new Vector(PanOffset.X + delta.X, PanOffset.Y + delta.Y);
    }

    private FactionViewModel CreateFactionViewModel(Faction faction)
        => _entityViewModelFactory.CreateFaction(
            faction,
            army => SelectedArmy = army,
            commander => SelectedCommander = commander,
            entity => SyncEntityInCollection(() => Factions, c => Factions = c, f => SelectedFaction = f, entity));

    private ArmyViewModel CreateArmyViewModel(Army army)
        => _entityViewModelFactory.CreateArmy(
            army,
            OnBrigadeTransferRequested,
            PathSelection.Start,
            PathSelection.ConfirmAsync,
            PathSelection.Cancel,
            OnScoutingReportRequested,
            OnArmyReportRequested,
            entity => SyncEntityInCollection(() => Armies, c => Armies = c, a => SelectedArmy = a, entity));

    private CommanderViewModel CreateCommanderViewModel(Commander commander)
        => _entityViewModelFactory.CreateCommander(
            commander,
            PathSelection.Start,
            PathSelection.ConfirmAsync,
            PathSelection.Cancel,
            () => Commanders = new ObservableCollection<Commander>(Commanders),
            entity => SyncEntityInCollection(() => Commanders, c => Commanders = c, c => SelectedCommander = c, entity));

    private MessageViewModel CreateMessageViewModel(Message message)
        => _entityViewModelFactory.CreateMessage(
            message,
            PathSelection.Start,
            PathSelection.ConfirmAsync,
            PathSelection.Cancel,
            entity => SyncEntityInCollection(() => Messages, c => Messages = c, m => SelectedMessage = m, entity));

    private CoLocationChannelViewModel CreateCoLocationChannelViewModel(CoLocationChannel channel)
        => _entityViewModelFactory.CreateCoLocationChannel(
            channel,
            entity => SyncEntityInCollection(() => CoLocationChannels, c => CoLocationChannels = c, c => SelectedCoLocationChannel = c, entity));

    private NavyViewModel CreateNavyViewModel(Navy navy)
        => _entityViewModelFactory.CreateNavy(
            navy,
            OnNavyReportRequested,
            entity => SyncEntityInCollection(() => Navies, c => Navies = c, n => SelectedNavy = n, entity));

    private MapHexViewModel CreateMapHexViewModel(MapHex mapHex)
        => _entityViewModelFactory.CreateMapHex(
            mapHex,
            entity =>
            {
                for (int i = 0; i < VisibleHexes.Count; i++)
                {
                    if (VisibleHexes[i].Q == entity.Q && VisibleHexes[i].R == entity.R)
                    {
                        VisibleHexes[i] = entity;
                        break;
                    }
                }

                SyncEntityInCollection(() => VisibleHexes, c => VisibleHexes = c, null, entity);
            });

    // Selection clearing - when one entity type is selected, clear others
    private void ClearAllSelectionsExcept(string keep)
    {
        if (keep != nameof(SelectedFaction)) SelectedFaction = null;
        if (keep != nameof(SelectedArmy)) SelectedArmy = null;
        if (keep != nameof(SelectedCommander)) SelectedCommander = null;
        if (keep != nameof(SelectedOrder)) SelectedOrder = null;
        if (keep != nameof(SelectedMessage)) SelectedMessage = null;
        if (keep != nameof(SelectedNavy)) SelectedNavy = null;
        if (keep != nameof(SelectedMapHex)) SelectedHex = null;
        if (keep != nameof(SelectedMapHex)) SelectedMapHex = null;
    }

    partial void OnSelectedFactionChanged(Faction? value)
    {
        if (_isSyncingCollection || value == null) return;
        ClearAllSelectionsExcept(nameof(SelectedFaction));
        SelectedEntityViewModel = CreateFactionViewModel(value);
        StatusMessage = $"Selected faction: {value.Name}";
        _ = LoadFactionWithDetailsAsync(value.Id);
    }

    partial void OnSelectedArmyChanged(Army? value)
    {
        if (_isSyncingCollection || value == null) return;
        ClearAllSelectionsExcept(nameof(SelectedArmy));
        // Load army with brigades - the Armies collection doesn't include them
        _ = LoadArmyWithDetails(value.Id);
        StatusMessage = $"Selected army: {value.Name}";
    }

    private async Task LoadFactionWithDetailsAsync(int factionId)
    {
        var factionWithDetails = await InScopeAsync(sp =>
            sp.GetRequiredService<IFactionService>().GetFactionWithArmiesAndCommandersAsync(factionId));
        if (factionWithDetails != null)
        {
            var factionVm = CreateFactionViewModel(factionWithDetails);

            SelectedEntityViewModel = factionVm;
            StatusMessage = $"Selected faction: {factionWithDetails.Name}";
        }
    }

    private async Task LoadArmyWithDetails(int armyId)
    {
        var armyWithDetails = await InScopeAsync(sp =>
            sp.GetRequiredService<IArmyService>().GetArmyWithBrigadesAsync(armyId));
        if (armyWithDetails != null)
        {
            var armyVm = CreateArmyViewModel(armyWithDetails);
            SelectedEntityViewModel = armyVm;
        }
    }

    /// <summary>
    /// Shows the New Map dialog so the user can choose grid dimensions on first launch.
    /// Falls back to 50Ã—50 if the dialog is cancelled or the window is not yet available.
    /// </summary>
    private static async Task<(int rows, int cols)> PromptMapSizeAsync()
    {
        if (App.MainWindow == null) return (50, 50);
        var dialog = new NewMapDialog();
        var confirmed = await dialog.ShowDialog<bool>(App.MainWindow);
        return confirmed ? (dialog.Rows, dialog.Columns) : (50, 50);
    }

    /// <summary>
    /// Handles brigade transfer request by showing army picker dialog.
    /// </summary>
    private async Task<Army?> OnBrigadeTransferRequested(Brigade brigade)
    {
        if (App.MainWindow == null) return null;

        // Get all armies except the current one
        var otherArmies = Armies.Where(a => a.Id != SelectedArmy?.Id).ToList();

        if (otherArmies.Count == 0)
        {
            StatusMessage = "No other armies to transfer to";
            return null;
        }

        var dialog = new TransferBrigadeDialog(otherArmies, SelectedArmy?.Id ?? 0, brigade.Name);
        var result = await dialog.ShowDialog<Army?>(App.MainWindow);

        if (result != null)
        {
            StatusMessage = $"Transferred {brigade.Name} to {result.Name}";
        }

        return result;
    }

    private async Task OnScoutingReportRequested(Army army)
    {
        // Validate prerequisites
        if (army.Commander == null)
        {
            StatusMessage = "Cannot send scouting report: army has no commander";
            return;
        }
        if (!army.Commander.DiscordChannelId.HasValue)
        {
            StatusMessage = "Cannot send scouting report: commander has no Discord channel";
            return;
        }
        if (!army.CoordinateQ.HasValue || !army.CoordinateR.HasValue)
        {
            StatusMessage = "Cannot send scouting report: army has no location";
            return;
        }
        if (army.Brigades == null || army.Brigades.Count == 0)
        {
            StatusMessage = "Cannot send scouting report: army has no brigades";
            return;
        }

        try
        {
            StatusMessage = "Generating scouting report...";

            int scoutingRange = army.Brigades.Max(b => b.ScoutingRange);
            var centerHex = new Hex(army.CoordinateQ.Value, army.CoordinateR.Value,
                -army.CoordinateQ.Value - army.CoordinateR.Value);

            // Gather map data
            var allHexes = await InScopeAsync(sp =>
                sp.GetRequiredService<IMapService>().GetAllHexesAsync());
            var hexesInRange = allHexes.Where(h => centerHex.Distance(h.ToHex()) <= scoutingRange).ToList();
            var terrainTypes = await InScopeAsync(sp =>
                sp.GetRequiredService<IMapService>().GetTerrainTypesAsync());
            var locationTypes = await InScopeAsync(sp =>
                sp.GetRequiredService<IMapService>().GetLocationTypesAsync());

            // Filter armies within scouting range
            var armiesInRange = Armies
                .Where(a => a.CoordinateQ.HasValue && a.CoordinateR.HasValue)
                .Where(a =>
                {
                    var aHex = new Hex(a.CoordinateQ!.Value, a.CoordinateR!.Value, -a.CoordinateQ.Value - a.CoordinateR.Value);
                    return centerHex.Distance(aHex) <= scoutingRange;
                })
                .ToList();

            // Filter navies within scouting range
            var naviesInRange = Navies
                .Where(n => n.CoordinateQ.HasValue && n.CoordinateR.HasValue)
                .Where(n =>
                {
                    var nHex = new Hex(n.CoordinateQ!.Value, n.CoordinateR!.Value, -n.CoordinateQ.Value - n.CoordinateR.Value);
                    return centerHex.Distance(nHex) <= scoutingRange;
                })
                .ToList();

            // Render
            using var bitmap = ScoutingReportRenderer.RenderScoutingReport(
                hexesInRange, terrainTypes, locationTypes, armiesInRange, naviesInRange, centerHex, scoutingRange);

            // Encode to PNG
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = new System.IO.MemoryStream();
            data.SaveTo(stream);
            stream.Position = 0;

            // Resolve weather at the center hex
            var centerMapHex = hexesInRange.FirstOrDefault(h => h.Q == centerHex.q && h.R == centerHex.r);
            string? weatherName = centerMapHex?.Weather?.Name;

            // Send via Discord
            await _discordChannelManager.SendScoutingReportAsync(army.Commander, stream, army.Name, weatherName);

            StatusMessage = $"Scouting report sent for {army.Name}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to send scouting report: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[HexMapViewModel] OnScoutingReportRequested failed: {ex}");
        }
    }

    private async Task OnArmyReportRequested(Army army)
    {
        if (army.CommanderId == null)
        {
            StatusMessage = "Cannot send army report: army has no commander";
            return;
        }
        try
        {
            var request = new RefereeActionRequest
            {
                ActionType = RefereeActionType.SendArmyReports,
                TriggerType = RefereeActionTriggerType.Manual,
                RequestedBy = "Avalonia UI",
                ParametersJson = JsonSerializer.Serialize(new SendArmyReportsParameters
                {
                    CommanderId = army.CommanderId.Value,
                    SourceArmyId = army.Id
                }),
                PublishOutboxImmediately = true
            };

            var result = await InScopeAsync(sp =>
                sp.GetRequiredService<IRefereeActionExecutor>().ExecuteAsync(request));

            StatusMessage = result.Status switch
            {
                RefereeActionRunStatus.Succeeded => $"Army report run #{result.RunId} sent for {army.Name}",
                RefereeActionRunStatus.PartiallySucceeded => $"Army report run #{result.RunId} partially sent: {result.OutboxMessagesSent} sent, {result.OutboxMessagesFailed} failed",
                RefereeActionRunStatus.Failed => $"Army report run #{result.RunId} failed: {result.ErrorMessage ?? $"{result.OutboxMessagesFailed} message(s) failed"}",
                _ => $"Army report run #{result.RunId}: {result.Status}"
            };
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to send army report: {ex.Message}";
        }
    }

    private async Task OnNavyReportRequested(Navy navy)
    {
        if (navy.CommanderId == null)
        {
            StatusMessage = "Cannot send navy report: navy has no commander";
            return;
        }
        try
        {
            await _discordChannelManager.SendNavyReportsToCommanderAsync(navy.CommanderId.Value);
            StatusMessage = $"Navy report sent for {navy.Name}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to send navy report: {ex.Message}";
        }
    }

    partial void OnSelectedCommanderChanged(Commander? value)
    {
        if (_isSyncingCollection || value == null) return;
        ClearAllSelectionsExcept(nameof(SelectedCommander));
        var cmdVm = CreateCommanderViewModel(value);
        SelectedEntityViewModel = cmdVm;
        StatusMessage = $"Selected commander: {value.Name}";
    }

    partial void OnSelectedOrderChanged(OrderViewModel? value)
    {
        if (_isSyncingCollection || value == null) return;
        ClearAllSelectionsExcept(nameof(SelectedOrder));
        SelectedEntityViewModel = value;  // Already a ViewModel
        StatusMessage = $"Selected order for {value.CommanderName ?? "?"}";
    }

    partial void OnSelectedMessageChanged(Message? value)
    {
        if (_isSyncingCollection || value == null) return;
        ClearAllSelectionsExcept(nameof(SelectedMessage));
        var messageVm = CreateMessageViewModel(value);
        SelectedEntityViewModel = messageVm;
        StatusMessage = $"Selected message: {value.SenderCommander?.Name ?? "?"} â†’ {value.TargetCommander?.Name ?? "?"}";
    }

    partial void OnSelectedCoLocationChannelChanged(CoLocationChannel? value)
    {
        if (_isSyncingCollection || value == null) return;
        ClearAllSelectionsExcept(nameof(SelectedCoLocationChannel));
        _ = LoadCoLocationChannelWithDetailsAsync(value.Id);
        StatusMessage = $"Selected co-location channel: {value.Name}";
    }

    private async Task LoadCoLocationChannelWithDetailsAsync(int channelId)
    {
        var channel = await InScopeAsync(sp =>
            sp.GetRequiredService<ICoLocationChannelService>().GetByIdAsync(channelId));
        if (channel != null)
        {
            var vm = CreateCoLocationChannelViewModel(channel);
            SelectedEntityViewModel = vm;
        }
    }

    partial void OnSelectedNavyChanged(Navy? value)
    {
        if (_isSyncingCollection || value == null) return;
        ClearAllSelectionsExcept(nameof(SelectedNavy));
        _ = LoadNavyWithDetailsAsync(value.Id);
        StatusMessage = $"Selected navy: {value.Name}";
    }

    private async Task LoadNavyWithDetailsAsync(int navyId)
    {
        var navyWithDetails = await InScopeAsync(sp =>
            sp.GetRequiredService<INavyService>().GetNavyWithShipsAsync(navyId));
        if (navyWithDetails != null)
        {
            var navyVm = CreateNavyViewModel(navyWithDetails);
            SelectedEntityViewModel = navyVm;
        }
    }

    partial void OnSelectedMapHexChanged(MapHex? value)
    {
        if (_isSyncingCollection || value == null) return;
        ClearAllSelectionsExcept(nameof(SelectedMapHex));
        SelectedEntityViewModel = CreateMapHexViewModel(value);
        StatusMessage = $"Selected Hex: {value.LocationName}";
        _ = LoadHexWithDetailsAsync(value.Q, value.R);
    }

    private async Task LoadHexWithDetailsAsync(int Q, int R)
    {
        var hexWithDetails = await InScopeAsync(sp =>
            sp.GetRequiredService<IMapService>().GetHexAsync(Q, R));
        if (hexWithDetails != null)
        {
            // Reconcile nav props to in-memory instances for ComboBox reference equality
            hexWithDetails.ControllingFaction = Factions.FirstOrDefault(f => f.Id == hexWithDetails.ControllingFactionId);
            hexWithDetails.LocationType = LocationTypes.FirstOrDefault(l => l.Id == hexWithDetails.LocationTypeId);
            hexWithDetails.LocationFaction = Factions.FirstOrDefault(f => f.Id == hexWithDetails.LocationFactionId);
            hexWithDetails.Weather = WeatherTypes.FirstOrDefault(w => w.Id == hexWithDetails.WeatherId);

            var hexVm = CreateMapHexViewModel(hexWithDetails);

            SelectedEntityViewModel = hexVm;
            StatusMessage = $"Selected hex: {hexWithDetails.Q}, {hexWithDetails.R}";
        }
    }

    #region Entity CRUD Commands

    [RelayCommand]
    private async Task AddFactionAsync()
    {
        var faction = new Faction
        {
            Name = "New Faction",
            ColorHex = "#808080",
            IsPlayerFaction = true
        };
        await InScopeAsync(sp =>
            sp.GetRequiredService<IFactionService>().CreateAsync(faction));
        await _discordChannelManager.OnFactionCreatedAsync(faction);
        await RefreshFactionsAsync();
        StatusMessage = $"Created faction: {faction.Name}";
    }

    [RelayCommand]
    private async Task DeleteFactionAsync(Faction faction)
    {
        if (faction == null) return;
        if (faction.Id == 1) { StatusMessage = "Cannot delete the No Faction sentinel."; return; }

        // 1. Discord cleanup â€” removes roles from commanders, moves their channels out
        await _discordChannelManager.OnFactionDeletedAsync(faction);

        // 2. Reassign all commanders of this faction to "No Faction" (Id=1)
        //    Must happen before faction delete due to FK restrict constraint.
        await InScopeAsync(async sp =>
        {
            var commanderService = sp.GetRequiredService<ICommanderService>();
            var armyService = sp.GetRequiredService<IArmyService>();
            var navyService = sp.GetRequiredService<INavyService>();
            var factionService = sp.GetRequiredService<IFactionService>();

            var commanders = await commanderService.GetAllAsync();
            foreach (var cmd in commanders.Where(c => c.FactionId == faction.Id))
            {
                cmd.FactionId = 1;
                await commanderService.UpdateAsync(cmd);
            }

            // 3. Reassign all armies of this faction to "No Faction" (same Restrict constraint).
            var armies = await armyService.GetAllAsync();
            foreach (var army in armies.Where(a => a.FactionId == faction.Id))
            {
                army.FactionId = 1;
                await armyService.UpdateAsync(army);
            }

            // 4. Reassign all navies of this faction to "No Faction" (same Restrict constraint).
            var navies = await navyService.GetAllAsync();
            foreach (var navy in navies.Where(n => n.FactionId == faction.Id))
            {
                navy.FactionId = 1;
                await navyService.UpdateAsync(navy);
            }

            // 5. Delete the faction itself
            await factionService.DeleteAsync(faction.Id);
        });
        await RefreshFactionsAsync();
        await RefreshCommandersAsync();
        await RefreshArmiesAsync();
        await RefreshNaviesAsync();
        StatusMessage = $"Deleted faction: {faction.Name}";
    }

    [RelayCommand]
    private async Task AddArmyAsync()
    {
        // Get first faction as default, or create without faction
        var defaultFaction = Factions.FirstOrDefault();
        var army = new Army
        {
            Name = "New Army",
            CoordinateQ = MapHex.SentinelQ,
            CoordinateR = MapHex.SentinelR,
            FactionId = defaultFaction?.Id ?? 1,
            Morale = 10,
            Wagons = 0,
            CarriedSupply = 0
        };
        await InScopeAsync(sp =>
            sp.GetRequiredService<IArmyService>().CreateAsync(army));
        await RefreshArmiesAsync();
        StatusMessage = $"Created army: {army.Name}";
    }

    [RelayCommand]
    private async Task DeleteArmyAsync(Army army)
    {
        if (army == null) return;
        await InScopeAsync(sp =>
            sp.GetRequiredService<IArmyService>().DeleteAsync(army.Id));
        await RefreshArmiesAsync();
        StatusMessage = $"Deleted army: {army.Name}";
    }

    [RelayCommand]
    private async Task AddNavyAsync()
    {
        var defaultFaction = Factions.FirstOrDefault();
        var navy = new Navy
        {
            Name = "New Navy",
            CoordinateQ = MapHex.SentinelQ,
            CoordinateR = MapHex.SentinelR,
            FactionId = defaultFaction?.Id ?? 1,
            CarriedSupply = 0
        };
        await InScopeAsync(sp =>
            sp.GetRequiredService<INavyService>().CreateAsync(navy));
        await RefreshNaviesAsync();
        StatusMessage = $"Created navy: {navy.Name}";
    }

    [RelayCommand]
    private async Task DeleteNavyAsync(Navy navy)
    {
        if (navy == null) return;
        await InScopeAsync(sp =>
            sp.GetRequiredService<INavyService>().DeleteAsync(navy.Id));
        await RefreshNaviesAsync();
        StatusMessage = $"Deleted navy: {navy.Name}";
    }

    [RelayCommand]
    private async Task AddCommanderAsync()
    {
        var defaultFaction = Factions.FirstOrDefault();
        var commander = new Commander
        {
            Name = "New Commander",
            FactionId = defaultFaction?.Id ?? 1,
            Age = 30
        };
        await InScopeAsync(sp =>
            sp.GetRequiredService<ICommanderService>().CreateAsync(commander));
        var cmdFaction = Factions.FirstOrDefault(f => f.Id == commander.FactionId);
        if (cmdFaction != null)
            await _discordChannelManager.OnCommanderCreatedAsync(commander, cmdFaction);
        await RefreshCommandersAsync();
        StatusMessage = $"Created commander: {commander.Name}";
    }

    [RelayCommand]
    private async Task DeleteCommanderAsync(Commander commander)
    {
        if (commander == null) return;
        await _discordChannelManager.OnCommanderDeletedAsync(commander);
        await InScopeAsync(sp =>
            sp.GetRequiredService<ICommanderService>().DeleteAsync(commander.Id));
        await RefreshCommandersAsync();
        StatusMessage = $"Deleted commander: {commander.Name}";
    }

    [RelayCommand]
    private async Task AddBrigadeToArmyAsync(Army army)
    {
        if (army == null) return;

        var brigade = new Brigade
        {
            Name = "New Brigade",
            ArmyId = army.Id,
            FactionId = army.FactionId,
            UnitType = UnitType.Infantry,
            Number = 1000
        };

        // Need a brigade service - for now use DbContext directly via army service
        // This is a temporary workaround until we add IBrigadeService
        var armyWithBrigades = await InScopeAsync(sp =>
            sp.GetRequiredService<IArmyService>().GetArmyWithBrigadesAsync(army.Id));
        if (armyWithBrigades != null)
        {
            armyWithBrigades.Brigades.Add(brigade);
            await InScopeAsync(sp =>
                sp.GetRequiredService<IArmyService>().UpdateAsync(armyWithBrigades));
            await RefreshArmiesAsync();
            StatusMessage = $"Added brigade to {army.Name}";
        }
    }

    [RelayCommand]
    private async Task AddOrderAsync()
    {
        var defaultCommander = Commanders.FirstOrDefault();
        if (defaultCommander == null)
        {
            StatusMessage = "Create a commander first";
            return;
        }

        var order = new Order
        {
            CommanderId = defaultCommander.Id,
            Contents = "New order",
            Processed = false,
            CreatedAt = System.DateTime.UtcNow
        };
        await InScopeAsync(sp =>
            sp.GetRequiredService<IOrderService>().CreateAsync(order));
        await RefreshOrdersAsync();
        StatusMessage = $"Created order for {defaultCommander.Name}";
    }

    [RelayCommand]
    private async Task DeleteOrderAsync(OrderViewModel? orderVm)
    {
        if (orderVm == null) return;
        await InScopeAsync(sp =>
            sp.GetRequiredService<IOrderService>().DeleteAsync(orderVm.Id));
        await RefreshOrdersAsync();
        StatusMessage = "Deleted order";
    }

    [RelayCommand]
    private async Task AddMessageAsync()
    {
        var commanders = Commanders.ToList();
        if (commanders.Count < 2)
        {
            StatusMessage = "Need at least 2 commanders for messages";
            return;
        }

        var message = new Message
        {
            SenderCommanderId = commanders[0].Id,
            SenderCoordinateQ = commanders[0].CoordinateQ ?? 0,
            SenderCoordinateR = commanders[0].CoordinateR ?? 0,
            TargetCommanderId = commanders[1].Id,
            TargetCoordinateQ = commanders[1].CoordinateQ ?? 0,
            TargetCoordinateR = commanders[1].CoordinateR ?? 0,
            Content = "New message",
            Delivered = false,
            CreatedAt = System.DateTime.UtcNow
        };
        await InScopeAsync(sp =>
            sp.GetRequiredService<IMessageService>().CreateAsync(message));
        await RefreshMessagesAsync();
        StatusMessage = $"Created message from {commanders[0].Name} to {commanders[1].Name}";
    }

    [RelayCommand]
    private async Task DeleteMessageAsync(Message message)
    {
        if (message == null) return;
        await InScopeAsync(sp =>
            sp.GetRequiredService<IMessageService>().DeleteAsync(message.Id));
        await RefreshMessagesAsync();
        StatusMessage = "Deleted message";
    }

    [RelayCommand]
    private async Task AddCoLocationChannelAsync()
    {
        var channel = new CoLocationChannel
        {
            Name = "New Channel",
        };
        await InScopeAsync(sp =>
            sp.GetRequiredService<ICoLocationChannelService>().CreateAsync(channel));
        await _discordChannelManager.OnCoLocationChannelCreatedAsync(channel);
        await RefreshCoLocationChannelsAsync();
        StatusMessage = $"Created co-location channel: {channel.Name}";
    }

    [RelayCommand]
    private async Task DeleteCoLocationChannelAsync(CoLocationChannel channel)
    {
        if (channel == null) return;
        await _discordChannelManager.OnCoLocationChannelDeletedAsync(channel);
        await InScopeAsync(sp =>
            sp.GetRequiredService<ICoLocationChannelService>().DeleteAsync(channel.Id));
        await RefreshCoLocationChannelsAsync();
        StatusMessage = $"Deleted co-location channel: {channel.Name}";
    }

    #endregion

    #region Automated Foraging Commands

    [RelayCommand]
    private void StartForageMode()
    {
        if (ForageTargetArmy == null)
        {
            StatusMessage = "Select an army first";
            return;
        }
        ForageSelectedHexes.Clear();
        IsForageModeActive = true;
        Editing.CurrentTool = "ForageSelect";
        StatusMessage = $"Forage Mode: Click hexes to select for {ForageTargetArmy.Name}";
    }

    [RelayCommand]
    private void CancelForageMode()
    {
        ForageSelectedHexes.Clear();
        IsForageModeActive = false;
        Editing.CurrentTool = "Pan";
        StatusMessage = "Forage cancelled";
    }

    public void ToggleForageHexSelection(Hex hex)
    {
        if (ForageSelectedHexes.Contains(hex))
            ForageSelectedHexes.Remove(hex);
        else
            ForageSelectedHexes.Add(hex);

        OnPropertyChanged(nameof(ForageSelectedCount));
        StatusMessage = $"Forage Mode: {ForageSelectedHexes.Count} hex(es) selected";
    }

    [RelayCommand]
    private async Task ConfirmForageAsync()
    {
        if (ForageTargetArmy == null || ForageSelectedHexes.Count == 0)
        {
            StatusMessage = "No hexes selected";
            return;
        }

        var supplyGained = await InScopeAsync(async sp =>
        {
            var mapService = sp.GetRequiredService<IMapService>();
            var armyService = sp.GetRequiredService<IArmyService>();
            var gained = await mapService.ForageHexesAsync(ForageSelectedHexes);
            ForageTargetArmy.CarriedSupply += gained;
            await armyService.UpdateAsync(ForageTargetArmy);
            return gained;
        });

        var armyName = ForageTargetArmy.Name;
        var hexCount = ForageSelectedHexes.Count;

        // Exit forage mode
        ForageSelectedHexes.Clear();
        IsForageModeActive = false;
        Editing.CurrentTool = "Pan";

        // Refresh if this army is currently selected
        if (SelectedEntityViewModel is ArmyViewModel armyVm && armyVm.Id ==
        ForageTargetArmy.Id)
        {
            await LoadArmyWithDetails(ForageTargetArmy.Id);
        }

        // Refresh visible hexes to show updated TimesForaged
        await LoadVisibleHexesAsync();

        StatusMessage = $"Foraged {hexCount} hexes for {armyName}, gained {supplyGained} supply";
    }

    #endregion

    #region Muster Commands

    [RelayCommand]
    private void StartMusterMode()
    {
        if (SelectedHex == null)
        {
            StatusMessage = "Select a destination hex first";
            return;
        }
        if (MusterSelectedFaction == null)
        {
            StatusMessage = "Select a faction first";
            return;
        }
        if (IsForageModeActive || PathSelection.IsActive)
        {
            StatusMessage = "Finish or cancel the current mode first";
            return;
        }

        MusterDestinationHex = SelectedHex;
        MusterSelectedHexes.Clear();
        IsMusterModeActive = true;
        Editing.CurrentTool = "MusterSelect";
        UpdateMusterPreview();
        StatusMessage = $"Muster Mode: Click/drag hexes to select levy sources. Destination: ({MusterDestinationHex.Value.q}, {MusterDestinationHex.Value.r})";
    }

    [RelayCommand]
    private void CancelMusterMode()
    {
        MusterSelectedHexes.Clear();
        IsMusterModeActive = false;
        MusterDestinationHex = null;
        Editing.CurrentTool = "Pan";
        MusterPreviewText = "";
        StatusMessage = "Muster cancelled";
    }

    public void ToggleMusterHexSelection(Hex hex)
    {
        // Filter out water hexes
        var mapHex = VisibleHexes.FirstOrDefault(h => h.Q == hex.q && h.R == hex.r);
        if (mapHex?.TerrainType?.IsWater == true) return;

        if (MusterSelectedHexes.Contains(hex))
            MusterSelectedHexes.Remove(hex);
        else
            MusterSelectedHexes.Add(hex);

        OnPropertyChanged(nameof(MusterSelectedCount));
        UpdateMusterPreview();
        StatusMessage = $"Muster Mode: {MusterSelectedHexes.Count} hex(es) selected";
    }

    public void AddMusterHexSelection(Hex hex)
    {
        // Add-only variant for drag painting â€” does not toggle off
        var mapHex = VisibleHexes.FirstOrDefault(h => h.Q == hex.q && h.R == hex.r);
        if (mapHex?.TerrainType?.IsWater == true) return;
        if (MusterSelectedHexes.Contains(hex)) return;

        MusterSelectedHexes.Add(hex);
        OnPropertyChanged(nameof(MusterSelectedCount));
        UpdateMusterPreview();
        StatusMessage = $"Muster Mode: {MusterSelectedHexes.Count} hex(es) selected";
    }

    private void UpdateMusterPreview()
    {
        var totals = MusterCalculator.Calculate(MusterSelectedHexes, VisibleHexes, MusterIncludeCavalry, MusterIncludeWagons);
        var parts = new List<string> { $"Inf: {totals.Infantry}" };
        if (MusterIncludeCavalry) parts.Add($"Cav: {totals.Cavalry}");
        if (MusterIncludeWagons) parts.Add($"Wag: {totals.Wagons}");
        MusterPreviewText = string.Join(" | ", parts);
    }

    [RelayCommand]
    private async Task ConfirmMusterAsync()
    {
        if (MusterDestinationHex == null || MusterSelectedFaction == null || MusterSelectedHexes.Count == 0)
        {
            StatusMessage = "No hexes selected for muster";
            return;
        }

        var totals = MusterCalculator.Calculate(MusterSelectedHexes, VisibleHexes, MusterIncludeCavalry, MusterIncludeWagons);
        int totalInfantry = totals.Infantry;
        int totalCavalry = totals.Cavalry;
        int totalWagons = totals.Wagons;

        if (totalInfantry == 0 && totalCavalry == 0)
        {
            StatusMessage = "Selected hexes have no population to muster";
            MusterSelectedHexes.Clear();
            IsMusterModeActive = false;
            Editing.CurrentTool = "Pan";
            MusterPreviewText = "";
            return;
        }

        var dest = MusterDestinationHex.Value;

        // Create the army
        var army = new Army
        {
            Name = $"Mustered Army",
            CoordinateQ = dest.q,
            CoordinateR = dest.r,
            FactionId = MusterSelectedFaction.Id,
            Morale = 9,
            Wagons = totalWagons,
            CarriedSupply = 0
        };
        var created = await InScopeAsync(sp =>
            sp.GetRequiredService<IArmyService>().CreateAsync(army));

        // Create infantry brigades (max 1000 per brigade)
        int brigadeNum = 1;
        int remaining = totalInfantry;
        while (remaining > 0)
        {
            int count = Math.Min(remaining, 1000);
            await InScopeAsync(sp => sp.GetRequiredService<IArmyService>().AddBrigadeAsync(new Brigade
            {
                ArmyId = created.Id,
                Name = $"Infantry {brigadeNum}",
                UnitType = UnitType.Infantry,
                Number = count,
                FactionId = MusterSelectedFaction.Id
            }));
            remaining -= count;
            brigadeNum++;
        }

        // Create cavalry brigades (max 250 per brigade)
        if (MusterIncludeCavalry && totalCavalry > 0)
        {
            int cavBrigadeNum = 1;
            remaining = totalCavalry;
            while (remaining > 0)
            {
                int count = Math.Min(remaining, 250);
                await InScopeAsync(sp => sp.GetRequiredService<IArmyService>().AddBrigadeAsync(new Brigade
                {
                    ArmyId = created.Id,
                    Name = $"Cavalry {cavBrigadeNum}",
                    UnitType = UnitType.Cavalry,
                    Number = count,
                    FactionId = MusterSelectedFaction.Id
                }));
                remaining -= count;
                cavBrigadeNum++;
            }
        }

        var hexCount = MusterSelectedHexes.Count;

        // Exit muster mode
        MusterSelectedHexes.Clear();
        IsMusterModeActive = false;
        MusterDestinationHex = null;
        Editing.CurrentTool = "Pan";
        MusterPreviewText = "";

        // Refresh entities
        await LoadAllEntitiesAsync();

        var summary = $"Mustered army at ({dest.q},{dest.r}) from {hexCount} hexes: {totalInfantry} infantry";
        if (totalCavalry > 0) summary += $", {totalCavalry} cavalry";
        if (totalWagons > 0) summary += $", {totalWagons} wagons";
        StatusMessage = summary;
    }

    #endregion

}
