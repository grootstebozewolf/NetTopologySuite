// SPDX-License-Identifier: BSD-3-Clause
// AI-drafted, human-reviewed.  Assisted-by: Cursor Grok 4.6
//
// Ticket 16 — NTS Year-1 ST_TIN WKT g4 list.
// Production: TIN [Z|M|ZM] (polygonText {, polygonText}…) | EMPTY.
// Writer never emits PATCHES. Named fields after the opener (PATCHES,
// ELEMENTS, MAXSIDELENGTH, and the eleven ST_TINElement / GML
// TINElementTypeType kinds) are refused by name. Short/wrong spellings
// stay ordinary unknown words or parse errors. No CIRCLE / GEODESICSTRING
// carrier. No WKB 16.

using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Curves;
using NetTopologySuite.IO;
using NUnit.Framework;

using Triangle = NetTopologySuite.Geometries.Curves.Triangle;

namespace NetTopologySuite.Tests.NUnit.Geometries.Curves
{
    /// <summary>
    /// Year-1 <c>TIN</c> WKT g4 list (Ticket 16).
    /// </summary>
    [Category("CurveAwareness")]
    public class TinWktYear1Test
    {
        private readonly WKTReader _reader = new WKTReader();
        private readonly WKTWriter _writer = new WKTWriter();
        private readonly WKTWriter _writerZ = new WKTWriter(3);

        /// <summary>
        /// Year-1-omitted TIN named fields. Exact spellings only:
        /// <c>PATCHES</c> / <c>ELEMENTS</c> / <c>MAXSIDELENGTH</c> plus the
        /// eleven ISO/IEC 13249-3 <c>ST_TINElement</c> kinds. The 11th kind
        /// not listed in the Ticket 16 body is <c>CONTROLCONTOUR</c>
        /// (GML <c>TINElementTypeType</c> controlContour).
        /// </summary>
        private static readonly string[] Year1OmittedTinNamedFields =
        {
            "PATCHES", "ELEMENTS", "MAXSIDELENGTH",
            "POINTS", "BREAKLINE", "BREAKVOID", "SOFTBREAK", "STOPLINE",
            "VOID", "HOLE", "DRAPEVOID", "GROUPSPOT", "BOUNDARY", "CONTROLCONTOUR"
        };

        [TestCase("TIN EMPTY", Description = "Ticket16 EMPTY")]
        [TestCase("TIN (((0 0, 1 0, 0 1, 0 0)))", Description = "Ticket16 one patch")]
        [TestCase("TIN (((0 0, 1 0, 0 1, 0 0)), ((1 0, 1 1, 0 1, 1 0)))", Description = "Ticket16 multi patch")]
        public void Ticket16_ReadWriteEmptyOnePatchAndMultiPatch(string wkt)
        {
            var geometry = _reader.Read(wkt);
            Assert.That(geometry, Is.InstanceOf<Tin>());
            Assert.That(geometry.AsText(), Is.EqualTo(wkt));
            Assert.That(_reader.Read(geometry.AsText()).EqualsExact(geometry), Is.True);
        }

        [Test]
        public void Ticket16_OnePatchRoundTripPreservesTriangle()
        {
            const string wkt = "TIN (((0 0, 1 0, 0 1, 0 0)))";
            var original = (Tin)_reader.Read(wkt);
            Assert.That(original.NumGeometries, Is.EqualTo(1));
            Assert.That(original.GetGeometryN(0), Is.InstanceOf<Triangle>());

            string emitted = _writer.Write(original);
            Assert.That(emitted, Is.EqualTo(wkt));
            Assert.That(emitted.ToUpperInvariant(), Does.Not.Contain("PATCHES"));

            var roundTrip = (Tin)_reader.Read(emitted);
            Assert.That(roundTrip.GetGeometryN(0), Is.InstanceOf<Triangle>());
            Assert.That(roundTrip.EqualsExact(original), Is.True);
        }

        [Test]
        public void Ticket16_MultiPatchRoundTripEqualsExact()
        {
            const string wkt = "TIN (((0 0, 1 0, 0 1, 0 0)), ((1 0, 1 1, 0 1, 1 0)))";
            var original = (Tin)_reader.Read(wkt);
            Assert.That(original.NumGeometries, Is.EqualTo(2));

            string emitted = _writer.Write(original);
            Assert.That(emitted, Is.EqualTo(wkt));
            Assert.That(_reader.Read(emitted).EqualsExact(original), Is.True);
        }

        [Test]
        public void Ticket16_ZOrdinatePreservedWhenWriterAskedForZ()
        {
            var original = _reader.Read(
                "TIN Z (((0 0 5, 1 0 5, 0 1 5, 0 0 5)))");
            Assert.That(original.Coordinates[0].Z, Is.EqualTo(5));

            string emitted = _writerZ.Write(original);
            Assert.That(emitted.ToUpperInvariant(), Does.Contain("Z"));
            Assert.That(emitted, Does.Contain("5"));
            Assert.That(emitted.ToUpperInvariant(), Does.Not.Contain("PATCHES"));

            var again = _reader.Read(emitted);
            Assert.That(again, Is.InstanceOf<Tin>());
            Assert.That(again.EqualsExact(original), Is.True);
            Assert.That(again.Coordinates[0].Z, Is.EqualTo(5));
        }

        [TestCase("PATCHES", "PATCHES (((0 0, 1 0, 0 1, 0 0)))")]
        [TestCase("ELEMENTS", "ELEMENTS (POINTS ((0 0, 1 0, 0 1)))")]
        [TestCase("MAXSIDELENGTH", "MAXSIDELENGTH 10")]
        public void Ticket16_RejectsNamedFieldAfterOpener(string field, string body)
        {
            var ex = Assert.Throws<ParseException>(() =>
                _reader.Read("TIN (" + body + ")"));
            Assert.That(ex.Message, Does.Contain(field));
            Assert.That(ex.Message, Does.Contain("named field"));
            Assert.That(ex.Message, Does.Contain("not implemented"));
            Assert.That(ex.Message, Does.Not.Contain("Unknown type"));
            Assert.That(ex.Message, Does.Not.Contain("Expected 'EMPTY' or '('"));
        }

        [TestCase("BREAKLINE", "BREAKLINE ((0 0, 1 0))")]
        [TestCase("VOID", "VOID (((0 0, 1 0, 0 1, 0 0)))")]
        [TestCase("CONTROLCONTOUR", "CONTROLCONTOUR ((0 0, 1 0, 1 1))")]
        public void Ticket16_RejectsElementsKindAsFieldToken(string field, string body)
        {
            var ex = Assert.Throws<ParseException>(() =>
                _reader.Read("TIN (" + body + ")"));
            Assert.That(ex.Message, Does.Contain(field));
            Assert.That(ex.Message, Does.Contain("named field"));
            Assert.That(ex.Message, Does.Contain("not implemented"));
            Assert.That(ex.Message, Does.Not.Contain("Unknown type"));
        }

        /// <summary>
        /// Short or wrong leftovers are not the ISO field tokens. They stay
        /// ordinary parse errors after <c>(</c> and must not be promoted to
        /// a named-field refuse.
        /// </summary>
        [TestCase("PATCH (((0 0, 1 0, 0 1, 0 0)))")]
        [TestCase("ELEMENT (POINTS ((0 0)))")]
        [TestCase("MAXSIDE 10")]
        [TestCase("POINT (0 0)")]
        [TestCase("BREAK ((0 0, 1 0))")]
        public void Ticket16_ShortSpellingsAreOrdinaryParseErrors(string body)
        {
            var ex = Assert.Throws<ParseException>(() =>
                _reader.Read("TIN (" + body + ")"));
            Assert.That(ex.Message, Does.Not.Contain("not implemented"));
            Assert.That(ex.Message, Does.Not.Contain("named field"));
        }

        /// <summary>
        /// In type position the same tokens are ordinary unknown words or
        /// ordinary parse errors, not TIN named-field refuses.
        /// <c>POINTS</c> starts with <c>POINT</c> and therefore hits
        /// <c>Invalid dimension modifiers</c> rather than <c>Unknown type</c>.
        /// </summary>
        [TestCase("PATCHES (((0 0, 1 0, 0 1, 0 0)))", "Unknown type")]
        [TestCase("ELEMENTS (POINTS ((0 0)))", "Unknown type")]
        [TestCase("POINTS ((0 0, 1 0, 0 1))", "Invalid dimension modifiers")]
        public void Ticket16_FieldTokensInTypePositionAreOrdinaryErrors(string wkt, string ordinary)
        {
            var ex = Assert.Throws<ParseException>(() => _reader.Read(wkt));
            Assert.That(ex.Message, Does.Contain(ordinary));
            Assert.That(ex.Message, Does.Not.Contain("named field"));
            Assert.That(ex.Message, Does.Not.Contain("not implemented"));
        }

        [Test]
        public void Ticket16_WriterNeverEmitsPatchesForYear1Object()
        {
            string[] year1 =
            {
                "TIN EMPTY",
                "TIN (((0 0, 1 0, 0 1, 0 0)))",
                "TIN (((0 0, 1 0, 0 1, 0 0)), ((1 0, 1 1, 0 1, 1 0)))",
                "TIN Z (((0 0 5, 1 0 5, 0 1 5, 0 0 5)))"
            };

            foreach (string wkt in year1)
            {
                var geometry = _reader.Read(wkt);
                string emitted = _writerZ.Write(geometry);
                Assert.That(emitted.ToUpperInvariant(), Does.StartWith("TIN"));
                Assert.That(emitted.ToUpperInvariant(), Does.Not.Contain("PATCHES"));
                foreach (string field in Year1OmittedTinNamedFields)
                {
                    Assert.That(emitted.ToUpperInvariant(), Does.Not.Match(@"\b" + field + @"\b"),
                        "The Year-1 writer must not emit " + field + " for " + wkt);
                }
            }
        }

        [Test]
        public void Ticket16_CircleAndGeodesicStringAreNotTinCarriers()
        {
            var circle = Assert.Throws<ParseException>(() =>
                _reader.Read("TIN (CIRCLE (0 0, 1 0, 0 1))"));
            Assert.That(circle.Message, Does.Not.Contain("named field"));

            var geodesic = Assert.Throws<ParseException>(() =>
                _reader.Read("TIN (GEODESICSTRING (0 0, 10 0, 10 10))"));
            Assert.That(geodesic.Message, Does.Not.Contain("named field"));
        }
    }
}
