// SPDX-License-Identifier: BSD-3-Clause
// AI-drafted, human-reviewed.  Assisted-by: Cursor Grok 4.6
//
// Ticket 1 — Year-1 ST_CurvePolygon WKT ring grammar (ISO/IEC 13249-3 §8.2).
// Rings: LineString (bare) | CircularString | CompoundCurve. Year-2 keywords
// (CIRCLE, GEODESIC, ELLIPSE, NURBS, CLOTHOID, SPIRAL) reject on read.

using System;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Curves;
using NetTopologySuite.IO;
using NUnit.Framework;

namespace NetTopologySuite.Tests.NUnit.Geometries.Curves
{
    /// <summary>
    /// Year-1 <c>CURVEPOLYGON</c> WKT ring grammar (Ticket 1 / FCP-WKT).
    /// </summary>
    [Category("CurveAwareness")]
    public class CurvePolygonWktYear1Test
    {
        private readonly WKTReader _reader = new WKTReader();
        private readonly WKTWriter _writer = new WKTWriter();
        private readonly WKTWriter _writerZ = new WKTWriter(3);

        private static readonly string[] Year2Keywords =
        {
            "CIRCLE", "GEODESIC", "ELLIPSE", "NURBS", "CLOTHOID", "SPIRAL"
        };

        [TestCase("CURVEPOLYGON EMPTY", Description = "FCP-WKT Ticket1 EMPTY")]
        [TestCase("CURVEPOLYGON ((0 0, 10 0, 10 10, 0 10, 0 0))", Description = "FCP-WKT Ticket1 one-ring bare LS")]
        [TestCase("CURVEPOLYGON ((0 0, 10 0, 10 10, 0 10, 0 0), (2 2, 8 2, 8 8, 2 8, 2 2))", Description = "FCP-WKT Ticket1 multi-ring bare LS")]
        public void Ticket1_ReadWriteEmptyOneRingAndMultiRing(string wkt)
        {
            var geometry = _reader.Read(wkt);
            Assert.That(geometry, Is.InstanceOf<CurvePolygon>());
            Assert.That(geometry.AsText(), Is.EqualTo(wkt));
            Assert.That(_reader.Read(geometry.AsText()).EqualsExact(geometry), Is.True);
        }

        [Test]
        public void Ticket1_FCP_WKT_BareLineStringRingRoundTripPreservesSubtype()
        {
            var original = (CurvePolygon)_reader.Read(
                "CURVEPOLYGON ((0 0, 10 0, 10 10, 0 10, 0 0))");
            Assert.That(original.ExteriorRing, Is.InstanceOf<LineString>());
            Assert.That(original.ExteriorRing, Is.Not.InstanceOf<CircularString>());
            Assert.That(original.ExteriorRing, Is.Not.InstanceOf<CompoundCurve>());

            string emitted = _writer.Write(original);
            Assert.That(emitted.ToUpperInvariant(), Does.Not.Contain("CIRCULARSTRING"));
            Assert.That(emitted.ToUpperInvariant(), Does.Not.Contain("COMPOUNDCURVE"));
            Assert.That(emitted.ToUpperInvariant(), Does.Contain("CURVEPOLYGON"));

            var roundTrip = (CurvePolygon)_reader.Read(emitted);
            Assert.That(roundTrip.ExteriorRing, Is.InstanceOf<LineString>());
            Assert.That(roundTrip.EqualsExact(original), Is.True);
        }

        [Test]
        public void Ticket1_FCP_WKT_TaggedCircularStringRingRoundTripPreservesSubtype()
        {
            const string wkt = "CURVEPOLYGON (CIRCULARSTRING (0 0, 2 2, 4 0, 2 -2, 0 0))";
            var original = (CurvePolygon)_reader.Read(wkt);
            Assert.That(original.ExteriorRing, Is.InstanceOf<CircularString>());

            string emitted = _writer.Write(original);
            Assert.That(emitted.ToUpperInvariant(), Does.Contain("CIRCULARSTRING"));
            Assert.That(emitted, Is.EqualTo(wkt));

            var roundTrip = (CurvePolygon)_reader.Read(emitted);
            Assert.That(roundTrip.ExteriorRing, Is.InstanceOf<CircularString>());
            Assert.That(roundTrip.EqualsExact(original), Is.True);
        }

        [Test]
        public void Ticket1_FCP_WKT_TaggedCompoundCurveRingRoundTripPreservesSubtype()
        {
            const string wkt =
                "CURVEPOLYGON (COMPOUNDCURVE (CIRCULARSTRING (0 0, 5 5, 10 0), (10 0, 0 0)))";
            var original = (CurvePolygon)_reader.Read(wkt);
            Assert.That(original.ExteriorRing, Is.InstanceOf<CompoundCurve>());
            var shell = (CompoundCurve)original.ExteriorRing;
            Assert.That(shell.Curves[0], Is.InstanceOf<CircularString>());
            Assert.That(shell.Curves[1], Is.InstanceOf<LineString>());

            string emitted = _writer.Write(original);
            Assert.That(emitted.ToUpperInvariant(), Does.Contain("COMPOUNDCURVE"));
            Assert.That(emitted, Is.EqualTo(wkt));

            var roundTrip = (CurvePolygon)_reader.Read(emitted);
            Assert.That(roundTrip.ExteriorRing, Is.InstanceOf<CompoundCurve>());
            Assert.That(roundTrip.EqualsExact(original), Is.True);
        }

        [Test]
        public void Ticket1_FCP_WKT_MixedRingsRoundTripPreservesSubtypes()
        {
            const string wkt =
                "CURVEPOLYGON ((0 0, 100 0, 100 100, 0 100, 0 0), CIRCULARSTRING (40 50, 50 60, 60 50, 50 40, 40 50))";
            var original = (CurvePolygon)_reader.Read(wkt);
            Assert.That(original.ExteriorRing, Is.InstanceOf<LineString>());
            Assert.That(original.GetInteriorRingN(0), Is.InstanceOf<CircularString>());

            string emitted = _writer.Write(original);
            Assert.That(emitted, Is.EqualTo(wkt));

            var roundTrip = (CurvePolygon)_reader.Read(emitted);
            Assert.That(roundTrip.ExteriorRing, Is.InstanceOf<LineString>());
            Assert.That(roundTrip.GetInteriorRingN(0), Is.InstanceOf<CircularString>());
            Assert.That(roundTrip.EqualsExact(original), Is.True);
        }

        [Test]
        public void Ticket1_ZOrdinatePreservedWhenWriterAskedForZ()
        {
            var original = _reader.Read(
                "CURVEPOLYGON Z (CIRCULARSTRING (0 0 5, 2 2 5, 4 0 5, 2 -2 5, 0 0 5))");
            Assert.That(original.Coordinates[0].Z, Is.EqualTo(5));

            string emitted = _writerZ.Write(original);
            Assert.That(emitted.ToUpperInvariant(), Does.Contain("Z"));
            Assert.That(emitted, Does.Contain("5"));

            var again = _reader.Read(emitted);
            Assert.That(again, Is.InstanceOf<CurvePolygon>());
            Assert.That(again.EqualsExact(original), Is.True);
            Assert.That(again.Coordinates[0].Z, Is.EqualTo(5));
        }

        [TestCase("CIRCLE", "CIRCLE (0 0, 1 0, 0 1)")]
        [TestCase("GEODESIC", "GEODESIC ((0 0, 10 0, 10 10, 0 10, 0 0))")]
        [TestCase("ELLIPSE", "ELLIPSE (0 0, 1 0, 0 1)")]
        [TestCase("NURBS", "NURBS ((0 0, 10 0, 10 10, 0 10, 0 0))")]
        [TestCase("CLOTHOID", "CLOTHOID ((0 0, 10 0, 10 10, 0 10, 0 0))")]
        [TestCase("SPIRAL", "SPIRAL ((0 0, 10 0, 10 10, 0 10, 0 0))")]
        public void Ticket1_RejectsYear2KeywordAsRing(string keyword, string ringBody)
        {
            var ex = Assert.Throws<ParseException>(() =>
                _reader.Read("CURVEPOLYGON (" + ringBody + ")"));
            Assert.That(ex.Message, Does.Contain(keyword));
            Assert.That(ex.Message, Does.Contain("Year-2"));
            Assert.That(ex.Message, Does.Contain("never silently flattened"));
        }

        [TestCase("CIRCLE Z (0 0, 1 0, 0 1)")]
        [TestCase("GEODESICM ((0 0, 1 0, 1 1, 0 0))")]
        public void Ticket1_RejectsYear2KeywordWithOrdinateSuffixAsRing(string ringBody)
        {
            Assert.Throws<ParseException>(() =>
                _reader.Read("CURVEPOLYGON (" + ringBody + ")"));
        }

        [Test]
        public void Ticket1_RejectsUnclosedBareLineStringRing()
        {
            Assert.Throws<ArgumentException>(() =>
                _reader.Read("CURVEPOLYGON ((0 0, 10 0, 10 10, 0 10))"));
        }

        [Test]
        public void Ticket1_RejectsUnclosedCircularStringRing()
        {
            Assert.Throws<ArgumentException>(() =>
                _reader.Read("CURVEPOLYGON (CIRCULARSTRING (0 0, 2 2, 4 0))"));
        }

        [Test]
        public void Ticket1_RejectsUnclosedCompoundCurveRing()
        {
            Assert.Throws<ArgumentException>(() =>
                _reader.Read("CURVEPOLYGON (COMPOUNDCURVE ((0 0, 1 0), CIRCULARSTRING (1 0, 2 1, 3 0)))"));
        }

        [Test]
        public void Ticket1_RejectsEvenCountCircularString()
        {
            Assert.Throws<ArgumentException>(() =>
                _reader.Read("CURVEPOLYGON (CIRCULARSTRING (0 0, 1 1))"));
        }

        [Test]
        public void Ticket1_RejectsFourControlFakeCircle()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                _reader.Read("CURVEPOLYGON (CIRCULARSTRING (0 0, 1 1, 2 0, 0 0))"));
            Assert.That(ex.Message, Does.Contain("4-control"));
            Assert.That(ex.Message, Does.Contain("not a circle"));
        }

        [Test]
        public void Ticket1_CircularStringABAIsDegenerateWindowNotACircle()
        {
            // 3-token first=last is a degenerate window, not ST_Circle.
            var cp = (CurvePolygon)_reader.Read("CURVEPOLYGON (CIRCULARSTRING (0 0, 1 1, 0 0))");
            Assert.That(cp.ExteriorRing, Is.InstanceOf<CircularString>());
            Assert.That(cp.ExteriorRing.NumPoints, Is.EqualTo(3));
            string emitted = _writer.Write(cp);
            Assert.That(emitted.ToUpperInvariant(), Does.Contain("CIRCULARSTRING"));
            AssertWriterHasNoYear2Keyword(emitted);
        }

        [Test]
        public void Ticket1_FullCircleIsFiveTokenFirstEqualsLast()
        {
            var cp = (CurvePolygon)_reader.Read(
                "CURVEPOLYGON (CIRCULARSTRING (0 0, 2 2, 4 0, 2 -2, 0 0))");
            Assert.That(cp.ExteriorRing.NumPoints, Is.EqualTo(5));
            Assert.That(cp.ExteriorRing.IsClosed, Is.True);
            Assert.That(_reader.Read(cp.AsText()).EqualsExact(cp), Is.True);
        }

        [Test]
        public void Ticket1_RejectsNestedCompoundCurveRing()
        {
            var ex = Assert.Throws<ParseException>(() =>
                _reader.Read(
                    "CURVEPOLYGON (COMPOUNDCURVE ((0 0, 1 0), COMPOUNDCURVE ((1 0, 1 1)), (1 1, 0 0)))"));
            Assert.That(ex.Message, Does.Contain("Nested COMPOUNDCURVE"));
        }

        [Test]
        public void Ticket1_RejectsNonContiguousCompoundCurveRing()
        {
            Assert.Throws<ArgumentException>(() =>
                _reader.Read(
                    "CURVEPOLYGON (COMPOUNDCURVE ((0 0, 1 0), (2 0, 3 0, 0 0)))"));
        }

        [Test]
        public void Ticket1_WriterNeverEmitsYear2KeywordForYear1Object()
        {
            string[] year1 =
            {
                "CURVEPOLYGON EMPTY",
                "CURVEPOLYGON ((0 0, 10 0, 10 10, 0 10, 0 0))",
                "CURVEPOLYGON (CIRCULARSTRING (0 0, 2 2, 4 0, 2 -2, 0 0))",
                "CURVEPOLYGON (COMPOUNDCURVE (CIRCULARSTRING (0 0, 5 5, 10 0), (10 0, 0 0)))",
                "CURVEPOLYGON ((0 0, 100 0, 100 100, 0 100, 0 0), CIRCULARSTRING (40 50, 50 60, 60 50, 50 40, 40 50))",
                "CURVEPOLYGON Z (CIRCULARSTRING (0 0 5, 2 2 5, 4 0 5, 2 -2 5, 0 0 5))"
            };

            foreach (string wkt in year1)
            {
                var geometry = _reader.Read(wkt);
                string emitted = _writerZ.Write(geometry);
                Assert.That(emitted.ToUpperInvariant(), Does.StartWith("CURVEPOLYGON"));
                AssertWriterHasNoYear2Keyword(emitted);
            }
        }

        /// <summary>
        /// Year-2 type words as whole tokens. Substring checks are unsafe:
        /// <c>CIRCULARSTRING</c> contains <c>CIRCLE</c>.
        /// </summary>
        private static void AssertWriterHasNoYear2Keyword(string wkt)
        {
            string upper = wkt.ToUpperInvariant();
            foreach (string keyword in Year2Keywords)
            {
                Assert.That(upper, Does.Not.Match(@"\b" + keyword + @"\b"),
                    "Year-1 writer must not emit " + keyword + " for " + wkt);
            }
        }
    }
}
