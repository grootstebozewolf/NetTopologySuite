// SPDX-License-Identifier: BSD-3-Clause
//
// AI assistance disclosure:
//   AI-drafted, human-reviewed and curated. Per the same convention used for the
//   companion JTS prototype at grootstebozewolf/jts#1, AI-generated portions are
//   dedicated to CC0-1.0; human curation falls under the NTS BSD-3-Clause grant.
//
// Arc-aware ST_IsValid (ISO/IEC 13249-3; NetTopologySuite.Proofs #615,
// tickets 615-g rung 1 / 615-h #634 verdict wiring / 615-h #639 collection
// arms / 615-h #641 containment / ADR-0005). The honesty contract:
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
//     Since rung 5 (#641) also: a hole outside the exterior ring, a hole
//     inside another hole (§8.2.1 Desc 12), a ring touch graph with a cycle
//     (Desc 13: disconnected interior), and a multi-surface element pair
//     with an exact boundary point of one strictly inside the other
//     (§4.2.27 interiors-disjoint).
//   * CHECKED TRUE -- a clean CircularString or CompoundCurve is valid:
//     §7.3.1 Desc 6+7 and §7.10.1 Desc 3+7 are those types' COMPLETE
//     validity obligations ("simple ∧ closed ⇒ ring" is a definition, not a
//     constraint; the reading is pinned in the research doc §2). A clean
//     MultiCurve is valid: §10.1.1 Desc 10 (elements well formed) is its
//     complete obligation -- §10.3.1 Desc 4's inter-member condition defines
//     ST_IsSimple, not validity (reading pinned in the research doc §2).
//     Since rung 5 (#641) a clean CurvePolygon is checked true too: with
//     simple rings meeting pairwise in at most one point, Desc 12-14 reduce
//     to hole containment, no nested holes and an acyclic touch graph, each
//     decided by EXACT point-in-ring (CurveContainment). A clean MultiSurface
//     is checked true when it is all-classical (the classical MultiPolygon
//     rule is §4.2.27) or when every inter-element contact is a vertex both
//     boundaries share and every boundary segment's exact sample lies
//     outside the other element.
//   * FAIL CLOSED -- a MultiSurface element pair touching away from shared
//     vertices with no refuting sample (the piece between two contacts has
//     no exact sample yet), and the simplicity kernel's own residues (an
//     unanchored exact crossing pair inside the tangency or near-cocircular
//     band, large-radius conditioning) propagate as throws -- the 615-h lane,
//     NetTopologySuite.Proofs issue #981. An UNCHECKED true is never
//     returned; that silent-green failure mode is what Proofs issue #522
//     exists to kill.
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
// decidable; rung 5 (#641) decides interiors-disjoint where it can.

using System;
using System.Collections.Generic;

namespace NetTopologySuite.Geometries.Curves
{
    /// <summary>
    /// Arc-aware validity verdicts: definite-false detection for the
    /// ISO/IEC 13249-3 clause rules, checked <c>true</c> for clean
    /// CircularString/CompoundCurve/MultiCurve/CurvePolygon values and for
    /// MultiSurface values whose element contacts are shared vertices,
    /// fail-closed only where a decision is genuinely out of reach (the
    /// 615-h lane, NetTopologySuite.Proofs issue #981).
    /// </summary>
    internal static class CurveValidity
    {
        /// <summary>
        /// The validity verdict for <paramref name="g"/> (615-h rungs 3–5,
        /// #634/#639/#641): <c>true</c> for the empty value (always valid,
        /// matching <c>IsValidOp</c>); <c>false</c> when an implemented
        /// clause rule is provably violated
        /// (<see cref="TryFindDefiniteInvalidity"/> names the clause). A
        /// clean CircularString or CompoundCurve is checked <c>true</c> —
        /// §7.3.1 Desc 6+7 and §7.10.1 Desc 3+7 are those types' complete
        /// validity obligations ("simple ∧ closed ⇒ ring" is a definition,
        /// not a constraint) — and so is a clean MultiCurve (§10.1.1
        /// Desc 10, elements well formed, is its complete obligation; both
        /// readings pinned in the research doc's §2). A CurvePolygon with a
        /// provably non-simple ring (§8.2.1 Desc 2–3), a ring pair meeting in
        /// more than one point (Desc 11), a misplaced or nested hole
        /// (Desc 12) or a disconnected interior (Desc 13) is definite
        /// <c>false</c>, and a clean one is checked <c>true</c> (rung 5). A
        /// MultiSurface is decided per element and per element pair
        /// (§10.1.1 Desc 10, §4.2.27); see <see cref="MultiSurfaceIsValid"/>.
        /// Never an unchecked <c>true</c>.
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
                    var rings = CollectRings(cp);
                    var pairContact = new Coordinate[rings.Count, rings.Count];
                    if (RingPairCountRefutes(cp, rings, pairContact))
                        return false;
                    // §8.2.1 Desc 12–14 (615-h rung 5, #641): hole
                    // containment, no nested holes, connected interior —
                    // decided by exact point-in-ring over the loci.
                    return RingConfigurationIsValid(cp, rings, pairContact);
                case MultiSurface ms:
                    return MultiSurfaceIsValid(ms);
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
        /// (tolerance-deduplicated) contact points. Kernel residues are
        /// deferred: a definite refutation from any pair is sound regardless
        /// (an ambiguous pair can only ADD contacts), but with a residue and
        /// no refutation the contact count is uncertain, so this throws
        /// rather than letting the caller's pending message claim Desc 11
        /// passed.
        /// </summary>
        private static bool RingPairCountRefutes(
            CurvePolygon cp, List<Curve> rings, Coordinate[,] pairContact)
        {
            var contacts = new System.Collections.Generic.List<Coordinate>();
            NotSupportedException deferred = null;
            for (int i = 0; i < rings.Count; i++)
            {
                for (int j = i + 1; j < rings.Count; j++)
                {
                    contacts.Clear();
                    var residue = CurveSimplicity.RingPairContacts(
                        cp, rings[i], rings[j], contacts, out bool overlap);
                    if (overlap || contacts.Count >= 2)
                        return true;
                    if (residue != null)
                        deferred = deferred ?? residue;
                    if (contacts.Count == 1)
                        pairContact[i, j] = contacts[0];
                }
            }
            if (deferred != null)
                throw deferred;
            return false;
        }

        /// <summary>
        /// §8.2.1 Desc 12–14 for a CurvePolygon whose rings are simple and
        /// pairwise meet in at most one point (615-h rung 5, #641). With
        /// those two facts the remaining conditions reduce to three exact
        /// questions, each decided by a sample point per ring:
        /// <list type="bullet">
        /// <item>every hole lies inside the exterior ring — a hole minus its
        /// (at most one) contact is connected and misses the shell, so one
        /// sample off the shell decides the whole hole (Desc 12: a hole
        /// outside the shell is not in the closure of the interior);</item>
        /// <item>no hole lies inside another (Desc 12 again: the inner ring
        /// would bound nothing);</item>
        /// <item>the interior is connected (Desc 13): with the above, it is
        /// disconnected exactly when the touch graph — rings and contact
        /// points as nodes, one edge per ring through a contact — has a
        /// cycle (the classical polygon argument; one-point contacts between
        /// two Jordan curves are touches, never crossings).</item>
        /// </list>
        /// Desc 14 (every hole disconnects the exterior) holds for a simple
        /// hole strictly inside the shell and outside every other hole. A
        /// clean value is a checked <c>true</c>.
        /// </summary>
        private static bool RingConfigurationIsValid(
            CurvePolygon cp, List<Curve> rings, Coordinate[,] pairContact)
        {
            var exact = ExactRings(cp, rings);
            for (int k = 1; k < rings.Count; k++)
            {
                if (SideOf(cp, exact[k], exact[0], pairContact[0, k],
                        $"interior ring {k - 1} against the exterior ring") != CurveContainment.Location.Interior)
                    return false;
            }
            for (int i = 1; i < rings.Count; i++)
            {
                for (int j = 1; j < rings.Count; j++)
                {
                    if (i == j)
                        continue;
                    var contact = pairContact[System.Math.Min(i, j), System.Math.Max(i, j)];
                    if (SideOf(cp, exact[j], exact[i], contact,
                            $"interior ring {j - 1} against interior ring {i - 1}") == CurveContainment.Location.Interior)
                        return false;
                }
            }
            return !TouchGraphHasCycle(cp, rings.Count, pairContact);
        }

        /// <summary>
        /// The side of <paramref name="against"/> that the ring
        /// <paramref name="ring"/> lies on, from its first exact sample not
        /// on <paramref name="against"/> (Interior or Exterior). A sample ON
        /// the other ring is skipped only when it is the pair's reported
        /// contact — any other boundary hit means the double kernel missed a
        /// contact, and the verdict refuses rather than trusting either side.
        /// </summary>
        private static CurveContainment.Location SideOf(
            Geometry owner, List<CurveContainment.ExactSegment> ring,
            List<CurveContainment.ExactSegment> against, Coordinate contact, string what)
        {
            foreach (var sample in CurveContainment.Samples(ring, false))
            {
                var location = CurveContainment.LocateInRing(sample, against);
                if (location != CurveContainment.Location.Boundary)
                    return location;
                if (contact == null || !CurveSimplicity.Matches(sample.ToCoordinate(), contact))
                    throw ContactDisagreement(owner, what);
            }
            throw ContactDisagreement(owner, what);
        }

        /// <summary>
        /// §8.2.1 Desc 13 (connected interior): does the bipartite touch
        /// graph — ring nodes, contact-point nodes, an edge for every ring
        /// through a contact — contain a cycle? Contact points from
        /// different ring pairs are identified within the kernel's match
        /// tolerance. Identification can only ADD cycles, so "no cycle" is
        /// sound as it stands; a cycle that rests on a non-identical match
        /// refuses instead of answering a definite <c>false</c>.
        /// </summary>
        private static bool TouchGraphHasCycle(CurvePolygon cp, int ringCount, Coordinate[,] pairContact)
        {
            var points = new List<Coordinate>();
            var parent = new List<int>();
            for (int r = 0; r < ringCount; r++)
                parent.Add(r);
            var edges = new HashSet<(int Ring, int Node)>();
            bool inexactIdentification = false;
            bool cycle = false;
            for (int i = 0; i < ringCount; i++)
            {
                for (int j = i + 1; j < ringCount; j++)
                {
                    var contact = pairContact[i, j];
                    if (contact == null)
                        continue;
                    int node = -1;
                    for (int p = 0; p < points.Count; p++)
                    {
                        if (CurveSimplicity.Matches(contact, points[p]))
                        {
                            node = ringCount + p;
                            if (!contact.Equals2D(points[p]))
                                inexactIdentification = true;
                            break;
                        }
                    }
                    if (node < 0)
                    {
                        points.Add(contact);
                        node = parent.Count;
                        parent.Add(node);
                    }
                    foreach (int ring in new[] { i, j })
                    {
                        if (!edges.Add((ring, node)))
                            continue;
                        int rootRing = Find(parent, ring), rootNode = Find(parent, node);
                        if (rootRing == rootNode)
                            cycle = true;
                        else
                            parent[rootRing] = rootNode;
                    }
                }
            }
            if (cycle && inexactIdentification)
                throw CurvedGeometry.Refused(cp,
                    "a connected-interior decision (8.2.1 Desc 13) whose touch-graph cycle rests on two ring-pair " +
                    "contacts identified within tolerance but not bit-identical");
            return cycle;
        }

        private static int Find(List<int> parent, int x)
        {
            while (parent[x] != x)
            {
                parent[x] = parent[parent[x]];
                x = parent[x];
            }
            return x;
        }

        /// <summary>
        /// The MultiSurface verdict (615-h rung 5, #641): every element well
        /// formed (§10.1.1 Desc 10 — the full CurvePolygon verdict since this
        /// rung), and the interiors of any two elements disjoint with their
        /// boundaries meeting at most in finitely many points (§4.2.27). An
        /// all-classical value is exactly a classical MultiPolygon, whose
        /// validity rule is that same condition — decided by
        /// <c>IsValidOp</c>. Otherwise each element pair is decided by
        /// <see cref="ElementPairInteriorsDisjoint"/>; residues are deferred
        /// behind any definite refutation.
        /// </summary>
        private static bool MultiSurfaceIsValid(MultiSurface ms)
        {
            var elements = new List<(int Index, Geometry Element)>();
            bool allClassical = true;
            NotSupportedException deferred = null;
            for (int e = 0; e < ms.NumGeometries; e++)
            {
                var element = ms.GetGeometryN(e);
                if (element.IsEmpty)
                    continue;
                elements.Add((e, element));
                if (element is CurvePolygon cp)
                {
                    allClassical = false;
                    try
                    {
                        if (!IsValid(cp))
                            return false;
                    }
                    catch (NotSupportedException residue)
                    {
                        deferred = deferred ?? residue;
                    }
                }
                // A classical Polygon element already passed classical
                // IsValidOp in TryFindDefiniteInvalidity.
            }

            if (allClassical)
            {
                var polygons = new Polygon[elements.Count];
                for (int i = 0; i < elements.Count; i++)
                    polygons[i] = (Polygon)elements[i].Element;
                return ms.Factory.CreateMultiPolygon(polygons).IsValid;
            }

            for (int a = 0; a < elements.Count; a++)
            {
                for (int b = a + 1; b < elements.Count; b++)
                {
                    bool? disjoint = ElementPairInteriorsDisjoint(
                        ms, elements[a], elements[b], out var residue);
                    if (disjoint == false)
                        return false;
                    if (residue != null)
                        deferred = deferred ?? residue;
                }
            }
            if (deferred != null)
                throw deferred;
            return true;
        }

        /// <summary>
        /// §4.2.27 for one element pair (615-h rung 5, #641): <c>false</c>
        /// when the boundaries provably share a 1-D piece, or when an exact
        /// sample on one element's boundary lies strictly inside the other
        /// (sound with no contact information at all). <c>true</c> when the
        /// contact set is complete, every contact is a vertex SHARED by both
        /// boundaries, and one exact sample strictly inside every boundary
        /// segment lies outside the other element: the contacts then cut the
        /// boundaries into whole-segment pieces, each piece lies entirely on
        /// one side of the other element, and if no boundary piece of either
        /// enters the other's interior the interiors are disjoint (a bounded
        /// open overlap would need a boundary of its own). <c>null</c> with
        /// <paramref name="residue"/> otherwise.
        /// </summary>
        private static bool? ElementPairInteriorsDisjoint(
            MultiSurface ms, (int Index, Geometry Element) a, (int Index, Geometry Element) b,
            out NotSupportedException residue)
        {
            residue = null;
            var ringsA = CollectRings(a.Element);
            var ringsB = CollectRings(b.Element);
            var contacts = new List<Coordinate>();
            var pair = new List<Coordinate>();
            foreach (var ringA in ringsA)
            {
                foreach (var ringB in ringsB)
                {
                    pair.Clear();
                    var pairResidue = CurveSimplicity.RingPairContacts(ms, ringA, ringB, pair, out bool overlap);
                    // §4.2.27: boundaries may meet in a finite number of
                    // POINTS — a shared 1-D piece is definite false.
                    if (overlap)
                        return false;
                    residue = residue ?? pairResidue;
                    foreach (var contact in pair)
                    {
                        if (!contacts.Exists(c => CurveSimplicity.Matches(contact, c)))
                            contacts.Add(contact);
                    }
                }
            }

            var exactA = ExactRings(ms, ringsA);
            var exactB = ExactRings(ms, ringsB);
            if (AnySampleInside(exactA, exactB) || AnySampleInside(exactB, exactA))
                return false;
            if (residue != null)
                return null;

            var verticesA = Vertices(ms, ringsA);
            var verticesB = Vertices(ms, ringsB);
            foreach (var contact in contacts)
            {
                if (!IsSharedVertex(contact, verticesA, verticesB))
                {
                    residue = CurvedGeometry.Refused(ms,
                        $"an interiors-disjoint decision (4.2.27) for elements {a.Index} and {b.Index}, whose " +
                        $"boundaries touch at ({contact.X}, {contact.Y}) — not a vertex shared by both, so the " +
                        "boundary pieces between contacts have no exact sample point yet (the 615-h lane, " +
                        "NetTopologySuite.Proofs issue #981)");
                    return null;
                }
            }
            if (!AllSegmentSamplesOutside(exactA, exactB) || !AllSegmentSamplesOutside(exactB, exactA))
            {
                residue = CurvedGeometry.Refused(ms,
                    $"an interiors-disjoint decision (4.2.27) for elements {a.Index} and {b.Index}: the exact " +
                    "location found a boundary contact the double-precision contact kernel did not report");
                return null;
            }
            return true;
        }

        /// <summary>Does any exact sample of <paramref name="rings"/> lie strictly inside the surface <paramref name="surface"/>?</summary>
        private static bool AnySampleInside(
            List<List<CurveContainment.ExactSegment>> rings, List<List<CurveContainment.ExactSegment>> surface)
        {
            foreach (var ring in rings)
            {
                foreach (var sample in CurveContainment.Samples(ring, false))
                {
                    if (CurveContainment.LocateInSurface(sample, surface) == CurveContainment.Location.Interior)
                        return true;
                }
            }
            return false;
        }

        /// <summary>Does the segment-interior sample of every segment of <paramref name="rings"/> lie outside <paramref name="surface"/>?</summary>
        private static bool AllSegmentSamplesOutside(
            List<List<CurveContainment.ExactSegment>> rings, List<List<CurveContainment.ExactSegment>> surface)
        {
            foreach (var ring in rings)
            {
                foreach (var sample in CurveContainment.Samples(ring, true))
                {
                    if (CurveContainment.LocateInSurface(sample, surface) != CurveContainment.Location.Exterior)
                        return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Is <paramref name="contact"/> (a kernel-computed point) a vertex
        /// present, bit-identical, in both boundaries?
        /// </summary>
        private static bool IsSharedVertex(Coordinate contact, List<Coordinate> verticesA, List<Coordinate> verticesB)
        {
            foreach (var vertex in verticesA)
            {
                if (CurveSimplicity.Matches(contact, vertex) && verticesB.Exists(v => v.Equals2D(vertex)))
                    return true;
            }
            return false;
        }

        private static List<Coordinate> Vertices(Geometry owner, List<Curve> rings)
        {
            var vertices = new List<Coordinate>();
            foreach (var ring in rings)
            {
                foreach (var segment in CurveSimplicity.BuildChain(owner, ring))
                    vertices.Add(segment.Start);
            }
            return vertices;
        }

        /// <summary>
        /// The exact form of every ring; refuses when the exact arc/chord
        /// classification of some segment disagrees with the double
        /// kernel's (whose contact verdicts the caller already holds).
        /// </summary>
        private static List<List<CurveContainment.ExactSegment>> ExactRings(Geometry owner, List<Curve> rings)
        {
            var exact = new List<List<CurveContainment.ExactSegment>>(rings.Count);
            foreach (var ring in rings)
            {
                var exactRing = CurveContainment.TryBuildRing(CurveSimplicity.BuildChain(owner, ring));
                if (exactRing == null)
                    throw CurvedGeometry.Refused(owner,
                        "a containment decision for a ring with a control triple the double kernel classifies " +
                        "differently from exact arithmetic (arc vs. collinear chord)");
                exact.Add(exactRing);
            }
            return exact;
        }

        /// <summary>
        /// The non-empty rings of a surface (CurvePolygon or classical
        /// Polygon — both ring types are <see cref="Curve"/>s on this
        /// branch).
        /// </summary>
        private static List<Curve> CollectRings(Geometry surface)
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
        /// The refusal when exact location finds a ring point ON another ring
        /// that is not the contact the double kernel reported for the pair —
        /// the two machineries disagree, so neither verdict is trusted.
        /// </summary>
        private static NotSupportedException ContactDisagreement(Geometry owner, string what)
        {
            return CurvedGeometry.Refused(owner,
                $"a containment decision for {what}: the exact location found a boundary contact the " +
                "double-precision contact kernel did not report");
        }
    }
}
