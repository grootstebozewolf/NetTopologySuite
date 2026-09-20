// SPDX-License-Identifier: BSD-3-Clause
// AI-drafted, human-reviewed.  Assisted-by: Cursor Grok 4.6
//
// Ticket 18 — Year-1 ST_TIN typed patches (ISO/IEC 13249-3).
// GetGeometryN / enumerator / copy / reverse expose Triangle (Surface<LinearRing>),
// not Polygon — the existing Triangle egg is kept. Polygon-shaped access is
// ExteriorRing on the stored patch (no coordinate dump). Closes Year-1 WKB 16
// "partial"; named fields (PATCHES / ELEMENTS / MAXSIDELENGTH) stay omitted.
// Does not remint Ticket 16 WKT or Ticket 17 WKB. No PATCHES object-model API.

using System;
using System.Linq;
using System.Reflection;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Curves;
using NetTopologySuite.IO;
using NUnit.Framework;
using Triangle = NetTopologySuite.Geometries.Curves.Triangle;

namespace NetTopologySuite.Tests.NUnit.Geometries.Curves
{
    /// <summary>
    /// Year-1 <c>TIN</c> typed patches (Ticket 18).
    /// </summary>
    [Category("CurveAwareness")]
    public class TinMembersYear1Test
    {
        private readonly GeometryFactory _factory = new GeometryFactory();

        private LinearRing Ring(params (double x, double y)[] pts)
        {
            var coords = new Coordinate[pts.Length];
            for (int i = 0; i < pts.Length; i++) coords[i] = new Coordinate(pts[i].x, pts[i].y);
            return _factory.CreateLinearRing(coords);
        }

        private Triangle Patch(params (double x, double y)[] pts) =>
            new Triangle(Ring(pts), _factory);

        /// <summary>Two Year-1 triangular patches.</summary>
        private Tin TwoPatches()
        {
            return new Tin(new[]
            {
                Patch((0, 0), (1, 0), (0, 1), (0, 0)),
                Patch((1, 0), (1, 1), (0, 1), (1, 0)),
            }, _factory);
        }

        private static void AssertClosedTriangularPatch(Geometry g, string label)
        {
            Assert.That(g, Is.InstanceOf<Triangle>(),
                label + ": Year-1 TIN patch is Triangle-as-surface, got " + g.GetType().Name);
            Assert.That(g, Is.Not.InstanceOf<Polygon>(),
                label + ": Triangle is not Polygon; the egg is kept (quoted gap vs 'yield Polygon')");
            Assert.That(g, Is.InstanceOf<ISurface>(),
                label + ": Triangle is a surface");
            var patch = (Triangle)g;
            Assert.That(patch.ExteriorRing, Is.Not.Null,
                label + ": polygon-shaped access is ExteriorRing, not a coordinate dump");
            Assert.That(patch.ExteriorRing, Is.InstanceOf<LinearRing>());
            Assert.That(patch.ExteriorRing.IsClosed, Is.True,
                label + ": ExteriorRing is closed");
            Assert.That(patch.ExteriorRing.NumPoints, Is.EqualTo(4),
                label + ": closed triangular patch has 4 coordinates");
            Assert.That(patch.NumInteriorRings, Is.EqualTo(0));
        }

        // ============================================================
        // Typed patch contract (Triangle-as-surface, not Polygon)
        // ============================================================

        [Test]
        public void Ticket18_GetGeometryN_is_typed_Triangle()
        {
            var getN = typeof(Tin).GetMethod(nameof(Tin.GetGeometryN),
                BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(int) }, null);
            Assert.That(getN, Is.Not.Null,
                "Ticket18: Tin must expose GetGeometryN (ST_GeometryN)");
            Assert.That(getN.ReturnType, Is.EqualTo(typeof(Triangle)),
                "Ticket18: GetGeometryN must return Triangle, not Polygon (egg kept)");

            var indexer = typeof(Tin).GetProperty("Item",
                BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance);
            Assert.That(indexer, Is.Not.Null, "Ticket18: Tin must expose a typed indexer");
            Assert.That(indexer.PropertyType, Is.EqualTo(typeof(Triangle)),
                "Ticket18: indexer must be typed as Triangle");
        }

        [Test]
        public void Ticket18_GetGeometryN_yields_closed_triangular_patch_with_ExteriorRing()
        {
            var tin = TwoPatches();
            Assert.That(tin.NumGeometries, Is.EqualTo(2));
            AssertClosedTriangularPatch(tin.GetGeometryN(0), "Ticket18 GetGeometryN(0)");
            AssertClosedTriangularPatch(tin.GetGeometryN(1), "Ticket18 GetGeometryN(1)");
            Assert.That(tin.GetGeometryN(0), Is.SameAs(tin.GetTriangleN(0)),
                "Ticket18: GetTriangleN is the typed alias, not a flatten");
            Assert.That(tin.GetGeometryN(0), Is.SameAs(tin[0]));
        }

        [Test]
        public void Ticket18_enumerator_exposes_Triangle_not_Polygon()
        {
            var tin = TwoPatches();
            var expected = new[] { typeof(Triangle), typeof(Triangle) };

            int i = 0;
            foreach (Geometry g in tin)
            {
                AssertClosedTriangularPatch(g, "Ticket18 enumerator " + i);
                Assert.That(g.GetType(), Is.EqualTo(expected[i]));
                Assert.That(g, Is.SameAs(tin.GetGeometryN(i)));
                i++;
            }
            Assert.That(i, Is.EqualTo(2));

            i = 0;
            foreach (Triangle t in tin.Triangles)
            {
                AssertClosedTriangularPatch(t, "Ticket18 Triangles " + i);
                Assert.That(t, Is.SameAs(tin[i]));
                i++;
            }
            Assert.That(i, Is.EqualTo(2));
        }

        [Test]
        public void Ticket18_copy_preserves_triangle_members()
        {
            var tin = TwoPatches();
            var copy = (Tin)tin.Copy();

            Assert.That(copy, Is.InstanceOf<Tin>(),
                "Ticket18: Copy must stay Tin, not flatten to GeometryCollection");
            Assert.That(copy.NumGeometries, Is.EqualTo(2));
            AssertClosedTriangularPatch(copy.GetGeometryN(0), "Ticket18 Copy(0)");
            AssertClosedTriangularPatch(copy.GetGeometryN(1), "Ticket18 Copy(1)");
            Assert.That(copy.GetGeometryN(0), Is.Not.SameAs(tin.GetGeometryN(0)),
                "Ticket18: copy must be a deep copy");
            Assert.That(copy.GetTriangleN(0).EqualsExact(tin.GetTriangleN(0)), Is.True);
            Assert.That(copy.EqualsExact(tin), Is.True);
        }

        [Test]
        public void Ticket18_reverse_preserves_triangle_members()
        {
            var tin = TwoPatches();
            var rev = (Tin)tin.Reverse();

            Assert.That(rev, Is.InstanceOf<Tin>(),
                "Ticket18: Reverse must stay Tin (not GeometryCollection flatten)");
            Assert.That(rev.NumGeometries, Is.EqualTo(2));
            AssertClosedTriangularPatch(rev.GetGeometryN(0), "Ticket18 Reverse(0)");
            AssertClosedTriangularPatch(rev.GetGeometryN(1), "Ticket18 Reverse(1)");
            Assert.That(rev.GetTriangleN(0).SignedDoubleArea,
                Is.EqualTo(-tin.GetTriangleN(0).SignedDoubleArea).Within(1e-9),
                "Ticket18: Reverse flips each patch, collection order kept");
            Assert.That(((Tin)rev.Reverse()).EqualsExact(tin), Is.True,
                "Ticket18: double reverse is identity");
        }

        // ============================================================
        // Construction + EMPTY + no PATCHES API
        // ============================================================

        [Test]
        public void Ticket18_ConstructorAcceptsEmptyCollection()
        {
            var fromNull = new Tin(null, _factory);
            var fromEmpty = new Tin(Array.Empty<Triangle>(), _factory);
            Assert.That(fromNull.IsEmpty, Is.True);
            Assert.That(fromEmpty.IsEmpty, Is.True);
            Assert.That(fromNull.NumGeometries, Is.EqualTo(0));
            Assert.That(fromEmpty.NumGeometries, Is.EqualTo(0));
            Assert.That(fromNull.GeometryType, Is.EqualTo("TIN"));
            Assert.That(fromNull.OgcGeometryType, Is.EqualTo(OgcGeometryType.TIN));
            Assert.That(Tin.Empty.IsEmpty, Is.True);
            Assert.That(Tin.Empty.NumGeometries, Is.EqualTo(0));
        }

        [Test]
        public void Ticket18_EmptySafe_AccessorsDoNotThrowNre()
        {
            var empty = Tin.Empty;
            Assert.That(empty.NumGeometries, Is.EqualTo(0));
            Assert.That(empty.IsEmpty, Is.True);
            Assert.That(empty.Area, Is.EqualTo(0d));
            Assert.That(() => { var _ = empty.EnvelopeInternal; }, Throws.Nothing);
            Assert.That(() => empty.GetHashCode(), Throws.Nothing);

            int n = 0;
            foreach (Triangle t in empty.Triangles)
                n++;
            Assert.That(n, Is.EqualTo(0), "Ticket18 EMPTY: Triangles enumerator is empty");

            n = 0;
            foreach (Geometry g in empty)
                n++;
            Assert.That(n, Is.EqualTo(0));

            Assert.That(() => empty.GetGeometryN(0), Throws.TypeOf<IndexOutOfRangeException>(),
                "Ticket18 EMPTY: GetGeometryN is index-bounded, not NRE");
        }

        [Test]
        public void Ticket18_ConstructorRejectsNullMember()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                new Tin(new Triangle[] { Patch((0, 0), (1, 0), (0, 1), (0, 0)), null }, _factory));
            Assert.That(ex.Message, Does.Contain("must not be null"));
        }

        [Test]
        public void Ticket18_NoPatchesApiOnObjectModel()
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static
                                       | BindingFlags.DeclaredOnly;
            var names = typeof(Tin).GetMembers(flags).Select(m => m.Name).ToArray();
            Assert.That(names.Any(n => n.Equals("Patches", StringComparison.OrdinalIgnoreCase)
                                       || n.Equals("Patch", StringComparison.OrdinalIgnoreCase)),
                Is.False,
                "Ticket18: do not invent PATCHES on the Tin object model; members were "
                + string.Join(", ", names));
            Assert.That(typeof(Tin).GetProperty("Patches",
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.IgnoreCase),
                Is.Null);
        }

        [Test]
        public void Ticket18_Wkb16_roundtrip_still_green_via_existing_Ticket17_path()
        {
            // Optional witness: Ticket 17 WKB 16 stays green; this ticket does not remint it.
            const string wkt = "TIN (((0 0, 1 0, 0 1, 0 0)), ((1 0, 1 1, 0 1, 1 0)))";
            var original = (Tin)new WKTReader().Read(wkt);
            AssertClosedTriangularPatch(original.GetGeometryN(0), "Ticket18 WKB witness original");

            var again = (Tin)new WKBReader().Read(new WKBWriter().Write(original));
            Assert.That(again.NumGeometries, Is.EqualTo(2));
            AssertClosedTriangularPatch(again.GetGeometryN(0), "Ticket18 WKB witness 0");
            AssertClosedTriangularPatch(again.GetGeometryN(1), "Ticket18 WKB witness 1");
            Assert.That(again.EqualsExact(original), Is.True);
        }
    }
}
