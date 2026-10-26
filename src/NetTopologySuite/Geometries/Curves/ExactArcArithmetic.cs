// SPDX-License-Identifier: BSD-3-Clause
//
// AI assistance disclosure:
//   AI-drafted, human-reviewed and curated. Per the same convention used for the
//   companion JTS prototype at grootstebozewolf/jts#1, AI-generated portions are
//   dedicated to CC0-1.0; human curation falls under the NTS BSD-3-Clause grant.
//
// Exact rational arithmetic over the arc control points (ISO/IEC 13249-3
// §7.3.1 Desc 8; NetTopologySuite.Proofs #615, ticket 615-h rung 5, issue
// #641). Every finite double is a dyadic rational, so the circle through
// three control points has an exactly representable rational centre and
// squared radius, and the predicates below decide their SIGNS with no
// rounding at all — the oracle's path. The double-precision pair kernel
// (CircularArcGeometry.SegmentPairContacts) stays the fast path; these
// predicates decide the bands where its float error spans both signs:
//
//   * tangency — the intersection discriminant of two circles, or of a
//     circle and a chord's line, is a rational expression in the control
//     points; its exact sign separates "no contact", "one touch point", and
//     "two crossings". The touch point itself is generally irrational, but
//     whether it lies on an arc reduces to the sign of A + λ·B with λ² and
//     A, B rational — decided exactly too.
//   * cocircularity — two arcs lie on the same circle iff their rational
//     centres and squared radii are equal; on one circle, interval overlap
//     and touch reduce to exact side-of-chord tests on the control points.
//   * point-in-ring — the winding number of a ring of arcs and chords about a
//     rational point, as the winding of its chord polygon plus one ±1
//     correction per circular segment (the region between an arc and its
//     chord) containing the point; every term is an exact orientation or
//     in-circle sign.
//
// A crossing PAIR inside a band is decided exactly when it is ANCHORED: when
// some control point of either segment lies exactly on both loci' carriers
// (typically the vertex two ring segments share), the second crossing is
// rational too — the anchor's reflection across the line of centres, or the
// other root of the circle–line quadratic (Vieta) — and both are placed on
// or off the arcs exactly. An unanchored crossing pair inside a band stays
// refused: its points are irrational and closer together than the float
// kernel can resolve (the residue the lane keeps, issue #981).

using System;
using System.Collections.Generic;
using System.Numerics;

namespace NetTopologySuite.Geometries.Curves
{
    /// <summary>
    /// An exact rational number with a positive denominator, kept in lowest
    /// terms so that equality is structural. Finite doubles convert exactly.
    /// </summary>
    internal readonly struct ExactRational
    {
        public readonly BigInteger Num;
        public readonly BigInteger Den;

        private ExactRational(BigInteger num, BigInteger den)
        {
            if (den.Sign < 0)
            {
                num = -num;
                den = -den;
            }
            var g = BigInteger.GreatestCommonDivisor(num, den);
            if (!g.IsOne && !g.IsZero)
            {
                num /= g;
                den /= g;
            }
            Num = num;
            Den = den;
        }

        /// <summary>The exact value of a finite double (every finite double is dyadic).</summary>
        public static ExactRational FromDouble(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v))
                throw new ArgumentOutOfRangeException(nameof(v), "Exact arithmetic needs finite coordinates.");
            long bits = BitConverter.DoubleToInt64Bits(v);
            bool negative = bits < 0;
            int exponent = (int)((bits >> 52) & 0x7FF);
            long mantissa = bits & 0xFFFFFFFFFFFFFL;
            if (exponent == 0)
                exponent = 1; // subnormal: no implicit leading bit
            else
                mantissa |= 1L << 52;
            exponent -= 1075;
            var num = new BigInteger(negative ? -mantissa : mantissa);
            return exponent >= 0
                ? new ExactRational(num << exponent, BigInteger.One)
                : new ExactRational(num, BigInteger.One << -exponent);
        }

        public int Sign => Num.Sign;

        public static ExactRational operator +(ExactRational a, ExactRational b)
            => new ExactRational(a.Num * b.Den + b.Num * a.Den, a.Den * b.Den);

        public static ExactRational operator -(ExactRational a, ExactRational b)
            => new ExactRational(a.Num * b.Den - b.Num * a.Den, a.Den * b.Den);

        public static ExactRational operator *(ExactRational a, ExactRational b)
            => new ExactRational(a.Num * b.Num, a.Den * b.Den);

        public static ExactRational operator /(ExactRational a, ExactRational b)
        {
            if (b.Num.IsZero)
                throw new DivideByZeroException();
            return new ExactRational(a.Num * b.Den, a.Den * b.Num);
        }

        /// <summary>Half of this value — exact (midpoints of dyadic coordinates).</summary>
        public ExactRational Half() => new ExactRational(Num, Den * 2);

        public bool ValueEquals(ExactRational other) => Num == other.Num && Den == other.Den;

        /// <summary>
        /// The nearest double, up to a few ulps — for REPORTING computed
        /// points only (contact lists are matched with a relative tolerance);
        /// no decision is ever taken on this value.
        /// </summary>
        public double ToDouble()
        {
            if (Num.IsZero)
                return 0d;
            int shift = 64 - (BitLength(BigInteger.Abs(Num)) - BitLength(Den));
            var q = shift >= 0 ? (Num << shift) / Den : Num / (Den << -shift);
            // Two power-of-two steps, so neither factor under/overflows for
            // values near the ends of the double range.
            int half = shift / 2;
            return (double)q * Math.Pow(2, -half) * Math.Pow(2, half - shift);
        }

        private static int BitLength(BigInteger v)
        {
            return v.IsZero ? 0 : (int)Math.Floor(BigInteger.Log(v, 2)) + 1;
        }
    }

    /// <summary>A point with exact rational coordinates.</summary>
    internal readonly struct ExactPoint
    {
        public readonly ExactRational X;
        public readonly ExactRational Y;

        public ExactPoint(ExactRational x, ExactRational y)
        {
            X = x;
            Y = y;
        }

        public static ExactPoint From(Coordinate c)
            => new ExactPoint(ExactRational.FromDouble(c.X), ExactRational.FromDouble(c.Y));

        /// <summary>The exact midpoint of two points.</summary>
        public static ExactPoint Midpoint(ExactPoint a, ExactPoint b)
            => new ExactPoint((a.X + b.X).Half(), (a.Y + b.Y).Half());

        public bool ValueEquals(ExactPoint other) => X.ValueEquals(other.X) && Y.ValueEquals(other.Y);

        /// <summary>A nearby double coordinate, for reporting only.</summary>
        public Coordinate ToCoordinate() => new Coordinate(X.ToDouble(), Y.ToDouble());
    }

    /// <summary>
    /// The exact circle carrying one arc segment, together with the arc's
    /// control points: <see cref="Side"/> is the side of the start–end chord
    /// the arc lies on (the side of the intermediate point), and
    /// <see cref="Direction"/> its traversal sense (+1 counter-clockwise).
    /// </summary>
    internal readonly struct ExactArc
    {
        public readonly ExactPoint P0;
        public readonly ExactPoint P1;
        public readonly ExactPoint P2;
        public readonly ExactPoint Centre;
        public readonly ExactRational RadiusSquared;
        public readonly int Side;
        public readonly int Direction;

        public ExactArc(ExactPoint p0, ExactPoint p1, ExactPoint p2)
        {
            P0 = p0;
            P1 = p1;
            P2 = p2;
            Direction = ExactArcArithmetic.Orientation(p0, p1, p2);
            Side = -Direction;
            // Circumcentre relative to p0: u = (|b|²·c⊥ − |c|²·b⊥) / (2·b×c).
            var bx = p1.X - p0.X;
            var by = p1.Y - p0.Y;
            var cx = p2.X - p0.X;
            var cy = p2.Y - p0.Y;
            var b2 = bx * bx + by * by;
            var c2 = cx * cx + cy * cy;
            var den = (bx * cy - by * cx) + (bx * cy - by * cx);
            var ux = (cy * b2 - by * c2) / den;
            var uy = (bx * c2 - cx * b2) / den;
            Centre = new ExactPoint(p0.X + ux, p0.Y + uy);
            RadiusSquared = ux * ux + uy * uy;
        }

        /// <summary>
        /// For a point known to lie EXACTLY on this arc's circle: is it on the
        /// arc (endpoints inclusive)? The chord splits the circle into two
        /// arcs; the locus is the one on the intermediate point's side, and
        /// the only circle points on the chord line are the endpoints.
        /// </summary>
        public bool ContainsCirclePoint(ExactPoint x)
        {
            int side = ExactArcArithmetic.Orientation(P0, P2, x);
            return side == 0 || side == Side;
        }

        /// <summary>
        /// For a circle point <paramref name="x"/>: is it strictly inside the
        /// arc (on the locus, not an endpoint)?
        /// </summary>
        public bool StrictlyContainsCirclePoint(ExactPoint x)
        {
            return ExactArcArithmetic.Orientation(P0, P2, x) == Side;
        }

        public bool SameCircle(ExactArc other)
        {
            return Centre.ValueEquals(other.Centre) && RadiusSquared.ValueEquals(other.RadiusSquared);
        }
    }

    /// <summary>
    /// Exact predicates and band decisions over arc control points
    /// (615-h rung 5, NetTopologySuite.Proofs issue #641).
    /// </summary>
    internal static class ExactArcArithmetic
    {
        /// <summary>(a − o) × (b − o), exactly.</summary>
        public static ExactRational Cross(ExactPoint o, ExactPoint a, ExactPoint b)
        {
            return (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
        }

        /// <summary>The exact orientation sign of (a, b, c): +1 CCW, −1 CW, 0 collinear.</summary>
        public static int Orientation(ExactPoint a, ExactPoint b, ExactPoint c)
        {
            return Cross(a, b, c).Sign;
        }

        /// <summary>
        /// +1 when <paramref name="d"/> is strictly inside the circle through
        /// the non-collinear <paramref name="a"/>, <paramref name="b"/>,
        /// <paramref name="c"/>; 0 on it; −1 outside. Exact.
        /// </summary>
        public static int InCircle(ExactPoint a, ExactPoint b, ExactPoint c, ExactPoint d)
        {
            var adx = a.X - d.X; var ady = a.Y - d.Y;
            var bdx = b.X - d.X; var bdy = b.Y - d.Y;
            var cdx = c.X - d.X; var cdy = c.Y - d.Y;
            var ad = adx * adx + ady * ady;
            var bd = bdx * bdx + bdy * bdy;
            var cd = cdx * cdx + cdy * cdy;
            var det = adx * (bdy * cd - bd * cdy)
                    - ady * (bdx * cd - bd * cdx)
                    + ad * (bdx * cdy - bdy * cdx);
            return det.Sign * Orientation(a, b, c);
        }

        /// <summary>
        /// Is <paramref name="x"/> on the closed segment [a, b]? Exact.
        /// </summary>
        public static bool OnSegment(ExactPoint a, ExactPoint b, ExactPoint x)
        {
            if (Orientation(a, b, x) != 0)
                return false;
            return Between(a.X, b.X, x.X) && Between(a.Y, b.Y, x.Y);
        }

        private static bool Between(ExactRational a, ExactRational b, ExactRational x)
        {
            int ax = (x - a).Sign;
            int bx = (x - b).Sign;
            return ax == 0 || bx == 0 || ax != bx;
        }

        /// <summary>
        /// The exact arc for a control triple the double kernel classified as
        /// an arc — or <c>false</c> when the exact orientation disagrees with
        /// the double one (exactly collinear, or the opposite sense), in which
        /// case the kernel's classification itself is unsound and the caller
        /// must refuse.
        /// </summary>
        public static bool TryArc(Coordinate p0, Coordinate p1, Coordinate p2, out ExactArc arc)
        {
            double floatSign = Math.Sign(CircularArcGeometry.OrientationIndex(p0, p1, p2));
            var e0 = ExactPoint.From(p0);
            var e1 = ExactPoint.From(p1);
            var e2 = ExactPoint.From(p2);
            if (floatSign == 0 || Orientation(e0, e1, e2) != floatSign)
            {
                arc = default;
                return false;
            }
            arc = new ExactArc(e0, e1, e2);
            return true;
        }

        /// <summary>
        /// The exact contact decision for two arcs on DIFFERENT circles:
        /// returns the sign of the intersection discriminant
        /// 4·ra²·rb² − (d² − ra² − rb²)² — −1 (the circles do not meet, no
        /// contact), 0 (they touch at exactly one point, which is appended to
        /// <paramref name="contacts"/> when it lies on both arcs), or +1 (two
        /// crossing points; nothing appended — the caller decides or refuses).
        /// </summary>
        public static int TwoCircleDecision(ExactArc a, ExactArc b, List<Coordinate> contacts)
        {
            var dx = b.Centre.X - a.Centre.X;
            var dy = b.Centre.Y - a.Centre.Y;
            var d2 = dx * dx + dy * dy;
            if (d2.Sign == 0)
                return -1; // concentric, distinct radii (equal circles are the caller's)
            var e = d2 - a.RadiusSquared - b.RadiusSquared;
            var four = ExactRational.FromDouble(4);
            int disc = (four * a.RadiusSquared * b.RadiusSquared - e * e).Sign;
            if (disc != 0)
                return disc;

            // Tangent: T = Ca + λ·(Cb − Ca) with |λ| = ra/d. External touch
            // (e > 0) and internal touch with ra > rb put T on the ray towards
            // Cb; an internal touch with rb > ra puts it on the opposite ray.
            int sigma = e.Sign > 0 || (a.RadiusSquared - b.RadiusSquared).Sign > 0 ? 1 : -1;
            var lambdaSquared = a.RadiusSquared / d2;
            if (ContainsTangentPoint(a, a.Centre, b.Centre, sigma, lambdaSquared)
                && ContainsTangentPoint(b, a.Centre, b.Centre, sigma, lambdaSquared))
            {
                double lambda = sigma * Math.Sqrt(lambdaSquared.ToDouble());
                double cax = a.Centre.X.ToDouble(), cay = a.Centre.Y.ToDouble();
                contacts.Add(new Coordinate(
                    cax + lambda * (b.Centre.X.ToDouble() - cax),
                    cay + lambda * (b.Centre.Y.ToDouble() - cay)));
            }
            return 0;
        }

        /// <summary>
        /// Places an exact crossing PAIR of two distinct circles (discriminant
        /// &gt; 0) when it is anchored: some control point of either arc lies
        /// exactly on both circles, so it is one crossing and its reflection
        /// across the line of centres is the other — both rational. Appends
        /// the crossings lying on both arcs and returns <c>true</c>;
        /// <c>false</c> (nothing appended) when no anchor exists, or when
        /// both crossings lie on both arcs but within the callers' vertex
        /// match tolerance of each other — the contact lists would merge two
        /// genuinely distinct points into one (an undercounted Desc 11, or a
        /// crossing mistaken for the permitted shared vertex).
        /// </summary>
        public static bool TryPlaceCrossingPair(ExactArc a, ExactArc b, List<Coordinate> contacts)
        {
            if (!TryFindAnchor(a, b, out var anchor))
                return false;
            var ux = b.Centre.X - a.Centre.X;
            var uy = b.Centre.Y - a.Centre.Y;
            var t = ((anchor.X - a.Centre.X) * ux + (anchor.Y - a.Centre.Y) * uy) / (ux * ux + uy * uy);
            var footX = a.Centre.X + t * ux;
            var footY = a.Centre.Y + t * uy;
            var other = new ExactPoint(footX + footX - anchor.X, footY + footY - anchor.Y);
            var placed = new List<Coordinate>(2);
            if (a.ContainsCirclePoint(anchor) && b.ContainsCirclePoint(anchor))
                placed.Add(anchor.ToCoordinate());
            if (!other.ValueEquals(anchor) && a.ContainsCirclePoint(other) && b.ContainsCirclePoint(other))
                placed.Add(other.ToCoordinate());
            return Commit(placed, contacts);
        }

        /// <summary>
        /// Appends exactly placed crossings unless two of them would be
        /// indistinguishable under the callers' relative vertex tolerance.
        /// </summary>
        private static bool Commit(List<Coordinate> placed, List<Coordinate> contacts)
        {
            if (placed.Count == 2 && CurveSimplicity.Matches(placed[0], placed[1]))
                return false;
            contacts.AddRange(placed);
            return true;
        }

        private static bool TryFindAnchor(ExactArc a, ExactArc b, out ExactPoint anchor)
        {
            foreach (var candidate in new[] { a.P0, a.P2, a.P1 })
            {
                if (OnCircle(b, candidate)) { anchor = candidate; return true; }
            }
            foreach (var candidate in new[] { b.P0, b.P2, b.P1 })
            {
                if (OnCircle(a, candidate)) { anchor = candidate; return true; }
            }
            anchor = default;
            return false;
        }

        private static bool OnCircle(ExactArc arc, ExactPoint x)
        {
            return InCircle(arc.P0, arc.P1, arc.P2, x) == 0;
        }

        /// <summary>
        /// Places an exact crossing PAIR of an arc's circle and a chord's line
        /// (discriminant &gt; 0) when it is anchored: a chord endpoint on the
        /// circle, or an arc control point on the line, is one root of the
        /// circle–line quadratic, and Vieta's sum gives the other — both
        /// rational. Appends the crossings on both the chord and the arc and
        /// returns <c>true</c>; <c>false</c> when no anchor exists or the two
        /// crossings are tolerance-indistinguishable (as for two circles).
        /// </summary>
        public static bool TryPlaceArcChordCrossings(ExactArc a, Coordinate q0, Coordinate q2, List<Coordinate> contacts)
        {
            var e0 = ExactPoint.From(q0);
            var e2 = ExactPoint.From(q2);
            var vx = e2.X - e0.X;
            var vy = e2.Y - e0.Y;
            var length2 = vx * vx + vy * vy;
            ExactRational? anchorT = null;
            if (OnCircle(a, e0))
                anchorT = ExactRational.FromDouble(0);
            else if (OnCircle(a, e2))
                anchorT = ExactRational.FromDouble(1);
            else
            {
                foreach (var control in new[] { a.P0, a.P2, a.P1 })
                {
                    if (Orientation(e0, e2, control) == 0)
                    {
                        anchorT = ((control.X - e0.X) * vx + (control.Y - e0.Y) * vy) / length2;
                        break;
                    }
                }
            }
            if (anchorT is null)
                return false;
            // Roots of |q0 + t·v − C|² = r²: t1 + t2 = 2·(C − q0)·v / |v|².
            var sum = ((a.Centre.X - e0.X) * vx + (a.Centre.Y - e0.Y) * vy) / length2;
            sum = sum + sum;
            var one = ExactRational.FromDouble(1);
            var placed = new List<Coordinate>(2);
            var otherT = sum - anchorT.Value;
            foreach (var t in otherT.ValueEquals(anchorT.Value) ? new[] { otherT } : new[] { anchorT.Value, otherT })
            {
                if (t.Sign < 0 || (t - one).Sign > 0)
                    continue;
                var x = new ExactPoint(e0.X + t * vx, e0.Y + t * vy);
                if (a.ContainsCirclePoint(x))
                    placed.Add(x.ToCoordinate());
            }
            return Commit(placed, contacts);
        }

        /// <summary>
        /// The exact contact decision for an arc and a chord [q0, q2]: the
        /// sign of the circle–line discriminant r²·|v|² − (v × (C − q0))² —
        /// −1 (the line misses the circle), 0 (tangent: the foot of the
        /// perpendicular, a rational point, is appended when it lies on both
        /// the chord and the arc), +1 (two crossing points; nothing appended).
        /// </summary>
        public static int ArcChordDecision(ExactArc a, Coordinate q0, Coordinate q2, List<Coordinate> contacts)
        {
            var e0 = ExactPoint.From(q0);
            var e2 = ExactPoint.From(q2);
            var vx = e2.X - e0.X;
            var vy = e2.Y - e0.Y;
            var length2 = vx * vx + vy * vy;
            var cross = Cross(e0, e2, a.Centre);
            int disc = (a.RadiusSquared * length2 - cross * cross).Sign;
            if (disc != 0)
                return disc;
            var t = ((a.Centre.X - e0.X) * vx + (a.Centre.Y - e0.Y) * vy) / length2;
            if (t.Sign < 0 || (t - ExactRational.FromDouble(1)).Sign > 0)
                return 0;
            var foot = new ExactPoint(e0.X + t * vx, e0.Y + t * vy);
            if (a.ContainsCirclePoint(foot))
                contacts.Add(foot.ToCoordinate());
            return 0;
        }

        /// <summary>
        /// Exact contacts of two arcs on the SAME circle: a 1-D overlap iff an
        /// endpoint of one lies strictly inside the other, or both share the
        /// same endpoint pair on the same side; otherwise the contacts are
        /// the endpoints of either lying on the other (exact input points).
        /// </summary>
        public static void CocircularContacts(
            ExactArc a, Coordinate p0, Coordinate p2,
            ExactArc b, Coordinate q0, Coordinate q2,
            List<Coordinate> contacts, ref bool overlap)
        {
            if (StrictlyInside(a, b.P0) || StrictlyInside(a, b.P2)
                || StrictlyInside(b, a.P0) || StrictlyInside(b, a.P2))
            {
                overlap = true;
                return;
            }
            bool sameEnds =
                (a.P0.ValueEquals(b.P0) && a.P2.ValueEquals(b.P2))
                || (a.P0.ValueEquals(b.P2) && a.P2.ValueEquals(b.P0));
            if (sameEnds && Orientation(a.P0, a.P2, b.P1) == a.Side)
            {
                overlap = true;
                return;
            }
            AddIfOnArc(a, b.P0, q0, contacts);
            AddIfOnArc(a, b.P2, q2, contacts);
            AddIfOnArc(b, a.P0, p0, contacts);
            AddIfOnArc(b, a.P2, p2, contacts);
        }

        private static bool StrictlyInside(ExactArc arc, ExactPoint x)
        {
            return !x.ValueEquals(arc.P0) && !x.ValueEquals(arc.P2) && arc.StrictlyContainsCirclePoint(x);
        }

        private static void AddIfOnArc(ExactArc arc, ExactPoint x, Coordinate original, List<Coordinate> contacts)
        {
            if (!arc.ContainsCirclePoint(x))
                return;
            foreach (var existing in contacts)
            {
                if (existing.Equals2D(original))
                    return;
            }
            contacts.Add(original.Copy());
        }

        /// <summary>
        /// Is the tangent point T = Ca + σ·√L·(Cb − Ca) on <paramref name="arc"/>?
        /// T is on the arc's circle by construction, so only its side of the
        /// arc's chord matters: orient(P0, P2, T) = A + σ·√L·B with
        /// A = orient(P0, P2, Ca) and B = (P2 − P0) × (Cb − Ca), all rational.
        /// </summary>
        private static bool ContainsTangentPoint(
            ExactArc arc, ExactPoint ca, ExactPoint cb, int sigma, ExactRational lambdaSquared)
        {
            var a = Cross(arc.P0, arc.P2, ca);
            var b = Cross(arc.P0, arc.P2, cb) - a;
            int side = SignOfSum(a, sigma, lambdaSquared, b);
            return side == 0 || side == arc.Side;
        }

        /// <summary>The exact sign of A + σ·√L·B for rational A, B and L ≥ 0.</summary>
        private static int SignOfSum(ExactRational a, int sigma, ExactRational l, ExactRational b)
        {
            int sa = a.Sign;
            int sb = l.Sign == 0 ? 0 : b.Sign * sigma;
            if (sb == 0) return sa;
            if (sa == 0 || sa == sb) return sb;
            int cmp = (a * a - l * b * b).Sign;
            return cmp > 0 ? sa : cmp < 0 ? sb : 0;
        }
    }
}
