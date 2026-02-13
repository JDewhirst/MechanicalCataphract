using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MechanicalCataphract.Data.Entities;

namespace MechanicalCataphract.Services;

public static class TerrainTypeLoader
{
    /// <summary>
    /// Parses a .properties file and returns a list of TerrainType entities.
    /// The properties file uses format: TerrainName.property=value
    /// </summary>
    public static List<TerrainType> LoadFromPropertiesFile(string filePath)
    {
        var terrainTypes = new Dictionary<string, TerrainType>();
        var lines = File.ReadAllLines(filePath);
        int nextId = 1;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("symbolnames="))
                continue;

            var eqIndex = line.IndexOf('=');
            if (eqIndex <= 0) continue;

            var key = line.Substring(0, eqIndex);
            var value = line.Substring(eqIndex + 1);

            var dotIndex = key.IndexOf('.');
            if (dotIndex <= 0) continue;

            var terrainKey = key.Substring(0, dotIndex);
            var property = key.Substring(dotIndex + 1).ToLowerInvariant();

            if (!terrainTypes.TryGetValue(terrainKey, out var terrain))
            {
                terrain = new TerrainType
                {
                    Id = nextId++,
                    Name = terrainKey.Replace('_', ' ')
                };
                terrainTypes[terrainKey] = terrain;
            }

            switch (property)
            {
                case "iconfilename":
                    if (!string.IsNullOrEmpty(value))
                        terrain.IconPath = $"avares://MechanicalCataphract/Assets/classic-icons/{value}";
                    break;
                case "backgroundrgb":
                    terrain.ColorHex = ConvertJavaColorToHex(value);
                    break;
                case "elevation":
                    // Use elevation as a proxy for movement cost (higher = harder)
                    if (int.TryParse(value, out var elevation))
                        terrain.BaseMovementCost = Math.Max(1, elevation / 2 + 1);
                    break;
                case "category":
                    terrain.IsWater = value.Equals("Water", StringComparison.OrdinalIgnoreCase);
                    break;
                case "isuseicon":
                    terrain.IsUseIcon = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                    break;
                case "scalefactor":
                    if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var scale))
                        terrain.ScaleFactor = scale;
                    break;
            }
        }

        return terrainTypes.Values.OrderBy(t => t.Id).ToList();
    }

    /// <summary>
    /// Parses an icon.properties file for location types.
    /// Returns (name, iconFilename, scaleFactor) tuples.
    /// </summary>
    public static List<(string name, string iconFilename, double scaleFactor)> LoadLocationIconsFromPropertiesFile(string filePath)
    {
        var results = new Dictionary<string, (string? iconFilename, double scaleFactor)>();
        var lines = File.ReadAllLines(filePath);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("symbolnames="))
                continue;

            var eqIndex = line.IndexOf('=');
            if (eqIndex <= 0) continue;

            var key = line.Substring(0, eqIndex);
            var value = line.Substring(eqIndex + 1);

            var dotIndex = key.IndexOf('.');
            if (dotIndex <= 0) continue;

            var locationKey = key.Substring(0, dotIndex);
            var property = key.Substring(dotIndex + 1).ToLowerInvariant();
            var name = locationKey.Replace('_', ' ');

            if (!results.TryGetValue(name, out var entry))
                entry = (null, 0.64);

            switch (property)
            {
                case "iconfilename":
                    entry.iconFilename = value;
                    break;
                case "scalefactor":
                    if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var scale))
                        entry.scaleFactor = scale;
                    break;
            }

            results[name] = entry;
        }

        return results
            .Where(kvp => !string.IsNullOrEmpty(kvp.Value.iconFilename))
            .Select(kvp => (kvp.Key, kvp.Value.iconFilename!, kvp.Value.scaleFactor))
            .ToList();
    }

    /// <summary>
    /// Converts Java's signed integer color format to HTML hex color.
    /// Java stores colors as signed 32-bit integers where the RGB is in the lower 24 bits.
    /// </summary>
    private static string ConvertJavaColorToHex(string javaColorStr)
    {
        if (!int.TryParse(javaColorStr, out var javaColor))
            return "#808080"; // Default gray

        // Convert signed int to unsigned RGB (lower 24 bits)
        uint rgb = (uint)(javaColor & 0xFFFFFF);
        return $"#{rgb:X6}";
    }
}
