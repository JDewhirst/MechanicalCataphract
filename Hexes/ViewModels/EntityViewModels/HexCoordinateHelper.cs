using Hexes;

namespace GUI.ViewModels.EntityViewModels;

/// <summary>
/// Shared Col/Row ↔ Q/R offset coordinate conversion logic.
/// Eliminates ~200 lines of identical conversion code across entity ViewModels.
/// </summary>
public static class HexCoordinateHelper
{
    public static int? GetCol(int? q, int? r)
        => q == null || r == null ? null
        : OffsetCoord.QoffsetFromCube(OffsetCoord.ODD, new Hex(q.Value, r.Value, -q.Value - r.Value)).col;

    public static int? GetRow(int? q, int? r)
        => q == null || r == null ? null
        : OffsetCoord.QoffsetFromCube(OffsetCoord.ODD, new Hex(q.Value, r.Value, -q.Value - r.Value)).row;

    /// <summary>
    /// Converts a new column value (with current row) to cube coordinates.
    /// Returns null if out of bounds.
    /// </summary>
    public static (int q, int r)? SetCol(int? newCol, int? currentRow, int mapCols, int mapRows)
    {
        if (newCol == null) return null;
        int row = currentRow is int r && r >= 0 ? r : 0;
        if (!IsOffsetInBounds(newCol.Value, row, mapCols, mapRows)) return null;
        var hex = OffsetCoord.QoffsetToCube(OffsetCoord.ODD, new OffsetCoord(newCol.Value, row));
        return (hex.q, hex.r);
    }

    /// <summary>
    /// Converts a new row value (with current col) to cube coordinates.
    /// Returns null if out of bounds.
    /// </summary>
    public static (int q, int r)? SetRow(int? newRow, int? currentCol, int mapCols, int mapRows)
    {
        if (newRow == null) return null;
        int col = currentCol is int c && c >= 0 ? c : 0;
        if (!IsOffsetInBounds(col, newRow.Value, mapCols, mapRows)) return null;
        var hex = OffsetCoord.QoffsetToCube(OffsetCoord.ODD, new OffsetCoord(col, newRow.Value));
        return (hex.q, hex.r);
    }

    public static bool IsOffsetInBounds(int col, int row, int mapCols, int mapRows)
        => col >= 0 && col < mapCols && row >= 0 && row < mapRows;
}
