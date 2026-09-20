// SPDX-License-Identifier: BSD-3-Clause
// AI-drafted, human-reviewed.  Assisted-by: Cursor Grok 4.6
//
// Ticket 4 — Year-1 ST_MultiCurve WKT member grammar (ISO/IEC 13249-3).
// Members: LineString (bare) | CircularString | CompoundCurve. Reuses Ticket 1
// g4 curveMember / ReadCurveText. Year-2 keywords and MULTICIRCULARSTRING /
// MULTICOMPOUNDCURVE reject on read. WKB type 11 is Ticket 5.

using System;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Curves;
using NetTopologySuite.IO;
using NUnit.Framework;

namespace NetTopologySuite.Tests.NUnit.Geometries.Curves
{
    /// <summary>
    /// Year-1 <c>MULTICURVE</c> WKT member grammar (Ticket 4).
    /// </summary>
    [Category("CurveAwareness")]
    public class MultiCurveWktYear1Test
    {
        private readonly WKTReader _reader = new WKTReader();
        private readonly WKTWriter _writer = new WKTWriter();
        private readonly WKTWriter _writerZ = new WKTWriter(3);

        private static readonly string[] Year2Keywords =
        {
            "CIRCLE", "GEODESIC", "ELLIPSE", "NURBS", "CLOTHOID", "SPIRAL"
        };

        [TestCase("MULTICURVE EMPTY", Description = "Ticket4 EMPTY")]
        [TestCase("MULTICURVE ((0 0, 1 0))", Description = "Ticket4 one-member bare LS")]
        [TestCase("MULTICURVE (CIRCULARSTRING (0 0, 1 1, 2 0))", Description = "Ticket4 one-member CS")]
        [TestCase("MULTICURVE (COMPOUNDCURVE (CIRCULARSTRING (0 0, 1 1, 2 0), (2 0, 3 0)))", Description = "Ticket4 one-member CC")]
        public void Ticket4_ReadWriteEmptyAndOneMember(string wkt)
        {
            var geometry = _reader.Read(wkt);
            Assert.That(geometry, Is.InstanceOf<MultiCurve>());
            Assert.That(geometry.AsText(), Is.EqualTo(wkt));
            Assert.That(_reader.Read(geometry.AsText()).EqualsExact(geometry), Is.True);
        }

        [Test]
        public void Ticket4_BareLineStringMemberRoundTripPreservesSubtype()
        {
            var original = (MultiCurve)_reader.Read("MULTICURVE ((0 0, 1 0, 2 0))");
            Assert.That(original.GetGeometryN(0), Is.InstanceOf<LineString>());
            Assert.That(original.GetGeometryN(0), Is.Not.InstanceOf<CircularString>());
            Assert.That(original.GetGeometryN(0), Is.Not.InstanceOf<CompoundCurve>());

            string emitted = _writer.Write(original);
            Assert.That(emitted.ToUpperInvariant(), Does.Not.Contain("LINESTRING"));
            Assert.That(emitted.ToUpperInvariant(), Does.Not.Contain("CIRCULARSTRING"));
            Assert.That(emitted.ToUpperInvariant(), Does.Not.Contain("COMPOUNDCURVE"));
            Assert.That(emitted.ToUpperInvariant(), Does.Contain("MULTICURVE"));

            var roundTrip = (MultiCurve)_reader.Read(emitted);
            Assert.That(roundTrip.GetGeometryN(0), Is.InstanceOf<LineString>());
            Assert.That(roundTrip.EqualsExact(original), Is.True);
        }

        [Test]
        public void Ticket4_TaggedCircularStringMemberRoundTripPreservesSubtype()
        {
            const string wkt = "MULTICURVE (CIRCULARSTRING (0 0, 1 1, 2 0))";
            var original = (MultiCurve)_reader.Read(wkt);
            Assert.That(original.GetGeometryN(0), Is.InstanceOf<CircularString>());

            string emitted = _writer.Write(original);
            Assert.That(emitted.ToUpperInvariant(), Does.Contain("CIRCULARSTRING"));
            Assert.That(emitted, Is.EqualTo(wkt));

            var roundTrip = (MultiCurve)_reader.Read(emitted);
            Assert.That(roundTrip.GetGeometryN(0), Is.InstanceOf<CircularString>());
            Assert.That(roundTrip.EqualsExact(original), Is.True);
        }

        [Test]
        public void Ticket4_TaggedCompoundCurveMemberRoundTripPreservesSubtype()
        {
            const string wkt =
                "MULTICURVE (COMPOUNDCURVE (CIRCULARSTRING (0 0, 1 1, 2 0), (2 0, 3 0)))";
            var original = (MultiCurve)_reader.Read(wkt);
            Assert.That(original.GetGeometryN(0), Is.InstanceOf<CompoundCurve>());
            var compound = (CompoundCurve)original.GetGeometryN(0);
            Assert.That(compound.Curves[0], Is.InstanceOf<CircularString>());
            Assert.That(compound.Curves[1], Is.InstanceOf<LineString>());

            string emitted = _writer.Write(original);
            Assert.That(emitted.ToUpperInvariant(), Does.Contain("COMPOUNDCURVE"));
            Assert.That(emitted, Is.EqualTo(wkt));

            var roundTrip = (MultiCurve)_reader.Read(emitted);
            Assert.That(roundTrip.GetGeometryN(0), Is.InstanceOf<CompoundCurve>());
            Assert.That(roundTrip.EqualsExact(original), Is.True);
        }

        [Test]
        public void Ticket4_MixedLsCsCcRoundTripPreservesSubtypes()
        {
            const string wkt =
                "MULTICURVE ((0 0, 1 0), CIRCULARSTRING (1 0, 2 1, 3 0), COMPOUNDCURVE ((3 0, 4 0), CIRCULARSTRING (4 0, 5 1, 6 0)))";
            var original = (MultiCurve)_reader.Read(wkt);
            Assert.That(original.NumGeometries, Is.EqualTo(3));
            Assert.That(original.GetGeometryN(0), Is.InstanceOf<LineString>());
            Assert.That(original.GetGeometryN(1), Is.InstanceOf<CircularString>());
            Assert.That(original.GetGeometryN(2), Is.InstanceOf<CompoundCurve>());

            string emitted = _writer.Write(original);
            Assert.That(emitted, Is.EqualTo(wkt));

            var roundTrip = (MultiCurve)_reader.Read(emitted);
            Assert.That(roundTrip.GetGeometryN(0), Is.InstanceOf<LineString>());
            Assert.That(roundTrip.GetGeometryN(1), Is.InstanceOf<CircularString>());
            Assert.That(roundTrip.GetGeometryN(2), Is.InstanceOf<CompoundCurve>());
            Assert.That(roundTrip.EqualsExact(original), Is.True);
        }

        [Test]
        public void Ticket4_TaggedLineStringAcceptedOnReadWriterEmitsBareBody()
        {
            // Same CompoundCurve tagging deviation as Ticket 1 / 615-i:
            // tagged LINESTRING is accepted for interop; writer stays conformant.
            var original = (MultiCurve)_reader.Read(
                "MULTICURVE (LINESTRING (0 0, 1 0), CIRCULARSTRING (1 0, 2 1, 3 0))");
            Assert.That(original.GetGeometryN(0), Is.InstanceOf<LineString>());
            Assert.That(original.GetGeometryN(1), Is.InstanceOf<CircularString>());

            string emitted = _writer.Write(original);
            Assert.That(emitted, Is.EqualTo(
                "MULTICURVE ((0 0, 1 0), CIRCULARSTRING (1 0, 2 1, 3 0))"));
            Assert.That(emitted.ToUpperInvariant(), Does.Not.Contain("LINESTRING"));
            Assert.That(_reader.Read(emitted).EqualsExact(original), Is.True);
        }

        [Test]
        public void Ticket4_ZOrdinatePreservedWhenWriterAskedForZ()
        {
            var original = _reader.Read(
                "MULTICURVE Z (CIRCULARSTRING (0 0 5, 1 1 5, 2 0 5), (3 0 5, 4 0 5))");
            Assert.That(original.Coordinates[0].Z, Is.EqualTo(5));

            string emitted = _writerZ.Write(original);
            Assert.That(emitted.ToUpperInvariant(), Does.Contain("Z"));
            Assert.That(emitted, Does.Contain("5"));

            var again = _reader.Read(emitted);
            Assert.That(again, Is.InstanceOf<MultiCurve>());
            Assert.That(again.EqualsExact(original), Is.True);
            Assert.That(again.Coordinates[0].Z, Is.EqualTo(5));
            Assert.That(again.Coordinates[again.NumPoints - 1].Z, Is.EqualTo(5));
        }

        [TestCase("CIRCLE", "CIRCLE (0 0, 1 0, 0 1)")]
        [TestCase("GEODESIC", "GEODESIC ((0 0, 10 0, 10 10))")]
        [TestCase("ELLIPSE", "ELLIPSE (0 0, 1 0, 0 1)")]
        [TestCase("NURBS", "NURBS ((0 0, 10 0, 10 10))")]
        [TestCase("CLOTHOID", "CLOTHOID ((0 0, 10 0, 10 10))")]
        [TestCase("SPIRAL", "SPIRAL ((0 0, 10 0, 10 10))")]
        public void Ticket4_RejectsYear2KeywordAsMember(string keyword, string memberBody)
        {
            var ex = Assert.Throws<ParseException>(() =>
                _reader.Read("MULTICURVE (" + memberBody + ")"));
            Assert.That(ex.Message, Does.Contain(keyword));
            Assert.That(ex.Message, Does.Contain("Year-2"));
            Assert.That(ex.Message, Does.Contain("never silently flattened"));
        }

        [TestCase("CIRCLE Z (0 0, 1 0, 0 1)")]
        [TestCase("GEODESICM ((0 0, 1 0, 1 1))")]
        public void Ticket4_RejectsYear2KeywordWithOrdinateSuffixAsMember(string memberBody)
        {
            Assert.Throws<ParseException>(() =>
                _reader.Read("MULTICURVE (" + memberBody + ")"));
        }

        [TestCase("MULTICIRCULARSTRING ((0 0, 1 1, 2 0))")]
        [TestCase("MULTICOMPOUNDCURVE (((0 0, 1 0)))")]
        [TestCase("MULTICIRCULARSTRING EMPTY")]
        [TestCase("MULTICOMPOUNDCURVE EMPTY")]
        public void Ticket4_RejectsWrongCollectionKeyword(string wkt)
        {
            var ex = Assert.Throws<ParseException>(() => _reader.Read(wkt));
            Assert.That(ex.Message, Does.Contain("MULTICIRCULARSTRING").Or.Contain("MULTICOMPOUNDCURVE"));
            Assert.That(ex.Message, Does.Contain("rejected"));
        }

        [Test]
        public void Ticket4_RejectsWrongCollectionKeywordAsMember()
        {
            var ex = Assert.Throws<ParseException>(() =>
                _reader.Read("MULTICURVE (MULTICIRCULARSTRING ((0 0, 1 1, 2 0)))"));
            Assert.That(ex.Message, Does.Contain("MULTICIRCULARSTRING"));
        }

        [Test]
        public void Ticket4_RejectsEvenCountCircularString()
        {
            Assert.Throws<ArgumentException>(() =>
                _reader.Read("MULTICURVE (CIRCULARSTRING (0 0, 1 1))"));
        }

        [Test]
        public void Ticket4_RejectsFourControlFakeCircle()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                _reader.Read("MULTICURVE (CIRCULARSTRING (0 0, 1 1, 2 0, 0 0))"));
            Assert.That(ex.Message, Does.Contain("4-control"));
            Assert.That(ex.Message, Does.Contain("not a circle"));
        }

        [Test]
        public void Ticket4_CircularStringABAIsDegenerateWindowNotACircle()
        {
            // 3-token first=last is a degenerate window, not ST_Circle.
            var mc = (MultiCurve)_reader.Read("MULTICURVE (CIRCULARSTRING (0 0, 1 1, 0 0))");
            Assert.That(mc.GetGeometryN(0), Is.InstanceOf<CircularString>());
            Assert.That(mc.GetGeometryN(0).NumPoints, Is.EqualTo(3));
            string emitted = _writer.Write(mc);
            Assert.That(emitted.ToUpperInvariant(), Does.Contain("CIRCULARSTRING"));
            AssertWriterHasNoYear2Keyword(emitted);
        }

        [Test]
        public void Ticket4_RejectsNestedCompoundCurveMember()
        {
            var ex = Assert.Throws<ParseException>(() =>
                _reader.Read(
                    "MULTICURVE (COMPOUNDCURVE ((0 0, 1 0), COMPOUNDCURVE ((1 0, 2 0))))"));
            Assert.That(ex.Message, Does.Contain("Nested COMPOUNDCURVE"));
        }

        [Test]
        public void Ticket4_WriterNeverEmitsYear2KeywordForYear1Object()
        {
            string[] year1 =
            {
                "MULTICURVE EMPTY",
                "MULTICURVE ((0 0, 1 0))",
                "MULTICURVE (CIRCULARSTRING (0 0, 1 1, 2 0))",
                "MULTICURVE (COMPOUNDCURVE (CIRCULARSTRING (0 0, 1 1, 2 0), (2 0, 3 0)))",
                "MULTICURVE ((0 0, 1 0), CIRCULARSTRING (1 0, 2 1, 3 0), COMPOUNDCURVE ((3 0, 4 0), CIRCULARSTRING (4 0, 5 1, 6 0)))",
                "MULTICURVE Z (CIRCULARSTRING (0 0 5, 1 1 5, 2 0 5), (3 0 5, 4 0 5))"
            };

            foreach (string wkt in year1)
            {
                var geometry = _reader.Read(wkt);
                string emitted = _writerZ.Write(geometry);
                Assert.That(emitted.ToUpperInvariant(), Does.StartWith("MULTICURVE"));
                AssertWriterHasNoYear2Keyword(emitted);
            }
        }

        [Test]
        public void Ticket4_GeometryCollectionOfCurvesStaysGeometryCollectionOnWrite()
        {
            const string wkt =
                "GEOMETRYCOLLECTION (CIRCULARSTRING (0 0, 1 1, 2 0), LINESTRING (3 0, 4 0))";
            var original = _reader.Read(wkt);
            Assert.That(original, Is.InstanceOf<GeometryCollection>());
            Assert.That(original, Is.Not.InstanceOf<MultiCurve>());

            string emitted = _writer.Write(original);
            Assert.That(emitted.ToUpperInvariant(), Does.StartWith("GEOMETRYCOLLECTION"));
            Assert.That(emitted.ToUpperInvariant(), Does.Not.Contain("MULTICURVE"));

            var again = _reader.Read(emitted);
            Assert.That(again, Is.InstanceOf<GeometryCollection>());
            Assert.That(again, Is.Not.InstanceOf<MultiCurve>());
            Assert.That(again.EqualsExact(original), Is.True);
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
