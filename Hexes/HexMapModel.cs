using Avalonia;
using Hexes;
using System.Collections.Generic;

namespace GUI;

public class Tile (byte? t)
{
    // Extend with (terrain, entities, flags, etc.)
    public byte? Terrain = t;
}

public sealed class HexMapModel
{
    public int Columns;
    public int Rows;
    public Dictionary<Hex, Tile> _tiles = new();

    public HexMapModel(int rows, int cols){
        Columns = cols;
        Rows = rows;
        for (int r=0; r < rows; r++)
        {
            for (int c=0; c < cols; c++)
            {   
                _tiles.Add(OffsetCoord.QoffsetToCube(-1, new OffsetCoord(r, c)), new Tile(0) );
            }
        }
    }

    public void SetTile(Hex h, Tile tile) => _tiles[h] = tile;

    public bool TryGetTile(Hex h, out Tile? tile) => _tiles.TryGetValue(h, out tile);

    public bool RemoveTile(Hex h) => _tiles.Remove(h);

    public void Clear() => _tiles.Clear();

    // Convert a pixel (control) coordinate to a hex, then try to fetch a tile at that hex.
    // Returns the computed hex (always) and whether a tile exists at that hex.
    public Hex PixelToHex(Avalonia.Point pixel, Layout layout)
    {
        return layout.PixelToHexRounded(new Avalonia.Point(pixel.X, pixel.Y));
    }

    public bool TryGetTileAtPixel(Avalonia.Point pixel, Layout layout, out Hex hex, out Tile? tile)
    {
        hex = PixelToHex(pixel, layout);
        return _tiles.TryGetValue(hex, out tile);
    }
}