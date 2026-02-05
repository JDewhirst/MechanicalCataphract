using Hexes;

namespace MechanicalCataphract.Tests.Hexes;

[TestFixture]
public class OffsetCoordTests
{
    [Test]
    public void QoffsetFromCube_ODD_Roundtrip()
    {
        var hex = new Hex(3, -2, -1);
        var offset = OffsetCoord.QoffsetFromCube(OffsetCoord.ODD, hex);
        var back = OffsetCoord.QoffsetToCube(OffsetCoord.ODD, offset);
        Assert.That(back.q, Is.EqualTo(hex.q));
        Assert.That(back.r, Is.EqualTo(hex.r));
        Assert.That(back.s, Is.EqualTo(hex.s));
    }

    [Test]
    public void QoffsetFromCube_EVEN_Roundtrip()
    {
        var hex = new Hex(3, -2, -1);
        var offset = OffsetCoord.QoffsetFromCube(OffsetCoord.EVEN, hex);
        var back = OffsetCoord.QoffsetToCube(OffsetCoord.EVEN, offset);
        Assert.That(back.q, Is.EqualTo(hex.q));
        Assert.That(back.r, Is.EqualTo(hex.r));
    }

    [Test]
    public void RoffsetFromCube_ODD_Roundtrip()
    {
        var hex = new Hex(2, -3, 1);
        var offset = OffsetCoord.RoffsetFromCube(OffsetCoord.ODD, hex);
        var back = OffsetCoord.RoffsetToCube(OffsetCoord.ODD, offset);
        Assert.That(back.q, Is.EqualTo(hex.q));
        Assert.That(back.r, Is.EqualTo(hex.r));
    }

    [Test]
    public void RoffsetFromCube_EVEN_Roundtrip()
    {
        var hex = new Hex(2, -3, 1);
        var offset = OffsetCoord.RoffsetFromCube(OffsetCoord.EVEN, hex);
        var back = OffsetCoord.RoffsetToCube(OffsetCoord.EVEN, offset);
        Assert.That(back.q, Is.EqualTo(hex.q));
        Assert.That(back.r, Is.EqualTo(hex.r));
    }

    [Test]
    public void QoffsetFromCube_Origin()
    {
        var hex = new Hex(0, 0, 0);
        var offset = OffsetCoord.QoffsetFromCube(OffsetCoord.ODD, hex);
        Assert.That(offset.col, Is.EqualTo(0));
        Assert.That(offset.row, Is.EqualTo(0));
    }

    [Test]
    public void QoffsetFromCube_InvalidOffset_Throws()
    {
        var hex = new Hex(0, 0, 0);
        Assert.Throws<ArgumentException>(() => OffsetCoord.QoffsetFromCube(0, hex));
    }

    [Test]
    public void QoffsetToCube_InvalidOffset_Throws()
    {
        var offset = new OffsetCoord(0, 0);
        Assert.Throws<ArgumentException>(() => OffsetCoord.QoffsetToCube(0, offset));
    }

    [Test]
    public void RoffsetFromCube_InvalidOffset_Throws()
    {
        var hex = new Hex(0, 0, 0);
        Assert.Throws<ArgumentException>(() => OffsetCoord.RoffsetFromCube(0, hex));
    }
}
