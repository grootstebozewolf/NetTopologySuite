// SPDX-License-Identifier: BSD-3-Clause
//
// AI assistance disclosure:
//   AI-drafted, human-reviewed and curated. This PR was drafted with AI
//   assistance. Per the same convention used for the companion JTS prototype
//   at grootstebozewolf/jts#1, AI-generated portions are dedicated to
//   CC0-1.0; human curation falls under the NTS BSD-3-Clause grant.
//
//   Assisted-by: Cursor Grok 4.6; Ticket 21 typed members: Cursor Grok 4.6
//
// Status: PRODUCTION (structure + Year-1 WKT + WKB type 18 + typed members).
// Year-1 CIRCLE WKT + WKB 18 is complete (Tickets 19–21; no longer partial).
// Named Year-2 curves (GEODESICSTRING / ELLIPTICALCURVE / NURBSCURVE /
// CLOTHOID / SPIRALCURVE) stay omitted. Three non-collinear control points
// define the unique circumcircle (ISO/IEC 13249-3 §4.2.7 / §5.1.67).
// GetControlN / GetControlPointN expose the stored controls (not a flatten).
// Centre and Radius are the planar circumcircle. Length is 2πr over the
// locus. Envelope is the axis-aligned box of the full circle. Reverse is a
// point-order reverse of the three controls and stays Circle. The remaining
// analytic ops match CircularString honesty (fail-closed where CS does).
// Writer keyword is CIRCLE — never demoted to CIRCULARSTRING.

using System;
using System.Collections.Generic;
using NetTopologySuite.Algorithm;

namespace NetTopologySuite.Geometries.Curves
{
    /// <summary>
    /// An ISO/IEC 13249-3 <c>ST_Circle</c>: the unique circle through three
    /// non-collinear control points on the circumference.
    /// </summary>
    /// <remarks>
    /// Year-1 WKT is <c>CIRCLE [Z|M|ZM] ( &lt;point&gt; , &lt;point&gt; , &lt;point&gt; ) | EMPTY</c>
    /// (§4.2.7 / §5.1.67). Year-1 WKT and WKB type 18 are complete
    /// (Tickets 19–21; no longer partial). A non-empty value is closed by
    /// definition. <see cref="GetControlN"/> / <see cref="GetControlPointN"/>
    /// expose the three circumference controls without flattening.
    /// <see cref="Centre"/> and <see cref="Radius"/> are the planar (XY)
    /// circumcircle; a 3D centre is not defined (Z/M on <see cref="Centre"/>
    /// are <see cref="Coordinate.NullOrdinate"/>). <c>CIRCLE EMPTY</c> has
    /// <see cref="NumPoints"/> 0; <see cref="Centre"/> and <see cref="Radius"/>
    /// throw <see cref="InvalidOperationException"/>.
    /// <see cref="Length"/> is <c>2πr</c> over the locus. The envelope is the
    /// axis-aligned box of the full circle. <see cref="Geometry.Reverse()"/>
    /// of a non-empty <c>Circle</c> stays this type with the three controls
    /// reversed (<c>P3, P2, P1</c>) — never a <see cref="CircularString"/>.
    /// Named Year-2 curves stay omitted. WKB type 18 uses reducer-only Z/M/ZM
    /// (1018 / 2018 / 3018).
    /// <para/>
    /// A <see cref="CircularString"/> with three controls is a single arc
    /// through those points, not this type. The writer never emits
    /// <c>CIRCULARSTRING</c> or WKB type 8 for a <c>Circle</c>.
    /// </remarks>
    [Serializable]
    public class Circle : Curve, ILinearizable<LineString>
    {
        /// <summary>The three circumference controls, or empty.</summary>
        private readonly CoordinateSequence _points;

        /// <summary>
        /// Initializes a new instance of the <see cref="Circle"/> class.
        /// </summary>
        /// <param name="points">
        /// The three circumference points, or an empty sequence.
        /// </param>
        /// <param name="factory">The geometry factory</param>
        /// <exception cref="ArgumentException">
        /// If the sequence is non-empty and is not exactly three non-collinear
        /// points (ISO/IEC 13249-3 §4.2.7 / §5.1.67).
        /// </exception>
        public Circle(CoordinateSequence points, GeometryFactory factory) : base(factory)
        {
            if (points == null)
            {
                points = factory.CoordinateSequenceFactory.Create(0, Ordinates.XY);
            }
            if (points.Count != 0)
            {
                if (points.Count != 3)
                {
                    throw new ArgumentException(
                        "A non-empty Circle must have exactly three control points " +
                        "(ISO/IEC 13249-3 §4.2.7 / §5.1.67 CIRCLE (point, point, point)).",
                        nameof(points));
                }
                if (!CircularArcGeometry.TryCircle(
                        points.GetCoordinate(0),
                        points.GetCoordinate(1),
                        points.GetCoordinate(2),
                        out _, out _))
                {
                    throw new ArgumentException(
                        "The three points of a CIRCLE must not be collinear: they define " +
                        "the unique circumcircle (ISO/IEC 13249-3 §4.2.7).",
                        nameof(points));
                }
            }
            _points = points;
        }

        /// <summary>The circumference control points of this <c>Circle</c>.</summary>
        public CoordinateSequence CoordinateSequence => _points;

        /// <inheritdoc cref="Geometry.NumPoints"/>
        /// <remarks>
        /// A non-empty <c>Circle</c> has exactly three circumference controls.
        /// <c>CIRCLE EMPTY</c> has zero.
        /// </remarks>
        public override int NumPoints => _points.Count;

        /// <summary>
        /// Returns circumference control <paramref name="n"/> as a
        /// <see cref="Coordinate"/> (0-based). This is the stored control,
        /// not a linearized dump.
        /// </summary>
        /// <param name="n">Control index: 0, 1 or 2 on a non-empty circle.</param>
        /// <returns>The stored circumference control.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// When <paramref name="n"/> is outside <c>0..NumPoints-1</c>
        /// (including any index on <c>CIRCLE EMPTY</c>).
        /// </exception>
        public Coordinate GetControlN(int n)
        {
            if (n < 0 || n >= _points.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(n), n,
                    "Circle control index must be in 0..NumPoints-1. CIRCLE EMPTY has no controls.");
            }
            return _points.GetCoordinate(n);
        }

        /// <summary>
        /// Returns circumference control <paramref name="n"/> as a
        /// <see cref="Point"/> created by this geometry's factory.
        /// </summary>
        /// <param name="n">Control index: 0, 1 or 2 on a non-empty circle.</param>
        /// <returns>A <see cref="Point"/> at the stored circumference control.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// When <paramref name="n"/> is outside <c>0..NumPoints-1</c>
        /// (including any index on <c>CIRCLE EMPTY</c>).
        /// </exception>
        public Point GetControlPointN(int n)
        {
            return Factory.CreatePoint(GetControlN(n));
        }

        /// <summary>
        /// The planar (XY) circumcentre of the three circumference controls.
        /// </summary>
        /// <remarks>
        /// Exact XY via the planar circumcircle of the three controls
        /// (<c>Triangle.Circumcentre</c> over XY).
        /// A 3D centre is not defined: Z and M on the returned coordinate are
        /// <see cref="Coordinate.NullOrdinate"/> even when the controls carry
        /// Z/M (CircularString locus honesty — metrics are planar). Read Z/M
        /// on the controls via <see cref="GetControlN"/>.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// When this value is empty (<c>CIRCLE EMPTY</c> has no circumcircle).
        /// </exception>
        public Coordinate Centre
        {
            get
            {
                if (!TryGetCircumcircle(out var centre, out _))
                    throw new InvalidOperationException("Centre is undefined for CIRCLE EMPTY.");
                return centre;
            }
        }

        /// <summary>
        /// The planar (XY) circumradius of the three circumference controls.
        /// </summary>
        /// <remarks>
        /// Exact XY distance from <see cref="Centre"/> to any control. A 3D
        /// radius is not defined; Z/M on the controls do not enter the
        /// answer (CircularString locus honesty).
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// When this value is empty (<c>CIRCLE EMPTY</c> has no circumcircle).
        /// </exception>
        public double Radius
        {
            get
            {
                if (!TryGetCircumcircle(out _, out double radius))
                    throw new InvalidOperationException("Radius is undefined for CIRCLE EMPTY.");
                return radius;
            }
        }

        /// <inheritdoc cref="Geometry.IsEmpty"/>
        public override bool IsEmpty => _points.Count == 0;

        /// <inheritdoc cref="Geometry.Coordinate"/>
        public override Coordinate Coordinate => IsEmpty ? null : _points.GetCoordinate(0);

        /// <inheritdoc cref="Geometry.Coordinates"/>
        public override Coordinate[] Coordinates => _points.ToCoordinateArray();

        /// <inheritdoc/>
        public override double[] GetOrdinates(Ordinate ordinate)
        {
            if (IsEmpty) return new double[0];
            var ordinateFlag = (Ordinates)(1 << (int)ordinate);
            if ((_points.Ordinates & ordinateFlag) != ordinateFlag)
            {
                var nulls = new double[_points.Count];
                for (int i = 0; i < nulls.Length; i++) nulls[i] = Coordinate.NullOrdinate;
                return nulls;
            }
            var vals = new double[_points.Count];
            for (int i = 0; i < _points.Count; i++) vals[i] = _points.GetOrdinate(i, (int)ordinate);
            return vals;
        }

        /// <inheritdoc cref="Curve.StartPoint"/>
        public override Point StartPoint =>
            IsEmpty ? null : Factory.CreatePoint(_points.GetCoordinate(0));

        /// <summary>
        /// The end point. A non-empty <c>Circle</c> is closed, so this is the
        /// same location as <see cref="StartPoint"/>.
        /// </summary>
        public override Point EndPoint => StartPoint;

        /// <summary>
        /// A non-empty <c>Circle</c> is closed by definition
        /// (ISO/IEC 13249-3 §4.2.7).
        /// </summary>
        public override bool IsClosed => !IsEmpty;

        /// <inheritdoc cref="Geometry.GeometryType"/>
        public override string GeometryType => TypeNameCircle;

        /// <inheritdoc cref="Geometry.OgcGeometryType"/>
        public override OgcGeometryType OgcGeometryType => OgcGeometryType.Circle;

        /// <summary>
        /// The exact metric length over the locus: <c>2πr</c> for a non-empty
        /// circle, 0 when empty. No linearization is involved.
        /// </summary>
        public override double Length
        {
            get
            {
                if (!TryGetCircumcircle(out _, out double radius))
                    return 0d;
                return 2d * Math.PI * radius;
            }
        }

        /// <summary>
        /// Well-formed when empty or when the three controls are finite.
        /// Collinear intake is refused by the constructor, so a constructed
        /// non-empty value that still has three finite points is valid.
        /// </summary>
        public override bool IsValid
        {
            get
            {
                if (IsEmpty) return true;
                return !HasNonFiniteControl();
            }
        }

        /// <summary>
        /// A well-formed circle is simple (the locus meets only at the
        /// identified start/end). Empty is simple.
        /// </summary>
        public override bool IsSimple => true;

        /// <summary>
        /// The boundary of a closed curve is empty (Mod-2).
        /// </summary>
        public override Geometry Boundary => Factory.CreateMultiPoint();

        /// <summary>
        /// The exact envelope of the full-circle locus: centre ± r on each
        /// axis. Empty is the empty envelope.
        /// </summary>
        protected override Envelope ComputeEnvelopeInternal()
        {
            if (!TryGetCircumcircle(out var centre, out double radius))
                return new Envelope();
            return new Envelope(
                centre.X - radius, centre.X + radius,
                centre.Y - radius, centre.Y + radius);
        }

        /// <summary>
        /// Hashes a locally computed control-point envelope.
        /// </summary>
        /// <remarks>
        /// Hashing is identity, not a geometric answer; control points are
        /// EqualsExact-consistent.
        /// </remarks>
        public override int GetHashCode() => CurvedGeometry.HashControlEnvelope(_points);

        /// <inheritdoc/>
        public override bool EqualsExact(Geometry other, double tolerance)
        {
            if (!IsEquivalentClass(other)) return false;
            var o = (Circle)other;
            if (_points.Count != o._points.Count) return false;
            var cec = Factory.CoordinateEqualityComparer;
            for (int i = 0; i < _points.Count; i++)
            {
                if (!cec.Equals(_points.GetCoordinate(i), o._points.GetCoordinate(i), tolerance))
                    return false;
            }
            return true;
        }

        /// <inheritdoc/>
        public override void Apply(ICoordinateFilter filter)
        {
            for (int i = 0; i < _points.Count; i++) filter.Filter(_points.GetCoordinate(i));
        }

        /// <inheritdoc/>
        public override void Apply(ICoordinateSequenceFilter filter)
        {
            if (_points.Count == 0) return;
            for (int i = 0; i < _points.Count; i++)
            {
                filter.Filter(_points, i);
                if (filter.Done) break;
            }
            if (filter.GeometryChanged) GeometryChanged();
        }

        /// <inheritdoc/>
        public override void Apply(IEntireCoordinateSequenceFilter filter)
        {
            filter.Filter(_points);
            if (filter.GeometryChanged) GeometryChanged();
        }

        /// <inheritdoc/>
        public override void Apply(IGeometryFilter filter) => filter.Filter(this);

        /// <inheritdoc/>
        public override void Apply(IGeometryComponentFilter filter) => filter.Filter(this);

        /// <summary>
        /// Deep-copies this value as a <see cref="Circle"/> of the same three
        /// circumference controls (or empty). Never demotes to
        /// <see cref="CircularString"/>.
        /// </summary>
        protected override Geometry CopyInternal() => new Circle(_points.Copy(), Factory);

        /// <summary>
        /// Rotates the control triple so the lexicographically smallest point
        /// is first, preserving cyclic order (orientation).
        /// </summary>
        public override void Normalize()
        {
            if (IsEmpty) return;
            int min = 0;
            for (int i = 1; i < _points.Count; i++)
            {
                if (_points.GetCoordinate(i).CompareTo(_points.GetCoordinate(min)) < 0)
                    min = i;
            }
            if (min == 0) return;
            var rotated = new Coordinate[3];
            for (int i = 0; i < 3; i++)
                rotated[i] = _points.GetCoordinate((min + i) % 3).Copy();
            for (int i = 0; i < 3; i++)
            {
                _points.SetX(i, rotated[i].X);
                _points.SetY(i, rotated[i].Y);
                if (_points.HasZ) _points.SetZ(i, rotated[i].Z);
                if (_points.HasM) _points.SetM(i, rotated[i].M);
            }
        }

        /// <summary>
        /// Reverses the three circumference controls
        /// (<c>P1, P2, P3</c> → <c>P3, P2, P1</c>). The result is still a
        /// <see cref="Circle"/> of the same circumcircle; a full circle's
        /// reverse is a point-order reverse, never a demotion to
        /// <see cref="CircularString"/>. Empty stays empty.
        /// </summary>
        protected override Geometry ReverseInternal()
        {
            var rev = _points.Copy();
            CoordinateSequences.Reverse(rev);
            return new Circle(rev, Factory);
        }

        /// <inheritdoc/>
        protected override bool IsEquivalentClass(Geometry other) => other is Circle;

        /// <inheritdoc/>
        protected internal override int CompareToSameClass(object o)
        {
            var other = (Circle)o;
            int n = Math.Min(_points.Count, other._points.Count);
            for (int i = 0; i < n; i++)
            {
                int c = _points.GetCoordinate(i).CompareTo(other._points.GetCoordinate(i));
                if (c != 0) return c;
            }
            return _points.Count.CompareTo(other._points.Count);
        }

        /// <inheritdoc/>
        protected internal override int CompareToSameClass(object o, IComparer<CoordinateSequence> comp)
        {
            return comp.Compare(_points, ((Circle)o)._points);
        }

        /// <inheritdoc/>
        protected override SortIndexValue SortIndex => SortIndexValue.Circle;

        /// <summary>
        /// Returns a closed chord approximation through the three controls
        /// (<c>A, B, C, A</c>).
        /// </summary>
        public LineString Linearize() => Linearize(double.NaN);

        /// <summary>
        /// Densifies this circle. A non-finite tolerance returns the closed
        /// control polyline <c>A, B, C, A</c>. A finite tolerance densifies
        /// the three circumference arcs <c>A→B</c>, <c>B→C</c>, <c>C→A</c>
        /// via <see cref="CircularString.Linearize(double)"/>, keeping every
        /// supplied control as an exact vertex.
        /// </summary>
        /// <param name="arcSegmentLength">
        /// Maximum chord length along the locus; non-finite yields the
        /// closed control polyline.
        /// </param>
        public LineString Linearize(double arcSegmentLength)
        {
            if (IsEmpty)
                return Factory.CreateLineString();
            var a = _points.GetCoordinate(0);
            var b = _points.GetCoordinate(1);
            var c = _points.GetCoordinate(2);
            if (double.IsNaN(arcSegmentLength) || double.IsInfinity(arcSegmentLength))
            {
                return Factory.CreateLineString(new[] { a.Copy(), b.Copy(), c.Copy(), a.Copy() });
            }
            if (arcSegmentLength < 0.0)
            {
                throw new ArgumentException("tolerance must be non-negative: " + arcSegmentLength,
                    nameof(arcSegmentLength));
            }
            if (!TryGetCircumcircle(out var centre, out double radius))
                return Factory.CreateLineString();
            bool ccw = Orientation.Index(a, b, c) == OrientationIndex.CounterClockwise;
            var midAB = PointAtHalfSweep(a, b, centre, radius, ccw);
            var midBC = PointAtHalfSweep(b, c, centre, radius, ccw);
            var midCA = PointAtHalfSweep(c, a, centre, radius, ccw);
            var seven = Factory.CoordinateSequenceFactory.Create(
                new[] { a.Copy(), midAB, b.Copy(), midBC, c.Copy(), midCA, a.Copy() });
            return new CircularString(seven, Factory).Linearize(arcSegmentLength);
        }

        /// <summary>
        /// The circumcircle of the three controls, when the value is non-empty.
        /// </summary>
        /// <param name="centre">The circumcentre.</param>
        /// <param name="radius">The circumradius.</param>
        /// <returns><c>false</c> when empty (intake already refused collinear).</returns>
        internal bool TryGetCircumcircle(out Coordinate centre, out double radius)
        {
            if (IsEmpty)
            {
                centre = null;
                radius = double.NaN;
                return false;
            }
            return CircularArcGeometry.TryCircle(
                _points.GetCoordinate(0),
                _points.GetCoordinate(1),
                _points.GetCoordinate(2),
                out centre, out radius);
        }

        private bool HasNonFiniteControl()
        {
            for (int i = 0; i < _points.Count; i++)
            {
                double x = _points.GetX(i), y = _points.GetY(i);
                if (double.IsNaN(x) || double.IsInfinity(x) || double.IsNaN(y) || double.IsInfinity(y))
                    return true;
            }
            return false;
        }

        private static Coordinate PointAtHalfSweep(
            Coordinate from, Coordinate to, Coordinate centre, double radius, bool ccw)
        {
            double a0 = Math.Atan2(from.Y - centre.Y, from.X - centre.X);
            double a1 = Math.Atan2(to.Y - centre.Y, to.X - centre.X);
            double sweep = ccw
                ? AngleUtility.NormalizePositive(a1 - a0)
                : AngleUtility.NormalizePositive(a0 - a1);
            double mid = ccw ? a0 + 0.5 * sweep : a0 - 0.5 * sweep;
            var p = from.Copy();
            p.X = centre.X + radius * Math.Cos(mid);
            p.Y = centre.Y + radius * Math.Sin(mid);
            if (!double.IsNaN(from.Z) && !double.IsNaN(to.Z))
                p.Z = 0.5 * (from.Z + to.Z);
            return p;
        }
    }
}
