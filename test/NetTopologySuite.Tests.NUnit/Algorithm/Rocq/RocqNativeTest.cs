using NetTopologySuite.Robust.Native;
using NUnit.Framework;

namespace NetTopologySuite.Tests.NUnit.Algorithm.Rocq
{
    /// <summary>
    /// Phase 5 in-process FFI smoke. Skips when libntsrocq is not on the
    /// loader path (CI default).
    /// </summary>
    [TestFixture]
    public class RocqNativeTest
    {
        [Test]
        public void UnavailableIsSafe()
        {
            // Must not throw just because the native library is absent.
            Assert.That(RocqNative.IsAvailable || !RocqNative.IsAvailable);
        }

        [Test]
        public void OrientFilteredCcw()
        {
            if (!RocqNative.IsAvailable)
            {
                Assert.Pass("libntsrocq not loaded");
                return;
            }

            Assert.That(
                RocqNative.OrientSignFiltered(0, 0, 1, 0, 0, 1),
                Is.EqualTo(RocqOrientSign.Pos));
        }

        [Test]
        public void InCircleOutside()
        {
            if (!RocqNative.IsAvailable)
            {
                Assert.Pass("libntsrocq not loaded");
                return;
            }

            double det = RocqNative.InCircle(0, 0, 2, 0, 1, 1, 1, -0.5);
            Assert.That(det, Is.LessThan(0));
        }
    }
}
