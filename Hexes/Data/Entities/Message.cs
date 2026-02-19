using Hexes;
using System;
using System.Collections.Generic;
using MechanicalCataphract.Services;

namespace MechanicalCataphract.Data.Entities;

public class Message : IPathMovable
{
    public int Id { get; set; }

    // Sender - can be commander or location
    public int? SenderCommanderId { get; set; }
    public Commander? SenderCommander { get; set; }
    public int? SenderCoordinateQ { get; set; }
    public int? SenderCoordinateR { get; set; }

    // Target - can be commander or location
    public int? TargetCommanderId { get; set; }
    public Commander? TargetCommander { get; set; }
    public int? TargetCoordinateQ { get; set; }
    public int? TargetCoordinateR { get; set; }

    // Content
    public string Content { get; set; } = string.Empty;

    // Coordinate (composite FK to MapHex)
    public int? CoordinateQ { get; set; }
    public int? CoordinateR { get; set; }
    public MapHex? MapHex { get; set; }

    // Movement (implements IPathMovable)
    public List<Hex>? Path { get; set; }
    public float TimeInTransit { get; set; }
    public float MovementRate => (float)GameRules.Current.MovementRates.MessengerBaseRate;

    // Status
    public bool Delivered { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
