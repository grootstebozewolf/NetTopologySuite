// SPDX-License-Identifier: BSD-3-Clause
// GEOS-aligned WKT/WKB curve I/O tests. Assisted-by: xAI Grok

using System;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Curves;
using NetTopologySuite.IO;
using NUnit.Framework;

namespace NetTopologySuite.Tests.NUnit.Geometries.Curves
{
    /// <summary>
    /// WKT/WKB import dovetailed with GEOS SQL/MM curve types (8–12).
    /// </summary>
    public class CurveWkbWktGeosTest
    {
        [TestCase("CIRCULARSTRING (0 0, 1 1, 2 0)")]
        [TestCase("CIRCULARSTRING EMPTY")]
        [TestCase("COMPOUNDCURVE ((0 0, 1 0), CIRCULARSTRING (1 0, 2 1, 3 0))")]
        [TestCase("COMPOUNDCURVE (LINESTRING (0 0, 1 0), CIRCULARSTRING (1 0, 2 1, 3 0))")]
        [TestCase("CURVEPOLYGON (CIRCULARSTRING (0 0, 2 2, 4 0, 2 -2, 0 0))")]
        [TestCase("MULTICURVE (CIRCULARSTRING (0 0, 1 1, 2 0), (3 0, 4 0))")]
        [TestCase("MULTICURVE EMPTY")]
        [TestCase("MULTISURFACE (CURVEPOLYGON (CIRCULARSTRING (0 0, 2 2, 4 0, 2 -2, 0 0)), POLYGON ((10 10, 20 10, 20 20, 10 20, 10 10)))")]
        [TestCase("MULTISURFACE EMPTY")]
        public void WktRoundTrip(string wkt)
        {
            var g = new WKTReader().Read(wkt);
            string written = g.AsText();
            var again = new WKTReader().Read(written);
            Assert.That(again.EqualsExact(g), Is.True, written);
        }

        [Test]
        public void GeosStyleLineStringTagInCompoundCurve()
        {
            var g = new WKTReader().Read(
                "COMPOUNDCURVE (LINESTRING (0 0, 1 0), CIRCULARSTRING (1 0, 2 1, 3 0))");
            var cc = (CompoundCurve)g;
            Assert.That(cc.Curves[0], Is.InstanceOf<LineString>());
            Assert.That(cc.Curves[1], Is.InstanceOf<CircularString>());
        }

        [TestCase("CIRCULARSTRING (0 0, 1 1, 2 0)")]
        [TestCase("CIRCULARSTRING EMPTY")]
        [TestCase("COMPOUNDCURVE ((0 0, 1 0), CIRCULARSTRING (1 0, 2 1, 3 0))")]
        [TestCase("CURVEPOLYGON (CIRCULARSTRING (0 0, 2 2, 4 0, 2 -2, 0 0))")]
        [TestCase("CURVEPOLYGON EMPTY")]
        [TestCase("MULTICURVE (CIRCULARSTRING (0 0, 1 1, 2 0), (3 0, 4 0))")]
        [TestCase("MULTICURVE EMPTY")]
        [TestCase("MULTISURFACE (CURVEPOLYGON (CIRCULARSTRING (0 0, 2 2, 4 0, 2 -2, 0 0)))")]
        [TestCase("MULTISURFACE EMPTY")]
        public void WkbRoundTrip(string wkt)
        {
            var g = new WKTReader().Read(wkt);
            var writer = new WKBWriter();
            byte[] bytes = writer.Write(g);
            var reader = new WKBReader();
            var again = reader.Read(bytes);
            Assert.That(again.GetType(), Is.EqualTo(g.GetType()), g.GeometryType);
            Assert.That(again.EqualsExact(g), Is.True, WKBWriter.ToHex(bytes));
        }

        [Test]
        public void WkbTypeCodesMatchGeosIso()
        {
            Assert.That((int)WKBGeometryTypes.WKBCircularString, Is.EqualTo(8));
            Assert.That((int)WKBGeometryTypes.WKBCompoundCurve, Is.EqualTo(9));
            Assert.That((int)WKBGeometryTypes.WKBCurvePolygon, Is.EqualTo(10));
            Assert.That((int)WKBGeometryTypes.WKBMultiCurve, Is.EqualTo(11));
            Assert.That((int)WKBGeometryTypes.WKBMultiSurface, Is.EqualTo(12));
        }

        // ISO/IEC 13249-3 §5.1.68 Table 15 lists an alternate WKB code series
        // for the curved types (1000001–1000005) alongside the base codes
        // 8–12 (ticket 615-i): accepted on read; the writer emits base codes
        // only (also pinned here).
        [TestCase("CIRCULARSTRING (0 0, 1 1, 2 0)", 8u, 1000001u, typeof(CircularString))]
        [TestCase("COMPOUNDCURVE ((0 0, 1 0), CIRCULARSTRING (1 0, 2 1, 3 0))", 9u, 1000002u, typeof(CompoundCurve))]
        [TestCase("CURVEPOLYGON (CIRCULARSTRING (0 0, 2 2, 4 0, 2 -2, 0 0))", 10u, 1000003u, typeof(CurvePolygon))]
        [TestCase("MULTICURVE (CIRCULARSTRING (0 0, 1 1, 2 0), (3 0, 4 0))", 11u, 1000004u, typeof(MultiCurve))]
        [TestCase("MULTISURFACE (CURVEPOLYGON (CIRCULARSTRING (0 0, 2 2, 4 0, 2 -2, 0 0)))", 12u, 1000005u, typeof(MultiSurface))]
        public void WkbAlternateTable15CodesReadAsBaseTypes(string wkt, uint baseCode, uint altCode, Type expectedType)
        {
            var g = new WKTReader().Read(wkt);
            byte[] bytes = new WKBWriter().Write(g);

            // Byte 0 is the little-endian marker; bytes 1–4 are the type code.
            Assert.That(bytes[0], Is.EqualTo(1), "expected little-endian WKB");
            Assert.That(ReadTypeLe(bytes), Is.EqualTo(baseCode),
                "the writer must emit the base code, never the alternate");

            WriteTypeLe(bytes, altCode);
            var again = new WKBReader().Read(bytes);
            Assert.That(again, Is.InstanceOf(expectedType));
            Assert.That(again.EqualsExact(g), Is.True, WKBWriter.ToHex(bytes));
        }

        private static uint ReadTypeLe(byte[] wkb) =>
            (uint)(wkb[1] | wkb[2] << 8 | wkb[3] << 16 | wkb[4] << 24);

        private static void WriteTypeLe(byte[] wkb, uint type)
        {
            wkb[1] = (byte)type;
            wkb[2] = (byte)(type >> 8);
            wkb[3] = (byte)(type >> 16);
            wkb[4] = (byte)(type >> 24);
        }

        [Test]
        public void MultiSurfacePreservesMemberSubtypes()
        {
            var g = (MultiSurface)new WKTReader().Read(
                "MULTISURFACE (CURVEPOLYGON (CIRCULARSTRING (0 0, 2 2, 4 0, 2 -2, 0 0)), POLYGON ((10 10, 20 10, 20 20, 10 20, 10 10)))");
            Assert.That(g.GetGeometryN(0), Is.InstanceOf<CurvePolygon>());
            Assert.That(g.GetGeometryN(1), Is.InstanceOf<Polygon>());
        }
    }
}
