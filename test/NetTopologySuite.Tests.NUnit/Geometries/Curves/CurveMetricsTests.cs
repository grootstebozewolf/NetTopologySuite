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
        public void Length_UnitSemicircle_ClockwiseTraversal_IsPi()
        {
            // The #618 witness verbatim: same locus as above, opposite (CW)
            // traversal — the sweep direction must not change the measure.
            var arc = Cs((-1, 0), (0, 1), (1, 0));
            Assert.That(arc.Length, Is.EqualTo(Math.PI).Within(1e-9));
        }

        [Test]
        public void Length_MultiSegmentUnequalRadii_IsSegmentSum()
        {
            // Two segments with different radii: r=1 semicircle then r=2
            // semicircle, total π + 2π.
            var arc = Cs((0, 0), (1, 1), (2, 0), (4, 2), (6, 0));
            Assert.That(arc.Length, Is.EqualTo(3 * Math.PI).Within(1e-9));
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

        // --- Envelope (ticket 615-e): extremes over the arc locus, §5.1.19
        // Desc 2b — the value's point set, not its control points. ------------

        private CircularString UnitArc(double startDeg, double midDeg, double endDeg)
        {
            static double Rad(double d) => d * Math.PI / 180.0;
            return Cs(
                (Math.Cos(Rad(startDeg)), Math.Sin(Rad(startDeg))),
                (Math.Cos(Rad(midDeg)), Math.Sin(Rad(midDeg))),
                (Math.Cos(Rad(endDeg)), Math.Sin(Rad(endDeg))));
        }

        [Test]
        public void Envelope_IncludesAxisExtremeBeyondControls()
        {
            // The former Red_Envelope contract, flipped green: −30°…50° on the
            // unit circle reaches x=1 at angle 0°, which is not a control point.
            var arc = UnitArc(-30, 10, 50);
            Assert.That(arc.EnvelopeInternal.MaxX, Is.EqualTo(1.0).Within(1e-12));
        }

        [TestCase(-30, 10, 50, "+x")]
        [TestCase(40, 80, 140, "+y")]
        [TestCase(130, 170, 230, "-x")]
        [TestCase(220, 260, 320, "-y")]
        public void Envelope_EachAxisCrossingIsExact(double s, double m, double e, string axis)
        {
            var env = UnitArc(s, m, e).EnvelopeInternal;
            double v = axis switch
            {
                "+x" => env.MaxX,
                "+y" => env.MaxY,
                "-x" => -env.MinX,
                _ => -env.MinY,
            };
            // The crossing extreme is centre ± r EXACTLY (unit vector math),
            // not an atan2 approximation.
            Assert.That(v, Is.EqualTo(1.0).Within(1e-12));
        }

        [Test]
        public void Envelope_NoAxisCrossing_IsTheEndpointBox()
        {
            // 10°…80° crosses no axis direction: x and y are monotone along
            // the arc, so the extremes are the endpoints'.
            static double Rad(double d) => d * Math.PI / 180.0;
            var env = UnitArc(10, 40, 80).EnvelopeInternal;
            Assert.That(env.MinX, Is.EqualTo(Math.Cos(Rad(80))).Within(1e-12));
            Assert.That(env.MaxX, Is.EqualTo(Math.Cos(Rad(10))).Within(1e-12));
            Assert.That(env.MinY, Is.EqualTo(Math.Sin(Rad(10))).Within(1e-12));
            Assert.That(env.MaxY, Is.EqualTo(Math.Sin(Rad(80))).Within(1e-12));
        }

        [Test]
        public void Envelope_ClockwiseArc_MatchesTheReversedLocus()
        {
            // Same locus as the flip test, traversed CW: the envelope is a
            // property of the point set, not the direction.
            var env = UnitArc(50, 10, -30).EnvelopeInternal;
            Assert.That(env.MaxX, Is.EqualTo(1.0).Within(1e-12));
        }

        [Test]
        public void Envelope_FivePointFullCircle_IsTheCircleBox()
        {
            var env = Cs((0, 0), (1, 1), (2, 0), (1, -1), (0, 0)).EnvelopeInternal;
            Assert.That(env.MinX, Is.EqualTo(0.0).Within(1e-12));
            Assert.That(env.MaxX, Is.EqualTo(2.0).Within(1e-12));
            Assert.That(env.MinY, Is.EqualTo(-1.0).Within(1e-12));
            Assert.That(env.MaxY, Is.EqualTo(1.0).Within(1e-12));
        }

        [Test]
        public void Envelope_CollinearSegment_IsTheChordBox_ExcludingTheIntermediate()
        {
            // §7.3.1 Desc 8b: the collinear locus is the start–end chord; the
            // intermediate control point is NOT part of the point set.
            var env = Cs((0, 0), (5, 5), (2, 2)).EnvelopeInternal;
            Assert.That(env.MaxX, Is.EqualTo(2.0).Within(1e-12));
            Assert.That(env.MaxY, Is.EqualTo(2.0).Within(1e-12));
        }

        [Test]
        public void Envelope_CompoundCurve_IsTheComponentUnion()
        {
            var line = _factory.CreateLineString(new[]
            {
                new Coordinate(-3, 0), new Coordinate(1, 0)
            });
            var arc = Cs((1, 0), (2, 1), (3, 0)); // bulges to y=1, x max 3
            var cc = new CompoundCurve(new Curve[] { line, arc }, _factory);
            var env = cc.EnvelopeInternal;
            Assert.That(env.MinX, Is.EqualTo(-3.0).Within(1e-12));
            Assert.That(env.MaxX, Is.EqualTo(3.0).Within(1e-12));
            Assert.That(env.MaxY, Is.EqualTo(1.0).Within(1e-12));
            Assert.That(env.MinY, Is.EqualTo(0.0).Within(1e-12));
        }
    }
}
