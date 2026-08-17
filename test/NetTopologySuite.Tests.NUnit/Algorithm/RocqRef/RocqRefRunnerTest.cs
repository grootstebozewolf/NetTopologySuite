using System.IO;
using NUnit.Framework;

namespace NetTopologySuite.Tests.NUnit.Algorithm.RocqRef
{
    /// <summary>
    /// NTS port of JTS <c>RocqRefRunnerTest</c>. Pins C# <see cref="RocqRefRunner.RefSign"/>
    /// to the Coq formula and to production <c>Orientation.Index</c> on the
    /// integer SAFE_BOUND domain. Shared vectors live in Proofs
    /// <c>oracle/rocqref/jts_nts_equiv_vectors.txt</c>.
    /// </summary>
    [TestFixture]
    public class RocqRefRunnerTest
    {
        [Test]
        public void TestReferenceIsExactInDomain()
        {
            var rnd = new System.Random(1);
            long bound = RocqRefRunner.SafeBound;
            for (int i = 0; i < 50000; i++)
            {
                long p0x = RndIn(rnd, bound), p0y = RndIn(rnd, bound);
                long p1x = RndIn(rnd, bound), p1y = RndIn(rnd, bound);
                long qx = RndIn(rnd, bound), qy = RndIn(rnd, bound);
                Assert.That(
                    RocqRefRunner.RefSign(p0x, p0y, p1x, p1y, qx, qy),
                    Is.EqualTo(RocqRefRunner.RefSignBig(p0x, p0y, p1x, p1y, qx, qy)));
            }
        }

        [Test]
        public void TestExhaustiveSmallGrid()
        {
            var r = RocqRefRunner.Run(RocqRefRunner.ExhaustiveGrid(4));
            Assert.That(r.IsSound, Is.True, "orientation unsound on small grid:\n" + r);
        }

        [Test]
        public void TestRandomWithinDomain()
        {
            var r = RocqRefRunner.Run(RocqRefRunner.Random(50000, RocqRefRunner.SafeBound, 42));
            Assert.That(r.IsSound, Is.True, "orientation unsound on random sample:\n" + r);
        }

        [Test]
        public void TestJtsNtsEquivVectors()
        {
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory,
                "Algorithm", "RocqRef", "jts_nts_equiv_vectors.txt");
            Assert.That(File.Exists(path), Is.True, "missing shared vectors at " + path);
            using (var reader = File.OpenText(path))
            {
                var cases = RocqRefRunner.LoadProofCases(reader);
                Assert.That(cases.Count, Is.GreaterThan(0));
                var r = RocqRefRunner.Run(cases);
                Assert.That(r.IsSound, Is.True, "NTS disagrees with RocqRefRunner vectors:\n" + r);
            }
        }

        [Test]
        public void TestLockedUnitExamples()
        {
            Assert.That(RocqRefRunner.RefSign(0, 0, 1, 0, 0, 1), Is.EqualTo(1));
            Assert.That(RocqRefRunner.RefSign(0, 0, 1, 0, 0, -1), Is.EqualTo(-1));
            Assert.That(RocqRefRunner.RefSign(0, 0, 2, 2, 1, 1), Is.EqualTo(0));
        }

        [Test]
        public void TestR2LiteratureHardCases()
        {
            double[][] triples =
            {
                new[] { 1.4540766091864998, -7.989685402102996,
                    23.131039116367354, -7.004368924503866,
                    1.4540766091865, -7.989685402102996 },
                new[] { 219.3649559090992, 140.84159161824724,
                    168.9018919682399, -5.713787599646864,
                    186.80814046338352, 46.28973405831556 },
            };
            foreach (var t in triples)
            {
                int expected = RocqRefRunner.RefSignExact(t[0], t[1], t[2], t[3], t[4], t[5]);
                int actual = (int)NetTopologySuite.Algorithm.Orientation.Index(
                    new NetTopologySuite.Geometries.Coordinate(t[0], t[1]),
                    new NetTopologySuite.Geometries.Coordinate(t[2], t[3]),
                    new NetTopologySuite.Geometries.Coordinate(t[4], t[5]));
                Assert.That(actual, Is.EqualTo(expected),
                    "DD vs RefSignExact on literature case");
            }
        }

        private static long RndIn(System.Random rnd, long bound)
        {
            long span = 2 * bound + 1;
            long v = rnd.NextInt64();
            long r = v % span;
            if (r < 0) r += span;
            return r - bound;
        }
    }
}
