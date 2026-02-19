namespace MechanicalCataphract.Data.Entities;

public class FactionRule
{
    public int Id { get; set; }
    public int FactionId { get; set; }
    public Faction? Faction { get; set; }
    public string RuleKey { get; set; } = string.Empty;
    public double Value { get; set; }
}
