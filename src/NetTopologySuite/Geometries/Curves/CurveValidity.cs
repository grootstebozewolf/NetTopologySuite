// SPDX-License-Identifier: BSD-3-Clause
//
// AI assistance disclosure:
//   AI-drafted, human-reviewed and curated. Per the same convention used for the
//   companion JTS prototype at grootstebozewolf/jts#1, AI-generated portions are
//   dedicated to CC0-1.0; human curation falls under the NTS BSD-3-Clause grant.
//
// Arc-aware ST_IsValid, rung 1 (ISO/IEC 13249-3; NetTopologySuite.Proofs #615,
// ticket 615-g / ADR-0005). The honesty contract:
//
//   * DEFINITE FALSE -- a value violating an implemented clause rule returns
//     IsValid == false. Rung 1 implements the cheap rules: per-arc-segment
//     start != end (§7.3.1 Desc 6), the 2n+1 well-formedness count shape
//     (§7.3.1 Desc 7), compound contiguity (§7.10.1 Desc 7), component
//     well-formedness propagation (§7.10.1 Desc 3), and curve-polygon ring
//     closure (§8.2.1 Desc 2-3, closed half).
//   * FAIL CLOSED -- a value passing every implemented rule THROWS, naming the
//     missing rung: curve simplicity (the other half of "ring", §4.2.4) needs
//     arc-arc intersection machinery, which is rung 2 -- ticket 615-h (#624)
//     in NetTopologySuite.Proofs. An UNCHECKED true is never returned; that
//     silent-green failure mode is what Proofs issue #522 exists to kill.
//
// Whole circles: a single segment with start == end is INVALID here (Desc 6);
// the spec reserves full circles for ST_Circle (§4.2.7, parked in the zoo
// backlog). Until ST_Circle exists, write a full circle in the two-segment
// five-point CIRCULARSTRING idiom -- it is Desc-6-clean and, like every clean
// value, fail-closed rather than false.

using System;

namespace NetTopologySuite.Geometries.Curves
{
    /// <summary>
    /// Rung-1 arc-aware validity: definite-false detection for the cheap
    /// ISO/IEC 13249-3 clause rules, fail-closed for everything that still
    /// needs arc-arc machinery (rung 2, NetTopologySuite.Proofs ticket 615-h).
    /// </summary>
    internal static class CurveValidity
    {
        /// <summary>
        /// The rung-1 verdict for <paramref name="g"/>: <c>true</c> for the
        /// empty value (always valid, matching <c>IsValidOp</c>), <c>false</c>
        /// when an implemented clause rule is provably violated, otherwise a
        /// <see cref="NotSupportedException"/> naming the missing rung.
        /// </summary>
        public static bool IsValidRung1(Geometry g)
        {
            if (g.IsEmpty) return true;
            if (HasDefiniteInvalidity(g)) return false;
            throw Rung2Pending(g);
        }

        /// <summary>
        /// True when an implemented rung-1 rule is provably violated — never a
        /// full validity verdict, only the definite-false half of it.
        /// </summary>
        private static bool HasDefiniteInvalidity(Geometry g)
        {
            switch (g)
            {
                case CircularString cs:
                    return HasDefiniteInvalidity(cs);
                case CompoundCurve cc:
                    return HasDefiniteInvalidity(cc);
                case CurvePolygon cp:
                    return HasDefiniteInvalidity(cp);
                case LineString ls:
                    // Fully supported classical type: its complete validity is
                    // decidable today, so a false here is definite.
                    return !ls.IsValid;
                default:
                    // Not a rung-1 type: no implemented rule can refute it.
                    return false;
            }
        }

        private static bool HasDefiniteInvalidity(CircularString cs)
        {
            var seq = cs.CoordinateSequence;
            int count = seq.Count;
            // §7.3.1 Desc 7 (re-asserted; intake enforces it, serialization
            // may not): 2n+1 points, n >= 1.
            if (count != 0 && (count < 3 || count % 2 == 0))
                return true;
            for (int i = 0; i + 2 < count; i += 2)
            {
                // §7.3.1 Desc 6: the end point of each segment shall be
                // distinct from its start point.
                if (seq.GetCoordinate(i).Equals2D(seq.GetCoordinate(i + 2)))
                    return true;
            }
            return false;
        }

        private static bool HasDefiniteInvalidity(CompoundCurve cc)
        {
            var curves = cc.Curves;
            for (int i = 0; i < curves.Count; i++)
            {
                // §7.10.1 Desc 3: well formed only if every component is.
                if (curves[i].IsEmpty) continue;
                if (HasDefiniteInvalidity(curves[i]))
                    return true;
                // §7.10.1 Desc 7 contiguity (re-asserted; intake enforces it).
                if (i > 0 && !curves[i - 1].EndPoint.Coordinate
                        .Equals2D(curves[i].StartPoint.Coordinate))
                    return true;
            }
            return false;
        }

        private static bool HasDefiniteInvalidity(CurvePolygon cp)
        {
            for (int i = -1; i < cp.NumInteriorRings; i++)
            {
                var ring = i < 0 ? cp.ExteriorRing : cp.GetInteriorRingN(i);
                if (ring == null || ring.IsEmpty) continue;
                // §8.2.1 Desc 2-3, closed half (re-asserted; intake enforces
                // it): rings are closed curves.
                if (ring is Curve c && !c.IsClosed)
                    return true;
                if (HasDefiniteInvalidity(ring))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// The rung-2 fail-closed signal: this value passes every implemented
        /// rung-1 rule, and the remaining validity obligations (curve
        /// simplicity per §4.2.4 / §8.2.1 — arc-arc intersection work) are
        /// ticket 615-h (#624) in NetTopologySuite.Proofs. Returning
        /// <c>true</c> without them would be an unchecked claim.
        /// </summary>
        private static NotSupportedException Rung2Pending(Geometry g)
        {
            return new NotSupportedException(
                $"Arc-aware IsValid for {g.GeometryType} is partial (rung 1): this value passes the " +
                "implemented ISO/IEC 13249-3 clause checks, but curve simplicity needs arc-arc " +
                "intersection (rung 2, NetTopologySuite.Proofs ticket 615-h). " +
                "A checked 'true' is not possible yet; an unchecked 'true' is never returned.");
        }
    }
}
