// SPDX-License-Identifier: BSD-3-Clause
//
// AI assistance disclosure:
//   AI-drafted, human-reviewed. Assisted-by: Cursor Grok 4.6
//   AI-generated portions dedicated to CC0-1.0; human curation BSD-3-Clause.

using System;
using NetTopologySuite.Algorithm.Distance;
using NetTopologySuite.IO;
using NUnit.Framework;

namespace NetTopologySuite.Tests.NUnit.Algorithm.Distance
{
    /// <summary>
    /// Two certified curve DHD pairs.
    /// Witness: CIRCULARSTRING (0 0, 2 3, 10 0) → LINESTRING (0 0, 10 0)
    /// is √949/6 − 7/6.
    /// Port of JTS <c>0ca71b40</c>.
    /// </summary>
    public class DiscreteHausdorffDistanceCurveTest
    {
        private readonly WKTReader _reader = new WKTReader();

        [Test]
        public void ArcToSegmentIsApexNotFarEnd()
        {
            double apex = Math.Sqrt(949.0) / 6.0 - 7.0 / 6.0;
            double got = Oriented(
                "CIRCULARSTRING (0 0, 2 3, 10 0)",
                "LINESTRING (0 0, 10 0)");
            Assert.That(got, Is.EqualTo(apex).Within(1.0e-9));
            Assert.That(got, Is.LessThan(9.0));
        }

        [Test]
        public void TwoDiscsMatchCircleToCircle()
        {
            double got = Oriented(
                "CURVEPOLYGON (CIRCULARSTRING (5 0, 0 5, -5 0, 0 -5, 5 0))",
                "CURVEPOLYGON (CIRCULARSTRING (12 0, 7 5, 2 0, 7 -5, 12 0))");
            // |d + r1 − r2| for r=5 discs 7 apart: directed HD is 7, not 10.
            Assert.That(got, Is.EqualTo(7.0).Within(1.0e-9));
        }

        private double Oriented(string wktA, string wktB)
        {
            var a = _reader.Read(wktA);
            var b = _reader.Read(wktB);
            return new DiscreteHausdorffDistance(a, b).OrientedDistance();
        }
    }
}
