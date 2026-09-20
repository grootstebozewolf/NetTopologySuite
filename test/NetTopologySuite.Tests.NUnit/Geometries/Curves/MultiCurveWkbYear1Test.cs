// SPDX-License-Identifier: BSD-3-Clause
// AI-drafted, human-reviewed.  Assisted-by: Cursor Grok 4.6
//
// Ticket 5 — Year-1 ST_MultiCurve WKB type 11 read/write (ISO/IEC 13249-3).
// Members: nested WKB LineString (2) | CircularString (8) | CompoundCurve (9).
// Reuses Ticket 4 Year-1 member lock; does not remint WKT grammar.
// Ticket 6: Year-1 WKB 11 is complete (no longer "partial"); Year-2 member names stay omitted.

using System;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Curves;
using NetTopologySuite.IO;
using NUnit.Framework;

namespace NetTopologySuite.Tests.NUnit.Geometries.Curves
{
    /// <summary>
    /// Year-1 <c>MULTICURVE</c> WKB type 11 (Ticket 5).
    /// </summary>
    [Category("CurveAwareness")]
    public class MultiCurveWkbYear1Test
    {
        private readonly WKTReader _wktReader = new WKTReader();
        private readonly WKTWriter _wktWriter = new WKTWriter();
        private readonly WKTWriter _wktWriterZ = new WKTWriter(3);
        private readonly WKTWriter _wktWriterZm = new WKTWriter(4);
        private readonly WKBReader _wkbReader = new WKBReader();
        private readonly WKBWriter _wkbWriter = new WKBWriter();

        // Ticket 4 fixtures reused as WKB witnesses (no WKT remint).
        private const string WktEmpty = "MULTICURVE EMPTY";
        private const string WktLs = "MULTICURVE ((0 0, 1 0))";
        private const string WktCs = "MULTICURVE (CIRCULARSTRING (0 0, 1 1, 2 0))";
        private const string WktCc =
            "MULTICURVE (COMPOUNDCURVE (CIRCULARSTRING (0 0, 1 1, 2 0), (2 0, 3 0)))";
        private const string WktMixedLsCs =
            "MULTICURVE ((0 0, 1 0), CIRCULARSTRING (1 0, 2 1, 3 0))";
        private const string WktMixedLsCsCc =
            "MULTICURVE ((0 0, 1 0), CIRCULARSTRING (1 0, 2 1, 3 0), COMPOUNDCURVE ((3 0, 4 0), CIRCULARSTRING (4 0, 5 1, 6 0)))";
        private const string WktZ =
            "MULTICURVE Z (CIRCULARSTRING (0 0 5, 1 1 5, 2 0 5), (3 0 5, 4 0 5))";
        private const string WktM =
            "MULTICURVE M (CIRCULARSTRING (0 0 7, 1 1 7, 2 0 7), (3 0 7, 4 0 7))";
        private const string WktZm =
            "MULTICURVE ZM (CIRCULARSTRING ZM (0 0 5 7, 1 1 5 7, 2 0 5 7), (3 0 5 7, 4 0 5 7))";

        // GEOS / ISO little-endian type-11 fixtures, hand-checked against the
        // nested WKB layout (byte order, type, numMembers, then nested 2|8|9).
        // Mixed LS + CS — same coordinates as WktMixedLsCs.
        private const string HexMixedLsCs =
            "010B00000002000000" +
            "010200000002000000" +
            "00000000000000000000000000000000" +
            "000000000000F03F0000000000000000" +
            "010800000003000000" +
            "000000000000F03F0000000000000000" +
            "0000000000000040000000000000F03F" +
            "00000000000008400000000000000000";

        // CC-only — same coordinates as WktCc.
        private const string HexCcOnly =
            "010B00000001000000" +
            "010900000002000000" +
            "010800000003000000" +
            "00000000000000000000000000000000" +
            "000000000000F03F000000000000F03F" +
            "00000000000000400000000000000000" +
            "010200000002000000" +
            "00000000000000400000000000000000" +
            "00000000000008400000000000000000";

        [Test]
        public void Ticket5_WkbTypeCodeIsEleven()
        {
            Assert.That((int)WKBGeometryTypes.WKBMultiCurve, Is.EqualTo(11));
            byte[] bytes = _wkbWriter.Write(_wktReader.Read(WktLs));
            Assert.That(bytes[0], Is.EqualTo(1), "little-endian");
            Assert.That(ReadTypeLe(bytes, 1), Is.EqualTo(11u));
        }

        [TestCase(WktEmpty, Description = "Ticket5 EMPTY")]
        [TestCase(WktLs, Description = "Ticket5 one-member LS")]
        [TestCase(WktCs, Description = "Ticket5 one-member CS")]
        [TestCase(WktCc, Description = "Ticket5 one-member CC")]
        [TestCase(WktMixedLsCs, Description = "Ticket5 mixed LS+CS")]
        [TestCase(WktMixedLsCsCc, Description = "Ticket5 mixed LS+CS+CC")]
        public void Ticket5_WkbObjectWkbPreservesEqualsExact(string wkt)
        {
            var original = _wktReader.Read(wkt);
            byte[] bytes = _wkbWriter.Write(original);
            var again = _wkbReader.Read(bytes);
            Assert.That(again, Is.InstanceOf<MultiCurve>());
            Assert.That(again.GetType(), Is.EqualTo(original.GetType()));
            Assert.That(again.EqualsExact(original), Is.True, WKBWriter.ToHex(bytes));
            Assert.That(_wkbWriter.Write(again), Is.EqualTo(bytes));
        }

        [TestCase(WktEmpty)]
        [TestCase(WktLs)]
        [TestCase(WktCs)]
        [TestCase(WktCc)]
        [TestCase(WktMixedLsCs)]
        [TestCase(WktMixedLsCsCc)]
        public void Ticket5_WktWkbWktPreservesMemberSubtypes(string wkt)
        {
            var original = (MultiCurve)_wktReader.Read(wkt);
            byte[] bytes = _wkbWriter.Write(original);
            var fromWkb = (MultiCurve)_wkbReader.Read(bytes);
            AssertMemberKinds(original, fromWkb);

            string emitted = _wktWriter.Write(fromWkb);
            var fromWkt = (MultiCurve)_wktReader.Read(emitted);
            AssertMemberKinds(original, fromWkt);
            Assert.That(fromWkt.EqualsExact(original), Is.True, emitted);
        }

        [Test]
        public void Ticket5_MixedLsCsByteLevelFixture()
        {
            var fromWkt = (MultiCurve)_wktReader.Read(WktMixedLsCs);
            byte[] written = _wkbWriter.Write(fromWkt);
            Assert.That(WKBWriter.ToHex(written), Is.EqualTo(HexMixedLsCs));

            Assert.That(written[0], Is.EqualTo(1));
            Assert.That(ReadTypeLe(written, 1), Is.EqualTo(11u));
            Assert.That(ReadUInt32Le(written, 5), Is.EqualTo(2u), "two members");
            Assert.That(written[9], Is.EqualTo(1));
            Assert.That(ReadTypeLe(written, 10), Is.EqualTo(2u), "LS member");
            int csOffset = 9 + 5 + 4 + 2 * 16;
            Assert.That(written[csOffset], Is.EqualTo(1));
            Assert.That(ReadTypeLe(written, csOffset + 1), Is.EqualTo(8u), "CS member");

            var fromHex = (MultiCurve)_wkbReader.Read(WKBReader.HexToBytes(HexMixedLsCs));
            Assert.That(fromHex.GetGeometryN(0), Is.InstanceOf<LineString>());
            Assert.That(fromHex.GetGeometryN(0), Is.Not.InstanceOf<CircularString>());
            Assert.That(fromHex.GetGeometryN(1), Is.InstanceOf<CircularString>());
            Assert.That(fromHex.EqualsExact(fromWkt), Is.True);
        }

        [Test]
        public void Ticket5_CcOnlyByteLevelFixture()
        {
            var fromWkt = (MultiCurve)_wktReader.Read(WktCc);
            byte[] written = _wkbWriter.Write(fromWkt);
            Assert.That(WKBWriter.ToHex(written), Is.EqualTo(HexCcOnly));

            Assert.That(ReadTypeLe(written, 1), Is.EqualTo(11u));
            Assert.That(ReadUInt32Le(written, 5), Is.EqualTo(1u), "one member");
            Assert.That(ReadTypeLe(written, 10), Is.EqualTo(9u), "CC member");
            Assert.That(ReadUInt32Le(written, 14), Is.EqualTo(2u), "two CC members");
            Assert.That(ReadTypeLe(written, 19), Is.EqualTo(8u), "CS component");
            int lsMemberOffset = 18 + 5 + 4 + 3 * 16;
            Assert.That(ReadTypeLe(written, lsMemberOffset + 1), Is.EqualTo(2u), "LS component");

            var fromHex = (MultiCurve)_wkbReader.Read(WKBReader.HexToBytes(HexCcOnly));
            Assert.That(fromHex.GetGeometryN(0), Is.InstanceOf<CompoundCurve>());
            var compound = (CompoundCurve)fromHex.GetGeometryN(0);
            Assert.That(compound.Curves[0], Is.InstanceOf<CircularString>());
            Assert.That(compound.Curves[1], Is.InstanceOf<LineString>());
            Assert.That(fromHex.EqualsExact(fromWkt), Is.True);
        }

        [Test]
        public void Ticket5_EmptyWritesZeroMembers()
        {
            byte[] bytes = _wkbWriter.Write(_wktReader.Read(WktEmpty));
            Assert.That(ReadTypeLe(bytes, 1), Is.EqualTo(11u));
            Assert.That(ReadUInt32Le(bytes, 5), Is.EqualTo(0u));
            var again = (MultiCurve)_wkbReader.Read(bytes);
            Assert.That(again.IsEmpty, Is.True);
        }

        [Test]
        public void Ticket5_IsoZmTypeCodesReadAndWrite()
        {
            // Same +1000/+2000/+3000 table already used for types 8–10.
            // Strict writer emits ISO codes (no EWKB high bits).
            AssertZmRoundTrip(WktZ, _wktWriterZ, emitZ: true, emitM: false, expectedIsoType: 1011u, expectZ: 5, expectM: double.NaN);
            AssertZmRoundTrip(WktM, _wktWriterZm, emitZ: false, emitM: true, expectedIsoType: 2011u, expectZ: double.NaN, expectM: 7);
            AssertZmRoundTrip(WktZm, _wktWriterZm, emitZ: true, emitM: true, expectedIsoType: 3011u, expectZ: 5, expectM: 7);
        }

        [Test]
        public void Ticket5_IsoZmTypeCodesAcceptedOnReadWhenPatched()
        {
            var xy = _wktReader.Read(WktCs);
            byte[] bytes = _wkbWriter.Write(xy);
            Assert.That(ReadTypeLe(bytes, 1), Is.EqualTo(11u));

            foreach (uint isoType in new uint[] { 1011u, 2011u, 3011u })
            {
                byte[] patched = (byte[])bytes.Clone();
                WriteTypeLe(patched, 1, isoType);
                var again = _wkbReader.Read(patched);
                Assert.That(again, Is.InstanceOf<MultiCurve>(), "ISO type " + isoType);
                Assert.That(((MultiCurve)again).GetGeometryN(0), Is.InstanceOf<CircularString>());
            }
        }

        [Test]
        public void Ticket5_EwkbSridPathMatchesMultiLineString()
        {
            var multiCurve = (MultiCurve)_wktReader.Read(WktLs);
            multiCurve.SRID = 4326;
            var multiLineString = (MultiLineString)_wktReader.Read("MULTILINESTRING ((0 0, 1 0))");
            multiLineString.SRID = 4326;

            var ewkbWriter = new WKBWriter(ByteOrder.LittleEndian, true);
            byte[] curveBytes = ewkbWriter.Write(multiCurve);
            byte[] lineBytes = ewkbWriter.Write(multiLineString);

            uint curveType = ReadTypeLe(curveBytes, 1);
            uint lineType = ReadTypeLe(lineBytes, 1);
            Assert.That((curveType & 0x20000000u) != 0, Is.True, "EWKB SRID flag on MultiCurve");
            Assert.That((lineType & 0x20000000u) != 0, Is.True, "EWKB SRID flag on MultiLineString");
            Assert.That(curveType & 0x0FFFFFFFu, Is.EqualTo(11u));
            Assert.That(lineType & 0x0FFFFFFFu, Is.EqualTo(5u));
            Assert.That(BitConverter.ToInt32(curveBytes, 5), Is.EqualTo(4326));
            Assert.That(BitConverter.ToInt32(lineBytes, 5), Is.EqualTo(4326));

            var again = (MultiCurve)_wkbReader.Read(curveBytes);
            Assert.That(again.SRID, Is.EqualTo(4326));
            Assert.That(again.GetGeometryN(0), Is.InstanceOf<LineString>());
            Assert.That(again.EqualsExact(multiCurve), Is.True);
        }

        [Test]
        public void Ticket5_Table15AlternateCodeReadsAsType11()
        {
            byte[] bytes = _wkbWriter.Write(_wktReader.Read(WktCs));
            Assert.That(ReadTypeLe(bytes, 1), Is.EqualTo(11u), "writer emits base 11, never 1000004");
            WriteTypeLe(bytes, 1, 1000004u);
            var again = _wkbReader.Read(bytes);
            Assert.That(again, Is.InstanceOf<MultiCurve>());
            Assert.That(((MultiCurve)again).GetGeometryN(0), Is.InstanceOf<CircularString>());
        }

        [TestCase(1u, Description = "nested Point")]
        [TestCase(3u, Description = "nested Polygon")]
        [TestCase(4u, Description = "nested MultiPoint")]
        [TestCase(5u, Description = "nested MultiLineString")]
        [TestCase(7u, Description = "nested GeometryCollection")]
        [TestCase(10u, Description = "nested CurvePolygon")]
        [TestCase(11u, Description = "nested MultiCurve")]
        [TestCase(12u, Description = "nested MultiSurface")]
        [TestCase(13u, Description = "unknown type 13")]
        [TestCase(19u, Description = "unknown type 19 (Year-2 curve still refused)")]
        public void Ticket5_RejectsNonYear1NestedMemberTypeCode(uint nestedType)
        {
            byte[] member = _wkbWriter.Write(_wktReader.Read("LINESTRING (0 0, 1 0)"));
            WriteTypeLe(member, 1, nestedType);
            byte[] wrapped = WrapAsMultiCurve(member);
            var ex = Assert.Throws<ParseException>(() => _wkbReader.Read(wrapped));
            Assert.That(ex.ToString(), Does.Contain("Year-1"));
            Assert.That(ex.ToString(), Does.Contain(nestedType.ToString()));
        }

        [Test]
        public void Ticket5_RejectsNestedCompoundCurveInsideMember()
        {
            byte[] ls1 = _wkbWriter.Write(_wktReader.Read("LINESTRING (0 0, 1 0)"));
            byte[] nestedCc = _wkbWriter.Write(_wktReader.Read("COMPOUNDCURVE ((1 0, 2 0))"));

            var ccBody = new byte[5 + 4 + ls1.Length + nestedCc.Length];
            ccBody[0] = 1;
            WriteTypeLe(ccBody, 1, 9u);
            WriteUInt32Le(ccBody, 5, 2u);
            int offset = 9;
            Buffer.BlockCopy(ls1, 0, ccBody, offset, ls1.Length);
            offset += ls1.Length;
            Buffer.BlockCopy(nestedCc, 0, ccBody, offset, nestedCc.Length);

            byte[] wrapped = WrapAsMultiCurve(ccBody);
            var ex = Assert.Throws<ParseException>(() => _wkbReader.Read(wrapped));
            Assert.That(ex.ToString(), Does.Contain("Nested COMPOUNDCURVE"));
        }

        [Test]
        public void Ticket5_StandaloneCompoundCurveStillFlattensNestedMembers()
        {
            // ADR-0005 is unchanged for standalone CompoundCurve (not a MultiCurve member).
            var cc = (CompoundCurve)_wktReader.Read(
                "COMPOUNDCURVE ((0 0, 1 0), COMPOUNDCURVE ((1 0, 1 1)), (1 1, 2 1))");
            Assert.That(cc.Curves.Count, Is.EqualTo(3));
            byte[] bytes = _wkbWriter.Write(cc);
            var again = (CompoundCurve)_wkbReader.Read(bytes);
            Assert.That(again.Curves.Count, Is.EqualTo(3));
            Assert.That(again.EqualsExact(cc), Is.True);
        }

        [Test]
        public void Ticket5_WriterNeverEmitsYear2TypeCodes()
        {
            string[] year1 = { WktEmpty, WktLs, WktCs, WktCc, WktMixedLsCs, WktMixedLsCsCc, WktZ };
            var writerZ = new WKBWriter(ByteOrder.LittleEndian, false, true);
            foreach (string wkt in year1)
            {
                byte[] bytes = writerZ.Write(_wktReader.Read(wkt));
                uint type = (ReadTypeLe(bytes, 1) & 0xFFFFu) % 1000;
                Assert.That(type, Is.EqualTo(11u), "top-level type for " + wkt);
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

            var fromWkb = (MultiCurve)_wkbReader.Read(bytes);
            Assert.That(fromWkb.EqualsExact(original), Is.True);
            if (!original.IsEmpty)
            {
                var first = fromWkb.GetGeometryN(0);
                CoordinateSequence seq = first is CircularString circular
                    ? circular.CoordinateSequence
                    : ((LineString)first).CoordinateSequence;
                if (emitZ)
                    Assert.That(seq.GetZ(0), Is.EqualTo(expectZ));
                if (emitM)
                    Assert.That(seq.GetM(0), Is.EqualTo(expectM));
            }

            string emitted = wktWriter.Write(fromWkb);
            var fromWkt = _wktReader.Read(emitted);
            Assert.That(fromWkt, Is.InstanceOf<MultiCurve>());
            Assert.That(fromWkt.EqualsExact(original), Is.True);
            Assert.That(_wkbWriter.Write(fromWkb), Is.EqualTo(_wkbWriter.Write(original)));
        }

        private static void AssertMemberKinds(MultiCurve expected, MultiCurve actual)
        {
            Assert.That(actual.IsEmpty, Is.EqualTo(expected.IsEmpty));
            Assert.That(actual.NumGeometries, Is.EqualTo(expected.NumGeometries));
            for (int i = 0; i < expected.NumGeometries; i++)
            {
                Assert.That(actual.GetGeometryN(i).GetType(),
                    Is.EqualTo(expected.GetGeometryN(i).GetType()));
                if (expected.GetGeometryN(i) is CompoundCurve expectedCc)
                {
                    var actualCc = (CompoundCurve)actual.GetGeometryN(i);
                    Assert.That(actualCc.Curves.Count, Is.EqualTo(expectedCc.Curves.Count));
                    for (int j = 0; j < expectedCc.Curves.Count; j++)
                    {
                        Assert.That(actualCc.Curves[j].GetType(),
                            Is.EqualTo(expectedCc.Curves[j].GetType()));
                    }
                }
            }
        }

        private static byte[] WrapAsMultiCurve(byte[] memberWkb)
        {
            var bytes = new byte[9 + memberWkb.Length];
            bytes[0] = 1;
            WriteTypeLe(bytes, 1, 11u);
            WriteUInt32Le(bytes, 5, 1u);
            Buffer.BlockCopy(memberWkb, 0, bytes, 9, memberWkb.Length);
            return bytes;
        }

        /// <summary>
        /// Walks a little-endian MultiCurve WKB and asserts every nested
        /// geometry type is 2, 8, 9 or 18 (ISO Z/M/ZM reduced via % 1000).
        /// </summary>
        private static void AssertYear1NestedTypeCodesOnly(byte[] bytes)
        {
            Assert.That(bytes[0], Is.EqualTo(1));
            uint top = (ReadTypeLe(bytes, 1) & 0xFFFFu) % 1000;
            Assert.That(top, Is.EqualTo(11u));
            bool hasSrid = (ReadTypeLe(bytes, 1) & 0x20000000u) != 0;
            int offset = hasSrid ? 9 : 5;
            uint numMembers = ReadUInt32Le(bytes, offset);
            offset += 4;
            for (uint m = 0; m < numMembers; m++)
                offset = AssertYear1CurveTypeAndSkip(bytes, offset);
        }

        private static int AssertYear1CurveTypeAndSkip(byte[] bytes, int offset)
        {
            Assert.That(bytes[offset], Is.EqualTo(1).Or.EqualTo(0));
            uint raw = ReadTypeLe(bytes, offset + 1);
            bool hasSrid = (raw & 0x20000000u) != 0;
            uint code = (raw & 0xFFFFu) % 1000;
            Assert.That(code, Is.EqualTo(2u).Or.EqualTo(8u).Or.EqualTo(9u).Or.EqualTo(18u),
                "Year-1 member type at offset " + offset);
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
