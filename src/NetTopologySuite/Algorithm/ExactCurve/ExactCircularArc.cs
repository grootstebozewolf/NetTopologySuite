// SPDX-License-Identifier: BSD-3-Clause
//
// AI assistance disclosure:
//   AI-drafted, human-reviewed. Assisted-by: Cursor Grok 4.6
//   AI-generated portions dedicated to CC0-1.0; human curation BSD-3-Clause.

using System;
using System.Collections.Generic;
using NetTopologySuite.Geometries;

namespace NetTopologySuite.Algorithm.ExactCurve
{
    /// <summary>
    /// Privileged ExactCurve primitive: one 3-control circular window.
    /// Closed-form <c>r·θ</c>, chord ≤ arc, in-arc, segment area,
    /// arc-length centroid, <see cref="PointAt"/>.
    /// </summary>
    /// <remarks>
    /// Maintainability: circumcircle lives here so callers do not copy the
    /// determinant; colinear triples degrade to an exact chord.
    /// Soundness: <see cref="Length"/> and <see cref="PointAt"/> never densify.
    /// Performance: static <see cref="LengthOf"/> is one hypot plus one multiply.
    /// Port of JTS <c>9797c2c4</c>.
    /// </remarks>
    public sealed class ExactCircularArc : IExactCurve
    {
        private const double DefaultToleranceFraction = 0.01;

        private readonly Coordinate _start;
        private readonly Coordinate _mid;
        private readonly Coordinate _end;
        private readonly double _cx;
        private readonly double _cy;
        private readonly double _r;
        private readonly double _a0;
        private readonly bool _ccw;
        private readonly double _sweep;
        private readonly bool _arc;

        public ExactCircularArc(Coordinate start, Coordinate mid, Coordinate end)
        {
            _start = start.Copy();
            _mid = mid.Copy();
            _end = end.Copy();
            if (!TryCircumcircle(start, mid, end, out _cx, out _cy, out _r))
            {
                _a0 = 0.0;
                _ccw = true;
                _sweep = 0.0;
                _arc = false;
                return;
            }
            _a0 = Math.Atan2(start.Y - _cy, start.X - _cx);
            var sw = AngleBetween.Through(_cx, _cy, start, mid, end);
            _ccw = sw.IsCcw;
            _sweep = sw.Radians;
            _arc = true;
        }

        /// <summary>Allocation-light <c>r·θ</c> (or chord).</summary>
        public static double LengthOf(Coordinate start, Coordinate mid, Coordinate end)
        {
            if (!TryCircumcircle(start, mid, end, out double cx, out double cy, out double r))
            {
                return start.Distance(end);
            }
            return r * AngleBetween.SweepOf(cx, cy, start, mid, end);
        }

        public Coordinate Start => _start;

        public Coordinate Mid => _mid;

        public Coordinate End => _end;

        public bool IsArc => _arc;

        public bool IsCcw => _ccw;

        public double Radius => _r;

        public Coordinate Center => _arc ? new Coordinate(_cx, _cy) : null;

        /// <summary>Central angle in <c>(0, 2π]</c>; <c>0</c> on a chord fallback.</summary>
        public double Sweep => _sweep;

        public double Length => _arc ? _r * _sweep : _start.Distance(_end);

        public double ChordLength => _start.Distance(_end);

        public bool IsExact => true;

        public Coordinate PointAt(double t)
        {
            if (double.IsNaN(t) || double.IsInfinity(t) || t < 0.0 || t > 1.0)
            {
                throw new ArgumentException("t must be in [0,1]: " + t, nameof(t));
            }
            if (t == 0.0)
            {
                return _start.Copy();
            }
            if (t == 1.0)
            {
                return _end.Copy();
            }
            if (!_arc)
            {
                return new Coordinate(
                    _start.X + t * (_end.X - _start.X),
                    _start.Y + t * (_end.Y - _start.Y));
            }
            double ang = _a0 + (_ccw ? _sweep : -_sweep) * t;
            return new Coordinate(_cx + _r * Math.Cos(ang), _cy + _r * Math.Sin(ang));
        }

        /// <summary>Documented densify shim. Not used by <see cref="Length"/> or <see cref="PointAt"/>.</summary>
        public Geometry ToLinear(double tolerance)
        {
            if (tolerance < 0.0)
            {
                throw new ArgumentException("tolerance must be non-negative: " + tolerance, nameof(tolerance));
            }
            if (!_arc)
            {
                return GeometryFactory.Default.CreateLineString(new[] { _start.Copy(), _end.Copy() });
            }
            double eps = tolerance == 0.0 ? _r * DefaultToleranceFraction : tolerance;
            int segments = SegmentCount(_r, _sweep, eps);
            double delta = _sweep / segments;
            if (!_ccw)
            {
                delta = -delta;
            }
            var pts = new List<Coordinate>(segments + 2) { _start.Copy() };
            double midEps = Math.Max(1.0e-12, _r * 1.0e-9);
            bool midEmitted = _mid.Equals2D(_start) || _mid.Equals2D(_end);
            for (int i = 1; i < segments; i++)
            {
                double ang = _a0 + i * delta;
                var pt = new Coordinate(_cx + _r * Math.Cos(ang), _cy + _r * Math.Sin(ang));
                if (!midEmitted && pt.Distance(_mid) <= midEps)
                {
                    pts.Add(_mid.Copy());
                    midEmitted = true;
                    continue;
                }
                pts.Add(pt);
            }
            if (!midEmitted)
            {
                double midSweep = AngleBetween.Travelled(_ccw,
                    _start.X - _cx, _start.Y - _cy, _mid.X - _cx, _mid.Y - _cy);
                int insertAt = pts.Count;
                for (int i = 1; i < pts.Count; i++)
                {
                    double s = AngleBetween.Travelled(_ccw,
                        _start.X - _cx, _start.Y - _cy, pts[i].X - _cx, pts[i].Y - _cy);
                    if (s >= midSweep)
                    {
                        insertAt = i;
                        break;
                    }
                }
                pts.Insert(insertAt, _mid.Copy());
            }
            pts.Add(_end.Copy());
            return GeometryFactory.Default.CreateLineString(pts.ToArray());
        }

        public bool ChordLeArc()
        {
            double chord = ChordLength;
            if (!_arc)
            {
                return true;
            }
            double arcLen = _r * _sweep;
            if (chord <= arcLen)
            {
                return true;
            }
            double chordFromSweep = 2.0 * _r * Math.Sin(0.5 * _sweep);
            double bound = Math.Max(arcLen, chordFromSweep);
            return chord <= bound + Ulp(Math.Max(bound, chord));
        }

        public bool InArc(Coordinate p, double radialTol)
        {
            if (p == null)
            {
                return false;
            }
            if (!_arc)
            {
                return OnSegment(p, _start, _end, radialTol);
            }
            double dx = p.X - _cx;
            double dy = p.Y - _cy;
            double d2 = dx * dx + dy * dy;
            double r2 = _r * _r;
            double tol2 = radialTol * (2.0 * _r + radialTol);
            if (Math.Abs(d2 - r2) > tol2)
            {
                return false;
            }
            return OnSweep(p);
        }

        /// <summary>Circular-segment area <c>r²/2 · (θ − sin θ)</c>. Zero on a chord.</summary>
        public double CircularSegmentArea()
        {
            if (!_arc)
            {
                return 0.0;
            }
            return 0.5 * _r * _r * (_sweep - Math.Sin(_sweep));
        }

        /// <summary>
        /// Wire (arc-length) centroid. One signed-sweep formula for both
        /// orientations: <c>(r/δ) (sin a1 − sin a0, −cos a1 + cos a0)</c>.
        /// </summary>
        public Coordinate ArcLengthCentroid()
        {
            if (!_arc)
            {
                return new Coordinate(0.5 * (_start.X + _end.X), 0.5 * (_start.Y + _end.Y));
            }
            if (_sweep == 0.0)
            {
                return _start.Copy();
            }
            double signed = _ccw ? _sweep : -_sweep;
            double a1 = _a0 + signed;
            double k = _r / signed;
            return new Coordinate(
                _cx + k * (Math.Sin(a1) - Math.Sin(_a0)),
                _cy + k * (-Math.Cos(a1) + Math.Cos(_a0)));
        }

        internal static bool TryCircumcircle(Coordinate a, Coordinate b, Coordinate c,
            out double cx, out double cy, out double r)
        {
            cx = double.NaN;
            cy = double.NaN;
            r = 0.0;
            if (Orientation.Index(a, b, c) == OrientationIndex.Collinear)
            {
                return false;
            }
            double ax = a.X, ay = a.Y;
            double bx = b.X, by = b.Y;
            double cxp = c.X, cyp = c.Y;
            double d = 2.0 * (ax * (by - cyp) + bx * (cyp - ay) + cxp * (ay - by));
            if (d == 0.0)
            {
                return false;
            }
            double a2 = ax * ax + ay * ay;
            double b2 = bx * bx + by * by;
            double c2 = cxp * cxp + cyp * cyp;
            cx = (a2 * (by - cyp) + b2 * (cyp - ay) + c2 * (ay - by)) / d;
            cy = (a2 * (cxp - bx) + b2 * (ax - cxp) + c2 * (bx - ax)) / d;
            r = Math.Sqrt((ax - cx) * (ax - cx) + (ay - cy) * (ay - cy));
            return !double.IsNaN(r) && !double.IsInfinity(r) && r != 0.0;
        }

        internal static int SegmentCount(double radius, double sweep, double tolerance)
        {
            if (tolerance >= radius)
            {
                return 1;
            }
            double thetaMax = 2.0 * Math.Acos(1.0 - tolerance / radius);
            if (double.IsNaN(thetaMax) || double.IsInfinity(thetaMax) || thetaMax <= 0.0)
            {
                return 1;
            }
            int n = (int)Math.Ceiling(sweep / thetaMax);
            return n < 1 ? 1 : n;
        }

        private bool OnSweep(Coordinate p)
        {
            if (!_arc)
            {
                return false;
            }
            double travelled = AngleBetween.Travelled(_ccw,
                _start.X - _cx, _start.Y - _cy, p.X - _cx, p.Y - _cy);
            return travelled <= _sweep + Ulp(_sweep);
        }

        private static bool OnSegment(Coordinate p, Coordinate a, Coordinate b, double tol)
        {
            double dx = b.X - a.X;
            double dy = b.Y - a.Y;
            double len2 = dx * dx + dy * dy;
            if (len2 == 0.0)
            {
                return p.Distance(a) <= tol;
            }
            double t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2;
            if (t < 0.0)
            {
                t = 0.0;
            }
            else if (t > 1.0)
            {
                t = 1.0;
            }
            double px = a.X + t * dx - p.X;
            double py = a.Y + t * dy - p.Y;
            return px * px + py * py <= tol * tol;
        }

        internal static double Ulp(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return value;
            }
            double abs = Math.Abs(value);
            long bits = BitConverter.DoubleToInt64Bits(abs);
            double next = BitConverter.Int64BitsToDouble(bits + 1);
            return next - abs;
        }
    }
}
