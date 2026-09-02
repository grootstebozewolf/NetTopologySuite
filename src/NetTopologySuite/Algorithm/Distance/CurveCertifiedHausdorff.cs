// SPDX-License-Identifier: BSD-3-Clause
//
// AI assistance disclosure:
//   AI-drafted, human-reviewed. Assisted-by: Cursor Grok 4.6
//   AI-generated portions dedicated to CC0-1.0; human curation BSD-3-Clause.

using System;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Curves;

namespace NetTopologySuite.Algorithm.Distance
{
    /// <summary>
    /// Closed-form oriented Hausdorff for two named curve pairs.
    /// </summary>
    /// <remarks>
    /// Maintainability: both pairs share one gate.
    /// Soundness: vertex DHD on control chords misses the arc apex
    /// (√949/6 − 7/6) and the two-disc far-point (7).
    /// Performance: certified pairs skip densify.
    /// Port of JTS <c>0ca71b40</c>.
    /// </remarks>
    internal static class CurveCertifiedHausdorff
    {
        private const double TwoPi = 2.0 * Math.PI;
        private const double SweepEps = 1.0e-9;

        internal static bool TryOriented(Geometry from, Geometry to, PointPairDistance dest)
        {
            if (TryCircularDisc(from, out var da) && TryCircularDisc(to, out var db))
            {
                CircleToCircle(da, db, dest);
                return true;
            }
            if (IsSingleArc(from, out var start, out var mid, out var end)
                && IsSingleSegment(to, out var seg0, out var seg1))
            {
                ArcToSegment(start, mid, end, seg0, seg1, dest);
                return true;
            }
            return false;
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

        private static bool IsOnSweep(Coordinate p, double cx, double cy, double r,
            Coordinate start, Coordinate mid, Coordinate end)
        {
            double a0 = Math.Atan2(start.Y - cy, start.X - cx);
            double aMid = Math.Atan2(mid.Y - cy, mid.X - cx);
            double a1 = Math.Atan2(end.Y - cy, end.X - cx);
            bool ccw = NormPos(aMid - a0) < NormPos(a1 - a0);
            double sweep = ccw ? NormPos(a1 - a0) : NormPos(a0 - a1);
            if (sweep == 0.0)
            {
                sweep = TwoPi;
            }
            double angle = Math.Atan2(p.Y - cy, p.X - cx);
            double travelled = ccw ? NormPos(angle - a0) : NormPos(a0 - angle);
            return travelled <= sweep + 1.0e-12;
        }

        private static Coordinate NearestOnSegment(Coordinate p, Coordinate a, Coordinate b)
        {
            double vx = b.X - a.X;
            double vy = b.Y - a.Y;
            double len2 = vx * vx + vy * vy;
            if (len2 == 0.0)
            {
                return a;
            }
            double t = ((p.X - a.X) * vx + (p.Y - a.Y) * vy) / len2;
            if (t <= 0.0) return a;
            if (t >= 1.0) return b;
            return new Coordinate(a.X + t * vx, a.Y + t * vy);
        }

        private static bool ProjectionOnSegment(Coordinate p, Coordinate a, Coordinate b)
        {
            double vx = b.X - a.X;
            double vy = b.Y - a.Y;
            double len2 = vx * vx + vy * vy;
            if (len2 == 0.0) return false;
            double t = ((p.X - a.X) * vx + (p.Y - a.Y) * vy) / len2;
            return t >= 0.0 && t <= 1.0;
        }

        private static void ArcToSegment(Coordinate start, Coordinate mid, Coordinate end,
            Coordinate seg0, Coordinate seg1, PointPairDistance dest)
        {
            dest.SetMaximum(start, NearestOnSegment(start, seg0, seg1));
            dest.SetMaximum(end, NearestOnSegment(end, seg0, seg1));
            if (!TryCircumcircle(start, mid, end, out double cx, out double cy, out double r))
            {
                return;
            }
            double sx = seg1.X - seg0.X;
            double sy = seg1.Y - seg0.Y;
            double slen = Math.Sqrt(sx * sx + sy * sy);
            if (slen > 0.0)
            {
                double nx = -sy / slen;
                double ny = sx / slen;
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    var q = new Coordinate(cx + sign * r * nx, cy + sign * r * ny);
                    if (IsOnSweep(q, cx, cy, r, start, mid, end)
                        && ProjectionOnSegment(q, seg0, seg1))
                    {
                        dest.SetMaximum(q, NearestOnSegment(q, seg0, seg1));
                    }
                }
            }
            ConsiderArcToEndpoint(cx, cy, r, start, mid, end, seg0, seg1, dest);
            ConsiderArcToEndpoint(cx, cy, r, start, mid, end, seg1, seg0, dest);
        }

        private static void ConsiderArcToEndpoint(double cx, double cy, double r,
            Coordinate start, Coordinate mid, Coordinate end,
            Coordinate endpoint, Coordinate other, PointPairDistance dest)
        {
            dest.SetMaximum(start, NearestOnSegment(start, endpoint, other));
            dest.SetMaximum(end, NearestOnSegment(end, endpoint, other));
            double dx = endpoint.X - cx;
            double dy = endpoint.Y - cy;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist <= 0.0)
            {
                return;
            }
            var plus = new Coordinate(cx + r * dx / dist, cy + r * dy / dist);
            var minus = new Coordinate(cx - r * dx / dist, cy - r * dy / dist);
            foreach (var p in new[] { plus, minus })
            {
                if (!IsOnSweep(p, cx, cy, r, start, mid, end))
                {
                    continue;
                }
                var nearest = NearestOnSegment(p, endpoint, other);
                if (nearest.Distance(endpoint) > 1.0e-12)
                {
                    continue;
                }
                dest.SetMaximum(p, nearest);
            }
        }

        private static void CircleToCircle((double cx, double cy, double r) a,
            (double cx, double cy, double r) b, PointPairDistance dest)
        {
            double d = Math.Sqrt((a.cx - b.cx) * (a.cx - b.cx) + (a.cy - b.cy) * (a.cy - b.cy));
            if (d == 0.0)
            {
                dest.SetMaximum(new Coordinate(a.cx + a.r, a.cy), new Coordinate(b.cx + b.r, b.cy));
                return;
            }
            double ux = (a.cx - b.cx) / d;
            double uy = (a.cy - b.cy) / d;
            var far = new Coordinate(a.cx + a.r * ux, a.cy + a.r * uy);
            var farN = new Coordinate(b.cx + b.r * ux, b.cy + b.r * uy);
            var near = new Coordinate(a.cx - a.r * ux, a.cy - a.r * uy);
            double ndx = near.X - b.cx;
            double ndy = near.Y - b.cy;
            double nlen = Math.Sqrt(ndx * ndx + ndy * ndy);
            var nearN = nlen == 0.0
                ? new Coordinate(b.cx + b.r, b.cy)
                : new Coordinate(b.cx + b.r * ndx / nlen, b.cy + b.r * ndy / nlen);
            double farD = Math.Abs(d + a.r - b.r);
            double nearD = Math.Abs(Math.Abs(d - a.r) - b.r);
            if (farD >= nearD)
            {
                dest.SetMaximum(far, farN);
            }
            else
            {
                dest.SetMaximum(near, nearN);
            }
        }

        private static bool TryFullCircle(Geometry ring, out (double cx, double cy, double r) disc)
        {
            disc = default;
            if (!(ring is CircularString cs) || cs.IsEmpty || cs.NumPoints < 5 || !cs.IsClosed)
            {
                return false;
            }
            var seq = cs.CoordinateSequence;
            bool found = false;
            double cx = 0, cy = 0, r = 0;
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
            if (!found || Math.Abs(Math.Abs(sweep) - TwoPi) > SweepEps)
            {
                return false;
            }
            disc = (cx, cy, r);
            return true;
        }

        private static bool TryCircularDisc(Geometry g, out (double cx, double cy, double r) disc)
        {
            disc = default;
            if (!(g is CurvePolygon cp) || cp.IsEmpty || cp.NumInteriorRings > 0)
            {
                return false;
            }
            return TryFullCircle(cp.ExteriorRing, out disc);
        }

        private static bool IsSingleArc(Geometry g, out Coordinate start, out Coordinate mid, out Coordinate end)
        {
            start = mid = end = null;
            if (!(g is CircularString cs) || cs.IsEmpty || cs.NumPoints != 3)
            {
                return false;
            }
            var seq = cs.CoordinateSequence;
            start = seq.GetCoordinate(0);
            mid = seq.GetCoordinate(1);
            end = seq.GetCoordinate(2);
            return TryCircumcircle(start, mid, end, out _, out _, out _);
        }

        private static bool IsSingleSegment(Geometry g, out Coordinate a, out Coordinate b)
        {
            a = b = null;
            if (!(g is LineString ls) || g is CircularString || ls.NumPoints != 2)
            {
                return false;
            }
            a = ls.GetCoordinateN(0);
            b = ls.GetCoordinateN(1);
            return true;
        }
    }
}
