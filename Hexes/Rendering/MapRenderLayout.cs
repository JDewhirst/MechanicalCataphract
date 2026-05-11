using System;
using System.Collections.Generic;

namespace MechanicalCataphract.Rendering;

public enum MarkerPosition
{
    Center,
    TopRight,
    BottomRight
}

public readonly record struct RenderPoint(double X, double Y);

public readonly record struct RenderSize(double Width, double Height);

public readonly record struct RenderRect(double X, double Y, double Width, double Height);

public static class MapRenderLayout
{
    public const double StackOffsetX = 4.0;
    public const double StackOffsetY = 6.0;
    public const double LocationIconScaleMultiplier = 2.0;

    public static Dictionary<(int q, int r), List<T>> GroupEntitiesByHex<T>(
        IEnumerable<T>? entities,
        Func<T, (int? q, int? r)> getLocation)
    {
        var groups = new Dictionary<(int q, int r), List<T>>();
        if (entities == null) return groups;

        foreach (var entity in entities)
        {
            var loc = getLocation(entity);
            if (!loc.q.HasValue || !loc.r.HasValue) continue;

            var key = (loc.q.Value, loc.r.Value);
            if (!groups.TryGetValue(key, out var group))
            {
                group = new List<T>();
                groups[key] = group;
            }

            group.Add(entity);
        }

        return groups;
    }

    public static RenderPoint GetMarkerOffset(MarkerPosition position, double hexRadius)
    {
        double hexHeight = Math.Sqrt(3) * hexRadius;
        return position switch
        {
            MarkerPosition.Center => new RenderPoint(0, 0),
            MarkerPosition.TopRight => new RenderPoint(hexRadius * 0.4, -hexHeight * 0.35),
            MarkerPosition.BottomRight => new RenderPoint(hexRadius * 0.4, hexHeight * 0.35),
            _ => new RenderPoint(0, 0)
        };
    }

    public static RenderPoint GetStackedMarkerCenter(RenderPoint hexCenter, RenderPoint baseOffset, int stackIndex)
    {
        return new RenderPoint(
            hexCenter.X + baseOffset.X - (stackIndex * StackOffsetX),
            hexCenter.Y + baseOffset.Y - (stackIndex * StackOffsetY));
    }

    public static RenderRect GetIconDestination(
        RenderPoint center,
        double hexRadius,
        double sourceWidth,
        double sourceHeight,
        double scaleFactor,
        double scaleMultiplier = 1.0)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0)
            return new RenderRect(center.X, center.Y, 0, 0);

        double hexHeight = Math.Sqrt(3) * hexRadius;
        double maxIconSize = hexHeight * scaleFactor * scaleMultiplier;
        double scale = maxIconSize / Math.Max(sourceWidth, sourceHeight);
        double drawWidth = sourceWidth * scale;
        double drawHeight = sourceHeight * scale;

        return new RenderRect(
            center.X - drawWidth / 2,
            center.Y - drawHeight / 2,
            drawWidth,
            drawHeight);
    }

    public static RenderSize GetStackBounds(int itemCount, RenderPoint baseOffset, double hitRadius)
    {
        return new RenderSize(
            Math.Abs(baseOffset.X) + (itemCount * StackOffsetX) + hitRadius,
            Math.Abs(baseOffset.Y) + (itemCount * StackOffsetY) + hitRadius);
    }
}
