// SPDX-License-Identifier: BSD-3-Clause
//
// AI assistance disclosure:
//   AI-drafted, human-reviewed. Assisted-by: Cursor Grok 4.6
//   AI-generated portions dedicated to CC0-1.0; human curation BSD-3-Clause.

using System;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NUnit.Framework;

namespace NetTopologySuite.Tests.NUnit.Geometries.Curves
{
    /// <summary>
    /// Closed-form circular ring length and area.
    /// Witness: r=2 circle length is 4π (was 8√2); r=3 disc area is 9π (was 18).
    /// Port of JTS <c>9808dfa1</c>.
    /// </summary>
    public class CircularRingMetricsTest
    {
        private readonly WKTReader _reader = new WKTReader();

        [Test]
        public void Radius2CircleLengthIsFourPi()
        {
            var g = _reader.Read("CURVEPOLYGON (CIRCULARSTRING (-2 0, 0 2, 2 0, 0 -2, -2 0))");
            Assert.That(g.Length, Is.EqualTo(4.0 * Math.PI).Within(1.0e-9));
            Assert.That(g.Length, Is.Not.EqualTo(8.0 * Math.Sqrt(2)).Within(1.0e-6));
        }

        [Test]
        public void Radius3DiscAreaIsNinePi()
        {
            var g = _reader.Read("CURVEPOLYGON (CIRCULARSTRING (-3 0, 0 3, 3 0, 0 -3, -3 0))");
            Assert.That(g.Area, Is.EqualTo(9.0 * Math.PI).Within(1.0e-9));
            Assert.That(g.Area, Is.Not.EqualTo(18.0).Within(1.0e-6));
        }

        [Test]
        public void CircularStringLengthMatchesArc()
        {
            var g = _reader.Read("CIRCULARSTRING (-2 0, 0 2, 2 0, 0 -2, -2 0)");
            Assert.That(g.Length, Is.EqualTo(4.0 * Math.PI).Within(1.0e-9));
        }
    }
}
