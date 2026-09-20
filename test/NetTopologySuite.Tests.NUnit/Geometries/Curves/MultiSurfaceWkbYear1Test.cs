// SPDX-License-Identifier: BSD-3-Clause
// AI-drafted, human-reviewed.  Assisted-by: Cursor Grok 4.6
//
// Ticket 8 — Year-1 ST_MultiSurface WKB type 12 read/write (ISO/IEC 13249-3).
// Members: nested WKB Polygon (3) | CurvePolygon (10).
// Reuses Ticket 7 Year-1 member lock and Ticket 2 CurvePolygon WKB path;
// does not remint WKT grammar. Ticket 9: Year-1 WKB 12 is complete (no
// longer "partial"); typed members stay in MultiSurfaceMembersYear1Test.

using System;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Curves;
using NetTopologySuite.IO;
using NUnit.Framework;

namespace NetTopologySuite.Tests.NUnit.Geometries.Curves
{
    /// <summary>
    /// Year-1 <c>MULTISURFACE</c> WKB type 12 (Ticket 8).
    /// </summary>
    [Category("CurveAwareness")]
    public class MultiSurfaceWkbYear1Test
    {
        private readonly WKTReader _wktReader = new WKTReader();
        private readonly WKTWriter _wktWriter = new WKTWriter();
        private readonly WKTWriter _wktWriterZ = new WKTWriter(3);
        private readonly WKTWriter _wktWriterZm = new WKTWriter(4);
        private readonly WKBReader _wkbReader = new WKBReader();
        private readonly WKBWriter _wkbWriter = new WKBWriter();

        // Ticket 7 fixtures reused as WKB witnesses (no WKT remint).
        private const string WktEmpty = "MULTISURFACE EMPTY";
        private const string WktPolygon =
            "MULTISURFACE (POLYGON ((0 0, 10 0, 10 10, 0 10, 0 0)))";
        private const string WktCurvePolygon =
            "MULTISURFACE (CURVEPOLYGON (CIRCULARSTRING (0 0, 2 2, 4 0, 2 -2, 0 0)))";
        private const string WktCcShell =
            "MULTISURFACE (CURVEPOLYGON (COMPOUNDCURVE (CIRCULARSTRING (0 0, 5 5, 10 0), (10 0, 0 0))))";
        private const string WktLsShellCsHole =
            "MULTISURFACE (CURVEPOLYGON ((0 0, 100 0, 100 100, 0 100, 0 0), CIRCULARSTRING (40 50, 50 60, 60 50, 50 40, 40 50)))";
        private const string WktMixed =
            "MULTISURFACE (CURVEPOLYGON (CIRCULARSTRING (0 0, 2 2, 4 0, 2 -2, 0 0)), POLYGON ((10 10, 20 10, 20 20, 10 20, 10 10)))";
        private const string WktYear1Rings =
            "MULTISURFACE (CURVEPOLYGON ((0 0, 100 0, 100 100, 0 100, 0 0), CIRCULARSTRING (40 50, 50 60, 60 50, 50 40, 40 50), COMPOUNDCURVE (CIRCULARSTRING (10 10, 15 15, 20 10), (20 10, 10 10))))";
        private const string WktZ =
            "MULTISURFACE Z (POLYGON ((0 0 5, 10 0 5, 10 10 5, 0 10 5, 0 0 5)), CURVEPOLYGON (CIRCULARSTRING (20 0 5, 22 2 5, 24 0 5, 22 -2 5, 20 0 5)))";
        private const string WktM =
            "MULTISURFACE M (POLYGON ((0 0 7, 10 0 7, 10 10 7, 0 10 7, 0 0 7)))";
        private const string WktZm =
            "MULTISURFACE ZM (POLYGON ZM ((0 0 5 7, 10 0 5 7, 10 10 5 7, 0 10 5 7, 0 0 5 7)), CURVEPOLYGON (CIRCULARSTRING ZM (20 0 5 7, 22 2 5 7, 24 0 5 7, 22 -2 5 7, 20 0 5 7)))";

        // GEOS / ISO little-endian type-12 fixtures, hand-checked against the
        // nested WKB layout (byte order, type, numMembers, then nested 3|10).
        private const string HexEmpty = "010C00000000000000";

        private const string HexPolygonOnly =
            "010C00000001000000" +
            "01030000000100000005000000" +
            "00000000000000000000000000000000" +
            "00000000000024400000000000000000" +
            "00000000000024400000000000002440" +
            "00000000000000000000000000002440" +
            "00000000000000000000000000000000";

        private const string HexCpOnly =
            "010C00000001000000" +
            "010A00000001000000" +
            "010800000005000000" +
            "00000000000000000000000000000000" +
            "00000000000000400000000000000040" +
            "00000000000010400000000000000000" +
            "000000000000004000000000000000C0" +
            "00000000000000000000000000000000";

        private const string HexMixedCpPoly =
            "010C00000002000000" +
            "010A00000001000000" +
            "010800000005000000" +
            "00000000000000000000000000000000" +
            "00000000000000400000000000000040" +
            "00000000000010400000000000000000" +
            "000000000000004000000000000000C0" +
            "00000000000000000000000000000000" +
            "01030000000100000005000000" +
            "00000000000024400000000000002440" +
            "00000000000034400000000000002440" +
            "00000000000034400000000000003440" +
            "00000000000024400000000000003440" +
            "00000000000024400000000000002440";

        private const string HexCcShell =
            "010C00000001000000" +
            "010A00000001000000" +
            "010900000002000000" +
            "010800000003000000" +
            "00000000000000000000000000000000" +
            "00000000000014400000000000001440" +
            "00000000000024400000000000000000" +
            "010200000002000000" +
            "00000000000024400000000000000000" +
            "00000000000000000000000000000000";

        [Test]
        public void Ticket8_WkbTypeCodeIsTwelve()
        {
            Assert.That((int)WKBGeometryTypes.WKBMultiSurface, Is.EqualTo(12));
            byte[] bytes = _wkbWriter.Write(_wktReader.Read(WktPolygon));
            Assert.That(bytes[0], Is.EqualTo(1), "little-endian");
            Assert.That(ReadTypeLe(bytes, 1), Is.EqualTo(12u));
        }

        [TestCase(WktEmpty, Description = "Ticket8 EMPTY")]
        [TestCase(WktPolygon, Description = "Ticket8 Polygon-only")]
        [TestCase(WktCurvePolygon, Description = "Ticket8 CurvePolygon-only")]
        [TestCase(WktCcShell, Description = "Ticket8 CurvePolygon CC shell")]
        [TestCase(WktLsShellCsHole, Description = "Ticket8 CurvePolygon LS+CS rings")]
        [TestCase(WktMixed, Description = "Ticket8 mixed Polygon+CurvePolygon")]
        [TestCase(WktYear1Rings, Description = "Ticket8 CurvePolygon Year-1 rings")]
        public void Ticket8_WkbObjectWkbPreservesEqualsExact(string wkt)
        {
            var original = _wktReader.Read(wkt);
            byte[] bytes = _wkbWriter.Write(original);
            var again = _wkbReader.Read(bytes);
            Assert.That(again, Is.InstanceOf<MultiSurface>());
            Assert.That(again.GetType(), Is.EqualTo(original.GetType()));
            Assert.That(again.EqualsExact(original), Is.True, WKBWriter.ToHex(bytes));
            Assert.That(_wkbWriter.Write(again), Is.EqualTo(bytes));
        }

        [TestCase(WktEmpty)]
        [TestCase(WktPolygon)]
        [TestCase(WktCurvePolygon)]
        [TestCase(WktCcShell)]
        [TestCase(WktLsShellCsHole)]
        [TestCase(WktMixed)]
        [TestCase(WktYear1Rings)]
        public void Ticket8_WktWkbWktPreservesMemberSubtypes(string wkt)
        {
            var original = (MultiSurface)_wktReader.Read(wkt);
            byte[] bytes = _wkbWriter.Write(original);
            var fromWkb = (MultiSurface)_wkbReader.Read(bytes);
            AssertMemberKinds(original, fromWkb);

            string emitted = _wktWriter.Write(fromWkb);
            var fromWkt = (MultiSurface)_wktReader.Read(emitted);
            AssertMemberKinds(original, fromWkt);
            Assert.That(fromWkt.EqualsExact(original), Is.True, emitted);
        }

        [Test]
        public void Ticket8_MixedCpPolyByteLevelFixture()
        {
            var fromWkt = (MultiSurface)_wktReader.Read(WktMixed);
            byte[] written = _wkbWriter.Write(fromWkt);
            Assert.That(WKBWriter.ToHex(written), Is.EqualTo(HexMixedCpPoly));

            Assert.That(written[0], Is.EqualTo(1));
            Assert.That(ReadTypeLe(written, 1), Is.EqualTo(12u));
            Assert.That(ReadUInt32Le(written, 5), Is.EqualTo(2u), "two members");
            Assert.That(written[9], Is.EqualTo(1));
            Assert.That(ReadTypeLe(written, 10), Is.EqualTo(10u), "CurvePolygon member");
            Assert.That(ReadTypeLe(written, 19), Is.EqualTo(8u), "CS shell via type 10");
            int polyOffset = 9 + 5 + 4 + 5 + 4 + 5 * 16;
            Assert.That(written[polyOffset], Is.EqualTo(1));
            Assert.That(ReadTypeLe(written, polyOffset + 1), Is.EqualTo(3u), "Polygon member");

            var fromHex = (MultiSurface)_wkbReader.Read(WKBReader.HexToBytes(HexMixedCpPoly));
            Assert.That(fromHex.GetGeometryN(0), Is.InstanceOf<CurvePolygon>());
            Assert.That(((CurvePolygon)fromHex.GetGeometryN(0)).ExteriorRing, Is.InstanceOf<CircularString>());
            Assert.That(fromHex.GetGeometryN(1), Is.InstanceOf<Polygon>());
            Assert.That(fromHex.GetGeometryN(1), Is.Not.InstanceOf<CurvePolygon>());
            Assert.That(fromHex.EqualsExact(fromWkt), Is.True);
        }

        [Test]
        public void Ticket8_PolygonOnlyByteLevelFixture()
        {
            var fromWkt = (MultiSurface)_wktReader.Read(WktPolygon);
            byte[] written = _wkbWriter.Write(fromWkt);
            Assert.That(WKBWriter.ToHex(written), Is.EqualTo(HexPolygonOnly));
            Assert.That(ReadTypeLe(written, 1), Is.EqualTo(12u));
            Assert.That(ReadTypeLe(written, 10), Is.EqualTo(3u), "Polygon member");

            var fromHex = (MultiSurface)_wkbReader.Read(WKBReader.HexToBytes(HexPolygonOnly));
            Assert.That(fromHex.GetGeometryN(0), Is.InstanceOf<Polygon>());
            Assert.That(fromHex.GetGeometryN(0), Is.Not.InstanceOf<CurvePolygon>());
            Assert.That(fromHex.EqualsExact(fromWkt), Is.True);
        }

        [Test]
        public void Ticket8_CurvePolygonOnlyByteLevelFixture()
        {
            var fromWkt = (MultiSurface)_wktReader.Read(WktCurvePolygon);
            byte[] written = _wkbWriter.Write(fromWkt);
            Assert.That(WKBWriter.ToHex(written), Is.EqualTo(HexCpOnly));
            Assert.That(ReadTypeLe(written, 1), Is.EqualTo(12u));
            Assert.That(ReadTypeLe(written, 10), Is.EqualTo(10u), "CurvePolygon member");
            Assert.That(ReadTypeLe(written, 19), Is.EqualTo(8u), "CS ring via type 10");

            var fromHex = (MultiSurface)_wkbReader.Read(WKBReader.HexToBytes(HexCpOnly));
            Assert.That(fromHex.GetGeometryN(0), Is.InstanceOf<CurvePolygon>());
            Assert.That(((CurvePolygon)fromHex.GetGeometryN(0)).ExteriorRing, Is.InstanceOf<CircularString>());
            Assert.That(fromHex.EqualsExact(fromWkt), Is.True);
        }

        [Test]
        public void Ticket8_CcShellByteLevelFixture()
        {
            var fromWkt = (MultiSurface)_wktReader.Read(WktCcShell);
            byte[] written = _wkbWriter.Write(fromWkt);
            Assert.That(WKBWriter.ToHex(written), Is.EqualTo(HexCcShell));

            Assert.That(ReadTypeLe(written, 1), Is.EqualTo(12u));
            Assert.That(ReadTypeLe(written, 10), Is.EqualTo(10u), "CurvePolygon member");
            Assert.That(ReadTypeLe(written, 19), Is.EqualTo(9u), "CC shell via type 10");
            Assert.That(ReadUInt32Le(written, 23), Is.EqualTo(2u), "two CC members");
            Assert.That(ReadTypeLe(written, 28), Is.EqualTo(8u), "CS component");
            int lsMemberOffset = 27 + 5 + 4 + 3 * 16;
            Assert.That(ReadTypeLe(written, lsMemberOffset + 1), Is.EqualTo(2u), "LS component");

            var fromHex = (MultiSurface)_wkbReader.Read(WKBReader.HexToBytes(HexCcShell));
            var cp = (CurvePolygon)fromHex.GetGeometryN(0);
            Assert.That(cp.ExteriorRing, Is.InstanceOf<CompoundCurve>());
            var shell = (CompoundCurve)cp.ExteriorRing;
            Assert.That(shell.Curves[0], Is.InstanceOf<CircularString>());
            Assert.That(shell.Curves[1], Is.InstanceOf<LineString>());
            Assert.That(fromHex.EqualsExact(fromWkt), Is.True);
        }

        [Test]
        public void Ticket8_EmptyWritesZeroMembers()
        {
            byte[] bytes = _wkbWriter.Write(_wktReader.Read(WktEmpty));
            Assert.That(WKBWriter.ToHex(bytes), Is.EqualTo(HexEmpty));
            Assert.That(ReadTypeLe(bytes, 1), Is.EqualTo(12u));
            Assert.That(ReadUInt32Le(bytes, 5), Is.EqualTo(0u));
            var again = (MultiSurface)_wkbReader.Read(bytes);
            Assert.That(again.IsEmpty, Is.True);
        }

        [Test]
        public void Ticket8_IsoZmTypeCodesReadAndWrite()
        {
            // Same +1000/+2000/+3000 table already used for types 8–11.
            // Strict writer emits ISO codes (no EWKB high bits).
            AssertZmRoundTrip(WktZ, _wktWriterZ, emitZ: true, emitM: false, expectedIsoType: 1012u, expectZ: 5, expectM: double.NaN);
            AssertZmRoundTrip(WktM, _wktWriterZm, emitZ: false, emitM: true, expectedIsoType: 2012u, expectZ: double.NaN, expectM: 7);
            AssertZmRoundTrip(WktZm, _wktWriterZm, emitZ: true, emitM: true, expectedIsoType: 3012u, expectZ: 5, expectM: 7);
        }

        [Test]
        public void Ticket8_IsoZmTypeCodesAcceptedOnReadWhenPatched()
        {
            var xy = _wktReader.Read(WktCurvePolygon);
            byte[] bytes = _wkbWriter.Write(xy);
            Assert.That(ReadTypeLe(bytes, 1), Is.EqualTo(12u));

            foreach (uint isoType in new uint[] { 1012u, 2012u, 3012u })
            {
                byte[] patched = (byte[])bytes.Clone();
                WriteTypeLe(patched, 1, isoType);
                var again = _wkbReader.Read(patched);
                Assert.That(again, Is.InstanceOf<MultiSurface>(), "ISO type " + isoType);
                Assert.That(((MultiSurface)again).GetGeometryN(0), Is.InstanceOf<CurvePolygon>());
            }
        }

        [Test]
        public void Ticket8_EwkbSridPathMatchesMultiPolygon()
        {
            var multiSurface = (MultiSurface)_wktReader.Read(WktPolygon);
            multiSurface.SRID = 4326;
            var multiPolygon = (MultiPolygon)_wktReader.Read("MULTIPOLYGON (((0 0, 10 0, 10 10, 0 10, 0 0)))");
            multiPolygon.SRID = 4326;

            var ewkbWriter = new WKBWriter(ByteOrder.LittleEndian, true);
            byte[] surfaceBytes = ewkbWriter.Write(multiSurface);
            byte[] polygonBytes = ewkbWriter.Write(multiPolygon);

            uint surfaceType = ReadTypeLe(surfaceBytes, 1);
            uint polygonType = ReadTypeLe(polygonBytes, 1);
            Assert.That((surfaceType & 0x20000000u) != 0, Is.True, "EWKB SRID flag on MultiSurface");
            Assert.That((polygonType & 0x20000000u) != 0, Is.True, "EWKB SRID flag on MultiPolygon");
            Assert.That(surfaceType & 0x0FFFFFFFu, Is.EqualTo(12u));
            Assert.That(polygonType & 0x0FFFFFFFu, Is.EqualTo(6u));
            Assert.That(BitConverter.ToInt32(surfaceBytes, 5), Is.EqualTo(4326));
            Assert.That(BitConverter.ToInt32(polygonBytes, 5), Is.EqualTo(4326));

            var again = (MultiSurface)_wkbReader.Read(surfaceBytes);
            Assert.That(again.SRID, Is.EqualTo(4326));
            Assert.That(again.GetGeometryN(0), Is.InstanceOf<Polygon>());
            Assert.That(again.EqualsExact(multiSurface), Is.True);
        }

        [Test]
        public void Ticket8_Table15AlternateCodeReadsAsType12()
        {
            byte[] bytes = _wkbWriter.Write(_wktReader.Read(WktCurvePolygon));
            Assert.That(ReadTypeLe(bytes, 1), Is.EqualTo(12u), "writer emits base 12, never 1000005");
            WriteTypeLe(bytes, 1, 1000005u);
            var again = _wkbReader.Read(bytes);
            Assert.That(again, Is.InstanceOf<MultiSurface>());
            Assert.That(((MultiSurface)again).GetGeometryN(0), Is.InstanceOf<CurvePolygon>());
        }

        [TestCase(1u, Description = "nested Point")]
        [TestCase(2u, Description = "nested LineString")]
        [TestCase(4u, Description = "nested MultiPoint")]
        [TestCase(5u, Description = "nested MultiLineString")]
        [TestCase(6u, Description = "nested MultiPolygon")]
        [TestCase(7u, Description = "nested GeometryCollection")]
        [TestCase(8u, Description = "nested CircularString")]
        [TestCase(9u, Description = "nested CompoundCurve")]
        [TestCase(11u, Description = "nested MultiCurve")]
        [TestCase(12u, Description = "nested MultiSurface")]
        [TestCase(13u, Description = "unknown type 13")]
        [TestCase(15u, Description = "PolyhedralSurface (omitted surface)")]
        [TestCase(16u, Description = "TIN (omitted surface)")]
        [TestCase(17u, Description = "Triangle (omitted surface)")]
        [TestCase(18u, Description = "unknown type 18 (not a Circle / Year-2 invention)")]
        public void Ticket8_RejectsNonYear1NestedMemberTypeCode(uint nestedType)
        {
            byte[] member = _wkbWriter.Write(_wktReader.Read("POLYGON ((0 0, 10 0, 10 10, 0 10, 0 0))"));
            WriteTypeLe(member, 1, nestedType);
            byte[] wrapped = WrapAsMultiSurface(member);
            var ex = Assert.Throws<ParseException>(() => _wkbReader.Read(wrapped));
            Assert.That(ex.ToString(), Does.Contain("Year-1"));
            Assert.That(ex.ToString(), Does.Contain(nestedType.ToString()));
        }

        [Test]
        public void Ticket8_RejectsUnsupportedRingTypeInsideCurvePolygonMember()
        {
            byte[] ring = _wkbWriter.Write(_wktReader.Read("LINESTRING (0 0, 10 0, 10 10, 0 10, 0 0)"));
            WriteTypeLe(ring, 1, 18u);
            byte[] curvePolygon = WrapAsCurvePolygon(ring);
            byte[] wrapped = WrapAsMultiSurface(curvePolygon);
            var ex = Assert.Throws<ParseException>(() => _wkbReader.Read(wrapped));
            Assert.That(ex.ToString(), Does.Contain("Year-1"));
            Assert.That(ex.ToString(), Does.Contain("18"));
        }

        [Test]
        public void Ticket8_RejectsNestedCompoundCurveInsideCurvePolygonMember()
        {
            byte[] ls1 = _wkbWriter.Write(_wktReader.Read("LINESTRING (0 0, 1 0)"));
            byte[] nestedCc = _wkbWriter.Write(_wktReader.Read("COMPOUNDCURVE ((1 0, 1 1))"));
            byte[] ls3 = _wkbWriter.Write(_wktReader.Read("LINESTRING (1 1, 0 0)"));

            var ccBody = new byte[5 + 4 + ls1.Length + nestedCc.Length + ls3.Length];
            ccBody[0] = 1;
            WriteTypeLe(ccBody, 1, 9u);
            WriteUInt32Le(ccBody, 5, 3u);
            int offset = 9;
            Buffer.BlockCopy(ls1, 0, ccBody, offset, ls1.Length);
            offset += ls1.Length;
            Buffer.BlockCopy(nestedCc, 0, ccBody, offset, nestedCc.Length);
            offset += nestedCc.Length;
            Buffer.BlockCopy(ls3, 0, ccBody, offset, ls3.Length);

            byte[] wrapped = WrapAsMultiSurface(WrapAsCurvePolygon(ccBody));
            var ex = Assert.Throws<ParseException>(() => _wkbReader.Read(wrapped));
            Assert.That(ex.ToString(), Does.Contain("Nested COMPOUNDCURVE"));
        }

        [Test]
        public void Ticket8_GeometryCollectionOfSurfacesIsNotRewrittenAsMultiSurface()
        {
            const string wkt =
                "GEOMETRYCOLLECTION (POLYGON ((0 0, 10 0, 10 10, 0 10, 0 0)), CURVEPOLYGON (CIRCULARSTRING (20 0, 22 2, 24 0, 22 -2, 20 0)))";
            var original = _wktReader.Read(wkt);
            Assert.That(original, Is.InstanceOf<GeometryCollection>());
            Assert.That(original, Is.Not.InstanceOf<MultiSurface>());

            byte[] bytes = _wkbWriter.Write(original);
            Assert.That(ReadTypeLe(bytes, 1), Is.EqualTo(7u), "GeometryCollection stays type 7");
            Assert.That(ReadUInt32Le(bytes, 5), Is.EqualTo(2u));
            Assert.That(ReadTypeLe(bytes, 10), Is.EqualTo(3u), "Polygon member");

            var again = _wkbReader.Read(bytes);
            Assert.That(again, Is.InstanceOf<GeometryCollection>());
            Assert.That(again, Is.Not.InstanceOf<MultiSurface>());
            Assert.That(again.GetGeometryN(0), Is.InstanceOf<Polygon>());
            Assert.That(again.GetGeometryN(1), Is.InstanceOf<CurvePolygon>());
            Assert.That(again.EqualsExact(original), Is.True);
        }

        [Test]
        public void Ticket8_WriterNeverEmitsYear2TypeCodes()
        {
            string[] year1 = { WktEmpty, WktPolygon, WktCurvePolygon, WktCcShell, WktMixed, WktYear1Rings, WktZ };
            var writerZ = new WKBWriter(ByteOrder.LittleEndian, false, true);
            foreach (string wkt in year1)
            {
                byte[] bytes = writerZ.Write(_wktReader.Read(wkt));
                uint type = (ReadTypeLe(bytes, 1) & 0xFFFFu) % 1000;
                Assert.That(type, Is.EqualTo(12u), "top-level type for " + wkt);
                AssertYear1NestedTypeCodesOnly(bytes);
            }
        }

        private void AssertZmRoundTrip(string wkt, WKTWriter wktWriter, bool emitZ, bool emitM,
            uint expectedIsoType, double expectZ, double expectM)
        {
            var original = _wktReader.Read(wkt);
            var writer = new WKBWriter(ByteOrder.LittleEndian, false, emitZ, emitM);
            Assert.That(writer.Strict, Is.True);
            byte[] bytes = writer.Write(original);
            Assert.That(ReadTypeLe(bytes, 1), Is.EqualTo(expectedIsoType));

            var fromWkb = (MultiSurface)_wkbReader.Read(bytes);
            Assert.That(fromWkb.EqualsExact(original), Is.True);
            if (!original.IsEmpty)
            {
                CoordinateSequence seq = FirstMemberSequence(fromWkb);
                if (emitZ)
                    Assert.That(seq.GetZ(0), Is.EqualTo(expectZ));
                if (emitM)
                    Assert.That(seq.GetM(0), Is.EqualTo(expectM));
            }

            string emitted = wktWriter.Write(fromWkb);
            var fromWkt = _wktReader.Read(emitted);
            Assert.That(fromWkt, Is.InstanceOf<MultiSurface>());
            Assert.That(fromWkt.EqualsExact(original), Is.True);
            Assert.That(_wkbWriter.Write(fromWkb), Is.EqualTo(_wkbWriter.Write(original)));
        }

        private static CoordinateSequence FirstMemberSequence(MultiSurface multiSurface)
        {
            var first = multiSurface.GetGeometryN(0);
            if (first is CurvePolygon curvePolygon)
            {
                return curvePolygon.ExteriorRing is CircularString circular
                    ? circular.CoordinateSequence
                    : ((LineString)curvePolygon.ExteriorRing).CoordinateSequence;
            }

            return ((Polygon)first).ExteriorRing.CoordinateSequence;
        }

        private static void AssertMemberKinds(MultiSurface expected, MultiSurface actual)
        {
            Assert.That(actual.IsEmpty, Is.EqualTo(expected.IsEmpty));
            Assert.That(actual.NumGeometries, Is.EqualTo(expected.NumGeometries));
            for (int i = 0; i < expected.NumGeometries; i++)
            {
                Assert.That(actual.GetGeometryN(i).GetType(),
                    Is.EqualTo(expected.GetGeometryN(i).GetType()));
                if (expected.GetGeometryN(i) is CurvePolygon expectedCp)
                {
                    var actualCp = (CurvePolygon)actual.GetGeometryN(i);
                    Assert.That(actualCp.ExteriorRing.GetType(),
                        Is.EqualTo(expectedCp.ExteriorRing.GetType()));
                    Assert.That(actualCp.NumInteriorRings, Is.EqualTo(expectedCp.NumInteriorRings));
                    for (int r = 0; r < expectedCp.NumInteriorRings; r++)
                    {
                        Assert.That(actualCp.GetInteriorRingN(r).GetType(),
                            Is.EqualTo(expectedCp.GetInteriorRingN(r).GetType()));
                    }

                    if (expectedCp.ExteriorRing is CompoundCurve expectedCc)
                    {
                        var actualCc = (CompoundCurve)actualCp.ExteriorRing;
                        Assert.That(actualCc.Curves.Count, Is.EqualTo(expectedCc.Curves.Count));
                        for (int j = 0; j < expectedCc.Curves.Count; j++)
                        {
                            Assert.That(actualCc.Curves[j].GetType(),
                                Is.EqualTo(expectedCc.Curves[j].GetType()));
                        }
                    }
                }
            }
        }

        private static byte[] WrapAsMultiSurface(byte[] memberWkb)
        {
            var bytes = new byte[9 + memberWkb.Length];
            bytes[0] = 1;
            WriteTypeLe(bytes, 1, 12u);
            WriteUInt32Le(bytes, 5, 1u);
            Buffer.BlockCopy(memberWkb, 0, bytes, 9, memberWkb.Length);
            return bytes;
        }

        private static byte[] WrapAsCurvePolygon(byte[] ringWkb)
        {
            var bytes = new byte[9 + ringWkb.Length];
            bytes[0] = 1;
            WriteTypeLe(bytes, 1, 10u);
            WriteUInt32Le(bytes, 5, 1u);
            Buffer.BlockCopy(ringWkb, 0, bytes, 9, ringWkb.Length);
            return bytes;
        }

        /// <summary>
        /// Walks a little-endian MultiSurface WKB and asserts every nested
        /// member type is 3 or 10, and every CurvePolygon ring is 2, 8 or 9
        /// (ISO Z/M/ZM reduced via % 1000).
        /// </summary>
        private static void AssertYear1NestedTypeCodesOnly(byte[] bytes)
        {
            Assert.That(bytes[0], Is.EqualTo(1));
            uint top = (ReadTypeLe(bytes, 1) & 0xFFFFu) % 1000;
            Assert.That(top, Is.EqualTo(12u));
            bool hasSrid = (ReadTypeLe(bytes, 1) & 0x20000000u) != 0;
            int offset = hasSrid ? 9 : 5;
            uint numMembers = ReadUInt32Le(bytes, offset);
            offset += 4;
            for (uint m = 0; m < numMembers; m++)
                offset = AssertYear1SurfaceTypeAndSkip(bytes, offset);
        }

        private static int AssertYear1SurfaceTypeAndSkip(byte[] bytes, int offset)
        {
            Assert.That(bytes[offset], Is.EqualTo(1).Or.EqualTo(0));
            uint raw = ReadTypeLe(bytes, offset + 1);
            bool hasSrid = (raw & 0x20000000u) != 0;
            uint code = (raw & 0xFFFFu) % 1000;
            Assert.That(code, Is.EqualTo(3u).Or.EqualTo(10u),
                "Year-1 member type at offset " + offset);
            int header = hasSrid ? 9 : 5;
            offset += header;
            if (code == 10)
            {
                uint n = ReadUInt32Le(bytes, offset);
                offset += 4;
                for (uint i = 0; i < n; i++)
                    offset = AssertYear1CurveTypeAndSkip(bytes, offset);
                return offset;
            }

            uint numRings = ReadUInt32Le(bytes, offset);
            offset += 4;
            uint dim = (raw & 0xFFFFu) / 1000;
            int ordinateBytes = 16;
            if (dim == 1 || dim == 2) ordinateBytes = 24;
            if (dim == 3) ordinateBytes = 32;
            if ((raw & 0x80000000u) != 0 && dim == 0) ordinateBytes += 8;
            if ((raw & 0x40000000u) != 0 && dim == 0) ordinateBytes += 8;
            for (uint r = 0; r < numRings; r++)
            {
                uint numPoints = ReadUInt32Le(bytes, offset);
                offset += 4 + (int)numPoints * ordinateBytes;
            }
            return offset;
        }

        private static int AssertYear1CurveTypeAndSkip(byte[] bytes, int offset)
        {
            Assert.That(bytes[offset], Is.EqualTo(1).Or.EqualTo(0));
            uint raw = ReadTypeLe(bytes, offset + 1);
            bool hasSrid = (raw & 0x20000000u) != 0;
            uint code = (raw & 0xFFFFu) % 1000;
            Assert.That(code, Is.EqualTo(2u).Or.EqualTo(8u).Or.EqualTo(9u),
                "Year-1 ring/member type at offset " + offset);
            int header = hasSrid ? 9 : 5;
            offset += header;
            if (code == 9)
            {
                uint n = ReadUInt32Le(bytes, offset);
                offset += 4;
                for (uint i = 0; i < n; i++)
                    offset = AssertYear1CurveTypeAndSkip(bytes, offset);
                return offset;
            }

            uint numPoints = ReadUInt32Le(bytes, offset);
            offset += 4;
            uint dim = (raw & 0xFFFFu) / 1000;
            int ordinateBytes = 16;
            if (dim == 1 || dim == 2) ordinateBytes = 24;
            if (dim == 3) ordinateBytes = 32;
            if ((raw & 0x80000000u) != 0 && dim == 0) ordinateBytes += 8;
            if ((raw & 0x40000000u) != 0 && dim == 0) ordinateBytes += 8;
            return offset + (int)numPoints * ordinateBytes;
        }

        private static uint ReadTypeLe(byte[] wkb, int offset) => ReadUInt32Le(wkb, offset);

        private static uint ReadUInt32Le(byte[] wkb, int offset) =>
            (uint)(wkb[offset] | wkb[offset + 1] << 8 | wkb[offset + 2] << 16 | wkb[offset + 3] << 24);

        private static void WriteTypeLe(byte[] wkb, int offset, uint type) =>
            WriteUInt32Le(wkb, offset, type);

        private static void WriteUInt32Le(byte[] wkb, int offset, uint value)
        {
            wkb[offset] = (byte)value;
            wkb[offset + 1] = (byte)(value >> 8);
            wkb[offset + 2] = (byte)(value >> 16);
            wkb[offset + 3] = (byte)(value >> 24);
        }
    }
}
