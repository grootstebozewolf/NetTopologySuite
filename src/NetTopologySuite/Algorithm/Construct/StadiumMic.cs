// SPDX-License-Identifier: BSD-3-Clause
//
// AI assistance disclosure:
//   AI-drafted, human-reviewed. Assisted-by: Cursor Grok 4.6
//   AI-generated portions dedicated to CC0-1.0; human curation BSD-3-Clause.

using System;
using NetTopologySuite.Algorithm.ExactCurve;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Curves;

namespace NetTopologySuite.Algorithm.Construct
{
    /// <summary>
    /// Closed-form MIC of a certified stadium. A miss returns false so
    /// the caller stays on the chord path; this class never densifies.
    /// </summary>
    /// <remarks>
    /// Maintainability: four-member CompoundCurve shell, two semicircle
    /// caps plus two parallel sides.
    /// Soundness: radius is the cap radius; centre is the midpoint of
    /// the two cap centres (any medial-segment point is a MIC centre).
    /// Performance: closed-form centre and radius skip the grid.
    /// Port of JTS <c>6b1dbac1</c>.
    /// </remarks>
    internal static class StadiumMic
    {
        private const double Eps = 1.0e-9;
        private const double SweepEps = 1.0e-9;
        private const double Pi = Math.PI;

        internal static bool TryGet(Geometry g, out double cx, out double cy, out double r)
        {
            cx = cy = r = 0.0;
            if (!(g is CurvePolygon cp) || cp.IsEmpty || cp.NumInteriorRings > 0)
            {
                return false;
            }
            if (!(cp.ExteriorRing is CompoundCurve cc) || !cc.IsClosed || cc.Curves.Count != 4)
            {
                return false;
            }

            var members = new Curve[4];
            for (int i = 0; i < 4; i++)
            {
                members[i] = cc.Curves[i];
            }
            if (!AlternatingCapsAndSides(members) || !JunctionsMeet(members))
            {
                return false;
            }

            var caps = new CircularString[2];
            var sides = new LineString[2];
            Split(members, caps, sides);
            if (!IsSegment(sides[0]) || !IsSegment(sides[1]))
            {
                return false;
            }

            if (!TrySameCircle(caps[0], out double c0x, out double c0y, out double r0)
                || !TrySameCircle(caps[1], out double c1x, out double c1y, out double r1))
            {
                return false;
            }
            if (Math.Abs(r0 - r1) > Eps || r0 <= 0.0)
            {
                return false;
            }
            r = r0;
            if (!IsSemicircle(caps[0]) || !IsSemicircle(caps[1]))
            {
                return false;
            }
            if (!Parallel(sides[0], sides[1]))
            {
                return false;
            }
            if (Math.Abs(LineDistance(sides[0], sides[1]) - 2.0 * r) > Eps)
            {
                return false;
            }
            if (!OnMedial(c0x, c0y, sides, r) || !OnMedial(c1x, c1y, sides, r))
            {
                return false;
            }
            if (!CapsFaceOutward(c0x, c0y, c1x, c1y, caps[0], caps[1]))
            {
                return false;
            }

            cx = 0.5 * (c0x + c1x);
            cy = 0.5 * (c0y + c1y);
            return true;
        }

        private static bool AlternatingCapsAndSides(Curve[] m)
        {
            bool a0 = m[0] is CircularString;
            bool a1 = m[1] is CircularString;
            bool a2 = m[2] is CircularString;
            bool a3 = m[3] is CircularString;
            return a0 != a1 && a0 == a2 && a1 == a3;
        }

        private static bool JunctionsMeet(Curve[] m)
        {
            for (int i = 0; i < 4; i++)
            {
                var end = m[i].EndPoint.Coordinate;
                var start = m[(i + 1) % 4].StartPoint.Coordinate;
                if (end.Distance(start) > Eps)
                {
                    return false;
                }
            }
            return true;
        }

        private static void Split(Curve[] members, CircularString[] caps, LineString[] sides)
        {
            int ic = 0;
            int iside = 0;
            for (int i = 0; i < 4; i++)
            {
                if (members[i] is CircularString cap)
                {
                    caps[ic++] = cap;
                }
                else if (members[i] is LineString side)
                {
                    sides[iside++] = side;
                }
            }
        }

        private static bool IsSemicircle(CircularString cs)
        {
            return Math.Abs(Math.Abs(TotalSweep(cs)) - Pi) <= SweepEps;
        }

        private static bool IsSegment(LineString ls)
        {
            return ls != null && ls.NumPoints == 2
                && !ls.GetCoordinateN(0).Equals2D(ls.GetCoordinateN(1));
        }

        private static bool Parallel(LineString a, LineString b)
        {
            double dx0 = a.GetCoordinateN(1).X - a.GetCoordinateN(0).X;
            double dy0 = a.GetCoordinateN(1).Y - a.GetCoordinateN(0).Y;
            double dx1 = b.GetCoordinateN(1).X - b.GetCoordinateN(0).X;
            double dy1 = b.GetCoordinateN(1).Y - b.GetCoordinateN(0).Y;
            double cross = dx0 * dy1 - dy0 * dx1;
            double scale = Math.Sqrt(dx0 * dx0 + dy0 * dy0) * Math.Sqrt(dx1 * dx1 + dy1 * dy1);
            return Math.Abs(cross) <= Eps * Math.Max(1.0, scale);
        }

        private static double LineDistance(LineString a, LineString b)
        {
            var a0 = a.GetCoordinateN(0);
            var a1 = a.GetCoordinateN(1);
            var b0 = b.GetCoordinateN(0);
            double dx = a1.X - a0.X;
            double dy = a1.Y - a0.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len == 0.0) return double.PositiveInfinity;
            return Math.Abs((b0.X - a0.X) * dy - (b0.Y - a0.Y) * dx) / len;
        }

        private static bool OnMedial(double x, double y, LineString[] sides, double r)
        {
            return Math.Abs(PointToLine(x, y, sides[0]) - r) <= Eps
                && Math.Abs(PointToLine(x, y, sides[1]) - r) <= Eps;
        }

        private static double PointToLine(double x, double y, LineString side)
        {
            var a = side.GetCoordinateN(0);
            var b = side.GetCoordinateN(1);
            double dx = b.X - a.X;
            double dy = b.Y - a.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len == 0.0) return double.PositiveInfinity;
            return Math.Abs((x - a.X) * dy - (y - a.Y) * dx) / len;
        }

        private static bool CapsFaceOutward(double c0x, double c0y, double c1x, double c1y,
            CircularString cap0, CircularString cap1)
        {
            var m0 = cap0.CoordinateSequence.GetCoordinate(1);
            var m1 = cap1.CoordinateSequence.GetCoordinate(1);
            double d0 = (m0.X - c0x) * (c1x - c0x) + (m0.Y - c0y) * (c1y - c0y);
            double d1 = (m1.X - c1x) * (c0x - c1x) + (m1.Y - c1y) * (c0y - c1y);
            return d0 < -Eps && d1 < -Eps;
        }

        private static bool TrySameCircle(CircularString cs,
            out double cx, out double cy, out double r)
        {
            cx = cy = r = 0.0;
            var seq = cs.CoordinateSequence;
            int n = seq.Count;
            if (n < 3)
            {
                return false;
            }
            bool found = false;
            for (int i = 0; i + 2 < n; i += 2)
            {
                if (!ExactCircularArc.TryCircumcircle(seq.GetCoordinate(i),
                        seq.GetCoordinate(i + 1), seq.GetCoordinate(i + 2),
                        out double wcx, out double wcy, out double wr))
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
            }
            return found;
        }

        private static double TotalSweep(CircularString cs)
        {
            var seq = cs.CoordinateSequence;
            int n = seq.Count;
            double total = 0.0;
            for (int i = 0; i + 2 < n; i += 2)
            {
                var start = seq.GetCoordinate(i);
                var mid = seq.GetCoordinate(i + 1);
                var end = seq.GetCoordinate(i + 2);
                if (!ExactCircularArc.TryCircumcircle(start, mid, end,
                        out double wcx, out double wcy, out _))
                {
                    continue;
                }
                total += AngleBetween.Through(wcx, wcy, start, mid, end).Signed;
            }
            return total;
        }
    }
}
