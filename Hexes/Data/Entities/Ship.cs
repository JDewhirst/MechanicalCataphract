namespace MechanicalCataphract.Data.Entities;

public class Ship
{
    public int Id { get; set; }
    public int Count { get; set; } = 1;
    public ShipType ShipType { get; set; } = ShipType.Transport;

    // Parent navy (cascade delete)
    public int NavyId { get; set; }
    public Navy? Navy { get; set; }
}
