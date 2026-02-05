using Hexes;

namespace MechanicalCataphract.Tests.Hexes;

[TestFixture]
public class FractionalHexTests
{
    [Test]
    public void HexRound_ExactInteger_ReturnsCorrectHex()
    {
        var frac = new FractionalHex(1.0, 2.0, -3.0);
        var result = frac.HexRound();
        Assert.That(result.q, Is.EqualTo(1));
        Assert.That(result.r, Is.EqualTo(2));
        Assert.That(result.s, Is.EqualTo(-3));
    }

    [Test]
    public void HexRound_NearBoundary_RoundsCorrectly()
    {
        // Slightly off from (1, 2, -3) — should still round back
        var frac = new FractionalHex(1.1, 1.9, -3.0);
        var result = frac.HexRound();
        Assert.That(result.q, Is.EqualTo(1));
        Assert.That(result.r, Is.EqualTo(2));
        Assert.That(result.s, Is.EqualTo(-3));
    }

    [Test]
    public void HexLerp_AtZero_ReturnsSelf()
    {
        var a = new FractionalHex(0.0, 0.0, 0.0);
        var b = new FractionalHex(2.0, -1.0, -1.0);
        var result = a.HexLerp(b, 0.0);
        Assert.That(result.q, Is.EqualTo(a.q).Within(1e-10));
        Assert.That(result.r, Is.EqualTo(a.r).Within(1e-10));
    }

    [Test]
    public void HexLerp_AtOne_ReturnsTarget()
    {
        var a = new FractionalHex(0.0, 0.0, 0.0);
        var b = new FractionalHex(2.0, -1.0, -1.0);
        var result = a.HexLerp(b, 1.0);
        Assert.That(result.q, Is.EqualTo(b.q).Within(1e-10));
        Assert.That(result.r, Is.EqualTo(b.r).Within(1e-10));
    }

    [Test]
    public void HexLerp_AtHalf_ReturnsMidpoint()
    {
        var a = new FractionalHex(0.0, 0.0, 0.0);
        var b = new FractionalHex(4.0, -2.0, -2.0);
        var result = a.HexLerp(b, 0.5);
        Assert.That(result.q, Is.EqualTo(2.0).Within(1e-10));
        Assert.That(result.r, Is.EqualTo(-1.0).Within(1e-10));
    }

    [Test]
    public void HexLinedraw_SameHex_ReturnsSingleElement()
    {
        var hex = new Hex(0, 0, 0);
        var line = FractionalHex.HexLinedraw(hex, hex);
        Assert.That(line, Has.Count.EqualTo(1));
        Assert.That(line[0].q, Is.EqualTo(0));
    }

    [Test]
    public void HexLinedraw_AdjacentHexes_ReturnsTwoElements()
    {
        var a = new Hex(0, 0, 0);
        var b = new Hex(1, 0, -1);
        var line = FractionalHex.HexLinedraw(a, b);
        Assert.That(line, Has.Count.EqualTo(2));
    }

    [Test]
    public void HexLinedraw_Count_EqualsDistancePlusOne()
    {
        var a = new Hex(0, 0, 0);
        var b = new Hex(3, -3, 0);
        var line = FractionalHex.HexLinedraw(a, b);
        Assert.That(line, Has.Count.EqualTo(a.Distance(b) + 1));
    }

    [Test]
    public void HexLinedraw_AllConsecutiveHexesAdjacent()
    {
        var a = new Hex(0, 0, 0);
        var b = new Hex(3, -2, -1);
        var line = FractionalHex.HexLinedraw(a, b);

        for (int i = 0; i < line.Count - 1; i++)
        {
            Assert.That(line[i].Distance(line[i + 1]), Is.EqualTo(1),
                $"Hexes at index {i} and {i + 1} are not adjacent");
        }
    }

    [Test]
    public void HexLinedraw_StartsAndEndsCorrectly()
    {
        var a = new Hex(0, 0, 0);
        var b = new Hex(2, -1, -1);
        var line = FractionalHex.HexLinedraw(a, b);
        Assert.That(line[0].q, Is.EqualTo(a.q));
        Assert.That(line[0].r, Is.EqualTo(a.r));
        Assert.That(line[^1].q, Is.EqualTo(b.q));
        Assert.That(line[^1].r, Is.EqualTo(b.r));
    }
}
