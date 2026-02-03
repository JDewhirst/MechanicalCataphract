// Credit to RedBlobGames for this -- http://www.redblobgames.com/grids/hexagons/

using System;
using System.Linq;
using System.Collections.Generic;

namespace Hexes
{
    public struct Hex
    {
        public Hex(int q, int r, int s)
        {
            this.q = q;
            this.r = r;
            this.s = -q-r;
            if (q + r + s != 0) throw new ArgumentException("q + r + s must be 0");
        }
        public readonly int q;
        public readonly int r;
        public readonly int s;

        public Hex Add(Hex b)
        {
            return new Hex(q + b.q, r + b.r, s + b.s);
        }


        public Hex Subtract(Hex b)
        {
            return new Hex(q - b.q, r - b.r, s - b.s);
        }


        public Hex Scale(int k)
        {
            return new Hex(q * k, r * k, s * k);
        }


        public Hex RotateLeft()
        {
            return new Hex(-s, -q, -r);
        }


        public Hex RotateRight()
        {
            return new Hex(-r, -s, -q);
        }

        static public List<Hex> directions = [new Hex(1, 0, -1), new Hex(1, -1, 0), new Hex(0, -1, 1), new Hex(-1, 0, 1), new Hex(-1, 1, 0), new Hex(0, 1, -1)];

        static public Hex Direction(int direction)
        {
            return Hex.directions[direction];
        }


        public Hex Neighbor(int direction)
        {
            return Add(Hex.Direction(direction));
        }

        static public List<Hex> diagonals = new List<Hex> { new Hex(2, -1, -1), new Hex(1, -2, 1), new Hex(-1, -1, 2), new Hex(-2, 1, 1), new Hex(-1, 2, -1), new Hex(1, 1, -2) };

        public Hex DiagonalNeighbor(int direction)
        {
            return Add(Hex.diagonals[direction]);
        }


        public int Length()
        {
            return (int)((Math.Abs(q) + Math.Abs(r) + Math.Abs(s)) / 2);
        }


        public int Distance(Hex b)
        {
            return Subtract(b).Length();
        }

        public int? DirectionTo(Hex neighbor)
        {
            for (int dir = 0; dir < 6; dir++)
            {
                if (Neighbor(dir).q == neighbor.q && Neighbor(dir).r == neighbor.r)
                    return dir;
            }
            return null; // Not adjacent
        }
    }

    public struct FractionalHex
    {
        public FractionalHex(double q, double r, double s)
        {
            this.q = q;
            this.r = r;
            this.s = s;
            if (Math.Round(q + r + s) != 0) throw new ArgumentException("q + r + s must be 0");
        }
        public readonly double q;
        public readonly double r;
        public readonly double s;

        //Rounding turns a fractional hex coordinate into the nearest integer hex coordinate.
        public Hex HexRound()
        {
            int qi = (int)(Math.Round(q));
            int ri = (int)(Math.Round(r));
            int si = (int)(Math.Round(s));
            double q_diff = Math.Abs(qi - q);
            double r_diff = Math.Abs(ri - r);
            double s_diff = Math.Abs(si - s);
            if (q_diff > r_diff && q_diff > s_diff)
            {
                qi = -ri - si;
            }
            else
                if (r_diff > s_diff)
            {
                ri = -qi - si;
            }
            else
            {
                si = -qi - ri;
            }
            return new Hex(qi, ri, si);
        }


        public FractionalHex HexLerp(FractionalHex b, double t)
        {
            return new FractionalHex(q * (1.0 - t) + b.q * t, r * (1.0 - t) + b.r * t, s * (1.0 - t) + b.s * t);
        }


        static public List<Hex> HexLinedraw(Hex a, Hex b)
        {
            int N = a.Distance(b);
            FractionalHex a_nudge = new FractionalHex(a.q + 1e-06, a.r + 1e-06, a.s - 2e-06);
            FractionalHex b_nudge = new FractionalHex(b.q + 1e-06, b.r + 1e-06, b.s - 2e-06);
            List<Hex> results = new List<Hex> { };
            double step = 1.0 / Math.Max(N, 1);
            for (int i = 0; i <= N; i++)
            {
                results.Add(a_nudge.HexLerp(b_nudge, step * i).HexRound());
            }
            return results;
        }

    }

    public struct OffsetCoord
    {
        public OffsetCoord(int col, int row)
        {
            this.col = col;
            this.row = row;
        }
        public readonly int col;
        public readonly int row;
        static public int EVEN = 1;
        static public int ODD = -1;

        static public OffsetCoord QoffsetFromCube(int offset, Hex h)
        {
            int parity = h.q & 1;
            int col = h.q;
            int row = h.r + (int)((h.q + offset * parity) / 2);
            if (offset != OffsetCoord.EVEN && offset != OffsetCoord.ODD)
            {
                throw new ArgumentException("offset must be EVEN (+1) or ODD (-1)");
            }
            return new OffsetCoord(col, row);
        }


        static public Hex QoffsetToCube(int offset, OffsetCoord h)
        {
            int parity = h.col & 1;
            int q = h.col;
            int r = h.row - (int)((h.col + offset * parity) / 2);
            int s = -q - r;
            if (offset != OffsetCoord.EVEN && offset != OffsetCoord.ODD)
            {
                throw new ArgumentException("offset must be EVEN (+1) or ODD (-1)");
            }
            return new Hex(q, r, s);
        }


        static public OffsetCoord RoffsetFromCube(int offset, Hex h)
        {
            int parity = h.r & 1;
            int col = h.q + (int)((h.r + offset * parity) / 2);
            int row = h.r;
            if (offset != OffsetCoord.EVEN && offset != OffsetCoord.ODD)
            {
                throw new ArgumentException("offset must be EVEN (+1) or ODD (-1)");
            }
            return new OffsetCoord(col, row);
        }


        static public Hex RoffsetToCube(int offset, OffsetCoord h)
        {
            int parity = h.row & 1;
            int q = h.col - (int)((h.row + offset * parity) / 2);
            int r = h.row;
            int s = -q - r;
            if (offset != OffsetCoord.EVEN && offset != OffsetCoord.ODD)
            {
                throw new ArgumentException("offset must be EVEN (+1) or ODD (-1)");
            }
            return new Hex(q, r, s);
        }


        static public OffsetCoord QoffsetFromQdoubled(int offset, DoubledCoord h)
        {
            int parity = h.col & 1;
            return new OffsetCoord(h.col, (int)((h.row + offset * parity) / 2));
        }


        static public DoubledCoord QoffsetToQdoubled(int offset, OffsetCoord h)
        {
            int parity = h.col & 1;
            return new DoubledCoord(h.col, 2 * h.row - offset * parity);
        }


         public OffsetCoord RoffsetFromRdoubled(int offset, DoubledCoord h)
        {
            int parity = h.row & 1;
            return new OffsetCoord((int)((h.col + offset * parity) / 2), h.row);
        }


        static public DoubledCoord RoffsetToRdoubled(int offset, OffsetCoord h)
        {
            int parity = h.row & 1;
            return new DoubledCoord(2 * h.col - offset * parity, h.row);
        }

    }

    public struct DoubledCoord
    {
        public DoubledCoord(int col, int row)
        {
            this.col = col;
            this.row = row;
        }
        public readonly int col;
        public readonly int row;

        static public DoubledCoord QdoubledFromCube(Hex h)
        {
            int col = h.q;
            int row = 2 * h.r + h.q;
            return new DoubledCoord(col, row);
        }


        public Hex QdoubledToCube()
        {
            int q = col;
            int r = (int)((row - col) / 2);
            int s = -q - r;
            return new Hex(q, r, s);
        }


        static public DoubledCoord RdoubledFromCube(Hex h)
        {
            int col = 2 * h.q + h.r;
            int row = h.r;
            return new DoubledCoord(col, row);
        }


        public Hex RdoubledToCube()
        {
            int q = (int)((col - row) / 2);
            int r = row;
            int s = -q - r;
            return new Hex(q, r, s);
        }

    }

    public struct Orientation
    {
        public Orientation(double f0, double f1, double f2, double f3, double b0, double b1, double b2, double b3, double start_angle)
        {
            this.f0 = f0;
            this.f1 = f1;
            this.f2 = f2;
            this.f3 = f3;
            this.b0 = b0;
            this.b1 = b1;
            this.b2 = b2;
            this.b3 = b3;
            this.start_angle = start_angle;
        }
        public readonly double f0;
        public readonly double f1;
        public readonly double f2;
        public readonly double f3;
        public readonly double b0;
        public readonly double b1;
        public readonly double b2;
        public readonly double b3;
        public readonly double start_angle;
    }

    public struct Layout
    {
        public Layout(Orientation orientation, Avalonia.Point size, Avalonia.Point origin)
        {
            this.orientation = orientation;
            this.size = size;
            this.origin = origin;
        }
        public readonly Orientation orientation;
        public readonly Avalonia.Point size;
        public readonly Avalonia.Point origin;
        static public Orientation pointy = new Orientation(Math.Sqrt(3.0), Math.Sqrt(3.0) / 2.0, 0.0, 3.0 / 2.0, Math.Sqrt(3.0) / 3.0, -1.0 / 3.0, 0.0, 2.0 / 3.0, 0.5);
        static public Orientation flat = new Orientation(3.0 / 2.0, 0.0, Math.Sqrt(3.0) / 2.0, Math.Sqrt(3.0), 2.0 / 3.0, 0.0, -1.0 / 3.0, Math.Sqrt(3.0) / 3.0, 0.0);

        public Avalonia.Point HexToPixel(Hex h)
        {
            Orientation M = orientation;
            double x = (M.f0 * h.q + M.f1 * h.r) * size.X;
            double y = (M.f2 * h.q + M.f3 * h.r) * size.Y;
            return new Avalonia.Point(x + origin.X, y + origin.Y);
        }


        public FractionalHex PixelToHexFractional(Avalonia.Point p)
        {
            Orientation M = orientation;
            Avalonia.Point pt = new Avalonia.Point((p.X - origin.X) / size.X, (p.Y - origin.Y) / size.Y);
            double q = M.b0 * pt.X + M.b1 * pt.Y;
            double r = M.b2 * pt.X + M.b3 * pt.Y;
            return new FractionalHex(q, r, -q - r);
        }


        public Hex PixelToHexRounded(Avalonia.Point p)
        {
            return PixelToHexFractional(p).HexRound();
        }


        public Avalonia.Point HexCornerOffset(int corner)
        {
            Orientation M = orientation;
            double angle = 2.0 * Math.PI * (M.start_angle - corner) / 6.0;
            return new Avalonia.Point(size.X * Math.Cos(angle), size.Y * Math.Sin(angle));
        }


        public List<Avalonia.Point> PolygonCorners(Hex h)
        {
            List<Avalonia.Point> corners = new List<Avalonia.Point> { };
            Avalonia.Point center = HexToPixel(h);
            for (int i = 0; i < 6; i++)
            {
                Avalonia.Point offset = HexCornerOffset(i);
                corners.Add(new Avalonia.Point(center.X + offset.X, center.Y + offset.Y));
            }
            return corners;
        }

    }
}
