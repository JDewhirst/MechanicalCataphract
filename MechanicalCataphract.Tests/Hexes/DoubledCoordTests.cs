using Hexes;

namespace MechanicalCataphract.Tests.Hexes;

[TestFixture]
public class DoubledCoordTests
{
    [Test]
    public void QdoubledFromCube_Roundtrip()
    {
        var hex = new Hex(2, -1, -1);
        var doubled = DoubledCoord.QdoubledFromCube(hex);
        var back = doubled.QdoubledToCube();
        Assert.That(back.q, Is.EqualTo(hex.q));
        Assert.That(back.r, Is.EqualTo(hex.r));
        Assert.That(back.s, Is.EqualTo(hex.s));
    }

    [Test]
    public void RdoubledFromCube_Roundtrip()
    {
        var hex = new Hex(2, -1, -1);
        var doubled = DoubledCoord.RdoubledFromCube(hex);
        var back = doubled.RdoubledToCube();
        Assert.That(back.q, Is.EqualTo(hex.q));
        Assert.That(back.r, Is.EqualTo(hex.r));
        Assert.That(back.s, Is.EqualTo(hex.s));
    }

    [Test]
    public void QdoubledFromCube_KnownValue()
    {
        // Hex(1, 2, -3): col = q = 1, row = 2*r + q = 2*2 + 1 = 5
        var hex = new Hex(1, 2, -3);
        var doubled = DoubledCoord.QdoubledFromCube(hex);
        Assert.That(doubled.col, Is.EqualTo(1));
        Assert.That(doubled.row, Is.EqualTo(5));
    }

    [Test]
    public void RdoubledFromCube_KnownValue()
    {
        // Hex(1, 2, -3): col = 2*q + r = 2*1 + 2 = 4, row = r = 2
        var hex = new Hex(1, 2, -3);
        var doubled = DoubledCoord.RdoubledFromCube(hex);
        Assert.That(doubled.col, Is.EqualTo(4));
        Assert.That(doubled.row, Is.EqualTo(2));
    }
}
