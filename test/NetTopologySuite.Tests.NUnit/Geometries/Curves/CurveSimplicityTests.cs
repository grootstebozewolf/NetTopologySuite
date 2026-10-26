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
        public void TwoArcsTangentAtSharedVertex_IsSimpleByExactTangency()
        {
            // Flipped twice. Checked-true until the rung-4 review showed the
            // verdict rested on a float discriminant computing an EXACT zero
            // (dyadic luck — inside the band, a touch and a crossing pair
            // ~sqrt(eps) apart look the same), then fail-closed. Rung 5
            // (#641) decides the band in exact rational arithmetic: the
            // circles (centres (1,0) and (3,0), r = 1) are EXACTLY externally
            // tangent at (2,0), the permitted connecting vertex — simple.
            var cs = Cs((0, 0), (1, 1), (2, 0), (3, -1), (4, 0));
            Assert.That(cs.IsSimple, Is.True);
            Assert.That(cs.IsRing, Is.False, "open");
        }

        [Test]
        public void TangentPairPerturbedOneUlp_IsDecidedByAnchoredCrossing()
        {
            // Nudge the second arc's intermediate point by one ulp and its
            // circle is no longer tangent: both circles still pass through
            // (2,0), so the exact discriminant is POSITIVE — a crossing pair
            // inside the float band. Rung 5 (#641) places it exactly: (2,0)
            // lies on both circles (the anchor), so the second crossing is
            // its rational reflection across the line of centres — and it
            // lies off the arcs. The only contact is the permitted vertex.
            var cs = Cs((0, 0), (1, 1), (2, 0), (3, Math.BitIncrement(-1.0)), (4, 0));
            Assert.That(cs.IsSimple, Is.True);
        }

        [Test]
        public void MultiCurve_UnanchoredCrossingPairInBand_StaysFailClosed()
        {
            // The residue rung 5 keeps (#981): circle A centre (0,0) r = 2,
            // circle B centre (3,0) r = 1 + 2^-50 — they cross in two points
            // ~3e-8 apart near (2,0), deep in the float band, and no control
            // point of either arc lies on the other circle, so neither
            // crossing is rational. Exact arithmetic knows the pair exists
            // but cannot place it on or off the arcs; refuse.
            double d = Math.Pow(2, -50);
            var mc = new MultiCurve(new Curve[]
            {
                Cs((0, 2), (2, 0), (0, -2)),
                Cs((2 - d, 0), (3, 1 + d), (4 + d, 0)),
            }, _factory);
            Assert.That(() => mc.IsSimple,
                Throws.TypeOf<NotSupportedException>().With.Message.Contains("tangent").And.Message.Contains("981"));
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
        public void NonAdjacentTangentTouch_IsNotSimpleByExactTangency()
        {
            // The last chord runs along y = 1, tangent to the first arc's
            // circle at (1,1). Fail-closed since the rung-4 review (the float
            // circle-line discriminant cannot tell a touch from a close
            // crossing pair); rung 5 (#641) decides it exactly: the
            // discriminant r²·|v|² − (v × (C − q0))² is exactly zero, and the
            // rational foot of the perpendicular, (1,1), lies on the chord
            // and on the first arc — a non-adjacent contact, not simple.
            var cs = Cs((0, 0), (1, 1), (2, 0), (3, 0.5), (4, 1), (2.5, 1), (1, 1));
            Assert.That(cs.IsSimple, Is.False);
        }

        [Test]
        public void ExactlyCocircularDistinctTriples_IsSimpleRing()
        {
            // Three arcs on the circle x² + y² = 25 through different control
            // triples. Already simple before rung 5 (the canonicalized double
            // circumcentres happen to agree); since rung 5 (#641) "same
            // circle" is an EXACT comparison of the rational circles, and on
            // one circle the overlap/touch decision is exact side-of-chord
            // arithmetic — this pins that path.
            var cs = Cs((5, 0), (4, 3), (0, 5), (-3, 4), (-5, 0), (0, -5), (5, 0));
            Assert.That(cs.IsSimple, Is.True);
            Assert.That(cs.IsRing, Is.True);
        }

        [Test]
        public void MultiCurve_NearMissInTangencyBand_IsSimple()
        {
            // Circle A: centre (0,0), r = 2. Circle B: centre (3,0),
            // r = 1 − 2^-50 — a gap of 2^-50 from external tangency, deep
            // inside the float band. The exact discriminant is negative: the
            // circles do not meet, so the members are disjoint.
            double d = Math.Pow(2, -50);
            var mc = new MultiCurve(new Curve[]
            {
                Cs((0, 2), (2, 0), (0, -2)),
                Cs((2 + d, 0), (3, 1 - d), (4 - d, 0)),
            }, _factory);
            Assert.That(mc.IsSimple, Is.True);
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
        public void CompoundLineTangentThroughArc_IsNotSimpleByExactTangency()
        {
            // Both of this compound's non-trivial contacts are line
            // tangencies (x = 2 at (2,0); y = 1 at (1,1)). Fail-closed since
            // the rung-4 review; rung 5 (#641) decides both exactly — the
            // y = 1 chord touches the arc at (1,1), interior to the arc and
            // not a permitted vertex: not simple.
            var cc = new CompoundCurve(new Curve[]
            {
                Cs((0, 0), (1, 1), (2, 0)),
                Line((2, 0), (2, 1), (0, 1)),
            }, _factory);
            Assert.That(cc.IsSimple, Is.False);
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
        public void MultiCurve_NearTangentMembers_IsNotSimpleByExactTangency()
        {
            // Rung-4 review witness W1: both members pass through the shared
            // control point (2,0) — the mid control of both arcs, so on both
            // loci and interior to both open members — and the circumcircles
            // are exactly internally tangent there. Before the tangency band
            // this answered a SILENT WRONG TRUE (h2 rounded negative and the
            // contact vanished); the band made it fail closed; rung 5 (#641)
            // decides the exact discriminant (zero) and places the touch
            // point exactly on both arcs — §10.3.1 Desc 4 refutes it.
            var mc = new MultiCurve(new Curve[]
            {
                Cs((0, 2), (2, 0), (0, -2)),
                Cs((0.8466466098542542, 1.1533533901457458), (2, 0),
                   (0.8466466098542542, -1.1533533901457458)),
            }, _factory);
            Assert.That(mc.IsSimple, Is.False);
        }

        [Test]
        public void MultiCurve_MemberResidueDoesNotPreemptCrossMemberRefutation()
        {
            // #641 note (c): the first member is undecidable on its own, but
            // the second and third members cross each other
            // transversally — a definite refutation that must win over the
            // deferred member residue (here a Desc-6-degenerate segment,
            // whose chain cannot even be built).
            var mc = new MultiCurve(new Curve[]
            {
                Cs((0, 0), (1, 1), (0, 0)),
                Line((10, 0), (12, 2)),
                Line((10, 2), (12, 0)),
            }, _factory);
            Assert.That(mc.IsSimple, Is.False);
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
        public void NearlyCocircularAdjacentPair_IsDecidedExactly()
        {
            // The second segment's circumcircle differs from the first's by
            // ~1e-13 in the centre: refused before rung 5 (too close to tell
            // "same circle" from "two circles" in double precision). Exactly,
            // the circles are distinct and both pass through (2,0) and
            // (1,-1) — those ARE the two crossings. (1,-1) is not on the
            // upper first arc, so the only contact is the connecting vertex,
            // and the 1e-13 gap keeps the open chain from closing: simple.
            var cs = Cs((0, 0), (1, 1), (2, 0), (1, -1), (1e-13, 0));
            Assert.That(cs.IsSimple, Is.True);
            Assert.That(cs.IsRing, Is.False, "open by 1e-13");
        }

        [Test]
        public void NearlyCocircularUnanchoredPair_StaysFailClosed()
        {
            // The residue rung 5 keeps (#981): the upper arcs of x² + y² = 25
            // and of the same circle shifted 2^-45 in x. The circles are
            // distinct but within the cocircular tolerance, and they cross at
            // x = 2^-46 with an irrational y near 5 — on both arcs, and no
            // control point lies on the other circle, so no crossing is
            // rational. Refused rather than guessed.
            double d = Math.Pow(2, -45);
            var mc = new MultiCurve(new Curve[]
            {
                Cs((-3, 4), (0, 5), (3, 4)),
                Cs((-3 + d, 4), (d, 5), (3 + d, 4)),
            }, _factory);
            Assert.That(() => mc.IsSimple,
                Throws.TypeOf<NotSupportedException>().With.Message.Contains("cocircular"));
        }
    }
}
