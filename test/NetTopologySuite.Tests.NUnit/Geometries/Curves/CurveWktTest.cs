// SPDX-License-Identifier: BSD-3-Clause
// AI-drafted, human-reviewed.  Assisted-by: Claude (Fable 5)

using System;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Curves;
using NetTopologySuite.IO;
using NUnit.Framework;

// The OGC Triangle geometry, not the coordinate utility class.
using Triangle = NetTopologySuite.Geometries.Curves.Triangle;

namespace NetTopologySuite.Tests.NUnit.Geometries.Curves
{
    /// <summary>
    /// WKT round-trip tests for the curve prototype types (CIRCULARSTRING,
    /// COMPOUNDCURVE, CURVEPOLYGON, TRIANGLE, TIN).
    /// </summary>
    public class CurveWktTest
    {
        [TestCase("CIRCULARSTRING (0 0, 1 1, 2 0)", typeof(CircularString))]
        [TestCase("CIRCULARSTRING (0 0, 1 1, 2 0, 3 -1, 4 0)", typeof(CircularString))]
        [TestCase("CIRCULARSTRING EMPTY", typeof(CircularString))]
        [TestCase("COMPOUNDCURVE ((0 0, 1 0), CIRCULARSTRING (1 0, 2 1, 3 0), (3 0, 4 0))", typeof(CompoundCurve))]
        [TestCase("COMPOUNDCURVE (CIRCULARSTRING (0 0, 1 1, 2 0), (2 0, 3 0))", typeof(CompoundCurve))]
        [TestCase("COMPOUNDCURVE EMPTY", typeof(CompoundCurve))]
        [TestCase("CURVEPOLYGON (CIRCULARSTRING (0 0, 2 2, 4 0, 2 -2, 0 0))", typeof(CurvePolygon))]
        [TestCase("CURVEPOLYGON (COMPOUNDCURVE (CIRCULARSTRING (0 0, 5 5, 10 0), (10 0, 0 0)), (2 1, 8 1, 8 2, 2 2, 2 1))", typeof(CurvePolygon))]
        [TestCase("CURVEPOLYGON ((0 0, 100 0, 100 100, 0 100, 0 0), CIRCULARSTRING (40 50, 50 60, 60 50, 50 40, 40 50))", typeof(CurvePolygon))]
        [TestCase("CURVEPOLYGON EMPTY", typeof(CurvePolygon))]
        [TestCase("TRIANGLE ((0 0, 1 0, 0 1, 0 0))", typeof(Triangle))]
        [TestCase("TRIANGLE EMPTY", typeof(Triangle))]
        [TestCase("TIN (((0 0, 1 0, 0 1, 0 0)), ((1 0, 1 1, 0 1, 1 0)))", typeof(Tin))]
        [TestCase("TIN EMPTY", typeof(Tin))]
        [TestCase("GEOMETRYCOLLECTION (CIRCULARSTRING (0 0, 1 1, 2 0), POINT (5 5))", typeof(GeometryCollection))]
        public void WktRoundTripIsStable(string wkt, Type expectedType)
        {
            var reader = new WKTReader();

            var geometry = reader.Read(wkt);
            Assert.That(geometry, Is.InstanceOf(expectedType));

            string written = geometry.AsText();
            Assert.That(written, Is.EqualTo(wkt));

            var reparsed = reader.Read(written);
            Assert.That(reparsed.EqualsExact(geometry), Is.True,
                "Round-tripped geometry should be EqualsExact to the original.");
        }

        [Test]
        public void ReadPreservesComponentAndRingSubtypes()
        {
            var reader = new WKTReader();

            var cc = (CompoundCurve)reader.Read("COMPOUNDCURVE ((0 0, 1 0), CIRCULARSTRING (1 0, 2 1, 3 0))");
            Assert.That(cc.Curves[0], Is.InstanceOf<LineString>());
            Assert.That(cc.Curves[1], Is.InstanceOf<CircularString>());

            var cp = (CurvePolygon)reader.Read(
                "CURVEPOLYGON ((0 0, 100 0, 100 100, 0 100, 0 0), CIRCULARSTRING (40 50, 50 60, 60 50, 50 40, 40 50))");
            Assert.That(cp.ExteriorRing, Is.InstanceOf<LineString>());
            Assert.That(cp.GetInteriorRingN(0), Is.InstanceOf<CircularString>());
        }

        [Test]
        public void ReadSupportsZOrdinates()
        {
            var geometry = new WKTReader().Read("CIRCULARSTRING Z(0 0 5, 1 1 5, 2 0 5)");

            var circularString = (CircularString)geometry;
            Assert.That(circularString.CoordinateSequence.GetZ(0), Is.EqualTo(5));
            Assert.That(new WKTWriter(3).Write(geometry), Is.EqualTo("CIRCULARSTRING Z(0 0 5, 1 1 5, 2 0 5)"));
        }

        [Test]
        public void ReadFlattensNestedCompoundCurves()
        {
            // §5.1.67 <curve text> admits a tagged COMPOUNDCURVE component; the
            // value is spliced into the flat list (ADR-0005), so the writer's
            // output is the flat grammar-clean form.
            var cc = (CompoundCurve)new WKTReader().Read(
                "COMPOUNDCURVE (COMPOUNDCURVE ((0 0, 1 0)))");
            Assert.That(cc.Curves.Count, Is.EqualTo(1));
            Assert.That(cc.Curves[0], Is.InstanceOf<LineString>());
            Assert.That(cc.AsText(), Is.EqualTo("COMPOUNDCURVE ((0 0, 1 0))"));
        }

        [Test]
        public void ReadDropsEmptyCompoundCurveComponents()
        {
            // An EMPTY component is grammatical and contributes nothing to the
            // point set; intake drops it (ADR-0005, ticket 615-c).
            var cc = (CompoundCurve)new WKTReader().Read(
                "COMPOUNDCURVE ((0 0, 1 0), CIRCULARSTRING EMPTY)");
            Assert.That(cc.Curves.Count, Is.EqualTo(1));
            Assert.That(cc.AsText(), Is.EqualTo("COMPOUNDCURVE ((0 0, 1 0))"));
        }

        [Test]
        public void ReadFlattensNestedCompoundCurvesAmongSiblings()
        {
            var cc = (CompoundCurve)new WKTReader().Read(
                "COMPOUNDCURVE ((0 0, 1 0), COMPOUNDCURVE (CIRCULARSTRING (1 0, 2 1, 3 0), (3 0, 4 0)))");
            Assert.That(cc.Curves.Count, Is.EqualTo(3));
            Assert.That(cc.Curves[0], Is.InstanceOf<LineString>());
            Assert.That(cc.Curves[1], Is.InstanceOf<CircularString>());
            Assert.That(cc.Curves[2], Is.InstanceOf<LineString>());
            Assert.That(cc.AsText(), Is.EqualTo(
                "COMPOUNDCURVE ((0 0, 1 0), CIRCULARSTRING (1 0, 2 1, 3 0), (3 0, 4 0))"));
        }

        [Test]
        public void ReadFlattensNestedCompoundCurveInsideCurvePolygonRing()
        {
            // §5.1.67 <ring text> has the same alternatives as <curve text>,
            // nested COMPOUNDCURVE included.
            var cp = (CurvePolygon)new WKTReader().Read(
                "CURVEPOLYGON (COMPOUNDCURVE ((0 0, 1 0), COMPOUNDCURVE ((1 0, 1 1)), (1 1, 0 0)))");
            var shell = (CompoundCurve)cp.ExteriorRing;
            Assert.That(shell.Curves.Count, Is.EqualTo(3));
            Assert.That(shell.IsClosed, Is.True);
        }

        [Test]
        public void ReadRejectsTriangleWithInteriorRing()
        {
            Assert.Throws<ParseException>(() =>
                new WKTReader().Read("TRIANGLE ((0 0, 10 0, 0 10, 0 0), (1 1, 2 1, 1 2, 1 1))"));
        }

        [Test]
        public void ReadRejectsUnexpectedCurveComponent()
        {
            Assert.Throws<ParseException>(() =>
                new WKTReader().Read("CURVEPOLYGON (POINT (0 0))"));
        }

        [Test]
        public void ReadRejectsEvenCircularStringPointCount()
        {
            Assert.Throws<ArgumentException>(() =>
                new WKTReader().Read("CIRCULARSTRING (0 0, 1 1)"));
        }

        [Test]
        public void ReadRejectsNonContiguousCompoundCurve()
        {
            Assert.Throws<ArgumentException>(() =>
                new WKTReader().Read("COMPOUNDCURVE ((0 0, 1 0), (2 0, 3 0))"));
        }

        // --- Nested <z m> consistency (ticket 615-i). ISO/IEC 13249-3
        // §5.1.67: every WKT nested inside a WKT that carries a <z m> tag
        // shall carry the same <z m>. Enforced on read — mixed-dimension
        // curve WKT is rejected with a clause-citing message, not silently
        // coerced (the reader used to discard the inner tag). --------------

        [TestCase("COMPOUNDCURVE Z (CIRCULARSTRING M (1 1 5, 2 2 5, 3 3 5))")]
        [TestCase("COMPOUNDCURVE (CIRCULARSTRING Z (0 0 5, 1 1 5, 2 0 5))")]
        [TestCase("COMPOUNDCURVE ZM (CIRCULARSTRING Z (0 0 5, 1 1 5, 2 0 5))")]
        [TestCase("COMPOUNDCURVE Z (LINESTRING M (0 0 1, 1 0 1))")]
        [TestCase("CURVEPOLYGON Z (CIRCULARSTRING M (0 0 1, 2 2 1, 4 0 1, 2 -2 1, 0 0 1))")]
        [TestCase("MULTICURVE Z (CIRCULARSTRING M (1 1 5, 2 2 5, 3 3 5))")]
        [TestCase("MULTISURFACE Z (CURVEPOLYGON M (CIRCULARSTRING (0 0 1, 2 2 1, 4 0 1, 2 -2 1, 0 0 1)))")]
        [TestCase("MULTISURFACE Z (POLYGON M ((0 0 1, 4 0 1, 4 4 1, 0 0 1)))")]
        [TestCase("COMPOUNDCURVE Z (COMPOUNDCURVE M ((0 0 1, 1 1 1)))")]
        public void ReadRejectsMixedZmTagOnCurveComponent(string wkt)
        {
            var ex = Assert.Throws<ParseException>(() => new WKTReader().Read(wkt));
            Assert.That(ex.Message, Does.Contain("5.1.67"),
                "the rejection must cite the dimension-consistency rule");
        }

        [TestCase("COMPOUNDCURVE Z (CIRCULARSTRINGZ M (0 0 5, 1 1 5, 2 0 5))")]
        [TestCase("COMPOUNDCURVE Z (CIRCULARSTRING Z M (0 0 5, 1 1 5, 2 0 5))")]
        public void ReadRejectsDoubledZmTagOnCurveComponent(string wkt)
        {
            // A second tag after the first would otherwise be discarded
            // silently by the reader's tag-skip.
            var ex = Assert.Throws<ParseException>(() => new WKTReader().Read(wkt));
            Assert.That(ex.Message, Does.Contain("5.1.67"));
        }

        [Test]
        public void ReadAcceptsJoinedComponentTagMatchingTheOuter()
        {
            var cc = (CompoundCurve)new WKTReader().Read(
                "COMPOUNDCURVE Z (CIRCULARSTRINGZ (0 0 5, 1 1 5, 2 0 5), (2 0 5, 3 0 5))");
            Assert.That(((CircularString)cc.Curves[0]).CoordinateSequence.GetZ(0), Is.EqualTo(5));
        }

        [Test]
        public void ReadRejectsPlain2dComponentUnderZOuter()
        {
            // The 615-i witness: a COMPOUNDCURVE Z with a plain 2D component
            // is rejected with a message citing the dimension-consistency
            // rule (not the bare accidental arity error it used to be).
            var ex = Assert.Throws<ParseException>(() => new WKTReader().Read(
                "COMPOUNDCURVE Z ((0 0 1, 1 1 1), (1 1, 2 2))"));
            Assert.That(ex.Message, Does.Contain("5.1.67"));
        }

        [Test]
        public void ReadAcceptsConsistentZTagsOnComponents()
        {
            var cc = (CompoundCurve)new WKTReader().Read(
                "COMPOUNDCURVE Z (CIRCULARSTRING Z (0 0 5, 1 1 5, 2 0 5), LINESTRING Z (2 0 5, 3 0 5))");
            Assert.That(cc.Curves.Count, Is.EqualTo(2));
            Assert.That(((CircularString)cc.Curves[0]).CoordinateSequence.GetZ(0), Is.EqualTo(5));
            Assert.That(cc.Curves[1].Coordinates[0].Z, Is.EqualTo(5));
        }

        [Test]
        public void ReadAcceptsConsistentMTagsOnComponents()
        {
            var cc = (CompoundCurve)new WKTReader().Read(
                "COMPOUNDCURVE M (CIRCULARSTRING M (0 0 7, 1 1 7, 2 0 7), (2 0 7, 3 0 7))");
            Assert.That(((CircularString)cc.Curves[0]).CoordinateSequence.GetM(0), Is.EqualTo(7));
        }

        [Test]
        public void ReadAcceptsConsistentZmTagsOnComponents()
        {
            var cc = (CompoundCurve)new WKTReader().Read(
                "COMPOUNDCURVE ZM (CIRCULARSTRING ZM (0 0 5 7, 1 1 5 7, 2 0 5 7), (2 0 5 7, 3 0 5 7))");
            var cs = (CircularString)cc.Curves[0];
            Assert.That(cs.CoordinateSequence.GetZ(1), Is.EqualTo(5));
            Assert.That(cs.CoordinateSequence.GetM(1), Is.EqualTo(7));
        }

        [Test]
        public void ReadUntaggedComponentsInheritTheOuterDimension()
        {
            // The grammar's own shape: nested bodies carry no tag of their
            // own and read under the outer <z m> dimension.
            var cc = (CompoundCurve)new WKTReader().Read(
                "COMPOUNDCURVE Z ((0 0 5, 1 0 5), CIRCULARSTRING (1 0 5, 2 1 5, 3 0 5))");
            Assert.That(((CircularString)cc.Curves[1]).CoordinateSequence.GetZ(0), Is.EqualTo(5));
        }

        [Test]
        public void ReadAcceptsTaggedLineStringComponentAsDocumentedDeviation()
        {
            // DEVIATION, kept deliberately (ticket 615-i): §5.1.67 <curve
            // text> admits only bare bodies, CIRCULARSTRING and COMPOUNDCURVE
            // as components; GEOS and PostGIS also accept tagged LINESTRING.
            // Accepted on input for interop — the writer stays conformant and
            // emits the bare-body form.
            var cc = (CompoundCurve)new WKTReader().Read(
                "COMPOUNDCURVE (LINESTRING (0 0, 1 0), CIRCULARSTRING (1 0, 2 1, 3 0))");
            Assert.That(cc.Curves[0], Is.InstanceOf<LineString>());
            Assert.That(cc.AsText(), Is.EqualTo(
                "COMPOUNDCURVE ((0 0, 1 0), CIRCULARSTRING (1 0, 2 1, 3 0))"));
        }

        // --- EMPTY and Z/M/ZM round-trips for the three curve types,
        // re-pinned in one place (ticket 615-i Refactor). -------------------

        [TestCase("CIRCULARSTRING Z EMPTY", typeof(CircularString))]
        [TestCase("COMPOUNDCURVE ZM EMPTY", typeof(CompoundCurve))]
        [TestCase("CURVEPOLYGON ZM EMPTY", typeof(CurvePolygon))]
        [TestCase("CIRCULARSTRING Z (0 0 5, 1 1 5, 2 0 5)", typeof(CircularString))]
        [TestCase("CIRCULARSTRING M (0 0 7, 1 1 7, 2 0 7)", typeof(CircularString))]
        [TestCase("CIRCULARSTRING ZM (0 0 5 7, 1 1 5 7, 2 0 5 7)", typeof(CircularString))]
        [TestCase("COMPOUNDCURVE Z ((0 0 5, 1 0 5))", typeof(CompoundCurve))]
        [TestCase("COMPOUNDCURVE M ((0 0 7, 1 0 7))", typeof(CompoundCurve))]
        [TestCase("COMPOUNDCURVE ZM ((0 0 5 7, 1 0 5 7))", typeof(CompoundCurve))]
        [TestCase("CURVEPOLYGON Z (CIRCULARSTRING (0 0 5, 2 2 5, 4 0 5, 2 -2 5, 0 0 5))", typeof(CurvePolygon))]
        [TestCase("CURVEPOLYGON M (CIRCULARSTRING (0 0 7, 2 2 7, 4 0 7, 2 -2 7, 0 0 7))", typeof(CurvePolygon))]
        [TestCase("CURVEPOLYGON ZM (CIRCULARSTRING ZM (0 0 5 7, 2 2 5 7, 4 0 5 7, 2 -2 5 7, 0 0 5 7))", typeof(CurvePolygon))]
        public void EmptyAndZmFormsRoundTripThroughFullOrdinateWriter(string wkt, Type expectedType)
        {
            var reader = new WKTReader();
            var geometry = reader.Read(wkt);
            Assert.That(geometry, Is.InstanceOf(expectedType));

            var again = reader.Read(new WKTWriter(4).Write(geometry));
            Assert.That(again.GetType(), Is.EqualTo(geometry.GetType()));
            Assert.That(again.EqualsExact(geometry), Is.True);
            if (!geometry.IsEmpty)
            {
                // EqualsExact compares XY only; probe the extra ordinates.
                var expected = geometry.Coordinates[0];
                var actual = again.Coordinates[0];
                Assert.That(actual.Z, Is.EqualTo(expected.Z));
                Assert.That(actual.M, Is.EqualTo(expected.M));
            }
        }
    }
}
