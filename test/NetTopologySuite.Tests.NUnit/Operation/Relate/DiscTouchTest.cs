// SPDX-License-Identifier: BSD-3-Clause
//
// AI assistance disclosure:
//   AI-drafted, human-reviewed. Assisted-by: Cursor Grok 4.6
//   AI-generated portions dedicated to CC0-1.0; human curation BSD-3-Clause.

using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NUnit.Framework;

namespace NetTopologySuite.Tests.NUnit.Operation.Relate
{
    /// <summary>
    /// T-ext two-disc kiss is FF2F01212. Witness: r=5 discs, kiss (4, 3).
    /// Port of JTS <c>cd426d0f</c>.
    /// </summary>
    public class DiscTouchTest
    {
        private const string Circle5 =
            "CURVEPOLYGON (CIRCULARSTRING (-5 0, 0 5, 5 0, 0 -5, -5 0))";
        private const string Circle345 =
            "CURVEPOLYGON (CIRCULARSTRING (13 6, 8 11, 3 6, 8 1, 13 6))";

        [Test]
        public void ExternalTangentDiscsAreFF2F01212()
        {
            var reader = new WKTReader();
            var a = reader.Read(Circle5);
            var b = reader.Read(Circle345);
            Assert.That(a.Relate(b).ToString(), Is.EqualTo("FF2F01212"));
            Assert.That(b.Relate(a).ToString(), Is.EqualTo("FF2F01212"));
            Assert.That(a.Touches(b), Is.True);
            Assert.That(b.Touches(a), Is.True);
        }
    }
}
