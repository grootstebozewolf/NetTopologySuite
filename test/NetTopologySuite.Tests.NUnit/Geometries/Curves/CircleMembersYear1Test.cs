// SPDX-License-Identifier: BSD-3-Clause
// AI-drafted, human-reviewed.  Assisted-by: Cursor Grok 4.6
//
// Ticket 21 — Year-1 ST_Circle typed members (ISO/IEC 13249-3 §4.2.7).
// GetControlN / GetControlPointN / Centre / Radius expose the three
// circumference controls and the planar circumcircle — not a coordinate
// dump and never a CircularString flatten. Copy / Reverse stay Circle.
// Closes Year-1 CIRCLE "partial"; named Year-2 curves stay omitted.
// Does not remint Ticket 19 WKT or Ticket 20 WKB.

using System;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Curves;
using NetTopologySuite.IO;
using NUnit.Framework;

namespace NetTopologySuite.Tests.NUnit.Geometries.Curves
{
    /// <summary>
    /// Year-1 <c>CIRCLE</c> typed members (Ticket 21).
    /// </summary>
    [Category("CurveAwareness")]
    public class CircleMembersYear1Test
    {
        private readonly GeometryFactory _factory = new GeometryFactory();
        private readonly WKTReader _reader = new WKTReader();
        private readonly WKTWriter _writer = new WKTWriter();
        private readonly WKBReader _wkbReader = new WKBReader();
        private readonly WKBWriter _wkbWriter = new WKBWriter();

        /// <summary>Unit circle through (1,0), (0,1), (-1,0).</summary>
        private const string UnitCircleWkt = "CIRCLE (1 0, 0 1, -1 0)";

        private Circle UnitCircle() => (Circle)_reader.Read(UnitCircleWkt);

        private Circle EmptyCircle() => (Circle)_reader.Read("CIRCLE EMPTY");

        // ============================================================
        // Typed control + circumcircle contract
        // ============================================================

        [Test]
        public void Ticket21_GetControlN_exposes_three_circumference_points()
        {
            var circle = UnitCircle();
            Assert.That(circle.NumPoints, Is.EqualTo(3));
            Assert.That(circle.GetControlN(0), Is.EqualTo(new Coordinate(1, 0)));
            Assert.That(circle.GetControlN(1), Is.EqualTo(new Coordinate(0, 1)));
            Assert.That(circle.GetControlN(2), Is.EqualTo(new Coordinate(-1, 0)));
            Assert.That(circle, Is.InstanceOf<Circle>());
            Assert.That(circle, Is.Not.InstanceOf<CircularString>(),
                "Ticket21: GetControlN must not flatten Circle to CircularString");
        }

        [Test]
        public void Ticket21_GetControlPointN_keeps_Point_identity_not_flatten()
        {
            var circle = UnitCircle();
            var p0 = circle.GetControlPointN(0);
            var p1 = circle.GetControlPointN(1);
            var p2 = circle.GetControlPointN(2);

            Assert.That(p0, Is.InstanceOf<Point>());
            Assert.That(p1, Is.InstanceOf<Point>());
            Assert.That(p2, Is.InstanceOf<Point>());
            Assert.That(p0.Coordinate, Is.EqualTo(new Coordinate(1, 0)));
            Assert.That(p1.Coordinate, Is.EqualTo(new Coordinate(0, 1)));
            Assert.That(p2.Coordinate, Is.EqualTo(new Coordinate(-1, 0)));
            Assert.That(p0.Factory, Is.SameAs(circle.Factory));
            Assert.That(circle.GeometryType, Is.EqualTo("Circle"));
            Assert.That(circle, Is.Not.InstanceOf<CircularString>());
        }

        [Test]
        public void Ticket21_Centre_and_Radius_are_planar_circumcircle()
        {
            var circle = UnitCircle();
            Assert.That(circle.Centre.X, Is.EqualTo(0d).Within(1e-12));
            Assert.That(circle.Centre.Y, Is.EqualTo(0d).Within(1e-12));
            Assert.That(circle.Radius, Is.EqualTo(1d).Within(1e-12));
            Assert.That(circle.Length, Is.EqualTo(2d * Math.PI * circle.Radius).Within(1e-12));
            Assert.That(circle.GetControlN(0).Distance(circle.Centre),
                Is.EqualTo(circle.Radius).Within(1e-12));
            Assert.That(circle.GetControlN(1).Distance(circle.Centre),
                Is.EqualTo(circle.Radius).Within(1e-12));
            Assert.That(circle.GetControlN(2).Distance(circle.Centre),
                Is.EqualTo(circle.Radius).Within(1e-12));
        }

        [Test]
        public void Ticket21_ZmHonesty_Centre_has_no_invented_3d()
        {
            // CircularString locus honesty: metrics are planar XY. A 3D
            // centre is not defined, even when every control shares a Z.
            var equalZ = (Circle)_reader.Read("CIRCLE Z (1 0 5, 0 1 5, -1 0 5)");
            Assert.That(equalZ.GetControlN(0).Z, Is.EqualTo(5));
            Assert.That(equalZ.GetControlN(1).Z, Is.EqualTo(5));
            Assert.That(equalZ.GetControlN(2).Z, Is.EqualTo(5));
            Assert.That(equalZ.Centre.X, Is.EqualTo(0d).Within(1e-12));
            Assert.That(equalZ.Centre.Y, Is.EqualTo(0d).Within(1e-12));
            Assert.That(double.IsNaN(equalZ.Centre.Z), Is.True,
                "Ticket21: do not invent a 3D centre; Z is NullOrdinate");
            Assert.That(equalZ.Radius, Is.EqualTo(1d).Within(1e-12));

            var mixedZ = (Circle)_reader.Read("CIRCLE Z (1 0 5, 0 1 9, -1 0 1)");
            Assert.That(mixedZ.GetControlN(1).Z, Is.EqualTo(9));
            Assert.That(mixedZ.Centre.X, Is.EqualTo(0d).Within(1e-12));
            Assert.That(mixedZ.Centre.Y, Is.EqualTo(0d).Within(1e-12));
            Assert.That(double.IsNaN(mixedZ.Centre.Z), Is.True);
            Assert.That(mixedZ.Radius, Is.EqualTo(1d).Within(1e-12));
        }

        [Test]
        public void Ticket21_GetControlN_out_of_range()
        {
            var circle = UnitCircle();
            Assert.That(() => circle.GetControlN(-1), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => circle.GetControlN(3), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => circle.GetControlPointN(-1), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => circle.GetControlPointN(3), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        // ============================================================
        // EMPTY: NumPoints 0; centre/radius throw; no NRE
        // ============================================================

        [Test]
        public void Ticket21_Empty_NumPoints_zero_Centre_Radius_throw()
        {
            var empty = EmptyCircle();
            Assert.That(empty.IsEmpty, Is.True);
            Assert.That(empty.NumPoints, Is.EqualTo(0));
            Assert.That(empty.GeometryType, Is.EqualTo("Circle"));

            var centreEx = Assert.Throws<InvalidOperationException>(() => { var _ = empty.Centre; });
            Assert.That(centreEx.Message, Does.Contain("EMPTY"));
            var radiusEx = Assert.Throws<InvalidOperationException>(() => { var _ = empty.Radius; });
            Assert.That(radiusEx.Message, Does.Contain("EMPTY"));
        }

        [Test]
        public void Ticket21_EmptySafe_AccessorsDoNotThrowNre()
        {
            var empty = EmptyCircle();
            Assert.That(empty.NumPoints, Is.EqualTo(0));
            Assert.That(empty.IsEmpty, Is.True);
            Assert.That(empty.Length, Is.EqualTo(0d));
            Assert.That(empty.Coordinate, Is.Null);
            Assert.That(empty.StartPoint, Is.Null);
            Assert.That(empty.EndPoint, Is.Null);
            Assert.That(() => { var _ = empty.EnvelopeInternal; }, Throws.Nothing);
            Assert.That(() => empty.GetHashCode(), Throws.Nothing);
            Assert.That(() => empty.Copy(), Throws.Nothing);
            Assert.That(() => empty.Reverse(), Throws.Nothing);

            Assert.That(() => empty.GetControlN(0), Throws.TypeOf<ArgumentOutOfRangeException>(),
                "Ticket21 EMPTY: GetControlN is index-bounded, not NRE");
            Assert.That(() => empty.GetControlPointN(0), Throws.TypeOf<ArgumentOutOfRangeException>(),
                "Ticket21 EMPTY: GetControlPointN is index-bounded, not NRE");
            Assert.That(() => { var _ = empty.Centre; }, Throws.TypeOf<InvalidOperationException>(),
                "Ticket21 EMPTY: Centre throws, not NRE");
            Assert.That(() => { var _ = empty.Radius; }, Throws.TypeOf<InvalidOperationException>(),
                "Ticket21 EMPTY: Radius throws, not NRE");
        }

        // ============================================================
        // Copy / Reverse stay Circle (no flatten)
        // ============================================================

        [Test]
        public void Ticket21_Copy_preserves_Circle_and_three_controls()
        {
            var circle = UnitCircle();
            var copy = (Circle)circle.Copy();

            Assert.That(copy, Is.InstanceOf<Circle>(),
                "Ticket21: Copy must stay Circle, not flatten to CircularString");
            Assert.That(copy, Is.Not.InstanceOf<CircularString>());
            Assert.That(copy.NumPoints, Is.EqualTo(3));
            Assert.That(copy.GetControlN(0), Is.EqualTo(circle.GetControlN(0)));
            Assert.That(copy.GetControlN(1), Is.EqualTo(circle.GetControlN(1)));
            Assert.That(copy.GetControlN(2), Is.EqualTo(circle.GetControlN(2)));
            Assert.That(copy.Centre.Distance(circle.Centre), Is.EqualTo(0d).Within(1e-12));
            Assert.That(copy.Radius, Is.EqualTo(circle.Radius).Within(1e-12));
            Assert.That(copy, Is.Not.SameAs(circle));
            Assert.That(copy.EqualsExact(circle), Is.True);

            var emptyCopy = (Circle)EmptyCircle().Copy();
            Assert.That(emptyCopy, Is.InstanceOf<Circle>());
            Assert.That(emptyCopy.IsEmpty, Is.True);
            Assert.That(emptyCopy.NumPoints, Is.EqualTo(0));
        }

        [Test]
        public void Ticket21_Reverse_preserves_Circle_and_reverses_controls()
        {
            var circle = UnitCircle();
            var rev = (Circle)circle.Reverse();

            Assert.That(rev, Is.InstanceOf<Circle>(),
                "Ticket21: Reverse must stay Circle (not CircularString flatten)");
            Assert.That(rev, Is.Not.InstanceOf<CircularString>());
            Assert.That(rev.NumPoints, Is.EqualTo(3));
            Assert.That(rev.GetControlN(0), Is.EqualTo(circle.GetControlN(2)),
                "Ticket21: Reverse is P3, P2, P1");
            Assert.That(rev.GetControlN(1), Is.EqualTo(circle.GetControlN(1)));
            Assert.That(rev.GetControlN(2), Is.EqualTo(circle.GetControlN(0)));
            Assert.That(rev.Centre.Distance(circle.Centre), Is.EqualTo(0d).Within(1e-12));
            Assert.That(rev.Radius, Is.EqualTo(circle.Radius).Within(1e-12));
            Assert.That(((Circle)rev.Reverse()).EqualsExact(circle), Is.True,
                "Ticket21: double reverse is identity");

            var emptyRev = (Circle)EmptyCircle().Reverse();
            Assert.That(emptyRev, Is.InstanceOf<Circle>());
            Assert.That(emptyRev.IsEmpty, Is.True);
        }

        [Test]
        public void Ticket21_ConstructorStillRejectsCollinear()
        {
            var seq = _factory.CoordinateSequenceFactory.Create(new[]
            {
                new Coordinate(0, 0),
                new Coordinate(1, 0),
                new Coordinate(2, 0)
            });
            var ex = Assert.Throws<ArgumentException>(() => new Circle(seq, _factory));
            Assert.That(ex.Message, Does.Contain("collinear").IgnoreCase);
        }

        [Test]
        public void Ticket21_WktWkb_roundtrip_still_green_via_existing_path()
        {
            // Optional witness: Tickets 19–20 stay green; this ticket does not remint them.
            var original = UnitCircle();
            Assert.That(_writer.Write(original), Is.EqualTo(UnitCircleWkt));

            var fromWkt = (Circle)_reader.Read(original.AsText());
            Assert.That(fromWkt.GetControlN(0), Is.EqualTo(original.GetControlN(0)));
            Assert.That(fromWkt.Centre.Distance(original.Centre), Is.EqualTo(0d).Within(1e-12));

            var again = (Circle)_wkbReader.Read(_wkbWriter.Write(original));
            Assert.That(again, Is.InstanceOf<Circle>());
            Assert.That(again.NumPoints, Is.EqualTo(3));
            Assert.That(again.EqualsExact(original), Is.True);
            Assert.That(again.GetControlN(2), Is.EqualTo(original.GetControlN(2)));
            Assert.That(again.Radius, Is.EqualTo(1d).Within(1e-12));
        }

        [Test]
        public void Ticket21_Year2CurvesStillRefused()
        {
            var leftover = Assert.Throws<ParseException>(() =>
                _reader.Read("GEODESICSTRING (0 0, 10 0, 10 10)"));
            Assert.That(leftover.Message, Does.Contain("not implemented"));
        }
    }
}
