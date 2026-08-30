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
            // The ring is closed and (since rung 3) provably simple, but the
            // ring-pair conditions (§8.2.1 Desc 11–14) are still undecided —
            // fail-closed naming them, never an unchecked true.
            var ring = Cs((0, 0), (2, 2), (4, 0), (2, -2), (0, 0));
            var cp = new CurvePolygon(ring, _factory);
            Assert.That(() => cp.IsValid,
                Throws.TypeOf<NotSupportedException>().With.Message.Contains("615-h").And.Message.Contains("Desc 11"));
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
