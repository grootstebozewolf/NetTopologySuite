// SPDX-License-Identifier: BSD-3-Clause
//
// AI assistance disclosure:
//   AI-drafted, human-reviewed. Assisted-by: Cursor Grok 4.6
//   AI-generated portions dedicated to CC0-1.0; human curation BSD-3-Clause.

using NetTopologySuite.Algorithm.Construct;
using NetTopologySuite.IO;
using NUnit.Framework;

namespace NetTopologySuite.Tests.NUnit.Algorithm.Construct
{
    /// <summary>
    /// ML.1 stadium MIC. Radius is the cap radius; centre is the
    /// midpoint of the two cap centres. Witness: STADIUM_FOUR (0, 2.5) r=1.
    /// Port of JTS <c>6b1dbac1</c>.
    /// </summary>
    public class StadiumMicTest
    {
        private const string Circle5 =
            "CURVEPOLYGON (CIRCULARSTRING (-5 0, 0 5, 5 0, 0 -5, -5 0))";
        private const string HalfDisc =
            "CURVEPOLYGON (COMPOUNDCURVE (CIRCULARSTRING (-5 0, 0 5, 5 0), (5 0, -5 0)))";
        private const string StadiumFour =
            "CURVEPOLYGON (COMPOUNDCURVE (CIRCULARSTRING (-1 -1, 0 -2, 1 -1), (1 -1, 1 6), CIRCULARSTRING (1 6, 0 7, -1 6), (-1 6, -1 -1)))";
        private const string StadiumOdd =
            "CURVEPOLYGON (COMPOUNDCURVE (CIRCULARSTRING (-1 4, 0 5, 1 4), (1 4, 1 -1), CIRCULARSTRING (1 -1, 0 -2, -1 -1), (-1 -1, -1 4)))";
        private const string StadiumIn =
            "CURVEPOLYGON (COMPOUNDCURVE (CIRCULARSTRING (-1 1, -2 2, -1 3), (-1 3, 1 3), CIRCULARSTRING (1 3, 2 2, 1 1), (1 1, -1 1)))";
        private const string HoledStadium =
            "CURVEPOLYGON (COMPOUNDCURVE (CIRCULARSTRING (-1 -1, 0 -2, 1 -1), (1 -1, 1 6), CIRCULARSTRING (1 6, 0 7, -1 6), (-1 6, -1 -1)), (0 1, 1 1, 1 2, 0 2, 0 1))";

        [Test]
        public void FullDiscIsNotAStadium()
        {
            Assert.That(StadiumMic.TryGet(new WKTReader().Read(Circle5),
                out _, out _, out _), Is.False);
        }

        [Test]
        public void StadiumFourClosedForm()
        {
            Assert.That(StadiumMic.TryGet(new WKTReader().Read(StadiumFour),
                out double cx, out double cy, out double r), Is.True);
            Assert.That(cx, Is.EqualTo(0.0));
            Assert.That(cy, Is.EqualTo(2.5));
            Assert.That(r, Is.EqualTo(1.0));
        }

        [Test]
        public void StadiumOddClosedForm()
        {
            Assert.That(StadiumMic.TryGet(new WKTReader().Read(StadiumOdd),
                out double cx, out double cy, out double r), Is.True);
            Assert.That(cx, Is.EqualTo(0.0));
            Assert.That(cy, Is.EqualTo(1.5));
            Assert.That(r, Is.EqualTo(1.0));
        }

        [Test]
        public void StadiumInHorizontalAlsoCertifies()
        {
            Assert.That(StadiumMic.TryGet(new WKTReader().Read(StadiumIn),
                out double cx, out double cy, out double r), Is.True);
            Assert.That(cx, Is.EqualTo(0.0));
            Assert.That(cy, Is.EqualTo(2.0));
            Assert.That(r, Is.EqualTo(1.0));
        }

        [Test]
        public void HalfDiscIsNotAStadium()
        {
            Assert.That(StadiumMic.TryGet(new WKTReader().Read(HalfDisc),
                out _, out _, out _), Is.False);
        }

        [Test]
        public void HoledStadiumStamps()
        {
            Assert.That(StadiumMic.TryGet(new WKTReader().Read(HoledStadium),
                out _, out _, out _), Is.False);
        }
    }
}
