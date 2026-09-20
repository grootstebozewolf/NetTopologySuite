// SPDX-License-Identifier: BSD-3-Clause
// AI-drafted, human-reviewed.  Assisted-by: Cursor Grok 4.6
//
// Ticket 2 — Year-1 ST_CurvePolygon WKB type 10 read/write (ISO/IEC 13249-3).
// Rings: nested WKB LineString (2) | CircularString (8) | CompoundCurve (9).
// Reuses Ticket 1 Year-1 ring validation; does not remint WKT grammar.
// Ticket 3: Year-1 WKB 10 is complete (no longer "partial"); Year-2 ring names stay omitted.

using System;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Curves;
using NetTopologySuite.IO;
using NUnit.Framework;

namespace NetTopologySuite.Tests.NUnit.Geometries.Curves
{
    /// <summary>
    /// Year-1 <c>CURVEPOLYGON</c> WKB type 10 (Ticket 2 / FCP-WKB).
    /// </summary>
    [Category("CurveAwareness")]
    public class CurvePolygonWkbYear1Test
    {
        private readonly WKTReader _wktReader = new WKTReader();
        private readonly WKTWriter _wktWriter = new WKTWriter();
        private readonly WKTWriter _wktWriterZ = new WKTWriter(3);
        private readonly WKTWriter _wktWriterZm = new WKTWriter(4);
        private readonly WKBReader _wkbReader = new WKBReader();
        private readonly WKBWriter _wkbWriter = new WKBWriter();

        // Ticket 1 fixtures reused as WKB witnesses (no WKT remint).
        private const string WktEmpty = "CURVEPOLYGON EMPTY";
        private const string WktLsShell = "CURVEPOLYGON ((0 0, 10 0, 10 10, 0 10, 0 0))";
        private const string WktMultiLs = "CURVEPOLYGON ((0 0, 10 0, 10 10, 0 10, 0 0), (2 2, 8 2, 8 8, 2 8, 2 2))";
        private const string WktCsShell = "CURVEPOLYGON (CIRCULARSTRING (0 0, 2 2, 4 0, 2 -2, 0 0))";
        private const string WktCcShell =
            "CURVEPOLYGON (COMPOUNDCURVE (CIRCULARSTRING (0 0, 5 5, 10 0), (10 0, 0 0)))";
        private const string WktLsShellCsHole =
            "CURVEPOLYGON ((0 0, 100 0, 100 100, 0 100, 0 0), CIRCULARSTRING (40 50, 50 60, 60 50, 50 40, 40 50))";
        private const string WktZ =
            "CURVEPOLYGON Z (CIRCULARSTRING (0 0 5, 2 2 5, 4 0 5, 2 -2 5, 0 0 5))";
        private const string WktM =
            "CURVEPOLYGON M (CIRCULARSTRING (0 0 7, 2 2 7, 4 0 7, 2 -2 7, 0 0 7))";
        private const string WktZm =
            "CURVEPOLYGON ZM (CIRCULARSTRING ZM (0 0 5 7, 2 2 5 7, 4 0 5 7, 2 -2 5 7, 0 0 5 7))";

        // GEOS / ISO little-endian type-10 fixtures, hand-checked against the
        // nested WKB layout (byte order, type, numRings, then nested 2|8|9).
        // LS shell + CS hole — same coordinates as WktLsShellCsHole.
        private const string HexLsShellCsHole =
            "010A00000002000000" +
            "010200000005000000" +
            "00000000000000000000000000000000" +
            "00000000000059400000000000000000" +
            "00000000000059400000000000005940" +
            "00000000000000000000000000005940" +
            "00000000000000000000000000000000" +
            "010800000005000000" +
            "00000000000044400000000000004940" +
            "00000000000049400000000000004E40" +
            "0000000000004E400000000000004940" +
            "00000000000049400000000000004440" +
            "00000000000044400000000000004940";

        // CC shell — same coordinates as WktCcShell.
        private const string HexCcShell =
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
        public void Ticket2_WkbTypeCodeIsTen()
        {
            Assert.That((int)WKBGeometryTypes.WKBCurvePolygon, Is.EqualTo(10));
            byte[] bytes = _wkbWriter.Write(_wktReader.Read(WktLsShell));
            Assert.That(bytes[0], Is.EqualTo(1), "little-endian");
            Assert.That(ReadTypeLe(bytes, 1), Is.EqualTo(10u));
        }

        [TestCase(WktEmpty, Description = "FCP-WKB Ticket2 EMPTY")]
        [TestCase(WktLsShell, Description = "FCP-WKB Ticket2 one-ring LS")]
        [TestCase(WktMultiLs, Description = "FCP-WKB Ticket2 multi-ring LS")]
        [TestCase(WktCsShell, Description = "FCP-WKB Ticket2 CS shell")]
        [TestCase(WktCcShell, Description = "FCP-WKB Ticket2 CC shell")]
        [TestCase(WktLsShellCsHole, Description = "FCP-WKB Ticket2 LS shell + CS hole")]
        public void Ticket2_WkbObjectWkbPreservesEqualsExact(string wkt)
        {
            var original = _wktReader.Read(wkt);
            byte[] bytes = _wkbWriter.Write(original);
            var again = _wkbReader.Read(bytes);
            Assert.That(again, Is.InstanceOf<CurvePolygon>());
            Assert.That(again.GetType(), Is.EqualTo(original.GetType()));
            Assert.That(again.EqualsExact(original), Is.True, WKBWriter.ToHex(bytes));
            Assert.That(_wkbWriter.Write(again), Is.EqualTo(bytes));
        }

        [TestCase(WktEmpty)]
        [TestCase(WktLsShell)]
        [TestCase(WktMultiLs)]
        [TestCase(WktCsShell)]
        [TestCase(WktCcShell)]
        [TestCase(WktLsShellCsHole)]
        public void Ticket2_WktWkbWktPreservesRingSubtypes(string wkt)
        {
            var original = (CurvePolygon)_wktReader.Read(wkt);
            byte[] bytes = _wkbWriter.Write(original);
            var fromWkb = (CurvePolygon)_wkbReader.Read(bytes);
            AssertRingKinds(original, fromWkb);

            string emitted = _wktWriter.Write(fromWkb);
            var fromWkt = (CurvePolygon)_wktReader.Read(emitted);
            AssertRingKinds(original, fromWkt);
            Assert.That(fromWkt.EqualsExact(original), Is.True, emitted);
        }

        [Test]
        public void Ticket2_FCP_WKB_LsShellCsHoleByteLevelFixture()
        {
            var fromWkt = (CurvePolygon)_wktReader.Read(WktLsShellCsHole);
            byte[] written = _wkbWriter.Write(fromWkt);
            Assert.That(WKBWriter.ToHex(written), Is.EqualTo(HexLsShellCsHole));

            Assert.That(written[0], Is.EqualTo(1));
            Assert.That(ReadTypeLe(written, 1), Is.EqualTo(10u));
            Assert.That(ReadUInt32Le(written, 5), Is.EqualTo(2u), "two rings");
            Assert.That(written[9], Is.EqualTo(1));
            Assert.That(ReadTypeLe(written, 10), Is.EqualTo(2u), "LS shell");
            int holeOffset = 9 + 5 + 4 + 5 * 16;
            Assert.That(written[holeOffset], Is.EqualTo(1));
            Assert.That(ReadTypeLe(written, holeOffset + 1), Is.EqualTo(8u), "CS hole");

            var fromHex = (CurvePolygon)_wkbReader.Read(WKBReader.HexToBytes(HexLsShellCsHole));
            Assert.That(fromHex.ExteriorRing, Is.InstanceOf<LineString>());
            Assert.That(fromHex.ExteriorRing, Is.Not.InstanceOf<CircularString>());
            Assert.That(fromHex.GetInteriorRingN(0), Is.InstanceOf<CircularString>());
            Assert.That(fromHex.EqualsExact(fromWkt), Is.True);
        }

        [Test]
        public void Ticket2_FCP_WKB_CcShellByteLevelFixture()
        {
            var fromWkt = (CurvePolygon)_wktReader.Read(WktCcShell);
            byte[] written = _wkbWriter.Write(fromWkt);
            Assert.That(WKBWriter.ToHex(written), Is.EqualTo(HexCcShell));

            Assert.That(ReadTypeLe(written, 1), Is.EqualTo(10u));
            Assert.That(ReadUInt32Le(written, 5), Is.EqualTo(1u), "one ring");
            Assert.That(ReadTypeLe(written, 10), Is.EqualTo(9u), "CC shell");
            Assert.That(ReadUInt32Le(written, 14), Is.EqualTo(2u), "two CC members");
            Assert.That(ReadTypeLe(written, 19), Is.EqualTo(8u), "CS member");
            int lsMemberOffset = 18 + 5 + 4 + 3 * 16;
            Assert.That(ReadTypeLe(written, lsMemberOffset + 1), Is.EqualTo(2u), "LS member");

            var fromHex = (CurvePolygon)_wkbReader.Read(WKBReader.HexToBytes(HexCcShell));
            Assert.That(fromHex.ExteriorRing, Is.InstanceOf<CompoundCurve>());
            var shell = (CompoundCurve)fromHex.ExteriorRing;
            Assert.That(shell.Curves[0], Is.InstanceOf<CircularString>());
            Assert.That(shell.Curves[1], Is.InstanceOf<LineString>());
            Assert.That(fromHex.EqualsExact(fromWkt), Is.True);
        }

        [Test]
        public void Ticket2_EmptyWritesZeroRings()
        {
            byte[] bytes = _wkbWriter.Write(_wktReader.Read(WktEmpty));
            Assert.That(ReadTypeLe(bytes, 1), Is.EqualTo(10u));
            Assert.That(ReadUInt32Le(bytes, 5), Is.EqualTo(0u));
            var again = (CurvePolygon)_wkbReader.Read(bytes);
            Assert.That(again.IsEmpty, Is.True);
        }

        [Test]
        public void Ticket2_IsoZmTypeCodesReadAndWrite()
        {
            // Same +1000/+2000/+3000 table already used for types 8–9.
            // Strict writer emits ISO codes (no EWKB high bits).
            AssertZmRoundTrip(WktZ, _wktWriterZ, emitZ: true, emitM: false, expectedIsoType: 1010u, expectZ: 5, expectM: double.NaN);
            AssertZmRoundTrip(WktM, _wktWriterZm, emitZ: false, emitM: true, expectedIsoType: 2010u, expectZ: double.NaN, expectM: 7);
            AssertZmRoundTrip(WktZm, _wktWriterZm, emitZ: true, emitM: true, expectedIsoType: 3010u, expectZ: 5, expectM: 7);
        }

        [Test]
        public void Ticket2_IsoZmTypeCodesAcceptedOnReadWhenPatched()
        {
            var xy = _wktReader.Read(WktCsShell);
            byte[] bytes = _wkbWriter.Write(xy);
            Assert.That(ReadTypeLe(bytes, 1), Is.EqualTo(10u));

            foreach (uint isoType in new uint[] { 1010u, 2010u, 3010u })
            {
                byte[] patched = (byte[])bytes.Clone();
                WriteTypeLe(patched, 1, isoType);
                var again = _wkbReader.Read(patched);
                Assert.That(again, Is.InstanceOf<CurvePolygon>(), "ISO type " + isoType);
                Assert.That(((CurvePolygon)again).ExteriorRing, Is.InstanceOf<CircularString>());
            }
        }

        [Test]
        public void Ticket2_EwkbSridPathMatchesPolygon()
        {
            var curvePolygon = (CurvePolygon)_wktReader.Read(WktLsShell);
            curvePolygon.SRID = 4326;
            var polygon = (Polygon)_wktReader.Read("POLYGON ((0 0, 10 0, 10 10, 0 10, 0 0))");
            polygon.SRID = 4326;

            var ewkbWriter = new WKBWriter(ByteOrder.LittleEndian, true);
            byte[] curveBytes = ewkbWriter.Write(curvePolygon);
            byte[] polygonBytes = ewkbWriter.Write(polygon);

            uint curveType = ReadTypeLe(curveBytes, 1);
            uint polygonType = ReadTypeLe(polygonBytes, 1);
            Assert.That((curveType & 0x20000000u) != 0, Is.True, "EWKB SRID flag on CurvePolygon");
            Assert.That((polygonType & 0x20000000u) != 0, Is.True, "EWKB SRID flag on Polygon");
            Assert.That(curveType & 0x0FFFFFFFu, Is.EqualTo(10u));
            Assert.That(polygonType & 0x0FFFFFFFu, Is.EqualTo(3u));
            Assert.That(BitConverter.ToInt32(curveBytes, 5), Is.EqualTo(4326));
            Assert.That(BitConverter.ToInt32(polygonBytes, 5), Is.EqualTo(4326));

            var again = (CurvePolygon)_wkbReader.Read(curveBytes);
            Assert.That(again.SRID, Is.EqualTo(4326));
            Assert.That(again.ExteriorRing, Is.InstanceOf<LineString>());
            Assert.That(again.EqualsExact(curvePolygon), Is.True);
        }

        [Test]
        public void Ticket2_Table15AlternateCodeReadsAsType10()
        {
            byte[] bytes = _wkbWriter.Write(_wktReader.Read(WktCsShell));
            Assert.That(ReadTypeLe(bytes, 1), Is.EqualTo(10u), "writer emits base 10, never 1000003");
            WriteTypeLe(bytes, 1, 1000003u);
            var again = _wkbReader.Read(bytes);
            Assert.That(again, Is.InstanceOf<CurvePolygon>());
            Assert.That(((CurvePolygon)again).ExteriorRing, Is.InstanceOf<CircularString>());
        }

        [TestCase(1u, Description = "nested Point")]
        [TestCase(3u, Description = "nested Polygon")]
        [TestCase(4u, Description = "nested MultiPoint")]
        [TestCase(7u, Description = "nested GeometryCollection")]
        [TestCase(11u, Description = "nested MultiCurve")]
        [TestCase(12u, Description = "nested MultiSurface")]
        [TestCase(13u, Description = "unknown type 13")]
        [TestCase(18u, Description = "unknown type 18 (not a Year-2 invention)")]
        public void Ticket2_RejectsNonYear1NestedRingTypeCode(uint nestedType)
        {
            byte[] member = _wkbWriter.Write(_wktReader.Read("LINESTRING (0 0, 10 0, 10 10, 0 10, 0 0)"));
            WriteTypeLe(member, 1, nestedType);
            byte[] wrapped = WrapAsCurvePolygon(member);
            var ex = Assert.Throws<ParseException>(() => _wkbReader.Read(wrapped));
            Assert.That(ex.ToString(), Does.Contain("Year-1"));
            Assert.That(ex.ToString(), Does.Contain(nestedType.ToString()));
        }

        [Test]
        public void Ticket2_RejectsNestedCompoundCurveInsideRing()
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

            byte[] wrapped = WrapAsCurvePolygon(ccBody);
            var ex = Assert.Throws<ParseException>(() => _wkbReader.Read(wrapped));
            Assert.That(ex.ToString(), Does.Contain("Nested COMPOUNDCURVE"));
        }

        [Test]
        public void Ticket2_StandaloneCompoundCurveStillFlattensNestedMembers()
        {
            // ADR-0005 is unchanged for standalone CompoundCurve (not a ring).
            var cc = (CompoundCurve)_wktReader.Read(
                "COMPOUNDCURVE ((0 0, 1 0), COMPOUNDCURVE ((1 0, 1 1)), (1 1, 2 1))");
            Assert.That(cc.Curves.Count, Is.EqualTo(3));
            byte[] bytes = _wkbWriter.Write(cc);
            var again = (CompoundCurve)_wkbReader.Read(bytes);
            Assert.That(again.Curves.Count, Is.EqualTo(3));
            Assert.That(again.EqualsExact(cc), Is.True);
        }

        [Test]
        public void Ticket2_RejectsUnclosedLineStringRing()
        {
            byte[] member = _wkbWriter.Write(_wktReader.Read("LINESTRING (0 0, 10 0, 10 10, 0 10)"));
            Assert.Throws<ParseException>(() => _wkbReader.Read(WrapAsCurvePolygon(member)));
        }

        [Test]
        public void Ticket2_WriterNeverEmitsYear2TypeCodes()
        {
            string[] year1 = { WktEmpty, WktLsShell, WktCsShell, WktCcShell, WktLsShellCsHole, WktZ };
            var writerZ = new WKBWriter(ByteOrder.LittleEndian, false, true);
            foreach (string wkt in year1)
            {
                byte[] bytes = writerZ.Write(_wktReader.Read(wkt));
                uint type = (ReadTypeLe(bytes, 1) & 0xFFFFu) % 1000;
                Assert.That(type, Is.EqualTo(10u), "top-level type for " + wkt);
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

            var fromWkb = (CurvePolygon)_wkbReader.Read(bytes);
            Assert.That(fromWkb.EqualsExact(original), Is.True);
            if (!original.IsEmpty)
            {
                var seq = ((CircularString)fromWkb.ExteriorRing).CoordinateSequence;
                if (emitZ)
                    Assert.That(seq.GetZ(0), Is.EqualTo(expectZ));
                if (emitM)
                    Assert.That(seq.GetM(0), Is.EqualTo(expectM));
            }

            string emitted = wktWriter.Write(fromWkb);
            var fromWkt = _wktReader.Read(emitted);
            Assert.That(fromWkt, Is.InstanceOf<CurvePolygon>());
            Assert.That(fromWkt.EqualsExact(original), Is.True);
            Assert.That(_wkbWriter.Write(fromWkb), Is.EqualTo(_wkbWriter.Write(original)));
        }

        private static void AssertRingKinds(CurvePolygon expected, CurvePolygon actual)
        {
            Assert.That(actual.IsEmpty, Is.EqualTo(expected.IsEmpty));
            if (expected.IsEmpty)
                return;
            Assert.That(actual.ExteriorRing.GetType(), Is.EqualTo(expected.ExteriorRing.GetType()));
            Assert.That(actual.NumInteriorRings, Is.EqualTo(expected.NumInteriorRings));
            for (int i = 0; i < expected.NumInteriorRings; i++)
            {
                Assert.That(actual.GetInteriorRingN(i).GetType(),
                    Is.EqualTo(expected.GetInteriorRingN(i).GetType()));
            }

            if (expected.ExteriorRing is CompoundCurve expectedCc)
            {
                var actualCc = (CompoundCurve)actual.ExteriorRing;
                Assert.That(actualCc.Curves.Count, Is.EqualTo(expectedCc.Curves.Count));
                for (int i = 0; i < expectedCc.Curves.Count; i++)
                {
                    Assert.That(actualCc.Curves[i].GetType(), Is.EqualTo(expectedCc.Curves[i].GetType()));
                }
            }
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
        /// Walks a little-endian CurvePolygon WKB and asserts every nested
        /// geometry type is 2, 8 or 9 (ISO Z/M/ZM reduced via % 1000).
        /// </summary>
        private static void AssertYear1NestedTypeCodesOnly(byte[] bytes)
        {
            Assert.That(bytes[0], Is.EqualTo(1));
            uint top = (ReadTypeLe(bytes, 1) & 0xFFFFu) % 1000;
            Assert.That(top, Is.EqualTo(10u));
            bool hasSrid = (ReadTypeLe(bytes, 1) & 0x20000000u) != 0;
            int offset = hasSrid ? 9 : 5;
            uint numRings = ReadUInt32Le(bytes, offset);
            offset += 4;
            for (uint r = 0; r < numRings; r++)
                offset = AssertYear1CurveTypeAndSkip(bytes, offset);
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

            // LineString / CircularString: numPoints + coordinates (ignore body).
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
