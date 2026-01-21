using System;
using System.Collections.Generic;
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

    #endregion

    #region Events

    public event EventHandler<Hex>? HexClicked;
    public event EventHandler<Vector>? PanCompleted;
    public event EventHandler<(Hex hex, int terrainTypeId)>? TerrainPainted;
    public event EventHandler<Hex>? RoadPainted;
    public event EventHandler<Hex>? RiverPainted;
    public event EventHandler<Hex>? EraseRequested;
    public event EventHandler<(Hex hex, string? locationName)>? LocationPainted;

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
        var hex = GetLayout().PixelToHexRounded(point);

        switch (CurrentTool)
        {
            case "Select":
                HexClicked?.Invoke(this, hex);
                break;
            case "Pan":
                _isDragging = true;
                _dragDelta = Vector.Zero;
                break;
            case "TerrainPaint" when SelectedTerrainType != null:
                TerrainPainted?.Invoke(this, (hex, SelectedTerrainType.Id));
                break;
            case "RoadPaint":
                RoadPainted?.Invoke(this, hex);
                break;
            case "RiverPaint":
                RiverPainted?.Invoke(this, hex);
                break;
            case "Erase":
                EraseRequested?.Invoke(this, hex);
                break;
            case "LocationPaint":
                // For now, use the location type name as the location name
                // TODO: Add UI to input custom location name
                LocationPainted?.Invoke(this, (hex, null));
                break;
        }

        InvalidateVisual();
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
            return new SolidColorBrush(Color.FromArgb(128, 128, 128, 128)); // Semi-transparent gray

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

        return new SolidColorBrush(Color.FromArgb(128, baseColor.R, baseColor.G, baseColor.B));
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
