// SPDX-License-Identifier: BSD-3-Clause
// AI-drafted, human-reviewed.  Assisted-by: Cursor Grok 4.6
//
// Ticket 9 — Year-1 ST_MultiSurface §4.2.27 typed members (ISO/IEC 13249-3).
// GetGeometryN / enumerator / copy / reverse expose ISurface (Polygon |
// CurvePolygon) and never downcast a CurvePolygon to Polygon or a ring to
// LinearRing (F-MS). Closes Year-1 WKB 12 "partial"; Year-2 surface types
// stay omitted. Does not remint WKT/WKB. CurvePolygon rings stay Curve
// (LS|CS|CC) — do not regress CP Ticket 3.

using System;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Curves;
using NetTopologySuite.IO;
using NUnit.Framework;
using Triangle = NetTopologySuite.Geometries.Curves.Triangle;

namespace NetTopologySuite.Tests.NUnit.Geometries.Curves
{
    /// <summary>
    /// Year-1 <c>MULTISURFACE</c> typed members (Ticket 9 / F-MS-MEM).
    /// </summary>
    [Category("CurveAwareness")]
    public class MultiSurfaceMembersYear1Test
    {
        private readonly GeometryFactory _factory = new GeometryFactory();

        private CircularString Arc(params (double x, double y)[] pts)
        {
            var coords = new Coordinate[pts.Length];
            for (int i = 0; i < pts.Length; i++) coords[i] = new Coordinate(pts[i].x, pts[i].y);
            return new CircularString(_factory.CoordinateSequenceFactory.Create(coords), _factory);
        }

        private LinearRing Ring(params (double x, double y)[] pts)
        {
            var coords = new Coordinate[pts.Length];
            for (int i = 0; i < pts.Length; i++) coords[i] = new Coordinate(pts[i].x, pts[i].y);
            return _factory.CreateLinearRing(coords);
        }

        private LineString Line(params (double x, double y)[] pts)
        {
            var coords = new Coordinate[pts.Length];
            for (int i = 0; i < pts.Length; i++) coords[i] = new Coordinate(pts[i].x, pts[i].y);
            return _factory.CreateLineString(coords);
        }

        private Polygon Square(double x0, double y0, double size)
        {
            double x1 = x0 + size;
            double y1 = y0 + size;
            return _factory.CreatePolygon(Ring((x0, y0), (x1, y0), (x1, y1), (x0, y1), (x0, y0)));
        }

        /// <summary>CurvePolygon with CS shell and CC hole (Year-1 rings stay Curve).</summary>
        private CurvePolygon CurvePolygonWithYear1Rings()
        {
            var hole = new CompoundCurve(
                new Curve[] { Arc((2, 0), (3, 1), (4, 0)), Line((4, 0), (2, 0)) },
                _factory);
            return new CurvePolygon(
                Arc((0, 0), (5, 5), (10, 0), (5, -5), (0, 0)),
                new Curve[] { hole },
                _factory);
        }

        /// <summary>Mixed Year-1 members: CurvePolygon then Polygon.</summary>
        private MultiSurface MixedCpPoly()
        {
            return new MultiSurface(
                new Geometry[] { CurvePolygonWithYear1Rings(), Square(20, 20, 10) },
                _factory);
        }

        // ============================================================
        // F-MS structural contract (§4.2.27 typed members)
        // ============================================================

        [Test]
        public void FMS_DOVE_GetGeometryN_is_typed_ISurface()
        {
            var getN = typeof(MultiSurface).GetMethod(nameof(MultiSurface.GetGeometryN),
                System.Reflection.BindingFlags.DeclaredOnly | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                null, new[] { typeof(int) }, null);
            Assert.That(getN, Is.Not.Null,
                "FMS-DOVE: MultiSurface must expose GetGeometryN (ST_GeometryN)");
            Assert.That(getN.ReturnType, Is.EqualTo(typeof(ISurface)),
                "FMS-DOVE: GetGeometryN must return ISurface, not Geometry or LinearRing");

            var indexer = typeof(MultiSurface).GetProperty("Item",
                System.Reflection.BindingFlags.DeclaredOnly | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            Assert.That(indexer, Is.Not.Null, "FMS-DOVE: MultiSurface must expose a typed indexer");
            Assert.That(indexer.PropertyType, Is.EqualTo(typeof(ISurface)),
                "FMS-DOVE: indexer must be typed as ISurface");
        }

        [Test]
        public void FMS_MEM_mixed_members_retain_subtypes()
        {
            var ms = MixedCpPoly();
            Assert.That(ms.NumGeometries, Is.EqualTo(2), "FMS-MEM: two members");
            Assert.That(ms.GetGeometryN(0), Is.InstanceOf<CurvePolygon>(),
                "FMS-MEM: member 0 should be a CurvePolygon, got "
                + ms.GetGeometryN(0).GetType().Name);
            Assert.That(ms.GetGeometryN(0), Is.Not.InstanceOf<Polygon>(),
                "FMS-MEM: CurvePolygon must not be downcast to Polygon");
            Assert.That(ms.GetGeometryN(1), Is.InstanceOf<Polygon>(),
                "FMS-MEM: member 1 should be a Polygon");
            Assert.That(ms.GetGeometryN(1), Is.Not.InstanceOf<CurvePolygon>());

            var cp = (CurvePolygon)ms.GetGeometryN(0);
            Assert.That(cp.ExteriorRing, Is.InstanceOf<CircularString>(),
                "FMS-MEM / F-CP: CS shell stays CircularString, not LinearRing");
            Assert.That(cp.GetInteriorRingN(0), Is.InstanceOf<CompoundCurve>(),
                "FMS-MEM / F-CP: CC hole stays CompoundCurve, not LinearRing");
        }

        [Test]
        public void FMS_MEM_enumerator_exposes_surface_subtypes()
        {
            var ms = MixedCpPoly();
            var expected = new[] { typeof(CurvePolygon), typeof(Polygon) };

            int i = 0;
            foreach (Geometry g in ms)
            {
                Assert.That(g, Is.InstanceOf<ISurface>(),
                    "FMS-MEM: GeometryCollection enumerator must yield ISurface, got "
                    + g.GetType().Name);
                Assert.That(g.GetType(), Is.EqualTo(expected[i]),
                    "FMS-MEM: enumerator must not downcast CurvePolygon to Polygon at " + i);
                Assert.That(g, Is.SameAs(ms.GetGeometryN(i)));
                i++;
            }
            Assert.That(i, Is.EqualTo(2));

            i = 0;
            foreach (ISurface s in ms.Surfaces)
            {
                Assert.That(s.GetType(), Is.EqualTo(expected[i]),
                    "FMS-MEM: Surfaces enumerator must keep subtype at " + i);
                Assert.That(s, Is.SameAs(ms[i]));
                i++;
            }
            Assert.That(i, Is.EqualTo(2));
        }

        [Test]
        public void FMS_CP_copy_preserves_member_subtypes_and_order()
        {
            var ms = MixedCpPoly();
            var copy = (MultiSurface)ms.Copy();

            Assert.That(copy.NumGeometries, Is.EqualTo(2));
            Assert.That(copy.GetGeometryN(0), Is.InstanceOf<CurvePolygon>(),
                "FMS-CP: copied member 0 must remain a CurvePolygon, got "
                + copy.GetGeometryN(0).GetType().Name);
            Assert.That(copy.GetGeometryN(1), Is.InstanceOf<Polygon>(),
                "FMS-CP: copied member 1 must remain a Polygon");
            Assert.That(copy.GetGeometryN(0), Is.Not.SameAs(ms.GetGeometryN(0)),
                "FMS-CP: copy must be a deep copy");
            var cp = (CurvePolygon)copy.GetGeometryN(0);
            Assert.That(cp.ExteriorRing, Is.InstanceOf<CircularString>(),
                "FMS-CP / F-CP: copied CS shell stays CircularString");
            Assert.That(cp.GetInteriorRingN(0), Is.InstanceOf<CompoundCurve>(),
                "FMS-CP / F-CP: copied CC hole stays CompoundCurve");
            Assert.That(copy.EqualsExact(ms), Is.True);
        }

        [Test]
        public void FMS_REV_reverse_preserves_member_subtypes_and_order()
        {
            var ms = MixedCpPoly();
            var rev = (MultiSurface)ms.Reverse();

            Assert.That(rev.NumGeometries, Is.EqualTo(2));
            Assert.That(rev.GetGeometryN(0), Is.InstanceOf<CurvePolygon>(),
                "FMS-REV: reversed member 0 stays CurvePolygon (collection order kept)");
            Assert.That(rev.GetGeometryN(1), Is.InstanceOf<Polygon>(),
                "FMS-REV: reversed member 1 stays Polygon");
            var cp = (CurvePolygon)rev.GetGeometryN(0);
            Assert.That(cp.ExteriorRing, Is.InstanceOf<CircularString>(),
                "FMS-REV / F-CP: reversed CS shell stays CircularString, not LinearRing");
            Assert.That(cp.GetInteriorRingN(0), Is.InstanceOf<CompoundCurve>(),
                "FMS-REV / F-CP: reversed CC hole stays CompoundCurve");
            Assert.That(((MultiSurface)rev.Reverse()).EqualsExact(ms), Is.True,
                "FMS-REV: double reverse is identity");
        }

        [Test]
        public void FMS_ENV_control_envelope_keeps_member_subtypes()
        {
            var ms = MixedCpPoly();
            Assert.That(() => ms.GetHashCode(), Throws.Nothing,
                "FMS-ENV: envelope-from-controls (GetHashCode) is identity-safe");
            Assert.That(ms.GetGeometryN(0), Is.InstanceOf<CurvePolygon>(),
                "FMS-ENV: control-envelope must not collapse the CurvePolygon");
            Assert.That(ms.GetGeometryN(1), Is.InstanceOf<Polygon>(),
                "FMS-ENV: control-envelope must not collapse the Polygon");
            var cp = (CurvePolygon)ms.GetGeometryN(0);
            Assert.That(cp.ExteriorRing, Is.InstanceOf<CircularString>(),
                "FMS-ENV / F-CP: control-envelope must not demote the CS shell");
        }

        // ============================================================
        // Construction (Ticket 7 Year-1 member lock) + EMPTY
        // ============================================================

        [Test]
        public void Ticket9_ConstructorAcceptsEmptyCollection()
        {
            var fromNull = new MultiSurface(null, _factory);
            var fromEmpty = new MultiSurface(Array.Empty<Geometry>(), _factory);
            Assert.That(fromNull.IsEmpty, Is.True);
            Assert.That(fromEmpty.IsEmpty, Is.True);
            Assert.That(fromNull.NumGeometries, Is.EqualTo(0));
            Assert.That(fromEmpty.NumGeometries, Is.EqualTo(0));
            Assert.That(fromNull.GeometryType, Is.EqualTo("MultiSurface"));
            Assert.That(fromNull.OgcGeometryType, Is.EqualTo(OgcGeometryType.MultiSurface));
        }

        [Test]
        public void Ticket9_EmptySafe_AccessorsDoNotThrowNre()
        {
            var empty = MultiSurface.Empty;
            Assert.That(empty.NumGeometries, Is.EqualTo(0));
            Assert.That(empty.IsEmpty, Is.True);
            Assert.That(empty.Length, Is.EqualTo(0d));
            Assert.That(empty.Area, Is.EqualTo(0d));
            Assert.That(() => { var _ = empty.EnvelopeInternal; }, Throws.Nothing);
            Assert.That(() => empty.GetHashCode(), Throws.Nothing);

            int n = 0;
            foreach (ISurface s in empty.Surfaces)
                n++;
            Assert.That(n, Is.EqualTo(0), "Ticket9 EMPTY: Surfaces enumerator is empty");

            n = 0;
            foreach (Geometry g in empty)
                n++;
            Assert.That(n, Is.EqualTo(0));

            Assert.That(() => empty.GetGeometryN(0), Throws.TypeOf<IndexOutOfRangeException>(),
                "Ticket9 EMPTY: GetGeometryN is index-bounded, not NRE");
        }

        [Test]
        public void Ticket9_ConstructorAcceptsYear1MemberTypes()
        {
            var poly = Square(0, 0, 10);
            var cp = CurvePolygonWithYear1Rings();
            var ms = new MultiSurface(new Geometry[] { poly, cp }, _factory);
            Assert.That(ms.GetGeometryN(0), Is.InstanceOf<Polygon>());
            Assert.That(ms.GetGeometryN(1), Is.InstanceOf<CurvePolygon>());
            Assert.That(((CurvePolygon)ms.GetGeometryN(1)).ExteriorRing, Is.InstanceOf<CircularString>());
        }

        [Test]
        public void Ticket9_ConstructorRejectsNullMember()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                new MultiSurface(new Geometry[] { Square(0, 0, 1), null }, _factory));
            Assert.That(ex.Message, Does.Contain("must not be null"));
        }

        [Test]
        public void Ticket9_ConstructorRejectsPoint()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                new MultiSurface(new Geometry[] { _factory.CreatePoint(new Coordinate(0, 0)) }, _factory));
            Assert.That(ex.Message, Does.Contain("must be surfaces").And.Contain("Point"));
            Assert.That(ex.Message, Does.Not.Contain("ISO forbids"));
        }

        [Test]
        public void Ticket9_ConstructorRejectsLineString()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                new MultiSurface(new Geometry[] { Line((0, 0), (1, 0)) }, _factory));
            Assert.That(ex.Message, Does.Contain("must be surfaces").And.Contain("LineString"));
            Assert.That(ex.Message, Does.Not.Contain("ISO forbids"));
        }

        [Test]
        public void Ticket9_ConstructorRejectsMultiPolygonAsMember()
        {
            var multi = _factory.CreateMultiPolygon(new[] { Square(0, 0, 1) });
            var ex = Assert.Throws<ArgumentException>(() =>
                new MultiSurface(new Geometry[] { multi }, _factory));
            Assert.That(ex.Message, Does.Contain("must be surfaces").And.Contain("MultiPolygon"));
            Assert.That(ex.Message, Does.Not.Contain("ISO forbids"));
        }

        [Test]
        public void Ticket9_ConstructorRejectsTriangle()
        {
            var triangle = new Triangle(Ring((0, 0), (10, 0), (5, 8), (0, 0)), _factory);
            var ex = Assert.Throws<ArgumentException>(() =>
                new MultiSurface(new Geometry[] { triangle }, _factory));
            Assert.That(ex.Message, Does.Contain("Year-1").And.Contain("Triangle"));
            Assert.That(ex.Message, Does.Not.Contain("ISO forbids"));
        }

        [Test]
        public void Ticket9_CurvePolygonRingsStayCurve_NotLinearRing()
        {
            var ms = MixedCpPoly();
            var cp = (CurvePolygon)ms.GetGeometryN(0);
            Assert.That(cp.ExteriorRing, Is.InstanceOf<CircularString>());
            Assert.That(cp.ExteriorRing, Is.Not.InstanceOf<LinearRing>(),
                "Ticket 3 lock: CurvePolygon shell is Curve, not LinearRing");
            Assert.That(cp.GetInteriorRingN(0), Is.InstanceOf<CompoundCurve>());
            Assert.That(cp.GetInteriorRingN(0), Is.Not.InstanceOf<LinearRing>());
        }

        [Test]
        public void Ticket9_WktRoundTripKeepsTypedMembers()
        {
            const string wkt =
                "MULTISURFACE (CURVEPOLYGON (CIRCULARSTRING (0 0, 2 2, 4 0, 2 -2, 0 0)), POLYGON ((10 10, 20 10, 20 20, 10 20, 10 10)))";
            var original = (MultiSurface)new WKTReader().Read(wkt);
            Assert.That(original.GetGeometryN(0), Is.InstanceOf<CurvePolygon>());
            Assert.That(original.GetGeometryN(1), Is.InstanceOf<Polygon>());
            Assert.That(((CurvePolygon)original.GetGeometryN(0)).ExteriorRing, Is.InstanceOf<CircularString>());

            var again = (MultiSurface)new WKTReader().Read(new WKTWriter().Write(original));
            Assert.That(again.GetGeometryN(0), Is.InstanceOf<CurvePolygon>());
            Assert.That(again.GetGeometryN(1), Is.InstanceOf<Polygon>());
            Assert.That(again.EqualsExact(original), Is.True);
        }

        [Test]
        public void Ticket9_WkbRoundTripKeepsTypedMembers()
        {
            const string wkt =
                "MULTISURFACE (CURVEPOLYGON (CIRCULARSTRING (0 0, 2 2, 4 0, 2 -2, 0 0)), POLYGON ((10 10, 20 10, 20 20, 10 20, 10 10)))";
            var original = (MultiSurface)new WKTReader().Read(wkt);
            var bytes = new WKBWriter().Write(original);
            var again = (MultiSurface)new WKBReader().Read(bytes);
            Assert.That(again.GetGeometryN(0), Is.InstanceOf<CurvePolygon>());
            Assert.That(again.GetGeometryN(1), Is.InstanceOf<Polygon>());
            Assert.That(((CurvePolygon)again.GetGeometryN(0)).ExteriorRing, Is.InstanceOf<CircularString>());
            Assert.That(again.EqualsExact(original), Is.True);
        }

        [Test]
        public void Ticket9_AreaAndEnvelopeStayFailClosed()
        {
            var ms = MixedCpPoly();
            Assert.That(() => { var _ = ms.Area; }, Throws.TypeOf<NotSupportedException>());
            Assert.That(() => { var _ = ms.Length; }, Throws.TypeOf<NotSupportedException>());
            Assert.That(() => ms.EnvelopeInternal, Throws.TypeOf<NotSupportedException>());
        }
    }
}
