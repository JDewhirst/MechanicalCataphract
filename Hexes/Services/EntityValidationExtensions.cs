using System.Threading.Tasks;
using MechanicalCataphract.Data;
using MechanicalCataphract.Data.Entities;

namespace MechanicalCataphract.Services;

public static class EntityValidationExtensions
{
    public static async Task ValidateCoordinatesAsync(this Army entity, WargameDbContext context)
    {
        await CoordinateValidator.ValidateCoordinatesAsync(context, entity.CoordinateQ, entity.CoordinateR, "Location");
        await CoordinateValidator.ValidateCoordinatesAsync(context, entity.TargetCoordinateQ, entity.TargetCoordinateR, "TargetCoordinate");
    }

    public static async Task ValidateCoordinatesAsync(this Navy entity, WargameDbContext context)
    {
        await CoordinateValidator.ValidateCoordinatesAsync(context, entity.CoordinateQ, entity.CoordinateR, "Location");
    }

    public static async Task ValidateCoordinatesAsync(this Commander entity, WargameDbContext context)
    {
        await CoordinateValidator.ValidateCoordinatesAsync(context, entity.CoordinateQ, entity.CoordinateR, "Location");
        await CoordinateValidator.ValidateCoordinatesAsync(context, entity.TargetCoordinateQ, entity.TargetCoordinateR, "TargetCoordinate");
    }

    public static async Task ValidateCoordinatesAsync(this Message entity, WargameDbContext context)
    {
        await CoordinateValidator.ValidateCoordinatesAsync(context, entity.CoordinateQ, entity.CoordinateR, "Location");
        await CoordinateValidator.ValidateCoordinatesAsync(context, entity.SenderCoordinateQ, entity.SenderCoordinateR, "SenderCoordinate");
        await CoordinateValidator.ValidateCoordinatesAsync(context, entity.TargetCoordinateQ, entity.TargetCoordinateR, "TargetCoordinate");
    }
}
