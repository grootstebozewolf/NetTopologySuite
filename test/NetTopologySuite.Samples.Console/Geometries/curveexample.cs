using System;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Curves;
using NetTopologySuite.IO;

namespace NetTopologySuite.Samples.Geometries
{
    /// <summary>
    /// Shows basic ways of creating and reading/writing the SQL/MM curve
    /// geometry types (<see cref="CircularString"/>, <see cref="CompoundCurve"/>,
    /// <see cref="CurvePolygon"/>).
    /// </summary>
    public class CurveExample
    {
        [STAThread]
        public static void main(string[] args)
        {
            var reader = new WKTReader();
            var writer = new WKTWriter();

            // A CircularString is a sequence of arcs, each defined by three
            // consecutive coordinates (start, a point on the arc, end).
            var arc = (CircularString)reader.Read("CIRCULARSTRING (0 0, 1 1, 2 0)");
            Console.WriteLine("Arc: " + writer.Write(arc));
            Console.WriteLine("Arc.NumArcs: " + arc.NumArcs);

            // Length, Area and Envelope are currently based on the control
            // points (chords), not the true arc geometry.
            Console.WriteLine("Arc.Length (chord, not true arc length): " + arc.Length);

            // A CompoundCurve joins straight LineString and CircularString
            // segments end-to-end into a single curve.
            var compound = (CompoundCurve)reader.Read(
                "COMPOUNDCURVE ((0 0, 1 0), CIRCULARSTRING (1 0, 2 1, 3 0), (3 0, 4 0))");
            Console.WriteLine("CompoundCurve: " + writer.Write(compound));

            // A CurvePolygon is like a Polygon but its rings may be curves;
            // here the shell is straight and the hole is a circular arc.
            var curvePolygon = (CurvePolygon)reader.Read(
                "CURVEPOLYGON ((0 0, 100 0, 100 100, 0 100, 0 0), " +
                "CIRCULARSTRING (40 50, 50 60, 60 50, 50 40, 40 50))");
            Console.WriteLine("CurvePolygon: " + writer.Write(curvePolygon));

            // WKB round-trips too.
            byte[] wkb = new WKBWriter().Write(arc);
            var arcFromWkb = new WKBReader().Read(wkb);
            Console.WriteLine("Arc from WKB equals original: " + arcFromWkb.EqualsExact(arc));

            // Linearize() currently just returns the control points as a
            // LineString/Polygon; it does not densify the arc yet.
            LineString approx = arc.Linearize();
            Console.WriteLine("Arc.Linearize(): " + writer.Write(approx));
        }
    }
}
