// SPDX-License-Identifier: BSD-3-Clause
//
// AI assistance disclosure:
//   AI-drafted, human-reviewed and curated. Per the same convention used for the
//   companion JTS prototype at grootstebozewolf/jts#1, AI-generated portions are
//   dedicated to CC0-1.0; human curation falls under the NTS BSD-3-Clause grant.
//
// Arc-aware point-in-ring over the ISO/IEC 13249-3 loci (§7.3.1 Desc 8),
// exact (NetTopologySuite.Proofs #615, ticket 615-h rung 5, issue #641 —
// the containment machinery behind CurvePolygon §8.2.1 Desc 12–14 and
// MultiSurface §4.2.27 interiors-disjoint).
//
// The winding number of a closed chain of arcs and chords about a point X
// that is not on it splits into two exact parts:
//
//   W(X) = W_poly(X) + Σ_arcs s_i · [X in the circular segment of arc i]
//
// where W_poly is the winding of the polygon that replaces every arc by its
// start–end chord, s_i is +1 for a counter-clockwise arc and −1 for a
// clockwise one, and the circular segment is the open region between the
// arc and its chord (open disc ∩ the chord side the arc lies on). This holds
// because the loop "arc, then chord back" winds s_i around exactly the
// points of that region. When X lies ON an arc's chord the arc is split at
// its intermediate control point instead (two sub-arcs, two sub-chords that
// cannot pass through X). Every term is an exact orientation or in-circle
// sign on rational points (ExactArcArithmetic), W_poly is Sunday's crossing
// rule with exact orientations — no rounding anywhere on this path.
//
// Sample points on a ring are exact too: segment start vertices, an arc's
// intermediate control point (on the locus and strictly inside the arc),
// and a chord's exact rational midpoint.

using System.Collections.Generic;

namespace NetTopologySuite.Geometries.Curves
{
    /// <summary>
    /// Exact point location against rings and surfaces whose rings are
    /// chains of arcs and chords (615-h rung 5, NetTopologySuite.Proofs
    /// issue #641).
    /// </summary>
    internal static class CurveContainment
    {
        /// <summary>Where a point lies relative to a ring or surface.</summary>
        internal enum Location
        {
            Exterior,
            Boundary,
            Interior,
        }

        /// <summary>One ring segment in exact form.</summary>
        internal readonly struct ExactSegment
        {
            public readonly ExactPoint P0;
            public readonly ExactPoint P1;
            public readonly ExactPoint P2;
            public readonly bool IsArc;
            public readonly ExactArc Arc;

            public ExactSegment(ExactPoint p0, ExactPoint p2)
            {
                P0 = p0;
                P1 = p0;
                P2 = p2;
                IsArc = false;
                Arc = default;
            }

            public ExactSegment(ExactArc arc)
            {
                P0 = arc.P0;
                P1 = arc.P1;
                P2 = arc.P2;
                IsArc = true;
                Arc = arc;
            }
        }

        /// <summary>
        /// The exact form of a ring's segment chain, with arc/chord
        /// classification taken from the double kernel and CHECKED exactly:
        /// <c>null</c> when the two disagree for some segment (an
        /// ultra-flat triple the double kernel calls an arc while it is
        /// exactly collinear, or vice versa) — the caller must refuse, since
        /// the simplicity/contact verdicts it already holds were computed
        /// under the double classification.
        /// </summary>
        public static List<ExactSegment> TryBuildRing(IReadOnlyList<CurveSimplicity.ChainSegment> chain)
        {
            var ring = new List<ExactSegment>(chain.Count);
            foreach (var segment in chain)
            {
                if (CircularArcGeometry.OrientationIndex(segment.Start, segment.Mid, segment.End) == 0d)
                {
                    var p0 = ExactPoint.From(segment.Start);
                    var p1 = ExactPoint.From(segment.Mid);
                    var p2 = ExactPoint.From(segment.End);
                    if (ExactArcArithmetic.Orientation(p0, p1, p2) != 0)
                        return null;
                    ring.Add(new ExactSegment(p0, p2));
                }
                else
                {
                    if (!ExactArcArithmetic.TryArc(segment.Start, segment.Mid, segment.End, out var arc))
                        return null;
                    ring.Add(new ExactSegment(arc));
                }
            }
            return ring;
        }

        /// <summary>
        /// Exact sample points on a ring's locus: with
        /// <paramref name="segmentInteriorsOnly"/> one point strictly inside
        /// every segment (an arc's intermediate control point, a chord's
        /// midpoint); otherwise also every segment's start vertex.
        /// </summary>
        public static IEnumerable<ExactPoint> Samples(List<ExactSegment> ring, bool segmentInteriorsOnly)
        {
            foreach (var segment in ring)
            {
                if (!segmentInteriorsOnly)
                    yield return segment.P0;
                yield return segment.IsArc ? segment.P1 : ExactPoint.Midpoint(segment.P0, segment.P2);
            }
        }

        /// <summary>The exact location of <paramref name="x"/> relative to a closed simple ring.</summary>
        public static Location LocateInRing(ExactPoint x, List<ExactSegment> ring)
        {
            foreach (var segment in ring)
            {
                if (segment.IsArc)
                {
                    if (ExactArcArithmetic.InCircle(segment.P0, segment.P1, segment.P2, x) == 0
                        && segment.Arc.ContainsCirclePoint(x))
                        return Location.Boundary;
                }
                else if (ExactArcArithmetic.OnSegment(segment.P0, segment.P2, x))
                {
                    return Location.Boundary;
                }
            }

            int winding = 0;
            foreach (var segment in ring)
            {
                if (!segment.IsArc)
                {
                    winding += Crossing(segment.P0, segment.P2, x);
                    continue;
                }
                var arc = segment.Arc;
                bool inDisc = ExactArcArithmetic.InCircle(arc.P0, arc.P1, arc.P2, x) > 0;
                if (!ExactArcArithmetic.OnSegment(arc.P0, arc.P2, x))
                {
                    winding += Crossing(arc.P0, arc.P2, x);
                    if (inDisc && ExactArcArithmetic.Orientation(arc.P0, arc.P2, x) == arc.Side)
                        winding += arc.Direction;
                    continue;
                }
                // X on the chord: split at the intermediate point. Each
                // sub-arc lies on the side of its sub-chord opposite the
                // third control point.
                winding += Crossing(arc.P0, arc.P1, x) + Crossing(arc.P1, arc.P2, x);
                if (inDisc)
                {
                    int s01 = ExactArcArithmetic.Orientation(arc.P0, arc.P1, x);
                    if (s01 != 0 && s01 == -ExactArcArithmetic.Orientation(arc.P0, arc.P1, arc.P2))
                        winding += arc.Direction;
                    int s12 = ExactArcArithmetic.Orientation(arc.P1, arc.P2, x);
                    if (s12 != 0 && s12 == -ExactArcArithmetic.Orientation(arc.P1, arc.P2, arc.P0))
                        winding += arc.Direction;
                }
            }
            return winding != 0 ? Location.Interior : Location.Exterior;
        }

        /// <summary>
        /// The exact location of <paramref name="x"/> relative to a surface
        /// given as its exterior ring and holes: inside the shell and outside
        /// the closure of every hole is the interior.
        /// </summary>
        public static Location LocateInSurface(ExactPoint x, List<List<ExactSegment>> rings)
        {
            var location = LocateInRing(x, rings[0]);
            if (location != Location.Interior)
                return location;
            for (int i = 1; i < rings.Count; i++)
            {
                switch (LocateInRing(x, rings[i]))
                {
                    case Location.Boundary:
                        return Location.Boundary;
                    case Location.Interior:
                        return Location.Exterior;
                }
            }
            return Location.Interior;
        }

        /// <summary>
        /// Sunday's signed crossing of the rightward ray from
        /// <paramref name="x"/> by the directed edge a → b (half-open in y,
        /// exact orientation; the callers never pass an edge through X).
        /// </summary>
        private static int Crossing(ExactPoint a, ExactPoint b, ExactPoint x)
        {
            if ((a.Y - x.Y).Sign <= 0)
            {
                if ((b.Y - x.Y).Sign > 0 && ExactArcArithmetic.Orientation(a, b, x) > 0)
                    return 1;
            }
            else if ((b.Y - x.Y).Sign <= 0 && ExactArcArithmetic.Orientation(a, b, x) < 0)
            {
                return -1;
            }
            return 0;
        }
    }
}
