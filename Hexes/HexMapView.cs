using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using System;
using AvaloniaPoint = Avalonia.Point;
using Hexes;
using System.Globalization;

namespace GUI;

public class HexMapView : Control
{
    #region Avalonia Properties

    public static readonly StyledProperty<HexMapModel?> MapModelProperty =
        AvaloniaProperty.Register<HexMapView, HexMapModel?>(nameof(MapModel));

    public static readonly StyledProperty<double> HexRadiusProperty =
        AvaloniaProperty.Register<HexMapView, double>(nameof(HexRadius), defaultValue: 20.0);

    public static readonly StyledProperty<Vector> PanOffsetProperty =
        AvaloniaProperty.Register<HexMapView, Vector>(nameof(PanOffset));

    public static readonly StyledProperty<string?> CurrentToolProperty =
        AvaloniaProperty.Register<HexMapView, string?>(nameof(CurrentTool), defaultValue: "Pan");

    public static readonly StyledProperty<Hex?> SelectedHexProperty =
        AvaloniaProperty.Register<HexMapView, Hex?>(nameof(SelectedHex));

    public HexMapModel? MapModel
    {
        get => GetValue(MapModelProperty);
        set => SetValue(MapModelProperty, value);
    }

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

    #endregion

    #region Events

    public event EventHandler<Hex>? HexClicked;
    public event EventHandler<Vector>? PanCompleted;

    #endregion

    #region Cached Geometry (rendering optimization)

    private double _cachedHexRadius = -1;
    private StreamGeometry? _cachedHexGeometry;
    private double _cachedHexWidth;
    private double _cachedHexHeight;

    #endregion

    #region Transient Interaction State

    private bool _isDragging = false;
    private AvaloniaPoint _lastPointerPosition = new(0, 0);
    private Vector _dragDelta = Vector.Zero;

    #endregion

    static HexMapView()
    {
        HexRadiusProperty.Changed.AddClassHandler<HexMapView>(OnHexRadiusChanged);
        PanOffsetProperty.Changed.AddClassHandler<HexMapView>(OnPanOffsetChanged);
        MapModelProperty.Changed.AddClassHandler<HexMapView>(OnMapModelChanged);
        SelectedHexProperty.Changed.AddClassHandler<HexMapView>(OnSelectedHexChanged);
    }

    private static void OnHexRadiusChanged(HexMapView view, AvaloniaPropertyChangedEventArgs e)
    {
        view._cachedHexRadius = -1; // Invalidate geometry cache
        view.InvalidateVisual();
    }

    private static void OnPanOffsetChanged(HexMapView view, AvaloniaPropertyChangedEventArgs e)
    {
        view.InvalidateVisual();
    }

    private static void OnMapModelChanged(HexMapView view, AvaloniaPropertyChangedEventArgs e)
    {
        view.InvalidateVisual();
    }

    private static void OnSelectedHexChanged(HexMapView view, AvaloniaPropertyChangedEventArgs e)
    {
        view.InvalidateVisual();
    }

    public HexMapView()
    {
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetPosition(this);
        _lastPointerPosition = point;

        if (CurrentTool == "Select")
        {
            var layout = GetLayout();
            var hex = layout.PixelToHexRounded(point);
            HexClicked?.Invoke(this, hex);
        }
        else if (CurrentTool == "Pan")
        {
            _isDragging = true;
            _dragDelta = Vector.Zero;
        }

        InvalidateVisual();
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging)
            return;

        var currentPosition = e.GetPosition(this);
        var delta = currentPosition - _lastPointerPosition;
        _dragDelta += delta;
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

    private Layout GetLayout()
    {
        var effectiveOffset = PanOffset + _dragDelta;
        return new Layout(
            Layout.flat,
            new AvaloniaPoint(HexRadius, HexRadius),
            new AvaloniaPoint(HexRadius + effectiveOffset.X, HexRadius + effectiveOffset.Y)
        );
    }

    private (int minCol, int maxCol, int minRow, int maxRow) CalculateVisibleHexRange(
        Rect viewportRect,
        Layout layout)
    {
        var mapModel = MapModel;
        if (mapModel == null)
            return (0, 0, 0, 0);

        double hexWidth = HexRadius * 1.5;
        double hexHeight = HexRadius * Math.Sqrt(3);

        double margin = Math.Max(hexWidth, hexHeight) * 2;

        var effectiveOffset = PanOffset + _dragDelta;
        double viewportLeft = -effectiveOffset.X - margin;
        double viewportRight = -effectiveOffset.X + viewportRect.Width + margin;
        double viewportTop = -effectiveOffset.Y - margin;
        double viewportBottom = -effectiveOffset.Y + viewportRect.Height + margin;

        int minCol = Math.Max(0, (int)(viewportLeft / hexWidth));
        int maxCol = Math.Min(mapModel.Columns - 1, (int)(viewportRight / hexWidth) + 2);

        int minRow = Math.Max(0, (int)(viewportTop / hexHeight));
        int maxRow = Math.Min(mapModel.Rows - 1, (int)(viewportBottom / hexHeight) + 2);

        return (minCol, maxCol, minRow, maxRow);
    }

    public override void Render(DrawingContext context)
    {
        // To Do Later:
        // when we have more than one type of thing,
        // Render order:
        // Hex (including terrain icons)
        // Locations (icons in hex centre)
        // Overlays (toggleable, one at a time):
            // Faction control by hex
            // Hex population
            // Times hex foraged
            // Weather
        // Armies/Commanders (toggleable)
            // Rectangular Icon with Faction colour border
                // If Army, gives #troops with army name along bottom edge
                // If commander, commander name in center.
            // If only one faction, stacked like game pieces with an ofset to read how many
            // If more than one faction, multiple stacks in the same hex.
        // Messengers (toggleable)
            // Display consistently as a stack in one hex-corner
        var mapModel = MapModel;
        if (mapModel == null)
        {
            base.Render(context);
            return;
        }

        var layout = GetLayout();

        var viewportRect = new Rect(Bounds.Size);
        context.FillRectangle(Brushes.Gray, viewportRect);

        base.Render(context);

        var stroke = new Pen(Brushes.Black, 1);
        var fill = Brushes.Transparent;

        if (_cachedHexRadius != HexRadius)
        {
            _cachedHexGeometry = BuildHexGeometry(layout);
            _cachedHexRadius = HexRadius;
            _cachedHexWidth = HexRadius * 1.5;
            _cachedHexHeight = HexRadius * Math.Sqrt(3);
        }

        using (context.PushClip(viewportRect))
        {
            var (minCol, maxCol, minRow, maxRow) = CalculateVisibleHexRange(viewportRect, layout);

            for (int row = minRow; row <= maxRow; row++)
            {
                for (int col = minCol; col <= maxCol; col++)
                {
                    var offset = new OffsetCoord(col, row);
                    Hex hex = OffsetCoord.QoffsetToCube(OffsetCoord.ODD, offset);

                    DrawHex(context, layout, hex, stroke, fill, viewportRect, mapModel);
                }
            }
        }
    }

    private StreamGeometry BuildHexGeometry(Layout layout)
    {
        var templateHex = new Hex(0, 0, 0);
        var corners = layout.PolygonCorners(templateHex);

        if (corners.Count == 0)
            return new StreamGeometry();

        var origin = layout.origin;

        var streamGeom = new StreamGeometry();
        using (var ctx = streamGeom.Open())
        {
            var firstPoint = new AvaloniaPoint(corners[0].X - origin.X, corners[0].Y - origin.Y);
            ctx.BeginFigure(firstPoint, true);

            for (int i = 1; i < corners.Count; i++)
            {
                ctx.LineTo(new AvaloniaPoint(corners[i].X - origin.X, corners[i].Y - origin.Y));
            }

            ctx.EndFigure(true);
        }

        return streamGeom;
    }

    private void DrawHex(DrawingContext context,
                         Layout layout,
                         Hex hex,
                         Pen stroke,
                         IBrush fill,
                         Rect viewport,
                         HexMapModel mapModel)
    {
        var corners = layout.PolygonCorners(hex);
        if (corners.Count == 0)
            return;

        double minX = corners[0].X, maxX = corners[0].X;
        double minY = corners[0].Y, maxY = corners[0].Y;

        for (int i = 1; i < corners.Count; i++)
        {
            if (corners[i].X < minX) minX = corners[i].X;
            if (corners[i].X > maxX) maxX = corners[i].X;
            if (corners[i].Y < minY) minY = corners[i].Y;
            if (corners[i].Y > maxY) maxY = corners[i].Y;
        }

        if (maxX < viewport.Left || minX > viewport.Right ||
            maxY < viewport.Top || minY > viewport.Bottom)
        {
            return;
        }

        bool isSelected = false;
        if (SelectedHex.HasValue)
        {
            var s = SelectedHex.Value;
            isSelected = (s.q == hex.q && s.r == hex.r && s.s == hex.s);
        }

        IBrush actualFill = isSelected ? Brushes.Yellow : Brushes.White;

        if (_cachedHexGeometry != null)
        {
            var centerPixel = layout.HexToPixel(hex);

            var translateMatrix = Matrix.CreateTranslation(centerPixel.X, centerPixel.Y);

            using (context.PushTransform(translateMatrix))
            {
                context.DrawGeometry(actualFill, stroke, _cachedHexGeometry);

                if (mapModel._tiles.TryGetValue(hex, out var tile))
                {
                    context.DrawText(
                        new FormattedText(
                            $"{tile.Terrain}",
                            CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            Typeface.Default,
                            10.0,
                            Brushes.Black),
                        _cachedHexGeometry.Bounds.TopLeft);
                }
            }
        }
    }
}
