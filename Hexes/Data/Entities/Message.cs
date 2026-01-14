using System;

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

    // Status
    public bool Delivered { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
