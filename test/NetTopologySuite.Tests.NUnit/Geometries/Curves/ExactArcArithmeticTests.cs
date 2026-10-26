// SPDX-License-Identifier: BSD-3-Clause
// AI-drafted, human-reviewed.

using System;
using System.Collections.Generic;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Curves;
using NUnit.Framework;

namespace NetTopologySuite.Tests.NUnit.Geometries.Curves
{
    /// <summary>
    /// The exact rational layer under the rung-5 verdicts
    /// (NetTopologySuite.Proofs #615, ticket 615-h, issue #641): exact
    /// conversion of doubles, the tangency/cocircular band decisions, and
    /// exact point-in-ring over arc/chord rings.
    /// </summary>
    public class ExactArcArithmeticTests
    {
        private static ExactPoint P(double x, double y) => ExactPoint.From(new Coordinate(x, y));

        [Test]
        public void FromDouble_IsExact()
        {
            // 0.1 + 0.2 != 0.3 in doubles — and exactly so in rationals.
            var sum = ExactRational.FromDouble(0.1) + ExactRational.FromDouble(0.2);
            Assert.That((sum - ExactRational.FromDouble(0.3)).Sign, Is.Not.Zero);
            Assert.That(ExactRational.FromDouble(0.1 + 0.2).ValueEquals(ExactRational.FromDouble(0.3)), Is.False);

            foreach (double v in new[] { 0d, -0d, 1d, -2.5, double.Epsilon, -double.Epsilon,
                         double.MaxValue, 1e-300, 0.7865526304750483 })
                Assert.That(ExactRational.FromDouble(v).ToDouble(), Is.EqualTo(v), v.ToString("R"));

            Assert.That(() => ExactRational.FromDouble(double.NaN), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Arc_CircleIsExact()
        {
            Assert.That(ExactArcArithmetic.TryArc(
                new Coordinate(0, 0), new Coordinate(1, 1), new Coordinate(2, 0), out var arc), Is.True);
            Assert.That(arc.Centre.ValueEquals(P(1, 0)), Is.True);
            Assert.That(arc.RadiusSquared.ValueEquals(ExactRational.FromDouble(1)), Is.True);
            Assert.That(arc.Direction, Is.EqualTo(-1), "clockwise over the top");

            // Collinear triples are not arcs.
            Assert.That(ExactArcArithmetic.TryArc(
                new Coordinate(0, 0), new Coordinate(1, 1), new Coordinate(2, 2), out _), Is.False);
        }

        [Test]
        public void TwoCircleDecision_SeparatesMissTouchCross()
        {
            ExactArcArithmetic.TryArc(new Coordinate(0, 0), new Coordinate(1, 1), new Coordinate(2, 0), out var a);
            var contacts = new List<Coordinate>();

            // External tangency at (2,0), on both arcs.
            ExactArcArithmetic.TryArc(new Coordinate(2, 0), new Coordinate(3, 1), new Coordinate(4, 0), out var touch);
            Assert.That(ExactArcArithmetic.TwoCircleDecision(a, touch, contacts), Is.Zero);
            Assert.That(contacts, Has.Count.EqualTo(1));
            Assert.That(contacts[0].Distance(new Coordinate(2, 0)), Is.LessThan(1e-15));

            // Tangent at (2,0) — but the second arc is the LOWER half: still
            // on it (an endpoint), so still a contact.
            contacts.Clear();
            ExactArcArithmetic.TryArc(new Coordinate(2, 0), new Coordinate(3, -1), new Coordinate(4, 0), out var lower);
            Assert.That(ExactArcArithmetic.TwoCircleDecision(a, lower, contacts), Is.Zero);
            Assert.That(contacts, Has.Count.EqualTo(1));

            // Tangent circles whose ARCS miss the touch point: (2,0) is not
            // on the arc from (3,1) over (4,0) to (3,-1).
            contacts.Clear();
            ExactArcArithmetic.TryArc(new Coordinate(3, 1), new Coordinate(4, 0), new Coordinate(3, -1), out var far);
            Assert.That(ExactArcArithmetic.TwoCircleDecision(a, far, contacts), Is.Zero);
            Assert.That(contacts, Is.Empty);

            // A 2^-50 gap: no contact. A one-ulp nudge through (2,0): crossing.
            double d = Math.Pow(2, -50);
            ExactArcArithmetic.TryArc(new Coordinate(2 + d, 0), new Coordinate(3, 1 - d), new Coordinate(4 - d, 0), out var miss);
            Assert.That(ExactArcArithmetic.TwoCircleDecision(a, miss, contacts), Is.EqualTo(-1));
            ExactArcArithmetic.TryArc(new Coordinate(2, 0), new Coordinate(3, Math.BitIncrement(1.0)), new Coordinate(4, 0), out var cross);
            Assert.That(ExactArcArithmetic.TwoCircleDecision(a, cross, contacts), Is.EqualTo(1));
        }

        [Test]
        public void TwoCircleDecision_InternalTangencyOnTheFarRay()
        {
            // Small circle centre (1,0) r=1 inside big circle centre (2,0)
            // r=2: they touch at (0,0), on the ray from the big centre AWAY
            // from the small one — the σ = −1 case, seen from the small arc.
            ExactArcArithmetic.TryArc(new Coordinate(1, 1), new Coordinate(0, 0), new Coordinate(1, -1), out var small);
            ExactArcArithmetic.TryArc(new Coordinate(2, 2), new Coordinate(0, 0), new Coordinate(2, -2), out var big);
            var contacts = new List<Coordinate>();
            Assert.That(ExactArcArithmetic.TwoCircleDecision(small, big, contacts), Is.Zero);
            Assert.That(contacts, Has.Count.EqualTo(1));
            Assert.That(contacts[0].Distance(new Coordinate(0, 0)), Is.LessThan(1e-15));

            contacts.Clear();
            Assert.That(ExactArcArithmetic.TwoCircleDecision(big, small, contacts), Is.Zero);
            Assert.That(contacts, Has.Count.EqualTo(1));
        }

        [Test]
        public void TryPlaceCrossingPair_AnchoredAtSharedVertices()
        {
            // Non-dyadic intermediates (0.2, 1.2, 0.8): the two arcs' exact
            // circles differ, but both pass exactly through (1,0.2) and
            // (3,0.2) — the anchor and its reflection across the line of
            // centres are the whole crossing set.
            ExactArcArithmetic.TryArc(new Coordinate(1, 0.2), new Coordinate(2, 1.2), new Coordinate(3, 0.2), out var upper);
            ExactArcArithmetic.TryArc(new Coordinate(3, 0.2), new Coordinate(2, -0.8), new Coordinate(1, 0.2), out var lower);
            Assert.That(upper.SameCircle(lower), Is.False);
            var contacts = new List<Coordinate>();
            Assert.That(ExactArcArithmetic.TwoCircleDecision(upper, lower, contacts), Is.EqualTo(1));
            Assert.That(ExactArcArithmetic.TryPlaceCrossingPair(upper, lower, contacts), Is.True);
            Assert.That(contacts, Has.Count.EqualTo(2));
            Assert.That(contacts.Exists(c => c.Equals2D(new Coordinate(1, 0.2))), Is.True);
            Assert.That(contacts.Exists(c => c.Equals2D(new Coordinate(3, 0.2))), Is.True);
        }

        [Test]
        public void TryPlaceCrossingPair_RefusesToleranceIndistinguishablePair()
        {
            // Circle B (centre (3, e), through (2,0)) crosses the unit circle
            // at (2,0) and at a point ~e away; both lie on both arcs. With
            // e = 2^-34 the two are closer than the 1e-9 relative vertex
            // tolerance, so reporting them as doubles would merge them.
            double e = Math.Pow(2, -34);
            ExactArcArithmetic.TryArc(new Coordinate(1, 1), new Coordinate(2, 0), new Coordinate(1, -1), out var a);
            ExactArcArithmetic.TryArc(new Coordinate(4, 2 * e), new Coordinate(2, 0), new Coordinate(4, 0), out var b);
            var contacts = new List<Coordinate>();
            Assert.That(ExactArcArithmetic.TwoCircleDecision(a, b, contacts), Is.EqualTo(1));
            Assert.That(ExactArcArithmetic.TryPlaceCrossingPair(a, b, contacts), Is.False);
            Assert.That(contacts, Is.Empty);
        }

        [Test]
        public void TryPlaceArcChordCrossings_VietaSecondRoot()
        {
            // The chord from (0,0) — on the circle — to (3,1) meets the circle
            // (centre (1,0), r = 1) again at the other quadratic root,
            // (1.8, 0.6): rational, and on the upper arc.
            ExactArcArithmetic.TryArc(new Coordinate(0, 0), new Coordinate(1, 1), new Coordinate(2, 0), out var a);
            var contacts = new List<Coordinate>();
            Assert.That(ExactArcArithmetic.TryPlaceArcChordCrossings(a, new Coordinate(0, 0), new Coordinate(3, 1), contacts), Is.True);
            Assert.That(contacts, Has.Count.EqualTo(2));
            Assert.That(contacts.Exists(c => c.Equals2D(new Coordinate(0, 0))), Is.True);
            Assert.That(contacts.Exists(c => c.Distance(new Coordinate(1.8, 0.6)) < 1e-15), Is.True);
        }

        [Test]
        public void ArcChordDecision_TangentFootIsRational()
        {
            ExactArcArithmetic.TryArc(new Coordinate(0, 0), new Coordinate(1, 1), new Coordinate(2, 0), out var a);
            var contacts = new List<Coordinate>();
            Assert.That(ExactArcArithmetic.ArcChordDecision(a, new Coordinate(-3, 1), new Coordinate(3, 1), contacts), Is.Zero);
            Assert.That(contacts, Has.Count.EqualTo(1));
            Assert.That(contacts[0].Equals2D(new Coordinate(1, 1)), Is.True);

            // Same tangent line, but the chord stops short of the foot.
            contacts.Clear();
            Assert.That(ExactArcArithmetic.ArcChordDecision(a, new Coordinate(2, 1), new Coordinate(3, 1), contacts), Is.Zero);
            Assert.That(contacts, Is.Empty);

            // The line y = -1 is tangent to the circle at (1,-1) — off the
            // upper arc.
            Assert.That(ExactArcArithmetic.ArcChordDecision(a, new Coordinate(-3, -1), new Coordinate(3, -1), contacts), Is.Zero);
            Assert.That(contacts, Is.Empty);
        }

        [Test]
        public void CocircularContacts_OverlapAndTouch()
        {
            ExactArcArithmetic.TryArc(new Coordinate(5, 0), new Coordinate(4, 3), new Coordinate(0, 5), out var a);
            ExactArcArithmetic.TryArc(new Coordinate(0, 5), new Coordinate(-3, 4), new Coordinate(-5, 0), out var b);
            Assert.That(a.SameCircle(b), Is.True);
            var contacts = new List<Coordinate>();
            bool overlap = false;
            ExactArcArithmetic.CocircularContacts(a, new Coordinate(5, 0), new Coordinate(0, 5),
                b, new Coordinate(0, 5), new Coordinate(-5, 0), contacts, ref overlap);
            Assert.That(overlap, Is.False);
            Assert.That(contacts, Has.Count.EqualTo(1));
            Assert.That(contacts[0].Equals2D(new Coordinate(0, 5)), Is.True);

            // (3,4) to (-4,3) via (0,5) overlaps a past the shared point.
            ExactArcArithmetic.TryArc(new Coordinate(3, 4), new Coordinate(0, 5), new Coordinate(-4, 3), out var c);
            contacts.Clear();
            ExactArcArithmetic.CocircularContacts(a, new Coordinate(5, 0), new Coordinate(0, 5),
                c, new Coordinate(3, 4), new Coordinate(-4, 3), contacts, ref overlap);
            Assert.That(overlap, Is.True);
        }

        [Test]
        public void LocateInRing_ArcRing()
        {
            // The circle x² + y² = 25 as two clockwise semicircles.
            var ring = Ring(new Coordinate(-5, 0), new Coordinate(0, 5), new Coordinate(5, 0),
                new Coordinate(0, -5), new Coordinate(-5, 0));
            Assert.That(CurveContainment.LocateInRing(P(0, 2.5), ring), Is.EqualTo(CurveContainment.Location.Interior));
            // The centre lies on BOTH arcs' chords: the split path.
            Assert.That(CurveContainment.LocateInRing(P(0, 0), ring), Is.EqualTo(CurveContainment.Location.Interior));
            Assert.That(CurveContainment.LocateInRing(P(2.5, 0), ring), Is.EqualTo(CurveContainment.Location.Interior));
            Assert.That(CurveContainment.LocateInRing(P(0, 5), ring), Is.EqualTo(CurveContainment.Location.Boundary));
            Assert.That(CurveContainment.LocateInRing(P(3, 4), ring), Is.EqualTo(CurveContainment.Location.Boundary));
            Assert.That(CurveContainment.LocateInRing(P(-4, -3), ring), Is.EqualTo(CurveContainment.Location.Boundary));
            Assert.That(CurveContainment.LocateInRing(P(3, 4.25), ring), Is.EqualTo(CurveContainment.Location.Exterior));
            Assert.That(CurveContainment.LocateInRing(P(3, 3.75), ring), Is.EqualTo(CurveContainment.Location.Interior));
            Assert.That(CurveContainment.LocateInRing(P(10, 0), ring), Is.EqualTo(CurveContainment.Location.Exterior));
            Assert.That(CurveContainment.LocateInRing(P(-7.5, 0), ring), Is.EqualTo(CurveContainment.Location.Exterior));
        }

        [Test]
        public void LocateInRing_SemicircleWithDiameter()
        {
            // Upper half-disc: an arc plus its chord back. A point in the
            // LOWER half of the circle is outside, though inside the circle.
            var factory = new GeometryFactory();
            var cc = new CompoundCurve(new Curve[]
            {
                new CircularString(factory.CoordinateSequenceFactory.Create(new[]
                    { new Coordinate(0, 0), new Coordinate(1, 1), new Coordinate(2, 0) }), factory),
                factory.CreateLineString(new[] { new Coordinate(2, 0), new Coordinate(0, 0) }),
            }, factory);
            var ring = CurveContainment.TryBuildRing(CurveSimplicity.BuildChain(cc, cc));
            Assert.That(CurveContainment.LocateInRing(P(1, 0.5), ring), Is.EqualTo(CurveContainment.Location.Interior));
            Assert.That(CurveContainment.LocateInRing(P(1, -0.5), ring), Is.EqualTo(CurveContainment.Location.Exterior));
            Assert.That(CurveContainment.LocateInRing(P(1, 0), ring), Is.EqualTo(CurveContainment.Location.Boundary));
        }

        [Test]
        public void LocateInRing_MajorArcRing()
        {
            // A 270° arc closed by a chord: the chord's far side, inside the
            // circle, is inside the ring (the circular segment is the MAJOR
            // one here).
            var factory = new GeometryFactory();
            var cc = new CompoundCurve(new Curve[]
            {
                new CircularString(factory.CoordinateSequenceFactory.Create(new[]
                    { new Coordinate(1, 0), new Coordinate(-1, 0), new Coordinate(0, 1) }), factory),
                factory.CreateLineString(new[] { new Coordinate(0, 1), new Coordinate(1, 0) }),
            }, factory);
            var ring = CurveContainment.TryBuildRing(CurveSimplicity.BuildChain(cc, cc));
            Assert.That(CurveContainment.LocateInRing(P(0, 0), ring), Is.EqualTo(CurveContainment.Location.Interior));
            Assert.That(CurveContainment.LocateInRing(P(-0.5, -0.5), ring), Is.EqualTo(CurveContainment.Location.Interior));
            Assert.That(CurveContainment.LocateInRing(P(0.6, 0.6), ring), Is.EqualTo(CurveContainment.Location.Exterior));
            Assert.That(CurveContainment.LocateInRing(P(0.5, 0.5), ring), Is.EqualTo(CurveContainment.Location.Boundary));
        }

        private static List<CurveContainment.ExactSegment> Ring(params Coordinate[] controls)
        {
            var factory = new GeometryFactory();
            var cs = new CircularString(factory.CoordinateSequenceFactory.Create(controls), factory);
            return CurveContainment.TryBuildRing(CurveSimplicity.BuildChain(cs, cs));
        }
    }
}
