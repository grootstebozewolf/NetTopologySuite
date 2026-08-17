using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using NetTopologySuite.Algorithm;
using NetTopologySuite.Geometries;

namespace NetTopologySuite.Tests.NUnit.Algorithm.RocqRef
{
    /// <summary>
    /// C# port of JTS <c>RocqRefRunner</c> (the integer-domain orientation
    /// reference). Catalyst for NTS: prove this port equals the Java
    /// reconstruction and the Coq <c>rocqref_refSign</c> formula.
    /// </summary>
    /// <remarks>
    /// Call this RocqRefRunner, not "oracle". Production
    /// <see cref="Orientation.Index"/> is not this class.
    /// SoT: <c>grootstebozewolf/NetTopologySuite.Proofs</c>
    /// <c>theories/RocqRefRunner.v</c> and
    /// <c>docs/rocqref-jts-nts-equiv.md</c>.
    /// </remarks>
    public static class RocqRefRunner
    {
        /// <summary>Largest |coord| for which <see cref="RefSign"/> is exact in 64-bit arithmetic: 2^25.</summary>
        public const long SafeBound = 1L << 25;

        public sealed class RefCase
        {
            public readonly long P0x, P0y, P1x, P1y, Qx, Qy;
            public readonly int Expected;

            public RefCase(long p0x, long p0y, long p1x, long p1y, long qx, long qy)
                : this(p0x, p0y, p1x, p1y, qx, qy, RefSign(p0x, p0y, p1x, p1y, qx, qy))
            {
            }

            public RefCase(long p0x, long p0y, long p1x, long p1y, long qx, long qy, int expected)
            {
                P0x = p0x; P0y = p0y;
                P1x = p1x; P1y = p1y;
                Qx = qx; Qy = qy;
                Expected = expected;
            }

            public override string ToString()
            {
                return "(" + P0x + " " + P0y + ", " + P1x + " " + P1y + ", " + Qx + " " + Qy
                    + ") expected=" + Expected;
            }
        }

        public sealed class Result
        {
            public long Checked;
            public long Mismatches;
            public readonly List<string> Failures = new List<string>();
            private const int MaxFailuresRecorded = 20;

            internal void Record(RefCase c, int actual)
            {
                Mismatches++;
                if (Failures.Count < MaxFailuresRecorded)
                    Failures.Add(c + " but Orientation.Index returned " + actual);
            }

            public bool IsSound => Mismatches == 0;

            public override string ToString()
            {
                var sb = new System.Text.StringBuilder();
                sb.Append(Checked).Append(" cases checked, ").Append(Mismatches).Append(" mismatch(es)");
                foreach (var f in Failures)
                    sb.Append("\n  ").Append(f);
                return sb.ToString();
            }
        }

        /// <summary>
        /// Exact orientation sign for integer coordinates in <see cref="SafeBound"/>.
        /// Same formula as JTS <c>RocqRefRunner.refSign</c> and Coq <c>rocqref_refSign</c>.
        /// </summary>
        public static int RefSign(long p0x, long p0y, long p1x, long p1y, long qx, long qy)
        {
            RequireInDomain(p0x); RequireInDomain(p0y);
            RequireInDomain(p1x); RequireInDomain(p1y);
            RequireInDomain(qx); RequireInDomain(qy);

            long dx1 = p1x - p0x;
            long dy1 = p1y - p0y;
            long dx2 = qx - p0x;
            long dy2 = qy - p0y;
            long det = dx1 * dy2 - dy1 * dx2;
            return Math.Sign(det);
        }

        /// <summary>Unconditionally exact integer determinant via <see cref="BigInteger"/>.</summary>
        public static int RefSignBig(long p0x, long p0y, long p1x, long p1y, long qx, long qy)
        {
            var dx1 = new BigInteger(p1x - p0x);
            var dy1 = new BigInteger(p1y - p0y);
            var dx2 = new BigInteger(qx - p0x);
            var dy2 = new BigInteger(qy - p0y);
            return (dx1 * dy2 - dy1 * dx2).Sign;
        }

        /// <summary>
        /// Exact orientation of arbitrary finite doubles via the common-exponent
        /// integer determinant (Coq <c>b64_orient2d_exact</c>).
        /// </summary>
        public static int RefSignExact(double p0x, double p0y, double p1x, double p1y, double qx, double qy)
        {
            RequireFinite(p0x); RequireFinite(p0y);
            RequireFinite(p1x); RequireFinite(p1y);
            RequireFinite(qx); RequireFinite(qy);

            Decompose(p0x, out var m0x, out var e0x);
            Decompose(p0y, out var m0y, out var e0y);
            Decompose(p1x, out var m1x, out var e1x);
            Decompose(p1y, out var m1y, out var e1y);
            Decompose(qx, out var mqx, out var eqx);
            Decompose(qy, out var mqy, out var eqy);

            int e = e0x;
            if (e0y < e) e = e0y;
            if (e1x < e) e = e1x;
            if (e1y < e) e = e1y;
            if (eqx < e) e = eqx;
            if (eqy < e) e = eqy;

            var x0 = Shift(m0x, e0x - e);
            var y0 = Shift(m0y, e0y - e);
            var x1 = Shift(m1x, e1x - e);
            var y1 = Shift(m1y, e1y - e);
            var xq = Shift(mqx, eqx - e);
            var yq = Shift(mqy, eqy - e);
            return ((x1 - x0) * (yq - y0) - (xq - x0) * (y1 - y0)).Sign;
        }

        public static Result Run(IEnumerable<RefCase> cases)
        {
            var r = new Result();
            foreach (var c in cases)
            {
                r.Checked++;
                int actual = (int)Orientation.Index(
                    new Coordinate(c.P0x, c.P0y),
                    new Coordinate(c.P1x, c.P1y),
                    new Coordinate(c.Qx, c.Qy));
                if (actual != c.Expected)
                    r.Record(c, actual);
            }
            return r;
        }

        public static List<RefCase> ExhaustiveGrid(int radius)
        {
            var cases = new List<RefCase>();
            for (long ax = -radius; ax <= radius; ax++)
                for (long ay = -radius; ay <= radius; ay++)
                    for (long bx = -radius; bx <= radius; bx++)
                        for (long by = -radius; by <= radius; by++)
                            for (long cx = -radius; cx <= radius; cx++)
                                for (long cy = -radius; cy <= radius; cy++)
                                    cases.Add(new RefCase(ax, ay, bx, by, cx, cy));
            return cases;
        }

        public static List<RefCase> Random(int n, long bound, int seed)
        {
            var rnd = new Random(seed);
            var cases = new List<RefCase>(n);
            for (int i = 0; i < n; i++)
            {
                cases.Add(new RefCase(
                    RandCoord(rnd, bound), RandCoord(rnd, bound),
                    RandCoord(rnd, bound), RandCoord(rnd, bound),
                    RandCoord(rnd, bound), RandCoord(rnd, bound)));
            }
            return cases;
        }

        public static List<RefCase> LoadProofCases(TextReader reader)
        {
            var cases = new List<RefCase>();
            int lineNo = 0;
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                lineNo++;
                int hash = line.IndexOf('#');
                if (hash >= 0)
                    line = line.Substring(0, hash);
                line = line.Trim();
                if (line.Length == 0)
                    continue;
                var tok = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (tok.Length != 6 && tok.Length != 7)
                    throw new InvalidDataException("line " + lineNo + ": expected 6 or 7 integers, got " + tok.Length);
                long p0x = long.Parse(tok[0], CultureInfo.InvariantCulture);
                long p0y = long.Parse(tok[1], CultureInfo.InvariantCulture);
                long p1x = long.Parse(tok[2], CultureInfo.InvariantCulture);
                long p1y = long.Parse(tok[3], CultureInfo.InvariantCulture);
                long qx = long.Parse(tok[4], CultureInfo.InvariantCulture);
                long qy = long.Parse(tok[5], CultureInfo.InvariantCulture);
                int derived = RefSign(p0x, p0y, p1x, p1y, qx, qy);
                if (tok.Length == 7)
                {
                    int claimed = int.Parse(tok[6], CultureInfo.InvariantCulture);
                    if (claimed != derived)
                    {
                        throw new InvalidDataException("line " + lineNo
                            + ": exported sign " + claimed + " disagrees with RocqRefRunner.RefSign " + derived);
                    }
                }
                cases.Add(new RefCase(p0x, p0y, p1x, p1y, qx, qy, derived));
            }
            return cases;
        }

        private static void RequireInDomain(long c)
        {
            if (c < -SafeBound || c > SafeBound)
                throw new ArgumentOutOfRangeException(nameof(c), c, "outside certified domain [-2^25, 2^25]");
        }

        private static void RequireFinite(double c)
        {
            if (double.IsNaN(c) || double.IsInfinity(c))
                throw new ArgumentException("coordinate is not finite: " + c);
        }

        private static long RandCoord(Random rnd, long bound)
        {
            long span = 2 * bound + 1;
            long v = FloorMod(NextLong(rnd), span) - bound;
            return v;
        }

        private static long NextLong(Random rnd)
        {
            Span<byte> buf = stackalloc byte[8];
            rnd.NextBytes(buf);
            return BitConverter.ToInt64(buf);
        }

        private static long FloorMod(long x, long m)
        {
            long r = x % m;
            return r < 0 ? r + m : r;
        }

        private static void Decompose(double d, out BigInteger mant, out int exp)
        {
            long bits = BitConverter.DoubleToInt64Bits(d);
            bool neg = bits < 0;
            int biased = (int)((bits >> 52) & 0x7FFL);
            long frac = bits & 0xFFFFFFFFFFFFFL;
            if (biased == 0)
            {
                mant = frac;
                exp = -1074;
            }
            else
            {
                mant = frac | (1L << 52);
                exp = biased - 1075;
            }
            if (neg)
                mant = -mant;
        }

        private static BigInteger Shift(BigInteger mant, int expDelta)
        {
            if (expDelta < 0)
                throw new InvalidOperationException("common exponent is not a minimum");
            return mant << expDelta;
        }
    }
}
