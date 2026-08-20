// SPDX-License-Identifier: BSD-3-Clause
//
// AI assistance disclosure:
//   AI-drafted, human-reviewed. Assisted-by: Cursor Grok 4.6
//   AI-generated portions dedicated to CC0-1.0; human curation BSD-3-Clause.

using System;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Curves;

namespace NetTopologySuite.Algorithm.Construct
{
    /// <summary>
    /// Detects a hole-free circular disc (or its full-circle ring).
    /// </summary>
    /// <remarks>
    /// Maintainability: MIC and LEC share one detector.
    /// Soundness: a four-control diamond is not a disc (r would be 5/√2).
    /// Performance: closed-form centre and radius skip the grid.
    /// Port of JTS <c>f24cb33d</c>.
    /// </remarks>
    internal static class CircularDisc
    {
        private const double TwoPi = 2.0 * Math.PI;
        private const double SweepEps = 1.0e-9;

        internal static bool TryGet(Geometry g, out double cx, out double cy, out double r)
        {
            if (TryDisc(g, out cx, out cy, out r))
            {
                return true;
            }
            return TryRing(g, out cx, out cy, out r);
        }

        internal static bool TryCertifiedCircle(Geometry obstacles, Geometry boundary,
            out double cx, out double cy, out double r)
        {
            cx = cy = r = 0.0;
            if (obstacles == null || !TryGet(obstacles, out cx, out cy, out r))
            {
                return false;
            }
            if (boundary == null || boundary.IsEmpty)
            {
                return true;
            }
            if (!TryDisc(boundary, out double bx, out double by, out double br))
            {
                return false;
            }
            return Math.Sqrt((cx - bx) * (cx - bx) + (cy - by) * (cy - by)) <= 1.0e-9
                   && Math.Abs(r - br) <= 1.0e-9;
        }

        private static bool TryDisc(Geometry g, out double cx, out double cy, out double r)
        {
            cx = cy = r = 0.0;
            if (!(g is CurvePolygon cp) || cp.IsEmpty || cp.NumInteriorRings > 0)
            {
                return false;
            }
            return TryFullCircle(cp.ExteriorRing, out cx, out cy, out r);
        }

        private static bool TryRing(Geometry g, out double cx, out double cy, out double r)
        {
            return TryFullCircle(g, out cx, out cy, out r);
        }

        private static bool TryFullCircle(Geometry ring, out double cx, out double cy, out double r)
        {
            cx = cy = r = 0.0;
            if (!(ring is CircularString cs) || cs.IsEmpty || cs.NumPoints < 5 || !cs.IsClosed)
            {
                return false;
            }
            var seq = cs.CoordinateSequence;
            bool found = false;
            double sweep = 0.0;
            for (int i = 0; i + 2 < seq.Count; i += 2)
            {
                if (!TryCircumcircle(seq.GetCoordinate(i), seq.GetCoordinate(i + 1),
                        seq.GetCoordinate(i + 2), out double wcx, out double wcy, out double wr))
                {
                    return false;
                }
                if (!found)
                {
                    cx = wcx;
                    cy = wcy;
                    r = wr;
                    found = true;
                }
                else if (Math.Sqrt((cx - wcx) * (cx - wcx) + (cy - wcy) * (cy - wcy)) > 1.0e-9
                         || Math.Abs(r - wr) > 1.0e-9)
                {
                    return false;
                }
                sweep += SignedSweep(seq.GetCoordinate(i), seq.GetCoordinate(i + 1),
                    seq.GetCoordinate(i + 2), wcx, wcy);
            }
            return found && Math.Abs(Math.Abs(sweep) - TwoPi) <= SweepEps;
        }

        private static bool TryCircumcircle(Coordinate a, Coordinate b, Coordinate c,
            out double cx, out double cy, out double r)
        {
            cx = double.NaN;
            cy = double.NaN;
            r = 0.0;
            if (Orientation.Index(a, b, c) == OrientationIndex.Collinear)
            {
                return false;
            }
            double d = 2.0 * (a.X * (b.Y - c.Y) + b.X * (c.Y - a.Y) + c.X * (a.Y - b.Y));
            if (d == 0.0)
            {
                return false;
            }
            double a2 = a.X * a.X + a.Y * a.Y;
            double b2 = b.X * b.X + b.Y * b.Y;
            double c2 = c.X * c.X + c.Y * c.Y;
            cx = (a2 * (b.Y - c.Y) + b2 * (c.Y - a.Y) + c2 * (a.Y - b.Y)) / d;
            cy = (a2 * (c.X - b.X) + b2 * (a.X - c.X) + c2 * (b.X - a.X)) / d;
            r = Math.Sqrt((a.X - cx) * (a.X - cx) + (a.Y - cy) * (a.Y - cy));
            return !double.IsNaN(r) && !double.IsInfinity(r) && r != 0.0;
        }

        private static double NormPos(double angle)
        {
            angle %= TwoPi;
            if (angle < 0.0) angle += TwoPi;
            return angle;
        }

        private static double SignedSweep(Coordinate start, Coordinate mid, Coordinate end,
            double cx, double cy)
        {
            double a0 = Math.Atan2(start.Y - cy, start.X - cx);
            double aMid = Math.Atan2(mid.Y - cy, mid.X - cx);
            double a1 = Math.Atan2(end.Y - cy, end.X - cx);
            bool ccw = NormPos(aMid - a0) < NormPos(a1 - a0);
            double sweep = ccw ? NormPos(a1 - a0) : -NormPos(a0 - a1);
            if (sweep == 0.0)
            {
                sweep = ccw ? TwoPi : -TwoPi;
            }
            return sweep;
        }
    }
}
