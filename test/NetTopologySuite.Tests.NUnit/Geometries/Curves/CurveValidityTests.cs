// SPDX-License-Identifier: BSD-3-Clause
// AI-drafted, human-reviewed.

using System;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Curves;
using NUnit.Framework;

namespace NetTopologySuite.Tests.NUnit.Geometries.Curves
{
    /// <summary>
    /// Arc-aware IsValid, rung 1 (ISO/IEC 13249-3; NetTopologySuite.Proofs
    /// #615 ticket 615-g). The honesty contract under test: a value violating
    /// an IMPLEMENTED clause rule returns definite <c>false</c>; a value
    /// passing every implemented rule still THROWS (fail-closed, naming the
    /// missing rung) because curve simplicity needs arc-arc intersection
    /// (rung 2, ticket 615-h) — an unchecked <c>true</c> is never returned.
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
        public void IsValid_CleanOpenArc_StillFailsClosed()
        {
            // Desc-6-clean, so no implemented rule refutes it — but simplicity
            // is rung 2 (ticket 615-h), so a checked true is not yet possible
            // and an unchecked true must never be returned.
            var arc = Cs((0, 0), (1, 1), (2, 0));
            Assert.That(() => arc.IsValid,
                Throws.TypeOf<NotSupportedException>().With.Message.Contains("615-h"));
        }

        [Test]
        public void IsValid_FivePointFullCircle_StillFailsClosed()
        {
            // The documented full-circle idiom is Desc-6-clean (each segment's
            // endpoints are distinct), so it must NOT come back false — and a
            // true would be unchecked, so it throws like any clean value.
            var circle = Cs((0, 0), (1, 1), (2, 0), (1, -1), (0, 0));
            Assert.That(() => circle.IsValid,
                Throws.TypeOf<NotSupportedException>().With.Message.Contains("615-h"));
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
        public void IsValid_CleanCompound_StillFailsClosed()
        {
            var line = _factory.CreateLineString(new[]
            {
                new Coordinate(0, 0), new Coordinate(1, 0)
            });
            var arc = Cs((1, 0), (2, 1), (3, 0));
            var cc = new CompoundCurve(new Curve[] { line, arc }, _factory);
            Assert.That(() => cc.IsValid,
                Throws.TypeOf<NotSupportedException>().With.Message.Contains("615-h"));
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
            var ring = Cs((0, 0), (2, 2), (4, 0), (2, -2), (0, 0));
            var cp = new CurvePolygon(ring, _factory);
            Assert.That(() => cp.IsValid,
                Throws.TypeOf<NotSupportedException>().With.Message.Contains("615-h"));
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
