// SPDX-License-Identifier: BSD-3-Clause
//
// AI assistance disclosure:
//   AI-drafted, human-reviewed. Assisted-by: Cursor Grok 4.7
//   AI-generated portions dedicated to CC0-1.0; human curation BSD-3-Clause.

using System;
using NetTopologySuite.Algorithm.ExactCurve;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Curves;
using NetTopologySuite.IO;
using NUnit.Framework;

namespace NetTopologySuite.Tests.NUnit.Geometries.Curves
{
    /// <summary>
    /// Full-circle circular arcs, where the first and last control of a
    /// 3-point window coincide.
    /// </summary>
    /// <remarks>
    /// Reference: GEOS main <c>ae9cdd9</c>, <c>CircularArc::isCircle</c> /
    /// <c>getAngle</c> and <c>CircularArcs::getCenter</c>. <c>p0 == p2</c> is a
    /// full circle whose centre is the midpoint of <c>p0</c> and <c>p1</c>
    /// (not the circumcentre of the degenerate triple) and whose sweep is
    /// <c>2π</c>. Linearize must emit a closed ring with at least three
    /// distinct vertices. A half-circle control is included so the
    /// non-degenerate path stays covered.
    /// </remarks>
    public class FullCircleArcTest
    {
        private const double Tol = 1.0e-9;

        /// <summary>Explicit sagitta passed to <see cref="CircularString.Linearize(double)"/>.</summary>
        private const double LinearizeTolerance = 0.1;

        private readonly WKTReader _reader = new WKTReader();

        [Test]
        public void FullCircleRadius3_LengthCentreAndLinearize()
        {
            const string wkt = "CIRCULARSTRING (-3 0, 3 0, -3 0)";
            var cs = (CircularString)_reader.Read(wkt);
            var arc = ArcOf(cs);
            var lin = cs.Linearize(LinearizeTolerance);

            Assert.Multiple(() =>
            {
                Assert.That(cs.IsClosed, Is.True, wkt);
                Assert.That(cs.Length, Is.EqualTo(6.0 * Math.PI).Within(Tol), wkt + " length");
                AssertCentre(arc, 0.0, 0.0, 3.0, wkt);
                Assert.That(arc.Sweep, Is.EqualTo(2.0 * Math.PI).Within(Tol), wkt + " sweep");
                AssertClosedRing(lin, wkt + " Linearize(" + LinearizeTolerance + ")");
            });
        }

        [Test]
        public void FullCircleOffset_LengthAndCentre()
        {
            const string wkt = "CIRCULARSTRING (3 4, 7 8, 3 4)";
            var cs = (CircularString)_reader.Read(wkt);
            var arc = ArcOf(cs);
            double radius = 2.0 * Math.Sqrt(2.0);
            var lin = cs.Linearize(LinearizeTolerance);

            Assert.Multiple(() =>
            {
                Assert.That(cs.IsClosed, Is.True, wkt);
                Assert.That(cs.Length, Is.EqualTo(2.0 * Math.PI * radius).Within(Tol), wkt + " length");
                AssertCentre(arc, 5.0, 6.0, radius, wkt);
                Assert.That(arc.Sweep, Is.EqualTo(2.0 * Math.PI).Within(Tol), wkt + " sweep");
                AssertClosedRing(lin, wkt + " Linearize(" + LinearizeTolerance + ")");
            });
        }

        [Test]
        public void FullCircleCurvePolygon_AreaLengthAndLinearize()
        {
            const string wkt = "CURVEPOLYGON (CIRCULARSTRING (-3 0, 3 0, -3 0))";
            var poly = (CurvePolygon)_reader.Read(wkt);
            var shell = (CircularString)poly.ExteriorRing;
            var arc = ArcOf(shell);

            Polygon lin = null;
            Exception linearizeError = null;
            try
            {
                lin = poly.Linearize(LinearizeTolerance);
            }
            catch (Exception ex)
            {
                linearizeError = ex;
            }

            Assert.Multiple(() =>
            {
                Assert.That(shell.IsClosed, Is.True, wkt + " shell");
                Assert.That(poly.Length, Is.EqualTo(6.0 * Math.PI).Within(Tol), wkt + " length");
                Assert.That(poly.Area, Is.EqualTo(9.0 * Math.PI).Within(Tol), wkt + " area");
                AssertCentre(arc, 0.0, 0.0, 3.0, wkt);
                Assert.That(linearizeError, Is.Null,
                    wkt + " Linearize threw " + linearizeError);
                if (lin != null)
                {
                    AssertClosedRing(lin.ExteriorRing, wkt + " linearized shell");
                    Assert.That(lin.Area, Is.EqualTo(9.0 * Math.PI).Within(1.0),
                        wkt + " linearized area (sagitta " + LinearizeTolerance + ")");
                }
            });
        }

        [Test]
        public void HalfCircleControl_NonDegeneratePath()
        {
            const string wkt = "CIRCULARSTRING (-3 0, 0 3, 3 0)";
            var cs = (CircularString)_reader.Read(wkt);
            var arc = ArcOf(cs);
            var lin = cs.Linearize(LinearizeTolerance);

            Assert.Multiple(() =>
            {
                Assert.That(cs.IsClosed, Is.False, wkt);
                Assert.That(cs.Length, Is.EqualTo(3.0 * Math.PI).Within(Tol), wkt + " length");
                AssertCentre(arc, 0.0, 0.0, 3.0, wkt);
                Assert.That(arc.Sweep, Is.EqualTo(Math.PI).Within(Tol), wkt + " sweep");
                Assert.That(DistinctCount(lin.Coordinates), Is.GreaterThanOrEqualTo(3), wkt);
            });
        }

        private static ExactCircularArc ArcOf(CircularString cs)
        {
            var seq = cs.CoordinateSequence;
            return new ExactCircularArc(
                seq.GetCoordinate(0), seq.GetCoordinate(1), seq.GetCoordinate(2));
        }

        private static void AssertCentre(ExactCircularArc arc, double x, double y, double radius, string label)
        {
            Assert.That(arc.IsArc, Is.True, label + " is an arc; centre=" + Format(arc.Center));
            if (arc.Center == null)
            {
                return;
            }
            Assert.That(arc.Center.X, Is.EqualTo(x).Within(Tol), label + " centre X");
            Assert.That(arc.Center.Y, Is.EqualTo(y).Within(Tol), label + " centre Y");
            Assert.That(arc.Radius, Is.EqualTo(radius).Within(Tol), label + " radius");
        }

        private static void AssertClosedRing(Geometry ring, string label)
        {
            Assert.That(ring, Is.InstanceOf<Curve>(), label);
            if (ring is Curve curve)
            {
                Assert.That(curve.IsClosed, Is.True, label + " closed");
            }
            Assert.That(DistinctCount(ring.Coordinates), Is.GreaterThanOrEqualTo(3),
                label + " distinct vertices, WKT " + ring.AsText());
        }

        private static int DistinctCount(Coordinate[] coords)
        {
            int n = 0;
            for (int i = 0; i < coords.Length; i++)
            {
                bool seen = false;
                for (int j = 0; j < i; j++)
                {
                    if (coords[i].Equals2D(coords[j]))
                    {
                        seen = true;
                        break;
                    }
                }
                if (!seen)
                {
                    n++;
                }
            }
            return n;
        }

        private static string Format(Coordinate c) => c == null ? "null" : c.X + " " + c.Y;
    }
}
