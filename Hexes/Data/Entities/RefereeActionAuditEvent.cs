using System;

namespace MechanicalCataphract.Data.Entities;

public class RefereeActionAuditEvent
{
    public int Id { get; set; }
    public int RunId { get; set; }
    public RefereeActionRun? Run { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public string? Message { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
