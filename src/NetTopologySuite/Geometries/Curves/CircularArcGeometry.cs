// SPDX-License-Identifier: BSD-3-Clause
//
// AI assistance disclosure:
//   AI-drafted, human-reviewed and curated. Per the same convention used for the
//   companion JTS prototype at grootstebozewolf/jts#1, AI-generated portions are
//   dedicated to CC0-1.0; human curation falls under the NTS BSD-3-Clause grant.
//
// The 3-point circular-arc geometry of ISO/IEC 13249-3 §7.3.1 Desc 8: the arc
// is the locus of points at distance R from the centre, where the centre is the
// intersection of the perpendicular bisectors of the two chords and R is the
// distance from that centre to any of the three points; traversal runs
// start → intermediate → end (Desc 8a). A collinear triple degenerates to the
// straight line from start to end (Desc 8b). This is the shared seam for the
// arc-aware metrics (Length; Envelope and Distance follow — NetTopologySuite.Proofs
// issue #615, tickets 615-d/e/f).

using NetTopologySuite.Algorithm;

// The planar-geometry Triangle, not the OGC Triangle curve type in this namespace.
using GeomTriangle = NetTopologySuite.Geometries.Triangle;

namespace NetTopologySuite.Geometries.Curves
{
    /// <summary>
    /// Exact geometry of a single 3-point circular-arc segment
    /// (ISO/IEC 13249-3 §7.3.1 Desc 8). Pure functions over coordinates;
    /// no linearization anywhere on this path.
    /// </summary>
    internal static class CircularArcGeometry
    {
        /// <summary>
        /// Computes the circle carrying the arc through <paramref name="p0"/>,
        /// <paramref name="p1"/>, <paramref name="p2"/>.
        /// </summary>
        /// <returns>
        /// <c>false</c> when the triple is collinear (Desc 8b: the segment
        /// degenerates to the start–end chord; centre and radius are undefined).
        /// </returns>
        public static bool TryCircle(Coordinate p0, Coordinate p1, Coordinate p2,
            out Coordinate centre, out double radius)
        {
            if (OrientationIndex(p0, p1, p2) == 0d)
            {
                centre = null;
                radius = double.NaN;
                return false;
            }
            centre = GeomTriangle.Circumcentre(p0, p1, p2);
            radius = centre.Distance(p0);
            return true;
        }

        /// <summary>
        /// The angle swept by the arc from <paramref name="p0"/> through
        /// <paramref name="p1"/> to <paramref name="p2"/>, in (0, 2π).
        /// The traversal direction is the orientation of the control triple:
        /// a CCW triple sweeps counter-clockwise (and thereby passes through
        /// the intermediate point), a CW triple clockwise.
        /// </summary>
        public static double SweepAngle(Coordinate p0, Coordinate p2,
            Coordinate centre, bool counterClockwise)
        {
            double a0 = System.Math.Atan2(p0.Y - centre.Y, p0.X - centre.X);
            double a2 = System.Math.Atan2(p2.Y - centre.Y, p2.X - centre.X);
            return counterClockwise
                ? AngleUtility.NormalizePositive(a2 - a0)
                : AngleUtility.NormalizePositive(a0 - a2);
        }

        /// <summary>
        /// The exact metric length of one arc segment: r·θ over the locus
        /// (Desc 8a), or the start–end chord length for a collinear triple
        /// (Desc 8b) — which also covers a coincident start/end pair, whose
        /// chord is zero (such a value is ill-formed per Desc 6; flagging it
        /// is arc-aware IsValid's job, ticket 615-g, not Length's).
        /// </summary>
        public static double SegmentLength(Coordinate p0, Coordinate p1, Coordinate p2)
        {
            if (!TryCircle(p0, p1, p2, out var centre, out double radius))
                return p0.Distance(p2);
            return radius * SweepAngle(p0, p2, centre, OrientationIndex(p0, p1, p2) > 0d);
        }

        /// <summary>
        /// The four axis directions, as exact unit vectors so a detected
        /// crossing contributes centre ± r exactly (no cos/sin noise).
        /// Index k is the direction at angle k·π/2.
        /// </summary>
        private static readonly (double X, double Y)[] AxisDirections =
        {
            (1, 0), (0, 1), (-1, 0), (0, -1),
        };

        /// <summary>
        /// Expands <paramref name="env"/> to cover one arc segment's locus
        /// (ISO/IEC 13249-3 §5.1.19 Desc 2b over §7.3.1 Desc 8): the two
        /// endpoints, plus centre ± r on each axis direction the sweep passes.
        /// A collinear triple contributes its start–end chord only — the
        /// intermediate control point is not part of the locus (Desc 8b).
        /// </summary>
        public static void ExpandEnvelope(Coordinate p0, Coordinate p1, Coordinate p2, Envelope env)
        {
            env.ExpandToInclude(p0);
            env.ExpandToInclude(p2);
            if (!TryCircle(p0, p1, p2, out var centre, out double radius))
                return;
            bool ccw = OrientationIndex(p0, p1, p2) > 0d;
            double a0 = System.Math.Atan2(p0.Y - centre.Y, p0.X - centre.X);
            double sweep = SweepAngle(p0, p2, centre, ccw);
            for (int k = 0; k < 4; k++)
            {
                double axisAngle = k * (System.Math.PI / 2);
                double delta = AngleUtility.NormalizePositive(
                    ccw ? axisAngle - a0 : a0 - axisAngle);
                if (delta <= sweep)
                {
                    env.ExpandToInclude(
                        centre.X + radius * AxisDirections[k].X,
                        centre.Y + radius * AxisDirections[k].Y);
                }
            }
        }

        /// <summary>
        /// The exact distance from <paramref name="point"/> to one arc
        /// segment's locus (ISO/IEC 13249-3 §5.1.41 Desc 2a over §7.3.1
        /// Desc 8): project the point onto the carrying circle; when the
        /// projection's angle lies within the sweep the answer is the radial
        /// gap |d − r| (zero on the locus, Desc 2a-iii — intersect → 0), and
        /// otherwise the nearer endpoint. The centre itself is at distance r
        /// from every locus point. A collinear triple measures against its
        /// start–end chord (Desc 8b).
        /// </summary>
        public static double SegmentDistance(Coordinate point, Coordinate p0, Coordinate p1, Coordinate p2)
        {
            if (!TryCircle(p0, p1, p2, out var centre, out double radius))
                return new LineSegment(p0, p2).Distance(point);
            double dx = point.X - centre.X;
            double dy = point.Y - centre.Y;
            double d = System.Math.Sqrt(dx * dx + dy * dy);
            if (d == 0d)
                return radius;
            bool ccw = OrientationIndex(p0, p1, p2) > 0d;
            double a0 = System.Math.Atan2(p0.Y - centre.Y, p0.X - centre.X);
            double sweep = SweepAngle(p0, p2, centre, ccw);
            double angle = System.Math.Atan2(dy, dx);
            double delta = AngleUtility.NormalizePositive(ccw ? angle - a0 : a0 - angle);
            if (delta <= sweep)
                return System.Math.Abs(d - radius);
            return System.Math.Min(point.Distance(p0), point.Distance(p2));
        }

        /// <summary>
        /// Twice the signed area of the control triple: positive for CCW,
        /// zero exactly when collinear. Plain double arithmetic — the sign
        /// selects the traversal direction and the zero selects Desc 8b;
        /// near-degenerate inputs stay on the arc path with a large radius.
        /// </summary>
        private static double OrientationIndex(Coordinate p0, Coordinate p1, Coordinate p2)
        {
            return (p1.X - p0.X) * (p2.Y - p0.Y) - (p1.Y - p0.Y) * (p2.X - p0.X);
        }
    }
}
