// SPDX-License-Identifier: BSD-3-Clause
//
// AI assistance disclosure: AI-drafted, human-reviewed.
//   Assisted-by: Claude (Opus-4.7); Ticket 18 typed patches: Cursor Grok 4.6
//
// Status: PRODUCTION (structure + Year-1 WKT g4 list + WKB type 16 + typed patches).
// Year-1 WKB 16 is complete (Tickets 16–18; no longer partial). Named fields
// after the opener (PATCHES / ELEMENTS / MAXSIDELENGTH, plus ST_TINElement /
// GML TINElementTypeType kinds) remain omitted (Ticket 16 refuse-by-name).
// Members are Triangle (Surface<LinearRing>), not Polygon — the existing
// Triangle egg is kept; there is no second Triangle type and no PATCHES
// property. GetGeometryN / enumerator / GetTriangleN / Triangles expose the
// stored Triangle (a closed triangular patch with ExteriorRing) and never
// flatten to a coordinate dump. Copy and Reverse keep Triangle members.

using System;
using System.Collections.Generic;

namespace NetTopologySuite.Geometries.Curves
{
    /// <summary>
    /// An OGC SFA-CA Triangulated Irregular Network: a homogeneous collection of
    /// <see cref="Triangle"/>s.  Conceptually a piecewise-linear surface, but
    /// represented here as a <see cref="GeometryCollection"/> for tooling
    /// compatibility with collection tooling.
    /// </summary>
    /// <remarks>
    /// Year-1 patches are <see cref="Triangle"/>-as-surface
    /// (<see cref="Surface{T}"/> with a closed 4-coordinate
    /// <see cref="Triangle.ExteriorRing"/>), not <see cref="Polygon"/>.
    /// The owner card's "yield Polygon" cannot be met without replacing the
    /// existing <c>Triangle[]</c> egg; typed access
    /// (<see cref="GetGeometryN"/>, <see cref="GetTriangleN"/>,
    /// <see cref="Triangles"/>) returns the stored triangle so callers can
    /// read the patch via <see cref="Triangle.ExteriorRing"/> without a
    /// coordinate dump. There is no <c>PATCHES</c> property.
    /// <para/>
    /// Year-1 WKT (g4 list) and WKB type 16 are complete. Named fields
    /// (<c>PATCHES</c>, <c>ELEMENTS</c>, <c>MAXSIDELENGTH</c>) remain omitted.
    /// <para/>
    /// All elements are required to be <see cref="Triangle"/> instances; the
    /// constructor enforces this and rejects null members. Empty TIN is allowed.
    /// Adjacency, shared-edge consistency, and orientation invariants are not
    /// currently checked -- those are downstream validity properties that should
    /// accompany an eventual <c>IsValid</c>-style predicate.
    /// </remarks>
    [Serializable]
    public class Tin : GeometryCollection
    {
        /// <summary>Empty TIN.</summary>
        public new static readonly Tin Empty = new Tin(null, DefaultFactory);

        /// <summary>
        /// Initializes a new instance of the <see cref="Tin"/> class.
        /// </summary>
        /// <param name="triangles">
        /// The constituent triangles, or <c>null</c>/empty for an empty TIN.
        /// </param>
        /// <param name="factory">The geometry factory</param>
        /// <exception cref="ArgumentException">
        /// If a member is <c>null</c>.
        /// </exception>
        public Tin(Triangle[] triangles, GeometryFactory factory)
            : base(ValidateMembers(triangles), factory)
        {
        }

        /// <summary>
        /// Rejects null members on <see cref="Tin"/> itself (not only
        /// <see cref="GeometryCollection"/>). A null or empty array is an
        /// empty TIN.
        /// </summary>
        private static Triangle[] ValidateMembers(Triangle[] triangles)
        {
            if (triangles == null || triangles.Length == 0)
                return Array.Empty<Triangle>();
            for (int i = 0; i < triangles.Length; i++)
            {
                if (triangles[i] == null)
                    throw new ArgumentException("Tin members must not be null", nameof(triangles));
            }
            return triangles;
        }

        /// <inheritdoc cref="Geometry.GeometryType"/>
        public override string GeometryType => "TIN";

        /// <inheritdoc cref="Geometry.OgcGeometryType"/>
        public override OgcGeometryType OgcGeometryType => OgcGeometryType.TIN;

        /// <summary>The dimension of a TIN is that of a surface (2).</summary>
        public override Dimension Dimension => Dimension.Surface;

        /// <summary>The boundary of a TIN is curvilinear (the perimeter of the union).</summary>
        public override Dimension BoundaryDimension => Dimension.Curve;

        /// <summary>
        /// The patch at <paramref name="n"/> as the stored <see cref="Triangle"/>
        /// (ISO/IEC 13249-3 <c>ST_GeometryN</c>). Year-1 patches are
        /// Triangle-as-surface, not <see cref="Polygon"/> — meeting "yield
        /// Polygon" would require replacing the <c>Triangle[]</c> egg.
        /// Returns the same instance stored at construction; never flattens
        /// to a coordinate dump. Polygon-shaped access is
        /// <see cref="Triangle.ExteriorRing"/> on the returned patch.
        /// netstandard2.x cannot covariant-override
        /// <see cref="GeometryCollection.GetGeometryN"/>.
        /// </summary>
        /// <param name="n">Zero-based member index.</param>
        /// <returns>The member triangle; the same instance stored at construction.</returns>
        public new Triangle GetGeometryN(int n) => (Triangle)base.GetGeometryN(n);

        /// <summary>
        /// The patch at <paramref name="i"/> as the stored <see cref="Triangle"/>.
        /// Same no-flatten contract as <see cref="GetGeometryN"/>.
        /// </summary>
        /// <param name="i">Zero-based member index.</param>
        public new Triangle this[int i] => GetGeometryN(i);

        /// <summary>
        /// Gets the triangle at the given index.
        /// </summary>
        /// <param name="index">Zero-based member index.</param>
        /// <returns>The stored <see cref="Triangle"/>; same as <see cref="GetGeometryN"/>.</returns>
        public Triangle GetTriangleN(int index) => GetGeometryN(index);

        /// <summary>
        /// The member triangles in construction order.
        /// Enumeration never flattens a patch to a coordinate dump and never
        /// remints the <see cref="Triangle"/> egg as <see cref="Polygon"/>.
        /// </summary>
        public IEnumerable<Triangle> Triangles
        {
            get
            {
                for (int i = 0; i < NumGeometries; i++)
                    yield return GetGeometryN(i);
            }
        }

        /// <inheritdoc/>
        protected override Geometry CopyInternal()
        {
            var copies = new Triangle[NumGeometries];
            for (int i = 0; i < NumGeometries; i++)
            {
                copies[i] = (Triangle)GetGeometryN(i).Copy();
            }
            return new Tin(copies, Factory);
        }

        /// <inheritdoc/>
        protected override Geometry ReverseInternal()
        {
            var reversed = new Triangle[NumGeometries];
            for (int i = 0; i < NumGeometries; i++)
            {
                reversed[i] = (Triangle)GetGeometryN(i).Reverse();
            }
            return new Tin(reversed, Factory);
        }

        /// <inheritdoc/>
        protected override bool IsEquivalentClass(Geometry other) => other is Tin;

        /// <summary>
        /// The total area of the TIN, computed as the sum of triangle areas.
        /// Adjacent triangles that overlap will double-count; the current
        /// assumes a well-formed TIN.
        /// </summary>
        public override double Area
        {
            get
            {
                double total = 0.0;
                for (int i = 0; i < NumGeometries; i++)
                {
                    total += GetGeometryN(i).Area;
                }
                return total;
            }
        }

        /// <inheritdoc/>
        protected override SortIndexValue SortIndex => SortIndexValue.Tin;
    }
}
