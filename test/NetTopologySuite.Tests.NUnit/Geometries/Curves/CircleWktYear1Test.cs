// SPDX-License-Identifier: BSD-3-Clause
// AI-drafted, human-reviewed.  Assisted-by: Cursor Grok 4.6
//
// Ticket 19 — Curves.Circle type + Year-1 WKT CIRCLE.
// ISO/IEC 13249-3 §4.2.7 / §5.1.67: CIRCLE [Z|M|ZM] (point, point, point) | EMPTY.
// WKB type 18 is Ticket 20 (not this PR). Typed members are Ticket 21.

using System;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Curves;
using NetTopologySuite.IO;
using NUnit.Framework;

namespace NetTopologySuite.Tests.NUnit.Geometries.Curves
{
    /// <summary>
    /// Year-1 <c>CIRCLE</c> type + WKT (Ticket 19).
    /// </summary>
    [Category("CurveAwareness")]
    public class CircleWktYear1Test
    {
        private readonly WKTReader _reader = new WKTReader();
        private readonly WKTWriter _writer = new WKTWriter();
        private readonly WKTWriter _writerZ = new WKTWriter(3);

        /// <summary>Unit circle through (1,0), (0,1), (-1,0).</summary>
        private const string UnitCircleWkt = "CIRCLE (1 0, 0 1, -1 0)";

        [Test]
        public void Ticket19_EmptyRoundTripWriterKeywordIsCircle()
        {
            var geometry = _reader.Read("CIRCLE EMPTY");
            Assert.That(geometry, Is.InstanceOf<Circle>());
            Assert.That(geometry.IsEmpty, Is.True);
            Assert.That(geometry.GeometryType, Is.EqualTo("Circle"));
            Assert.That(geometry.OgcGeometryType, Is.EqualTo(OgcGeometryType.Circle));
            Assert.That(geometry.AsText(), Is.EqualTo("CIRCLE EMPTY"));
            Assert.That(_reader.Read(geometry.AsText()).EqualsExact(geometry), Is.True);
        }

        [Test]
        public void Ticket19_ThreePointUnitCircleRoundTrip()
        {
            var geometry = _reader.Read(UnitCircleWkt);
            Assert.That(geometry, Is.InstanceOf<Circle>());
            var circle = (Circle)geometry;
            Assert.That(circle.IsEmpty, Is.False);
            Assert.That(circle.IsClosed, Is.True);
            Assert.That(circle.NumPoints, Is.EqualTo(3));
            Assert.That(circle.Length, Is.EqualTo(2d * Math.PI).Within(1e-12));

            string emitted = _writer.Write(circle);
            Assert.That(emitted, Is.EqualTo(UnitCircleWkt));
            Assert.That(emitted.ToUpperInvariant(), Does.Contain("CIRCLE"));
            Assert.That(emitted.ToUpperInvariant(), Does.Not.Contain("CIRCULARSTRING"));

            var again = (Circle)_reader.Read(emitted);
            Assert.That(again.EqualsExact(circle), Is.True);
        }

        [Test]
        public void Ticket19_ZOrdinateRoundTrip()
        {
            const string wkt = "CIRCLE Z (1 0 5, 0 1 5, -1 0 5)";
            var original = _reader.Read(wkt);
            Assert.That(original, Is.InstanceOf<Circle>());
            Assert.That(original.Coordinates[0].Z, Is.EqualTo(5));

            string emitted = _writerZ.Write(original);
            Assert.That(emitted.ToUpperInvariant(), Does.StartWith("CIRCLE"));
            Assert.That(emitted.ToUpperInvariant(), Does.Contain("Z"));
            Assert.That(emitted.ToUpperInvariant(), Does.Not.Contain("CIRCULARSTRING"));
            Assert.That(emitted, Does.Contain("5"));

            var again = _reader.Read(emitted);
            Assert.That(again, Is.InstanceOf<Circle>());
            Assert.That(again.EqualsExact(original), Is.True);
            Assert.That(again.Coordinates[0].Z, Is.EqualTo(5));
        }

        [Test]
        public void Ticket19_CollinearThreePointsRefusedOnRead()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                _reader.Read("CIRCLE (0 0, 1 0, 2 0)"));
            Assert.That(ex.Message, Does.Contain("collinear").IgnoreCase);
            Assert.That(ex.Message, Does.Contain("§4.2.7"));
        }

        [Test]
        public void Ticket19_CollinearThreePointsRefusedOnCtor()
        {
            var factory = GeometryFactory.Default;
            var seq = factory.CoordinateSequenceFactory.Create(new[]
            {
                new Coordinate(0, 0),
                new Coordinate(1, 0),
                new Coordinate(2, 0)
            });
            var ex = Assert.Throws<ArgumentException>(() => new Circle(seq, factory));
            Assert.That(ex.Message, Does.Contain("collinear").IgnoreCase);
        }

        [Test]
        public void Ticket19_WrongControlCountRefused()
        {
            var two = Assert.Throws<ArgumentException>(() =>
                _reader.Read("CIRCLE (0 0, 1 0)"));
            Assert.That(two.Message, Does.Contain("exactly three"));

            var four = Assert.Throws<ArgumentException>(() =>
                _reader.Read("CIRCLE (0 0, 1 0, 0 1, 1 1)"));
            Assert.That(four.Message, Does.Contain("exactly three"));
        }

        [Test]
        public void Ticket19_CircleNoLongerHitsSqlMmUnimplemented()
        {
            Assert.That(() => _reader.Read(UnitCircleWkt), Throws.Nothing);
            Assert.That(() => _reader.Read("CIRCLE EMPTY"), Throws.Nothing);
            Assert.That(() => _reader.Read("CIRCLE Z (1 0 5, 0 1 5, -1 0 5)"), Throws.Nothing);

            var leftover = Assert.Throws<ParseException>(() =>
                _reader.Read("GEODESICSTRING (0 0, 10 0, 10 10)"));
            Assert.That(leftover.Message, Does.Contain("not implemented"));
            Assert.That(leftover.Message, Does.Contain("§4.2.1"));
        }

        [Test]
        public void Ticket19_CircularStringStaysDistinctNoSilentDemote()
        {
            var cs = _reader.Read("CIRCULARSTRING (1 0, 0 1, -1 0)");
            Assert.That(cs, Is.InstanceOf<CircularString>());
            Assert.That(cs, Is.Not.InstanceOf<Circle>());
            Assert.That(cs.Length, Is.Not.EqualTo(2d * Math.PI).Within(1e-6),
                "A three-control CircularString is one arc, not the full circle.");

            var circle = _reader.Read(UnitCircleWkt);
            Assert.That(circle, Is.InstanceOf<Circle>());
            Assert.That(circle, Is.Not.InstanceOf<CircularString>());

            string csEmitted = _writer.Write(cs);
            string circleEmitted = _writer.Write(circle);
            Assert.That(csEmitted.ToUpperInvariant(), Does.StartWith("CIRCULARSTRING"));
            Assert.That(csEmitted.ToUpperInvariant(), Does.Not.Match(@"\bCIRCLE\b"));
            Assert.That(circleEmitted.ToUpperInvariant(), Does.StartWith("CIRCLE"));
            Assert.That(circleEmitted.ToUpperInvariant(), Does.Not.Contain("CIRCULARSTRING"));
            Assert.That(cs.EqualsExact(circle), Is.False);
        }

        [Test]
        public void Ticket19_MultiCurveCanCarryCircleMember()
        {
            const string wkt = "MULTICURVE (CIRCLE (1 0, 0 1, -1 0), (3 0, 4 0))";
            var original = (MultiCurve)_reader.Read(wkt);
            Assert.That(original.NumGeometries, Is.EqualTo(2));
            Assert.That(original.GetGeometryN(0), Is.InstanceOf<Circle>());
            Assert.That(original.GetGeometryN(1), Is.InstanceOf<LineString>());

            string emitted = _writer.Write(original);
            Assert.That(emitted, Is.EqualTo(wkt));
            Assert.That(emitted.ToUpperInvariant(), Does.Contain("CIRCLE"));
            Assert.That(emitted.ToUpperInvariant(), Does.Not.Contain("CIRCULARSTRING"));

            var again = (MultiCurve)_reader.Read(emitted);
            Assert.That(again.GetGeometryN(0), Is.InstanceOf<Circle>());
            Assert.That(again.EqualsExact(original), Is.True);
        }

        [Test]
        public void Ticket19_CurvePolygonCanCarryCircleRing()
        {
            const string wkt = "CURVEPOLYGON (CIRCLE (1 0, 0 1, -1 0))";
            var original = (CurvePolygon)_reader.Read(wkt);
            Assert.That(original.ExteriorRing, Is.InstanceOf<Circle>());
            Assert.That(original.ExteriorRing.IsClosed, Is.True);

            string emitted = _writer.Write(original);
            Assert.That(emitted, Is.EqualTo(wkt));
            Assert.That(emitted.ToUpperInvariant(), Does.Contain("CIRCLE"));
            Assert.That(emitted.ToUpperInvariant(), Does.Not.Contain("CIRCULARSTRING"));

            var again = (CurvePolygon)_reader.Read(emitted);
            Assert.That(again.ExteriorRing, Is.InstanceOf<Circle>());
            Assert.That(again.EqualsExact(original), Is.True);
        }

        [Test]
        public void Ticket19_MultiSurfaceCurvePolygonRingCanBeCircle()
        {
            const string wkt = "MULTISURFACE (CURVEPOLYGON (CIRCLE (1 0, 0 1, -1 0)))";
            var original = (MultiSurface)_reader.Read(wkt);
            var cp = (CurvePolygon)original.GetGeometryN(0);
            Assert.That(cp.ExteriorRing, Is.InstanceOf<Circle>());

            string emitted = _writer.Write(original);
            Assert.That(emitted, Is.EqualTo(wkt));
            Assert.That(_reader.Read(emitted).EqualsExact(original), Is.True);
        }

        [Test]
        public void Ticket19_CircleIsNotAMultiSurfaceMember()
        {
            var ex = Assert.Throws<ParseException>(() =>
                _reader.Read("MULTISURFACE (CIRCLE (1 0, 0 1, -1 0))"));
            Assert.That(ex.Message, Does.Contain("CIRCLE"));
            Assert.That(ex.Message, Does.Contain("Year-1"));
            Assert.That(ex.Message, Does.Not.Contain("not implemented"));
            Assert.That(ex.Message, Does.Not.Contain("Unknown type"));
        }

        [Test]
        public void Ticket19_WriterNeverEmitsCircularStringForCircleObject()
        {
            string[] wkts =
            {
                "CIRCLE EMPTY",
                UnitCircleWkt,
                "CIRCLE Z (1 0 5, 0 1 5, -1 0 5)",
                "MULTICURVE (CIRCLE (1 0, 0 1, -1 0))",
                "CURVEPOLYGON (CIRCLE (1 0, 0 1, -1 0))",
                "GEOMETRYCOLLECTION (CIRCLE (1 0, 0 1, -1 0))"
            };
            foreach (string wkt in wkts)
            {
                var geometry = _reader.Read(wkt);
                string emitted = _writerZ.Write(geometry);
                Assert.That(emitted.ToUpperInvariant(), Does.Contain("CIRCLE"), wkt);
                Assert.That(emitted.ToUpperInvariant(), Does.Not.Contain("CIRCULARSTRING"), wkt);
            }
        }

        [Test]
        public void Ticket19_WkbType18IsOwnedByTicket20()
        {
            // Ticket 19 pinned the ISO type number only. Ticket 20 owns
            // WKBCircle read/write (CircleWkbYear1Test); this fixture must
            // not remint WKT.
            Assert.That((int)OgcGeometryType.Circle, Is.EqualTo(18));
        }

        [Test]
        public void Ticket19_OgcGeometryTypeCircleIsWkbTypeNumber18()
        {
            Assert.That((int)OgcGeometryType.Circle, Is.EqualTo(18));
        }

        [Test]
        public void Ticket19_Year1CompoundCurveStillRefusesCircleMember()
        {
            var ex = Assert.Throws<ParseException>(() =>
                _reader.Read("MULTICURVE (COMPOUNDCURVE ((0 0, 1 0), CIRCLE (1 0, 0 1, -1 0)))"));
            Assert.That(ex.Message, Does.Contain("CIRCLE"));
            Assert.That(ex.Message, Does.Contain("CompoundCurve"));
        }
    }
}
