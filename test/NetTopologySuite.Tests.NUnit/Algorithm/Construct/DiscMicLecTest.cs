// SPDX-License-Identifier: BSD-3-Clause
//
// AI assistance disclosure:
//   AI-drafted, human-reviewed. Assisted-by: Cursor Grok 4.6
//   AI-generated portions dedicated to CC0-1.0; human curation BSD-3-Clause.

using System;
using NetTopologySuite.Algorithm.Construct;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NUnit.Framework;

namespace NetTopologySuite.Tests.NUnit.Algorithm.Construct
{
    /// <summary>
    /// Disc MIC/LEC r = R. Witness: radius-5 disc inscribed 5.0, not 5/√2.
    /// Port of JTS <c>f24cb33d</c>.
    /// </summary>
    public class DiscMicLecTest
    {
        private const string Disc5 =
            "CURVEPOLYGON (CIRCULARSTRING (5 0, 0 5, -5 0, 0 -5, 5 0))";

        [Test]
        public void RadiusFiveDiscMicIsFiveNotDiamond()
        {
            var disc = new WKTReader().Read(Disc5);
            var radius = MaximumInscribedCircle.GetRadiusLine(disc, 0.01);
            Assert.That(radius.Length, Is.EqualTo(5.0).Within(1.0e-9));
            Assert.That(radius.Length, Is.GreaterThan(4.0));
        }

        [Test]
        public void RadiusFiveDiscLecIsFive()
        {
            var disc = new WKTReader().Read(Disc5);
            var radius = LargestEmptyCircle.GetRadiusLine(disc, 0.01);
            Assert.That(radius.Length, Is.EqualTo(5.0).Within(1.0e-9));
        }
    }
}
