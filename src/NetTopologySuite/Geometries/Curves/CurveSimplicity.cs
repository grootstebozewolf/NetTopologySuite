// SPDX-License-Identifier: BSD-3-Clause
//
// AI assistance disclosure:
//   AI-drafted, human-reviewed and curated. Per the same convention used for the
//   companion JTS prototype at grootstebozewolf/jts#1, AI-generated portions are
//   dedicated to CC0-1.0; human curation falls under the NTS BSD-3-Clause grant.
//
// Arc-aware simplicity over the ISO/IEC 13249-3 loci (§4.2.4 over §7.3.1
// Desc 8; NetTopologySuite.Proofs #615, ticket 615-h — rung 2 #630 for
// CircularString, rung 3 #634 for CompoundCurve and CurvePolygon rings,
// rung 4 #639 for MultiCurve and MultiSurface).
// A chain of segments is simple iff no two segment loci meet outside the
// permitted shared vertices: consecutive segments may share exactly their
// connecting vertex, and a closed chain may additionally share its start/end
// point between the first and last segments. A CompoundCurve's chain
// concatenates its components' segments — each multi-point LineString
// component contributes one Desc-8b-style chord per consecutive coordinate
// pair (zero-length pairs are skipped: no locus, and contiguity survives the
// skip). This mirrors the composition of the Proofs oracle's RING_SIMPLE
// lane (proof companion theories/CurveRingSimple.v: a detected non-adjacent
// contact refutes curve_ring_simple), with one deliberate difference: an
// OPEN chain permits no first/last-segment contact — the oracle lane always
// models a ring.
//
// A MultiCurve is simple iff every member is simple AND any two members
// meet only at points in the boundaries of BOTH members (§4.2.25 / §10.3.1
// Desc 4; Mod-2 boundary: the endpoints of an open member, nothing for a
// closed one — so any touch on a closed member refutes simplicity). A
// MultiSurface follows the polygonal reading: every element's rings are
// simple (§4.2.27 makes MultiSurface simplicity definitional; the reading
// is pinned in the research doc §2).
//
// Fail-closed residues, named in the throws: a degenerate closed arc segment
// (start == end — Desc-6-invalid, its locus is not a decidable arc), a
// non-finite control coordinate (no locus at all; IsValid is definite-false
// there), the nearly-cocircular ambiguity band, and the large-circumradius
// conditioning guard (see CircularArcGeometry.SegmentPairContacts) — the
// exactness upgrades are the 615-h continuation, issue #641.

using System;
using System.Collections.Generic;

namespace NetTopologySuite.Geometries.Curves
{
    /// <summary>
    /// Simplicity verdicts for <see cref="CircularString"/>,
    /// <see cref="CompoundCurve"/>, <see cref="CurvePolygon"/> rings,
    /// <see cref="MultiCurve"/> and <see cref="MultiSurface"/> over the
    /// arc/chord loci; see
    /// <see cref="CircularArcGeometry.SegmentPairContacts"/> for the pair
    /// kernel.
    /// </summary>
    internal static class CurveSimplicity
    {
        /// <summary>
        /// One chain segment. An arc keeps its real control triple; a chord
        /// from a LineString component is encoded as (a, a, b) — the repeated
        /// first point makes the orientation cross exactly zero, so the pair
        /// kernel takes the Desc-8b chord path with no floating-point doubt.
        /// </summary>
        private readonly struct ChainSegment
        {
            public readonly Coordinate Start;
            public readonly Coordinate Mid;
            public readonly Coordinate End;

            public ChainSegment(Coordinate start, Coordinate mid, Coordinate end)
            {
                Start = start;
                Mid = mid;
                End = end;
            }
        }

        /// <summary>
        /// The simplicity verdict for a non-empty <see cref="CircularString"/>:
        /// checked <c>true</c>/<c>false</c> for every value whose segments
        /// are non-degenerate arcs or Desc-8b chords, a fail-closed throw for
        /// the kernel's named residues. Never an unchecked <c>true</c>.
        /// </summary>
        public static bool IsSimple(CircularString cs)
        {
            var chain = new List<ChainSegment>();
            AppendCircularString(cs, cs.CoordinateSequence, chain);
            return IsSimpleChain(cs, chain, cs.IsClosed);
        }

        /// <summary>
        /// The simplicity verdict for a non-empty <see cref="CompoundCurve"/>:
        /// the same pairwise composition over the concatenated component
        /// chain (615-h rung 3, #634).
        /// </summary>
        public static bool IsSimple(CompoundCurve cc)
        {
            var chain = new List<ChainSegment>();
            AppendCurve(cc, cc, chain);
            return IsSimpleChain(cc, chain, cc.IsClosed);
        }

        /// <summary>
        /// The simplicity verdict for a non-empty <see cref="MultiCurve"/>
        /// (615-h rung 4, #639): §4.2.25 / §10.3.1 Desc 4 — simple iff every
        /// member is simple AND any two members intersect only at points in
        /// the boundaries of BOTH members. Under the Mod-2 boundary rule an
        /// open member's boundary is its two endpoints and a closed member's
        /// boundary is empty, so any contact on a closed member refutes
        /// simplicity. Empty members contribute nothing.
        /// </summary>
        public static bool IsSimple(MultiCurve mc)
        {
            var chains = new List<List<ChainSegment>>();
            var closed = new List<bool>();
            for (int m = 0; m < mc.NumGeometries; m++)
            {
                var member = (Curve)mc.GetGeometryN(m);
                if (member.IsEmpty)
                    continue;
                var chain = new List<ChainSegment>();
                // The member is the owner here: chain-build and
                // member-simplicity residues are member-local (the review
                // flagged the mixed naming).
                AppendCurve(member, member, chain);
                if (!IsSimpleChain(member, chain, member.IsClosed))
                    return false;
                chains.Add(chain);
                closed.Add(member.IsClosed);
            }

            var contacts = new List<Coordinate>();
            NotSupportedException deferred = null;
            for (int a = 0; a < chains.Count; a++)
            {
                for (int b = a + 1; b < chains.Count; b++)
                {
                    var chainA = chains[a];
                    var chainB = chains[b];
                    for (int i = 0; i < chainA.Count; i++)
                    {
                        for (int j = 0; j < chainB.Count; j++)
                        {
                            contacts.Clear();
                            var residue = PairContacts(mc, chainA[i], chainB[j],
                                $"members {a} and {b} (segments {i} and {j})",
                                contacts, out bool overlap);
                            if (residue != null)
                            {
                                deferred = deferred ?? residue;
                                continue;
                            }
                            if (overlap)
                                return false;
                            foreach (var contact in contacts)
                            {
                                // Permitted only at a point in the boundary
                                // of BOTH members; a closed member has none.
                                if (closed[a] || closed[b])
                                    return false;
                                if (!MatchesEndpoint(contact, chainA) ||
                                    !MatchesEndpoint(contact, chainB))
                                    return false;
                            }
                        }
                    }
                }
            }
            if (deferred != null)
                throw deferred;
            return true;
        }

        /// <summary>
        /// The simplicity verdict for a non-empty <see cref="MultiSurface"/>
        /// (615-h rung 4, #639): the polygonal reading — every element's
        /// rings are simple (§4.2.27 makes MultiSurface simplicity
        /// definitional; the classical machinery reads it as ring
        /// simplicity, and the arc-aware verdict follows that reading —
        /// pinned in the research doc §2).
        /// </summary>
        public static bool IsSimple(MultiSurface ms)
        {
            for (int e = 0; e < ms.NumGeometries; e++)
            {
                switch (ms.GetGeometryN(e))
                {
                    case CurvePolygon cp:
                        for (int i = -1; i < cp.NumInteriorRings; i++)
                        {
                            var ring = i < 0 ? cp.ExteriorRing : cp.GetInteriorRingN(i);
                            if (ring != null && !RingIsSimple(ring))
                                return false;
                        }
                        break;
                    case Polygon p:
                        if (!p.IsSimple) // classical IsSimpleOp: ring simplicity
                            return false;
                        break;
                    case Geometry other when other.IsEmpty:
                        break;
                    case Geometry other:
                        // The constructor admits only Polygon and CurvePolygon.
                        throw CurvedGeometry.NotYetSupported(ms,
                            $"IsSimple with an element of type {other.GeometryType}");
                }
            }
            return true;
        }

        /// <summary>
        /// Ring simplicity for a <see cref="CurvePolygon"/> ring (§8.2.1
        /// Desc 2–3's "simple" half): CircularString and CompoundCurve rings
        /// go through the chain kernel, LineString rings through the
        /// classical machinery. An empty ring is trivially simple.
        /// </summary>
        public static bool RingIsSimple(Curve ring)
        {
            if (ring.IsEmpty)
                return true;
            switch (ring)
            {
                case CircularString cs: return IsSimple(cs);
                case CompoundCurve cc: return IsSimple(cc);
                default: return ring.IsSimple; // LineString: classical IsSimpleOp
            }
        }

        /// <summary>
        /// All distinct contact points between two rings' loci (615-h rung 4,
        /// #639 — the §8.2.1 Desc 11 count and the §4.2.27 boundary
        /// condition both consume this): <paramref name="overlap"/> reports a
        /// shared 1-D piece (decided without enumerating points); otherwise
        /// <paramref name="contacts"/> receives the tolerance-deduplicated
        /// contact points from the DECIDED pairs — a tangency reported by
        /// several adjacent segment pairs counts once. A kernel residue is
        /// RETURNED, not thrown: the decided contacts stay sound (ambiguity
        /// can only add contacts), so the caller may still refute on them —
        /// but it must fail closed on the residue before certifying any
        /// verdict that needs the COMPLETE contact set.
        /// </summary>
        internal static NotSupportedException RingPairContacts(
            Geometry owner, Curve ringA, Curve ringB,
            List<Coordinate> contacts, out bool overlap)
        {
            var chainA = new List<ChainSegment>();
            var chainB = new List<ChainSegment>();
            AppendCurve(owner, ringA, chainA);
            AppendCurve(owner, ringB, chainB);
            overlap = false;
            NotSupportedException deferred = null;
            var pair = new List<Coordinate>();
            for (int i = 0; i < chainA.Count; i++)
            {
                for (int j = 0; j < chainB.Count; j++)
                {
                    pair.Clear();
                    var residue = PairContacts(owner, chainA[i], chainB[j],
                        $"ring-pair segments {i} and {j}", pair, out bool segOverlap);
                    if (residue != null)
                    {
                        deferred = deferred ?? residue;
                        continue;
                    }
                    if (segOverlap)
                    {
                        overlap = true;
                        return null;
                    }
                    foreach (var contact in pair)
                    {
                        if (!ContainsMatch(contacts, contact))
                            contacts.Add(contact);
                    }
                }
            }
            return deferred;
        }

        /// <summary>
        /// Appends <paramref name="curve"/>'s segments to
        /// <paramref name="chain"/>: arcs keep their control triples,
        /// LineStrings contribute Desc-8b chords, compounds concatenate their
        /// components. Fail-closed on anything else.
        /// </summary>
        private static void AppendCurve(Geometry owner, Curve curve, List<ChainSegment> chain)
        {
            switch (curve)
            {
                case CircularString cs:
                    AppendCircularString(owner, cs.CoordinateSequence, chain);
                    break;
                case CompoundCurve cc:
                    // Intake splices nested compounds flat, so this recursion
                    // is one level deep for constructed values; a
                    // serialization-bypass nest still terminates.
                    foreach (var component in cc.Curves)
                        AppendCurve(owner, component, chain);
                    break;
                case LineString ls:
                    AppendLineString(owner, ls.CoordinateSequence, chain);
                    break;
                default:
                    throw CurvedGeometry.NotYetSupported(owner,
                        $"a segment chain for a component of type {curve.GeometryType}");
            }
        }

        private static void AppendCircularString(Geometry owner, CoordinateSequence seq, List<ChainSegment> chain)
        {
            RequireFinite(owner, seq);
            int segCount = (seq.Count - 1) / 2;
            for (int s = 0; s < segCount; s++)
            {
                var start = seq.GetCoordinate(2 * s);
                var end = seq.GetCoordinate(2 * s + 2);
                if (start.Equals2D(end))
                    throw CurvedGeometry.Refused(owner, segCount == 1 && owner is CircularString
                        ? "IsSimple for a degenerate closed single segment (start == end, invalid under 7.3.1 Desc 6)"
                        : "IsSimple with a degenerate closed arc segment (start == end, invalid under 7.3.1 Desc 6)");
                chain.Add(new ChainSegment(start, seq.GetCoordinate(2 * s + 1), end));
            }
        }

        private static void AppendLineString(Geometry owner, CoordinateSequence seq, List<ChainSegment> chain)
        {
            RequireFinite(owner, seq);
            for (int i = 0; i + 1 < seq.Count; i++)
            {
                var a = seq.GetCoordinate(i);
                var b = seq.GetCoordinate(i + 1);
                if (a.Equals2D(b))
                    continue; // repeated point: zero-length chord, no locus
                chain.Add(new ChainSegment(a, a, b));
            }
        }

        private static bool IsSimpleChain(Geometry owner, List<ChainSegment> chain, bool closed)
        {
            if (chain.Count == 0)
                throw CurvedGeometry.Refused(owner,
                    "IsSimple for a value whose locus degenerates to a single point (every sub-segment is zero-length)");
            if (chain.Count == 1)
            {
                // A single segment with distinct endpoints is simple —
                // non-collinear controls give an arc with sweep in (0, 2π),
                // injective in the angle (Desc 8a); collinear controls or a
                // chord give a straight segment (Desc 8b).
                return true;
            }

            var contacts = new List<Coordinate>();
            NotSupportedException deferred = null;
            for (int i = 0; i < chain.Count; i++)
            {
                for (int j = i + 1; j < chain.Count; j++)
                {
                    contacts.Clear();
                    var residue = PairContacts(owner, chain[i], chain[j],
                        $"segments {i} and {j}", contacts, out bool overlap);
                    if (residue != null)
                    {
                        // Deferred, not thrown: a definite refutation from a
                        // later pair is sound regardless of this one.
                        deferred = deferred ?? residue;
                        continue;
                    }
                    if (overlap)
                        return false;
                    foreach (var contact in contacts)
                    {
                        if (!IsPermitted(contact, chain, i, j, closed))
                            return false;
                    }
                }
            }
            if (deferred != null)
                throw deferred;
            return true;
        }

        /// <summary>
        /// The pair kernel with the fail-closed residues turned into named
        /// exceptions, returned rather than thrown so the caller can DEFER
        /// them: a definite refutation found elsewhere in the same scan is
        /// sound regardless of an ambiguous pair (ambiguity can only add
        /// contacts, never remove a witnessed one), so <c>false</c> beats a
        /// residue — only a would-be <c>true</c> (or an uncertain count)
        /// must fail closed on it. <paramref name="what"/> says which pair.
        /// </summary>
        private static NotSupportedException PairContacts(
            Geometry owner, ChainSegment a, ChainSegment b, string what,
            List<Coordinate> contacts, out bool overlap)
        {
            var result = CircularArcGeometry.SegmentPairContacts(
                a.Start, a.Mid, a.End, b.Start, b.Mid, b.End,
                contacts, out overlap);
            switch (result)
            {
                case CircularArcGeometry.SegmentPairResult.AmbiguousCocircular:
                    return CurvedGeometry.Refused(owner,
                        $"a contact decision for nearly cocircular {what} — their circumcircles are too close " +
                        "to distinguish from one circle at double precision, but not exactly equal; refusing to " +
                        "guess between interval overlap and radical-line intersection (the 615-h lane, " +
                        "NetTopologySuite.Proofs issue #641)");
                case CircularArcGeometry.SegmentPairResult.IllConditioned:
                    return CurvedGeometry.Refused(owner,
                        $"a contact decision for {what} — a circumradius is too large relative to the " +
                        "coordinate scale for a double-precision contact decision (the r² error would swamp " +
                        "the match tolerance); exact arithmetic is the way to widen this (the 615-h lane, " +
                        "NetTopologySuite.Proofs issue #641)");
                case CircularArcGeometry.SegmentPairResult.AmbiguousTangency:
                    return CurvedGeometry.Refused(owner,
                        $"a contact decision for nearly tangent {what} — the intersection discriminant is " +
                        "within its own double-precision error band, so one touch point cannot be told from " +
                        "a close crossing pair or a near-miss (rung-4 review-demonstrated); exact arithmetic " +
                        "is the way to widen this (the 615-h lane, NetTopologySuite.Proofs issue #641)");
                default:
                    return null;
            }
        }

        /// <summary>
        /// Non-finite coordinates carry no locus: every downstream contact
        /// decision would be an unchecked guess (NaN even slips past the
        /// conditioning guard, whose comparison is false for NaN), so the
        /// chain build fail-closes here. IsValid is definite-false for the
        /// same value — this guard only keeps IsSimple honest.
        /// </summary>
        private static void RequireFinite(Geometry owner, CoordinateSequence seq)
        {
            for (int i = 0; i < seq.Count; i++)
            {
                double x = seq.GetX(i), y = seq.GetY(i);
                if (double.IsNaN(x) || double.IsInfinity(x) || double.IsNaN(y) || double.IsInfinity(y))
                    throw CurvedGeometry.Refused(owner,
                        $"a simplicity decision with a non-finite control coordinate (index {i}); " +
                        "IsValid is definite-false for this value");
            }
        }

        /// <summary>Does <paramref name="contact"/> match either endpoint of the chain?</summary>
        private static bool MatchesEndpoint(Coordinate contact, List<ChainSegment> chain)
        {
            return Matches(contact, chain[0].Start)
                || Matches(contact, chain[chain.Count - 1].End);
        }

        private static bool ContainsMatch(List<Coordinate> contacts, Coordinate candidate)
        {
            foreach (var existing in contacts)
            {
                if (Matches(candidate, existing))
                    return true;
            }
            return false;
        }

        private static bool IsPermitted(
            Coordinate contact, List<ChainSegment> chain, int i, int j, bool closed)
        {
            // Consecutive segments share exactly their connecting vertex.
            if (j == i + 1 && Matches(contact, chain[j].Start))
                return true;
            // A closed chain additionally shares its start/end point between
            // the first and last segments (for a two-segment ring both rules
            // apply to the same pair).
            if (closed && i == 0 && j == chain.Count - 1 && Matches(contact, chain[0].Start))
                return true;
            return false;
        }

        /// <summary>
        /// Contact-to-vertex matching with the oracle lane's relative
        /// tolerance: computed contact points carry float error from the
        /// radical-line / quadratic step, while permitted vertices are exact
        /// input coordinates.
        /// </summary>
        private static bool Matches(Coordinate contact, Coordinate vertex)
        {
            double scale = 1 + System.Math.Sqrt(vertex.X * vertex.X + vertex.Y * vertex.Y);
            return contact.Distance(vertex) <= CircularArcGeometry.RelativeTolerance * scale;
        }
    }
}
