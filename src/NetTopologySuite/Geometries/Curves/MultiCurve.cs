// SPDX-License-Identifier: BSD-3-Clause
// Status: PRODUCTION (structure + Year-1 WKT + WKB type 11 + §4.2.25 typed members).
// Year-1 WKB 11 is complete (Tickets 4–6; no longer partial). Year-2 member
// names (CIRCLE, GEODESIC, ELLIPSE, NURBS, CLOTHOID, SPIRAL) remain omitted.
// Members are Curve (LS|CS|CC), never collapsed to LineString (F-MC / §4.2.25).
// IsSimple and IsValid are arc-aware (ISO/IEC 13249-3 §10.3.1 Desc 4 /
// §10.1.1 Desc 10; NetTopologySuite.Proofs #615 ticket 615-h rung 4, #639),
// with the kernel's fail-closed residues named in the throws. The remaining
// metrics and analytic ops (Length, Envelope, Distance, Centroid,
// InteriorPoint) fail closed with NotSupportedException until arc-aware
// implementations land; Linearize() is the explicit chord escape hatch.
// Assisted-by: xAI Grok; Ticket 6 typed-members: Cursor Grok 4.6

using System;
using System.Collections.Generic;

namespace NetTopologySuite.Geometries.Curves
{
    /// <summary>
    /// A SQL/MM <c>MultiCurve</c>: a collection of <see cref="Curve"/>s
    /// (<see cref="LineString"/>, <see cref="CircularString"/>, <see cref="CompoundCurve"/>).
    /// Matches GEOS <c>geom::MultiCurve</c> / ISO WKB type 11.
    /// </summary>
    /// <remarks>
    /// <see cref="GetGeometryN"/> and enumeration expose <see cref="Curve"/>
    /// and never collapse a <see cref="CircularString"/> or
    /// <see cref="CompoundCurve"/> to a flat <see cref="LineString"/> (the
    /// F-MC structural contract; ISO/IEC 13249-3 §4.2.25 <c>ST_GeometryN</c> /
    /// <c>ST_NumGeometries</c>). Year-1 WKT and WKB type 11 are complete.
    /// Year-2 member names (<c>CIRCLE</c>, <c>GEODESIC</c>, <c>ELLIPSE</c>,
    /// <c>NURBS</c>, <c>CLOTHOID</c>, <c>SPIRAL</c>) remain omitted.
    /// <para/>
    /// The remaining analytic ops fail closed with
    /// <see cref="NotSupportedException"/> until arc-aware implementations land.
    /// </remarks>
    [Serializable]
    public class MultiCurve : GeometryCollection, ILineal
    {
        /// <summary>Empty multi-curve.</summary>
        public new static readonly MultiCurve Empty = new MultiCurve(null, DefaultFactory);

        /// <summary>
        /// Constructs a <see cref="MultiCurve"/>.
        /// </summary>
        /// <param name="curves">
        /// Member geometries, or <c>null</c>/empty for an empty multi-curve.
        /// Each non-empty member must be a Year-1 curve
        /// (<see cref="LineString"/>, <see cref="CircularString"/>, or
        /// <see cref="CompoundCurve"/> with LineString|CircularString members).
        /// </param>
        /// <param name="factory">Geometry factory</param>
        /// <exception cref="ArgumentException">
        /// If a member is <c>null</c>, is not a <see cref="Curve"/>, or is not
        /// a Year-1 type (Ticket 4 member lock).
        /// </exception>
        public MultiCurve(Geometry[] curves, GeometryFactory factory)
            : base(ValidateYear1Members(curves), factory)
        {
        }

        private static Geometry[] ValidateYear1Members(Geometry[] curves)
        {
            if (curves == null || curves.Length == 0)
                return Array.Empty<Geometry>();
            for (int i = 0; i < curves.Length; i++)
            {
                if (curves[i] == null)
                    throw new ArgumentException("MultiCurve members must not be null", nameof(curves));
                if (!(curves[i] is Curve curve))
                {
                    throw new ArgumentException(
                        "MultiCurve members must be curves (LineString, CircularString or CompoundCurve), got "
                        + curves[i].GetType().Name + ".", nameof(curves));
                }
                ValidateYear1Member(curve, nameof(curves));
            }
            return curves;
        }

        /// <summary>
        /// Year-1 <c>ST_MultiCurve</c> member types (ISO/IEC 13249-3 §4.2.25 /
        /// §5.1.67 g4 <c>curveMember</c>, Ticket 4): <see cref="LineString"/>
        /// (including <see cref="LinearRing"/>), <see cref="CircularString"/>,
        /// or <see cref="CompoundCurve"/> whose members are
        /// <see cref="LineString"/> | <see cref="CircularString"/> only.
        /// Nested <see cref="CompoundCurve"/> members are rejected.
        /// </summary>
        /// <param name="member">The member to check.</param>
        /// <param name="paramName">The constructor parameter name for exceptions.</param>
        /// <exception cref="ArgumentException">When the member is not a Year-1 curve type.</exception>
        private static void ValidateYear1Member(Curve member, string paramName)
        {
            if (member == null || member.IsEmpty)
                return;
            if (member is LineString || member is CircularString)
                return;
            if (member is CompoundCurve compound)
            {
                foreach (var component in compound.Curves)
                {
                    if (component is CompoundCurve)
                    {
                        throw new ArgumentException(
                            "A Year-1 CompoundCurve member must not contain nested CompoundCurve members " +
                            "(contiguous LineString | CircularString only).", paramName);
                    }
                    if (!(component is LineString || component is CircularString))
                    {
                        throw new ArgumentException(
                            "A Year-1 CompoundCurve member admits only LineString and CircularString components, got "
                            + component.GetType().Name + ".", paramName);
                    }
                }
                return;
            }
            throw new ArgumentException(
                "A Year-1 MultiCurve member must be a LineString, CircularString or CompoundCurve, got "
                + member.GetType().Name + ".", paramName);
        }

        /// <inheritdoc />
        protected override SortIndexValue SortIndex => SortIndexValue.MultiCurve;

        /// <inheritdoc />
        public override Dimension Dimension => Dimension.Curve;

        /// <inheritdoc />
        public override bool HasDimension(Dimension dim) => dim == Dimension.Curve;

        /// <inheritdoc />
        public override Dimension BoundaryDimension
        {
            get
            {
                if (IsClosed)
                    return Dimension.False;
                return Dimension.Point;
            }
        }

        /// <inheritdoc />
        public override string GeometryType => TypeNameMultiCurve;

        /// <inheritdoc />
        public override OgcGeometryType OgcGeometryType => OgcGeometryType.MultiCurve;

        /// <summary>
        /// The member at <paramref name="n"/> as a <see cref="Curve"/>
        /// (ISO/IEC 13249-3 §4.2.25 <c>ST_GeometryN</c>). Never downcast to
        /// <see cref="LineString"/>: a <see cref="CircularString"/> or
        /// <see cref="CompoundCurve"/> member is returned as that subtype.
        /// </summary>
        /// <param name="n">Zero-based member index.</param>
        /// <returns>The member curve; the same instance stored at construction.</returns>
        public new Curve GetGeometryN(int n) => (Curve)base.GetGeometryN(n);

        /// <summary>
        /// The member at <paramref name="i"/> as a <see cref="Curve"/>
        /// (ISO/IEC 13249-3 §4.2.25). Same no-downcast contract as
        /// <see cref="GetGeometryN"/>.
        /// </summary>
        /// <param name="i">Zero-based member index.</param>
        public new Curve this[int i] => GetGeometryN(i);

        /// <summary>
        /// The member curves in construction order (ISO/IEC 13249-3 §4.2.25).
        /// Enumeration never downcasts <see cref="CircularString"/> or
        /// <see cref="CompoundCurve"/> to <see cref="LineString"/>.
        /// </summary>
        public IEnumerable<Curve> Curves
        {
            get
            {
                for (int i = 0; i < NumGeometries; i++)
                    yield return GetGeometryN(i);
            }
        }

        /// <summary>True if non-empty and every member curve is closed.</summary>
        public bool IsClosed
        {
            get
            {
                if (IsEmpty)
                    return false;
                for (int i = 0; i < NumGeometries; i++)
                {
                    if (!GetGeometryN(i).IsClosed)
                        return false;
                }
                return true;
            }
        }

        /// <inheritdoc />
        protected override Geometry ReverseInternal()
        {
            int n = NumGeometries;
            var rev = new Curve[n];
            for (int i = 0; i < n; i++)
                rev[i] = (Curve)GetGeometryN(i).Reverse();
            return new MultiCurve(rev, Factory);
        }

        /// <inheritdoc />
        protected override Geometry CopyInternal()
        {
            int n = NumGeometries;
            var copy = new Curve[n];
            for (int i = 0; i < n; i++)
                copy[i] = (Curve)GetGeometryN(i).Copy();
            return new MultiCurve(copy, Factory);
        }

        /// <summary>
        /// Arc-aware simplicity (§4.2.25 / §10.3.1 Desc 4; ticket 615-h
        /// rung 4, #639): simple iff every member is simple and any two
        /// members meet only at points in the boundaries of BOTH members
        /// (Mod-2: the endpoints of an open member; a closed member has no
        /// boundary). Fail-closed residues are the kernel's, named in the
        /// throws.
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
        /// Arc-aware validity (§10.1.1 Desc 10; ticket 615-h rung 4, #639):
        /// definite <c>false</c> when a member provably violates an
        /// implemented ISO/IEC 13249-3 rule, checked <c>true</c> otherwise —
        /// element well-formedness is the collection's complete validity
        /// obligation (§10.3.1 Desc 4's inter-member condition defines
        /// ST_IsSimple, not validity).
        /// </summary>
        public override bool IsValid => CurveValidity.IsValid(this);

        /// <summary>
        /// Arc-aware length is not implemented yet. Empty is 0; otherwise throws,
        /// including when every member is a <see cref="LineString"/>.
        /// </summary>
        public override double Length =>
            IsEmpty ? 0d : throw CurvedGeometry.NotYetSupported(this, "Length");

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
