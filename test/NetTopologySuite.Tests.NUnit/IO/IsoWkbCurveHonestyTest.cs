// SPDX-License-Identifier: BSD-3-Clause
//
// AI assistance disclosure:
//   AI-drafted, human-reviewed. Assisted-by: Cursor Grok 4.6
//   AI-generated portions dedicated to CC0-1.0; human curation BSD-3-Clause.

using System;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Curves;
using NetTopologySuite.IO;
using NUnit.Framework;

namespace NetTopologySuite.Tests.NUnit.IO
{
    // topic: koc
    // Port of JTS b1b7a650.
    // Witness: a circular ring writes WKB type 8, not type 3.
    public class IsoWkbCurveHonestyTest
    {
        private readonly GeometryFactory _factory = new GeometryFactory();

        [Test]
        public void CircularStringWritesType8Not3()
        {
            var cs = CircleR2();
            byte[] wkb = new WKBWriter().Write(cs);
            Assert.That(wkb[0], Is.EqualTo((byte)ByteOrder.LittleEndian));
            uint type = BitConverter.ToUInt32(wkb, 1);
            Assert.That(type & 0xff, Is.EqualTo(8u));
            Assert.That(type & 0xff, Is.Not.EqualTo(3u));

            var read = new WKBReader().Read(wkb);
            Assert.That(read, Is.InstanceOf<CircularString>());
            Assert.That(read.NumPoints, Is.EqualTo(5));
        }

        [Test]
        public void CurvePolygonWritesType10Not3()
        {
            var ring = CircleR2();
            var cp = new CurvePolygon(ring, _factory);
            byte[] wkb = new WKBWriter().Write(cp);
            uint type = BitConverter.ToUInt32(wkb, 1);
            Assert.That(type & 0xff, Is.EqualTo(10u));
            Assert.That(type & 0xff, Is.Not.EqualTo(3u));

            var read = new WKBReader().Read(wkb);
            Assert.That(read, Is.InstanceOf<CurvePolygon>());
        }

        [Test]
        public void CompoundCurveWritesType9()
        {
            var arc = new CircularString(_factory.CoordinateSequenceFactory.Create(new[]
            {
                new Coordinate(1, 0), new Coordinate(0, 1), new Coordinate(-1, 0)
            }), _factory);
            var cc = new CompoundCurve(new Curve[] { arc }, _factory);
            byte[] wkb = new WKBWriter().Write(cc);
            uint type = BitConverter.ToUInt32(wkb, 1);
            Assert.That(type & 0xff, Is.EqualTo(9u));

            var read = new WKBReader().Read(wkb);
            Assert.That(read, Is.InstanceOf<CompoundCurve>());
        }

        private CircularString CircleR2()
        {
            var seq = _factory.CoordinateSequenceFactory.Create(new[]
            {
                new Coordinate(2, 0),
                new Coordinate(0, 2),
                new Coordinate(-2, 0),
                new Coordinate(0, -2),
                new Coordinate(2, 0)
            });
            return new CircularString(seq, _factory);
        }
    }
}
