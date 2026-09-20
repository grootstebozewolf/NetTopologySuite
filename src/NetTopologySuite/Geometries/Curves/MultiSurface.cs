// SPDX-License-Identifier: BSD-3-Clause
// Status: PRODUCTION (structure + Year-1 WKT + WKB type 12 + §4.2.27 typed members).
// Year-1 WKB 12 is complete (Tickets 7–9; no longer partial). The six ISO
// §5.1.67 curve names NTS has no carrier for (CIRCLE, GEODESICSTRING,
// ELLIPTICALCURVE, NURBSCURVE, CLOTHOID, SPIRALCURVE) remain omitted, as do
// Year-2 surface types (COMPOUNDSURFACE, POLYHEDRALSURFACE, TRIANGLE, TIN).
// Members are ISurface (Polygon | CurvePolygon), never collapsed to a bare
// Geometry or LinearRing (F-MS / §4.2.27). CurvePolygon rings stay Curve
// (LS|CS|CC), not LinearRing (F-CP / Ticket 3).
// IsSimple is arc-aware (ISO/IEC 13249-3 §4.2.27's definitional reading:
// every element's rings simple) and IsValid is arc-aware partial (element
// propagation, per-element ring conditions, cross-element boundary overlap;
// the interiors-disjoint condition fail-closes naming issue #641) —
// NetTopologySuite.Proofs #615 ticket 615-h rung 4, #639. The remaining
// metrics and analytic ops (Length, Area, Envelope, Distance, Centroid,
// InteriorPoint) fail closed with NotSupportedException until arc-aware
// implementations land; Linearize() is the explicit chord escape hatch.
// Assisted-by: xAI Grok; Ticket 9 typed-members: Cursor Grok 4.6

using System;
using System.Collections.Generic;

namespace NetTopologySuite.Geometries.Curves
{
    /// <summary>
    /// A SQL/MM <c>MultiSurface</c>: a collection of <see cref="ISurface"/>s
    /// (<see cref="Polygon"/>, <see cref="CurvePolygon"/>).
    /// Matches GEOS <c>geom::MultiSurface</c> / ISO WKB type 12.
    /// </summary>
    /// <remarks>
    /// <see cref="GetGeometryN"/> and enumeration expose <see cref="ISurface"/>
    /// and never collapse a <see cref="CurvePolygon"/> to a <see cref="Polygon"/>
    /// or a ring to a <see cref="LinearRing"/> (the F-MS structural contract;
    /// ISO/IEC 13249-3 §4.2.27 <c>ST_GeometryN</c> / <c>ST_NumGeometries</c>).
    /// There is no non-generic <c>Surface</c> class — <see cref="Surface{T}"/>
    /// is parameterized — so <see cref="ISurface"/> is the typed member.
    /// Year-1 WKT and WKB type 12 are complete. Year-2 surface types
    /// (<c>COMPOUNDSURFACE</c>, <c>POLYHEDRALSURFACE</c>, <c>TRIANGLE</c>,
    /// <c>TIN</c>) remain omitted.
    /// <para/>
    /// The remaining analytic ops fail closed with
    /// <see cref="NotSupportedException"/> until arc-aware implementations land.
    /// </remarks>
    [Serializable]
    public class MultiSurface : GeometryCollection, IPolygonal
    {
        /// <summary>Empty multi-surface.</summary>
        public new static readonly MultiSurface Empty = new MultiSurface(null, DefaultFactory);

        /// <summary>
        /// Constructs a <see cref="MultiSurface"/>.
        /// </summary>
        /// <param name="surfaces">
        /// Member geometries, or <c>null</c>/empty for an empty multi-surface.
        /// Each non-empty member must be a Year-1 surface
        /// (<see cref="Polygon"/> or <see cref="CurvePolygon"/>).
        /// </param>
        /// <param name="factory">Geometry factory</param>
        /// <exception cref="ArgumentException">
        /// If a member is <c>null</c>, is not an <see cref="ISurface"/>, or is
        /// not a Year-1 type (Ticket 7 member lock).
        /// </exception>
        public MultiSurface(Geometry[] surfaces, GeometryFactory factory)
            : base(ValidateYear1Members(surfaces), factory)
        {
        }

        private static Geometry[] ValidateYear1Members(Geometry[] surfaces)
        {
            if (surfaces == null || surfaces.Length == 0)
                return Array.Empty<Geometry>();
            for (int i = 0; i < surfaces.Length; i++)
            {
                if (surfaces[i] == null)
                    throw new ArgumentException("MultiSurface members must not be null", nameof(surfaces));
                if (!(surfaces[i] is ISurface surface))
                {
                    throw new ArgumentException(
                        "MultiSurface members must be surfaces (Polygon or CurvePolygon), got "
                        + surfaces[i].GetType().Name + ".", nameof(surfaces));
                }
                ValidateYear1Member(surface, nameof(surfaces));
            }
            return surfaces;
        }

        /// <summary>
        /// Year-1 <c>ST_MultiSurface</c> member types (ISO/IEC 13249-3 §4.2.27,
        /// Ticket 7): <see cref="Polygon"/> or <see cref="CurvePolygon"/> only.
        /// Other <see cref="ISurface"/> carriers (<see cref="Triangle"/>,
        /// a future <c>CompoundSurface</c>, …) are NTS Year-1 scope exclusions,
        /// not an ISO prohibition.
        /// </summary>
        /// <param name="member">The member to check.</param>
        /// <param name="paramName">The constructor parameter name for exceptions.</param>
        /// <exception cref="ArgumentException">When the member is not a Year-1 surface type.</exception>
        private static void ValidateYear1Member(ISurface member, string paramName)
        {
            if (member == null)
                return;
            if (member is Polygon || member is CurvePolygon)
                return;
            throw new ArgumentException(
                "An NTS Year-1 MultiSurface member must be a Polygon or CurvePolygon, got "
                + member.GetType().Name + ".", paramName);
        }

        /// <inheritdoc />
        protected override SortIndexValue SortIndex => SortIndexValue.MultiSurface;

        /// <inheritdoc />
        public override Dimension Dimension => Dimension.Surface;

        /// <inheritdoc />
        public override bool HasDimension(Dimension dim) => dim == Dimension.Surface;

        /// <inheritdoc />
        public override Dimension BoundaryDimension => Dimension.Curve;

        /// <inheritdoc />
        public override string GeometryType => TypeNameMultiSurface;

        /// <inheritdoc />
        public override OgcGeometryType OgcGeometryType => OgcGeometryType.MultiSurface;

        /// <summary>
        /// The member at <paramref name="n"/> as an <see cref="ISurface"/>
        /// (ISO/IEC 13249-3 §4.2.27 <c>ST_GeometryN</c>). Never downcast to
        /// a bare <see cref="Geometry"/> or a <see cref="LinearRing"/>: a
        /// <see cref="Polygon"/> or <see cref="CurvePolygon"/> member is
        /// returned as that subtype. There is no non-generic <c>Surface</c>
        /// class, so <see cref="ISurface"/> is the typed member (netstandard2.x
        /// cannot covariant-override <see cref="GeometryCollection.GetGeometryN"/>).
        /// </summary>
        /// <param name="n">Zero-based member index.</param>
        /// <returns>The member surface; the same instance stored at construction.</returns>
        public new ISurface GetGeometryN(int n) => (ISurface)base.GetGeometryN(n);

        /// <summary>
        /// The member at <paramref name="i"/> as an <see cref="ISurface"/>
        /// (ISO/IEC 13249-3 §4.2.27). Same no-downcast contract as
        /// <see cref="GetGeometryN"/>.
        /// </summary>
        /// <param name="i">Zero-based member index.</param>
        public new ISurface this[int i] => GetGeometryN(i);

        /// <summary>
        /// The member surfaces in construction order (ISO/IEC 13249-3 §4.2.27).
        /// Enumeration never downcasts a <see cref="CurvePolygon"/> to a
        /// <see cref="Polygon"/> or a ring to a <see cref="LinearRing"/>.
        /// </summary>
        public IEnumerable<ISurface> Surfaces
        {
            get
            {
                for (int i = 0; i < NumGeometries; i++)
                    yield return GetGeometryN(i);
            }
        }

        /// <inheritdoc />
        protected override Geometry ReverseInternal()
        {
            int n = NumGeometries;
            var rev = new Geometry[n];
            for (int i = 0; i < n; i++)
                rev[i] = ((Geometry)GetGeometryN(i)).Reverse();
            return new MultiSurface(rev, Factory);
        }

        /// <inheritdoc />
        protected override Geometry CopyInternal()
        {
            int n = NumGeometries;
            var copy = new Geometry[n];
            for (int i = 0; i < n; i++)
                copy[i] = ((Geometry)GetGeometryN(i)).Copy();
            return new MultiSurface(copy, Factory);
        }

        /// <summary>
        /// Arc-aware simplicity (§4.2.27; ticket 615-h rung 4, #639): the
        /// polygonal reading — simple iff every element's rings are simple
        /// (the clause makes MultiSurface simplicity definitional; see
        /// <see cref="CurveSimplicity"/>). Fail-closed residues are the
        /// kernel's, named in the throws.
        /// </summary>
        public override bool IsSimple
        {
            get
            {
                if (IsEmpty)
                    return true;
                return CurveSimplicity.IsSimple(this);
            }
        }

        /// <summary>
        /// Arc-aware validity, partial (§10.1.1 Desc 10, §8.2.1, §4.2.27;
        /// ticket 615-h rung 4, #639): definite <c>false</c> when an element
        /// provably violates an implemented rule or two element boundaries
        /// share a 1-D piece; a value passing everything throws naming the
        /// undecided interiors-disjoint condition — never an unchecked
        /// <c>true</c>.
        /// </summary>
        public override bool IsValid => CurveValidity.IsValid(this);

        /// <summary>
        /// Arc-aware length is not implemented yet. Empty is 0; otherwise throws.
        /// </summary>
        public override double Length =>
            IsEmpty ? 0d : throw CurvedGeometry.NotYetSupported(this, "Length");

        /// <summary>
        /// Arc-aware area is not implemented yet. Empty is 0; otherwise throws,
        /// including when every member is a <see cref="Polygon"/>.
        /// </summary>
        public override double Area =>
            IsEmpty ? 0d : throw CurvedGeometry.NotYetSupported(this, "Area");

        /// <inheritdoc />
        protected override Envelope ComputeEnvelopeInternal()
        {
            if (IsEmpty) return new Envelope();
            throw CurvedGeometry.NotYetSupported(this, "Envelope");
        }

        /// <summary>
        /// Arc-aware boundary is not implemented yet.
        /// </summary>
        /// <remarks>
        /// The inherited <see cref="GeometryCollection.Boundary"/> asserts because
        /// <see cref="OgcGeometryType"/> is not <c>GeometryCollection</c>.
        /// </remarks>
        public override Geometry Boundary =>
            throw CurvedGeometry.NotYetSupported(this, "Boundary");

        /// <summary>
        /// Hashes a locally computed control-point envelope.
        /// </summary>
        /// <remarks>
        /// Base <see cref="Geometry.GetHashCode"/> reads <c>EnvelopeInternal</c>,
        /// which now throws for non-empty curve types. Hashing is identity, not a
        /// geometric answer; control points are EqualsExact-consistent.
        /// </remarks>
        public override int GetHashCode() => CurvedGeometry.HashControlEnvelope(this);
    }
}
