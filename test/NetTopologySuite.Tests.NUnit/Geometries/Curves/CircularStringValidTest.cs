// SPDX-License-Identifier: BSD-3-Clause
// AI-drafted, human-reviewed.  Assisted-by: Cursor Grok 4.6
// Port of JTS 2b56b1a4 CircularStringValidTest (V-CS / #86).

using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Curves;
using NetTopologySuite.IO;
using NUnit.Framework;

namespace NetTopologySuite.Tests.NUnit.Geometries.Curves
{
    /// <summary>
    /// V-CS / #86: <c>CIRCULARSTRING(A,B,C,A)</c> is a valid geometry.
    /// A pair of those rings is a valid annulus (CurvePolygon hole).
    /// Not H-CC area.
    /// </summary>
    public class CircularStringValidTest
    {
        private const string Ring5 = "CIRCULARSTRING (-5 0, 0 5, 5 0, -5 0)";
        private const string Ring3 = "CIRCULARSTRING (-3 0, 0 3, 3 0, -3 0)";
        private const string Annulus = "CURVEPOLYGON (" + Ring5 + ", " + Ring3 + ")";
        private const string OddClosed = "CIRCULARSTRING (-5 0, 0 5, 5 0, 0 -5, -5 0)";

        [Test]
        public void ClosedFourPointCircularStringIsValid()
        {
            var g = new WKTReader().Read(Ring5);
            Assert.That(g, Is.InstanceOf<CircularString>());
            Assert.That(g.NumPoints, Is.EqualTo(4));
            Assert.That(((CircularString)g).IsClosed, Is.True);
            Assert.That(g.IsValid, Is.True, "CIRCULARSTRING(A,B,C,A) is a valid geometry");
            Assert.That(CircularString.IsValidControlCount(
                ((CircularString)g).CoordinateSequence), Is.True);
        }

        [Test]
        public void OpenFourPointControlIsInvalid()
        {
            var factory = new GeometryFactory();
            var pts = new[]
            {
                new Coordinate(0, 0),
                new Coordinate(1, 1),
                new Coordinate(2, 0),
                new Coordinate(3, 1)
            };
            var seq = factory.CoordinateSequenceFactory.Create(pts);
            Assert.That(CircularString.IsValidControlCount(seq), Is.False);
            Assert.Throws<ArgumentException>(() => new CircularString(seq, factory));
        }

        [Test]
        public void OddClosedCircularStringStillValid()
        {
            var g = new WKTReader().Read(OddClosed);
            Assert.That(g.IsValid, Is.True);
            Assert.That(g.NumPoints, Is.EqualTo(5));
        }

        [Test]
        public void FourPointAnnulusCurvePolygonConstructs()
        {
            var g = new WKTReader().Read(Annulus);
            Assert.That(g, Is.InstanceOf<CurvePolygon>());
            var cp = (CurvePolygon)g;
            Assert.That(cp.NumInteriorRings, Is.EqualTo(1));
            Assert.That(cp.ExteriorRing, Is.InstanceOf<CircularString>());
            Assert.That(cp.GetInteriorRingN(0), Is.InstanceOf<CircularString>());
            Assert.That(cp.ExteriorRing.NumPoints, Is.EqualTo(4));
            Assert.That(cp.GetInteriorRingN(0).NumPoints, Is.EqualTo(4));
            Assert.That(((CircularString)cp.ExteriorRing).IsClosed, Is.True);
            Assert.That(((CircularString)cp.GetInteriorRingN(0)).IsClosed, Is.True);
            Assert.That(cp.ExteriorRing.IsValid, Is.True);
            Assert.That(cp.GetInteriorRingN(0).IsValid, Is.True);
        }
    }
}
