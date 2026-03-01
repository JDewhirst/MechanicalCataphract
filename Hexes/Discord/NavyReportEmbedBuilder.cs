using System.Linq;
using System.Text;
using Hexes;
using NetCord.Rest;
using MechanicalCataphract.Data.Entities;

namespace MechanicalCataphract.Discord;

public static class NavyReportEmbedBuilder
{
    public static EmbedProperties BuildNavyReport(Navy navy, Commander commander, string formattedGameTime)
    {
        var embed = new EmbedProperties
        {
            Title = $"Navy Report: {navy.Name}",
            Description = $"Commander: **{commander.Name}**",
            Footer = new EmbedFooterProperties { Text = $"Game Time: {formattedGameTime}" },
        };

        var fields = new System.Collections.Generic.List<EmbedFieldProperties>();

        // Location
        string location = "Unknown";
        if (navy.CoordinateQ.HasValue && navy.CoordinateR.HasValue)
        {
            var hex = new Hex(navy.CoordinateQ.Value, navy.CoordinateR.Value,
                -(navy.CoordinateQ.Value + navy.CoordinateR.Value));
            var offset = OffsetCoord.QoffsetFromCube(OffsetCoord.ODD, hex);
            location = $"Col {offset.col}, Row {offset.row}";
        }
        fields.Add(new EmbedFieldProperties { Name = "Location", Value = location, Inline = true });

        // Crew Supply
        fields.Add(new EmbedFieldProperties
        {
            Name = "Crew Supply",
            Value = $"{navy.CarriedSupply} ({navy.DaysOfSupply:F1} days)",
            Inline = true,
        });

        // Daily Supply Use
        fields.Add(new EmbedFieldProperties
        {
            Name = "Daily Supply Use",
            Value = navy.DailySupplyConsumption.ToString(),
            Inline = true,
        });

        // Transports
        fields.Add(new EmbedFieldProperties { Name = "Transports", Value = navy.TransportCount.ToString(), Inline = true });

        // Warships
        fields.Add(new EmbedFieldProperties { Name = "Warships", Value = navy.WarshipCount.ToString(), Inline = true });

        // Cargo
        fields.Add(new EmbedFieldProperties
        {
            Name = "Cargo",
            Value = $"{navy.TotalCarryUnits:F1} / {navy.MaxCarryUnits} units",
            Inline = true,
        });

        // Ship breakdown
        if (navy.Ships != null && navy.Ships.Count != 0)
        {
            var sb = new StringBuilder();
            foreach (var group in navy.Ships.GroupBy(s => s.ShipType).OrderBy(g => g.Key))
            {
                int total = group.Sum(s => s.Count);
                sb.AppendLine($"**{total}x** {group.Key}");
            }
            fields.Add(new EmbedFieldProperties { Name = "Ships", Value = sb.ToString().TrimEnd(), Inline = false });
        }

        embed.Fields = fields;
        return embed;
    }
}
