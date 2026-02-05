using Hexes;

namespace MechanicalCataphract.Tests.Hexes;

[TestFixture]
public class HexTests
{
    [Test]
    public void Constructor_SetsQRS()
    {
        var hex = new Hex(1, 2, -3);
        Assert.That(hex.q, Is.EqualTo(1));
        Assert.That(hex.r, Is.EqualTo(2));
        Assert.That(hex.s, Is.EqualTo(-3));
    }

    [Test]
    public void Constructor_DerivesSFromQR()
    {
        // s is always set to -q-r regardless of what you pass
        var hex = new Hex(3, -1, -2);
        Assert.That(hex.s, Is.EqualTo(-3 - (-1)));
        Assert.That(hex.s, Is.EqualTo(-2));
    }

    [Test]
    public void Constructor_ThrowsOnInvariantViolation()
    {
        Assert.Throws<ArgumentException>(() => new Hex(1, 1, 1));
    }

    [Test]
    public void Add_ReturnsSumOfCoords()
    {
        var a = new Hex(1, -2, 1);
        var b = new Hex(2, 1, -3);
        var result = a.Add(b);
        Assert.That(result.q, Is.EqualTo(3));
        Assert.That(result.r, Is.EqualTo(-1));
        Assert.That(result.s, Is.EqualTo(-2));
    }

    [Test]
    public void Subtract_ReturnsDifference()
    {
        var a = new Hex(3, -1, -2);
        var b = new Hex(1, -2, 1);
        var result = a.Subtract(b);
        Assert.That(result.q, Is.EqualTo(2));
        Assert.That(result.r, Is.EqualTo(1));
        Assert.That(result.s, Is.EqualTo(-3));
    }

    [Test]
    public void Scale_MultipliesAllCoords()
    {
        var hex = new Hex(1, -1, 0);
        var result = hex.Scale(3);
        Assert.That(result.q, Is.EqualTo(3));
        Assert.That(result.r, Is.EqualTo(-3));
        Assert.That(result.s, Is.EqualTo(0));
    }

    [Test]
    public void RotateLeft_ThenRight_IsIdentity()
    {
        var hex = new Hex(2, -1, -1);
        var result = hex.RotateLeft().RotateRight();
        Assert.That(result.q, Is.EqualTo(hex.q));
        Assert.That(result.r, Is.EqualTo(hex.r));
        Assert.That(result.s, Is.EqualTo(hex.s));
    }

    [Test]
    public void RotateLeft_ProducesExpectedResult()
    {
        // RotateLeft: (-s, -q, -r)
        var hex = new Hex(1, -1, 0);
        var result = hex.RotateLeft();
        Assert.That(result.q, Is.EqualTo(0));
        Assert.That(result.r, Is.EqualTo(-1));
        Assert.That(result.s, Is.EqualTo(1));
    }

    [TestCase(0, 1, 0, -1)]
    [TestCase(1, 1, -1, 0)]
    [TestCase(2, 0, -1, 1)]
    [TestCase(3, -1, 0, 1)]
    [TestCase(4, -1, 1, 0)]
    [TestCase(5, 0, 1, -1)]
    public void Direction_ReturnsCorrectHex_AllSix(int dir, int expectedQ, int expectedR, int expectedS)
    {
        var d = Hex.Direction(dir);
        Assert.That(d.q, Is.EqualTo(expectedQ));
        Assert.That(d.r, Is.EqualTo(expectedR));
        Assert.That(d.s, Is.EqualTo(expectedS));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    public void Neighbor_ReturnsAdjacentHex(int direction)
    {
        var origin = new Hex(0, 0, 0);
        var neighbor = origin.Neighbor(direction);
        Assert.That(origin.Distance(neighbor), Is.EqualTo(1));
    }

    [Test]
    public void Neighbor_AllSixDirections_AreDistinct()
    {
        var origin = new Hex(0, 0, 0);
        var neighbors = Enumerable.Range(0, 6)
            .Select(d => origin.Neighbor(d))
            .Select(h => (h.q, h.r))
            .ToHashSet();
        Assert.That(neighbors, Has.Count.EqualTo(6));
    }

    [Test]
    public void DiagonalNeighbor_ReturnsCorrectHex()
    {
        var origin = new Hex(0, 0, 0);
        // Diagonal 0 should be (2, -1, -1)
        var diag = origin.DiagonalNeighbor(0);
        Assert.That(diag.q, Is.EqualTo(2));
        Assert.That(diag.r, Is.EqualTo(-1));
        Assert.That(diag.s, Is.EqualTo(-1));
    }

    [Test]
    public void DiagonalNeighbor_AllSix_AreDistanceTwo()
    {
        var origin = new Hex(0, 0, 0);
        for (int i = 0; i < 6; i++)
        {
            var diag = origin.DiagonalNeighbor(i);
            Assert.That(origin.Distance(diag), Is.EqualTo(2));
        }
    }

    [Test]
    public void Length_OriginIsZero()
    {
        Assert.That(new Hex(0, 0, 0).Length(), Is.EqualTo(0));
    }

    [Test]
    public void Length_KnownValue()
    {
        Assert.That(new Hex(2, -1, -1).Length(), Is.EqualTo(2));
    }

    [Test]
    public void Distance_IsSymmetric()
    {
        var a = new Hex(1, -3, 2);
        var b = new Hex(-2, 1, 1);
        Assert.That(a.Distance(b), Is.EqualTo(b.Distance(a)));
    }

    [Test]
    public void Distance_AdjacentHexes_IsOne()
    {
        var a = new Hex(0, 0, 0);
        var b = new Hex(1, 0, -1);
        Assert.That(a.Distance(b), Is.EqualTo(1));
    }

    [Test]
    public void Distance_ToSelf_IsZero()
    {
        var a = new Hex(3, -2, -1);
        Assert.That(a.Distance(a), Is.EqualTo(0));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    public void DirectionTo_AdjacentHex_ReturnsDirection(int dir)
    {
        var origin = new Hex(0, 0, 0);
        var neighbor = origin.Neighbor(dir);
        Assert.That(origin.DirectionTo(neighbor), Is.EqualTo(dir));
    }

    [Test]
    public void DirectionTo_NonAdjacent_ReturnsNull()
    {
        var a = new Hex(0, 0, 0);
        var b = new Hex(2, -1, -1); // distance 2
        Assert.That(a.DirectionTo(b), Is.Null);
    }

    [Test]
    public void DirectionTo_Self_ReturnsNull()
    {
        var a = new Hex(0, 0, 0);
        Assert.That(a.DirectionTo(a), Is.Null);
    }
}
