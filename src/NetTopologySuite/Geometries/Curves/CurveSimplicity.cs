// SPDX-License-Identifier: BSD-3-Clause
//
// AI assistance disclosure:
//   AI-drafted, human-reviewed and curated. Per the same convention used for the
//   companion JTS prototype at grootstebozewolf/jts#1, AI-generated portions are
//   dedicated to CC0-1.0; human curation falls under the NTS BSD-3-Clause grant.
//
// Arc-aware simplicity for CircularString (ISO/IEC 13249-3 §4.2.4 over the
// §7.3.1 Desc 8 locus; NetTopologySuite.Proofs #615, ticket 615-h rung 2,
// issue #630 there). A chain of segments is simple iff no two segment loci
// meet outside the permitted shared vertices: consecutive segments may share
// exactly their connecting vertex, and a closed chain may additionally share
// its start/end point between the first and last segments. This mirrors the
// composition of the Proofs oracle's RING_SIMPLE lane (proof companion
// theories/CurveRingSimple.v: a detected non-adjacent contact refutes
// curve_ring_simple), with one deliberate difference: an OPEN chain permits
// no first/last-segment contact — the oracle lane always models a ring.
//
// Fail-closed residues, named in the throws: a degenerate closed segment
// (start == end — Desc-6-invalid, its locus is not a decidable arc) and the
// nearly-cocircular ambiguity band (see CircularArcGeometry.SegmentPairContacts).
// CompoundCurve / CurvePolygon simplicity are the next rung (issue #634).

using System.Collections.Generic;

namespace NetTopologySuite.Geometries.Curves
{
    /// <summary>
    /// Simplicity verdicts for <see cref="CircularString"/> over the arc
    /// locus; see <see cref="CircularArcGeometry.SegmentPairContacts"/> for
    /// the pair kernel.
    /// </summary>
    internal static class CurveSimplicity
    {
        /// <summary>
        /// The simplicity verdict for a non-empty <see cref="CircularString"/>:
        /// checked <c>true</c>/<c>false</c> for every value whose segments
        /// are non-degenerate arcs or Desc-8b chords, a fail-closed throw for
        /// the degenerate and nearly-cocircular residues. Never an unchecked
        /// <c>true</c>.
        /// </summary>
        public static bool IsSimple(CircularString cs)
        {
            var seq = cs.CoordinateSequence;
            int segCount = (seq.Count - 1) / 2;

            for (int s = 0; s < segCount; s++)
            {
                if (seq.GetCoordinate(2 * s).Equals2D(seq.GetCoordinate(2 * s + 2)))
                    throw CurvedGeometry.NotYetSupported(cs, segCount == 1
                        ? "IsSimple for a degenerate closed single segment (start == end, invalid under 7.3.1 Desc 6)"
                        : $"IsSimple with a degenerate closed segment (segment {s} has start == end, invalid under 7.3.1 Desc 6)");
            }

            if (segCount == 1)
            {
                // Rung 1: a single segment with distinct endpoints is simple —
                // non-collinear controls give an arc with sweep in (0, 2π),
                // injective in the angle (Desc 8a); collinear controls give
                // the start–end chord (Desc 8b).
                return true;
            }

            bool closed = seq.GetCoordinate(0).Equals2D(seq.GetCoordinate(seq.Count - 1));
            var contacts = new List<Coordinate>();
            for (int i = 0; i < segCount; i++)
            {
                for (int j = i + 1; j < segCount; j++)
                {
                    contacts.Clear();
                    var result = CircularArcGeometry.SegmentPairContacts(
                        seq.GetCoordinate(2 * i), seq.GetCoordinate(2 * i + 1), seq.GetCoordinate(2 * i + 2),
                        seq.GetCoordinate(2 * j), seq.GetCoordinate(2 * j + 1), seq.GetCoordinate(2 * j + 2),
                        contacts, out bool overlap);
                    if (result == CircularArcGeometry.SegmentPairResult.AmbiguousCocircular)
                        throw CurvedGeometry.NotYetSupported(cs,
                            $"IsSimple for nearly cocircular segments {i} and {j} — their circumcircles are too close " +
                            "to distinguish from one circle at double precision, but not exactly equal; refusing to " +
                            "guess between interval overlap and radical-line intersection (the 615-h lane, " +
                            "NetTopologySuite.Proofs issue #634)");
                    if (overlap)
                        return false;
                    foreach (var contact in contacts)
                    {
                        if (!IsPermitted(contact, seq, segCount, i, j, closed))
                            return false;
                    }
                }
            }
            return true;
        }

        private static bool IsPermitted(
            Coordinate contact, CoordinateSequence seq, int segCount, int i, int j, bool closed)
        {
            // Consecutive segments share exactly their connecting vertex.
            if (j == i + 1 && Matches(contact, seq.GetCoordinate(2 * j)))
                return true;
            // A closed chain additionally shares its start/end point between
            // the first and last segments (for a two-segment ring both rules
            // apply to the same pair).
            if (closed && i == 0 && j == segCount - 1 && Matches(contact, seq.GetCoordinate(0)))
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
