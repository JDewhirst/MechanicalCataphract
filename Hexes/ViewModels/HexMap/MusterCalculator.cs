using System.Collections.Generic;
using System.Linq;
using Hexes;
using MechanicalCataphract.Data.Entities;

namespace GUI.ViewModels.HexMap;

public readonly record struct MusterTotals(int Infantry, int Cavalry, int Wagons, int HexCount);

public static class MusterCalculator
{
    public static MusterTotals Calculate(
        IEnumerable<Hex> selectedHexes,
        IEnumerable<MapHex> visibleHexes,
        bool includeCavalry,
        bool includeWagons)
    {
        var hexLookup = visibleHexes.ToDictionary(h => (h.Q, h.R));
        int infantry = 0;
        int cavalry = 0;
        int wagons = 0;
        int hexCount = 0;

        foreach (var hex in selectedHexes)
        {
            if (!hexLookup.TryGetValue((hex.q, hex.r), out var mapHex))
                continue;

            hexCount++;
            int population = mapHex.PopulationDensity;
            infantry += population;
            if (includeCavalry) cavalry += (int)(population * 0.25);
            if (includeWagons) wagons += (int)(population * 0.05);
        }

        return new MusterTotals(infantry, cavalry, wagons, hexCount);
    }
}
