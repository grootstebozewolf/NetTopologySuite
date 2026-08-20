// SPDX-License-Identifier: BSD-3-Clause
//
// AI assistance disclosure:
//   AI-drafted, human-reviewed. Assisted-by: Cursor Grok 4.6
//   AI-generated portions dedicated to CC0-1.0; human curation BSD-3-Clause.

using System;
using System.Diagnostics;
using NetTopologySuite.Algorithm.ExactCurve;
using NetTopologySuite.Geometries;
using NUnit.Framework;

namespace NetTopologySuite.Tests.NUnit.Algorithm.ExactCurve
{
    /// <summary>
    /// Closed-form cells for ExactCircularArc. Witness: semicircle length is
    /// <c>5π</c>; L1/L2 hard 0; P1 length ≤ 1.15× densify.
    /// Port of JTS <c>9797c2c4</c>.
    /// </summary>
    public class ExactCircularArcTest
    {
        private const int GateTrials = 20_000;
        private const int P1Samples = 8_000;
        private const long Seed = unchecked((long)0xa7ea0001);
        private const int NChord = 64;

        [Test]
        public void SemicircleLengthIsFivePi()
        {
            var a = new ExactCircularArc(
                new Coordinate(5, 0), new Coordinate(0, 5), new Coordinate(-5, 0));
            Assert.That(a.IsArc, Is.True);
            Assert.That(a.Radius, Is.EqualTo(5.0).Within(1.0e-12));
            Assert.That(a.Sweep, Is.EqualTo(Math.PI).Within(1.0e-12));
            Assert.That(a.Length, Is.EqualTo(5.0 * Math.PI).Within(1.0e-12));
            Assert.That(a.ChordLeArc(), Is.True);
            Assert.That(a.ChordLength, Is.EqualTo(10.0).Within(1.0e-12));
        }

        [Test]
        public void FullCircleTwoWindows()
        {
            var a = new ExactCircularArc(
                new Coordinate(5, 0), new Coordinate(0, 5), new Coordinate(-5, 0));
            var b = new ExactCircularArc(
                new Coordinate(-5, 0), new Coordinate(0, -5), new Coordinate(5, 0));
            Assert.That(a.Length + b.Length, Is.EqualTo(2.0 * Math.PI * 5.0).Within(1.0e-12));
        }

        [Test]
        public void ColinearIsChord()
        {
            var a = new ExactCircularArc(
                new Coordinate(0, 0), new Coordinate(1, 0), new Coordinate(3, 0));
            Assert.That(a.IsArc, Is.False);
            Assert.That(a.Length, Is.EqualTo(3.0).Within(1.0e-12));
            Assert.That(a.Length, Is.EqualTo(a.ChordLength));
            Assert.That(a.ChordLeArc(), Is.True);
            Assert.That(a.CircularSegmentArea(), Is.EqualTo(0.0));
        }

        [Test]
        public void InArcEndsAndMid()
        {
            var a = new ExactCircularArc(
                new Coordinate(5, 0), new Coordinate(0, 5), new Coordinate(-5, 0));
            Assert.That(a.InArc(new Coordinate(5, 0), 1.0e-9), Is.True);
            Assert.That(a.InArc(new Coordinate(0, 5), 1.0e-9), Is.True);
            Assert.That(a.InArc(new Coordinate(-5, 0), 1.0e-9), Is.True);
            Assert.That(a.InArc(new Coordinate(0, -5), 1.0e-9), Is.False);
            Assert.That(a.InArc(new Coordinate(0, 0), 1.0e-9), Is.False);
        }

        [Test]
        public void CircularSegmentAreaHalfDisc()
        {
            var a = new ExactCircularArc(
                new Coordinate(5, 0), new Coordinate(0, 5), new Coordinate(-5, 0));
            Assert.That(a.CircularSegmentArea(), Is.EqualTo(12.5 * Math.PI).Within(1.0e-12));
        }

        [Test]
        public void StaticLengthMatchesInstance()
        {
            var s = new Coordinate(1, 0);
            var m = new Coordinate(0, 1);
            var e = new Coordinate(-1, 0);
            Assert.That(ExactCircularArc.LengthOf(s, m, e),
                Is.EqualTo(new ExactCircularArc(s, m, e).Length));
        }

        [Test]
        public void ArcLengthCentroidSemicircle()
        {
            var a = new ExactCircularArc(
                new Coordinate(5, 0), new Coordinate(0, 5), new Coordinate(-5, 0));
            var c = a.ArcLengthCentroid();
            Assert.That(c.X, Is.EqualTo(0.0).Within(1.0e-12));
            Assert.That(c.Y, Is.EqualTo(10.0 / Math.PI).Within(1.0e-12));
        }

        [Test]
        public void CwCentroidMirrorsCcw()
        {
            var ccw = new ExactCircularArc(
                new Coordinate(5, 0), new Coordinate(0, 5), new Coordinate(-5, 0));
            var cw = new ExactCircularArc(
                new Coordinate(5, 0), new Coordinate(0, -5), new Coordinate(-5, 0));
            Assert.That(cw.IsCcw, Is.False);
            Assert.That(cw.Length, Is.EqualTo(ccw.Length));
            Assert.That(cw.ArcLengthCentroid().X, Is.EqualTo(ccw.ArcLengthCentroid().X).Within(1.0e-12));
            Assert.That(cw.ArcLengthCentroid().Y, Is.EqualTo(-ccw.ArcLengthCentroid().Y).Within(1.0e-12));
        }

        [Test]
        public void ExactCurveProtocol()
        {
            IExactCurve a = new ExactCircularArc(
                new Coordinate(5, 0), new Coordinate(0, 5), new Coordinate(-5, 0));
            Assert.That(a.IsExact, Is.True);
            Assert.That(a.Length, Is.EqualTo(5.0 * Math.PI).Within(1.0e-12));
            Assert.That(a.Start.X, Is.EqualTo(5.0));
            Assert.That(a.End.X, Is.EqualTo(-5.0));
            var mid = a.PointAt(0.5);
            Assert.That(mid.X, Is.EqualTo(0.0).Within(1.0e-12));
            Assert.That(mid.Y, Is.EqualTo(5.0).Within(1.0e-12));
            Assert.That(a.PointAt(0.0).X, Is.EqualTo(a.Start.X));
            Assert.That(a.PointAt(1.0).X, Is.EqualTo(a.End.X));
            var lin = a.ToLinear(0.01);
            Assert.That(lin, Is.InstanceOf<LineString>());
            Assert.That(lin.NumPoints, Is.GreaterThan(2));
            Assert.That(lin.Coordinates[0].X, Is.EqualTo(5.0));
            Assert.That(lin.Coordinates[lin.NumPoints - 1].X, Is.EqualTo(-5.0));
        }

        [Test]
        public void PointAtRejectsOutOfRange()
        {
            var a = new ExactCircularArc(
                new Coordinate(5, 0), new Coordinate(0, 5), new Coordinate(-5, 0));
            Assert.Throws<ArgumentException>(() => a.PointAt(-0.1));
            Assert.Throws<ArgumentException>(() => a.PointAt(1.1));
        }

        [Test]
        public void ColinearPointAtIsLerp()
        {
            var a = new ExactCircularArc(
                new Coordinate(0, 0), new Coordinate(1, 0), new Coordinate(4, 0));
            Assert.That(a.IsExact, Is.True);
            Assert.That(a.IsArc, Is.False);
            var p = a.PointAt(0.25);
            Assert.That(p.X, Is.EqualTo(1.0).Within(1.0e-15));
            Assert.That(p.Y, Is.EqualTo(0.0));
        }

        [Test]
        public void ConstructorDoesNotAliasCallerCoordinates()
        {
            var s = new Coordinate(5, 0);
            var m = new Coordinate(0, 5);
            var e = new Coordinate(-5, 0);
            var a = new ExactCircularArc(s, m, e);
            s.X = 99;
            Assert.That(a.Start.X, Is.EqualTo(5.0));
        }

        [Test]
        public void L1InscribedNGonNeverExceedsExact()
        {
            var rnd = new Random(unchecked((int)Seed));
            int hard = 0;
            for (int i = 0; i < GateTrials; i++)
            {
                var a = RandomArc(rnd);
                double exact = a.Length;
                double inscribed = InscribedLength(a);
                double slack = ExactCircularArc.Ulp(Math.Max(exact, 1.0));
                if (inscribed > exact + slack)
                {
                    hard++;
                }
            }
            Assert.That(hard, Is.EqualTo(0));
        }

        [Test]
        public void L2ChordLeArcHardZero()
        {
            var rnd = new Random(unchecked((int)Seed) ^ 1);
            int hard = 0;
            for (int i = 0; i < GateTrials; i++)
            {
                var a = new ExactCircularArc(Pt(rnd), Pt(rnd), Pt(rnd));
                if (!a.ChordLeArc())
                {
                    hard++;
                }
            }
            Assert.That(hard, Is.EqualTo(0));
        }

        [Test]
        public void P1LengthAtMost115PercentOfDensify()
        {
            var rnd = new Random(unchecked((int)Seed) ^ 0x51);
            var sample = new ExactCircularArc[P1Samples];
            int n = 0;
            while (n < sample.Length)
            {
                var a = new ExactCircularArc(Pt(rnd), Pt(rnd), Pt(rnd));
                if (a.IsArc)
                {
                    sample[n++] = a;
                }
            }

            // Warm the JIT so the ratio is not dominated by first-call compile.
            for (int i = 0; i < 64; i++)
            {
                _ = sample[i].Length;
                _ = sample[i].ToLinear(0.01);
            }

            var sw = Stopwatch.StartNew();
            double sink = 0.0;
            for (int i = 0; i < sample.Length; i++)
            {
                sink += sample[i].Length;
            }
            sw.Stop();
            long aTicks = sw.ElapsedTicks;

            sw.Restart();
            for (int i = 0; i < sample.Length; i++)
            {
                sink += sample[i].ToLinear(0.01).Length;
            }
            sw.Stop();
            long dTicks = sw.ElapsedTicks;
            Assert.That(sink, Is.Not.EqualTo(0.0));
            Assert.That((double)aTicks / dTicks, Is.LessThanOrEqualTo(1.15));
        }

        private static ExactCircularArc RandomArc(Random rnd)
        {
            ExactCircularArc a;
            do
            {
                a = new ExactCircularArc(Pt(rnd), Pt(rnd), Pt(rnd));
            } while (!a.IsArc);
            return a;
        }

        private static Coordinate Pt(Random rnd)
        {
            return new Coordinate(rnd.NextDouble() * 200.0 - 100.0, rnd.NextDouble() * 200.0 - 100.0);
        }

        private static double InscribedLength(ExactCircularArc a)
        {
            if (!a.IsArc)
            {
                return a.ChordLength;
            }
            return NChord * 2.0 * a.Radius * Math.Sin(a.Sweep / (2.0 * NChord));
        }
    }
}
