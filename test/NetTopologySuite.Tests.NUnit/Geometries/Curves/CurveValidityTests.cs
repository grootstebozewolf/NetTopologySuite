// SPDX-License-Identifier: BSD-3-Clause
// AI-drafted, human-reviewed.

using System;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Curves;
using NUnit.Framework;

namespace NetTopologySuite.Tests.NUnit.Geometries.Curves
{
    /// <summary>
    /// Arc-aware IsValid (ISO/IEC 13249-3; NetTopologySuite.Proofs #615 —
    /// rung 1 ticket 615-g #623, verdict wiring 615-h rung 3 #634). The
    /// honesty contract under test: a value violating an implemented clause
    /// rule returns definite <c>false</c>; CircularString and CompoundCurve
    /// values passing every rule return a checked <c>true</c> (their
    /// §7.3.1 / §7.10.1 obligations are fully rung-covered — "simple ∧
    /// closed ⇒ ring" is a definition, not a validity constraint); a
    /// CurvePolygon with a non-simple ring is definite <c>false</c>
    /// (§8.2.1 Desc 2–3), and a CP passing everything still THROWS naming
    /// the ring-pair conditions (Desc 11–14, issue #639) — an unchecked
    /// <c>true</c> is never returned.
    /// </summary>
    public class CurveValidityTests
    {
        private readonly GeometryFactory _factory = new GeometryFactory();

        private CircularString Cs(params (double x, double y)[] pts)
        {
            var coords = new Coordinate[pts.Length];
            for (int i = 0; i < pts.Length; i++) coords[i] = new Coordinate(pts[i].x, pts[i].y);
            return new CircularString(_factory.CoordinateSequenceFactory.Create(coords), _factory);
        }

        private LineString Line(params (double x, double y)[] pts)
        {
            var coords = new Coordinate[pts.Length];
            for (int i = 0; i < pts.Length; i++) coords[i] = new Coordinate(pts[i].x, pts[i].y);
            return _factory.CreateLineString(coords);
        }

        [Test]
        public void IsValid_DescSixViolatingArc_IsDefiniteFalse()
        {
            // §7.3.1 Desc 6: each arc segment's end shall be distinct from its
            // start. This constructs (intake is representability only, 615-c)
            // and is provably invalid — no arc machinery needed.
            var arc = Cs((0, 0), (1, 1), (0, 0));
            Assert.That(arc.IsValid, Is.False);
        }

        [Test]
        public void IsValid_CleanOpenArc_IsCheckedTrue()
        {
            // Flipped by 615-h rung 3 (#634): §7.3.1's validity obligations
            // are Desc 6 (per-segment endpoints distinct) and Desc 7 (count
            // shape) — both rung-covered — so a clean value is checked-valid.
            // Simplicity is NOT a CircularString validity constraint
            // ("simple ∧ closed ⇒ circular ring", Desc 9–10, is a definition).
            var arc = Cs((0, 0), (1, 1), (2, 0));
            Assert.That(arc.IsValid, Is.True);
        }

        [Test]
        public void IsValid_FivePointFullCircle_IsCheckedTrue()
        {
            // The documented full-circle idiom: Desc-6-clean, Desc-7-shaped —
            // checked-valid (and, since rung 2, a checked simple ring).
            var circle = Cs((0, 0), (1, 1), (2, 0), (1, -1), (0, 0));
            Assert.That(circle.IsValid, Is.True);
        }

        [Test]
        public void IsValid_CoincidentIntermediate_IsCheckedTrueViaDesc8b()
        {
            // Review-pinned sub-reading (615-h rung 3): an intermediate
            // coincident with an endpoint makes the triple exactly collinear
            // (the orientation cross is exactly zero), so §7.3.1 Desc 8b
            // applies — the segment is the start–end chord, and "collinear
            // inputs are legal, not invalid" (§2.1 of the research doc).
            // Desc 6 binds only the segment's START and END points.
            var chordLike = Cs((0, 0), (0, 0), (2, 0));
            Assert.That(chordLike.IsValid, Is.True);
            Assert.That(chordLike.IsSimple, Is.True, "the chord (0,0)-(2,0)");

            var multi = Cs((0, 0), (0, 0), (2, 0), (3, 1), (4, 0));
            Assert.That(multi.IsValid, Is.True);
        }

        [Test]
        public void IsValid_NonFiniteCoordinate_IsDefiniteFalse()
        {
            // Parity with classical IsValidOp, which marks non-finite
            // coordinates invalid — a checked true for garbage would be worse
            // than the old fail-closed throw.
            var arc = Cs((0, 0), (1, double.NaN), (2, 0));
            Assert.That(arc.IsValid, Is.False);
        }

        [Test]
        public void IsValid_CompoundWithDescSixDirtyComponent_IsDefiniteFalse()
        {
            // §7.10.1 Desc 3: well formed only if every component is — a
            // definite-false component makes the compound definite-false.
            var line = _factory.CreateLineString(new[]
            {
                new Coordinate(0, 0), new Coordinate(1, 0)
            });
            var dirty = Cs((1, 0), (2, 2), (1, 0));
            var cc = new CompoundCurve(new Curve[] { line, dirty }, _factory);
            Assert.That(cc.IsValid, Is.False);
        }

        [Test]
        public void IsValid_CleanCompound_IsCheckedTrue()
        {
            // Flipped by 615-h rung 3 (#634): §7.10.1's obligations are
            // contiguity (Desc 7, intake + re-assert) and component
            // well-formedness (Desc 3) — both rung-covered.
            var line = _factory.CreateLineString(new[]
            {
                new Coordinate(0, 0), new Coordinate(1, 0)
            });
            var arc = Cs((1, 0), (2, 1), (3, 0));
            var cc = new CompoundCurve(new Curve[] { line, arc }, _factory);
            Assert.That(cc.IsValid, Is.True);
        }

        [Test]
        public void IsValid_CurvePolygonWithDescSixDirtyRing_IsDefiniteFalse()
        {
            // A single-segment closed arc IS closed (intake accepts it as a
            // ring) and IS Desc-6-dirty — the false propagates through the
            // ring walk.
            var dirtyRing = Cs((0, 0), (1, 1), (0, 0));
            var cp = new CurvePolygon(dirtyRing, _factory);
            Assert.That(cp.IsValid, Is.False);
        }

        [Test]
        public void IsValid_CleanCurvePolygon_StillFailsClosed()
        {
            // The ring is closed and (since rung 3) provably simple, and
            // (since rung 4) with a single ring there is no ring PAIR to
            // refute — but the remaining polygon conditions (§8.2.1
            // Desc 12–14: no spikes/cuts, connected interior) are still
            // undecided. Fail-closed naming them, never an unchecked true.
            var ring = Cs((0, 0), (2, 2), (4, 0), (2, -2), (0, 0));
            var cp = new CurvePolygon(ring, _factory);
            Assert.That(() => cp.IsValid,
                Throws.TypeOf<NotSupportedException>().With.Message.Contains("615-h").And.Message.Contains("Desc 12"));
        }

        [Test]
        public void IsValid_CurvePolygonHoleCrossingShell_IsDefiniteFalse()
        {
            // §8.2.1 Desc 11: the boundary of two rings may intersect in at
            // most one point. These two circular rings CROSS (two proper
            // intersection points) — provably invalid via the pair kernel,
            // and pinned against the oracle's HOLES_DISJOINT lane
            // (NOT_DISJOINT CROSS golden).
            var shell = Cs((0, 0), (2, 2), (4, 0), (2, -2), (0, 0));
            var hole = Cs((3, 1), (5, 3), (7, 1), (5, -1), (3, 1));
            var cp = new CurvePolygon(shell, new Curve[] { hole }, _factory);
            Assert.That(cp.IsValid, Is.False);
        }

        [Test]
        public void IsValid_CurvePolygonRingsTouchingAtOnePoint_StaysFailClosed()
        {
            // Internally tangent circles: shell centre (2,0) r=2, hole centre
            // (3,0) r=1, touching only at (4,0). Fail-closed, now via the
            // kernel's tangency refusal band (rung-4 review): near tangency
            // the discriminant's float error spans both signs, so one touch
            // cannot be told from a close crossing PAIR — the review
            // demonstrated a variant of this shape answering a wrong
            // definite false through a phantom split contact. Deciding the
            // band (and then Desc 12–14 behind it) is issue #641's exact
            // arithmetic.
            var shell = Cs((0, 0), (2, 2), (4, 0), (2, -2), (0, 0));
            var hole = Cs((2, 0), (3, 1), (4, 0), (3, -1), (2, 0));
            var cp = new CurvePolygon(shell, new Curve[] { hole }, _factory);
            Assert.That(() => cp.IsValid,
                Throws.TypeOf<NotSupportedException>().With.Message.Contains("615-h").And.Message.Contains("tangent"));
        }

        [Test]
        public void IsValid_CurvePolygonNearTangentHole_StaysFailClosed()
        {
            // Rung-4 review witness W2, pinned: shell and hole are exactly
            // internally tangent at (2,0) — the rings meet in exactly one
            // point (verified with exact rational arithmetic in the review),
            // so Desc 11 passes and a definite false is WRONG. Before the
            // tangency band this returned exactly that wrong false: the
            // kernel split the tangency into a phantom contact pair ~6e-8
            // apart, 20× the dedup tolerance. The honest double-precision
            // answer is the fail-closed refusal.
            var shell = Cs((0, 2), (2, 0), (0, -2), (-2, 0), (0, 2));
            var hole = new CompoundCurve(new Curve[]
            {
                Cs((0.7865526304750483, 1.213447369524952), (2, 0),
                   (0.7865526304750483, -1.213447369524952)),
                _factory.CreateLineString(new[]
                {
                    new Coordinate(0.7865526304750483, -1.213447369524952),
                    new Coordinate(0.4865526304750483, 0),
                    new Coordinate(0.7865526304750483, 1.213447369524952),
                }),
            }, _factory);
            var cp = new CurvePolygon(shell, new Curve[] { hole }, _factory);
            Assert.That(() => cp.IsValid,
                Throws.TypeOf<NotSupportedException>().With.Message.Contains("tangent"));
        }

        [Test]
        public void IsValid_MultiCurveAllMembersValid_IsCheckedTrue()
        {
            // 615-h rung 4 (#639): §10.1.1 Desc 10 — a geometry collection is
            // well formed only if all elements are; ST_MultiCurve adds no
            // further validity constraint of its own (simplicity is Desc 4's
            // ST_IsSimple obligation, not a validity one). All members are
            // rung-decidable, so the whole value is checked true.
            var mc = new MultiCurve(new Curve[]
            {
                Line((5, 0), (6, 0)),
                Cs((0, 0), (1, 1), (2, 0)),
            }, _factory);
            Assert.That(mc.IsValid, Is.True);
        }

        [Test]
        public void IsValid_MultiCurveWithDescSixDirtyMember_IsDefiniteFalse()
        {
            // §10.1.1 Desc 10: one provably ill-formed member (Desc-6-dirty
            // arc) makes the collection definite false.
            var mc = new MultiCurve(new Curve[]
            {
                Cs((0, 0), (1, 1), (0, 0)),
            }, _factory);
            Assert.That(mc.IsValid, Is.False);
        }

        [Test]
        public void IsValid_MultiSurfaceWithInvalidElement_IsDefiniteFalse()
        {
            // §10.1.1 Desc 10 propagation: a CurvePolygon with a provably
            // non-simple (bowtie) ring is definite false, so the MultiSurface
            // holding it is too.
            var bowtie = Line((0, 0), (2, 2), (2, 0), (0, 2), (0, 0));
            var ms = new MultiSurface(new Geometry[]
            {
                new CurvePolygon(bowtie, _factory),
            }, _factory);
            Assert.That(ms.IsValid, Is.False);
        }

        [Test]
        public void IsValid_MultiSurfaceOverlappingClassicalElements_StaysFailClosed()
        {
            // The rung-3-demonstrated silent-true hole: two overlapping
            // classical squares as a MultiSurface answered TRUE through
            // IsValidOp's GeometryCollection arm, where the same pair as a
            // MultiPolygon answers false. §4.2.27 says element interiors
            // shall not intersect; that interiors-disjoint check is not
            // implemented yet, so the honest answer is a fail-closed throw —
            // never the old unchecked true.
            var a = _factory.CreatePolygon(new[]
            {
                new Coordinate(0, 0), new Coordinate(4, 0), new Coordinate(4, 4),
                new Coordinate(0, 4), new Coordinate(0, 0),
            });
            var b = _factory.CreatePolygon(new[]
            {
                new Coordinate(2, 2), new Coordinate(6, 2), new Coordinate(6, 6),
                new Coordinate(2, 6), new Coordinate(2, 2),
            });
            var ms = new MultiSurface(new Geometry[] { a, b }, _factory);
            Assert.That(() => ms.IsValid,
                Throws.TypeOf<NotSupportedException>().With.Message.Contains("615-h"));
        }

        [Test]
        public void IsValid_MultiSurfaceBoundaryOverlapElements_IsDefiniteFalse()
        {
            // §4.2.27: element boundaries may intersect at a finite number of
            // POINTS — a shared 1-D arc is provably too much. The first
            // element's shell is the full circle; the second's is the upper
            // semicircle closed by its diameter, so the two boundaries share
            // the entire upper semicircle.
            var circle = new CurvePolygon(
                Cs((0, 0), (1, 1), (2, 0), (1, -1), (0, 0)), _factory);
            var halfDisc = new CurvePolygon(
                new CompoundCurve(new Curve[]
                {
                    Cs((0, 0), (1, 1), (2, 0)),
                    Line((2, 0), (0, 0)),
                }, _factory), _factory);
            var ms = new MultiSurface(new Geometry[] { circle, halfDisc }, _factory);
            Assert.That(ms.IsValid, Is.False);
        }

        [Test]
        public void IsValid_CurvePolygonWithNonSimpleRing_IsDefiniteFalse()
        {
            // §8.2.1 Desc 2–3: rings shall be rings = closed ∧ simple. The
            // bowtie LineString ring is closed but provably non-simple —
            // definite false, no ring-pair machinery needed.
            var bowtie = _factory.CreateLineString(new[]
            {
                new Coordinate(0, 0), new Coordinate(2, 2), new Coordinate(2, 0),
                new Coordinate(0, 2), new Coordinate(0, 0),
            });
            var cp = new CurvePolygon(bowtie, _factory);
            Assert.That(cp.IsValid, Is.False);
        }

        [Test]
        public void DefiniteInvalidity_ReasonNamesTheClause()
        {
            var arc = Cs((0, 0), (1, 1), (0, 0));
            Assert.That(CurveValidity.TryFindDefiniteInvalidity(arc, out string reason), Is.True);
            Assert.That(reason, Does.Contain("Desc 6"));

            var dirtyRing = new CurvePolygon(arc, _factory);
            Assert.That(CurveValidity.TryFindDefiniteInvalidity(dirtyRing, out reason), Is.True);
            Assert.That(reason, Does.Contain("exterior ring").And.Contain("Desc 6"));
        }

        [Test]
        public void ContiguityReassertSkipsEmptyNeighboursWithoutCrashing()
        {
            // Serialization-bypass shape: an empty component the constructor
            // would have dropped. The re-assert must give a verdict, not an
            // NRE on the empty neighbour's null endpoints.
            var line = _factory.CreateLineString(new[]
            {
                new Coordinate(0, 0), new Coordinate(1, 0)
            });
            var arc = Cs((1, 0), (2, 1), (3, 0));
            var cc = new CompoundCurve(new Curve[] { line, arc }, _factory);
            Assert.That(CurveValidity.TryFindDefiniteInvalidity(cc, out _), Is.False);
        }

        [Test]
        public void IsValid_EmptyCurves_AreTrue()
        {
            // Matches IsValidOp: empty geometries are always valid.
            Assert.That(Cs().IsValid, Is.True);
            Assert.That(new CompoundCurve(null, _factory).IsValid, Is.True);
            Assert.That(new CurvePolygon(null, _factory).IsValid, Is.True);
        }
    }
}
