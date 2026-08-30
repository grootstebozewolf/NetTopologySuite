// SPDX-License-Identifier: BSD-3-Clause
//
// Intentional-fail hooks for arc-aware Distance on the SQL/MM curve
// foundation. These pin expected contracts and stay red until arc-aware
// Distance lands; today DistanceOp throws NotSupportedException instead
// of returning chord-based stubs. Length (615-d) and Envelope (615-e)
// flipped green and live in CurveMetricsTests.
//
// Assisted-by: xAI Grok

using System;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Curves;
using NetTopologySuite.Operation.Distance;
using NUnit.Framework;

namespace NetTopologySuite.Tests.NUnit.Geometries.Curves
{
    /// <summary>
    /// Intentional-fail contract tests for arc-aware curve metrics.
    /// Assert SQL/MM / GEOS-quality Distance behaviour; they stay red until
    /// arc-aware Distance lands (today they fail with
    /// <see cref="NotSupportedException"/> rather than wrong chord values).
    /// Length (615-d) and Envelope (615-e) flipped green: see
    /// <see cref="CurveMetricsTests"/>.
    /// Excluded from default CI via <c>FailureCase</c> (same pattern as other known-fail fixtures).
    /// </summary>
    [Category("FailureCase")]
    [Category("Red")]
    [Category("Curves.MetricsContract")]
    public class CurveMetricsContractTests
    {
        private readonly GeometryFactory _factory = new GeometryFactory();

        /// <summary>
        /// Unit upper semicircle: controls (1,0), (0,1), (-1,0). Radius 1, centre origin.
        /// </summary>
        private CircularString UnitSemicircle()
        {
            var seq = _factory.CoordinateSequenceFactory.Create(new[]
            {
                new Coordinate(1, 0),
                new Coordinate(0, 1),
                new Coordinate(-1, 0)
            });
            return new CircularString(seq, _factory);
        }


        /// <summary>
        /// P0 — Distance to a curve must be finite and arc-correct.
        /// Today <see cref="DistanceOp"/> fails closed with
        /// <see cref="NotSupportedException"/> (expected distance at centre is 1).
        /// </summary>
        [Test]
        public void Red_Distance_PointToCircularString_CentreOfUnitSemicircle_IsRadius()
        {
            var arc = UnitSemicircle();
            var centre = _factory.CreatePoint(new Coordinate(0, 0));

            double d = DistanceOp.Distance(centre, arc);

            Assert.That(double.IsFinite(d), Is.True,
                "Distance must not leave the MaxValue sentinel for curve inputs.");
            Assert.That(d, Is.EqualTo(1.0).Within(1e-9),
                "Expected arc-aware distance: centre of unit semicircle is at distance r = 1.");
        }

        /// <summary>
        /// P0 companion — endpoint query is zero on the true arc.
        /// </summary>
        [Test]
        public void Red_Distance_PointToCircularString_Endpoint_IsZero()
        {
            var arc = UnitSemicircle();
            var end = _factory.CreatePoint(new Coordinate(1, 0));

            double d = DistanceOp.Distance(end, arc);

            Assert.That(double.IsFinite(d), Is.True);
            Assert.That(d, Is.EqualTo(0.0).Within(1e-12));
        }

    }
}
