using MechanicalCataphract.Data.Entities;

namespace MechanicalCataphract.Tests.Data.Entities;

[TestFixture]
public class MapHexTests
{
    [Test]
    public void ToHex_ReturnsCorrectHex()
    {
        var mapHex = new MapHex { Q = 1, R = 2 };
        var hex = mapHex.ToHex();
        Assert.That(hex.q, Is.EqualTo(1));
        Assert.That(hex.r, Is.EqualTo(2));
        Assert.That(hex.s, Is.EqualTo(-3));
    }

    [Test]
    public void HasRoadInDirection_NullRoads_ReturnsFalse()
    {
        var mapHex = new MapHex { RoadDirections = null };
        Assert.That(mapHex.HasRoadInDirection(0), Is.False);
    }

    [Test]
    public void HasRoadInDirection_EmptyString_ReturnsFalse()
    {
        var mapHex = new MapHex { RoadDirections = "" };
        Assert.That(mapHex.HasRoadInDirection(0), Is.False);
    }

    [Test]
    public void HasRoadInDirection_SingleMatch_ReturnsTrue()
    {
        var mapHex = new MapHex { RoadDirections = "3" };
        Assert.That(mapHex.HasRoadInDirection(3), Is.True);
    }

    [Test]
    public void HasRoadInDirection_SingleNoMatch_ReturnsFalse()
    {
        var mapHex = new MapHex { RoadDirections = "3" };
        Assert.That(mapHex.HasRoadInDirection(0), Is.False);
    }

    [Test]
    public void HasRoadInDirection_MultipleRoads_MatchFound()
    {
        var mapHex = new MapHex { RoadDirections = "0,3,5" };
        Assert.That(mapHex.HasRoadInDirection(3), Is.True);
    }

    [Test]
    public void HasRoadInDirection_MultipleRoads_NoMatch()
    {
        var mapHex = new MapHex { RoadDirections = "0,3,5" };
        Assert.That(mapHex.HasRoadInDirection(2), Is.False);
    }

    [Test]
    public void HasRiverOnEdge_NullEdges_ReturnsFalse()
    {
        var mapHex = new MapHex { RiverEdges = null };
        Assert.That(mapHex.HasRiverOnEdge(1), Is.False);
    }

    [Test]
    public void HasRiverOnEdge_MatchReturnsTrue()
    {
        var mapHex = new MapHex { RiverEdges = "1,4" };
        Assert.That(mapHex.HasRiverOnEdge(1), Is.True);
    }

    [Test]
    public void HasRiverOnEdge_NoMatchReturnsFalse()
    {
        var mapHex = new MapHex { RiverEdges = "1,4" };
        Assert.That(mapHex.HasRiverOnEdge(3), Is.False);
    }
}
