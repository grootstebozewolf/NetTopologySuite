// SPDX-License-Identifier: BSD-3-Clause
//
// AI assistance disclosure:
//   AI-drafted, human-reviewed and curated. Per the same convention used for the
//   companion JTS prototype at grootstebozewolf/jts#1, AI-generated portions are
//   dedicated to CC0-1.0; human curation falls under the NTS BSD-3-Clause grant.
//
// Arc-aware Distance, point×curve slice (ISO/IEC 13249-3 §5.1.41 Desc 2a;
// NetTopologySuite.Proofs #615, ticket 615-f). Serves exactly the pairs whose
// exact answer the CircularArcGeometry seam can give today: a Point against a
// CircularString or CompoundCurve, either operand order. Every other
// curve-containing pair stays fail-closed in the DistanceOp constructor —
// curve×curve needs arc-arc machinery (the 615-h lane), and an unchecked
// chord answer is never returned.

namespace NetTopologySuite.Geometries.Curves
{
    /// <summary>
    /// Exact point-to-curve distance over the arc locus, for the pairs the
    /// arc seam covers; see <see cref="CircularArcGeometry.SegmentDistance"/>.
    /// </summary>
    internal static class CurveDistance
    {
        /// <summary>
        /// Computes the exact distance when the pair is (Point, CircularString
        /// | CompoundCurve) in either order; returns <c>false</c> otherwise
        /// so the caller falls through to the classical pipeline (whose
        /// constructor keeps every other curve-containing pair fail-closed).
        /// Empty operands answer 0, matching <c>DistanceOp.Distance()</c>.
        /// </summary>
        public static bool TryDistance(Geometry g0, Geometry g1, out double distance)
        {
            distance = double.NaN;
            Point point;
            Geometry curve;
            if (g0 is Point pt0 && IsServedCurve(g1)) { point = pt0; curve = g1; }
            else if (g1 is Point pt1 && IsServedCurve(g0)) { point = pt1; curve = g0; }
            else return false;

            if (point.IsEmpty || curve.IsEmpty)
            {
                distance = 0.0;
                return true;
            }
            distance = ToCurve(point.Coordinate, curve);
            return true;
        }

        private static bool IsServedCurve(Geometry g) =>
            g is CircularString || g is CompoundCurve;

        private static double ToCurve(Coordinate c, Geometry curve)
        {
            switch (curve)
            {
                case CircularString cs:
                    return ToCircularString(c, cs);
                case CompoundCurve cc:
                {
                    double min = double.PositiveInfinity;
                    foreach (var component in cc.Curves)
                    {
                        double d = component is CircularString arc
                            ? ToCircularString(c, arc)
                            // LineString component: classical, fully supported.
                            : Operation.Distance.DistanceOp.Distance(
                                curve.Factory.CreatePoint(c), component);
                        if (d < min) min = d;
                    }
                    return min;
                }
                default:
                    throw new System.ArgumentException("not a served curve", nameof(curve));
            }
        }

        private static double ToCircularString(Coordinate c, CircularString cs)
        {
            var seq = cs.CoordinateSequence;
            double min = double.PositiveInfinity;
            for (int i = 0; i + 2 < seq.Count; i += 2)
            {
                double d = CircularArcGeometry.SegmentDistance(
                    c, seq.GetCoordinate(i), seq.GetCoordinate(i + 1), seq.GetCoordinate(i + 2));
                if (d < min) min = d;
            }
            return min;
        }
    }
}
