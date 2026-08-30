// SPDX-License-Identifier: BSD-3-Clause
// Status: PRODUCTION (structure + WKT/WKB) — GEOS / ISO WKB type 12.
// IsSimple is arc-aware (ISO/IEC 13249-3 §4.2.27's definitional reading:
// every element's rings simple) and IsValid is arc-aware partial (element
// propagation, per-element ring conditions, cross-element boundary overlap;
// the interiors-disjoint condition fail-closes naming issue #641) —
// NetTopologySuite.Proofs #615 ticket 615-h rung 4, #639. The remaining
// metrics and analytic ops (Length, Area, Envelope, Distance, Centroid,
// InteriorPoint) fail closed with NotSupportedException until arc-aware
// implementations land; Linearize() is the explicit chord escape hatch.
// Assisted-by: xAI Grok

using System;

namespace NetTopologySuite.Geometries.Curves
{
    /// <summary>
    /// A SQL/MM <c>MultiSurface</c>: a collection of surfaces
    /// (<see cref="Polygon"/>, <see cref="CurvePolygon"/>).
    /// Matches GEOS <c>geom::MultiSurface</c> / ISO WKB type 12.
    /// </summary>
    [Serializable]
    public class MultiSurface : GeometryCollection, IPolygonal
    {
        /// <summary>Empty multi-surface.</summary>
        public new static readonly MultiSurface Empty = new MultiSurface(null, DefaultFactory);

        /// <summary>
        /// Constructs a <see cref="MultiSurface"/>.
        /// </summary>
        /// <param name="surfaces">Member surfaces (must implement <see cref="ISurface"/>)</param>
        /// <param name="factory">Geometry factory</param>
        public MultiSurface(Geometry[] surfaces, GeometryFactory factory)
            : base(Validate(surfaces), factory)
        {
        }

        private static Geometry[] Validate(Geometry[] surfaces)
        {
            if (surfaces == null || surfaces.Length == 0)
                return Array.Empty<Geometry>();
            for (int i = 0; i < surfaces.Length; i++)
            {
                if (surfaces[i] == null)
                    throw new ArgumentException("MultiSurface members must not be null", nameof(surfaces));
                if (!(surfaces[i] is ISurface))
                    throw new ArgumentException(
                        "MultiSurface members must be surfaces (Polygon or CurvePolygon)", nameof(surfaces));
            }
            return surfaces;
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

        /// <inheritdoc />
        protected override Geometry ReverseInternal()
        {
            int n = NumGeometries;
            var rev = new Geometry[n];
            for (int i = 0; i < n; i++)
                rev[i] = GetGeometryN(i).Reverse();
            return new MultiSurface(rev, Factory);
        }

        /// <inheritdoc />
        protected override Geometry CopyInternal()
        {
            int n = NumGeometries;
            var copy = new Geometry[n];
            for (int i = 0; i < n; i++)
                copy[i] = GetGeometryN(i).Copy();
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
