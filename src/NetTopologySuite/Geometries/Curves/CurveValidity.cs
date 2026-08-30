// SPDX-License-Identifier: BSD-3-Clause
//
// AI assistance disclosure:
//   AI-drafted, human-reviewed and curated. Per the same convention used for the
//   companion JTS prototype at grootstebozewolf/jts#1, AI-generated portions are
//   dedicated to CC0-1.0; human curation falls under the NTS BSD-3-Clause grant.
//
// Arc-aware ST_IsValid (ISO/IEC 13249-3; NetTopologySuite.Proofs #615,
// tickets 615-g rung 1 / 615-h #634 verdict wiring / ADR-0005). The honesty
// contract:
//
//   * DEFINITE FALSE -- a value violating an implemented clause rule returns
//     IsValid == false, and TryFindDefiniteInvalidity names the clause: per-
//     arc-segment start != end (§7.3.1 Desc 6), the 2n+1 well-formedness
//     count shape (§7.3.1 Desc 7), compound contiguity (§7.10.1 Desc 7),
//     component well-formedness propagation (§7.10.1 Desc 3), curve-polygon
//     ring closure (§8.2.1 Desc 2-3, closed half), non-finite coordinates
//     (classical IsValidOp parity, not a clause rule), and -- since the
//     simplicity rungs -- a provably non-simple curve-polygon ring (§8.2.1
//     Desc 2-3, simple half).
//   * CHECKED TRUE -- a clean CircularString or CompoundCurve is valid:
//     §7.3.1 Desc 6+7 and §7.10.1 Desc 3+7 are those types' COMPLETE
//     validity obligations ("simple ∧ closed ⇒ ring" is a definition, not a
//     constraint; the reading is pinned in the research doc §2).
//   * FAIL CLOSED -- a CurvePolygon passing everything still THROWS: the
//     ring-pair conditions (§8.2.1 Desc 11-14) are undecided -- the 615-h
//     lane, NetTopologySuite.Proofs issue #639. The simplicity kernel's own
//     residues (degenerate segments, near-cocircular band, large-radius
//     conditioning) also propagate as throws. An UNCHECKED true is never
//     returned; that silent-green failure mode is what Proofs issue #522
//     exists to kill.
//
// Whole circles: a single segment with start == end is INVALID here (Desc 6);
// the spec reserves full circles for ST_Circle (§4.2.7, parked in the zoo
// backlog). Until ST_Circle exists, write a full circle in the two-segment
// five-point CIRCULARSTRING idiom -- Desc-6-clean, and since #634 a checked
// VALID value (and, since #630, a checked simple ring).
//
// MultiCurve / MultiSurface deliberately have NO override here: their
// members reach IsValidOp's default arm, which throws (fail-closed with a
// bare type name). Wiring them through is NetTopologySuite.Proofs issue
// #639; no curve-containing geometry has a silent-true path either way.

using System;

namespace NetTopologySuite.Geometries.Curves
{
    /// <summary>
    /// Arc-aware validity verdicts: definite-false detection for the
    /// ISO/IEC 13249-3 clause rules, checked <c>true</c> for clean
    /// CircularString/CompoundCurve values, fail-closed only where a rule
    /// is genuinely undecided (CurvePolygon ring-pair conditions —
    /// NetTopologySuite.Proofs issue #639, the 615-h lane).
    /// </summary>
    internal static class CurveValidity
    {
        /// <summary>
        /// The validity verdict for <paramref name="g"/> (615-h rung 3,
        /// #634): <c>true</c> for the empty value (always valid, matching
        /// <c>IsValidOp</c>); <c>false</c> when an implemented clause rule is
        /// provably violated (<see cref="TryFindDefiniteInvalidity"/> names
        /// the clause). A clean CircularString or CompoundCurve is checked
        /// <c>true</c> — §7.3.1 Desc 6+7 and §7.10.1 Desc 3+7 are those
        /// types' complete validity obligations ("simple ∧ closed ⇒ ring" is
        /// a definition, not a constraint; the reading is pinned in the
        /// research doc's §2). A CurvePolygon with a provably non-simple ring
        /// is definite <c>false</c> (§8.2.1 Desc 2–3); a CP passing
        /// everything throws naming the still-undecided ring-pair conditions
        /// (Desc 11–14). Never an unchecked <c>true</c>.
        /// </summary>
        public static bool IsValid(Geometry g)
        {
            if (g.IsEmpty) return true;
            if (TryFindDefiniteInvalidity(g, out _)) return false;
            switch (g)
            {
                case CircularString _:
                case CompoundCurve _:
                    return true;
                case CurvePolygon cp:
                    // Ring simplicity is decidable since the simplicity rungs
                    // (#630/#634); the kernel's fail-closed residues propagate
                    // as throws.
                    if (!RingsAllSimple(cp))
                        return false;
                    throw RingPairConditionsPending(cp);
                default:
                    throw new NotSupportedException(
                        $"Arc-aware IsValid has no verdict path for {g.GeometryType}.");
            }
        }

        private static bool RingsAllSimple(CurvePolygon cp)
        {
            for (int i = -1; i < cp.NumInteriorRings; i++)
            {
                var ring = i < 0 ? cp.ExteriorRing : cp.GetInteriorRingN(i);
                if (ring == null)
                    continue;
                if (!CurveSimplicity.RingIsSimple(ring))
                    return false;
            }
            return true;
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
            for (int i = 0; i < count; i++)
            {
                // Parity with classical IsValidOp, which marks non-finite
                // coordinates invalid (the spec does not contemplate NaN/Inf).
                // Needed now that a clean value returns a checked true.
                double x = seq.GetX(i), y = seq.GetY(i);
                if (double.IsNaN(x) || double.IsInfinity(x) || double.IsNaN(y) || double.IsInfinity(y))
                {
                    reason = "non-finite coordinate at index " + i +
                        " (classical IsValidOp parity; not a clause rule).";
                    return true;
                }
            }
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
        /// The CurvePolygon fail-closed signal: every implemented rule
        /// passes and all rings are provably simple, but the ring-pair
        /// conditions (§8.2.1 Desc 11–14: ring intersection at most one
        /// point, no spikes or cuts, connected interior) are still
        /// undecided — the 615-h lane, continued at issue #639 in
        /// NetTopologySuite.Proofs. Returning <c>true</c> without them
        /// would be an unchecked claim.
        /// </summary>
        private static NotSupportedException RingPairConditionsPending(CurvePolygon cp)
        {
            return new NotSupportedException(
                $"Arc-aware IsValid for {cp.GeometryType} is partial: this value passes the implemented " +
                "ISO/IEC 13249-3 clause checks and every ring is provably simple, but the ring-pair " +
                "conditions (8.2.1 Desc 11-14: ring intersection at most one point, no spikes/cuts, " +
                "connected interior) are still pending (the 615-h lane, NetTopologySuite.Proofs issue #639). " +
                "A checked 'true' is not possible yet; an unchecked 'true' is never returned.");
        }
    }
}
