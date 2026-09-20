// SPDX-License-Identifier: BSD-3-Clause
// AI-drafted, human-reviewed.  Assisted-by: Cursor Grok 4.6
//
// Ticket 20 — Year-1 ST_Circle WKB type 18 read/write (ISO/IEC 13249-3).
// Payload: three circumference points, or EMPTY (type 18, zero points).
// Z/M/ZM via the existing (type & 0xffff) % 1000 reducer only
// (1018 / 2018 / 3018); no WKBCircleZ|M|ZM enum arms.
// Nested Circle in MultiCurve / CurvePolygon mirrors Ticket 19 object-model
// acceptance. MultiSurface still refuses Circle as a surface member; TIN
// members stay Polygon (3) only. Does not remint Ticket 19 WKT. No Exact*,
// OverlayNGCurve, or Ticket 21 typed-members rewrite.
// GEODESICSTRING / ELLIPTICALCURVE / NURBSCURVE / CLOTHOID / SPIRALCURVE
// stay refused.

using System;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Curves;
using NetTopologySuite.IO;
using NUnit.Framework;

namespace NetTopologySuite.Tests.NUnit.Geometries.Curves
{
    /// <summary>
    /// Year-1 <c>CIRCLE</c> WKB type 18 (Ticket 20).
    /// </summary>
    [Category("CurveAwareness")]
    public class CircleWkbYear1Test
    {
        private readonly WKTReader _wktReader = new WKTReader();
        private readonly WKTWriter _wktWriter = new WKTWriter();
        private readonly WKTWriter _wktWriterZ = new WKTWriter(3);
        private readonly WKTWriter _wktWriterZm = new WKTWriter(4);
        private readonly WKBReader _wkbReader = new WKBReader();
        private readonly WKBWriter _wkbWriter = new WKBWriter();

        // Ticket 19 WKT fixtures reused as WKB witnesses (no WKT remint).
        private const string WktEmpty = "CIRCLE EMPTY";
        private const string WktUnit = "CIRCLE (1 0, 0 1, -1 0)";
        private const string WktZ = "CIRCLE Z (1 0 5, 0 1 5, -1 0 5)";
        private const string WktM = "CIRCLE M (1 0 7, 0 1 7, -1 0 7)";
        private const string WktZm = "CIRCLE ZM (1 0 5 7, 0 1 5 7, -1 0 5 7)";
        private const string WktMultiCurve =
            "MULTICURVE (CIRCLE (1 0, 0 1, -1 0), (3 0, 4 0))";
        private const string WktCurvePolygon =
            "CURVEPOLYGON (CIRCLE (1 0, 0 1, -1 0))";
        private const string WktMultiSurfaceCurvePolygon =
            "MULTISURFACE (CURVEPOLYGON (CIRCLE (1 0, 0 1, -1 0)))";
        private const string WktGeometryCollection =
            "GEOMETRYCOLLECTION (CIRCLE (1 0, 0 1, -1 0))";

        // ISO little-endian type-18 fixtures, hand-checked against the
        // CircularString EMPTY / three-point layout (byte order, type,
        // numPoints, then coordinates).
        private const string HexEmpty = "011200000000000000";

        private const string HexUnit =
            "011200000003000000" +
            "000000000000F03F0000000000000000" +
            "0000000000000000000000000000F03F" +
            "000000000000F0BF0000000000000000";

        [Test]
        public void Ticket20_WkbTypeCodeIsEighteen()
        {
            Assert.That((int)WKBGeometryTypes.WKBCircle, Is.EqualTo(18));
            Assert.That(Enum.GetNames(typeof(WKBGeometryTypes)), Has.Member("WKBCircle"));
            Assert.That(Enum.GetNames(typeof(WKBGeometryTypes)), Has.None.EqualTo("WKBCircleZ"));
            Assert.That(Enum.GetNames(typeof(WKBGeometryTypes)), Has.None.EqualTo("WKBCircleM"));
            Assert.That(Enum.GetNames(typeof(WKBGeometryTypes)), Has.None.EqualTo("WKBCircleZM"));

            byte[] bytes = _wkbWriter.Write(_wktReader.Read(WktUnit));
            Assert.That(bytes[0], Is.EqualTo(1), "little-endian");
            Assert.That(ReadTypeLe(bytes, 1), Is.EqualTo(18u));
            Assert.That(ReadTypeLe(bytes, 1), Is.Not.EqualTo(8u), "must not demote to CircularString");
            Assert.That(ReadTypeLe(bytes, 1), Is.Not.EqualTo(7u), "must not fall through as GeometryCollection");
        }

        [TestCase(WktEmpty, Description = "Ticket20 EMPTY")]
        [TestCase(WktUnit, Description = "Ticket20 unit circle")]
        public void Ticket20_WkbObjectWkbPreservesEqualsExact(string wkt)
        {
            var original = _wktReader.Read(wkt);
            byte[] bytes = _wkbWriter.Write(original);
            var again = _wkbReader.Read(bytes);
            Assert.That(again, Is.InstanceOf<Circle>());
            Assert.That(again.GetType(), Is.EqualTo(original.GetType()));
            Assert.That(again.EqualsExact(original), Is.True, WKBWriter.ToHex(bytes));
            Assert.That(_wkbWriter.Write(again), Is.EqualTo(bytes));
        }

        [Test]
        public void Ticket20_EmptyWritesZeroPointsTypeEighteen()
        {
            byte[] bytes = _wkbWriter.Write(_wktReader.Read(WktEmpty));
            Assert.That(WKBWriter.ToHex(bytes), Is.EqualTo(HexEmpty));
            Assert.That(ReadTypeLe(bytes, 1), Is.EqualTo(18u));
            Assert.That(ReadUInt32Le(bytes, 5), Is.EqualTo(0u));
            var again = (Circle)_wkbReader.Read(bytes);
            Assert.That(again.IsEmpty, Is.True);
            Assert.That(again.NumPoints, Is.EqualTo(0));
            Assert.That(again.AsText(), Is.EqualTo(WktEmpty));
        }

        [Test]
        public void Ticket20_UnitCircleByteLevelFixture()
        {
            var fromWkt = (Circle)_wktReader.Read(WktUnit);
            byte[] written = _wkbWriter.Write(fromWkt);
            Assert.That(WKBWriter.ToHex(written), Is.EqualTo(HexUnit));
            Assert.That(ReadTypeLe(written, 1), Is.EqualTo(18u));
            Assert.That(ReadUInt32Le(written, 5), Is.EqualTo(3u), "three circumference points");

            var fromHex = (Circle)_wkbReader.Read(WKBReader.HexToBytes(HexUnit));
            Assert.That(fromHex.NumPoints, Is.EqualTo(3));
            Assert.That(fromHex.EqualsExact(fromWkt), Is.True);
            Assert.That(fromHex.Length, Is.EqualTo(2d * Math.PI).Within(1e-12));
        }

        [Test]
        public void Ticket20_WriterEmitsEighteenNeverCircularString()
        {
            AssertCircleTypeNotEight(_wkbWriter.Write(_wktReader.Read(WktEmpty)), 1);
            AssertCircleTypeNotEight(_wkbWriter.Write(_wktReader.Read(WktUnit)), 1);

            var writerZ = new WKBWriter(ByteOrder.LittleEndian, false, true);
            byte[] z = writerZ.Write(_wktReader.Read(WktZ));
            Assert.That((ReadTypeLe(z, 1) & 0xFFFFu) % 1000, Is.EqualTo(18u));
            Assert.That((ReadTypeLe(z, 1) & 0xFFFFu) % 1000, Is.Not.EqualTo(8u));

            byte[] multiCurve = _wkbWriter.Write(_wktReader.Read(WktMultiCurve));
            AssertCircleTypeNotEight(multiCurve, 10);
            byte[] curvePolygon = _wkbWriter.Write(_wktReader.Read(WktCurvePolygon));
            AssertCircleTypeNotEight(curvePolygon, 10);
            byte[] collection = _wkbWriter.Write(_wktReader.Read(WktGeometryCollection));
            AssertCircleTypeNotEight(collection, 10);

            foreach (string wkt in new[] { WktEmpty, WktUnit, WktZ, WktMultiCurve, WktCurvePolygon, WktGeometryCollection })
            {
                string emittedWkt = _wktWriterZ.Write(_wkbReader.Read(writerZ.Write(_wktReader.Read(wkt))));
                Assert.That(emittedWkt.ToUpperInvariant(), Does.Contain("CIRCLE"), wkt);
                Assert.That(emittedWkt.ToUpperInvariant(), Does.Not.Contain("CIRCULARSTRING"), wkt);
            }
        }

        [Test]
        public void Ticket20_IsoZmTypeCodesReadAndWrite()
        {
            // Same +1000/+2000/+3000 table already used for types 8–16.
            // Strict writer emits ISO codes (no EWKB high bits). Reducer-only
            // decode: 1018 / 2018 / 3018 → 18.
            AssertZmRoundTrip(WktZ, _wktWriterZ, emitZ: true, emitM: false, expectedIsoType: 1018u, expectZ: 5, expectM: double.NaN);
            AssertZmRoundTrip(WktM, _wktWriterZm, emitZ: false, emitM: true, expectedIsoType: 2018u, expectZ: double.NaN, expectM: 7);
            AssertZmRoundTrip(WktZm, _wktWriterZm, emitZ: true, emitM: true, expectedIsoType: 3018u, expectZ: 5, expectM: 7);
        }

        [Test]
        public void Ticket20_IsoZmTypeCodesAcceptedOnReadWhenPatched()
        {
            var xy = _wktReader.Read(WktUnit);
            byte[] bytes = _wkbWriter.Write(xy);
            Assert.That(ReadTypeLe(bytes, 1), Is.EqualTo(18u));

            foreach (uint isoType in new uint[] { 1018u, 2018u, 3018u })
            {
                byte[] patched = (byte[])bytes.Clone();
                WriteTypeLe(patched, 1, isoType);
                var again = _wkbReader.Read(patched);
                Assert.That(again, Is.InstanceOf<Circle>(), "ISO type " + isoType);
                Assert.That(((Circle)again).NumPoints, Is.EqualTo(3));
            }
        }

        [Test]
        public void Ticket20_CollinearThreePointsRefused()
        {
            byte[] asCircularString = _wkbWriter.Write(
                _wktReader.Read("CIRCULARSTRING (0 0, 1 0, 2 0)"));
            Assert.That(ReadTypeLe(asCircularString, 1), Is.EqualTo(8u));
            WriteTypeLe(asCircularString, 1, 18u);

            var ex = Assert.Throws<ParseException>(() => _wkbReader.Read(asCircularString));
            Assert.That(ex.ToString(), Does.Contain("collinear").IgnoreCase);
            Assert.That(ex.ToString(), Does.Contain("§4.2.7"));
        }

        [Test]
        public void Ticket20_WrongControlCountRefused()
        {
            var twoEx = Assert.Throws<ParseException>(() =>
                _wkbReader.Read(CircleWkbWithPointCount(2)));
            Assert.That(twoEx.ToString(), Does.Contain("exactly three"));

            var fourEx = Assert.Throws<ParseException>(() =>
                _wkbReader.Read(CircleWkbWithPointCount(4)));
            Assert.That(fourEx.ToString(), Does.Contain("exactly three"));
        }

        [Test]
        public void Ticket20_MultiCurveNestedCircleRoundTrip()
        {
            var original = (MultiCurve)_wktReader.Read(WktMultiCurve);
            Assert.That(original.GetGeometryN(0), Is.InstanceOf<Circle>());

            byte[] bytes = _wkbWriter.Write(original);
            Assert.That(ReadTypeLe(bytes, 1), Is.EqualTo(11u));
            Assert.That(ReadUInt32Le(bytes, 5), Is.EqualTo(2u));
            Assert.That(ReadTypeLe(bytes, 10), Is.EqualTo(18u), "nested Circle, not CircularString 8");
            Assert.That(ReadTypeLe(bytes, 10), Is.Not.EqualTo(8u));

            var again = (MultiCurve)_wkbReader.Read(bytes);
            Assert.That(again.GetGeometryN(0), Is.InstanceOf<Circle>());
            Assert.That(again.GetGeometryN(1), Is.InstanceOf<LineString>());
            Assert.That(again.EqualsExact(original), Is.True);
            Assert.That(_wktWriter.Write(again), Is.EqualTo(WktMultiCurve));
        }

        [Test]
        public void Ticket20_CurvePolygonNestedCircleRoundTrip()
        {
            var original = (CurvePolygon)_wktReader.Read(WktCurvePolygon);
            Assert.That(original.ExteriorRing, Is.InstanceOf<Circle>());

            byte[] bytes = _wkbWriter.Write(original);
            Assert.That(ReadTypeLe(bytes, 1), Is.EqualTo(10u));
            Assert.That(ReadUInt32Le(bytes, 5), Is.EqualTo(1u));
            Assert.That(ReadTypeLe(bytes, 10), Is.EqualTo(18u), "Circle ring, not CircularString 8");
            Assert.That(ReadTypeLe(bytes, 10), Is.Not.EqualTo(8u));

            var again = (CurvePolygon)_wkbReader.Read(bytes);
            Assert.That(again.ExteriorRing, Is.InstanceOf<Circle>());
            Assert.That(again.EqualsExact(original), Is.True);
            Assert.That(_wktWriter.Write(again), Is.EqualTo(WktCurvePolygon));
        }

        [Test]
        public void Ticket20_MultiSurfaceCurvePolygonRingCanBeCircle()
        {
            var original = (MultiSurface)_wktReader.Read(WktMultiSurfaceCurvePolygon);
            Assert.That(((CurvePolygon)original.GetGeometryN(0)).ExteriorRing, Is.InstanceOf<Circle>());

            byte[] bytes = _wkbWriter.Write(original);
            Assert.That(ReadTypeLe(bytes, 1), Is.EqualTo(12u));
            Assert.That(ReadTypeLe(bytes, 10), Is.EqualTo(10u), "CurvePolygon member");
            Assert.That(ReadTypeLe(bytes, 19), Is.EqualTo(18u), "Circle ring inside CurvePolygon");

            var again = (MultiSurface)_wkbReader.Read(bytes);
            Assert.That(((CurvePolygon)again.GetGeometryN(0)).ExteriorRing, Is.InstanceOf<Circle>());
            Assert.That(again.EqualsExact(original), Is.True);
        }

        [Test]
        public void Ticket20_CircleIsNotAMultiSurfaceMember()
        {
            byte[] circle = _wkbWriter.Write(_wktReader.Read(WktUnit));
            Assert.That(ReadTypeLe(circle, 1), Is.EqualTo(18u));
            byte[] wrapped = WrapAsMultiSurface(circle);
            var ex = Assert.Throws<ParseException>(() => _wkbReader.Read(wrapped));
            Assert.That(ex.ToString(), Does.Contain("Year-1"));
            Assert.That(ex.ToString(), Does.Contain("18"));
        }

        [Test]
        public void Ticket20_TinRejectsNestedCircle()
        {
            byte[] circle = _wkbWriter.Write(_wktReader.Read(WktUnit));
            byte[] wrapped = WrapAsTin(circle);
            var ex = Assert.Throws<ParseException>(() => _wkbReader.Read(wrapped));
            Assert.That(ex.ToString(), Does.Contain("Year-1"));
            Assert.That(ex.ToString(), Does.Contain("18"));
        }

        [Test]
        public void Ticket20_GeometryCollectionOfCircleIsNotRewritten()
        {
            var original = _wktReader.Read(WktGeometryCollection);
            Assert.That(original, Is.InstanceOf<GeometryCollection>());
            Assert.That(original, Is.Not.InstanceOf<Circle>());

            byte[] bytes = _wkbWriter.Write(original);
            Assert.That(ReadTypeLe(bytes, 1), Is.EqualTo(7u), "GeometryCollection stays type 7");
            Assert.That(ReadTypeLe(bytes, 10), Is.EqualTo(18u), "Circle member");
            Assert.That(ReadTypeLe(bytes, 10), Is.Not.EqualTo(8u));

            var again = _wkbReader.Read(bytes);
            Assert.That(again, Is.InstanceOf<GeometryCollection>());
            Assert.That(again.GetGeometryN(0), Is.InstanceOf<Circle>());
            Assert.That(again.EqualsExact(original), Is.True);
        }

        [Test]
        public void Ticket20_Year1CompoundCurveStillRefusesCircleMember()
        {
            byte[] circle = _wkbWriter.Write(_wktReader.Read(WktUnit));
            var cc = new byte[9 + circle.Length];
            cc[0] = 1;
            WriteTypeLe(cc, 1, 9u);
            WriteUInt32Le(cc, 5, 1u);
            Buffer.BlockCopy(circle, 0, cc, 9, circle.Length);

            var ex = Assert.Throws<ParseException>(() => _wkbReader.Read(cc));
            Assert.That(ex.ToString(), Does.Contain("LineString, CircularString or CompoundCurve"));
        }

        [Test]
        public void Ticket20_Year2CurvesStillRefused()
        {
            var leftover = Assert.Throws<ParseException>(() =>
                _wktReader.Read("GEODESICSTRING (0 0, 10 0, 10 10)"));
            Assert.That(leftover.Message, Does.Contain("not implemented"));

            byte[] asCircularString = _wkbWriter.Write(
                _wktReader.Read("CIRCULARSTRING (0 0, 1 1, 2 0)"));
            foreach (uint year2 in new uint[] { 13u, 14u, 15u, 17u, 19u })
            {
                byte[] patched = (byte[])asCircularString.Clone();
                WriteTypeLe(patched, 1, year2);
                var ex = Assert.Throws<ParseException>(() => _wkbReader.Read(patched),
                    "type " + year2 + " must stay refused");
                Assert.That(ex.ToString(), Does.Contain("Geometry type not recognized").Or.Contain("Should never reach here"));
            }
        }

        [Test]
        public void Ticket20_CircularStringStaysTypeEight()
        {
            var cs = _wktReader.Read("CIRCULARSTRING (1 0, 0 1, -1 0)");
            byte[] bytes = _wkbWriter.Write(cs);
            Assert.That(ReadTypeLe(bytes, 1), Is.EqualTo(8u));
            Assert.That(_wkbReader.Read(bytes), Is.InstanceOf<CircularString>());
            Assert.That(_wkbReader.Read(bytes), Is.Not.InstanceOf<Circle>());
        }

        [Test]
        public void Ticket20_WktWkbWktPreservesCircle()
        {
            var original = (Circle)_wktReader.Read(WktUnit);
            byte[] bytes = _wkbWriter.Write(original);
            var fromWkb = (Circle)_wkbReader.Read(bytes);
            string emitted = _wktWriter.Write(fromWkb);
            Assert.That(emitted, Is.EqualTo(WktUnit));
            Assert.That(_wktReader.Read(emitted).EqualsExact(original), Is.True);
        }

        private void AssertZmRoundTrip(string wkt, WKTWriter wktWriter, bool emitZ, bool emitM,
            uint expectedIsoType, double expectZ, double expectM)
        {
            var original = _wktReader.Read(wkt);
            var writer = new WKBWriter(ByteOrder.LittleEndian, false, emitZ, emitM);
            Assert.That(writer.Strict, Is.True);
            byte[] bytes = writer.Write(original);
            Assert.That(ReadTypeLe(bytes, 1), Is.EqualTo(expectedIsoType));

            var fromWkb = (Circle)_wkbReader.Read(bytes);
            Assert.That(fromWkb.EqualsExact(original), Is.True);
            var seq = fromWkb.CoordinateSequence;
            if (emitZ)
                Assert.That(seq.GetZ(0), Is.EqualTo(expectZ));
            if (emitM)
                Assert.That(seq.GetM(0), Is.EqualTo(expectM));

            string emitted = wktWriter.Write(fromWkb);
            var fromWkt = _wktReader.Read(emitted);
            Assert.That(fromWkt, Is.InstanceOf<Circle>());
            Assert.That(fromWkt.EqualsExact(original), Is.True);
            Assert.That(_wkbWriter.Write(fromWkb), Is.EqualTo(_wkbWriter.Write(original)));
        }

        private static void AssertCircleTypeNotEight(byte[] bytes, int typeOffset)
        {
            Assert.That(ReadTypeLe(bytes, typeOffset), Is.EqualTo(18u));
            Assert.That(ReadTypeLe(bytes, typeOffset), Is.Not.EqualTo(8u));
        }

        /// <summary>
        /// Little-endian Circle WKB with <paramref name="numPoints"/> XY
        /// coordinates. Used to probe the three-point intake lock without
        /// going through CircularString (which itself requires 2n+1 ≥ 3).
        /// </summary>
        private static byte[] CircleWkbWithPointCount(int numPoints)
        {
            var bytes = new byte[9 + numPoints * 16];
            bytes[0] = 1;
            WriteTypeLe(bytes, 1, 18u);
            WriteUInt32Le(bytes, 5, (uint)numPoints);
            for (int i = 0; i < numPoints; i++)
            {
                byte[] x = BitConverter.GetBytes((double)i);
                Buffer.BlockCopy(x, 0, bytes, 9 + i * 16, 8);
            }
            return bytes;
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

        private static byte[] WrapAsTin(byte[] memberWkb)
        {
            var bytes = new byte[9 + memberWkb.Length];
            bytes[0] = 1;
            WriteTypeLe(bytes, 1, 16u);
            WriteUInt32Le(bytes, 5, 1u);
            Buffer.BlockCopy(memberWkb, 0, bytes, 9, memberWkb.Length);
            return bytes;
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
