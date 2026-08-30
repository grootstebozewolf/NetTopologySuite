// SPDX-License-Identifier: BSD-3-Clause
// AI-drafted, human-reviewed.

using System;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Curves;
using NUnit.Framework;

namespace NetTopologySuite.Tests.NUnit.Geometries.Curves
{
    /// <summary>
    /// Multi-segment simplicity contracts for CircularString (ISO/IEC 13249-3
    /// §4.2.4 over the §7.3.1 Desc 8 locus; NetTopologySuite.Proofs ticket
    /// 615-h, issue #630 there — the rung after single-segment simplicity).
    /// A value is simple iff no two segment loci meet outside the permitted
    /// shared vertices: consecutive segments may share exactly their
    /// connecting vertex, and a closed chain may additionally share its
    /// start/end point between the first and last segments. Mirrors the
    /// oracle's RING_SIMPLE composition (proofs companion
    /// theories/CurveRingSimple.v), except that an OPEN chain permits no
    /// first/last-segment contact — the ring lane always closes the chain.
    /// </summary>
    public class CurveSimplicityTests
    {
        private readonly GeometryFactory _factory = new GeometryFactory();

        private CircularString Cs(params (double x, double y)[] pts)
        {
            var coords = new Coordinate[pts.Length];
            for (int i = 0; i < pts.Length; i++) coords[i] = new Coordinate(pts[i].x, pts[i].y);
            return new CircularString(_factory.CoordinateSequenceFactory.Create(coords), _factory);
        }

        [Test]
        public void TwoArcsTangentAtSharedVertex_StaysFailClosed()
        {
            // FLIPPED by the rung-4 review: this used to be checked-true
            // ("the only locus contact is the permitted vertex (2,0)"), but
            // that verdict rested on the tangency discriminant computing an
            // EXACT zero — dyadic luck. A 1-ulp perturbation of either
            // circle turns the touch into a crossing pair ~sqrt(eps) apart,
            // indistinguishable at double precision, and the review
            // demonstrated wrong checked verdicts in both directions inside
            // that band. The kernel now refuses it (AmbiguousTangency,
            // continued at issue #641).
            var cs = Cs((0, 0), (1, 1), (2, 0), (3, -1), (4, 0));
            Assert.That(() => cs.IsSimple,
                Throws.TypeOf<NotSupportedException>().With.Message.Contains("tangent"));
            Assert.That(() => cs.IsRing, Throws.TypeOf<NotSupportedException>());
        }

        [Test]
        public void FivePointFullCircleIdiom_IsSimpleRing()
        {
            // The documented full-circle idiom (two cocircular semicircles):
            // the segments share exactly the two ring vertices (2,0) and
            // (0,0), both permitted in a closed chain — a simple ring.
            var circle = Cs((0, 0), (1, 1), (2, 0), (1, -1), (0, 0));
            Assert.That(circle.IsSimple, Is.True);
            Assert.That(circle.IsRing, Is.True);
        }

        [Test]
        public void ThereAndBackOnTheSameArc_IsNotSimple()
        {
            // Both segments carry the SAME upper semicircle (cocircular,
            // identical angular interval): a 1-dimensional self-overlap.
            var cs = Cs((0, 0), (1, 1), (2, 0), (1, 1), (0, 0));
            Assert.That(cs.IsSimple, Is.False);
            Assert.That(cs.IsRing, Is.False, "closed but not simple");
        }

        [Test]
        public void ChordOverlapBeyondSharedVertex_IsNotSimple()
        {
            // Two collinear (Desc 8b) chords: (0,0)–(2,2), then back to
            // (1,1) — the loci overlap in the whole sub-segment
            // [(1,1),(2,2)], far more than the shared vertex.
            var cs = Cs((0, 0), (1, 1), (2, 2), (1.5, 1.5), (1, 1));
            Assert.That(cs.IsSimple, Is.False);
        }

        [Test]
        public void NonAdjacentTouch_IsNotSimple()
        {
            // Three segments; the third (a chord ending at (1,1)) touches
            // the FIRST arc's locus at (1,1). Non-adjacent pairs share no
            // permitted vertex, so any contact refutes simplicity.
            var cs = Cs((0, 0), (1, 1), (2, 0), (3, -1), (4, 0), (2.5, 0.5), (1, 1));
            Assert.That(cs.IsSimple, Is.False);
        }

        [Test]
        public void ThreeCleanArcs_IsSimple()
        {
            // "Clean" = TRANSVERSAL at both connecting vertices (the second
            // circle-pair intersection is off at least one arc's sweep, and
            // the discriminants sit far above the tangency band): the chain
            // stays checked-true after the rung-4 tangency refusal. The old
            // coordinates of this test formed a smoothly TANGENT chain —
            // that shape is now fail-closed, see
            // TwoArcsTangentAtSharedVertex_StaysFailClosed.
            var cs = Cs(
                (0, 0), (1, 1), (2, 0),
                (2.7071067811865475, -0.2928932188134525), (3, -1),
                (4, -0.3819660112501051), (5, -1));
            Assert.That(cs.IsSimple, Is.True);
        }

        [Test]
        public void AdjacentArcsCrossingBeyondSharedVertex_IsNotSimple()
        {
            // Second arc rides the circle centre (1, 0.4), r = √1.16, which
            // meets the first arc's circle (centre (1,0), r=1) at (2,0) — the
            // permitted connecting vertex — AND at (0,0). (0,0) is inside the
            // second arc's sweep and is the OPEN chain's start (a boundary
            // point touched by another segment's interior): not permitted.
            // Note the oracle's RING_SIMPLE lane would call this SIMPLE — it
            // models a closed ring, where the chain's start is a permitted
            // shared vertex; an open CircularString is not a ring.
            var cs = Cs(
                (0, 0), (1, 1), (2, 0),
                (1.0, 1.4770329614269007),
                (0.17494488484294612, -0.29230344282921217));
            Assert.That(cs.IsSimple, Is.False);
        }

        [Test]
        public void ChordBackThroughArcStart_IsNotSimple()
        {
            // Arc then a collinear chord heading back along y=0: the chord's
            // interior passes through (0,0), the open chain's start point.
            var cs = Cs((0, 0), (1, 1), (2, 0), (0.5, 0), (-1, 0));
            Assert.That(cs.IsSimple, Is.False);
        }

        [Test]
        public void MixedArcThenChordClean_IsSimple()
        {
            // Arc plus a collinear chord leaving the circle: the chord's
            // line meets the circle at (0,0) and (2,0), but only (2,0) lies
            // in the chord's parameter range — the permitted vertex.
            var cs = Cs((0, 0), (1, 1), (2, 0), (3, 0), (4, 0));
            Assert.That(cs.IsSimple, Is.True);
        }

        [Test]
        public void DegenerateClosedSegmentWithinMultiSegment_StaysFailClosed()
        {
            // First segment has start == end (invalid under §7.3.1 Desc 6);
            // its locus is not a decidable arc — no unchecked verdict.
            var cs = Cs((0, 0), (1, 1), (0, 0), (1, -1), (0, 0));
            Assert.That(() => cs.IsSimple, Throws.TypeOf<NotSupportedException>());
        }

        [Test]
        public void NonAdjacentTangentTouch_StaysFailClosed()
        {
            // FLIPPED by the rung-4 review: the last chord runs along y = 1,
            // exactly tangent to the first arc's circle at (1,1). The old
            // definite-false rested on the circle-line discriminant being an
            // EXACT zero — inside the band where a touch, a close crossing
            // pair, and a near-miss are indistinguishable at double
            // precision (the b and c terms carry the float circumradius), so
            // the kernel now refuses (AmbiguousTangency, issue #641). The
            // deferral rule keeps NonAdjacentTouch_IsNotSimple decided: its
            // refutation comes from a transversal crossing, which is sound
            // regardless of any banded pair elsewhere in the scan.
            var cs = Cs((0, 0), (1, 1), (2, 0), (3, 0.5), (4, 1), (2.5, 1), (1, 1));
            Assert.That(() => cs.IsSimple,
                Throws.TypeOf<NotSupportedException>().With.Message.Contains("tangent"));
        }

        [Test]
        public void LargeCircumradiusPair_StaysFailClosed()
        {
            // Review-demonstrated blocker, pinned: the r² terms in the
            // radical-line / circle-chord algebra carry absolute error
            // ~eps·r², so a nearly-collinear control triple (circumradius
            // ~2e8 here) produced silently wrong verdicts in both directions
            // before the conditioning guard. This flat arc transversally
            // double-crosses the unit semicircle near y = 0.9 — a decided
            // kernel must answer false; the double kernel must refuse
            // instead of guessing.
            var cs = Cs(
                (-2, 0.9), (0, 0.9 + 1e-8), (2, 0.9),
                (1.5, 0.45), (1, 0),
                (0, 1), (-1, 0));
            var ex = Assert.Throws<NotSupportedException>(() => _ = cs.IsSimple);
            Assert.That(ex.Message, Does.Contain("circumradius"));
        }

        private LineString Line(params (double x, double y)[] pts)
        {
            var coords = new Coordinate[pts.Length];
            for (int i = 0; i < pts.Length; i++) coords[i] = new Coordinate(pts[i].x, pts[i].y);
            return _factory.CreateLineString(coords);
        }

        // --- CompoundCurve / CurvePolygon chains (ticket 615-h rung 3,
        // #634): the same pairwise composition over mixed arc/chord chains;
        // a multi-point LineString component contributes one chord per
        // consecutive coordinate pair. ---------------------------------------

        [Test]
        public void CompoundLineTangentThroughArc_StaysFailClosed()
        {
            // FLIPPED by the rung-4 review: BOTH of this compound's
            // non-trivial contacts are exact line tangencies (x = 2 at
            // (2,0); y = 1 at (1,1)) — every refuting pair sits in the
            // tangency band, so with no transversal witness left the value
            // fail-closes (AmbiguousTangency, issue #641) instead of
            // resting a definite false on exact-zero discriminants.
            var cc = new CompoundCurve(new Curve[]
            {
                Cs((0, 0), (1, 1), (2, 0)),
                Line((2, 0), (2, 1), (0, 1)),
            }, _factory);
            Assert.That(() => cc.IsSimple,
                Throws.TypeOf<NotSupportedException>().With.Message.Contains("tangent"));
        }

        [Test]
        public void CompoundThereAndBackLine_IsNotSimple()
        {
            var cc = new CompoundCurve(new Curve[]
            {
                Line((0, 0), (1, 0)),
                Line((1, 0), (0, 0)),
            }, _factory);
            Assert.That(cc.IsSimple, Is.False);
            Assert.That(cc.IsRing, Is.False, "closed but overlapping");
        }

        [Test]
        public void CompoundZeroLengthSubSegment_IsSkippedNotFatal()
        {
            // Classical LineStrings tolerate repeated points; the zero-length
            // chord contributes no locus and must not break adjacency.
            var cc = new CompoundCurve(new Curve[]
            {
                Line((0, 0), (0, 0), (1, 0)),
                Cs((1, 0), (2, 1), (3, 0)),
            }, _factory);
            Assert.That(cc.IsSimple, Is.True);
        }

        [Test]
        public void CompoundWithDegenerateArcComponent_StaysFailClosed()
        {
            // A CircularString component whose segment has start == end is
            // Desc-6-degenerate: its locus is not a decidable arc.
            var cc = new CompoundCurve(new Curve[]
            {
                Line((0, 0), (1, 1)),
                Cs((1, 1), (2, 2), (1, 1)),
            }, _factory);
            Assert.That(() => cc.IsSimple, Throws.TypeOf<NotSupportedException>());
        }

        [Test]
        public void CurvePolygonRings_SimpleAndNot()
        {
            // Polygonal simplicity = every ring simple (the classical
            // IsSimplePolygonal reading): the full-circle idiom ring is
            // simple; the bowtie LineString ring is not.
            var circleRing = Cs((0, 0), (1, 1), (2, 0), (1, -1), (0, 0));
            Assert.That(new CurvePolygon(circleRing, _factory).IsSimple, Is.True);

            var bowtie = Line((0, 0), (2, 2), (2, 0), (0, 2), (0, 0));
            Assert.That(new CurvePolygon(bowtie, _factory).IsSimple, Is.False);

            Assert.That(new CurvePolygon(null, _factory).IsSimple, Is.True, "empty");
        }

        [Test]
        public void CocircularReversedAsymmetricTraversal_NowDecided()
        {
            // The #630 review's probe: the same asymmetric triple traversed
            // back used to land in the ambiguity band (circumcentres differed
            // in the last ulps by argument order). TryCircle now canonicalizes
            // the triple before computing the circumcentre, so both segments
            // get the bit-identical circle and the cocircular overlap decides.
            var cs = Cs((0.1, 0.2), (1.3, 1.7), (2.9, 0.3), (1.3, 1.7), (0.1, 0.2));
            Assert.That(cs.IsSimple, Is.False);
        }

        // --- MultiCurve / MultiSurface (ticket 615-h rung 4, #639):
        // §4.2.25 / §10.3.1 Desc 4 — an ST_MultiCurve is simple iff all
        // elements are simple and any two elements intersect only at points
        // in the boundaries of BOTH elements (Mod-2: endpoints of open
        // members; a closed member has no boundary, so any touch on it
        // refutes simplicity). MultiSurface follows the classical polygonal
        // reading: every element's rings simple. ---------------------------

        [Test]
        public void MultiCurve_DisjointMembers_IsSimple()
        {
            var mc = new MultiCurve(new Curve[]
            {
                Cs((0, 0), (1, 1), (2, 0)),
                Line((5, 0), (6, 0)),
            }, _factory);
            Assert.That(mc.IsSimple, Is.True);
        }

        [Test]
        public void MultiCurve_OpenMembersTouchingAtSharedEndpoint_IsSimple()
        {
            // (1,0) is a boundary point of both open members — permitted.
            var mc = new MultiCurve(new Curve[]
            {
                Line((0, 0), (1, 0)),
                Cs((1, 0), (2, 1), (3, 0)),
            }, _factory);
            Assert.That(mc.IsSimple, Is.True);
        }

        [Test]
        public void MultiCurve_EndpointOnOtherMembersInterior_IsNotSimple()
        {
            // (1,1) is the line's boundary but the arc's INTERIOR — the
            // contact is not in the boundaries of both.
            var mc = new MultiCurve(new Curve[]
            {
                Cs((0, 0), (1, 1), (2, 0)),
                Line((1, 1), (1, 3)),
            }, _factory);
            Assert.That(mc.IsSimple, Is.False);
        }

        [Test]
        public void MultiCurve_TouchOnClosedMember_IsNotSimple()
        {
            // The closed member's boundary is empty, so no touch on it can
            // be permitted — even at the other member's endpoint.
            var mc = new MultiCurve(new Curve[]
            {
                Cs((0, 0), (1, 1), (2, 0), (1, -1), (0, 0)),
                Line((2, 0), (3, 0)),
            }, _factory);
            Assert.That(mc.IsSimple, Is.False);
        }

        [Test]
        public void MultiCurve_NonSimpleMember_IsNotSimple()
        {
            var mc = new MultiCurve(new Curve[]
            {
                Cs((0, 0), (1, 1), (2, 2), (1.5, 1.5), (1, 1)),
            }, _factory);
            Assert.That(mc.IsSimple, Is.False);
        }

        [Test]
        public void MultiCurve_MemberOverlap_IsNotSimple()
        {
            var mc = new MultiCurve(new Curve[]
            {
                Line((0, 0), (2, 0)),
                Line((1, 0), (3, 0)),
            }, _factory);
            Assert.That(mc.IsSimple, Is.False);
        }

        [Test]
        public void MultiCurve_NearTangentMembers_StayFailClosed()
        {
            // Rung-4 review witness W1, pinned: both members pass through
            // the shared control point (2,0) — it is the mid control of
            // both arcs, so it is on both loci, interior to both open
            // members — and the circumcircles are exactly internally
            // tangent there. Before the tangency band this answered a
            // SILENT WRONG TRUE (the kernel's h2 rounded negative and the
            // contact vanished); §10.3.1 Desc 4 refutes it. The honest
            // double-precision answer is the fail-closed refusal.
            var mc = new MultiCurve(new Curve[]
            {
                Cs((0, 2), (2, 0), (0, -2)),
                Cs((0.8466466098542542, 1.1533533901457458), (2, 0),
                   (0.8466466098542542, -1.1533533901457458)),
            }, _factory);
            Assert.That(() => mc.IsSimple,
                Throws.TypeOf<NotSupportedException>().With.Message.Contains("tangent"));
        }

        [Test]
        public void MultiCurve_EmptyAndDegenerate()
        {
            Assert.That(new MultiCurve(null, _factory).IsSimple, Is.True, "empty");
            var degenerate = new MultiCurve(new Curve[] { Cs((0, 0), (1, 1), (0, 0)) }, _factory);
            Assert.That(() => degenerate.IsSimple, Throws.TypeOf<NotSupportedException>());
        }

        [Test]
        public void MultiSurface_RingsDecide()
        {
            var simple = new MultiSurface(new Geometry[]
            {
                new CurvePolygon(Cs((0, 0), (1, 1), (2, 0), (1, -1), (0, 0)), _factory),
                _factory.CreatePolygon(new[]
                {
                    new Coordinate(10, 10), new Coordinate(12, 10),
                    new Coordinate(12, 12), new Coordinate(10, 10),
                }),
            }, _factory);
            Assert.That(simple.IsSimple, Is.True);

            var bowtieRing = Line((0, 0), (2, 2), (2, 0), (0, 2), (0, 0));
            var notSimple = new MultiSurface(new Geometry[]
            {
                new CurvePolygon(bowtieRing, _factory),
            }, _factory);
            Assert.That(notSimple.IsSimple, Is.False);
        }

        [Test]
        public void IsSimple_NonFiniteCoordinate_StaysFailClosed()
        {
            // Review-added hardening (#639): NaN used to slip through the
            // chain-of-1 shortcut and past the conditioning guard
            // (eps·NaN² > tol is false) into empty-contact trues. IsSimple
            // now fail-closes on non-finite coordinates, matching the
            // lane's no-unchecked-verdict rule (IsValid is definite-false).
            var single = Cs((0, 0), (1, double.NaN), (2, 0));
            Assert.That(() => single.IsSimple, Throws.TypeOf<NotSupportedException>());

            var multi = Cs((0, 0), (1, 1), (2, 0), (3, double.NaN), (4, 0));
            Assert.That(() => multi.IsSimple, Throws.TypeOf<NotSupportedException>());
        }

        [Test]
        public void NearlyCocircularAdjacentPair_StaysFailClosed()
        {
            // The second segment's circumcircle differs from the first's by
            // ~1e-13 in the centre: too close to distinguish "same circle"
            // (interval overlap) from "two circles" (radical line) in double
            // precision. The kernel refuses rather than guessing.
            var cs = Cs((0, 0), (1, 1), (2, 0), (1, -1), (1e-13, 0));
            var ex = Assert.Throws<NotSupportedException>(() => _ = cs.IsSimple);
            Assert.That(ex.Message, Does.Contain("cocircular"));
        }
    }
}
