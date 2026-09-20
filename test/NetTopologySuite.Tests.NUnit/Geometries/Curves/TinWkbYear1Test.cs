// SPDX-License-Identifier: BSD-3-Clause
// AI-drafted, human-reviewed.  Assisted-by: Cursor Grok 4.6
//
// Ticket 17 — Year-1 ST_TIN WKB type 16 read/write (ISO/IEC 13249-3).
// Members: nested WKB Polygon (3) only (g4 = polygonText). Nested 15 / 17 / 18
// and any other non-3 are refused by numeric type. Reuses Ticket 16 Year-1
// WKT g4 lock; does not remint WKT. No CIRCLE / WKB 18. Dim decode is the
// existing (type & 0xffff) % 1000 reducer only (no WKBTinZ|M|ZM arms).

using System;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Curves;
using NetTopologySuite.IO;
using NUnit.Framework;

using Triangle = NetTopologySuite.Geometries.Curves.Triangle;

namespace NetTopologySuite.Tests.NUnit.Geometries.Curves
{
    /// <summary>
    /// Year-1 <c>TIN</c> WKB type 16 (Ticket 17).
    /// </summary>
    [Category("CurveAwareness")]
    public class TinWkbYear1Test
    {
        private readonly WKTReader _wktReader = new WKTReader();
        private readonly WKTWriter _wktWriter = new WKTWriter();
        private readonly WKTWriter _wktWriterZ = new WKTWriter(3);
        private readonly WKTWriter _wktWriterZm = new WKTWriter(4);
        private readonly WKBReader _wkbReader = new WKBReader();
        private readonly WKBWriter _wkbWriter = new WKBWriter();

        // Ticket 16 fixtures reused as WKB witnesses (no WKT remint).
        private const string WktEmpty = "TIN EMPTY";
        private const string WktOnePatch = "TIN (((0 0, 1 0, 0 1, 0 0)))";
        private const string WktMultiPatch =
            "TIN (((0 0, 1 0, 0 1, 0 0)), ((1 0, 1 1, 0 1, 1 0)))";
        private const string WktZ = "TIN Z (((0 0 5, 1 0 5, 0 1 5, 0 0 5)))";
        private const string WktM = "TIN M (((0 0 7, 1 0 7, 0 1 7, 0 0 7)))";
        private const string WktZm =
            "TIN ZM (((0 0 5 7, 1 0 5 7, 0 1 5 7, 0 0 5 7)))";

        // GEOS / ISO little-endian type-16 fixtures, hand-checked against the
        // nested WKB layout (byte order, type, numPatches, then nested 3).
        private const string HexEmpty = "011000000000000000";

        private const string HexOnePatch =
            "011000000001000000" +
            "01030000000100000004000000" +
            "00000000000000000000000000000000" +
            "000000000000F03F0000000000000000" +
            "0000000000000000000000000000F03F" +
            "00000000000000000000000000000000";

        private const string HexMultiPatch =
            "011000000002000000" +
            "01030000000100000004000000" +
            "00000000000000000000000000000000" +
            "000000000000F03F0000000000000000" +
            "0000000000000000000000000000F03F" +
            "00000000000000000000000000000000" +
            "01030000000100000004000000" +
            "000000000000F03F0000000000000000" +
            "000000000000F03F000000000000F03F" +
            "0000000000000000000000000000F03F" +
            "000000000000F03F0000000000000000";

        [Test]
        public void Ticket17_WkbTypeCodeIsSixteen()
        {
            Assert.That((int)WKBGeometryTypes.WKBTin, Is.EqualTo(16));
            byte[] bytes = _wkbWriter.Write(_wktReader.Read(WktOnePatch));
            Assert.That(bytes[0], Is.EqualTo(1), "little-endian");
            Assert.That(ReadTypeLe(bytes, 1), Is.EqualTo(16u));
            Assert.That(ReadTypeLe(bytes, 1), Is.Not.EqualTo(7u), "must not fall through as GeometryCollection");
        }

        [TestCase(WktEmpty, Description = "Ticket17 EMPTY")]
        [TestCase(WktOnePatch, Description = "Ticket17 one patch")]
        [TestCase(WktMultiPatch, Description = "Ticket17 multi patch")]
        public void Ticket17_WkbObjectWkbPreservesEqualsExact(string wkt)
        {
            var original = _wktReader.Read(wkt);
            byte[] bytes = _wkbWriter.Write(original);
            var again = _wkbReader.Read(bytes);
            Assert.That(again, Is.InstanceOf<Tin>());
            Assert.That(again.GetType(), Is.EqualTo(original.GetType()));
            Assert.That(again.EqualsExact(original), Is.True, WKBWriter.ToHex(bytes));
            Assert.That(_wkbWriter.Write(again), Is.EqualTo(bytes));
        }

        [TestCase(WktEmpty)]
        [TestCase(WktOnePatch)]
        [TestCase(WktMultiPatch)]
        public void Ticket17_WktWkbWktPreservesTriangleMembers(string wkt)
        {
            var original = (Tin)_wktReader.Read(wkt);
            byte[] bytes = _wkbWriter.Write(original);
            var fromWkb = (Tin)_wkbReader.Read(bytes);
            AssertTriangleMembers(original, fromWkb);

            string emitted = _wktWriter.Write(fromWkb);
            var fromWkt = (Tin)_wktReader.Read(emitted);
            AssertTriangleMembers(original, fromWkt);
            Assert.That(fromWkt.EqualsExact(original), Is.True, emitted);
            Assert.That(emitted.ToUpperInvariant(), Does.Not.Contain("PATCHES"));
        }

        [Test]
        public void Ticket17_EmptyWritesZeroPatchesTypeSixteen()
        {
            byte[] bytes = _wkbWriter.Write(_wktReader.Read(WktEmpty));
            Assert.That(WKBWriter.ToHex(bytes), Is.EqualTo(HexEmpty));
            Assert.That(ReadTypeLe(bytes, 1), Is.EqualTo(16u));
            Assert.That(ReadUInt32Le(bytes, 5), Is.EqualTo(0u));
            var again = (Tin)_wkbReader.Read(bytes);
            Assert.That(again.IsEmpty, Is.True);
            Assert.That(again.NumGeometries, Is.EqualTo(0));
        }

        [Test]
        public void Ticket17_OnePatchByteLevelFixture()
        {
            var fromWkt = (Tin)_wktReader.Read(WktOnePatch);
            byte[] written = _wkbWriter.Write(fromWkt);
            Assert.That(WKBWriter.ToHex(written), Is.EqualTo(HexOnePatch));
            Assert.That(ReadTypeLe(written, 1), Is.EqualTo(16u));
            Assert.That(ReadUInt32Le(written, 5), Is.EqualTo(1u), "one patch");
            Assert.That(written[9], Is.EqualTo(1));
            Assert.That(ReadTypeLe(written, 10), Is.EqualTo(3u), "Polygon member, not Triangle 17");
            Assert.That(ReadTypeLe(written, 10), Is.Not.EqualTo(17u));

            var fromHex = (Tin)_wkbReader.Read(WKBReader.HexToBytes(HexOnePatch));
            Assert.That(fromHex.NumGeometries, Is.EqualTo(1));
            Assert.That(fromHex.GetGeometryN(0), Is.InstanceOf<Triangle>());
            Assert.That(fromHex.EqualsExact(fromWkt), Is.True);
        }

        [Test]
        public void Ticket17_MultiPatchByteLevelFixture()
        {
            var fromWkt = (Tin)_wktReader.Read(WktMultiPatch);
            byte[] written = _wkbWriter.Write(fromWkt);
            Assert.That(WKBWriter.ToHex(written), Is.EqualTo(HexMultiPatch));
            Assert.That(ReadTypeLe(written, 1), Is.EqualTo(16u));
            Assert.That(ReadUInt32Le(written, 5), Is.EqualTo(2u), "two patches");
            Assert.That(ReadTypeLe(written, 10), Is.EqualTo(3u), "first Polygon member");
            int secondOffset = 9 + 5 + 4 + 4 + 4 * 16;
            Assert.That(written[secondOffset], Is.EqualTo(1));
            Assert.That(ReadTypeLe(written, secondOffset + 1), Is.EqualTo(3u), "second Polygon member");

            var fromHex = (Tin)_wkbReader.Read(WKBReader.HexToBytes(HexMultiPatch));
            Assert.That(fromHex.NumGeometries, Is.EqualTo(2));
            Assert.That(fromHex.GetGeometryN(0), Is.InstanceOf<Triangle>());
            Assert.That(fromHex.GetGeometryN(1), Is.InstanceOf<Triangle>());
            Assert.That(fromHex.EqualsExact(fromWkt), Is.True);
        }

        [Test]
        public void Ticket17_IsoZmTypeCodesReadAndWrite()
        {
            // Same +1000/+2000/+3000 table already used for types 8–12.
            // Strict writer emits ISO codes (no EWKB high bits). Reducer-only
            // decode: 1016 / 2016 / 3016 → 16.
            AssertZmRoundTrip(WktZ, _wktWriterZ, emitZ: true, emitM: false, expectedIsoType: 1016u, expectZ: 5, expectM: double.NaN);
            AssertZmRoundTrip(WktM, _wktWriterZm, emitZ: false, emitM: true, expectedIsoType: 2016u, expectZ: double.NaN, expectM: 7);
            AssertZmRoundTrip(WktZm, _wktWriterZm, emitZ: true, emitM: true, expectedIsoType: 3016u, expectZ: 5, expectM: 7);
        }

        [Test]
        public void Ticket17_IsoZmTypeCodesAcceptedOnReadWhenPatched()
        {
            var xy = _wktReader.Read(WktOnePatch);
            byte[] bytes = _wkbWriter.Write(xy);
            Assert.That(ReadTypeLe(bytes, 1), Is.EqualTo(16u));

            foreach (uint isoType in new uint[] { 1016u, 2016u, 3016u })
            {
                byte[] patched = (byte[])bytes.Clone();
                WriteTypeLe(patched, 1, isoType);
                var again = _wkbReader.Read(patched);
                Assert.That(again, Is.InstanceOf<Tin>(), "ISO type " + isoType);
                Assert.That(((Tin)again).GetGeometryN(0), Is.InstanceOf<Triangle>());
            }
        }

        [Test]
        public void Ticket17_NestedIsoPolygonZmTypesReduceToThree()
        {
            var xy = _wktReader.Read(WktOnePatch);
            byte[] bytes = _wkbWriter.Write(xy);
            Assert.That(ReadTypeLe(bytes, 10), Is.EqualTo(3u));

            foreach (uint isoType in new uint[] { 1003u, 2003u, 3003u })
            {
                byte[] patched = (byte[])bytes.Clone();
                WriteTypeLe(patched, 10, isoType);
                var again = (Tin)_wkbReader.Read(patched);
                Assert.That(again.GetGeometryN(0), Is.InstanceOf<Triangle>(), "nested ISO type " + isoType);
                Assert.That(again.EqualsExact(xy), Is.True);
            }
        }

        [Test]
        public void Ticket17_EwkbSridPathMatchesMultiPolygon()
        {
            var tin = (Tin)_wktReader.Read(WktOnePatch);
            tin.SRID = 4326;
            var multiPolygon = (MultiPolygon)_wktReader.Read("MULTIPOLYGON (((0 0, 1 0, 0 1, 0 0)))");
            multiPolygon.SRID = 4326;

            var ewkbWriter = new WKBWriter(ByteOrder.LittleEndian, true);
            byte[] tinBytes = ewkbWriter.Write(tin);
            byte[] polygonBytes = ewkbWriter.Write(multiPolygon);

            uint tinType = ReadTypeLe(tinBytes, 1);
            uint polygonType = ReadTypeLe(polygonBytes, 1);
            Assert.That((tinType & 0x20000000u) != 0, Is.True, "EWKB SRID flag on TIN");
            Assert.That((polygonType & 0x20000000u) != 0, Is.True, "EWKB SRID flag on MultiPolygon");
            Assert.That(tinType & 0x0FFFFFFFu, Is.EqualTo(16u));
            Assert.That(polygonType & 0x0FFFFFFFu, Is.EqualTo(6u));
            Assert.That(BitConverter.ToInt32(tinBytes, 5), Is.EqualTo(4326));
            Assert.That(BitConverter.ToInt32(polygonBytes, 5), Is.EqualTo(4326));

            var again = (Tin)_wkbReader.Read(tinBytes);
            Assert.That(again.SRID, Is.EqualTo(4326));
            Assert.That(again.GetGeometryN(0), Is.InstanceOf<Triangle>());
            Assert.That(again.EqualsExact(tin), Is.True);
        }

        [TestCase(1u, Description = "nested Point")]
        [TestCase(2u, Description = "nested LineString")]
        [TestCase(6u, Description = "nested MultiPolygon")]
        [TestCase(7u, Description = "nested GeometryCollection")]
        [TestCase(8u, Description = "nested CircularString")]
        [TestCase(10u, Description = "nested CurvePolygon")]
        [TestCase(12u, Description = "nested MultiSurface")]
        [TestCase(15u, Description = "PolyhedralSurface")]
        [TestCase(16u, Description = "nested TIN")]
        [TestCase(17u, Description = "Triangle (object-model type, not Year-1 wire)")]
        [TestCase(18u, Description = "unknown type 18 (not a Circle / Year-2 invention)")]
        public void Ticket17_RejectsNonYear1NestedMemberTypeCode(uint nestedType)
        {
            byte[] member = _wkbWriter.Write(_wktReader.Read("POLYGON ((0 0, 1 0, 0 1, 0 0))"));
            WriteTypeLe(member, 1, nestedType);
            byte[] wrapped = WrapAsTin(member);
            var ex = Assert.Throws<ParseException>(() => _wkbReader.Read(wrapped));
            Assert.That(ex.ToString(), Does.Contain("Year-1"));
            Assert.That(ex.ToString(), Does.Contain(nestedType.ToString()));
            Assert.That(ex.ToString(), Does.Not.Contain("feature expected"));
        }

        [Test]
        public void Ticket17_RejectsNestedPolygonWithInteriorRing()
        {
            byte[] member = _wkbWriter.Write(_wktReader.Read(
                "POLYGON ((0 0, 10 0, 10 10, 0 10, 0 0), (2 2, 3 2, 2 3, 2 2))"));
            Assert.That(ReadTypeLe(member, 1), Is.EqualTo(3u));
            byte[] wrapped = WrapAsTin(member);
            var ex = Assert.Throws<ParseException>(() => _wkbReader.Read(wrapped));
            Assert.That(ex.ToString(), Does.Contain("interior rings"));
        }

        [Test]
        public void Ticket17_GeometryCollectionOfTinIsNotRewrittenAsTin()
        {
            const string wkt = "GEOMETRYCOLLECTION (TIN (((0 0, 1 0, 0 1, 0 0))))";
            var original = _wktReader.Read(wkt);
            Assert.That(original, Is.InstanceOf<GeometryCollection>());
            Assert.That(original, Is.Not.InstanceOf<Tin>());

            byte[] bytes = _wkbWriter.Write(original);
            Assert.That(ReadTypeLe(bytes, 1), Is.EqualTo(7u), "GeometryCollection stays type 7");
            Assert.That(ReadUInt32Le(bytes, 5), Is.EqualTo(1u));
            Assert.That(ReadTypeLe(bytes, 10), Is.EqualTo(16u), "TIN member");
            Assert.That(ReadTypeLe(bytes, 19), Is.EqualTo(3u), "TIN patch is Polygon 3");

            var again = _wkbReader.Read(bytes);
            Assert.That(again, Is.InstanceOf<GeometryCollection>());
            Assert.That(again, Is.Not.InstanceOf<Tin>());
            Assert.That(again.GetGeometryN(0), Is.InstanceOf<Tin>());
            Assert.That(again.EqualsExact(original), Is.True);
        }

        [Test]
        public void Ticket17_WriterNeverEmitsYear2TypeCodes()
        {
            string[] year1 = { WktEmpty, WktOnePatch, WktMultiPatch, WktZ };
            var writerZ = new WKBWriter(ByteOrder.LittleEndian, false, true);
            foreach (string wkt in year1)
            {
                byte[] bytes = writerZ.Write(_wktReader.Read(wkt));
                uint type = (ReadTypeLe(bytes, 1) & 0xFFFFu) % 1000;
                Assert.That(type, Is.EqualTo(16u), "top-level type for " + wkt);
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

            var fromWkb = (Tin)_wkbReader.Read(bytes);
            Assert.That(fromWkb.EqualsExact(original), Is.True);
            if (!original.IsEmpty)
            {
                CoordinateSequence seq = ((Triangle)fromWkb.GetGeometryN(0)).ExteriorRing.CoordinateSequence;
                if (emitZ)
                    Assert.That(seq.GetZ(0), Is.EqualTo(expectZ));
                if (emitM)
                    Assert.That(seq.GetM(0), Is.EqualTo(expectM));
            }

            string emitted = wktWriter.Write(fromWkb);
            var fromWkt = _wktReader.Read(emitted);
            Assert.That(fromWkt, Is.InstanceOf<Tin>());
            Assert.That(fromWkt.EqualsExact(original), Is.True);
            Assert.That(_wkbWriter.Write(fromWkb), Is.EqualTo(_wkbWriter.Write(original)));
        }

        private static void AssertTriangleMembers(Tin expected, Tin actual)
        {
            Assert.That(actual.IsEmpty, Is.EqualTo(expected.IsEmpty));
            Assert.That(actual.NumGeometries, Is.EqualTo(expected.NumGeometries));
            for (int i = 0; i < expected.NumGeometries; i++)
            {
                Assert.That(actual.GetGeometryN(i), Is.InstanceOf<Triangle>());
                Assert.That(actual.GetGeometryN(i).GetType(),
                    Is.EqualTo(expected.GetGeometryN(i).GetType()));
            }
        }

        private static byte[] WrapAsTin(byte[] memberWkb)
        {
            var bytes = new byte[9 + memberWkb.Length];
            bytes[0] = 1;
            WriteTypeLe(bytes, 1, 16u);
            WriteUInt32Le(bytes, 5, 1u);
            Buffer.BlockCopy(memberWkb, 0, bytes, 9, memberWkb.Length);
            return bytes;
        }

        /// <summary>
        /// Walks a little-endian TIN WKB and asserts every nested member
        /// type is 3 (ISO Z/M/ZM reduced via % 1000). Never 15 / 17 / 18.
        /// </summary>
        private static void AssertYear1NestedTypeCodesOnly(byte[] bytes)
        {
            Assert.That(bytes[0], Is.EqualTo(1));
            uint top = (ReadTypeLe(bytes, 1) & 0xFFFFu) % 1000;
            Assert.That(top, Is.EqualTo(16u));
            bool hasSrid = (ReadTypeLe(bytes, 1) & 0x20000000u) != 0;
            int offset = hasSrid ? 9 : 5;
            uint numMembers = ReadUInt32Le(bytes, offset);
            offset += 4;
            for (uint m = 0; m < numMembers; m++)
            {
                Assert.That(bytes[offset], Is.EqualTo(1).Or.EqualTo(0));
                uint raw = ReadTypeLe(bytes, offset + 1);
                uint code = (raw & 0xFFFFu) % 1000;
                Assert.That(code, Is.EqualTo(3u), "Year-1 member type at offset " + offset);
                Assert.That(code, Is.Not.EqualTo(15u).And.Not.EqualTo(17u).And.Not.EqualTo(18u));
                bool memberSrid = (raw & 0x20000000u) != 0;
                offset += memberSrid ? 9 : 5;
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
            }
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
