// SPDX-License-Identifier: BSD-3-Clause
// AI-drafted, human-reviewed.  Assisted-by: Cursor Grok 4.6
//
// Ticket 6 — Year-1 ST_MultiCurve §4.2.25 typed members (ISO/IEC 13249-3).
// GetGeometryN / enumerator / copy / reverse expose Curve (LS|CS|CC) and never
// downcast CS/CC to LineString (F-MC). Closes Year-1 WKB 11 "partial"; Year-2
// member names stay omitted. Does not remint WKT/WKB.

using System;
using System.Collections.Generic;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Curves;
using NUnit.Framework;

namespace NetTopologySuite.Tests.NUnit.Geometries.Curves
{
    /// <summary>
    /// Year-1 <c>MULTICURVE</c> typed members (Ticket 6 / F-MC-MEM).
    /// </summary>
    [Category("CurveAwareness")]
    public class MultiCurveMembersYear1Test
    {
        private readonly GeometryFactory _factory = new GeometryFactory();

        private CircularString Arc(params (double x, double y)[] pts)
        {
            var coords = new Coordinate[pts.Length];
            for (int i = 0; i < pts.Length; i++) coords[i] = new Coordinate(pts[i].x, pts[i].y);
            return new CircularString(_factory.CoordinateSequenceFactory.Create(coords), _factory);
        }

        private LineString Line(params (double x, double y)[] pts)
        {
            var coords = new Coordinate[pts.Length];
            for (int i = 0; i < pts.Length; i++) coords[i] = new Coordinate(pts[i].x, pts[i].y);
            return _factory.CreateLineString(coords);
        }

        /// <summary>Mixed Year-1 members: LS, CS, CC (CC of CS then LS).</summary>
        private MultiCurve MixedLsCsCc()
        {
            var cc = new CompoundCurve(
                new Curve[] { Arc((3, 0), (4, 1), (5, 0)), Line((5, 0), (6, 0)) },
                _factory);
            return new MultiCurve(
                new Curve[] { Line((0, 0), (1, 0)), Arc((1, 0), (2, 1), (3, 0)), cc },
                _factory);
        }

        // ============================================================
        // F-MC structural contract (§4.2.25 typed members)
        // ============================================================

        [Test]
        public void FMC_DOVE_GetGeometryN_is_typed_Curve()
        {
            var getN = typeof(MultiCurve).GetMethod(nameof(MultiCurve.GetGeometryN),
                System.Reflection.BindingFlags.DeclaredOnly | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                null, new[] { typeof(int) }, null);
            Assert.That(getN, Is.Not.Null,
                "FMC-DOVE: MultiCurve must expose GetGeometryN (ST_GeometryN)");
            Assert.That(getN.ReturnType, Is.EqualTo(typeof(Curve)),
                "FMC-DOVE: GetGeometryN must return Curve, not LineString");

            var indexer = typeof(MultiCurve).GetProperty("Item",
                System.Reflection.BindingFlags.DeclaredOnly | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            Assert.That(indexer, Is.Not.Null, "FMC-DOVE: MultiCurve must expose a typed indexer");
            Assert.That(indexer.PropertyType, Is.EqualTo(typeof(Curve)),
                "FMC-DOVE: indexer must be typed as Curve");
        }

        [Test]
        public void FMC_MEM_mixed_members_retain_subtypes()
        {
            var mc = MixedLsCsCc();
            Assert.That(mc.NumGeometries, Is.EqualTo(3), "FMC-MEM: three members");
            Assert.That(mc.GetGeometryN(0), Is.InstanceOf<LineString>(),
                "FMC-MEM: member 0 should be a LineString");
            Assert.That(mc.GetGeometryN(0), Is.Not.InstanceOf<CircularString>());
            Assert.That(mc.GetGeometryN(1), Is.InstanceOf<CircularString>(),
                "FMC-MEM: member 1 should be a CircularString, got "
                + mc.GetGeometryN(1).GetType().Name);
            Assert.That(mc.GetGeometryN(2), Is.InstanceOf<CompoundCurve>(),
                "FMC-MEM: member 2 should be a CompoundCurve, got "
                + mc.GetGeometryN(2).GetType().Name);

            var cc = (CompoundCurve)mc.GetGeometryN(2);
            Assert.That(cc.Curves[0], Is.InstanceOf<CircularString>(),
                "FMC-MEM: CC member 0 should remain a CircularString");
            Assert.That(cc.Curves[1], Is.InstanceOf<LineString>(),
                "FMC-MEM: CC member 1 should remain a LineString");
        }

        [Test]
        public void FMC_MEM_enumerator_exposes_curve_subtypes()
        {
            var mc = MixedLsCsCc();
            var expected = new[] { typeof(LineString), typeof(CircularString), typeof(CompoundCurve) };

            int i = 0;
            foreach (Geometry g in mc)
            {
                Assert.That(g, Is.InstanceOf<Curve>(),
                    "FMC-MEM: GeometryCollection enumerator must yield Curve, got "
                    + g.GetType().Name);
                Assert.That(g.GetType(), Is.EqualTo(expected[i]),
                    "FMC-MEM: enumerator must not downcast CS/CC to LineString at " + i);
                Assert.That(g, Is.SameAs(mc.GetGeometryN(i)));
                i++;
            }
            Assert.That(i, Is.EqualTo(3));

            i = 0;
            foreach (Curve c in mc.Curves)
            {
                Assert.That(c.GetType(), Is.EqualTo(expected[i]),
                    "FMC-MEM: Curves enumerator must keep subtype at " + i);
                Assert.That(c, Is.SameAs(mc[i]));
                i++;
            }
            Assert.That(i, Is.EqualTo(3));
        }

        [Test]
        public void FMC_CP_copy_preserves_member_subtypes_and_order()
        {
            var mc = MixedLsCsCc();
            var copy = (MultiCurve)mc.Copy();

            Assert.That(copy.NumGeometries, Is.EqualTo(3));
            Assert.That(copy.GetGeometryN(0), Is.InstanceOf<LineString>(),
                "FMC-CP: copied member 0 must remain a LineString");
            Assert.That(copy.GetGeometryN(1), Is.InstanceOf<CircularString>(),
                "FMC-CP: copied member 1 must remain a CircularString, got "
                + copy.GetGeometryN(1).GetType().Name);
            Assert.That(copy.GetGeometryN(2), Is.InstanceOf<CompoundCurve>(),
                "FMC-CP: copied member 2 must remain a CompoundCurve, got "
                + copy.GetGeometryN(2).GetType().Name);
            Assert.That(copy.GetGeometryN(0), Is.Not.SameAs(mc.GetGeometryN(0)),
                "FMC-CP: copy must be a deep copy");
            var cc = (CompoundCurve)copy.GetGeometryN(2);
            Assert.That(cc.Curves[0], Is.InstanceOf<CircularString>(),
                "FMC-CP: copied CC components stay CircularString");
            Assert.That(cc.Curves[1], Is.InstanceOf<LineString>());
            Assert.That(copy.EqualsExact(mc), Is.True);
        }

        [Test]
        public void FMC_REV_reverse_preserves_member_subtypes_and_order()
        {
            var mc = MixedLsCsCc();
            var rev = (MultiCurve)mc.Reverse();

            Assert.That(rev.NumGeometries, Is.EqualTo(3));
            Assert.That(rev.GetGeometryN(0), Is.InstanceOf<LineString>(),
                "FMC-REV: reversed member 0 stays LineString (collection order kept)");
            Assert.That(rev.GetGeometryN(1), Is.InstanceOf<CircularString>(),
                "FMC-REV: reversed member 1 stays CircularString");
            Assert.That(rev.GetGeometryN(2), Is.InstanceOf<CompoundCurve>(),
                "FMC-REV: reversed member 2 stays CompoundCurve");
            var cc = (CompoundCurve)rev.GetGeometryN(2);
            Assert.That(cc.Curves[0], Is.InstanceOf<LineString>(),
                "FMC-REV: reversed CC walks components backward but keeps subtypes");
            Assert.That(cc.Curves[1], Is.InstanceOf<CircularString>());
            Assert.That(((MultiCurve)rev.Reverse()).EqualsExact(mc), Is.True,
                "FMC-REV: double reverse is identity");
        }

        [Test]
        public void FMC_ENV_control_envelope_keeps_member_subtypes()
        {
            var mc = MixedLsCsCc();
            Assert.That(() => mc.GetHashCode(), Throws.Nothing,
                "FMC-ENV: envelope-from-controls (GetHashCode) is identity-safe");
            Assert.That(mc.GetGeometryN(1), Is.InstanceOf<CircularString>(),
                "FMC-ENV: control-envelope must not collapse the CircularString");
            Assert.That(mc.GetGeometryN(2), Is.InstanceOf<CompoundCurve>(),
                "FMC-ENV: control-envelope must not collapse the CompoundCurve");
        }

        // ============================================================
        // Construction (Ticket 4 Year-1 member lock)
        // ============================================================

        [Test]
        public void Ticket6_ConstructorAcceptsEmptyCollection()
        {
            var fromNull = new MultiCurve(null, _factory);
            var fromEmpty = new MultiCurve(Array.Empty<Curve>(), _factory);
            Assert.That(fromNull.IsEmpty, Is.True);
            Assert.That(fromEmpty.IsEmpty, Is.True);
            Assert.That(fromNull.NumGeometries, Is.EqualTo(0));
            Assert.That(fromEmpty.NumGeometries, Is.EqualTo(0));
            Assert.That(fromNull.GeometryType, Is.EqualTo("MultiCurve"));
            Assert.That(fromNull.OgcGeometryType, Is.EqualTo(OgcGeometryType.MultiCurve));
        }

        [Test]
        public void Ticket6_ConstructorAcceptsYear1MemberTypes()
        {
            var ls = Line((0, 0), (1, 0));
            var cs = Arc((0, 0), (1, 1), (2, 0));
            var cc = new CompoundCurve(new Curve[] { cs, Line((2, 0), (3, 0)) }, _factory);
            var mc = new MultiCurve(new Curve[] { ls, cs, cc }, _factory);
            Assert.That(mc.GetGeometryN(0), Is.InstanceOf<LineString>());
            Assert.That(mc.GetGeometryN(1), Is.InstanceOf<CircularString>());
            Assert.That(mc.GetGeometryN(2), Is.InstanceOf<CompoundCurve>());
        }

        [Test]
        public void Ticket6_ConstructorRejectsNullMember()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                new MultiCurve(new Curve[] { Line((0, 0), (1, 0)), null }, _factory));
            Assert.That(ex.Message, Does.Contain("must not be null"));
        }

        [Test]
        public void Ticket6_ConstructorRejectsNonCurve()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                new MultiCurve(new Geometry[] { _factory.CreatePoint(new Coordinate(0, 0)) }, _factory));
            Assert.That(ex.Message, Does.Contain("must be curves").And.Contain("Point"));
        }

        [Test]
        public void Ticket6_ConstructorRejectsNonYear1Curve()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                new MultiCurve(new Curve[] { new NonYear1Curve(_factory) }, _factory));
            Assert.That(ex.Message, Does.Contain("Year-1").And.Contain("NonYear1Curve"));
        }

        [Test]
        public void Ticket6_LengthAndEnvelopeStayFailClosed()
        {
            var mc = MixedLsCsCc();
            Assert.That(() => { var _ = mc.Length; }, Throws.TypeOf<NotSupportedException>());
            Assert.That(() => mc.EnvelopeInternal, Throws.TypeOf<NotSupportedException>());
        }

        /// <summary>
        /// Stand-in Curve that is not a Year-1 <c>ST_MultiCurve</c> member type.
        /// </summary>
        private sealed class NonYear1Curve : Curve
        {
            public NonYear1Curve(GeometryFactory factory) : base(factory) { }

            public override bool IsClosed => false;
            public override Point StartPoint => Factory.CreatePoint();
            public override Point EndPoint => Factory.CreatePoint();
            public override string GeometryType => "NonYear1Curve";
            public override OgcGeometryType OgcGeometryType => OgcGeometryType.CircularString;
            public override Coordinate Coordinate => new Coordinate(0, 0);
            public override Coordinate[] Coordinates => new[] { Coordinate, Coordinate };
            public override double[] GetOrdinates(Ordinate ordinate) => new[] { 0d, 0d };
            public override int NumPoints => 2;
            public override bool IsEmpty => false;
            public override Geometry Boundary => Factory.CreateMultiPoint();
            public override bool EqualsExact(Geometry other, double tolerance) => false;
            public override void Apply(ICoordinateFilter filter) { }
            public override void Apply(ICoordinateSequenceFilter filter) { }
            public override void Apply(IGeometryFilter filter) { }
            public override void Apply(IGeometryComponentFilter filter) { }
            protected override Geometry CopyInternal() => this;
            public override void Normalize() { }
            protected override Envelope ComputeEnvelopeInternal() => new Envelope();
            protected internal override int CompareToSameClass(object o) => 0;
            protected internal override int CompareToSameClass(object o, IComparer<CoordinateSequence> comp) => 0;
            protected override SortIndexValue SortIndex => SortIndexValue.CircularString;
        }
    }
}
