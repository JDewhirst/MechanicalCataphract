using Hexes;
using Avalonia;

namespace MechanicalCataphract.Tests.Hexes;

[TestFixture]
public class LayoutTests
{
    private Layout _flatLayout;
    private Layout _pointyLayout;

    [SetUp]
    public void Setup()
    {
        _flatLayout = new Layout(Layout.flat, new Point(10, 10), new Point(0, 0));
        _pointyLayout = new Layout(Layout.pointy, new Point(10, 10), new Point(0, 0));
    }

    [Test]
    public void HexToPixel_Origin_ReturnsLayoutOrigin()
    {
        var origin = new Hex(0, 0, 0);
        var pixel = _flatLayout.HexToPixel(origin);
        Assert.That(pixel.X, Is.EqualTo(0.0).Within(1e-6));
        Assert.That(pixel.Y, Is.EqualTo(0.0).Within(1e-6));
    }

    [Test]
    public void HexToPixel_WithOffset_ReturnsOffsetOrigin()
    {
        var layout = new Layout(Layout.flat, new Point(10, 10), new Point(100, 200));
        var origin = new Hex(0, 0, 0);
        var pixel = layout.HexToPixel(origin);
        Assert.That(pixel.X, Is.EqualTo(100.0).Within(1e-6));
        Assert.That(pixel.Y, Is.EqualTo(200.0).Within(1e-6));
    }

    [Test]
    public void PixelToHexRounded_Roundtrip_Flat()
    {
        var hex = new Hex(2, -1, -1);
        var pixel = _flatLayout.HexToPixel(hex);
        var back = _flatLayout.PixelToHexRounded(pixel);
        Assert.That(back.q, Is.EqualTo(hex.q));
        Assert.That(back.r, Is.EqualTo(hex.r));
    }

    [Test]
    public void PixelToHexRounded_Roundtrip_Pointy()
    {
        var hex = new Hex(2, -1, -1);
        var pixel = _pointyLayout.HexToPixel(hex);
        var back = _pointyLayout.PixelToHexRounded(pixel);
        Assert.That(back.q, Is.EqualTo(hex.q));
        Assert.That(back.r, Is.EqualTo(hex.r));
    }

    [Test]
    public void PolygonCorners_ReturnsSixPoints()
    {
        var hex = new Hex(0, 0, 0);
        var corners = _flatLayout.PolygonCorners(hex);
        Assert.That(corners, Has.Count.EqualTo(6));
    }

    [Test]
    public void HexToPixel_KnownValues_Flat()
    {
        // For flat-top with size (10,10) and origin (0,0):
        // Hex(1,0,-1): x = (3/2 * 1 + 0 * 0) * 10 = 15
        //              y = (sqrt(3)/2 * 1 + sqrt(3) * 0) * 10 = 5*sqrt(3) ≈ 8.66
        var hex = new Hex(1, 0, -1);
        var pixel = _flatLayout.HexToPixel(hex);
        Assert.That(pixel.X, Is.EqualTo(15.0).Within(1e-6));
        Assert.That(pixel.Y, Is.EqualTo(10.0 * Math.Sqrt(3.0) / 2.0).Within(1e-6));
    }
}
