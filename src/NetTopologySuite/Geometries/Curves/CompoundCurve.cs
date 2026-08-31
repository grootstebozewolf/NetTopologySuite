// SPDX-License-Identifier: BSD-3-Clause
//
// AI assistance disclosure:
//   AI-drafted, human-reviewed and curated. Per the same convention used for the
//   companion JTS prototype at grootstebozewolf/jts#1, AI-generated portions are
//   dedicated to CC0-1.0; human curation falls under the NTS BSD-3-Clause grant.
//
//   Assisted-by: Claude (Fable 5)
//
// Status: PRODUCTION (structure + WKT/WKB) — GEOS 3.13-class foundation.
// Length is EXACT over the arc locus (ISO/IEC 13249-3 7.3.1 Desc 8; issue
// NetTopologySuite.Proofs#615 ticket 615-d), Envelope is EXACT over the
// locus (5.1.19 Desc 2b; ticket 615-e), and point-to-curve Distance is EXACT
// over the locus via DistanceOp.Distance (5.1.41 Desc 2a; ticket 615-f --
// curve-to-curve stays fail-closed pending arc-arc machinery, 615-h lane).
// The remaining metrics and analytic ops (Area, IsSimple, Centroid,
// InteriorPoint) fail closed with NotSupportedException until their
// arc-aware implementations land; Linearize() is the explicit chord escape
// hatch.
// IsValid is rung-1 partial (ticket 615-g): definite-false for implemented
// clause rules, fail-closed naming rung 2 (ticket 615-h) otherwise.

using System;
using System.Collections.Generic;

namespace NetTopologySuite.Geometries.Curves
{
    /// <summary>
    /// A SQL/MM Spatial (ISO/IEC 13249-3) <c>CompoundCurve</c>: a single, contiguous
    /// curve composed of a sequence of <see cref="Curve"/> components -- either
    /// <see cref="LineString"/>s or <see cref="CircularString"/>s -- where each
    /// component starts at the end point of its predecessor.
    /// </summary>
    /// <remarks>
    /// Nested <c>CompoundCurve</c> components are accepted -- ISO/IEC 13249-3
    /// §7.10.1 admits every <c>ST_Curve</c> subtype as a component -- and are
    /// spliced into the flat component list on construction: the value's point
    /// set is unchanged and component enumeration reports the spliced sequence
    /// (normalization, not restriction; PostGIS behaves the same way). See
    /// ADR-0005 in NetTopologySuite.Proofs: conform at the boundary, normalize
    /// inside.
    /// <para/>
    /// <see cref="Length"/>, the envelope, and point-to-curve distance (via
    /// <c>DistanceOp.Distance</c>) are exact over the arc locus; the remaining
    /// metrics and analytic ops fail closed with
    /// <see cref="NotSupportedException"/> until their arc-aware implementations
    /// land; <see cref="Linearize()"/> is the explicit chord escape hatch.
    /// </remarks>
    [Serializable]
    public class CompoundCurve : Curve, ILinearizable<LineString>
    {
        /// <summary>The component curves.</summary>
        private readonly Curve[] _curves;

        /// <summary>
        /// Initializes a new instance of the <see cref="CompoundCurve"/> class.
        /// </summary>
        /// <param name="curves">
        /// The component curves, in traversal order. A <c>CompoundCurve</c> component
        /// is spliced into the flat list (its own components are flat by construction,
        /// so the splice is depth-1); an empty component contributes nothing to the
        /// value's point set and is dropped.
        /// </param>
        /// <param name="factory">The geometry factory</param>
        /// <exception cref="ArgumentException">
        /// If a component is <c>null</c>, or if a component does not start at the
        /// end point of its predecessor (checked on the flattened sequence, so
        /// splice boundaries -- and neighbours of dropped empties -- join too).
        /// </exception>
        /// <remarks>
        /// Intake enforces representability only (ADR-0005 in
        /// NetTopologySuite.Proofs): null components are forbidden by ISO/IEC
        /// 13249-3 §7.10.1 Desc 5; contiguity is §7.10.1 Desc 7 / §4.2.13, and
        /// without it the value's traversal is not even well defined. The spec is
        /// silent on empty components, so they are normalized away rather than
        /// rejected. Every further ISO "shall" belongs to arc-aware
        /// <c>ST_IsValid</c>, not this constructor.
        /// </remarks>
        public CompoundCurve(Curve[] curves, GeometryFactory factory) : base(factory)
        {
            if (curves == null || curves.Length == 0)
            {
                _curves = new Curve[0];
                return;
            }
            var flat = new List<Curve>();
            for (int i = 0; i < curves.Length; i++)
            {
                if (curves[i] == null)
                {
                    // §7.10.1 Desc 5: components shall not be null.
                    throw new ArgumentException(
                        "A CompoundCurve must not contain null components.", nameof(curves));
                }
                if (curves[i].IsEmpty)
                {
                    // Spec-silent (only null is forbidden): an empty component
                    // adds nothing to the point set -- drop it (ticket 615-c).
                    continue;
                }
                if (curves[i] is CompoundCurve nested)
                {
                    flat.AddRange(nested.Curves);
                }
                else
                {
                    flat.Add(curves[i]);
                }
            }
            for (int i = 1; i < flat.Count; i++)
            {
                // §7.10.1 Desc 7 / §4.2.13: end point of each curve coincides
                // with the start point of the next (2D, matching ST_IsClosed's
                // default reading, §4.2.4.1 item 5).
                var previousEnd = flat[i - 1].EndPoint.Coordinate;
                var currentStart = flat[i].StartPoint.Coordinate;
                if (!previousEnd.Equals2D(currentStart))
                {
                    throw new ArgumentException(
                        "The components of a CompoundCurve must be contiguous: flattened component " + i +
                        " starts at " + currentStart + " but its predecessor ends at " + previousEnd + ".",
                        nameof(curves));
                }
            }
            // Fresh array: Normalize reverses and rewrites this array in place.
            _curves = flat.ToArray();
        }

        /// <summary>
        /// Gets the component curves of this <c>CompoundCurve</c>.
        /// </summary>
        public IReadOnlyList<Curve> Curves => _curves;

        /// <inheritdoc cref="Geometry.IsEmpty"/>
        public override bool IsEmpty => _curves.Length == 0;

        /// <inheritdoc cref="Geometry.Coordinate"/>
        public override Coordinate Coordinate => IsEmpty ? null : _curves[0].Coordinate;

        /// <summary>
        /// The coordinates of the components, concatenated in traversal order with
        /// the shared join point of adjacent components emitted only once.
        /// </summary>
        public override Coordinate[] Coordinates
        {
            get
            {
                var coordinates = new List<Coordinate>();
                for (int i = 0; i < _curves.Length; i++)
                {
                    var componentCoordinates = _curves[i].Coordinates;
                    for (int j = i == 0 ? 0 : 1; j < componentCoordinates.Length; j++)
                    {
                        coordinates.Add(componentCoordinates[j]);
                    }
                }
                return coordinates.ToArray();
            }
        }

        /// <inheritdoc cref="Geometry.NumPoints"/>
        public override int NumPoints
        {
            get
            {
                if (IsEmpty) return 0;
                int numPoints = _curves[0].NumPoints;
                for (int i = 1; i < _curves.Length; i++)
                {
                    numPoints += _curves[i].NumPoints - 1;
                }
                return numPoints;
            }
        }

        /// <inheritdoc/>
        public override double[] GetOrdinates(Ordinate ordinate)
        {
            if (IsEmpty) return new double[0];
            var ordinates = new List<double>();
            for (int i = 0; i < _curves.Length; i++)
            {
                double[] componentOrdinates = _curves[i].GetOrdinates(ordinate);
                for (int j = i == 0 ? 0 : 1; j < componentOrdinates.Length; j++)
                {
                    ordinates.Add(componentOrdinates[j]);
                }
            }
            return ordinates.ToArray();
        }

        /// <inheritdoc cref="Curve.StartPoint"/>
        public override Point StartPoint =>
            IsEmpty ? null : _curves[0].StartPoint;

        /// <inheritdoc cref="Curve.EndPoint"/>
        public override Point EndPoint =>
            IsEmpty ? null : _curves[_curves.Length - 1].EndPoint;

        /// <inheritdoc cref="Curve.IsClosed"/>
        public override bool IsClosed
        {
            get
            {
                if (IsEmpty) return false;
                return StartPoint.Coordinate.Equals2D(EndPoint.Coordinate);
            }
        }

        /// <inheritdoc cref="Geometry.GeometryType"/>
        public override string GeometryType => "CompoundCurve";

        /// <inheritdoc cref="Geometry.OgcGeometryType"/>
        public override OgcGeometryType OgcGeometryType => OgcGeometryType.CompoundCurve;

        /// <summary>
        /// The exact metric length: the sum of the component lengths
        /// (ISO/IEC 13249-3 §7.10.1 over §7.1.2), each component computing its
        /// own exact length — arc components over the arc locus, never a chord
        /// approximation. Empty is 0.
        /// </summary>
        public override double Length
        {
            get
            {
                double total = 0;
                for (int i = 0; i < _curves.Length; i++)
                    total += _curves[i].Length;
                return total;
            }
        }

        /// <summary>
        /// Arc-aware validity (<see cref="CurveValidity"/>; tickets 615-g,
        /// 615-h #634): definite <c>false</c> when an implemented ISO/IEC
        /// 13249-3 rule is violated (component well-formedness §7.10.1
        /// Desc 3, contiguity Desc 7), checked <c>true</c> otherwise —
        /// those two rules are §7.10.1's complete validity obligations
        /// ("simple ∧ closed ⇒ ring", Desc 8, is a definition, not a
        /// constraint).
        /// </summary>
        public override bool IsValid => CurveValidity.IsValid(this);

        /// <summary>
        /// Arc-aware simplicity over the concatenated component chain
        /// (§4.2.4 over the §7.3.1 Desc 8 loci; ticket 615-h rung 3,
        /// #634): simple iff no two chain segments meet outside the permitted
        /// shared vertices (see <see cref="CurveSimplicity"/>). LineString
        /// components contribute one chord per consecutive coordinate pair.
        /// Fail-closed residues are the kernel's, named in the throws.
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
        /// The boundary of a curve per the Mod-2 rule: empty when the curve is empty
        /// or closed, otherwise the two end points.
        /// </summary>
        public override Geometry Boundary
        {
            get
            {
                if (IsEmpty || IsClosed)
                {
                    return Factory.CreateMultiPoint();
                }
                return Factory.CreateMultiPoint(new[] { StartPoint, EndPoint });
            }
        }

        /// <summary>
        /// The exact envelope: the union of the component envelopes, each
        /// component computing its own exact envelope (arc components over the
        /// arc locus, ISO/IEC 13249-3 §5.1.19 Desc 2b).
        /// </summary>
        protected override Envelope ComputeEnvelopeInternal()
        {
            var env = new Envelope();
            for (int i = 0; i < _curves.Length; i++)
                env.ExpandToInclude(_curves[i].EnvelopeInternal);
            return env;
        }

        /// <summary>
        /// Hashes a locally computed control-point envelope.
        /// </summary>
        /// <remarks>
        /// Base <see cref="Geometry.GetHashCode"/> reads <c>EnvelopeInternal</c>,
        /// which now throws for non-empty curve types. Hashing is identity, not a
        /// geometric answer; control points are EqualsExact-consistent.
        /// </remarks>
        public override int GetHashCode() => CurvedGeometry.HashControlEnvelope(Coordinates);

        /// <inheritdoc/>
        public override bool EqualsExact(Geometry other, double tolerance)
        {
            if (!IsEquivalentClass(other)) return false;
            var o = (CompoundCurve)other;
            if (_curves.Length != o._curves.Length) return false;
            for (int i = 0; i < _curves.Length; i++)
            {
                if (!_curves[i].EqualsExact(o._curves[i], tolerance))
                    return false;
            }
            return true;
        }

        /// <inheritdoc/>
        public override void Apply(ICoordinateFilter filter)
        {
            for (int i = 0; i < _curves.Length; i++) _curves[i].Apply(filter);
        }

        /// <inheritdoc/>
        public override void Apply(ICoordinateSequenceFilter filter)
        {
            for (int i = 0; i < _curves.Length; i++)
            {
                _curves[i].Apply(filter);
                if (filter.Done) break;
            }
            if (filter.GeometryChanged) GeometryChanged();
        }

        /// <inheritdoc/>
        public override void Apply(IEntireCoordinateSequenceFilter filter)
        {
            for (int i = 0; i < _curves.Length; i++)
            {
                _curves[i].Apply(filter);
                if (filter.Done) break;
            }
            if (filter.GeometryChanged) GeometryChanged();
        }

        /// <inheritdoc/>
        public override void Apply(IGeometryFilter filter) => filter.Filter(this);

        /// <inheritdoc/>
        public override void Apply(IGeometryComponentFilter filter)
        {
            filter.Filter(this);
            for (int i = 0; i < _curves.Length; i++) _curves[i].Apply(filter);
        }

        /// <inheritdoc/>
        protected override Geometry CopyInternal()
        {
            var curves = new Curve[_curves.Length];
            for (int i = 0; i < _curves.Length; i++)
            {
                curves[i] = (Curve)_curves[i].Copy();
            }
            return new CompoundCurve(curves, Factory);
        }

        /// <inheritdoc/>
        public override void Normalize()
        {
            // Mirror the CircularString choice: flip traversal direction when the
            // start point is lexicographically greater than the end point.
            if (IsEmpty) return;
            if (StartPoint.Coordinate.CompareTo(EndPoint.Coordinate) > 0)
            {
                Array.Reverse(_curves);
                for (int i = 0; i < _curves.Length; i++)
                {
                    _curves[i] = (Curve)_curves[i].Reverse();
                }
                GeometryChanged();
            }
        }

        /// <inheritdoc/>
        protected override Geometry ReverseInternal()
        {
            var curves = new Curve[_curves.Length];
            for (int i = 0; i < _curves.Length; i++)
            {
                curves[i] = (Curve)_curves[_curves.Length - 1 - i].Reverse();
            }
            return new CompoundCurve(curves, Factory);
        }

        /// <inheritdoc/>
        protected override bool IsEquivalentClass(Geometry other) => other is CompoundCurve;

        /// <summary>
        /// CompareTo for two CompoundCurves uses lex order of the concatenated
        /// coordinates (type-blind across component kinds).
        /// </summary>
        protected internal override int CompareToSameClass(object o)
        {
            var other = (CompoundCurve)o;
            var a = Coordinates;
            var b = other.Coordinates;
            int n = Math.Min(a.Length, b.Length);
            for (int i = 0; i < n; i++)
            {
                int c = a[i].CompareTo(b[i]);
                if (c != 0) return c;
            }
            return a.Length.CompareTo(b.Length);
        }

        /// <inheritdoc/>
        protected internal override int CompareToSameClass(object o, IComparer<CoordinateSequence> comp)
        {
            var other = (CompoundCurve)o;
            var factory = Factory.CoordinateSequenceFactory;
            return comp.Compare(factory.Create(Coordinates), factory.Create(other.Coordinates));
        }

        /// <inheritdoc/>
        protected override SortIndexValue SortIndex => SortIndexValue.CompoundCurve;

        /// <summary>
        /// Returns a chord approximation of this compound curve as a single
        /// <see cref="LineString"/>, concatenating linearized components and
        /// collapsing shared join points.
        /// </summary>
        public LineString Linearize() => Linearize(double.NaN);

        /// <summary>
        /// Returns a chord approximation of this compound curve as a single
        /// <see cref="LineString"/>.
        /// </summary>
        /// <param name="arcSegmentLength">
        /// Passed through to <see cref="CircularString"/> components, which
        /// densify by sagitta for a finite value (keeping every supplied
        /// control as an exact vertex); a non-finite value yields the
        /// control polyline.
        /// </param>
        public LineString Linearize(double arcSegmentLength)
        {
            if (IsEmpty)
            {
                return Factory.CreateLineString();
            }

            var coordinates = new List<Coordinate>();
            for (int i = 0; i < _curves.Length; i++)
            {
                LineString componentLine = LinearizeComponent(_curves[i], arcSegmentLength);
                var componentCoordinates = componentLine.Coordinates;
                for (int j = i == 0 ? 0 : 1; j < componentCoordinates.Length; j++)
                {
                    coordinates.Add(componentCoordinates[j]);
                }
            }
            return Factory.CreateLineString(coordinates.ToArray());
        }

        private static LineString LinearizeComponent(Curve component, double arcSegmentLength)
        {
            switch (component)
            {
                case CircularString circularString:
                    return circularString.Linearize(arcSegmentLength);
                case LineString lineString:
                    return lineString;
                default:
                    // Defensive: the constructor splices nested CompoundCurves flat,
                    // so no component is compound here; other Curve subtypes
                    // linearize via control coordinates.
                    return component.Factory.CreateLineString(component.Coordinates);
            }
        }
    }
}
