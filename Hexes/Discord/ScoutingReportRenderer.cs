using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hexes;
using MechanicalCataphract.Data.Entities;
using SkiaSharp;

namespace MechanicalCataphract.Discord;

public static class ScoutingReportRenderer
{
    private const float HexRadius = 30f;
    private const float Padding = 40f;

    public static SKBitmap RenderScoutingReport(
        IList<MapHex> hexes,
        IList<TerrainType> terrainTypes,
        IList<LocationType> locationTypes,
        IList<Army> armies,
        Hex centerHex,
        int scoutingRange)
    {
        // Build lookup dictionaries
        var terrainById = terrainTypes.ToDictionary(t => t.Id);
        var locationById = locationTypes.ToDictionary(l => l.Id);

        // Create layout centered on origin; we'll compute offsets after measuring
        var layout = new Layout(
            Layout.flat,
            new Avalonia.Point(HexRadius, HexRadius),
            new Avalonia.Point(0, 0));

        // Compute pixel positions for all hexes to determine image bounds
        var hexPixels = hexes.Select(h =>
        {
            var hex = h.ToHex();
            var center = layout.HexToPixel(hex);
            return (mapHex: h, hex, centerX: (float)center.X, centerY: (float)center.Y);
        }).ToList();

        if (hexPixels.Count == 0)
            return new SKBitmap(1, 1);

        float minX = hexPixels.Min(h => h.centerX) - HexRadius - Padding;
        float minY = hexPixels.Min(h => h.centerY) - HexRadius - Padding;
        float maxX = hexPixels.Max(h => h.centerX) + HexRadius + Padding;
        float maxY = hexPixels.Max(h => h.centerY) + HexRadius + Padding;

        int width = (int)Math.Ceiling(maxX - minX);
        int height = (int)Math.Ceiling(maxY - minY);

        // Offset so all hexes are in positive coordinate space
        float offsetX = -minX;
        float offsetY = -minY;

        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Gray);

        // Load icon caches
        var terrainIcons = new Dictionary<int, SKBitmap>();
        var locationIcons = new Dictionary<int, SKBitmap>();
        try
        {
            foreach (var terrain in terrainTypes)
            {
                if (terrain.IsUseIcon && !string.IsNullOrEmpty(terrain.IconPath))
                {
                    var icon = LoadIcon(terrain.IconPath);
                    if (icon != null) terrainIcons[terrain.Id] = icon;
                }
            }

            foreach (var locType in locationTypes)
            {
                if (!string.IsNullOrEmpty(locType.IconPath))
                {
                    var icon = LoadIcon(locType.IconPath);
                    if (icon != null) locationIcons[locType.Id] = icon;
                }
            }

            // Pass 1: Hex backgrounds, terrain icons, outlines, coordinate labels
            foreach (var (mapHex, hex, cx, cy) in hexPixels)
            {
                float drawX = cx + offsetX;
                float drawY = cy + offsetY;

                var corners = GetHexCorners(drawX, drawY);

                // Fill with terrain color
                var fillColor = SKColors.White;
                if (mapHex.TerrainTypeId.HasValue && terrainById.TryGetValue(mapHex.TerrainTypeId.Value, out var terrain))
                {
                    fillColor = ParseColor(terrain.ColorHex);
                }

                using var fillPaint = new SKPaint { Color = fillColor, Style = SKPaintStyle.Fill, IsAntialias = true };
                using var strokePaint = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true };

                var path = CornersToPath(corners);
                canvas.DrawPath(path, fillPaint);

                // Terrain icon
                if (mapHex.TerrainTypeId.HasValue && terrainIcons.TryGetValue(mapHex.TerrainTypeId.Value, out var terrainIcon))
                {
                    double scaleFactor = terrainById.TryGetValue(mapHex.TerrainTypeId.Value, out var t) ? t.ScaleFactor : 0.82;
                    DrawIcon(canvas, terrainIcon, drawX, drawY, scaleFactor);
                }

                canvas.DrawPath(path, strokePaint);

                // Coordinate label
                var offset = OffsetCoord.QoffsetFromCube(OffsetCoord.ODD, hex);
                string label = $"{offset.col},{offset.row}";
                float fontSize = Math.Max(6f, HexRadius * 0.4f);
                using var labelFont = new SKFont(SKTypeface.Default, fontSize);
                using var labelPaint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
                float textWidth = labelFont.MeasureText(label);
                float hexHeight = (float)(Math.Sqrt(3) * HexRadius);
                float textY = drawY + hexHeight / 2 - 4;
                canvas.DrawText(label, drawX - textWidth / 2, textY, SKTextAlign.Left, labelFont, labelPaint);
            }

            // Pass 2: Roads and rivers
            using var roadPaint = new SKPaint { Color = SKColor.Parse("#8B4513"), Style = SKPaintStyle.Stroke, StrokeWidth = 3, IsAntialias = true };
            using var riverPaint = new SKPaint { Color = SKColor.Parse("#4169E1"), Style = SKPaintStyle.Stroke, StrokeWidth = 4, IsAntialias = true };

            foreach (var (mapHex, hex, cx, cy) in hexPixels)
            {
                float drawX = cx + offsetX;
                float drawY = cy + offsetY;

                // Rivers (on hex edges)
                if (!string.IsNullOrEmpty(mapHex.RiverEdges))
                {
                    var corners = GetHexCorners(drawX, drawY);
                    for (int dir = 0; dir < 6; dir++)
                    {
                        if (mapHex.HasRiverOnEdge(dir))
                        {
                            int cornerIdx = (dir + 5) % 6;
                            var c1 = corners[cornerIdx];
                            var c2 = corners[(cornerIdx + 1) % 6];
                            canvas.DrawLine(c1, c2, riverPaint);
                        }
                    }
                }

                // Roads (from center toward neighbor)
                if (!string.IsNullOrEmpty(mapHex.RoadDirections))
                {
                    for (int dir = 0; dir < 6; dir++)
                    {
                        if (mapHex.HasRoadInDirection(dir))
                        {
                            var neighborHex = hex.Neighbor(dir);
                            var neighborPixel = layout.HexToPixel(neighborHex);
                            float nx = (float)neighborPixel.X + offsetX;
                            float ny = (float)neighborPixel.Y + offsetY;
                            canvas.DrawLine(drawX, drawY, nx, ny, roadPaint);
                        }
                    }
                }
            }

            // Pass 3: Location icons
            foreach (var (mapHex, hex, cx, cy) in hexPixels)
            {
                if (!mapHex.LocationTypeId.HasValue) continue;
                if (!locationIcons.TryGetValue(mapHex.LocationTypeId.Value, out var locIcon)) continue;

                float drawX = cx + offsetX;
                float drawY = cy + offsetY;
                double scaleFactor = locationById.TryGetValue(mapHex.LocationTypeId.Value, out var lt) ? lt.ScaleFactor : 0.64;
                DrawIcon(canvas, locIcon, drawX, drawY, scaleFactor);
            }

            // Pass 4: Army markers
            foreach (var army in armies)
            {
                if (!army.CoordinateQ.HasValue || !army.CoordinateR.HasValue) continue;

                var armyHex = new Hex(army.CoordinateQ.Value, army.CoordinateR.Value, -army.CoordinateQ.Value - army.CoordinateR.Value);
                var armyPixel = layout.HexToPixel(armyHex);
                float ax = (float)armyPixel.X + offsetX;
                float ay = (float)armyPixel.Y + offsetY;
                float markerRadius = Math.Max(6, HexRadius * 0.35f);

                // Faction color
                var markerColor = SKColors.Gray;
                if (army.Faction != null)
                {
                    markerColor = ParseColor(army.Faction.ColorHex);
                }

                using var markerFill = new SKPaint { Color = markerColor, Style = SKPaintStyle.Fill, IsAntialias = true };
                using var markerStroke = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };
                canvas.DrawCircle(ax, ay, markerRadius, markerFill);
                canvas.DrawCircle(ax, ay, markerRadius, markerStroke);

                // Initial letter
                if (!string.IsNullOrEmpty(army.Name))
                {
                    string initial = army.Name[0].ToString().ToUpperInvariant();
                    float letterSize = Math.Max(8, markerRadius * 1.2f);
                    using var letterFont = new SKFont(SKTypeface.Default, letterSize);
                    using var letterPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
                    // Vertically center the text
                    var metrics = letterFont.Metrics;
                    float textOffsetY = -(metrics.Ascent + metrics.Descent) / 2;
                    canvas.DrawText(initial, ax, ay + textOffsetY, SKTextAlign.Center, letterFont, letterPaint);
                }
            }
        }
        finally
        {
            // Dispose icon bitmaps
            foreach (var icon in terrainIcons.Values) icon.Dispose();
            foreach (var icon in locationIcons.Values) icon.Dispose();
        }

        return bitmap;
    }

    private static SKPoint[] GetHexCorners(float centerX, float centerY)
    {
        var corners = new SKPoint[6];
        for (int i = 0; i < 6; i++)
        {
            // Flat-top hex: start_angle = 0.0 (from Layout.flat)
            double angle = 2.0 * Math.PI * (0.0 - i) / 6.0;
            corners[i] = new SKPoint(
                centerX + HexRadius * (float)Math.Cos(angle),
                centerY + HexRadius * (float)Math.Sin(angle));
        }
        return corners;
    }

    private static SKPath CornersToPath(SKPoint[] corners)
    {
        var path = new SKPath();
        path.MoveTo(corners[0]);
        for (int i = 1; i < corners.Length; i++)
            path.LineTo(corners[i]);
        path.Close();
        return path;
    }

    private static void DrawIcon(SKCanvas canvas, SKBitmap icon, float cx, float cy, double scaleFactor)
    {
        float hexHeight = (float)(Math.Sqrt(3) * HexRadius);
        float maxIconSize = hexHeight * (float)scaleFactor;
        float scale = maxIconSize / Math.Max(icon.Width, icon.Height);
        float drawWidth = icon.Width * scale;
        float drawHeight = icon.Height * scale;
        var destRect = new SKRect(cx - drawWidth / 2, cy - drawHeight / 2, cx + drawWidth / 2, cy + drawHeight / 2);
        canvas.DrawBitmap(icon, destRect);
    }

    private static SKBitmap? LoadIcon(string iconPath)
    {
        if (string.IsNullOrEmpty(iconPath)) return null;

        try
        {
            // Convert avares:// URI to filesystem path
            // e.g. "avares://MechanicalCataphract/Assets/classic-icons/foo.png"
            //   -> "Assets/classic-icons/foo.png" relative to AppContext.BaseDirectory
            const string prefix = "avares://MechanicalCataphract/";
            string relativePath;
            if (iconPath.StartsWith(prefix))
            {
                relativePath = iconPath.Substring(prefix.Length);
            }
            else
            {
                relativePath = iconPath;
            }

            string fullPath = Path.Combine(AppContext.BaseDirectory, relativePath);
            if (!File.Exists(fullPath)) return null;

            return SKBitmap.Decode(fullPath);
        }
        catch
        {
            return null;
        }
    }

    private static SKColor ParseColor(string hex)
    {
        if (string.IsNullOrEmpty(hex)) return SKColors.White;
        try
        {
            return SKColor.Parse(hex);
        }
        catch
        {
            return SKColors.White;
        }
    }
}
