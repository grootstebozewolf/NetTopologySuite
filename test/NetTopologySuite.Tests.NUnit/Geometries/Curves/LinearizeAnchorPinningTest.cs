// SPDX-License-Identifier: BSD-3-Clause
//
// AI assistance disclosure:
//   AI-drafted, human-reviewed. Assisted-by: Cursor Grok 4.6
//   AI-generated portions dedicated to CC0-1.0; human curation BSD-3-Clause.

using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Curves;
using NUnit.Framework;

namespace NetTopologySuite.Tests.NUnit.Geometries.Curves
{
    /// <summary>
    /// Control points survive Linearize(tolerance) exactly.
    /// Witness: CIRCULARSTRING (1 0, 0 1, -1 0) keeps exact (0, 1).
    /// Port of JTS <c>f6347444</c>.
    /// </summary>
    public class LinearizeAnchorPinningTest
    {
        private readonly GeometryFactory _factory = new GeometryFactory();

        [Test]
        public void MidControlExactOnSweepTie()
        {
            var cs = Make((1, 0), (0, 1), (-1, 0));
            var ls = cs.Linearize(0.01);
            Assert.That(ContainsExactly(ls, new Coordinate(0, 1)), Is.True);
        }

        [Test]
        public void AllControlsOfTwoArcStringSurvive()
        {
            var cs = Make((1, 0), (0, 1), (-1, 0), (-2, -1), (-3, 0));
            var ls = cs.Linearize(0.01);
            Assert.That(ContainsExactly(ls, new Coordinate(1, 0)), Is.True);
            Assert.That(ContainsExactly(ls, new Coordinate(0, 1)), Is.True);
            Assert.That(ContainsExactly(ls, new Coordinate(-1, 0)), Is.True);
            Assert.That(ContainsExactly(ls, new Coordinate(-2, -1)), Is.True);
            Assert.That(ContainsExactly(ls, new Coordinate(-3, 0)), Is.True);
        }

        private CircularString Make(params (double x, double y)[] pts)
        {
            var coords = new Coordinate[pts.Length];
            for (int i = 0; i < pts.Length; i++)
            {
                coords[i] = new Coordinate(pts[i].x, pts[i].y);
            }
            return new CircularString(_factory.CoordinateSequenceFactory.Create(coords), _factory);
        }

        private static bool ContainsExactly(Geometry g, Coordinate anchor)
        {
            foreach (var p in g.Coordinates)
            {
                if (p.Equals2D(anchor))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
