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
        public void TwoArcsTangentAtSharedVertex_IsSimple()
        {
            // The circles (centre (1,0), r=1) and (centre (3,0), r=1) are
            // externally tangent exactly at the connecting vertex (2,0):
            // the only locus contact is the permitted one.
            var cs = Cs((0, 0), (1, 1), (2, 0), (3, -1), (4, 0));
            Assert.That(cs.IsSimple, Is.True);
            Assert.That(cs.IsRing, Is.False, "open chain");
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
            var cs = Cs((0, 0), (1, 1), (2, 0), (3, -1), (4, 0), (5, 1), (6, 0));
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
