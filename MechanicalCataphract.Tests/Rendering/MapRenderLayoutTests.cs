using MechanicalCataphract.Rendering;

namespace MechanicalCataphract.Tests.Rendering;

public class MapRenderLayoutTests
{
    [Test]
    public void GroupEntitiesByHex_IgnoresEntitiesWithoutCoordinates()
    {
        var entities = new[]
        {
            new TestEntity("A", 1, 2),
            new TestEntity("NoQ", null, 2),
            new TestEntity("NoR", 1, null),
            new TestEntity("B", 3, 4)
        };

        var groups = MapRenderLayout.GroupEntitiesByHex(entities, e => (e.Q, e.R));

        Assert.That(groups.Keys, Is.EquivalentTo(new[] { (1, 2), (3, 4) }));
        Assert.That(groups[(1, 2)].Select(e => e.Name), Is.EqualTo(new[] { "A" }));
        Assert.That(groups[(3, 4)].Select(e => e.Name), Is.EqualTo(new[] { "B" }));
    }

    [Test]
    public void GroupEntitiesByHex_PreservesInputOrderWithinHex()
    {
        var entities = new[]
        {
            new TestEntity("First", 1, 2),
            new TestEntity("Second", 1, 2),
            new TestEntity("Third", 1, 2)
        };

        var groups = MapRenderLayout.GroupEntitiesByHex(entities, e => (e.Q, e.R));

        Assert.That(groups[(1, 2)].Select(e => e.Name), Is.EqualTo(new[] { "First", "Second", "Third" }));
    }

    [Test]
    public void GetStackedMarkerCenter_UsesMapViewStackOffsets()
    {
        var hexCenter = new RenderPoint(100, 200);
        var baseOffset = MapRenderLayout.GetMarkerOffset(MarkerPosition.Center, hexRadius: 30);

        var first = MapRenderLayout.GetStackedMarkerCenter(hexCenter, baseOffset, 0);
        var second = MapRenderLayout.GetStackedMarkerCenter(hexCenter, baseOffset, 1);
        var third = MapRenderLayout.GetStackedMarkerCenter(hexCenter, baseOffset, 2);

        Assert.That(first, Is.EqualTo(new RenderPoint(100, 200)));
        Assert.That(second, Is.EqualTo(new RenderPoint(96, 194)));
        Assert.That(third, Is.EqualTo(new RenderPoint(92, 188)));
    }

    [Test]
    public void GetIconDestination_DoublesLocationIconSizeWithMultiplier()
    {
        var normal = MapRenderLayout.GetIconDestination(
            new RenderPoint(0, 0),
            hexRadius: 30,
            sourceWidth: 100,
            sourceHeight: 50,
            scaleFactor: 0.64);

        var doubled = MapRenderLayout.GetIconDestination(
            new RenderPoint(0, 0),
            hexRadius: 30,
            sourceWidth: 100,
            sourceHeight: 50,
            scaleFactor: 0.64,
            scaleMultiplier: MapRenderLayout.LocationIconScaleMultiplier);

        Assert.That(doubled.Width, Is.EqualTo(normal.Width * 2).Within(0.0001));
        Assert.That(doubled.Height, Is.EqualTo(normal.Height * 2).Within(0.0001));
    }

    private sealed record TestEntity(string Name, int? Q, int? R);
}
