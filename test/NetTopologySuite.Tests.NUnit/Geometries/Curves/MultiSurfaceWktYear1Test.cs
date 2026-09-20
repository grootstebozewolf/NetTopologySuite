// SPDX-License-Identifier: BSD-3-Clause
// AI-drafted, human-reviewed.  Assisted-by: Cursor Grok 4.6
//
// Ticket 7 — NTS Year-1 ST_MultiSurface WKT member grammar.
// Members: Polygon | CurvePolygon (surfaceMember = polygonText | curvePolygonGeometry).
// CurvePolygon rings reuse Ticket 1 ReadCurvePolygonText (LS|CS|CC). The six
// ISO/IEC 13249-3 §5.1.67 curve names NTS has no carrier for (CIRCLE,
// GEODESICSTRING, ELLIPTICALCURVE, NURBSCURVE, CLOTHOID, SPIRALCURVE) are
// named and refused via IsUnimplementedSqlMmCurve. TRIANGLE / TIN /
// POLYHEDRALSURFACE / COMPOUNDSURFACE are not Year-1 members. Ticket 9:
// Year-1 WKB 12 is complete (no longer "partial"); typed members stay in
// MultiSurfaceMembersYear1Test. Does not remint WKT.

using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Curves;
using NetTopologySuite.IO;
using NUnit.Framework;

namespace NetTopologySuite.Tests.NUnit.Geometries.Curves
{
    /// <summary>
    /// Year-1 <c>MULTISURFACE</c> WKT member grammar (Ticket 7).
    /// </summary>
    [Category("CurveAwareness")]
    public class MultiSurfaceWktYear1Test
    {
        private readonly WKTReader _reader = new WKTReader();
        private readonly WKTWriter _writer = new WKTWriter();
        private readonly WKTWriter _writerZ = new WKTWriter(3);

        /// <summary>
        /// The ISO/IEC 13249-3 §5.1.67 keywords for the instantiable §4.2.1 curve
        /// types NTS has no carrier for. These spellings, not shortened ones:
        /// the standard has no GEODESIC, ELLIPSE, NURBS or SPIRAL keyword.
        /// </summary>
        private static readonly string[] UnimplementedSqlMmCurveKeywords =
        {
            "GEODESICSTRING", "ELLIPTICALCURVE", "NURBSCURVE", "CLOTHOID", "SPIRALCURVE"
        };

        /// <summary>
        /// Surface types omitted from NTS Year-1 <c>MULTISURFACE</c> membership.
        /// </summary>
        private static readonly string[] OmittedSurfaceMemberKeywords =
        {
            "TRIANGLE", "TIN", "POLYHEDRALSURFACE", "COMPOUNDSURFACE"
        };

        [TestCase("MULTISURFACE EMPTY", Description = "Ticket7 EMPTY")]
        [TestCase("MULTISURFACE (POLYGON ((0 0, 10 0, 10 10, 0 10, 0 0)))", Description = "Ticket7 Polygon-only")]
        [TestCase("MULTISURFACE (CURVEPOLYGON (CIRCULARSTRING (0 0, 2 2, 4 0, 2 -2, 0 0)))", Description = "Ticket7 CurvePolygon-only")]
        public void Ticket7_ReadWriteEmptyAndOneMember(string wkt)
        {
            var geometry = _reader.Read(wkt);
            Assert.That(geometry, Is.InstanceOf<MultiSurface>());
            Assert.That(geometry.AsText(), Is.EqualTo(wkt));
            Assert.That(_reader.Read(geometry.AsText()).EqualsExact(geometry), Is.True);
        }

        [Test]
        public void Ticket7_PolygonOnlyRoundTripPreservesSubtype()
        {
            const string wkt = "MULTISURFACE (POLYGON ((0 0, 10 0, 10 10, 0 10, 0 0)))";
            var original = (MultiSurface)_reader.Read(wkt);
            Assert.That(original.GetGeometryN(0), Is.InstanceOf<Polygon>());
            Assert.That(original.GetGeometryN(0), Is.Not.InstanceOf<CurvePolygon>());

            string emitted = _writer.Write(original);
            Assert.That(emitted.ToUpperInvariant(), Does.Contain("POLYGON"));
            Assert.That(emitted.ToUpperInvariant(), Does.Not.Contain("CURVEPOLYGON"));
            Assert.That(emitted, Is.EqualTo(wkt));

            var roundTrip = (MultiSurface)_reader.Read(emitted);
            Assert.That(roundTrip.GetGeometryN(0), Is.InstanceOf<Polygon>());
            Assert.That(roundTrip.EqualsExact(original), Is.True);
        }

        [Test]
        public void Ticket7_CurvePolygonOnlyRoundTripPreservesSubtype()
        {
            const string wkt =
                "MULTISURFACE (CURVEPOLYGON (CIRCULARSTRING (0 0, 2 2, 4 0, 2 -2, 0 0)))";
            var original = (MultiSurface)_reader.Read(wkt);
            Assert.That(original.GetGeometryN(0), Is.InstanceOf<CurvePolygon>());
            Assert.That(((CurvePolygon)original.GetGeometryN(0)).ExteriorRing, Is.InstanceOf<CircularString>());

            string emitted = _writer.Write(original);
            Assert.That(emitted.ToUpperInvariant(), Does.Contain("CURVEPOLYGON"));
            Assert.That(emitted, Is.EqualTo(wkt));

            var roundTrip = (MultiSurface)_reader.Read(emitted);
            Assert.That(roundTrip.GetGeometryN(0), Is.InstanceOf<CurvePolygon>());
            Assert.That(roundTrip.EqualsExact(original), Is.True);
        }

        [Test]
        public void Ticket7_MixedPolygonAndCurvePolygonRoundTripPreservesSubtypes()
        {
            const string wkt =
                "MULTISURFACE (CURVEPOLYGON (CIRCULARSTRING (0 0, 2 2, 4 0, 2 -2, 0 0)), POLYGON ((10 10, 20 10, 20 20, 10 20, 10 10)))";
            var original = (MultiSurface)_reader.Read(wkt);
            Assert.That(original.NumGeometries, Is.EqualTo(2));
            Assert.That(original.GetGeometryN(0), Is.InstanceOf<CurvePolygon>());
            Assert.That(original.GetGeometryN(1), Is.InstanceOf<Polygon>());

            string emitted = _writer.Write(original);
            Assert.That(emitted, Is.EqualTo(wkt));

            var roundTrip = (MultiSurface)_reader.Read(emitted);
            Assert.That(roundTrip.GetGeometryN(0), Is.InstanceOf<CurvePolygon>());
            Assert.That(roundTrip.GetGeometryN(1), Is.InstanceOf<Polygon>());
            Assert.That(roundTrip.EqualsExact(original), Is.True);
        }

        [Test]
        public void Ticket7_CurvePolygonMemberObeysYear1RingGrammar()
        {
            const string wkt =
                "MULTISURFACE (CURVEPOLYGON ((0 0, 100 0, 100 100, 0 100, 0 0), CIRCULARSTRING (40 50, 50 60, 60 50, 50 40, 40 50), COMPOUNDCURVE (CIRCULARSTRING (10 10, 15 15, 20 10), (20 10, 10 10))))";
            var original = (MultiSurface)_reader.Read(wkt);
            var cp = (CurvePolygon)original.GetGeometryN(0);
            Assert.That(cp.ExteriorRing, Is.InstanceOf<LineString>());
            Assert.That(cp.GetInteriorRingN(0), Is.InstanceOf<CircularString>());
            Assert.That(cp.GetInteriorRingN(1), Is.InstanceOf<CompoundCurve>());

            string emitted = _writer.Write(original);
            Assert.That(emitted, Is.EqualTo(wkt));
            Assert.That(_reader.Read(emitted).EqualsExact(original), Is.True);
        }

        [Test]
        public void Ticket7_TaggedLineStringRingAcceptedOnReadWriterEmitsBareBody()
        {
            // Same 615-i deviation as Ticket 1: tagged LINESTRING is accepted
            // as a CurvePolygon ring for interop; writer stays conformant.
            var original = (MultiSurface)_reader.Read(
                "MULTISURFACE (CURVEPOLYGON (LINESTRING (0 0, 10 0, 10 10, 0 10, 0 0)))");
            Assert.That(original.GetGeometryN(0), Is.InstanceOf<CurvePolygon>());
            Assert.That(((CurvePolygon)original.GetGeometryN(0)).ExteriorRing, Is.InstanceOf<LineString>());

            string emitted = _writer.Write(original);
            Assert.That(emitted, Is.EqualTo(
                "MULTISURFACE (CURVEPOLYGON ((0 0, 10 0, 10 10, 0 10, 0 0)))"));
            Assert.That(emitted.ToUpperInvariant(), Does.Not.Contain("LINESTRING"));
            Assert.That(_reader.Read(emitted).EqualsExact(original), Is.True);
        }

        [Test]
        public void Ticket7_BarePolygonTextAcceptedOnReadWriterEmitsTaggedPolygon()
        {
            var original = (MultiSurface)_reader.Read(
                "MULTISURFACE (((0 0, 10 0, 10 10, 0 10, 0 0)))");
            Assert.That(original.GetGeometryN(0), Is.InstanceOf<Polygon>());

            string emitted = _writer.Write(original);
            Assert.That(emitted, Is.EqualTo(
                "MULTISURFACE (POLYGON ((0 0, 10 0, 10 10, 0 10, 0 0)))"));
            Assert.That(_reader.Read(emitted).EqualsExact(original), Is.True);
        }

        [Test]
        public void Ticket7_ZOrdinatePreservedWhenWriterAskedForZ()
        {
            var original = _reader.Read(
                "MULTISURFACE Z (POLYGON ((0 0 5, 10 0 5, 10 10 5, 0 10 5, 0 0 5)), CURVEPOLYGON (CIRCULARSTRING (20 0 5, 22 2 5, 24 0 5, 22 -2 5, 20 0 5)))");
            Assert.That(original.Coordinates[0].Z, Is.EqualTo(5));

            string emitted = _writerZ.Write(original);
            Assert.That(emitted.ToUpperInvariant(), Does.Contain("Z"));
            Assert.That(emitted, Does.Contain("5"));

            var again = _reader.Read(emitted);
            Assert.That(again, Is.InstanceOf<MultiSurface>());
            Assert.That(again.EqualsExact(original), Is.True);
            Assert.That(again.Coordinates[0].Z, Is.EqualTo(5));
            Assert.That(again.Coordinates[again.NumPoints - 1].Z, Is.EqualTo(5));
        }

        [TestCase("GEODESICSTRING", "GEODESICSTRING (0 0, 10 0, 10 10)")]
        [TestCase("ELLIPTICALCURVE", "ELLIPTICALCURVE (0 0, 1, 1, 0, 90)")]
        [TestCase("NURBSCURVE", "NURBSCURVE ((0 0, 10 0, 10 10))")]
        [TestCase("CLOTHOID", "CLOTHOID ((0 0, 10 0, 10 10))")]
        [TestCase("SPIRALCURVE", "SPIRALCURVE ((0 0, 10 0, 10 10))")]
        public void Ticket7_RejectsUnimplementedSqlMmCurveAsMember(string keyword, string memberBody)
        {
            var ex = Assert.Throws<ParseException>(() =>
                _reader.Read("MULTISURFACE (" + memberBody + ")"));
            Assert.That(ex.Message, Does.Contain(keyword));
            Assert.That(ex.Message, Does.Contain("§4.2.1"));
            Assert.That(ex.Message, Does.Contain("not implemented"));
            Assert.That(ex.Message, Does.Not.Contain("Unknown type"));
        }

        [TestCase("GEODESICSTRINGM (0 0, 1 0, 1 1)")]
        [TestCase("SPIRALCURVEZM ((0 0, 1 0, 1 1))")]
        public void Ticket7_RejectsUnimplementedSqlMmCurveWithOrdinateSuffixAsMember(string memberBody)
        {
            var ex = Assert.Throws<ParseException>(() =>
                _reader.Read("MULTISURFACE (" + memberBody + ")"));
            Assert.That(ex.Message, Does.Contain("not implemented"));
        }

        [TestCase("GEODESICSTRING", "GEODESICSTRING (0 0, 10 0, 10 10)")]
        [TestCase("ELLIPTICALCURVE", "ELLIPTICALCURVE (0 0, 1, 1, 0, 90)")]
        [TestCase("NURBSCURVE", "NURBSCURVE ((0 0, 10 0, 10 10, 0 10, 0 0))")]
        [TestCase("CLOTHOID", "CLOTHOID ((0 0, 10 0, 10 10, 0 10, 0 0))")]
        [TestCase("SPIRALCURVE", "SPIRALCURVE ((0 0, 10 0, 10 10, 0 10, 0 0))")]
        public void Ticket7_RejectsUnimplementedSqlMmCurveAsCurvePolygonRing(string keyword, string ringBody)
        {
            var ex = Assert.Throws<ParseException>(() =>
                _reader.Read("MULTISURFACE (CURVEPOLYGON (" + ringBody + "))"));
            Assert.That(ex.Message, Does.Contain(keyword));
            Assert.That(ex.Message, Does.Contain("not implemented"));
        }

        /// <summary>
        /// The standard has no GEODESIC / ELLIPSE / NURBS / SPIRAL keyword
        /// (ISO/IEC 13249-3 §5.1.67). They are ordinary unknown words, and must
        /// not be refused as though they named an ISO type.
        /// </summary>
        [TestCase("GEODESIC ((0 0, 1 0, 1 1, 0 0))")]
        [TestCase("ELLIPSE (0 0, 1 0, 0 1)")]
        [TestCase("NURBS ((0 0, 1 0, 1 1, 0 0))")]
        [TestCase("SPIRAL ((0 0, 1 0, 1 1, 0 0))")]
        public void Ticket7_ShortenedSpellingsAreNotIsoTypeNames(string memberBody)
        {
            var ex = Assert.Throws<ParseException>(() =>
                _reader.Read("MULTISURFACE (" + memberBody + ")"));
            Assert.That(ex.Message, Does.Not.Contain("not implemented"));
            Assert.That(ex.Message, Does.Not.Contain("ISO forbids"));
        }

        [TestCase("TRIANGLE", "TRIANGLE ((0 0, 1 0, 0 1, 0 0))")]
        [TestCase("TIN", "TIN (((0 0, 1 0, 0 1, 0 0)))")]
        [TestCase("POLYHEDRALSURFACE", "POLYHEDRALSURFACE (((0 0, 1 0, 1 1, 0 1, 0 0)))")]
        [TestCase("COMPOUNDSURFACE", "COMPOUNDSURFACE (POLYGON ((0 0, 1 0, 1 1, 0 1, 0 0)))")]
        public void Ticket7_RejectsOmittedSurfaceAsMember(string keyword, string memberBody)
        {
            var ex = Assert.Throws<ParseException>(() =>
                _reader.Read("MULTISURFACE (" + memberBody + ")"));
            Assert.That(ex.Message, Does.Contain(keyword));
            Assert.That(ex.Message, Does.Contain("Year-1"));
            Assert.That(ex.Message, Does.Contain("Polygon"));
            Assert.That(ex.Message, Does.Not.Contain("ISO forbids"));
        }

        [Test]
        public void Ticket7_RejectsNestedCompoundCurveOnWktRead()
        {
            var ex = Assert.Throws<ParseException>(() =>
                _reader.Read(
                    "MULTISURFACE (CURVEPOLYGON (COMPOUNDCURVE ((0 0, 1 0), COMPOUNDCURVE ((1 0, 1 1)), (1 1, 0 0))))"));
            Assert.That(ex.Message, Does.Contain("Nested COMPOUNDCURVE"));
            Assert.That(ex.Message, Does.Contain("Year-1"));
            Assert.That(ex.Message, Does.Not.Contain("ISO forbids"));
        }

        [Test]
        public void Ticket7_WriterNeverEmitsOmittedKeywordForYear1Object()
        {
            string[] year1 =
            {
                "MULTISURFACE EMPTY",
                "MULTISURFACE (POLYGON ((0 0, 10 0, 10 10, 0 10, 0 0)))",
                "MULTISURFACE (CURVEPOLYGON (CIRCULARSTRING (0 0, 2 2, 4 0, 2 -2, 0 0)))",
                "MULTISURFACE (CURVEPOLYGON (COMPOUNDCURVE (CIRCULARSTRING (0 0, 5 5, 10 0), (10 0, 0 0))))",
                "MULTISURFACE (CURVEPOLYGON (CIRCULARSTRING (0 0, 2 2, 4 0, 2 -2, 0 0)), POLYGON ((10 10, 20 10, 20 20, 10 20, 10 10)))",
                "MULTISURFACE Z (POLYGON ((0 0 5, 10 0 5, 10 10 5, 0 10 5, 0 0 5)))"
            };

            foreach (string wkt in year1)
            {
                var geometry = _reader.Read(wkt);
                string emitted = _writerZ.Write(geometry);
                Assert.That(emitted.ToUpperInvariant(), Does.StartWith("MULTISURFACE"));
                AssertWriterHasNoOmittedKeyword(emitted);
            }
        }

        [Test]
        public void Ticket7_GeometryCollectionOfSurfacesStaysGeometryCollectionOnWrite()
        {
            const string wkt =
                "GEOMETRYCOLLECTION (POLYGON ((0 0, 10 0, 10 10, 0 10, 0 0)), CURVEPOLYGON (CIRCULARSTRING (20 0, 22 2, 24 0, 22 -2, 20 0)))";
            var original = _reader.Read(wkt);
            Assert.That(original, Is.InstanceOf<GeometryCollection>());
            Assert.That(original, Is.Not.InstanceOf<MultiSurface>());

            string emitted = _writer.Write(original);
            Assert.That(emitted.ToUpperInvariant(), Does.StartWith("GEOMETRYCOLLECTION"));
            Assert.That(emitted.ToUpperInvariant(), Does.Not.Contain("MULTISURFACE"));

            var again = _reader.Read(emitted);
            Assert.That(again, Is.InstanceOf<GeometryCollection>());
            Assert.That(again, Is.Not.InstanceOf<MultiSurface>());
            Assert.That(again.EqualsExact(original), Is.True);
        }

        /// <summary>
        /// Whole-token match. A bare substring test would also fire on a longer
        /// type word that merely starts with one of these (CIRCLE inside
        /// CIRCULARSTRING), so the boundaries stay.
        /// </summary>
        private static void AssertWriterHasNoOmittedKeyword(string wkt)
        {
            string upper = wkt.ToUpperInvariant();
            foreach (string keyword in UnimplementedSqlMmCurveKeywords)
            {
                Assert.That(upper, Does.Not.Match(@"\b" + keyword + @"\b"),
                    "The Year-1 writer must not emit " + keyword + " for " + wkt);
            }
            foreach (string keyword in OmittedSurfaceMemberKeywords)
            {
                Assert.That(upper, Does.Not.Match(@"\b" + keyword + @"\b"),
                    "The Year-1 writer must not emit " + keyword + " for " + wkt);
            }
        }
    }
}
