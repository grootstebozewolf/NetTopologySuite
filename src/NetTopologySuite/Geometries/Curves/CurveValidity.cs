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
//     IsValid == false, and TryFindDefiniteInvalidity names the clause. Rung 1
//     implements the cheap rules: per-arc-segment start != end (§7.3.1 Desc 6),
//     the 2n+1 well-formedness count shape (§7.3.1 Desc 7), compound
//     contiguity (§7.10.1 Desc 7), component well-formedness propagation
//     (§7.10.1 Desc 3), and curve-polygon ring closure (§8.2.1 Desc 2-3,
//     closed half).
//   * FAIL CLOSED -- a value passing every implemented rule THROWS, naming the
//     missing rung: curve simplicity (the other half of "ring", §4.2.4) needs
//     arc-arc intersection machinery -- the 615-h lane. Its first rung
//     (single-segment IsSimple, ticket 615-h, #624) is landed on
//     CircularString; wiring decided simplicity into THIS verdict and the
//     multi-segment case are NetTopologySuite.Proofs issue #630. An UNCHECKED
//     true is never returned; that silent-green failure mode is what Proofs
//     issue #522 exists to kill.
//
// Whole circles: a single segment with start == end is INVALID here (Desc 6);
// the spec reserves full circles for ST_Circle (§4.2.7, parked in the zoo
// backlog). Until ST_Circle exists, write a full circle in the two-segment
// five-point CIRCULARSTRING idiom -- it is Desc-6-clean and, like every clean
// value, fail-closed rather than false.
//
// MultiCurve / MultiSurface deliberately have NO rung-1 override: their
// members reach IsValidOp's default arm, which throws (fail-closed with a
// bare type name). Wiring them through this rung is later 615-h-lane work;
// no curve-containing geometry has a silent-true path either way.

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
        /// when an implemented clause rule is provably violated
        /// (<see cref="TryFindDefiniteInvalidity"/> names the clause),
        /// otherwise a <see cref="NotSupportedException"/> naming the missing
        /// rung.
        /// </summary>
        public static bool IsValidRung1(Geometry g)
        {
            if (g.IsEmpty) return true;
            if (TryFindDefiniteInvalidity(g, out _)) return false;
            throw Rung2Pending(g);
        }

        /// <summary>
        /// True when an implemented rung-1 rule is provably violated, with
        /// <paramref name="reason"/> naming the violated clause — never a full
        /// validity verdict, only the definite-false half of it.
        /// </summary>
        public static bool TryFindDefiniteInvalidity(Geometry g, out string reason)
        {
            switch (g)
            {
                case CircularString cs:
                    return TryFindDefiniteInvalidity(cs, out reason);
                case CompoundCurve cc:
                    return TryFindDefiniteInvalidity(cc, out reason);
                case CurvePolygon cp:
                    return TryFindDefiniteInvalidity(cp, out reason);
                case LineString ls when !ls.IsValid:
                    // Fully supported classical type: its complete validity is
                    // decidable today, so a false here is definite.
                    reason = "classical LineString validity failed (IsValidOp).";
                    return true;
                default:
                    // Not a rung-1 type (or a valid classical value): no
                    // implemented rule can refute it.
                    reason = null;
                    return false;
            }
        }

        private static bool TryFindDefiniteInvalidity(CircularString cs, out string reason)
        {
            var seq = cs.CoordinateSequence;
            int count = seq.Count;
            // §7.3.1 Desc 7 (re-asserted; intake enforces it, serialization
            // may not): 2n+1 points, n >= 1.
            if (count != 0 && (count < 3 || count % 2 == 0))
            {
                reason = "ISO/IEC 13249-3 §7.3.1 Desc 7: a CircularString needs " +
                    "2n+1 control points (n >= 1); found " + count + ".";
                return true;
            }
            for (int i = 0; i + 2 < count; i += 2)
            {
                // §7.3.1 Desc 6: the end point of each segment shall be
                // distinct from its start point.
                if (seq.GetCoordinate(i).Equals2D(seq.GetCoordinate(i + 2)))
                {
                    reason = "ISO/IEC 13249-3 §7.3.1 Desc 6: arc segment " + (i / 2) +
                        " has coincident start and end points. A whole circle is " +
                        "ST_Circle's job (§4.2.7); write it as the two-segment " +
                        "five-point CIRCULARSTRING idiom.";
                    return true;
                }
            }
            reason = null;
            return false;
        }

        private static bool TryFindDefiniteInvalidity(CompoundCurve cc, out string reason)
        {
            var curves = cc.Curves;
            Curve previous = null;
            for (int i = 0; i < curves.Count; i++)
            {
                // Empty components carry no endpoints and no rules of their
                // own; skip them for both checks (the constructor drops them,
                // but a deserialized value may not have gone through it).
                if (curves[i].IsEmpty) continue;
                // §7.10.1 Desc 3: well formed only if every component is.
                if (TryFindDefiniteInvalidity(curves[i], out string inner))
                {
                    reason = "component " + i + ": " + inner;
                    return true;
                }
                // §7.10.1 Desc 7 contiguity (re-asserted; intake enforces it),
                // between consecutive NON-empty components.
                if (previous != null && !previous.EndPoint.Coordinate
                        .Equals2D(curves[i].StartPoint.Coordinate))
                {
                    reason = "ISO/IEC 13249-3 §7.10.1 Desc 7: component " + i +
                        " does not start at its predecessor's end point.";
                    return true;
                }
                previous = curves[i];
            }
            reason = null;
            return false;
        }

        private static bool TryFindDefiniteInvalidity(CurvePolygon cp, out string reason)
        {
            for (int i = -1; i < cp.NumInteriorRings; i++)
            {
                var ring = i < 0 ? cp.ExteriorRing : cp.GetInteriorRingN(i);
                string label = i < 0 ? "exterior ring" : "interior ring " + i;
                if (ring == null || ring.IsEmpty) continue;
                // §8.2.1 Desc 2-3, closed half (re-asserted; intake enforces
                // it): rings are closed curves.
                if (ring is Curve c && !c.IsClosed)
                {
                    reason = "ISO/IEC 13249-3 §8.2.1 Desc 2-3: the " + label +
                        " is not closed.";
                    return true;
                }
                if (TryFindDefiniteInvalidity(ring, out string inner))
                {
                    reason = label + ": " + inner;
                    return true;
                }
            }
            reason = null;
            return false;
        }

        /// <summary>
        /// The rung-2 fail-closed signal: this value passes every implemented
        /// rung-1 rule, and the remaining validity obligations (curve
        /// simplicity per §4.2.4 / §8.2.1 — arc-arc intersection work, plus
        /// wiring already-decided simplicity into this verdict) are the
        /// 615-h lane, continued at issue #630 in NetTopologySuite.Proofs.
        /// Returning <c>true</c> without them would be an unchecked claim.
        /// </summary>
        private static NotSupportedException Rung2Pending(Geometry g)
        {
            return new NotSupportedException(
                $"Arc-aware IsValid for {g.GeometryType} is partial (rung 1): this value passes the " +
                "implemented ISO/IEC 13249-3 clause checks, but the simplicity half of validity is " +
                "still pending here (the 615-h lane, continued at NetTopologySuite.Proofs issue #630 — " +
                "single-segment IsSimple is decided; wiring it into IsValid and the multi-segment case " +
                "are #630 work). A checked 'true' is not possible yet; an unchecked 'true' is never returned.");
        }
    }
}
