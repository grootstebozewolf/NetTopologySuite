using NetTopologySuite.Geometries;

namespace NetTopologySuite.IO
{
    /// <summary>
    /// Constants used in the WKT (Well-Known Text) format.
    /// </summary>
    /// <author>Martin Davis</author>
    public class WKTConstants
    {
        /// <summary>
        /// Token text for <see cref="GeometryCollection"/> geometries
        /// </summary>
        public const string GEOMETRYCOLLECTION = "GEOMETRYCOLLECTION";
        /// <summary>
        /// Token text for <see cref="LinearRing"/> geometries
        /// </summary>
        public const string LINEARRING = "LINEARRING";
        /// <summary>
        /// Token text for <see cref="LineString"/> geometries
        /// </summary>
        public const string LINESTRING = "LINESTRING";
        /// <summary>
        /// Token text for <see cref="MultiPolygon"/> geometries
        /// </summary>
        public const string MULTIPOLYGON = "MULTIPOLYGON";
        /// <summary>
        /// Token text for <see cref="MultiLineString"/> geometries
        /// </summary>
        public const string MULTILINESTRING = "MULTILINESTRING";
        /// <summary>
        /// Token text for <see cref="MultiPoint"/> geometries
        /// </summary>
        public const string MULTIPOINT = "MULTIPOINT";
        /// <summary>
        /// Token text for <see cref="Point"/> geometries
        /// </summary>
        public const string POINT = "POINT";
        /// <summary>
        /// Token text for <see cref="Polygon"/> geometries
        /// </summary>
        public const string POLYGON = "POLYGON";

        /// <summary>
        /// Token text for <see cref="Geometries.Curves.CircularString"/> geometries
        /// </summary>
        public const string CIRCULARSTRING = "CIRCULARSTRING";
        /// <summary>
        /// Token text for <see cref="Geometries.Curves.CompoundCurve"/> geometries
        /// </summary>
        public const string COMPOUNDCURVE = "COMPOUNDCURVE";
        /// <summary>
        /// Token text for <see cref="Geometries.Curves.CurvePolygon"/> geometries
        /// </summary>
        public const string CURVEPOLYGON = "CURVEPOLYGON";
        /// <summary>
        /// Token text for <see cref="Geometries.Curves.Triangle"/> geometries
        /// </summary>
        public const string TRIANGLE = "TRIANGLE";
        /// <summary>
        /// Token text for <see cref="Geometries.Curves.Tin"/> geometries
        /// </summary>
        public const string TIN = "TIN";

        /// <summary>
        /// Token text for ISO/IEC 13249-3 §4.2.7 ST_Circle. Instantiable;
        /// NTS has no carrier yet — the reader names the type and refuses.
        /// </summary>
        public const string CIRCLE = "CIRCLE";
        /// <summary>
        /// Token text for ISO/IEC 13249-3 §4.2.8 ST_GeodesicString.
        /// Instantiable; named refuse until a carrier exists.
        /// </summary>
        public const string GEODESICSTRING = "GEODESICSTRING";
        /// <summary>
        /// Token text for ISO/IEC 13249-3 §4.2.9 ST_EllipticalCurve.
        /// Instantiable; named refuse until a carrier exists.
        /// </summary>
        public const string ELLIPTICALCURVE = "ELLIPTICALCURVE";
        /// <summary>
        /// Token text for ISO/IEC 13249-3 §4.2.10 ST_NURBSCurve.
        /// Instantiable; named refuse until a carrier exists.
        /// </summary>
        public const string NURBSCURVE = "NURBSCURVE";
        /// <summary>
        /// Token text for ISO/IEC 13249-3 §4.2.11 ST_Clothoid.
        /// Instantiable; named refuse until a carrier exists.
        /// </summary>
        public const string CLOTHOID = "CLOTHOID";
        /// <summary>
        /// Token text for ISO/IEC 13249-3 §4.2.12 ST_SpiralCurve.
        /// Instantiable; named refuse until a carrier exists.
        /// </summary>
        public const string SPIRALCURVE = "SPIRALCURVE";

        /// <summary>
        /// Token text for empty geometries
        /// </summary>
        public const string EMPTY = "EMPTY";

        /// <summary>
        /// Token text indicating that geometries have measure-ordinate values
        /// </summary>
        public const string M = "M";
        /// <summary>
        /// Token text indicating that geometries have z-ordinate values
        /// </summary>
        public const string Z = "Z";
        /// <summary>
        /// Token text indicating that geometries have both z- and measure-ordinate values
        /// </summary>
        public const string ZM = "ZM";

    }
}
