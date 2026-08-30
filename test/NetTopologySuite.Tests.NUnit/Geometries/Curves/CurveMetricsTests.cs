// SPDX-License-Identifier: BSD-3-Clause
// AI-drafted, human-reviewed.

using System;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Curves;
using NUnit.Framework;

namespace NetTopologySuite.Tests.NUnit.Geometries.Curves
{
    /// <summary>
    /// Green contract tests for arc-aware curve metrics as they land
    /// (ISO/IEC 13249-3; NetTopologySuite.Proofs issue #615). Length computes
    /// over the arc locus of §7.3.1 Desc 8: r·θ per segment, with a collinear
    /// triple degenerating to its start–end chord (Desc 8b). Contracts still
    /// red (Distance, Envelope) live in <see cref="CurveMetricsContractTests"/>.
    /// </summary>
    public class CurveMetricsTests
    {
        private readonly GeometryFactory _factory = new GeometryFactory();

        private CircularString Cs(params (double x, double y)[] pts)
        {
            var coords = new Coordinate[pts.Length];
            for (int i = 0; i < pts.Length; i++) coords[i] = new Coordinate(pts[i].x, pts[i].y);
            return new CircularString(_factory.CoordinateSequenceFactory.Create(coords), _factory);
        }

        [Test]
        public void Length_UnitSemicircle_IsPi()
        {
            // The former Red_Length_UnitSemicircle_IsPi contract, flipped green.
            var arc = Cs((1, 0), (0, 1), (-1, 0));
            Assert.That(arc.Length, Is.EqualTo(Math.PI).Within(1e-9),
                "Unit semicircle length is π (the locus, not the 2√2 control chords).");
        }

        [Test]
        public void Length_MajorArc_IsThreeHalvesPi()
        {
            // Sweep > π: (1,0) → (0,1) → (0,-1) runs CCW through 270°, not the
            // 90° minor arc. A wrong-side sweep bug is silent everywhere else.
            var arc = Cs((1, 0), (0, 1), (0, -1));
            Assert.That(arc.Length, Is.EqualTo(3 * Math.PI / 2).Within(1e-9));
        }

        [Test]
        public void Length_CollinearTriple_IsChord()
        {
            // §7.3.1 Desc 8b: a collinear triple degenerates to the straight
            // line from start to end.
            var arc = Cs((0, 0), (1, 1), (2, 2));
            Assert.That(arc.Length, Is.EqualTo(2 * Math.Sqrt(2)).Within(1e-12));
        }

        [Test]
        public void Length_ClosedDegenerateSegment_IsZero()
        {
            // start = end makes the triple trivially collinear, so Desc 8b gives
            // the zero-length start–end chord. The value is ill-formed per
            // Desc 6 — flagging that is arc-aware IsValid's job (ticket 615-g),
            // not Length's.
            var arc = Cs((0, 0), (1, 1), (0, 0));
            Assert.That(arc.Length, Is.EqualTo(0.0).Within(1e-12));
        }

        [Test]
        public void Length_FivePointFullCircle_IsTwoPi()
        {
            // The documented full-circle idiom: two semicircle segments,
            // centre (1,0), radius 1.
            var arc = Cs((0, 0), (1, 1), (2, 0), (1, -1), (0, 0));
            Assert.That(arc.Length, Is.EqualTo(2 * Math.PI).Within(1e-9));
        }

        [Test]
        public void Length_EmptyCircularString_IsZero()
        {
            var arc = Cs();
            Assert.That(arc.Length, Is.EqualTo(0.0));
        }

        [Test]
        public void Length_CompoundCurve_IsComponentSum()
        {
            var line = _factory.CreateLineString(new[]
            {
                new Coordinate(0, 0), new Coordinate(1, 0)
            });
            var arc = Cs((1, 0), (2, 1), (3, 0));
            var cc = new CompoundCurve(new Curve[] { line, arc }, _factory);
            Assert.That(cc.Length, Is.EqualTo(1.0 + Math.PI).Within(1e-9));
        }

        [Test]
        public void Length_EmptyCompoundCurve_IsZero()
        {
            var cc = new CompoundCurve(null, _factory);
            Assert.That(cc.Length, Is.EqualTo(0.0));
        }
    }
}
