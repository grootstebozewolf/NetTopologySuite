// SPDX-License-Identifier: BSD-3-Clause
//
// AI assistance disclosure:
//   AI-drafted, human-reviewed. Assisted-by: Cursor Grok 4.6
//   AI-generated portions dedicated to CC0-1.0; human curation BSD-3-Clause.

using System;
using NetTopologySuite.Algorithm;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Curves;

namespace NetTopologySuite.Operation.Relate
{
    /// <summary>
    /// T-ext laser: two externally tangent circular discs are FF2F01212.
    /// </summary>
    /// <remarks>
    /// Maintainability: one gate for Relate and Touches on both relate engines.
    /// Soundness: T-ext is d² == (r1+r2)² in R²; control diamonds miss (4, 3).
    /// Performance: certified pair skips noding.
    /// Port of JTS <c>cd426d0f</c>.
    /// </remarks>
    internal static class CurveCertifiedRelate
    {
        internal const string AreaExtTangent = "FF2F01212";

        private const double TwoPi = 2.0 * Math.PI;
        private const double SweepEps = 1.0e-9;

        internal static bool TryDiscExternalTouch(Geometry a, Geometry b, out IntersectionMatrix im)
        {
            im = null;
            if (!TryCircularDisc(a, out double ax, out double ay, out double ar)
                || !TryCircularDisc(b, out double bx, out double by, out double br))
            {
                return false;
            }
            double dx = ax - bx;
            double dy = ay - by;
            double d2 = dx * dx + dy * dy;
            double sum = ar + br;
            double sum2 = sum * sum;
            if (d2 != sum2)
            {
                return false;
            }
            im = new IntersectionMatrix(AreaExtTangent);
            return true;
        }

        private static bool TryCircularDisc(Geometry g, out double cx, out double cy, out double r)
        {
            cx = cy = r = 0.0;
            if (!(g is CurvePolygon cp) || cp.IsEmpty || cp.NumInteriorRings > 0)
            {
                return false;
            }
            return TryFullCircle(cp.ExteriorRing, out cx, out cy, out r);
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
