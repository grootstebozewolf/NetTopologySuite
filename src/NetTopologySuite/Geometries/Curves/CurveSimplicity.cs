// SPDX-License-Identifier: BSD-3-Clause
//
// AI assistance disclosure:
//   AI-drafted, human-reviewed and curated. Per the same convention used for the
//   companion JTS prototype at grootstebozewolf/jts#1, AI-generated portions are
//   dedicated to CC0-1.0; human curation falls under the NTS BSD-3-Clause grant.
//
// Arc-aware simplicity over the ISO/IEC 13249-3 loci (§4.2.4 over §7.3.1
// Desc 8; NetTopologySuite.Proofs #615, ticket 615-h — rung 2 #630 for
// CircularString, rung 3 #634 for CompoundCurve and CurvePolygon rings).
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
// Fail-closed residues, named in the throws: a degenerate closed arc segment
// (start == end — Desc-6-invalid, its locus is not a decidable arc), the
// nearly-cocircular ambiguity band, and the large-circumradius conditioning
// guard (see CircularArcGeometry.SegmentPairContacts). MultiCurve /
// MultiSurface simplicity are the next rung (issue #639).

using System.Collections.Generic;

namespace NetTopologySuite.Geometries.Curves
{
    /// <summary>
    /// Simplicity verdicts for <see cref="CircularString"/>,
    /// <see cref="CompoundCurve"/> and <see cref="CurvePolygon"/> rings over
    /// the arc/chord loci; see
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
            foreach (var component in cc.Curves)
            {
                switch (component)
                {
                    case CircularString cs:
                        AppendCircularString(cc, cs.CoordinateSequence, chain);
                        break;
                    case LineString ls:
                        AppendLineString(ls.CoordinateSequence, chain);
                        break;
                    default:
                        // The constructor admits only LineString and
                        // CircularString components (nested compounds are
                        // spliced flat) — anything else is fail-closed.
                        throw CurvedGeometry.NotYetSupported(cc,
                            $"IsSimple with a component of type {component.GeometryType}");
                }
            }
            return IsSimpleChain(cc, chain, cc.IsClosed);
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

        private static void AppendCircularString(Geometry owner, CoordinateSequence seq, List<ChainSegment> chain)
        {
            int segCount = (seq.Count - 1) / 2;
            for (int s = 0; s < segCount; s++)
            {
                var start = seq.GetCoordinate(2 * s);
                var end = seq.GetCoordinate(2 * s + 2);
                if (start.Equals2D(end))
                    throw CurvedGeometry.NotYetSupported(owner, segCount == 1 && owner is CircularString
                        ? "IsSimple for a degenerate closed single segment (start == end, invalid under 7.3.1 Desc 6)"
                        : $"IsSimple with a degenerate closed arc segment (start == end, invalid under 7.3.1 Desc 6)");
                chain.Add(new ChainSegment(start, seq.GetCoordinate(2 * s + 1), end));
            }
        }

        private static void AppendLineString(CoordinateSequence seq, List<ChainSegment> chain)
        {
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
                throw CurvedGeometry.NotYetSupported(owner,
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
            for (int i = 0; i < chain.Count; i++)
            {
                for (int j = i + 1; j < chain.Count; j++)
                {
                    contacts.Clear();
                    var result = CircularArcGeometry.SegmentPairContacts(
                        chain[i].Start, chain[i].Mid, chain[i].End,
                        chain[j].Start, chain[j].Mid, chain[j].End,
                        contacts, out bool overlap);
                    if (result == CircularArcGeometry.SegmentPairResult.AmbiguousCocircular)
                        throw CurvedGeometry.NotYetSupported(owner,
                            $"IsSimple for nearly cocircular segments {i} and {j} — their circumcircles are too close " +
                            "to distinguish from one circle at double precision, but not exactly equal; refusing to " +
                            "guess between interval overlap and radical-line intersection (the 615-h lane, " +
                            "NetTopologySuite.Proofs issue #639)");
                    if (result == CircularArcGeometry.SegmentPairResult.IllConditioned)
                        throw CurvedGeometry.NotYetSupported(owner,
                            $"IsSimple for segments {i} and {j} — a circumradius is too large relative to the " +
                            "coordinate scale for a double-precision contact decision (the r² error would swamp " +
                            "the match tolerance); exact arithmetic is the way to widen this (the 615-h lane, " +
                            "NetTopologySuite.Proofs issue #639)");
                    if (overlap)
                        return false;
                    foreach (var contact in contacts)
                    {
                        if (!IsPermitted(contact, chain, i, j, closed))
                            return false;
                    }
                }
            }
            return true;
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
