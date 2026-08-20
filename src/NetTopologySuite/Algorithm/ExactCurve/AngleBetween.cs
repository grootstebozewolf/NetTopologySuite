// SPDX-License-Identifier: BSD-3-Clause
//
// AI assistance disclosure:
//   AI-drafted, human-reviewed. Assisted-by: Cursor Grok 4.6
//   AI-generated portions dedicated to CC0-1.0; human curation BSD-3-Clause.

using System;
using NetTopologySuite.Geometries;

namespace NetTopologySuite.Algorithm.ExactCurve
{
    /// <summary>
    /// Sweep owner for <see cref="ExactCircularArc"/>: signed short via
    /// <c>atan2(cross, dot)</c>, then mid-point long/short disambiguation.
    /// </summary>
    /// <remarks>
    /// Maintainability: one place for the transcendental; callers must not
    /// re-roll <c>% 2π</c> locally.
    /// Soundness: relative <c>atan2(u×v, u·v)</c> does not collapse a tiny
    /// crossing at the <c>±π</c> branch cut into a false full turn.
    /// Performance: <see cref="SweepOf(double, double, Coordinate, Coordinate, Coordinate)"/>
    /// is allocation-free.
    /// Port of JTS <c>9797c2c4</c>.
    /// </remarks>
    public static class AngleBetween
    {
        public const double TwoPi = 2.0 * Math.PI;

        public static double SignedShort(double ux, double uy, double vx, double vy)
        {
            return Math.Atan2(ux * vy - uy * vx, ux * vx + uy * vy);
        }

        public static double SignedShort(double cx, double cy, Coordinate from, Coordinate to)
        {
            return SignedShort(from.X - cx, from.Y - cy, to.X - cx, to.Y - cy);
        }

        public static DirectedSweep Through(double cx, double cy,
            Coordinate start, Coordinate mid, Coordinate end)
        {
            return FromShorts(
                SignedShort(cx, cy, start, end),
                SignedShort(cx, cy, start, mid));
        }

        public static double SweepOf(double cx, double cy,
            Coordinate start, Coordinate mid, Coordinate end)
        {
            return SweepFromShorts(
                SignedShort(cx, cy, start, end),
                SignedShort(cx, cy, start, mid));
        }

        public static double Travelled(bool ccw, double ux, double uy, double px, double py)
        {
            double s = SignedShort(ux, uy, px, py);
            return ccw ? NormalizePositive(s) : NormalizePositive(-s);
        }

        public static double NormalizePositive(double angle)
        {
            angle %= TwoPi;
            if (angle < 0.0) angle += TwoPi;
            return angle;
        }

        public readonly struct DirectedSweep
        {
            public DirectedSweep(bool ccw, double radians)
            {
                IsCcw = ccw;
                Radians = radians;
            }

            public bool IsCcw { get; }

            public double Radians { get; }

            public double Signed => IsCcw ? Radians : -Radians;
        }

        private static DirectedSweep FromShorts(double shortSE, double shortSM)
        {
            bool ccw = CcwFromShorts(shortSE, shortSM);
            return new DirectedSweep(ccw, SweepFromShorts(shortSE, shortSM));
        }

        private static bool CcwFromShorts(double shortSE, double shortSM)
        {
            return NormalizePositive(shortSM) < NormalizePositive(shortSE);
        }

        private static double SweepFromShorts(double shortSE, double shortSM)
        {
            double s = CcwFromShorts(shortSE, shortSM)
                ? NormalizePositive(shortSE)
                : NormalizePositive(-shortSE);
            return s == 0.0 ? TwoPi : s;
        }
    }
}
