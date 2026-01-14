using System;

namespace MechanicalCataphract.Data.Entities;

public class Order
{
    public int Id { get; set; }

    // Commander issuing the order
    public int CommanderId { get; set; }
    public Commander? Commander { get; set; }

    // Order content
    public string Contents { get; set; } = string.Empty;

    // Status
    public bool Processed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
}
