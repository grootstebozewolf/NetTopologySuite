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
    /// (§8.2.1 Desc 2–3), and since rung 5 (#641) the remaining polygon
    /// conditions (Desc 11 counting, Desc 12–14: hole containment, no nested
    /// holes, connected interior) are decided by exact point-in-ring over the
    /// loci — a clean CP is checked <c>true</c>. MultiSurface interiors-
    /// disjoint (§4.2.27) is decided where the boundary pieces have exact
    /// samples. An unchecked <c>true</c> is never returned.
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
        public void IsValid_CleanCurvePolygon_IsCheckedTrue()
        {
            // Flipped by rung 5 (#641): the ring is closed and provably
            // simple (rung 3), and with a single ring §8.2.1 Desc 11–14 have
            // nothing left to refute — no hole to contain, no touch graph.
            var ring = Cs((0, 0), (2, 2), (4, 0), (2, -2), (0, 0));
            var cp = new CurvePolygon(ring, _factory);
            Assert.That(cp.IsValid, Is.True);
        }

        [Test]
        public void IsValid_CurvePolygonConcentricHole_IsCheckedTrue()
        {
            // Hole centre (2,0) r = 1 inside the shell centre (2,0) r = 2, no
            // contact. The hole's vertices (1,0) and (3,0) lie ON the
            // shell's arc chords, so the exact winding splits those arcs at
            // their intermediate points — the hole is inside: valid.
            var shell = Cs((0, 0), (2, 2), (4, 0), (2, -2), (0, 0));
            var hole = Cs((1, 0), (2, 1), (3, 0), (2, -1), (1, 0));
            var cp = new CurvePolygon(shell, new Curve[] { hole }, _factory);
            Assert.That(cp.IsValid, Is.True);
        }

        [Test]
        public void IsValid_CurvePolygonHoleOutsideShell_IsDefiniteFalse()
        {
            // §8.2.1 Desc 12 (p = Closure(Interior(p))): a hole outside the
            // shell is not in the closure of the interior. No contact at all,
            // so this is decided purely by exact point-in-ring.
            var shell = Cs((0, 0), (2, 2), (4, 0), (2, -2), (0, 0));
            var hole = Cs((9, 0), (10, 1), (11, 0), (10, -1), (9, 0));
            var cp = new CurvePolygon(shell, new Curve[] { hole }, _factory);
            Assert.That(cp.IsValid, Is.False);
        }

        [Test]
        public void IsValid_CurvePolygonHoleTangentFromOutside_IsDefiniteFalse()
        {
            // One contact — externally tangent at (4,0), decided exactly — so
            // Desc 11 passes; the hole's other samples lie outside the shell.
            var shell = Cs((0, 0), (2, 2), (4, 0), (2, -2), (0, 0));
            var hole = Cs((4, 0), (5, 1), (6, 0), (5, -1), (4, 0));
            var cp = new CurvePolygon(shell, new Curve[] { hole }, _factory);
            Assert.That(cp.IsValid, Is.False);
        }

        [Test]
        public void IsValid_CurvePolygonNestedHoles_IsDefiniteFalse()
        {
            // Desc 12 again: the inner hole bounds nothing — its ring is not
            // in the closure of the polygon's interior.
            var shell = Cs((-4, 0), (0, 4), (4, 0), (0, -4), (-4, 0));
            var outerHole = Cs((-2, 0), (0, 2), (2, 0), (0, -2), (-2, 0));
            var innerHole = Cs((-1, 0), (0, 1), (1, 0), (0, -1), (-1, 0));
            var cp = new CurvePolygon(shell, new Curve[] { outerHole, innerHole }, _factory);
            Assert.That(cp.IsValid, Is.False);
        }

        [Test]
        public void IsValid_CurvePolygonHoleChainDisconnectsInterior_IsDefiniteFalse()
        {
            // §8.2.1 Desc 13: two holes, each internally tangent to the shell
            // at one end of the x-axis diameter and tangent to each other at
            // the origin. Every ring pair meets in exactly one point (Desc 11
            // passes), but the touch graph shell–h1–h2–shell is a cycle: the
            // interior splits into an upper and a lower half.
            var shell = Cs((-4, 0), (0, 4), (4, 0), (0, -4), (-4, 0));
            var left = Cs((-4, 0), (-2, 2), (0, 0), (-2, -2), (-4, 0));
            var right = Cs((0, 0), (2, 2), (4, 0), (2, -2), (0, 0));
            var cp = new CurvePolygon(shell, new Curve[] { left, right }, _factory);
            Assert.That(cp.IsValid, Is.False);

            // Drop the right hole: shell–h1 alone is a tree — connected.
            var tree = new CurvePolygon(shell, new Curve[] { left }, _factory);
            Assert.That(tree.IsValid, Is.True);
        }

        [Test]
        public void IsValid_CurvePolygonHolesTouchingEachOther_IsCheckedTrue()
        {
            // Two holes tangent to each other at the origin, neither touching
            // the shell: the touch graph is a single edge pair — a tree.
            var shell = Cs((-4, 0), (0, 4), (4, 0), (0, -4), (-4, 0));
            var left = Cs((-2, 0), (-1, 1), (0, 0), (-1, -1), (-2, 0));
            var right = Cs((0, 0), (1, 1), (2, 0), (1, -1), (0, 0));
            var cp = new CurvePolygon(shell, new Curve[] { left, right }, _factory);
            Assert.That(cp.IsValid, Is.True);
        }

        [Test]
        public void IsValid_CurvePolygonLineShellArcHole_IsCheckedTrue()
        {
            // Mixed ring types: a LineString square shell around a circular
            // hole.
            var shell = Line((0, 0), (10, 0), (10, 10), (0, 10), (0, 0));
            var hole = Cs((4, 5), (5, 6), (6, 5), (5, 4), (4, 5));
            var cp = new CurvePolygon(shell, new Curve[] { hole }, _factory);
            Assert.That(cp.IsValid, Is.True);
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
        public void IsValid_CurvePolygonRingsTouchingAtOnePoint_IsCheckedTrue()
        {
            // #641's canonical pending witness: internally tangent circles,
            // shell centre (2,0) r=2, hole centre (3,0) r=1, touching only at
            // (4,0). Fail-closed since the rung-4 tangency band; rung 5
            // decides it end to end — the exact discriminant is zero (one
            // touch point, on every arc pair that reaches (4,0)), so Desc 11
            // passes; an exact sample of the hole lies inside the shell
            // (containment); the touch graph shell–(4,0)–hole is a tree
            // (connected interior). Valid.
            var shell = Cs((0, 0), (2, 2), (4, 0), (2, -2), (0, 0));
            var hole = Cs((2, 0), (3, 1), (4, 0), (3, -1), (2, 0));
            var cp = new CurvePolygon(shell, new Curve[] { hole }, _factory);
            Assert.That(cp.IsValid, Is.True);
        }

        [Test]
        public void IsValid_CurvePolygonNearTangentHole_IsCheckedTrue()
        {
            // Rung-4 review witness W2: shell and hole are exactly internally
            // tangent at (2,0) — the rings meet in exactly one point (the
            // review verified it with exact rational arithmetic), so Desc 11
            // passes and a definite false is WRONG. Before the tangency band
            // this returned exactly that wrong false (the kernel split the
            // tangency into a phantom contact pair ~6e-8 apart, 20× the dedup
            // tolerance); the band made it fail closed; rung 5 (#641) decides
            // the tangency exactly and the containment by exact
            // point-in-ring: valid.
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
            Assert.That(cp.IsValid, Is.True);
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
        public void IsValid_MultiSurfaceOverlappingClassicalElements_IsDefiniteFalse()
        {
            // The rung-3-demonstrated silent-true hole: two overlapping
            // classical squares as a MultiSurface answered TRUE through
            // IsValidOp's GeometryCollection arm. Rung 4 made it fail closed;
            // rung 5 (#641) decides it: an all-classical MultiSurface is a
            // classical MultiPolygon, whose validity rule IS §4.2.27
            // (interiors disjoint, boundaries meeting in finitely many
            // points) — definite false, as the MultiPolygon answers.
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
            Assert.That(ms.IsValid, Is.False);

            var touching = new MultiSurface(new Geometry[]
            {
                a,
                _factory.CreatePolygon(new[]
                {
                    new Coordinate(4, 4), new Coordinate(8, 4), new Coordinate(8, 8),
                    new Coordinate(4, 8), new Coordinate(4, 4),
                }),
            }, _factory);
            Assert.That(touching.IsValid, Is.True, "corner touch: finitely many boundary points");
        }

        [Test]
        public void IsValid_MultiSurfaceDisjointCurveElements_IsCheckedTrue()
        {
            // §4.2.27 with arcs: two disjoint circles, and a circle sitting in
            // the hole of an annulus. No boundary contact at all, every exact
            // boundary sample of each element lies outside the other.
            var a = new CurvePolygon(Cs((0, 0), (1, 1), (2, 0), (1, -1), (0, 0)), _factory);
            var b = new CurvePolygon(Cs((5, 0), (6, 1), (7, 0), (6, -1), (5, 0)), _factory);
            Assert.That(new MultiSurface(new Geometry[] { a, b }, _factory).IsValid, Is.True);

            var annulus = new CurvePolygon(
                Cs((-4, 0), (0, 4), (4, 0), (0, -4), (-4, 0)),
                new Curve[] { Cs((-2, 0), (0, 2), (2, 0), (0, -2), (-2, 0)) }, _factory);
            var island = new CurvePolygon(Cs((-1, 0), (0, 1), (1, 0), (0, -1), (-1, 0)), _factory);
            Assert.That(new MultiSurface(new Geometry[] { annulus, island }, _factory).IsValid, Is.True);
        }

        [Test]
        public void IsValid_MultiSurfaceTangentAtSharedVertex_IsCheckedTrue()
        {
            // Two circles externally tangent at (2,0), a vertex of both. The
            // exact discriminant decides the touch; the contacts cut both
            // boundaries into whole segments, and each segment's exact
            // interior sample lies outside the other element.
            var a = new CurvePolygon(Cs((0, 0), (1, 1), (2, 0), (1, -1), (0, 0)), _factory);
            var b = new CurvePolygon(Cs((2, 0), (3, 1), (4, 0), (3, -1), (2, 0)), _factory);
            Assert.That(new MultiSurface(new Geometry[] { a, b }, _factory).IsValid, Is.True);
        }

        [Test]
        public void IsValid_MultiSurfaceOverlappingCurveElements_IsDefiniteFalse()
        {
            // An exact boundary sample of one element strictly inside the
            // other proves the interiors intersect — no contact bookkeeping
            // needed. Circle b's vertex (1,0) lies inside circle a.
            var a = new CurvePolygon(Cs((-2, 0), (0, 2), (2, 0), (0, -2), (-2, 0)), _factory);
            var b = new CurvePolygon(Cs((1, 0), (3, 2), (5, 0), (3, -2), (1, 0)), _factory);
            Assert.That(new MultiSurface(new Geometry[] { a, b }, _factory).IsValid, Is.False);

            // Mixed classical/curve elements take the same path.
            var square = _factory.CreatePolygon(new[]
            {
                new Coordinate(-1, -1), new Coordinate(1, -1), new Coordinate(1, 1),
                new Coordinate(-1, 1), new Coordinate(-1, -1),
            });
            Assert.That(new MultiSurface(new Geometry[] { a, square }, _factory).IsValid, Is.False);
        }

        [Test]
        public void IsValid_MultiSurfaceLensOverlapWithoutInsideSample_StaysFailClosed()
        {
            // The residue rung 5 names: circles centre (0,0) and (7,7), both
            // r = 5, overlap in a thin lens around (3.5, 3.5), but no control
            // point of either lies inside the other and the boundaries cross
            // at points that are vertices of neither — the boundary pieces
            // between those crossings have no exact sample yet.
            var a = new CurvePolygon(Cs((5, 0), (0, 5), (-5, 0), (0, -5), (5, 0)), _factory);
            var b = new CurvePolygon(Cs((12, 7), (7, 12), (2, 7), (7, 2), (12, 7)), _factory);
            var ms = new MultiSurface(new Geometry[] { a, b }, _factory);
            Assert.That(() => ms.IsValid,
                Throws.TypeOf<NotSupportedException>().With.Message.Contains("not a vertex shared by both"));
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
