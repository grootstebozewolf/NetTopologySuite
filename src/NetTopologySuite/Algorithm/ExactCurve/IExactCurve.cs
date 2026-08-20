// SPDX-License-Identifier: BSD-3-Clause
//
// AI assistance disclosure:
//   AI-drafted, human-reviewed. Assisted-by: Cursor Grok 4.6
//   AI-generated portions dedicated to CC0-1.0; human curation BSD-3-Clause.

using NetTopologySuite.Geometries;

namespace NetTopologySuite.Algorithm.ExactCurve
{
    /// <summary>
    /// Thin ExactCurve protocol. Only Exact* value types implement it.
    /// </summary>
    /// <remarks>
    /// Maintainability: do not add methods; do not introduce a rich base.
    /// Soundness: <see cref="IsExact"/> implementations must not densify from
    /// <see cref="Length"/> or <see cref="PointAt"/>.
    /// Performance: closed-form cells stay allocation-light.
    /// </remarks>
    public interface IExactCurve
    {
        Coordinate Start { get; }

        Coordinate End { get; }

        double Length { get; }

        /// <summary>Point at arc-length fraction <c>t</c> in [0, 1].</summary>
        Coordinate PointAt(double t);

        /// <summary>Documented densify shim. The only allowed linearisation path.</summary>
        Geometry ToLinear(double tolerance);

        bool IsExact { get; }
    }
}
