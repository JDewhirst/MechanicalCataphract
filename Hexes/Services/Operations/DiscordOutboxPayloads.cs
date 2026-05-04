using System.Collections.Generic;
using NetCord.Rest;

namespace MechanicalCataphract.Services.Operations;

public class DiscordOutboxEmbedBatchPayload
{
    public List<DiscordOutboxEmbedPayload> Embeds { get; set; } = new();

    public IReadOnlyList<EmbedProperties> ToEmbedProperties()
        => Embeds.ConvertAll(e => e.ToEmbedProperties());
}

public class DiscordOutboxEmbedPayload
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? FooterText { get; set; }
    public List<DiscordOutboxEmbedFieldPayload> Fields { get; set; } = new();

    public EmbedProperties ToEmbedProperties()
    {
        var embed = new EmbedProperties
        {
            Title = Title,
            Description = Description
        };

        if (!string.IsNullOrWhiteSpace(FooterText))
            embed.Footer = new EmbedFooterProperties { Text = FooterText };

        embed.Fields = Fields.ConvertAll(f => new EmbedFieldProperties
        {
            Name = f.Name,
            Value = f.Value,
            Inline = f.Inline
        });

        return embed;
    }
}

public class DiscordOutboxEmbedFieldPayload
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public bool Inline { get; set; }
}
