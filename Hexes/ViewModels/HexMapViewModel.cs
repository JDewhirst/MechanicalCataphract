using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GUI.ViewModels.EntityViewModels;
using GUI.Windows;
using Hexes;
using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Services;

namespace GUI.ViewModels;

public partial class HexMapViewModel : ObservableObject
{
    private readonly IMapService _mapService;
    private readonly IFactionService _factionService;
    private readonly IArmyService _armyService;
    private readonly ICommanderService _commanderService;
    private readonly IOrderService _orderService;
    private readonly IMessageService _messageService;
    private readonly IGameStateService _gameStateService;
    private readonly ITimeAdvanceService _timeAdvanceService;
    private readonly IPathfindingService _pathfindingService;

    // Database-backed gamestate data
    [ObservableProperty]
    private DateTime _gameTime = new();

    // Database-backed hex data
    [ObservableProperty]
    private ObservableCollection<MapHex> _visibleHexes = new();

    [ObservableProperty]
    private ObservableCollection<TerrainType> _terrainTypes = new();

    [ObservableProperty]
    private TerrainType? _selectedTerrainType;

    [ObservableProperty]
    private ObservableCollection<LocationType> _locationTypes = new();

    [ObservableProperty]
    private LocationType? _selectedLocationType;

    // Overlay options for map visualization
    public ObservableCollection<string> OverlayOptions { get; } = new()
    {
        "None",
        "Faction Control",
        "Population Density",
        "Times Foraged",
        "Weather"
    };

    [ObservableProperty]
    private string _selectedOverlay = "None";

    // Properties for forage mode
    [ObservableProperty] 
    private bool _isForageModeActive;

    [ObservableProperty] 
    private Army? _forageTargetArmy;

    [ObservableProperty]
    private ObservableCollection<Hex> _forageSelectedHexes = new();
    public int ForageSelectedCount => ForageSelectedHexes.Count;

    // Properties for path selection mode
    [ObservableProperty]
    private bool _isPathSelectionModeActive;

    [ObservableProperty]
    private IPathMovable? _pathSelectionTarget;

    [ObservableProperty]
    private ObservableCollection<Hex> _pathSelectionHexes = new();
    public int PathSelectionCount => PathSelectionHexes.Count;

    // Map dimensions
    private int _mapRows;
    private int _mapColumns;

    [ObservableProperty]
    private double _hexRadius = 20.0;

    [ObservableProperty]
    private Vector _panOffset = Vector.Zero;

    [ObservableProperty]
    private string _currentTool = "Pan";

    [ObservableProperty]
    private Hex? _selectedHex;

    [ObservableProperty]
    private MapHex? _selectedMapHex;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    // Road painting state - tracks first hex in two-click road creation
    [ObservableProperty]
    private Hex? _roadStartHex;

    // River Painting state - tracks first hex in two-click river creation
    [ObservableProperty]
    private Hex? _riverStartHex;

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

    public HexMapViewModel(
        IMapService mapService,
        IFactionService factionService,
        IArmyService armyService,
        ICommanderService commanderService,
        IOrderService orderService,
        IMessageService messageService,
        IGameStateService gameStateService,
        ITimeAdvanceService timeAdvanceService,
        IPathfindingService pathfindingService)
    {
        _mapService = mapService;
        _factionService = factionService;
        _armyService = armyService;
        _commanderService = commanderService;
        _orderService = orderService;
        _messageService = messageService;
        _gameStateService = gameStateService;
        _timeAdvanceService = timeAdvanceService;
        _pathfindingService = pathfindingService;
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        // Load terrain types
        var terrainTypes = await _mapService.GetTerrainTypesAsync();
        TerrainTypes = new ObservableCollection<TerrainType>(terrainTypes);

        if (TerrainTypes.Count > 0)
            SelectedTerrainType = TerrainTypes[0];

        // Load location types
        var locationTypes = await _mapService.GetLocationTypesAsync();
        LocationTypes = new ObservableCollection<LocationType>(locationTypes);

        if (LocationTypes.Count > 0)
            SelectedLocationType = LocationTypes[0];

        // Check if map exists, if not create one
        if (!await _mapService.MapExistsAsync())
        {
            await _mapService.InitializeMapAsync(50, 50, TerrainTypes.Count > 0 ? TerrainTypes[0].Id : 1);
        }

        // Get map dimensions
        var (rows, cols) = await _mapService.GetMapDimensionsAsync();
        _mapRows = rows;
        _mapColumns = cols;

        // Load all hexes (for small maps; for large maps would use viewport-based loading)
        await LoadVisibleHexesAsync();

        // Load all entities for admin panels
        await LoadAllEntitiesAsync();

        StatusMessage = $"Loaded {VisibleHexes.Count} hexes, {Factions.Count} factions, {Armies.Count} armies";
    }

    private async Task LoadAllEntitiesAsync()
    {
        var factions = await _factionService.GetAllAsync();
        Factions = new ObservableCollection<Faction>(factions);

        var armies = await _armyService.GetAllAsync();
        Armies = new ObservableCollection<Army>(armies);

        var commanders = await _commanderService.GetAllAsync();
        Commanders = new ObservableCollection<Commander>(commanders);

        var orders = await _orderService.GetAllAsync();
        Orders = new ObservableCollection<OrderViewModel>(
            orders.Select(o => new OrderViewModel(o, _orderService)));

        var messages = await _messageService.GetAllAsync();
        Messages = new ObservableCollection<Message>(messages);

        var gameState = await _gameStateService.GetGameStateAsync();
        GameTime = gameState.CurrentGameTime;
    }

    /// <summary>
    /// Reloads entity collections from the database.
    /// </summary>
    [RelayCommand]
    public async Task RefreshAllAsync()
    {
        // Reload all data from database
        //await RefreshHexesAsync();      // VisibleHexes
        await RefreshArmiesAsync();     // Armies
        await RefreshCommandersAsync(); // Commanders
        await RefreshMessagesAsync();   // Messages
        await RefreshFactionsAsync();   // Factions
        await RefreshOrdersAsync();     // Orders
        //await RefreshGameStateAsync();  // GameTime, etc.

        // Clear selections (optional - entities may have changed)
        SelectedArmy = null;
        SelectedCommander = null;
        SelectedMessage = null;
        SelectedEntityViewModel = null;

        StatusMessage = "Data refreshed";
    }
    public async Task RefreshFactionsAsync()
    {
        var factions = await _factionService.GetAllAsync();
        Factions = new ObservableCollection<Faction>(factions);
    }

    public async Task RefreshArmiesAsync()
    {
        var armies = await _armyService.GetAllAsync();
        Armies = new ObservableCollection<Army>(armies);
    }

    public async Task RefreshCommandersAsync()
    {
        var commanders = await _commanderService.GetAllAsync();
        Commanders = new ObservableCollection<Commander>(commanders);
    }

    public async Task RefreshOrdersAsync()
    {
        var orders = await _orderService.GetAllAsync();
        Orders = new ObservableCollection<OrderViewModel>(
            orders.Select(o => new OrderViewModel(o, _orderService)));
    }

    public async Task RefreshMessagesAsync()
    {
        var messages = await _messageService.GetAllAsync();
        Messages = new ObservableCollection<Message>(messages);
    }

    private async Task LoadVisibleHexesAsync()
    {
        var hexes = await _mapService.GetAllHexesAsync();
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
        var result = await _timeAdvanceService.AdvanceTimeAsync(TimeSpan.FromHours(1));
        if (result.Success)
        {
            GameTime = result.NewGameTime;
            StatusMessage = $"Advanced to {result.NewGameTime:g}. " +
                            $"{result.MessagesDelivered} messages delivered.";
        }
        else
        {
            StatusMessage = $"Failed to advance time: {result.Error}";
        }
        await RefreshAllAsync();  // Refresh everything

    }

    [RelayCommand]
    private void SelectTool()
    {
        CurrentTool = "Select";
        StatusMessage = "Tool: Select";
    }

    [RelayCommand]
    private void PanTool()
    {
        CurrentTool = "Pan";
        StatusMessage = "Tool: Pan";
    }

    [RelayCommand]
    private void TerrainPaintTool()
    {
        CurrentTool = "TerrainPaint";
        StatusMessage = $"Tool: Terrain Paint - {SelectedTerrainType?.Name ?? "None"}";
    }

    [RelayCommand]
    private void RoadPaintTool()
    {
        CurrentTool = "RoadPaint";
        RoadStartHex = null;
        StatusMessage = "Tool: Road - Click first hex";
    }

    [RelayCommand]
    private void RiverPaintTool()
    {
        CurrentTool = "RiverPaint";
        RiverStartHex = null;
        StatusMessage = "Tool: River -  Click first hex";
    }

    [RelayCommand]
    private void EraseTool()
    {
        CurrentTool = "Erase";
        RoadStartHex = null;
        StatusMessage = "Tool: Erase - Click hex to clear roads/rivers";
    }

    [RelayCommand]
    private void LocationPaintTool()
    {
        CurrentTool = "LocationPaint";
        StatusMessage = $"Tool: Location Paint - {SelectedLocationType?.Name ?? "None"}";
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

    [RelayCommand]
    private async Task PaintTerrainAsync((Hex hex, int terrainTypeId) args)
    {
        await _mapService.SetTerrainAsync(args.hex, args.terrainTypeId);

        // Update local hex in collection
        for (int i = 0; i < VisibleHexes.Count; i++)
        {
            var mapHex = VisibleHexes[i];
            if (mapHex.Q == args.hex.q && mapHex.R == args.hex.r)
            {
                // Reload the hex from database to get updated TerrainType
                var updatedHex = await _mapService.GetHexAsync(args.hex);
                if (updatedHex != null)
                {
                    VisibleHexes[i] = updatedHex;
                }
                break;
            }
        }

        var terrainName = TerrainTypes.FirstOrDefault(t => t.Id == args.terrainTypeId)?.Name ?? "Unknown";
        StatusMessage = $"Painted {terrainName} at ({args.hex.q}, {args.hex.r})";
    }

    [RelayCommand]
    private async Task PaintRoadAsync(Hex clickedHex)
    {
        if (!RoadStartHex.HasValue)
        {
            // First click - set start hex
            RoadStartHex = clickedHex;
            StatusMessage = $"Road start: ({clickedHex.q}, {clickedHex.r}) - Click adjacent hex";
            return;
        }

        var startHex = RoadStartHex.Value;

        // Check if same hex clicked - cancel
        if (startHex.q == clickedHex.q && startHex.r == clickedHex.r)
        {
            RoadStartHex = null;
            StatusMessage = "Road cancelled - Click first hex";
            return;
        }

        // Find direction from start to clicked hex
        int? dirFromStart = GetNeighborDirection(startHex, clickedHex);
        if (!dirFromStart.HasValue)
        {
            // Not adjacent - start over with this hex
            RoadStartHex = clickedHex;
            StatusMessage = $"Not adjacent. New start: ({clickedHex.q}, {clickedHex.r}) - Click adjacent hex";
            return;
        }

        // Get opposite direction for the other hex
        int dirFromEnd = (dirFromStart.Value + 3) % 6;

        // Check if road already exists - toggle off if so
        var startMapHex = VisibleHexes.FirstOrDefault(h => h.Q == startHex.q && h.R == startHex.r);
        bool hasRoad = startMapHex?.HasRoadInDirection(dirFromStart.Value) ?? false;

        // Set road on both hexes
        await _mapService.SetRoadAsync(startHex, dirFromStart.Value, !hasRoad);
        await _mapService.SetRoadAsync(clickedHex, dirFromEnd, !hasRoad);

        // Update local hexes in collection
        await RefreshHexInCollection(startHex);
        await RefreshHexInCollection(clickedHex);

        // Reset for next road
        RoadStartHex = null;
        StatusMessage = hasRoad
            ? $"Removed road between ({startHex.q}, {startHex.r}) and ({clickedHex.q}, {clickedHex.r})"
            : $"Added road between ({startHex.q}, {startHex.r}) and ({clickedHex.q}, {clickedHex.r})";
    }

    /// <summary>
    /// Gets the direction index (0-5) from hex A to hex B, or null if not adjacent.
    /// </summary>
    private static int? GetNeighborDirection(Hex from, Hex to)
    {
        for (int dir = 0; dir < 6; dir++)
        {
            var neighbor = from.Neighbor(dir);
            if (neighbor.q == to.q && neighbor.r == to.r)
                return dir;
        }
        return null;
    }

    /// <summary>
    /// Checks if two hexes are adjacent (neighbors).
    /// </summary>
    private static bool IsAdjacent(Hex a, Hex b) => GetNeighborDirection(a, b).HasValue;

    /// <summary>
    /// Reloads a hex from the database and updates the local collection.
    /// </summary>
    private async Task RefreshHexInCollection(Hex hex)
    {
        for (int i = 0; i < VisibleHexes.Count; i++)
        {
            var mapHex = VisibleHexes[i];
            if (mapHex.Q == hex.q && mapHex.R == hex.r)
            {
                var updatedHex = await _mapService.GetHexAsync(hex);
                if (updatedHex != null)
                {
                    VisibleHexes[i] = updatedHex;
                }
                break;
            }
        }
    }

    [RelayCommand]
    private async Task PaintRiverAsync(Hex clickedHex)
    {
        if (!RiverStartHex.HasValue)
        {
            // First click - set start hex
            RiverStartHex = clickedHex;
            StatusMessage = $"River start: ({clickedHex.q}, {clickedHex.r}) - Click adjacent hex";
            return;
        }

        var startHex = RiverStartHex.Value;

        // Check if same hex clicked - cancel
        if (startHex.q == clickedHex.q && startHex.r == clickedHex.r)
        {
            RiverStartHex = null;
            StatusMessage = "River cancelled - Click first hex";
            return;
        }

        // Find direction from start to clicked hex
        int? dirFromStart = GetNeighborDirection(startHex, clickedHex);
        if (!dirFromStart.HasValue)
        {
            // Not adjacent - start over with this hex
            RiverStartHex = clickedHex;
            StatusMessage = $"Not adjacent. New start: ({clickedHex.q}, {clickedHex.r}) - Click adjacent hex";
            return;
        }

        // Get opposite direction for the other hex
        int dirFromEnd = (dirFromStart.Value + 3) % 6;

        // Check if road already exists - toggle off if so
        var startMapHex = VisibleHexes.FirstOrDefault(h => h.Q == startHex.q && h.R == startHex.r);
        bool hasRiver = startMapHex?.HasRiverOnEdge(dirFromStart.Value) ?? false;

        // Set river on both hexes
        await _mapService.SetRiverAsync(startHex, dirFromStart.Value, !hasRiver);
        await _mapService.SetRiverAsync(clickedHex, dirFromEnd, !hasRiver);

        // Update local hexes in collection
        await RefreshHexInCollection(startHex);
        await RefreshHexInCollection(clickedHex);

        // Reset for next river
        RiverStartHex = null;
        StatusMessage = hasRiver
            ? $"Removed river between ({startHex.q}, {startHex.r}) and ({clickedHex.q}, {clickedHex.r})"
            : $"Added river between ({startHex.q}, {startHex.r}) and ({clickedHex.q}, {clickedHex.r})";
    }

    [RelayCommand]
    private async Task EraseAsync(Hex hex)
    {
        await _mapService.ClearRoadsAndRiversAsync(hex);
        await RefreshHexInCollection(hex);
        StatusMessage = $"Cleared roads/rivers at ({hex.q}, {hex.r})";
    }

    [RelayCommand]
    private async Task PaintLocationAsync((Hex hex, string? locationName) args)
    {
        if (SelectedLocationType == null)
        {
            StatusMessage = "No location type selected";
            return;
        }

        await _mapService.SetLocationAsync(args.hex, SelectedLocationType.Id, args.locationName);
        await RefreshHexInCollection(args.hex);

        var name = string.IsNullOrEmpty(args.locationName) ? SelectedLocationType.Name : args.locationName;
        StatusMessage = $"Set location '{name}' ({SelectedLocationType.Name}) at ({args.hex.q}, {args.hex.r})";
    }

    [RelayCommand]
    private async Task ClearLocationAsync(Hex hex)
    {
        await _mapService.ClearLocationAsync(hex);
        await RefreshHexInCollection(hex);
        StatusMessage = $"Cleared location at ({hex.q}, {hex.r})";
    }

    partial void OnSelectedTerrainTypeChanged(TerrainType? value)
    {
        if (CurrentTool == "TerrainPaint" && value != null)
        {
            StatusMessage = $"Tool: Terrain Paint - {value.Name}";
        }
    }

    partial void OnSelectedLocationTypeChanged(LocationType? value)
    {
        if (CurrentTool == "LocationPaint" && value != null)
        {
            StatusMessage = $"Tool: Location Paint - {value.Name}";
        }
    }

    // Selection clearing - when one entity type is selected, clear others
    partial void OnSelectedFactionChanged(Faction? value)
    {
        if (value != null)
        {
            SelectedArmy = null;
            SelectedCommander = null;
            SelectedOrder = null;
            SelectedMessage = null;
            SelectedHex = null;
            SelectedMapHex = null;
            SelectedEntityViewModel = new FactionViewModel(value, _factionService);
            StatusMessage = $"Selected faction: {value.Name}";
            _ = LoadFactionWithDetailsAsync(value.Id);
        }
    }

    partial void OnSelectedArmyChanged(Army? value)
    {
        if (value != null)
        {
            SelectedFaction = null;
            SelectedCommander = null;
            SelectedOrder = null;
            SelectedMessage = null;
            SelectedHex = null;
            SelectedMapHex = null;

            // Load army with brigades - the Armies collection doesn't include them
            _ = LoadArmyWithDetails(value.Id);

            StatusMessage = $"Selected army: {value.Name}";
        }
    }

    private async Task LoadFactionWithDetailsAsync(int factionId)
    {
        var factionWithDetails = await
    _factionService.GetFactionWithArmiesAndCommandersAsync(factionId);
        if (factionWithDetails != null)
        {
            var factionVm = new FactionViewModel(factionWithDetails, _factionService);

            // Wire up navigation events
            factionVm.ArmySelected += army => SelectedArmy = army;
            factionVm.CommanderSelected += commander => SelectedCommander = commander;

            SelectedEntityViewModel = factionVm;
            StatusMessage = $"Selected faction: {factionWithDetails.Name}";
        }
    }

    private async Task LoadArmyWithDetails(int armyId)
    {
        var armyWithDetails = await _armyService.GetArmyWithBrigadesAsync(armyId);
        if (armyWithDetails != null)
        {
            var armyVm = new ArmyViewModel(armyWithDetails, _armyService, Commanders, Factions, _pathfindingService);
            armyVm.TransferRequested += OnBrigadeTransferRequested;
            armyVm.PathSelectionRequested += StartPathSelectionMode;
            armyVm.PathSelectionConfirmRequested += ConfirmPathSelectionAsync;
            armyVm.PathSelectionCancelRequested += CancelPathSelectionMode;
            SelectedEntityViewModel = armyVm;
        }
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

    partial void OnSelectedCommanderChanged(Commander? value)
    {
        if (value != null)
        {
            SelectedFaction = null;
            SelectedArmy = null;
            SelectedOrder = null;
            SelectedMessage = null;
            SelectedHex = null;
            SelectedMapHex = null;
            var cmdVm = new CommanderViewModel(value, _commanderService, Armies, Factions, _pathfindingService);
            cmdVm.PathSelectionRequested += StartPathSelectionMode;
            cmdVm.PathSelectionConfirmRequested += ConfirmPathSelectionAsync;
            cmdVm.PathSelectionCancelRequested += CancelPathSelectionMode;
            SelectedEntityViewModel = cmdVm;
            StatusMessage = $"Selected commander: {value.Name}";
        }
    }

    partial void OnSelectedOrderChanged(OrderViewModel? value)
    {
        if (value != null)
        {
            SelectedFaction = null;
            SelectedArmy = null;
            SelectedCommander = null;
            SelectedMessage = null;
            SelectedHex = null;
            SelectedMapHex = null;
            SelectedEntityViewModel = value;  // Already a ViewModel
            StatusMessage = $"Selected order for {value.CommanderName ?? "?"}";
        }
    }

    partial void OnSelectedMessageChanged(Message? value)
    {
        if (value != null)
        {
            SelectedFaction = null;
            SelectedArmy = null;
            SelectedCommander = null;
            SelectedOrder = null;
            SelectedHex = null;
            SelectedMapHex = null;
            var messageVm = new MessageViewModel(value, _messageService, Commanders, _pathfindingService);
            messageVm.PathSelectionRequested += StartPathSelectionMode;
            messageVm.PathSelectionConfirmRequested += ConfirmPathSelectionAsync;
            messageVm.PathSelectionCancelRequested += CancelPathSelectionMode;
            SelectedEntityViewModel = messageVm;
            StatusMessage = $"Selected message: {value.SenderCommander?.Name ?? "?"} → {value.TargetCommander?.Name ?? "?"}";
        }
    }

    partial void OnSelectedMapHexChanged(MapHex? value)
    {
        if (value != null)
        {
            SelectedFaction = null;
            SelectedArmy = null;
            SelectedCommander = null;
            SelectedOrder = null;
            SelectedMessage = null;
            SelectedEntityViewModel = new MapHexViewModel(value, _mapService, Factions, LocationTypes);
            StatusMessage = $"Selected Hex: {value.LocationName}";
            _ = LoadHexWithDetailsAsync(value.Q, value.R);
        }
    }

    private async Task LoadHexWithDetailsAsync(int Q, int R)
    {
        var hexWithDetails = await _mapService.GetHexAsync(Q,R);
        if (hexWithDetails != null)
        {
            var hexVm = new MapHexViewModel(hexWithDetails, _mapService, Factions, LocationTypes);

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
        await _factionService.CreateAsync(faction);
        await RefreshFactionsAsync();
        StatusMessage = $"Created faction: {faction.Name}";
    }

    [RelayCommand]
    private async Task DeleteFactionAsync(Faction faction)
    {
        if (faction == null) return;
        await _factionService.DeleteAsync(faction.Id);
        await RefreshFactionsAsync();
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
            CoordinateQ = 0,
            CoordinateR = 0,
            FactionId = defaultFaction?.Id ?? 1,
            Morale = 10,
            Wagons = 0,
            CarriedSupply = 0
        };
        await _armyService.CreateAsync(army);
        await RefreshArmiesAsync();
        StatusMessage = $"Created army: {army.Name}";
    }

    [RelayCommand]
    private async Task DeleteArmyAsync(Army army)
    {
        if (army == null) return;
        await _armyService.DeleteAsync(army.Id);
        await RefreshArmiesAsync();
        StatusMessage = $"Deleted army: {army.Name}";
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
        await _commanderService.CreateAsync(commander);
        await RefreshCommandersAsync();
        StatusMessage = $"Created commander: {commander.Name}";
    }

    [RelayCommand]
    private async Task DeleteCommanderAsync(Commander commander)
    {
        if (commander == null) return;
        await _commanderService.DeleteAsync(commander.Id);
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
            Number = 1000,
            ScoutingRange = 1
        };

        // Need a brigade service - for now use DbContext directly via army service
        // This is a temporary workaround until we add IBrigadeService
        var armyWithBrigades = await _armyService.GetArmyWithBrigadesAsync(army.Id);
        if (armyWithBrigades != null)
        {
            armyWithBrigades.Brigades.Add(brigade);
            await _armyService.UpdateAsync(armyWithBrigades);
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
        await _orderService.CreateAsync(order);
        await RefreshOrdersAsync();
        StatusMessage = $"Created order for {defaultCommander.Name}";
    }

    [RelayCommand]
    private async Task DeleteOrderAsync(OrderViewModel? orderVm)
    {
        if (orderVm == null) return;
        await _orderService.DeleteAsync(orderVm.Id);
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
        await _messageService.CreateAsync(message);
        await RefreshMessagesAsync();
        StatusMessage = $"Created message from {commanders[0].Name} to {commanders[1].Name}";
    }

    [RelayCommand]
    private async Task DeleteMessageAsync(Message message)
    {
        if (message == null) return;
        await _messageService.DeleteAsync(message.Id);
        await RefreshMessagesAsync();
        StatusMessage = "Deleted message";
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
        CurrentTool = "ForageSelect";
        StatusMessage = $"Forage Mode: Click hexes to select for {ForageTargetArmy.Name}";
    }

    [RelayCommand]
    private void CancelForageMode()
    {
        ForageSelectedHexes.Clear();
        IsForageModeActive = false;
        CurrentTool = "Pan";
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

        // Calculate supply and update hexes
        var supplyGained = await _mapService.ForageHexesAsync(ForageSelectedHexes);

        // Update army's carried supply
        ForageTargetArmy.CarriedSupply += supplyGained;
        await _armyService.UpdateAsync(ForageTargetArmy);

        var armyName = ForageTargetArmy.Name;
        var hexCount = ForageSelectedHexes.Count;

        // Exit forage mode
        ForageSelectedHexes.Clear();
        IsForageModeActive = false;
        CurrentTool = "Pan";

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

    #region Path Selection Commands

    public void StartPathSelectionMode(Message message) => StartPathSelectionModeForEntity(message, "Message");
    public void StartPathSelectionMode(Army army) => StartPathSelectionModeForEntity(army, "Army");
    public void StartPathSelectionMode(Commander commander) => StartPathSelectionModeForEntity(commander, "Commander");

    private void StartPathSelectionModeForEntity(IPathMovable entity, string entityName)
    {
        if (entity.CoordinateQ == null || entity.CoordinateR == null)
        {
            StatusMessage = $"{entityName} must have a location before selecting a path";
            return;
        }

        PathSelectionTarget = entity;
        PathSelectionHexes.Clear();
        IsPathSelectionModeActive = true;
        CurrentTool = "PathSelect";
        StatusMessage = "Click hexes to build path (must be adjacent)";

        // Update ViewModel state if it's the current selection
        if (SelectedEntityViewModel is MessageViewModel msgVm)
        {
            msgVm.IsPathSelectionActive = true;
            msgVm.PathSelectionCount = 0;
        }
        else if (SelectedEntityViewModel is ArmyViewModel armyVm)
        {
            armyVm.IsPathSelectionActive = true;
            armyVm.PathSelectionCount = 0;
        }
        else if (SelectedEntityViewModel is CommanderViewModel cmdVm)
        {
            cmdVm.IsPathSelectionActive = true;
            cmdVm.PathSelectionCount = 0;
        }
    }

    public void AddPathHex(Hex hex)
    {
        if (PathSelectionTarget == null) return;

        // Determine the last hex in the chain (message location or last selected hex)
        Hex lastHex;
        if (PathSelectionHexes.Count == 0)
        {
            lastHex = new Hex(
                PathSelectionTarget.CoordinateQ!.Value,
                PathSelectionTarget.CoordinateR!.Value,
                -PathSelectionTarget.CoordinateQ!.Value - PathSelectionTarget.CoordinateR!.Value);
        }
        else
        {
            lastHex = PathSelectionHexes.Last();
        }

        // Validate adjacency using the shared helper
        if (!IsAdjacent(lastHex, hex))
        {
            StatusMessage = "Hex must be adjacent to previous hex in path";
            return;
        }

        PathSelectionHexes.Add(hex);
        OnPropertyChanged(nameof(PathSelectionCount));
        StatusMessage = $"Path: {PathSelectionCount} hex(es)";

        // Update ViewModel count
        if (SelectedEntityViewModel is MessageViewModel msgVm)
            msgVm.PathSelectionCount = PathSelectionHexes.Count;
        else if (SelectedEntityViewModel is ArmyViewModel armyVm)
            armyVm.PathSelectionCount = PathSelectionHexes.Count;
        else if (SelectedEntityViewModel is CommanderViewModel cmdVm)
            cmdVm.PathSelectionCount = PathSelectionHexes.Count;
    }

    [RelayCommand]
    private async Task ConfirmPathSelectionAsync()
    {
        if (PathSelectionTarget == null || PathSelectionHexes.Count == 0)
        {
            StatusMessage = "No path selected";
            CancelPathSelectionMode();
            return;
        }

        PathSelectionTarget.Path = PathSelectionHexes.ToList();
        var pathLength = PathSelectionHexes.Count;

        // Save based on entity type and refresh ViewModel
        if (PathSelectionTarget is Message msg)
        {
            await _messageService.UpdateAsync(msg);
            await RefreshMessagesAsync();

            // Recreate the MessageViewModel to reflect the updated Path
            if (SelectedMessage != null)
            {
                var refreshedMsgVm = new MessageViewModel(SelectedMessage, _messageService, Commanders, _pathfindingService);
                refreshedMsgVm.PathSelectionRequested += StartPathSelectionMode;
                refreshedMsgVm.PathSelectionConfirmRequested += ConfirmPathSelectionAsync;
                refreshedMsgVm.PathSelectionCancelRequested += CancelPathSelectionMode;
                SelectedEntityViewModel = refreshedMsgVm;
            }
        }
        else if (PathSelectionTarget is Army army)
        {
            await _armyService.UpdateAsync(army);

            // Recreate the ArmyViewModel to reflect the updated Path
            if (SelectedArmy != null)
            {
                var refreshedArmyVm = new ArmyViewModel(SelectedArmy, _armyService, Commanders, Factions, _pathfindingService);
                refreshedArmyVm.TransferRequested += OnBrigadeTransferRequested;
                refreshedArmyVm.PathSelectionRequested += StartPathSelectionMode;
                refreshedArmyVm.PathSelectionConfirmRequested += ConfirmPathSelectionAsync;
                refreshedArmyVm.PathSelectionCancelRequested += CancelPathSelectionMode;
                SelectedEntityViewModel = refreshedArmyVm;
            }
        }
        else if (PathSelectionTarget is Commander commander)
        {
            await _commanderService.UpdateAsync(commander);

            // Recreate the CommanderViewModel to reflect the updated Path
            if (SelectedCommander != null)
            {
                var refreshedCmdVm = new CommanderViewModel(SelectedCommander, _commanderService, Armies, Factions, _pathfindingService);
                refreshedCmdVm.PathSelectionRequested += StartPathSelectionMode;
                refreshedCmdVm.PathSelectionConfirmRequested += ConfirmPathSelectionAsync;
                refreshedCmdVm.PathSelectionCancelRequested += CancelPathSelectionMode;
                SelectedEntityViewModel = refreshedCmdVm;
            }
        }

        CancelPathSelectionMode();
        StatusMessage = $"Path set: {pathLength} hex(es)";
    }

    [RelayCommand]
    private void CancelPathSelectionMode()
    {
        // Update ViewModel state before clearing
        if (SelectedEntityViewModel is MessageViewModel msgVm)
        {
            msgVm.IsPathSelectionActive = false;
            msgVm.PathSelectionCount = 0;
        }
        else if (SelectedEntityViewModel is ArmyViewModel armyVm)
        {
            armyVm.IsPathSelectionActive = false;
            armyVm.PathSelectionCount = 0;
        }
        else if (SelectedEntityViewModel is CommanderViewModel cmdVm)
        {
            cmdVm.IsPathSelectionActive = false;
            cmdVm.PathSelectionCount = 0;
        }

        PathSelectionHexes.Clear();
        PathSelectionTarget = null;
        IsPathSelectionModeActive = false;
        CurrentTool = "Pan";
        OnPropertyChanged(nameof(PathSelectionCount));
    }

    #endregion
}
