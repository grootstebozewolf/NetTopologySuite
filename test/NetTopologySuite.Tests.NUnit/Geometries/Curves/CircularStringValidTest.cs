// SPDX-License-Identifier: BSD-3-Clause
// AI-drafted, human-reviewed.  Assisted-by: Cursor Grok 4.6
// Port of JTS 81c2e996 CircularStringValidTest (EX-CS-4).

using System;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Curves;
using NetTopologySuite.IO;
using NUnit.Framework;

namespace NetTopologySuite.Tests.NUnit.Geometries.Curves
{
    /// <summary>
    /// Four-item <c>CIRCULARSTRING (A, B, C, A)</c> is rejected.
    /// Not in the PostGIS model (odd control count ≥ 3).
    /// Not V-CS. Not #86 / #87 draw.
    /// </summary>
    public class CircularStringValidTest
    {
        private const string Ring4 = "CIRCULARSTRING (-5 0, 0 5, 5 0, -5 0)";
        private const string OddClosed = "CIRCULARSTRING (-5 0, 0 5, 5 0, 0 -5, -5 0)";
        private const string OnRamp = "CIRCULARSTRING (0 0, 2 0, 0 0)";

        [Test]
        public void ClosedFourPointCircularStringIsRejected()
        {
            var factory = new GeometryFactory();
            var pts = new[]
            {
                new Coordinate(-5, 0),
                new Coordinate(0, 5),
                new Coordinate(5, 0),
                new Coordinate(-5, 0)
            };
            var seq = factory.CoordinateSequenceFactory.Create(pts);
            Assert.That(CircularString.IsValidControlCount(seq), Is.False);
            Assert.Throws<ArgumentException>(() => new CircularString(seq, factory));
        }

        [Test]
        public void WktFourItemCircularStringIsRejected()
        {
            Assert.Throws<ArgumentException>(() => new WKTReader().Read(Ring4));
        }

        [Test]
        public void OpenFourPointControlIsInvalid()
        {
            var factory = new GeometryFactory();
            var pts = new[]
            {
                new Coordinate(0, 0),
                new Coordinate(1, 1),
                new Coordinate(2, 0),
                new Coordinate(3, 1)
            };
            var seq = factory.CoordinateSequenceFactory.Create(pts);
            Assert.That(CircularString.IsValidControlCount(seq), Is.False);
            Assert.Throws<ArgumentException>(() => new CircularString(seq, factory));
        }

        [Test]
        public void OddClosedCircularStringStillValid()
        {
            var g = new WKTReader().Read(OddClosed);
            Assert.That(g.IsValid, Is.True);
            Assert.That(g.NumPoints, Is.EqualTo(5));
            Assert.That(g.GeometryType, Is.EqualTo("CircularString"));
        }

        [Test]
        public void FactoryRewritesAbaToFiveTokenCircle()
        {
            var factory = new GeometryFactory();
            var seq = factory.CoordinateSequenceFactory.Create(new[]
            {
                new Coordinate(0, 0),
                new Coordinate(0, 2),
                new Coordinate(0, 0)
            });
            var g = factory.CreateCircularString(seq);
            Assert.That(g.GeometryType, Is.EqualTo("CircularString"));
            Assert.That(g.NumPoints, Is.EqualTo(5));
            Assert.That(g.Coordinates[1].X, Is.EqualTo(1.0).Within(1e-12));
            Assert.That(g.Coordinates[1].Y, Is.EqualTo(1.0).Within(1e-12));
            Assert.That(g.Coordinates[2].X, Is.EqualTo(0.0).Within(1e-12));
            Assert.That(g.Coordinates[2].Y, Is.EqualTo(2.0).Within(1e-12));
            Assert.That(g.Coordinates[3].X, Is.EqualTo(-1.0).Within(1e-12));
            Assert.That(g.Coordinates[3].Y, Is.EqualTo(1.0).Within(1e-12));
        }

        [Test]
        public void DiameterOnRampRewritesToFiveTokenCircle()
        {
            var g = new WKTReader().Read(OnRamp);
            Assert.That(g, Is.InstanceOf<CircularString>());
            Assert.That(g.GeometryType, Is.EqualTo("CircularString"));
            Assert.That(g.NumPoints, Is.EqualTo(5));
            Assert.That(g.IsValid, Is.True);
            Assert.That(g.Coordinates[1].X, Is.EqualTo(1.0).Within(1e-12));
            Assert.That(g.Coordinates[1].Y, Is.EqualTo(-1.0).Within(1e-12));
            Assert.That(g.Coordinates[3].X, Is.EqualTo(1.0).Within(1e-12));
            Assert.That(g.Coordinates[3].Y, Is.EqualTo(1.0).Within(1e-12));
        }

        [Test]
        public void RefuseAEqualsBOnRead()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                new WKTReader().Read("CIRCULARSTRING (0 0, 0 0, 0 0)"));
            Assert.That(ex.Message, Does.Contain("A = B").Or.Contain("distinct"));
        }
    }
}
