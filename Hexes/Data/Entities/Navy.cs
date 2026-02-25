using System.Collections.Generic;
using System.Linq;
using MechanicalCataphract.Services;

namespace MechanicalCataphract.Data.Entities;

public class Navy
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    // Coordinate (composite FK to MapHex, Restrict on delete)
    public int? CoordinateQ { get; set; }
    public int? CoordinateR { get; set; }
    public MapHex? MapHex { get; set; }

    // Commander (SetNull on delete)
    public int? CommanderId { get; set; }
    public Commander? Commander { get; set; }

    // Crew supply carried aboard
    public int CarriedSupply { get; set; }

    // Whether the fleet is rowing (bonus movement)
    public bool IsRowing { get; set; }

    // Navigation
    public ICollection<Ship> Ships { get; set; } = new List<Ship>();

    // The army currently embarked on this navy (navigation via Army.NavyId)
    public Army? CarriedArmy { get; set; }

    // Computed properties
    public int TransportCount => Ships.Where(s => s.ShipType == ShipType.Transport).Sum(s => s.Count);
    public int WarshipCount   => Ships.Where(s => s.ShipType == ShipType.Warship).Sum(s => s.Count);

    public int DailySupplyConsumption =>
        Ships.Sum(s => s.Count) * GameRules.Current.Ships.CrewSupplyConsumptionPerShip;

    public double DaysOfSupply =>
        DailySupplyConsumption > 0 ? (double)CarriedSupply / DailySupplyConsumption : 0;

    public int MaxCarryUnits
    {
        get
        {
            var rules = GameRules.Current.Ships;
            return TransportCount * rules.TransportInfantryCapacity
                 + (int)(WarshipCount * rules.TransportInfantryCapacity * rules.WarshipCapacityMultiplier);
        }
    }

    /// <summary>
    /// Total cargo units currently aboard (navy supply + embarked army cargo).
    /// </summary>
    public double TotalCarryUnits
    {
        get
        {
            var rules = GameRules.Current.Ships;
            double infantryWeight  = 1.0;
            double cavalryWeight   = (double)rules.TransportInfantryCapacity / rules.TransportCavalryCapacity;
            double supplyWeight    = 1.0 / rules.TransportSupplyCapacity;
            double wagonWeight     = (double)rules.TransportInfantryCapacity / rules.TransportWagonCapacity;

            double units = CarriedSupply * supplyWeight;

            if (CarriedArmy != null)
            {
                var army = CarriedArmy;
                if (army.Brigades != null)
                {
                    foreach (var b in army.Brigades)
                    {
                        double weight = b.UnitType == UnitType.Cavalry ? cavalryWeight : infantryWeight;
                        units += b.Number * weight;
                    }
                }
                units += army.NonCombatants * infantryWeight;
                units += (army.CarriedSupply + army.CarriedLoot + army.CarriedCoins) * supplyWeight;
                units += army.Wagons * wagonWeight;
            }

            return units;
        }
    }
}
