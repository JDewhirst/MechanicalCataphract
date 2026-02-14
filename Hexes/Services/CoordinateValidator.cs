using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MechanicalCataphract.Data;

namespace MechanicalCataphract.Services;

public static class CoordinateValidator
{
    public static async Task ValidateCoordinatesAsync(
        WargameDbContext context, int? q, int? r, string fieldName)
    {
        if (q == null && r == null)
            return;

        if (q == null || r == null)
            throw new InvalidOperationException(
                $"{fieldName}: both Q and R must be set or both must be null.");

        var exists = await context.MapHexes.AnyAsync(h => h.Q == q && h.R == r);
        if (!exists)
            throw new InvalidOperationException(
                $"{fieldName}: no hex exists at ({q}, {r}).");
    }
}
