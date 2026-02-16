using System;
using System.Linq;
using System.Text;
using Hexes;
using NetCord.Rest;
using MechanicalCataphract.Data.Entities;

namespace MechanicalCataphract.Discord;

public static class ArmyReportEmbedBuilder
{
    public static EmbedProperties BuildArmyReport(Army army, Commander commander, DateTime gameTime)
    {
        var embed = new EmbedProperties
        {
            Title = $"Army Report: {army.Name}",
            Description = $"Commander: **{commander.Name}**",
            Footer = new EmbedFooterProperties { Text = $"Game Time: {gameTime:yyyy-MM-dd HH:mm}" },
        };

        var fields = new System.Collections.Generic.List<EmbedFieldProperties>();

        // Location
        string location = "Unknown";
        if (army.CoordinateQ.HasValue && army.CoordinateR.HasValue)
        {
            var hex = new Hex(army.CoordinateQ.Value, army.CoordinateR.Value,
                -(army.CoordinateQ.Value + army.CoordinateR.Value));
            var offset = OffsetCoord.QoffsetFromCube(OffsetCoord.ODD, hex);
            location = $"Col {offset.col}, Row {offset.row}";
        }
        fields.Add(new EmbedFieldProperties { Name = "Location", Value = location, Inline = true });

        // Morale
        fields.Add(new EmbedFieldProperties { Name = "Morale", Value = army.Morale.ToString(), Inline = true });

        // Status
        string status = army.IsGarrison ? "Garrison" : army.IsResting ? "Resting" : "Active";
        fields.Add(new EmbedFieldProperties { Name = "Status", Value = status, Inline = true });

        // Combat Strength
        fields.Add(new EmbedFieldProperties { Name = "Combat Strength", Value = army.CombatStrength.ToString(), Inline = true });

        // Supply
        fields.Add(new EmbedFieldProperties
        {
            Name = "Supply",
            Value = $"{army.CarriedSupply} carried ({army.DaysOfSupply:F1} days)",
            Inline = true,
        });

        // Non-Combatants
        fields.Add(new EmbedFieldProperties { Name = "Non-Combatants", Value = army.NonCombatants.ToString(), Inline = true });

        // Wagons
        fields.Add(new EmbedFieldProperties { Name = "Wagons", Value = army.Wagons.ToString(), Inline = true });

        // Loot
        fields.Add(new EmbedFieldProperties { Name = "Loot", Value = army.CarriedLoot.ToString(), Inline = true });

        // Coins
        fields.Add(new EmbedFieldProperties { Name = "Coins", Value = army.CarriedCoins.ToString(), Inline = true });

        // Brigade breakdown
        if (army.Brigades != null && army.Brigades.Count != 0)
        {
            var sb = new StringBuilder();
            foreach (var brigade in army.Brigades)
            {
                sb.AppendLine($"**{brigade.Name}** — {brigade.Number} {brigade.UnitType}");
            }
            fields.Add(new EmbedFieldProperties { Name = "Brigades", Value = sb.ToString().TrimEnd(), Inline = false });
        }

        embed.Fields = fields;
        return embed;
    }
}
