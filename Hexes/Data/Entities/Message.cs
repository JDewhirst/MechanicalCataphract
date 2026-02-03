using Hexes;
using System;
using System.Collections.Generic;

namespace MechanicalCataphract.Data.Entities;

public class Message
{
    public int Id { get; set; }

    // Sender - can be commander or location
    public int? SenderCommanderId { get; set; }
    public Commander? SenderCommander { get; set; }
    public int? SenderLocationQ { get; set; }
    public int? SenderLocationR { get; set; }

    // Target - can be commander or location
    public int? TargetCommanderId { get; set; }
    public Commander? TargetCommander { get; set; }
    public int? TargetLocationQ { get; set; }
    public int? TargetLocationR { get; set; }

    // Content
    public string Content { get; set; } = string.Empty;

    // Location (composite FK to MapHex)
    public int? LocationQ { get; set; }
    public int? LocationR { get; set; }
    public MapHex? Location { get; set; }

    // Movement
    public List<Hex>? Path { get; set; } //currently planned path
    public int TimeInTransit { get; set; } //time in transit to next step of path 

    // Status
    public bool Delivered { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
