using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Hexes;
using MechanicalCataphract.Data.Entities;
using AvaloniaPoint = Avalonia.Point;

namespace GUI;

public class HexMapView : Control
{
    #region Avalonia Properties

    public static readonly StyledProperty<double> HexRadiusProperty =
        AvaloniaProperty.Register<HexMapView, double>(nameof(HexRadius), defaultValue: 20.0);

    public static readonly StyledProperty<Vector> PanOffsetProperty =
        AvaloniaProperty.Register<HexMapView, Vector>(nameof(PanOffset));

    public static readonly StyledProperty<string?> CurrentToolProperty =
        AvaloniaProperty.Register<HexMapView, string?>(nameof(CurrentTool), defaultValue: "Pan");

    public static readonly StyledProperty<Hex?> SelectedHexProperty =
        AvaloniaProperty.Register<HexMapView, Hex?>(nameof(SelectedHex));

    public static readonly StyledProperty<IList<MapHex>?> VisibleHexesProperty =
        AvaloniaProperty.Register<HexMapView, IList<MapHex>?>(nameof(VisibleHexes));

    public static readonly StyledProperty<IList<TerrainType>?> TerrainTypesProperty =
        AvaloniaProperty.Register<HexMapView, IList<TerrainType>?>(nameof(TerrainTypes));

    public static readonly StyledProperty<TerrainType?> SelectedTerrainTypeProperty =
        AvaloniaProperty.Register<HexMapView, TerrainType?>(nameof(SelectedTerrainType));

    public static readonly StyledProperty<string?> SelectedOverlayProperty =
        AvaloniaProperty.Register<HexMapView, string?>(nameof(SelectedOverlay), defaultValue: "None");

    public static readonly StyledProperty<IList<Army>?> ArmiesProperty =
        AvaloniaProperty.Register<HexMapView, IList<Army>?>(nameof(Armies));

    public static readonly StyledProperty<IList<Commander>?> CommandersProperty =
        AvaloniaProperty.Register<HexMapView, IList<Commander>?>(nameof(Commanders));

    public static readonly StyledProperty<Army?> SelectedArmyProperty =
        AvaloniaProperty.Register<HexMapView, Army?>(nameof(SelectedArmy));

    public static readonly StyledProperty<Commander?> SelectedCommanderProperty =
        AvaloniaProperty.Register<HexMapView, Commander?>(nameof(SelectedCommander));

    public double HexRadius
    {
        get => GetValue(HexRadiusProperty);
        set => SetValue(HexRadiusProperty, value);
    }

    public Vector PanOffset
    {
        get => GetValue(PanOffsetProperty);
        set => SetValue(PanOffsetProperty, value);
    }

    public string? CurrentTool
    {
        get => GetValue(CurrentToolProperty);
        set => SetValue(CurrentToolProperty, value);
    }

    public Hex? SelectedHex
    {
        get => GetValue(SelectedHexProperty);
        set => SetValue(SelectedHexProperty, value);
    }

    public IList<MapHex>? VisibleHexes
    {
        get => GetValue(VisibleHexesProperty);
        set => SetValue(VisibleHexesProperty, value);
    }

    public IList<TerrainType>? TerrainTypes
    {
        get => GetValue(TerrainTypesProperty);
        set => SetValue(TerrainTypesProperty, value);
    }

    public TerrainType? SelectedTerrainType
    {
        get => GetValue(SelectedTerrainTypeProperty);
        set => SetValue(SelectedTerrainTypeProperty, value);
    }

    public string? SelectedOverlay
    {
        get => GetValue(SelectedOverlayProperty);
        set => SetValue(SelectedOverlayProperty, value);
    }

    public static readonly StyledProperty<IList<Hex>?> ForageSelectedHexesProperty =
    AvaloniaProperty.Register<HexMapView, IList<Hex>?>(nameof(ForageSelectedHexes));

    public IList<Hex>? ForageSelectedHexes
    {
        get => GetValue(ForageSelectedHexesProperty);
        set => SetValue(ForageSelectedHexesProperty, value);
    }

    public IList<Army>? Armies
    {
        get => GetValue(ArmiesProperty);
        set => SetValue(ArmiesProperty, value);
    }

    public IList<Commander>? Commanders
    {
        get => GetValue(CommandersProperty);
        set => SetValue(CommandersProperty, value);
    }

    public Army? SelectedArmy
    {
        get => GetValue(SelectedArmyProperty);
        set => SetValue(SelectedArmyProperty, value);
    }

    public Commander? SelectedCommander
    {
        get => GetValue(SelectedCommanderProperty);
        set => SetValue(SelectedCommanderProperty, value);
    }

    #endregion

    #region Events

    public event EventHandler<Hex>? HexClicked;
    public event EventHandler<Army>? ArmyClicked;
    public event EventHandler<Commander>? CommanderClicked;
    public event EventHandler<Vector>? PanCompleted;
    public event EventHandler<(Hex hex, int terrainTypeId)>? TerrainPainted;
    public event EventHandler<Hex>? RoadPainted;
    public event EventHandler<Hex>? RiverPainted;
    public event EventHandler<Hex>? EraseRequested;
    public event EventHandler<(Hex hex, string? locationName)>? LocationPainted;
    private void OnForageSelectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        InvalidateVisual();
    }
    #endregion

    #region Cached Geometry

    private double _cachedHexRadius = -1;
    private StreamGeometry? _cachedHexGeometry;
    private Dictionary<int, ISolidColorBrush> _terrainColorCache = new();
    private Dictionary<int, ISolidColorBrush> _factionColorCache = new();

    private static readonly Pen StrokePen = new Pen(Brushes.Black, 1);
    private static readonly Pen RoadPen = new Pen(new SolidColorBrush(Color.Parse("#8B4513")), 3);
    private static readonly Pen RiverPen = new Pen(new SolidColorBrush(Color.Parse("#4169E1")), 4);

    // Selection highlight (semi-transparent yellow ~70% opacity)
    private static readonly ISolidColorBrush SelectionBrush = new SolidColorBrush(Color.FromArgb(180, 255, 255, 0));

    // Army/Commander marker rendering
    private static readonly Pen MarkerOutlinePen = new Pen(Brushes.Black, 2);
    private static readonly Pen CommanderOutlinePen = new Pen(Brushes.White, 2);
    private static readonly Pen SelectionOutlinePen = new Pen(Brushes.Yellow, 3);
    private static readonly ISolidColorBrush DefaultMarkerBrush = new SolidColorBrush(Color.Parse("#808080"));

    #endregion

    #region Transient Interaction State

    private bool _isDragging;
    private AvaloniaPoint _lastPointerPosition;
    private Vector _dragDelta;

    #endregion

    static HexMapView()
    {
        HexRadiusProperty.Changed.AddClassHandler<HexMapView>((view, _) =>
        {
            view._cachedHexRadius = -1;
            view.InvalidateVisual();
        });
        PanOffsetProperty.Changed.AddClassHandler<HexMapView>((view, _) => view.InvalidateVisual());
        SelectedHexProperty.Changed.AddClassHandler<HexMapView>((view, _) => view.InvalidateVisual());
        VisibleHexesProperty.Changed.AddClassHandler<HexMapView>((view, _) =>
        {
            view.RebuildFactionColorCache();
            view.InvalidateVisual();
        });
        TerrainTypesProperty.Changed.AddClassHandler<HexMapView>((view, _) =>
        {
            view.RebuildTerrainColorCache();
            view.InvalidateVisual();
        });
        SelectedOverlayProperty.Changed.AddClassHandler<HexMapView>((view, _) => view.InvalidateVisual());
        ArmiesProperty.Changed.AddClassHandler<HexMapView>((view, _) => view.InvalidateVisual());
        CommandersProperty.Changed.AddClassHandler<HexMapView>((view, _) => view.InvalidateVisual());
        SelectedArmyProperty.Changed.AddClassHandler<HexMapView>((view, _) => view.InvalidateVisual());
        SelectedCommanderProperty.Changed.AddClassHandler<HexMapView>((view, _) => view.InvalidateVisual());
        ForageSelectedHexesProperty.Changed.AddClassHandler<HexMapView>((view, args) =>
        {
            // Unsubscribe from old collection
            if (args.OldValue is System.Collections.Specialized.INotifyCollectionChanged oldCollection)
            {
                oldCollection.CollectionChanged -= view.OnForageSelectionChanged;
            }
            // Subscribe to new collection
            if (args.NewValue is System.Collections.Specialized.INotifyCollectionChanged newCollection)
            {
                newCollection.CollectionChanged += view.OnForageSelectionChanged;
            }
            view.InvalidateVisual();
        });
    }

    public HexMapView()
    {
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
    }

    #region Input Handling

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetPosition(this);
        _lastPointerPosition = point;
        var layout = GetLayout();
        var hex = layout.PixelToHexRounded(point);

        var pointerPoint = e.GetCurrentPoint(this);

        // Right-click always starts panning
        if (pointerPoint.Properties.IsRightButtonPressed)
        {
            _isDragging = true;
            _dragDelta = Vector.Zero;
            InvalidateVisual();
            return;
        }

        // Left-click: tool actions or context-sensitive selection
        if (pointerPoint.Properties.IsLeftButtonPressed)
        {
            // Check if we're in a map editing tool mode
            switch (CurrentTool)
            {
                case "TerrainPaint" when SelectedTerrainType != null:
                    TerrainPainted?.Invoke(this, (hex, SelectedTerrainType.Id));
                    InvalidateVisual();
                    return;
                case "RoadPaint":
                    RoadPainted?.Invoke(this, hex);
                    InvalidateVisual();
                    return;
                case "RiverPaint":
                    RiverPainted?.Invoke(this, hex);
                    InvalidateVisual();
                    return;
                case "Erase":
                    EraseRequested?.Invoke(this, hex);
                    InvalidateVisual();
                    return;
                case "LocationPaint":
                    LocationPainted?.Invoke(this, (hex, null));
                    InvalidateVisual();
                    return;
                case "ForageSelect":
                    HexClicked?.Invoke(this, hex);
                    return;
            }

            // Default: context-sensitive selection
            // Check for army click first (armies are larger markers)
            var clickedArmy = FindArmyAtPoint(point, layout);
            if (clickedArmy != null)
            {
                ArmyClicked?.Invoke(this, clickedArmy);
                InvalidateVisual();
                return;
            }

            // Check for commander click
            var clickedCommander = FindCommanderAtPoint(point, layout);
            if (clickedCommander != null)
            {
                CommanderClicked?.Invoke(this, clickedCommander);
                InvalidateVisual();
                return;
            }

            // Default to hex selection
            HexClicked?.Invoke(this, hex);
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Finds an army at the given screen point, or null if none found.
    /// </summary>
    private Army? FindArmyAtPoint(AvaloniaPoint point, Layout layout)
    {
        var armies = Armies;
        if (armies == null || armies.Count == 0) return null;

        double hitRadius = Math.Max(10, HexRadius * 0.4);

        foreach (var army in armies)
        {
            var hex = new Hex(army.LocationQ, army.LocationR, -army.LocationQ - army.LocationR);
            var center = layout.HexToPixel(hex);
            // Army markers are offset up-left
            var markerCenter = new AvaloniaPoint(center.X - 5, center.Y - 5);

            double distance = Math.Sqrt(
                Math.Pow(point.X - markerCenter.X, 2) +
                Math.Pow(point.Y - markerCenter.Y, 2));

            if (distance <= hitRadius)
                return army;
        }

        return null;
    }

    /// <summary>
    /// Finds a commander at the given screen point, or null if none found.
    /// </summary>
    private Commander? FindCommanderAtPoint(AvaloniaPoint point, Layout layout)
    {
        var commanders = Commanders;
        if (commanders == null || commanders.Count == 0) return null;

        double hitRadius = Math.Max(8, HexRadius * 0.3);

        foreach (var commander in commanders)
        {
            if (commander.LocationQ == null || commander.LocationR == null)
                continue;

            var hex = new Hex(commander.LocationQ.Value, commander.LocationR.Value,
                -commander.LocationQ.Value - commander.LocationR.Value);
            var center = layout.HexToPixel(hex);
            // Commander markers are offset down-right
            var markerCenter = new AvaloniaPoint(center.X + 5, center.Y + 5);

            double distance = Math.Sqrt(
                Math.Pow(point.X - markerCenter.X, 2) +
                Math.Pow(point.Y - markerCenter.Y, 2));

            if (distance <= hitRadius)
                return commander;
        }

        return null;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging) return;

        var currentPosition = e.GetPosition(this);
        _dragDelta += currentPosition - _lastPointerPosition;
        _lastPointerPosition = currentPosition;
        InvalidateVisual();
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isDragging && _dragDelta != Vector.Zero)
        {
            PanCompleted?.Invoke(this, _dragDelta);
            _dragDelta = Vector.Zero;
        }
        _isDragging = false;
    }

    #endregion

    #region Rendering

    public override void Render(DrawingContext context)
    {
        var layout = GetLayout();
        var viewport = new Rect(Bounds.Size);

        context.FillRectangle(Brushes.Gray, viewport);
        base.Render(context);

        if (_cachedHexRadius != HexRadius)
        {
            _cachedHexGeometry = BuildHexGeometry(layout);
            _cachedHexRadius = HexRadius;
        }

        var hexes = VisibleHexes;
        if (hexes == null || hexes.Count == 0 || _cachedHexGeometry == null)
            return;

        using (context.PushClip(viewport))
        {
            foreach (var mapHex in hexes)
            {
                DrawHex(context, layout, mapHex, viewport);
            }

            // Render army and commander markers on top of hexes
            RenderArmyMarkers(context, layout, viewport);
            RenderCommanderMarkers(context, layout, viewport);
        }
    }

    private void DrawHex(DrawingContext context, Layout layout, MapHex mapHex, Rect viewport)
    {
        var hex = mapHex.ToHex();
        var rowcolcoord = OffsetCoord.QoffsetFromCube(OffsetCoord.ODD, hex);
        var center = layout.HexToPixel(hex);
        var corners = layout.PolygonCorners(hex);

        // Viewport culling
        double minX = corners.Min(c => c.X);
        double maxX = corners.Max(c => c.X);
        double minY = corners.Min(c => c.Y);
        double maxY = corners.Max(c => c.Y);

        if (maxX < viewport.Left || minX > viewport.Right ||
            maxY < viewport.Top || minY > viewport.Bottom)
            return;

        // Determine if hex is selected
        bool isSelected = SelectedHex.HasValue &&
                          SelectedHex.Value.q == mapHex.Q &&
                          SelectedHex.Value.r == mapHex.R;

        // Determine if we are currently using the Forage tool
        bool isForageSelected = ForageSelectedHexes != null &&
            ForageSelectedHexes.Any(fh => fh.q == mapHex.Q && fh.r == mapHex.R);

        // Layer rendering: terrain → overlay → selection
        var terrainFill = GetTerrainBrush(mapHex.TerrainTypeId);
        var overlayBrush = GetOverlayBrushWithAlpha(mapHex);

        // Draw hex geometry with layered fills
        var translateMatrix = Matrix.CreateTranslation(center.X, center.Y);
        using (context.PushTransform(translateMatrix))
        {
            // 1. Draw terrain base layer (always)
            context.DrawGeometry(terrainFill, StrokePen, _cachedHexGeometry!);

            // 2. Draw overlay on top (if not "None")
            if (overlayBrush != null)
            {
                context.DrawGeometry(overlayBrush, null, _cachedHexGeometry!);
            }

            // 3. Draw selection highlight last (if selected)
            if (isSelected)
            {
                context.DrawGeometry(SelectionBrush, null, _cachedHexGeometry!);
            }

            // 4. Draw forage selection highlight
            if (isForageSelected)
            {
                context.DrawGeometry(SelectionBrush, null, _cachedHexGeometry!);
            }

            // Draw coordinate label
            var text = new FormattedText(
                $"{rowcolcoord.col},{rowcolcoord.row}",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                Typeface.Default,
                8.0,
                Brushes.Black);
            context.DrawText(text, new AvaloniaPoint(-text.Width / 2, -text.Height / 2));
        }

        // Draw roads and rivers
        RenderRoads(context, mapHex, center);
        RenderRivers(context, mapHex, corners);
    }

    private void RenderRoads(DrawingContext context, MapHex mapHex, AvaloniaPoint center)
    {
        if (string.IsNullOrEmpty(mapHex.RoadDirections)) return;

        var layout = GetLayout();
        var hex = mapHex.ToHex();

        for (int dir = 0; dir < 6; dir++)
        {
            if (mapHex.HasRoadInDirection(dir))
            {
                var neighborCenter = layout.HexToPixel(hex.Neighbor(dir));
                context.DrawLine(RoadPen, center, neighborCenter);
            }
        }
    }

    private void RenderRivers(DrawingContext context, MapHex mapHex, List<AvaloniaPoint> corners)
    {
        if (string.IsNullOrEmpty(mapHex.RiverEdges)) return;

        for (int dir = 0; dir < 6; dir++)
        {
            if (mapHex.HasRiverOnEdge(dir))
            {
                int cornerIdx = (dir + 5) % 6;
                var corner1 = corners[cornerIdx];
                var corner2 = corners[(cornerIdx + 1) % 6];
                context.DrawLine(RiverPen, corner1, corner2);
            }
        }
    }

    /// <summary>
    /// Renders army markers as filled circles at their hex locations.
    /// Armies are rendered as larger circles offset slightly to avoid overlap with commanders.
    /// </summary>
    private void RenderArmyMarkers(DrawingContext context, Layout layout, Rect viewport)
    {
        var armies = Armies;
        if (armies == null || armies.Count == 0) return;

        foreach (var army in armies)
        {
            var hex = new Hex(army.LocationQ, army.LocationR, -army.LocationQ - army.LocationR);
            var center = layout.HexToPixel(hex);

            // Viewport culling
            if (center.X < viewport.Left - 20 || center.X > viewport.Right + 20 ||
                center.Y < viewport.Top - 20 || center.Y > viewport.Bottom + 20)
                continue;

            // Get faction color or default gray
            var factionBrush = GetArmyMarkerBrush(army);

            // Draw army marker: filled circle with black outline
            // Offset slightly up-left from hex center
            var markerCenter = new AvaloniaPoint(center.X - 5, center.Y - 5);
            double markerRadius = Math.Max(6, HexRadius * 0.35);

            // Check if this army is selected - draw selection highlight
            bool isSelected = SelectedArmy != null && SelectedArmy.Id == army.Id;
            if (isSelected)
            {
                // Draw yellow selection ring behind the marker
                context.DrawEllipse(null, SelectionOutlinePen, markerCenter, markerRadius + 3, markerRadius + 3);
            }

            context.DrawEllipse(factionBrush, MarkerOutlinePen, markerCenter, markerRadius, markerRadius);

            // Draw army initial letter in the marker
            if (!string.IsNullOrEmpty(army.Name))
            {
                var initial = army.Name[0].ToString().ToUpperInvariant();
                var text = new FormattedText(
                    initial,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    Typeface.Default,
                    Math.Max(8, markerRadius * 1.2),
                    Brushes.White);
                context.DrawText(text, new AvaloniaPoint(
                    markerCenter.X - text.Width / 2,
                    markerCenter.Y - text.Height / 2));
            }
        }
    }

    /// <summary>
    /// Renders commander markers as smaller diamond shapes at their hex locations.
    /// </summary>
    private void RenderCommanderMarkers(DrawingContext context, Layout layout, Rect viewport)
    {
        var commanders = Commanders;
        if (commanders == null || commanders.Count == 0) return;

        foreach (var commander in commanders)
        {
            // Skip commanders without a location
            if (commander.LocationQ == null || commander.LocationR == null)
                continue;

            var hex = new Hex(commander.LocationQ.Value, commander.LocationR.Value,
                -commander.LocationQ.Value - commander.LocationR.Value);
            var center = layout.HexToPixel(hex);

            // Viewport culling
            if (center.X < viewport.Left - 20 || center.X > viewport.Right + 20 ||
                center.Y < viewport.Top - 20 || center.Y > viewport.Bottom + 20)
                continue;

            // Get faction color or default gray
            var factionBrush = GetCommanderMarkerBrush(commander);

            // Draw commander marker: small diamond offset down-right from hex center
            var markerCenter = new AvaloniaPoint(center.X + 5, center.Y + 5);
            double markerSize = Math.Max(5, HexRadius * 0.25);

            // Check if this commander is selected
            bool isSelected = SelectedCommander != null && SelectedCommander.Id == commander.Id;

            // Draw selection highlight first (larger diamond behind)
            if (isSelected)
            {
                double selectionSize = markerSize + 3;
                var selectionGeom = new StreamGeometry();
                using (var ctx = selectionGeom.Open())
                {
                    ctx.BeginFigure(new AvaloniaPoint(markerCenter.X, markerCenter.Y - selectionSize), true);
                    ctx.LineTo(new AvaloniaPoint(markerCenter.X + selectionSize, markerCenter.Y));
                    ctx.LineTo(new AvaloniaPoint(markerCenter.X, markerCenter.Y + selectionSize));
                    ctx.LineTo(new AvaloniaPoint(markerCenter.X - selectionSize, markerCenter.Y));
                    ctx.EndFigure(true);
                }
                context.DrawGeometry(null, SelectionOutlinePen, selectionGeom);
            }

            // Draw diamond shape
            var diamondGeom = new StreamGeometry();
            using (var ctx = diamondGeom.Open())
            {
                ctx.BeginFigure(new AvaloniaPoint(markerCenter.X, markerCenter.Y - markerSize), true);
                ctx.LineTo(new AvaloniaPoint(markerCenter.X + markerSize, markerCenter.Y));
                ctx.LineTo(new AvaloniaPoint(markerCenter.X, markerCenter.Y + markerSize));
                ctx.LineTo(new AvaloniaPoint(markerCenter.X - markerSize, markerCenter.Y));
                ctx.EndFigure(true);
            }

            context.DrawGeometry(factionBrush, CommanderOutlinePen, diamondGeom);
        }
    }

    /// <summary>
    /// Gets the brush for an army marker based on its faction color.
    /// </summary>
    private ISolidColorBrush GetArmyMarkerBrush(Army army)
    {
        if (army.Faction == null)
            return DefaultMarkerBrush;

        if (_factionColorCache.TryGetValue(army.FactionId, out var brush))
            return brush;

        try
        {
            brush = new SolidColorBrush(Color.Parse(army.Faction.ColorHex));
            _factionColorCache[army.FactionId] = brush;
            return brush;
        }
        catch
        {
            return DefaultMarkerBrush;
        }
    }

    /// <summary>
    /// Gets the brush for a commander marker based on their faction color.
    /// </summary>
    private ISolidColorBrush GetCommanderMarkerBrush(Commander commander)
    {
        if (commander.Faction == null)
            return DefaultMarkerBrush;

        if (_factionColorCache.TryGetValue(commander.FactionId, out var brush))
            return brush;

        try
        {
            brush = new SolidColorBrush(Color.Parse(commander.Faction.ColorHex));
            _factionColorCache[commander.FactionId] = brush;
            return brush;
        }
        catch
        {
            return DefaultMarkerBrush;
        }
    }

    #endregion

    #region Helpers

    private Layout GetLayout()
    {
        var effectiveOffset = PanOffset + _dragDelta;
        return new Layout(
            Layout.flat,
            new AvaloniaPoint(HexRadius, HexRadius),
            new AvaloniaPoint(HexRadius + effectiveOffset.X, HexRadius + effectiveOffset.Y)
        );
    }

    private StreamGeometry BuildHexGeometry(Layout layout)
    {
        var corners = layout.PolygonCorners(new Hex(0, 0, 0));
        var origin = layout.origin;

        var geom = new StreamGeometry();
        using (var ctx = geom.Open())
        {
            ctx.BeginFigure(new AvaloniaPoint(corners[0].X - origin.X, corners[0].Y - origin.Y), true);
            for (int i = 1; i < corners.Count; i++)
            {
                ctx.LineTo(new AvaloniaPoint(corners[i].X - origin.X, corners[i].Y - origin.Y));
            }
            ctx.EndFigure(true);
        }
        return geom;
    }

    private void RebuildTerrainColorCache()
    {
        _terrainColorCache.Clear();
        var terrainTypes = TerrainTypes;
        if (terrainTypes == null) return;

        foreach (var terrain in terrainTypes)
        {
            try
            {
                _terrainColorCache[terrain.Id] = new SolidColorBrush(Color.Parse(terrain.ColorHex));
            }
            catch
            {
                _terrainColorCache[terrain.Id] = Brushes.Gray;
            }
        }
    }

    private ISolidColorBrush GetTerrainBrush(int? terrainTypeId)
    {
        if (terrainTypeId.HasValue && _terrainColorCache.TryGetValue(terrainTypeId.Value, out var brush))
            return brush;
        return Brushes.White;
    }

    /// <summary>
    /// Returns semi-transparent overlay brush based on selected overlay mode.
    /// Returns null for "None" (no overlay should be drawn on top of terrain).
    /// </summary>
    private ISolidColorBrush? GetOverlayBrushWithAlpha(MapHex mapHex)
    {
        return SelectedOverlay switch
        {
            "Faction Control" => GetFactionBrushWithAlpha(mapHex),
            "Population Density" => GetPopulationBrushWithAlpha(mapHex.PopulationDensity),
            "Times Foraged" => GetForagedBrushWithAlpha(mapHex.TimesForaged),
            "Weather" => GetWeatherBrushWithAlpha(mapHex),
            _ => null // "None" - no overlay
        };
    }

    /// <summary>
    /// Returns semi-transparent faction color for overlay (~50% opacity).
    /// Uses cache for faction colors.
    /// </summary>
    private ISolidColorBrush GetFactionBrushWithAlpha(MapHex mapHex)
    {
        if (!mapHex.ControllingFactionId.HasValue)
            return new SolidColorBrush(Color.FromArgb(191, 128, 128, 128)); // Semi-transparent gray

        // Get base color from cache or parse from faction
        Color baseColor;
        if (_factionColorCache.TryGetValue(mapHex.ControllingFactionId.Value, out var cachedBrush))
        {
            baseColor = cachedBrush.Color;
        }
        else if (mapHex.ControllingFaction != null)
        {
            try
            {
                baseColor = Color.Parse(mapHex.ControllingFaction.ColorHex);
                _factionColorCache[mapHex.ControllingFactionId.Value] = new SolidColorBrush(baseColor);
            }
            catch
            {
                baseColor = Color.Parse("#808080"); // Gray fallback
            }
        }
        else
        {
            baseColor = Color.Parse("#808080"); // Gray fallback
        }

        return new SolidColorBrush(Color.FromArgb(191, baseColor.R, baseColor.G, baseColor.B));
    }

    /// <summary>
    /// Semi-transparent heat map for population density overlay (~50% opacity).
    /// </summary>
    private static ISolidColorBrush GetPopulationBrushWithAlpha(int density)
    {
        // Clamp to 0-100
        density = Math.Max(0, Math.Min(100, density));

        // Interpolate from light blue (#ADD8E6) to deep red (#8B0000)
        double t = density / 100.0;

        byte r = (byte)(173 + (139 - 173) * t);
        byte g = (byte)(216 + (0 - 216) * t);
        byte b = (byte)(230 + (0 - 230) * t);

        return new SolidColorBrush(Color.FromArgb(128, r, g, b));
    }

    /// <summary>
    /// Semi-transparent foraged gradient overlay (~50% opacity).
    /// </summary>
    private static ISolidColorBrush GetForagedBrushWithAlpha(int timesForaged)
    {
        // Clamp to 0-10
        timesForaged = Math.Max(0, Math.Min(10, timesForaged));

        // Interpolate from green (#228B22) to brown (#8B4513)
        double t = timesForaged / 10.0;

        byte r = (byte)(34 + (139 - 34) * t);
        byte g = (byte)(139 + (69 - 139) * t);
        byte b = (byte)(34 + (19 - 34) * t);

        return new SolidColorBrush(Color.FromArgb(128, r, g, b));
    }

    /// <summary>
    /// Returns semi-transparent weather color overlay (~50% opacity).
    /// </summary>
    private static ISolidColorBrush GetWeatherBrushWithAlpha(MapHex mapHex)
    {
        if (!mapHex.WeatherId.HasValue || mapHex.Weather == null)
            return new SolidColorBrush(Color.FromArgb(128, 240, 240, 240)); // Semi-transparent off-white

        var weatherName = mapHex.Weather.Name?.ToLowerInvariant() ?? "";

        return weatherName switch
        {
            "clear" => new SolidColorBrush(Color.FromArgb(128, 135, 206, 235)),  // Light blue
            "rain" => new SolidColorBrush(Color.FromArgb(128, 65, 105, 225)),    // Dark blue
            "storm" => new SolidColorBrush(Color.FromArgb(128, 139, 0, 139)),    // Purple
            "snow" => new SolidColorBrush(Color.FromArgb(128, 255, 250, 250)),   // White
            "fog" => new SolidColorBrush(Color.FromArgb(128, 211, 211, 211)),    // Light gray
            _ => new SolidColorBrush(Color.FromArgb(128, 240, 240, 240))         // Off-white
        };
    }

    /// <summary>
    /// Rebuilds the faction color cache from visible hexes.
    /// </summary>
    private void RebuildFactionColorCache()
    {
        _factionColorCache.Clear();
        var hexes = VisibleHexes;
        if (hexes == null) return;

        foreach (var hex in hexes)
        {
            if (hex.ControllingFactionId.HasValue && hex.ControllingFaction != null)
            {
                if (!_factionColorCache.ContainsKey(hex.ControllingFactionId.Value))
                {
                    try
                    {
                        _factionColorCache[hex.ControllingFactionId.Value] =
                            new SolidColorBrush(Color.Parse(hex.ControllingFaction.ColorHex));
                    }
                    catch
                    {
                        _factionColorCache[hex.ControllingFactionId.Value] = Brushes.Gray;
                    }
                }
            }
        }
    }

    #endregion
}
