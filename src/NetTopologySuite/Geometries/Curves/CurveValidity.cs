// SPDX-License-Identifier: BSD-3-Clause
//
// AI assistance disclosure:
//   AI-drafted, human-reviewed and curated. Per the same convention used for the
//   companion JTS prototype at grootstebozewolf/jts#1, AI-generated portions are
//   dedicated to CC0-1.0; human curation falls under the NTS BSD-3-Clause grant.
//
// Arc-aware ST_IsValid (ISO/IEC 13249-3; NetTopologySuite.Proofs #615,
// tickets 615-g rung 1 / 615-h #634 verdict wiring / 615-h #639 collection
// arms / ADR-0005). The honesty contract:
//
//   * DEFINITE FALSE -- a value violating an implemented clause rule returns
//     IsValid == false, and TryFindDefiniteInvalidity names the clause: per-
//     arc-segment start != end (§7.3.1 Desc 6), the 2n+1 well-formedness
//     count shape (§7.3.1 Desc 7), compound contiguity (§7.10.1 Desc 7),
//     component well-formedness propagation (§7.10.1 Desc 3), curve-polygon
//     ring closure (§8.2.1 Desc 2-3, closed half), non-finite coordinates
//     (classical IsValidOp parity, not a clause rule), member/element
//     propagation for MultiCurve and MultiSurface (§10.1.1 Desc 10), and --
//     since the simplicity rungs -- a provably non-simple curve-polygon ring
//     (§8.2.1 Desc 2-3, simple half), a curve-polygon ring pair meeting in
//     more than one point or sharing a 1-D piece (§8.2.1 Desc 11), and a
//     multi-surface element pair whose boundaries share a 1-D piece
//     (§4.2.27: boundaries may intersect at a finite number of POINTS).
//   * CHECKED TRUE -- a clean CircularString or CompoundCurve is valid:
//     §7.3.1 Desc 6+7 and §7.10.1 Desc 3+7 are those types' COMPLETE
//     validity obligations ("simple ∧ closed ⇒ ring" is a definition, not a
//     constraint; the reading is pinned in the research doc §2). A clean
//     MultiCurve is valid: §10.1.1 Desc 10 (elements well formed) is its
//     complete obligation -- §10.3.1 Desc 4's inter-member condition defines
//     ST_IsSimple, not validity (reading pinned in the research doc §2).
//   * FAIL CLOSED -- a CurvePolygon passing everything still THROWS: with
//     Desc 11 counted, the remaining conditions (§8.2.1 Desc 12-14: no
//     spikes/cuts, connected interior -- and hole-inside-shell containment)
//     are undecided pending arc-aware point-in-ring -- the 615-h lane,
//     NetTopologySuite.Proofs issue #641. A MultiSurface passing everything
//     also THROWS: §4.2.27's "interiors shall not intersect" needs the same
//     containment machinery (#641). The simplicity kernel's own residues
//     (degenerate segments, near-cocircular band, large-radius conditioning)
//     also propagate as throws. An UNCHECKED true is never returned; that
//     silent-green failure mode is what Proofs issue #522 exists to kill.
//
// Whole circles: a single segment with start == end is INVALID here (Desc 6);
// the spec reserves full circles for ST_Circle (§4.2.7, parked in the zoo
// backlog). Until ST_Circle exists, write a full circle in the two-segment
// five-point CIRCULARSTRING idiom -- Desc-6-clean, and since #634 a checked
// VALID value (and, since #630, a checked simple ring).
//
// The rung-3 review demonstrated a silent-true HOLE here: with no MultiCurve
// / MultiSurface overrides, IsValidOp's GeometryCollection arm silently gave
// an all-classical-member MultiCurve classical GC validity and let an
// all-classical MultiSurface of two overlapping polygons answer true. Rung 4
// (#639) closed it: both types now route here (overrides + IsValidOp arms) --
// member propagation and the boundary conditions above decide what is
// decidable, and the interiors-disjoint residue fail-closes naming #641.

using System;

namespace NetTopologySuite.Geometries.Curves
{
    /// <summary>
    /// Arc-aware validity verdicts: definite-false detection for the
    /// ISO/IEC 13249-3 clause rules, checked <c>true</c> for clean
    /// CircularString/CompoundCurve/MultiCurve values, fail-closed only
    /// where a rule is genuinely undecided (CurvePolygon Desc 12–14 +
    /// containment, MultiSurface interiors-disjoint —
    /// NetTopologySuite.Proofs issue #641, the 615-h lane).
    /// </summary>
    internal static class CurveValidity
    {
        /// <summary>
        /// The validity verdict for <paramref name="g"/> (615-h rungs 3–4,
        /// #634/#639): <c>true</c> for the empty value (always valid,
        /// matching <c>IsValidOp</c>); <c>false</c> when an implemented
        /// clause rule is provably violated
        /// (<see cref="TryFindDefiniteInvalidity"/> names the clause). A
        /// clean CircularString or CompoundCurve is checked <c>true</c> —
        /// §7.3.1 Desc 6+7 and §7.10.1 Desc 3+7 are those types' complete
        /// validity obligations ("simple ∧ closed ⇒ ring" is a definition,
        /// not a constraint) — and so is a clean MultiCurve (§10.1.1
        /// Desc 10, elements well formed, is its complete obligation; both
        /// readings pinned in the research doc's §2). A CurvePolygon with a
        /// provably non-simple ring (§8.2.1 Desc 2–3) or a ring pair meeting
        /// in more than one point (Desc 11) is definite <c>false</c>; one
        /// passing everything throws naming the still-undecided Desc 12–14 +
        /// containment. A MultiSurface with an element-boundary pair sharing
        /// a 1-D piece is definite <c>false</c> (§4.2.27); one passing
        /// everything throws naming the undecided interiors-disjoint
        /// condition. Never an unchecked <c>true</c>.
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
                case MultiCurve _:
                    // §10.1.1 Desc 10 (all elements well formed) is already
                    // ruled in above; §10.3.1 Desc 4's inter-member condition
                    // defines ST_IsSimple, not validity — so nothing is left
                    // to check (615-h rung 4, #639; reading pinned in the
                    // research doc §2).
                    return true;
                case CurvePolygon cp:
                    // Ring simplicity is decidable since the simplicity rungs
                    // (#630/#634); the kernel's fail-closed residues propagate
                    // as throws.
                    if (!RingsAllSimple(cp))
                        return false;
                    // §8.2.1 Desc 11 (615-h rung 4, #639): the boundaries of
                    // any two rings may intersect in at most one point — a
                    // shared 1-D piece or ≥ 2 distinct contacts is definite
                    // false.
                    if (RingPairCountRefutes(cp))
                        return false;
                    throw RingPairConditionsPending(cp);
                case MultiSurface ms:
                    if (!ElementRingsAllSimple(ms))
                        return false;
                    if (AnyElementRingPairRefuted(ms))
                        return false;
                    // §4.2.27 (615-h rung 4, #639): element boundaries may
                    // intersect at a finite number of POINTS — any count of
                    // point contacts passes, a shared 1-D piece is definite
                    // false.
                    if (CrossElementBoundaryOverlap(ms))
                        return false;
                    throw InteriorsDisjointPending(ms);
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
        /// §8.2.1 Desc 11 (615-h rung 4, #639): <c>true</c> when some pair of
        /// this CurvePolygon's rings provably intersects in more than one
        /// point — a shared 1-D piece, or two or more distinct
        /// (tolerance-deduplicated) contact points. Kernel residues throw.
        /// </summary>
        private static bool RingPairCountRefutes(CurvePolygon cp)
        {
            var rings = CollectRings(cp);
            var contacts = new System.Collections.Generic.List<Coordinate>();
            for (int i = 0; i < rings.Count; i++)
            {
                for (int j = i + 1; j < rings.Count; j++)
                {
                    contacts.Clear();
                    CurveSimplicity.RingPairContacts(cp, rings[i], rings[j], contacts, out bool overlap);
                    if (overlap || contacts.Count >= 2)
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// §8.2.1 Desc 2–3 propagated through a MultiSurface's elements:
        /// <c>false</c> when a CurvePolygon element has a provably non-simple
        /// ring (a classically invalid Polygon element is already caught by
        /// <see cref="TryFindDefiniteInvalidity(Geometry, out string)"/>).
        /// </summary>
        private static bool ElementRingsAllSimple(MultiSurface ms)
        {
            for (int e = 0; e < ms.NumGeometries; e++)
            {
                if (ms.GetGeometryN(e) is CurvePolygon cp && !cp.IsEmpty && !RingsAllSimple(cp))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// §8.2.1 Desc 11 propagated through a MultiSurface's elements: a
        /// CurvePolygon element with a refuted ring pair makes the whole
        /// value definite <c>false</c>.
        /// </summary>
        private static bool AnyElementRingPairRefuted(MultiSurface ms)
        {
            for (int e = 0; e < ms.NumGeometries; e++)
            {
                if (ms.GetGeometryN(e) is CurvePolygon cp && !cp.IsEmpty && RingPairCountRefutes(cp))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// §4.2.27 (615-h rung 4, #639): <c>true</c> when the boundaries of
        /// two DIFFERENT elements provably share a 1-D piece. Point contacts
        /// — however many — do not refute here: the clause permits "a finite
        /// number of points", unlike the per-polygon Desc 11 count.
        /// </summary>
        private static bool CrossElementBoundaryOverlap(MultiSurface ms)
        {
            var elementRings = new System.Collections.Generic.List<System.Collections.Generic.List<Curve>>();
            for (int e = 0; e < ms.NumGeometries; e++)
            {
                var element = ms.GetGeometryN(e);
                if (element.IsEmpty) continue;
                elementRings.Add(CollectRings(element));
            }
            var contacts = new System.Collections.Generic.List<Coordinate>();
            for (int a = 0; a < elementRings.Count; a++)
            {
                for (int b = a + 1; b < elementRings.Count; b++)
                {
                    foreach (var ringA in elementRings[a])
                    {
                        foreach (var ringB in elementRings[b])
                        {
                            contacts.Clear();
                            CurveSimplicity.RingPairContacts(ms, ringA, ringB, contacts, out bool overlap);
                            if (overlap)
                                return true;
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// The non-empty rings of a surface (CurvePolygon or classical
        /// Polygon — both ring types are <see cref="Curve"/>s on this
        /// branch).
        /// </summary>
        private static System.Collections.Generic.List<Curve> CollectRings(Geometry surface)
        {
            var rings = new System.Collections.Generic.List<Curve>();
            switch (surface)
            {
                case CurvePolygon cp:
                    for (int i = -1; i < cp.NumInteriorRings; i++)
                    {
                        var ring = i < 0 ? cp.ExteriorRing : cp.GetInteriorRingN(i);
                        if (ring != null && !ring.IsEmpty)
                            rings.Add(ring);
                    }
                    break;
                case Polygon p:
                    for (int i = -1; i < p.NumInteriorRings; i++)
                    {
                        var ring = i < 0 ? p.ExteriorRing : p.GetInteriorRingN(i);
                        if (ring != null && !ring.IsEmpty)
                            rings.Add(ring);
                    }
                    break;
                default:
                    throw new NotSupportedException(
                        $"Arc-aware IsValid has no ring model for {surface.GeometryType}.");
            }
            return rings;
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
                case MultiCurve mc:
                    // §10.1.1 Desc 10: a collection is well formed only if
                    // all its elements are (615-h rung 4, #639).
                    return TryFindDefiniteElementInvalidity(mc, "member", out reason);
                case MultiSurface ms:
                    return TryFindDefiniteElementInvalidity(ms, "element", out reason);
                case LineString ls when !ls.IsValid:
                    // Fully supported classical type: its complete validity is
                    // decidable today, so a false here is definite.
                    reason = "classical LineString validity failed (IsValidOp).";
                    return true;
                case Polygon p when !p.IsValid:
                    reason = "classical Polygon validity failed (IsValidOp).";
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
                // distinct from its start point. Desc 6 binds ONLY start and
                // end: an intermediate coincident with an endpoint makes the
                // triple exactly collinear (the cross is exactly zero), so
                // Desc 8b applies and the segment is the legal start–end
                // chord — the sub-reading of record, pinned in the research
                // doc §2.1 and by IsValid_CoincidentIntermediate_… tests.
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
        /// §10.1.1 Desc 10 propagation (615-h rung 4, #639): a collection is
        /// well formed only if all its elements are — a definitely invalid
        /// member/element makes the collection definite <c>false</c>. Empty
        /// elements carry no rules of their own.
        /// </summary>
        private static bool TryFindDefiniteElementInvalidity(
            GeometryCollection gc, string label, out string reason)
        {
            for (int i = 0; i < gc.NumGeometries; i++)
            {
                var element = gc.GetGeometryN(i);
                if (element.IsEmpty) continue;
                if (TryFindDefiniteInvalidity(element, out string inner))
                {
                    reason = label + " " + i + ": " + inner;
                    return true;
                }
            }
            reason = null;
            return false;
        }

        /// <summary>
        /// The CurvePolygon fail-closed signal: every implemented rule
        /// passes, all rings are provably simple, and (since rung 4, #639)
        /// no ring pair meets in more than one point — but the remaining
        /// polygon conditions (§8.2.1 Desc 12–14: no spikes or cuts,
        /// connected interior — and hole-inside-shell containment) need
        /// arc-aware point-in-ring, still pending — the 615-h lane,
        /// continued at issue #641 in NetTopologySuite.Proofs. Returning
        /// <c>true</c> without them would be an unchecked claim.
        /// </summary>
        private static NotSupportedException RingPairConditionsPending(CurvePolygon cp)
        {
            return new NotSupportedException(
                $"Arc-aware IsValid for {cp.GeometryType} is partial: this value passes the implemented " +
                "ISO/IEC 13249-3 clause checks, every ring is provably simple, and no ring pair meets in " +
                "more than one point (8.2.1 Desc 11), but the remaining conditions (8.2.1 Desc 12-14: " +
                "no spikes/cuts, connected interior - plus hole-inside-shell containment) need arc-aware " +
                "point-in-ring and are still pending (the 615-h lane, NetTopologySuite.Proofs issue #641). " +
                "A checked 'true' is not possible yet; an unchecked 'true' is never returned.");
        }

        /// <summary>
        /// The MultiSurface fail-closed signal: elements are individually
        /// unrefuted and no element-boundary pair shares a 1-D piece, but
        /// §4.2.27's "the interiors of any two ST_Surface values … shall not
        /// intersect" needs the same containment machinery as the
        /// CurvePolygon conditions — the 615-h lane, NetTopologySuite.Proofs
        /// issue #641.
        /// </summary>
        private static NotSupportedException InteriorsDisjointPending(MultiSurface ms)
        {
            return new NotSupportedException(
                $"Arc-aware IsValid for {ms.GeometryType} is partial: this value passes the implemented " +
                "ISO/IEC 13249-3 clause checks and no element-boundary pair shares a 1-D piece (4.2.27 " +
                "permits boundary contact at a finite number of points), but whether the element INTERIORS " +
                "are pairwise disjoint (4.2.27) needs arc-aware containment and is still pending (the " +
                "615-h lane, NetTopologySuite.Proofs issue #641). A checked 'true' is not possible yet; " +
                "an unchecked 'true' is never returned.");
        }
    }
}
