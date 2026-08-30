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
            // Canonicalize the triple (lexicographic order) before computing
            // the circle, so arcs carried by the same circle through the same
            // control points get bit-identical centre/radius regardless of
            // traversal order — the exact-cocircular arm of the simplicity
            // kernel compares circles by equality (615-h rung 3, #634 review
            // scope item from #630).
            Sort3(ref p0, ref p1, ref p2);
            centre = GeomTriangle.Circumcentre(p0, p1, p2);
            radius = centre.Distance(p0);
            return true;
        }

        private static void Sort3(ref Coordinate a, ref Coordinate b, ref Coordinate c)
        {
            if (Lex(b, a)) (a, b) = (b, a);
            if (Lex(c, b)) (b, c) = (c, b);
            if (Lex(b, a)) (a, b) = (b, a);
        }

        private static bool Lex(Coordinate p, Coordinate q)
        {
            return p.X < q.X || (p.X == q.X && p.Y < q.Y);
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

        // ---------------------------------------------------------------
        // Pairwise segment contacts (NetTopologySuite.Proofs #615, ticket
        // 615-h rung 2, issue #630 there): the point set where two segment
        // loci meet, mirroring the composition of the Proofs oracle's
        // RING_SIMPLE lane (arc-arc via the radical line, exact-cocircular
        // via angular intervals, arc-chord via the circle-line quadratic,
        // chord-chord via RobustLineIntersector).
        // ---------------------------------------------------------------

        /// <summary>Outcome of <see cref="SegmentPairContacts"/>.</summary>
        public enum SegmentPairResult
        {
            /// <summary>The contact set was computed.</summary>
            Decided,

            /// <summary>
            /// The two arcs' circumcircles are too close to distinguish from
            /// one circle at double precision, but not exactly equal — the
            /// kernel refuses to guess between "same circle, interval
            /// overlap" and "two circles, radical line" (a wrong guess here
            /// flips a 1-dimensional overlap into nothing, or vice versa).
            /// </summary>
            AmbiguousCocircular,

            /// <summary>
            /// A circumradius is too large relative to the coordinate scale
            /// for a double-precision contact decision: the r² terms in the
            /// radical-line / circle-chord algebra carry absolute error of
            /// order eps·r², which would swamp the match tolerance — the
            /// review of this rung demonstrated silently wrong verdicts in
            /// both directions for such near-collinear control triples. The
            /// kernel refuses rather than guessing; exact arithmetic (the
            /// oracle's path) is the way to widen this.
            /// </summary>
            IllConditioned,
        }

        /// <summary>
        /// Relative tolerance for the near-cocircular refusal band and for
        /// vertex matching by callers — the same 1e-9 relative epsilon the
        /// oracle's RING_SIMPLE lane uses for its permitted-vertex filter.
        /// </summary>
        public const double RelativeTolerance = 1e-9;

        /// <summary>
        /// Computes the contact points of two segment loci (each segment an
        /// arc per Desc 8a or, when collinear, its start–end chord per
        /// Desc 8b). <paramref name="overlap"/> reports a 1-dimensional
        /// shared piece (collinear chord overlap, or cocircular arcs with
        /// overlapping angular intervals); when it is set, the contact list
        /// is not populated further. Degenerate segments (start == end) are
        /// the caller's job to pre-filter.
        /// </summary>
        public static SegmentPairResult SegmentPairContacts(
            Coordinate p0, Coordinate p1, Coordinate p2,
            Coordinate q0, Coordinate q1, Coordinate q2,
            System.Collections.Generic.List<Coordinate> contacts, out bool overlap)
        {
            overlap = false;
            bool aIsArc = TryCircle(p0, p1, p2, out var ca, out double ra);
            bool bIsArc = TryCircle(q0, q1, q2, out var cb, out double rb);

            if (!aIsArc && !bIsArc)
            {
                ChordChordContacts(p0, p2, q0, q2, contacts, ref overlap);
                return SegmentPairResult.Decided;
            }

            // Conditioning guard (review-caught on this rung): the r² terms
            // below carry absolute error ~eps·r², so a circumradius far
            // beyond the coordinate scale silently flips verdicts. Refuse
            // whenever that error could exceed the match tolerance.
            double scale = 1 + MaxAbs(p0, p1, p2, q0, q1, q2);
            double rMax = System.Math.Max(aIsArc ? ra : 0, bIsArc ? rb : 0);
            const double machineEpsilon = 2.220446049250313e-16;
            if (machineEpsilon * rMax * rMax > RelativeTolerance * scale * scale)
                return SegmentPairResult.IllConditioned;

            if (aIsArc && !bIsArc)
            {
                ArcChordContacts(ca, ra, ArcOf(p0, p1, p2, ca), q0, q2, contacts);
                return SegmentPairResult.Decided;
            }
            if (!aIsArc)
            {
                ArcChordContacts(cb, rb, ArcOf(q0, q1, q2, cb), p0, p2, contacts);
                return SegmentPairResult.Decided;
            }

            var arcA = ArcOf(p0, p1, p2, ca);
            var arcB = ArcOf(q0, q1, q2, cb);
            bool exactlyCocircular = ca.Equals2D(cb) && ra == rb;
            if (!exactlyCocircular)
            {
                double tol = RelativeTolerance * (1 + System.Math.Max(ra, rb));
                if (ca.Distance(cb) <= tol && System.Math.Abs(ra - rb) <= tol)
                    return SegmentPairResult.AmbiguousCocircular;
            }
            if (exactlyCocircular)
                CocircularContacts(ca, arcA, p0, p2, arcB, q0, q2, contacts, ref overlap);
            else
                TwoCircleContacts(ca, ra, arcA, cb, rb, arcB, contacts);
            return SegmentPairResult.Decided;
        }

        /// <summary>Sweep frame of one arc: start angle, width, direction.</summary>
        private readonly struct ArcFrame
        {
            public readonly double A0;
            public readonly double Sweep;
            public readonly bool Ccw;

            public ArcFrame(double a0, double sweep, bool ccw)
            {
                A0 = a0;
                Sweep = sweep;
                Ccw = ccw;
            }
        }

        private static ArcFrame ArcOf(Coordinate p0, Coordinate p1, Coordinate p2, Coordinate centre)
        {
            bool ccw = OrientationIndex(p0, p1, p2) > 0d;
            double a0 = System.Math.Atan2(p0.Y - centre.Y, p0.X - centre.X);
            return new ArcFrame(a0, SweepAngle(p0, p2, centre, ccw), ccw);
        }

        /// <summary>
        /// Endpoint-inclusive sweep membership — the same normalization
        /// convention as <see cref="SegmentDistance"/> and
        /// <see cref="ExpandEnvelope"/>.
        /// </summary>
        private static bool OnArc(Coordinate centre, ArcFrame arc, double x, double y)
        {
            double theta = System.Math.Atan2(y - centre.Y, x - centre.X);
            double delta = AngleUtility.NormalizePositive(arc.Ccw ? theta - arc.A0 : arc.A0 - theta);
            return delta <= arc.Sweep;
        }

        private static void ChordChordContacts(
            Coordinate p0, Coordinate p2, Coordinate q0, Coordinate q2,
            System.Collections.Generic.List<Coordinate> contacts, ref bool overlap)
        {
            var li = new RobustLineIntersector();
            li.ComputeIntersection(p0, p2, q0, q2);
            if (li.IntersectionNum == 2)
            {
                var i0 = li.GetIntersection(0);
                var i1 = li.GetIntersection(1);
                if (i0.Equals2D(i1))
                    contacts.Add(i0.Copy());
                else
                    overlap = true;
            }
            else if (li.IntersectionNum == 1)
            {
                contacts.Add(li.GetIntersection(0).Copy());
            }
        }

        private static void ArcChordContacts(
            Coordinate centre, double radius, ArcFrame arc,
            Coordinate q0, Coordinate q2,
            System.Collections.Generic.List<Coordinate> contacts)
        {
            double dx = q2.X - q0.X, dy = q2.Y - q0.Y;
            double fx = q0.X - centre.X, fy = q0.Y - centre.Y;
            double a = dx * dx + dy * dy;
            double b = 2 * (fx * dx + fy * dy);
            double c = fx * fx + fy * fy - radius * radius;
            double disc = b * b - 4 * a * c;
            if (disc < 0)
                return;
            double sq = System.Math.Sqrt(disc);
            for (int k = 0; k < (sq == 0d ? 1 : 2); k++)
            {
                double t = (-b + (k == 0 ? -sq : sq)) / (2 * a);
                if (t < 0 || t > 1)
                    continue;
                double x = q0.X + t * dx, y = q0.Y + t * dy;
                if (OnArc(centre, arc, x, y))
                    contacts.Add(new Coordinate(x, y));
            }
        }

        private static double MaxAbs(params Coordinate[] coords)
        {
            double max = 0;
            foreach (var c in coords)
            {
                max = System.Math.Max(max, System.Math.Abs(c.X));
                max = System.Math.Max(max, System.Math.Abs(c.Y));
            }
            return max;
        }

        private static void TwoCircleContacts(
            Coordinate ca, double ra, ArcFrame arcA,
            Coordinate cb, double rb, ArcFrame arcB,
            System.Collections.Generic.List<Coordinate> contacts)
        {
            double d = ca.Distance(cb);
            // (ra−rb)(ra+rb) and (ra−a)(ra+a): the stable difference-of-
            // squares forms; the conditioning guard upstream bounds what
            // error the remaining r-terms can carry.
            double a = (d * d + (ra - rb) * (ra + rb)) / (2 * d);
            double h2 = (ra - a) * (ra + a);
            if (h2 < 0)
                return;
            double ux = (cb.X - ca.X) / d, uy = (cb.Y - ca.Y) / d;
            double bx = ca.X + a * ux, by = ca.Y + a * uy;
            double h = System.Math.Sqrt(h2);
            for (int k = 0; k < (h == 0d ? 1 : 2); k++)
            {
                double sign = k == 0 ? 1 : -1;
                double x = bx + sign * h * -uy;
                double y = by + sign * h * ux;
                if (OnArc(ca, arcA, x, y) && OnArc(cb, arcB, x, y))
                    contacts.Add(new Coordinate(x, y));
            }
        }

        private static void CocircularContacts(
            Coordinate centre,
            ArcFrame arcA, Coordinate p0, Coordinate p2,
            ArcFrame arcB, Coordinate q0, Coordinate q2,
            System.Collections.Generic.List<Coordinate> contacts, ref bool overlap)
        {
            // Normalize each arc to a CCW interval [s, s + w] on the shared
            // circle. Positive-length overlap iff one interval's start lies
            // in the other's interior (start coincidence included) — which
            // also catches the two-reflex case w_A + w_B > 2π.
            double sA = arcA.Ccw ? arcA.A0 : AngleOf(p2, centre);
            double sB = arcB.Ccw ? arcB.A0 : AngleOf(q2, centre);
            if (AngleUtility.NormalizePositive(sB - sA) < arcA.Sweep
                || AngleUtility.NormalizePositive(sA - sB) < arcB.Sweep)
            {
                overlap = true;
                return;
            }

            // Touch-only: report endpoints of one arc lying (inclusively) on
            // the other, as the ORIGINAL endpoint coordinates.
            AddEndpointContact(q0, centre, arcA, contacts);
            AddEndpointContact(q2, centre, arcA, contacts);
            AddEndpointContact(p0, centre, arcB, contacts);
            AddEndpointContact(p2, centre, arcB, contacts);
        }

        private static void AddEndpointContact(
            Coordinate endpoint, Coordinate centre, ArcFrame other,
            System.Collections.Generic.List<Coordinate> contacts)
        {
            if (!OnArc(centre, other, endpoint.X, endpoint.Y))
                return;
            foreach (var existing in contacts)
            {
                if (existing.Equals2D(endpoint))
                    return;
            }
            contacts.Add(endpoint.Copy());
        }

        private static double AngleOf(Coordinate p, Coordinate centre)
        {
            return System.Math.Atan2(p.Y - centre.Y, p.X - centre.X);
        }
    }
}
