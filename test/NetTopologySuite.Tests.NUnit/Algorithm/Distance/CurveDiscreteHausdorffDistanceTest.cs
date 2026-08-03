// SPDX-License-Identifier: BSD-3-Clause AND CC0-1.0
// AI-drafted, human-reviewed. Assisted-by: Claude / Grok (xAI)
//
// TAG D-HF green pin — arc-length densify for curve directed Hausdorff.
// In-library Geometries.Curves (NTS #854), not NetTopologySuite.Curve package.

using NetTopologySuite.Algorithm.Distance;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Curves;
using NetTopologySuite.IO;
using NUnit.Framework;

namespace NetTopologySuite.Tests.NUnit.Algorithm.Distance
{
    /// <summary>
    /// Green pin for TAG D-HF (curve-aware discrete / directed Hausdorff).
    /// </summary>
    /// <remarks>
    /// Witness: asymmetric <c>CIRCULARSTRING (0 0, 2 3, 10 0)</c> vs baseline
    /// <c>LINESTRING (0 0, 10 0)</c>. Continuous directed Hausdorff (apex of the
    /// circle above the x-axis) is ≈ 3.96764. Control-point discrete densify
    /// under-estimates at mid-control height 3; arc-length densify via
    /// <see cref="CurveDiscreteHausdorffDistance"/> approaches continuous.
    /// </remarks>
    public class CurveDiscreteHausdorffDistanceTest
    {
        /// <summary>
        /// Continuous directed Hausdorff h(arc, baseline) for the witness arc
        /// (analytic max height of the circle through (0,0),(2,3),(10,0)
        /// above the x-axis; centre (5, −7/6), r ≈ 5.134).
        /// </summary>
        private const double ExpectedContinuous = 3.96764;

        private const double Tol = 1e-3;

        private readonly WKTReader _reader = new WKTReader();

        [Test]
        public void OrientedHausdorff_samplesArcNotControlChords()
        {
            var arc = _reader.Read("CIRCULARSTRING (0 0, 2 3, 10 0)");
            var baseline = _reader.Read("LINESTRING (0 0, 10 0)");

            Assert.That(arc, Is.InstanceOf<CircularString>());

            var cs = (CircularString)arc;
            var controlCoords = cs.CoordinateSequence.ToCoordinateArray();
            var controlPolyline = arc.Factory.CreateLineString(controlCoords);

            double controlOnly = new DiscreteHausdorffDistance(controlPolyline, baseline)
                .OrientedDistance();
            double controlChordDensify = new DiscreteHausdorffDistance(controlPolyline, baseline)
            {
                DensifyFraction = 0.05
            }.OrientedDistance();

            double curveAware = CurveDiscreteHausdorffDistance.OrientedDistance(
                cs, baseline, densifyFraction: 0.05);

            Assert.That(controlOnly, Is.EqualTo(3.0).Within(1e-9),
                "control-only discrete stays at mid-control height 3");
            Assert.That(controlChordDensify, Is.EqualTo(3.0).Within(1e-9),
                "control-chord densify cannot exceed mid-control height");

            Assert.That(curveAware, Is.EqualTo(ExpectedContinuous).Within(Tol),
                "D-HF: arc-length densify should approach continuous h≈"
                + ExpectedContinuous + "; got " + curveAware);
        }

        [Test]
        public void DefaultDensifyFraction_matchesExplicit()
        {
            var cs = (CircularString)_reader.Read("CIRCULARSTRING (0 0, 2 3, 10 0)");
            var baseline = _reader.Read("LINESTRING (0 0, 10 0)");

            double withDefault = CurveDiscreteHausdorffDistance.OrientedDistance(cs, baseline);
            double withExplicit = CurveDiscreteHausdorffDistance.OrientedDistance(cs, baseline, 0.05);

            Assert.That(withDefault, Is.EqualTo(withExplicit).Within(1e-12));
            Assert.That(withDefault, Is.EqualTo(ExpectedContinuous).Within(Tol));
        }

        [Test]
        public void SymmetricDistance_isMaxOfBothDirections()
        {
            var cs = (CircularString)_reader.Read("CIRCULARSTRING (0 0, 2 3, 10 0)");
            var baseline = _reader.Read("LINESTRING (0 0, 10 0)");

            double hAb = CurveDiscreteHausdorffDistance.OrientedDistance(cs, baseline, 0.05);
            double hBa = CurveDiscreteHausdorffDistance.OrientedDistance(baseline, cs, 0.05);
            double h = CurveDiscreteHausdorffDistance.Distance(cs, baseline, 0.05);

            Assert.That(h, Is.EqualTo(System.Math.Max(hAb, hBa)).Within(1e-12));
            Assert.That(hAb, Is.EqualTo(ExpectedContinuous).Within(Tol));
            // Reverse direction densifies the chord query; nearest-on-arc for a
            // CircularString target still uses the control graph today, so hBa is
            // not yet pinned to continuous. Only the max-of-both definition is.
            Assert.That(hBa, Is.GreaterThanOrEqualTo(0.0));
        }

        [Test]
        public void CompoundCurve_delegatesCircularMembers()
        {
            // Line segment + the asymmetric arc, continuous.
            var cc = (CompoundCurve)_reader.Read(
                "COMPOUNDCURVE ((-1 0, 0 0), CIRCULARSTRING (0 0, 2 3, 10 0))");
            var baseline = _reader.Read("LINESTRING (-1 0, 10 0)");

            double h = CurveDiscreteHausdorffDistance.OrientedDistance(cc, baseline, 0.05);
            Assert.That(h, Is.EqualTo(ExpectedContinuous).Within(Tol),
                "CompoundCurve should densify CircularString members by arc length");
        }
    }
}
