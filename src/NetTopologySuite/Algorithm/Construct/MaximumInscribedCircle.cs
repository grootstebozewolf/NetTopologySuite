// AI-drafted, human-reviewed. Assisted-by: Cursor Grok 4.6
// Port of JTS f76d2245.

using System;
using NetTopologySuite.Algorithm.Locate;
using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Distance;
using NetTopologySuite.Utilities;
using Point = NetTopologySuite.Geometries.Point;

namespace NetTopologySuite.Algorithm.Construct
{
    /// <summary>
    /// Constructs the Maximum Inscribed Circle for a
    /// polygonal <see cref="Geometry"/>, up to a specified tolerance
    /// (which can be specified or determined automatically).
    /// The Maximum Inscribed Circle is determined by a point in the interior of the area
    /// which has the farthest distance from the area boundary,
    /// along with a boundary point at that distance.
    /// <para/>
    /// In the context of geography the center of the Maximum Inscribed Circle
    /// is known as the <b>Pole of Inaccessibility</b>.
    /// A cartographic use case is to determine a suitable point
    /// to place a map label within a polygon.
    /// <para/>
    /// The radius length of the Maximum Inscribed Circle is a
    /// measure of how "narrow" a polygon is. It is the
    /// distance at which the negative buffer becomes empty.
    /// The class supports testing whether a polygon is "narrower"
    /// than a specified distance via
    /// <see cref="IsRadiusWithin(Geometry, double)"/> or
    /// <see cref="IsRadiusWithin(double)"/>.
    /// Testing for the maximum radius is generally much faster
    /// than computing the actual radius value, since short-circuiting
    /// is used to limit the approximation iterations.
    /// <para/>
    /// The class supports polygons with holes and multipolygons.
    /// <para/>
    /// The implementation uses a successive-approximation technique
    /// over a grid of square cells covering the area geometry.
    /// The grid is refined using a branch-and-bound algorithm.
    /// Point containment and distance are computed in a performant
    /// way by using spatial indexes.
    /// <h3>Future Enhancements</h3>
    /// <list type="bullet">
    /// <item><description>Support a polygonal constraint on placement of center</description></item>
    /// </list>
    /// </summary>
    /// <author>Martin Davis</author>
    /// <see cref="LargestEmptyCircle"/>
    /// <see cref="InteriorPoint"/>
    /// <see cref="Centroid"/>
    public class MaximumInscribedCircle
    {
        // Empirically determined to balance accuracy and speed.
        private const double AutoToleranceFraction = 0.001;
        // Used for IsRadiusWithin.
        private const double MaxRadiusFraction = 0.0001;

        /// <summary>
        /// Computes the center point of the Maximum Inscribed Circle
        /// of a polygonal geometry.
        /// </summary>
        /// <param name="polygonal">A polygonal geometry</param>
        /// <returns>The center point of the maximum inscribed circle</returns>
        public static Point GetCenter(Geometry polygonal)
        {
            var mic = new MaximumInscribedCircle(polygonal);
            return mic.GetCenter();
        }

        /// <summary>
        /// Computes the center point of the Maximum Inscribed Circle
        /// of a polygonal geometry, up to a given tolerance distance.
        /// </summary>
        /// <param name="polygonal">A polygonal geometry</param>
        /// <param name="tolerance">The distance tolerance for computing the center point</param>
        /// <returns>The center point of the maximum inscribed circle</returns>
        public static Point GetCenter(Geometry polygonal, double tolerance)
        {
            var mic = new MaximumInscribedCircle(polygonal, tolerance);
            return mic.GetCenter();
        }

        /// <summary>
        /// Computes a radius line of the Maximum Inscribed Circle
        /// of a polygonal geometry.
        /// </summary>
        /// <param name="polygonal">A polygonal geometry</param>
        /// <returns>A line from the center to a point on the circle</returns>
        public static LineString GetRadiusLine(Geometry polygonal)
        {
            var mic = new MaximumInscribedCircle(polygonal);
            return mic.GetRadiusLine();
        }

        /// <summary>
        /// Computes a radius line of the Maximum Inscribed Circle
        /// of a polygonal geometry, up to a given tolerance distance.
        /// </summary>
        /// <param name="polygonal">A polygonal geometry</param>
        /// <param name="tolerance">The distance tolerance for computing the center point</param>
        /// <returns>A line from the center to a point on the circle</returns>
        public static LineString GetRadiusLine(Geometry polygonal, double tolerance)
        {
            var mic = new MaximumInscribedCircle(polygonal, tolerance);
            return mic.GetRadiusLine();
        }

        /// <summary>
        /// Tests if the radius of the maximum inscribed circle
        /// is no longer than the specified distance.
        /// The approximation tolerance is determined automatically
        /// as a fraction of the <paramref name="maxRadius"/> value.
        /// </summary>
        /// <param name="polygonal">A polygonal geometry</param>
        /// <param name="maxRadius">The radius value to test</param>
        /// <returns><c>true</c> if the max in-circle radius is no longer than the max radius</returns>
        public static bool IsRadiusWithin(Geometry polygonal, double maxRadius)
        {
            var mic = new MaximumInscribedCircle(polygonal);
            return mic.IsRadiusWithin(maxRadius);
        }

        /// <summary>
        /// Computes the maximum number of iterations allowed.
        /// Uses a heuristic based on the size of the input geometry
        /// and the tolerance distance.
        /// A smaller tolerance distance allows more iterations.
        /// This is a rough heuristic, intended
        /// to prevent huge iterations for very thin geometries.
        /// </summary>
        /// <param name="geom">The input geometry</param>
        /// <param name="toleranceDist">The tolerance distance</param>
        /// <returns>The maximum number of iterations allowed</returns>
        internal static long ComputeMaximumIterations(Geometry geom, double toleranceDist)
        {
            double diam = geom.EnvelopeInternal.Diameter;
            double tolDist = toleranceDist <= 0 ? 0.5 * diam * AutoToleranceFraction : toleranceDist;
            double ncells = diam / tolDist;
            //-- Using log of ncells allows control over number of iterations
            int factor = (int)Math.Log(ncells);
            if (factor < 1) factor = 1;
            return 2000 + 2000 * factor;
        }

        private readonly Geometry _inputGeom;
        private double _tolerance;

        private readonly GeometryFactory _factory;
        private readonly IndexedPointInAreaLocator _ptLocater;
        private readonly IndexedFacetDistance _indexedDistance;
        private Cell _centerCell;
        private Coordinate _centerPt;
        private Coordinate _radiusPt;
        private Point _centerPoint;
        private Point _radiusPoint;
        private double _maximumRadius = -1;

        /// <summary>
        /// Creates a new instance of a Maximum Inscribed Circle computation.
        /// A zero tolerance automatically determines an approximation tolerance.
        /// </summary>
        /// <param name="polygonal">An areal geometry</param>
        /// <exception cref="ArgumentException">Thrown if the input geometry is non-polygonal or empty</exception>
        public MaximumInscribedCircle(Geometry polygonal)
            : this(polygonal, 0.0)
        {
        }

        /// <summary>
        /// Creates a new instance of a Maximum Inscribed Circle computation.
        /// A zero tolerance automatically determines an approximation tolerance.
        /// </summary>
        /// <param name="polygonal">An areal geometry</param>
        /// <param name="tolerance">The distance tolerance for computing the centre point
        /// (must be non-negative)</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the tolerance is negative</exception>
        /// <exception cref="ArgumentException">Thrown if the input geometry is non-polygonal or empty</exception>
        public MaximumInscribedCircle(Geometry polygonal, double tolerance)
        {
            if (tolerance < 0)
                throw new ArgumentOutOfRangeException(nameof(tolerance), "Tolerance must be non-negative");

            if (!(polygonal is IPolygonal))
                throw new ArgumentException("Input geometry must be a Polygon or MultiPolygon");

            if (polygonal.IsEmpty)
                throw new ArgumentException("Empty input geometry is not supported");

            _inputGeom = polygonal;
            _factory = polygonal.Factory;
            _tolerance = tolerance;
            _ptLocater = new IndexedPointInAreaLocator(polygonal);
            _indexedDistance = new IndexedFacetDistance(polygonal.Boundary);
        }

        /// <summary>
        /// Tests if the radius of the maximum inscribed circle
        /// is no longer than the specified distance.
        /// This method determines the distance tolerance automatically
        /// as a fraction of the <paramref name="maxRadius"/> value.
        /// After this method is called the center and radius
        /// points provide locations demonstrating where
        /// the radius exceeds the specified maximum.
        /// </summary>
        /// <param name="maxRadius">The (non-negative) radius value to test</param>
        /// <returns><c>true</c> if the max in-circle radius is no longer than the max radius</returns>
        public bool IsRadiusWithin(double maxRadius)
        {
            if (maxRadius < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRadius), "Radius length must be non-negative");
            // Handle 0 corner case, to provide maximum domain.
            if (maxRadius == 0)
                return false;
            _maximumRadius = maxRadius;

            var env = _inputGeom.EnvelopeInternal;
            double maxDiam = 2 * _maximumRadius;
            if (env.Width < maxDiam || env.Height < maxDiam)
                return true;

            _tolerance = maxRadius * MaxRadiusFraction;
            Compute();
            double radius = _centerPt.Distance(_radiusPt);
            return radius <= _maximumRadius;
        }

        /// <summary>
        /// Gets the center point of the maximum inscribed circle
        /// (up to the tolerance distance).</summary>
        /// <returns>The center point of the maximum inscribed circle</returns>
        public Point GetCenter()
        {
            Compute();
            return _centerPoint;
        }

        /// <summary>
        /// Gets a point defining the radius of the Maximum Inscribed Circle.
        /// This is a point on the boundary which is
        /// nearest to the computed center of the Maximum Inscribed Circle.
        /// The line segment from the center to this point
        /// is a radius of the constructed circle, and this point
        /// lies on the boundary of the circle.
        /// </summary>
        /// <returns>A point defining the radius of the Maximum Inscribed Circle</returns>
        public Point GetRadiusPoint()
        {
            Compute();
            return _radiusPoint;
        }

        /// <summary>
        /// Gets a line representing a radius of the Largest Empty Circle.
        /// </summary>
        /// <returns>A line from the center of the circle to a point on the edge</returns>
        public LineString GetRadiusLine()
        {
            Compute();
            var radiusLine = _factory.CreateLineString(new[] {_centerPt.Copy(), _radiusPt.Copy()});
            return radiusLine;
        }

        /// <summary>
        /// Computes the signed distance from a point to the area boundary.
        /// Points outside the polygon are assigned a negative distance.
        /// Their containing cells will be last in the priority queue
        /// (but may still end up being tested since they may need to be refined).
        /// </summary>
        /// <param name="p">The point to compute the distance for</param>
        /// <returns>The signed distance to the area boundary (negative indicates outside the area)</returns>
        private double DistanceToBoundary(Point p)
        {
            double dist = _indexedDistance.Distance(p);
            bool isOutide = Location.Exterior == _ptLocater.Locate(p.Coordinate);
            if (isOutide) return -dist;
            return dist;
        }

        private double DistanceToBoundary(double x, double y)
        {
            var coord = new Coordinate(x, y);
            var pt = _factory.CreatePoint(coord);
            return DistanceToBoundary(pt);
        }

        private void Compute()
        {
            // check if already computed
            if (_centerCell != null) return;

            // Priority queue of cells, ordered by maximum distance from boundary
            var cellQueue = new PriorityQueue<Cell>();

            CreateInitialGrid(_inputGeom.EnvelopeInternal, cellQueue);

            // initial candidate center point
            var farthestCell = CreateInteriorPointCell(_inputGeom);
            //int totalCells = cellQueue.size();

            /*
             * Carry out the branch-and-bound search
             * of the cell space
             */
            long maxIter = ComputeMaximumIterations(_inputGeom, _tolerance);
            long iter = 0;
            while (!cellQueue.IsEmpty() && iter < maxIter)
            {
                // Increase iteration counter
                iter++;

                // pick the most promising cell from the queue
                var cell = cellQueue.Poll();
                //Console.WriteLine(_factory.ToGeometry(cell.Envelope));
                //Console.WriteLine($"{iter}] Dist: {cell.Distance} size: {cell.HSide}");

                // update the circle center cell if the candidate is further from the boundary
                if (cell.Distance > farthestCell.Distance)
                {
                    farthestCell = cell;
                }

                // Search termination when checking IsRadiusWithin predicate.
                if (_maximumRadius >= 0)
                {
                    if (farthestCell.Distance > _maximumRadius)
                        break;
                    if (cell.MaxDistance < _maximumRadius)
                        break;
                }

                /*
                 * Refine this cell if the potential distance improvement
                 * is greater than the required tolerance.
                 * Otherwise the cell is pruned (not investigated further),
                 * since no point in it is further than
                 * the current farthest distance (up to tolerance).
                 *
                 * The tolerance can be automatically determined
                 * as a fraction of the current farthest distance.
                 */
                double requiredTol = _tolerance > 0
                    ? _tolerance
                    : farthestCell.Distance * AutoToleranceFraction;

                double potentialIncrease = cell.MaxDistance - farthestCell.Distance;
                if (potentialIncrease < requiredTol)
                    break;

                // refine the cell into four sub-cells
                double h2 = cell.HSide / 2;
                cellQueue.Add(CreateCell(cell.X - h2, cell.Y - h2, h2));
                cellQueue.Add(CreateCell(cell.X + h2, cell.Y - h2, h2));
                cellQueue.Add(CreateCell(cell.X - h2, cell.Y + h2, h2));
                cellQueue.Add(CreateCell(cell.X + h2, cell.Y + h2, h2));
            }
            // the farthest cell is the best approximation to the MIC center
            _centerCell = farthestCell;
            _centerPt = new Coordinate(_centerCell.X, _centerCell.Y);
            _centerPoint = _factory.CreatePoint(_centerPt);
            // compute radius point
            var nearestPts = _indexedDistance.NearestPoints(_centerPoint);
            _radiusPt = nearestPts[0].Copy();
            _radiusPoint = _factory.CreatePoint(_radiusPt);
        }

        /// <summary>
        /// Initializes the queue with a grid of cells covering
        /// the extent of the area.
        /// </summary>
        /// <param name="env">The area extent to cover</param>
        /// <param name="cellQueue">The queue to initialize</param>
        private void CreateInitialGrid(Envelope env, PriorityQueue<Cell> cellQueue)
        {
            double cellSize = env.MaxExtent;
            double hSide = cellSize / 2.0;

            // Check for flat collapsed input and if so short-circuit
            // Result will just be centroid
            if (cellSize == 0) return;

            var centre = env.Centre;
            cellQueue.Add(CreateCell(centre.X, centre.Y, hSide));
        }

        private Cell CreateCell(double x, double y, double hSide)
        {
            return new Cell(x, y, hSide, DistanceToBoundary(x, y));
        }

        // create a cell at an interior point
        private Cell CreateInteriorPointCell(Geometry geom)
        {
            var p = geom.InteriorPoint;
            return new Cell(p.X, p.Y, 0, DistanceToBoundary(p));
        }

        /// <summary>
        /// A square grid cell centered on a given point
        /// with a given side half-length,
        /// and having a given distance from the center point to the constraints.
        /// The maximum possible distance from any point in the cell to the
        /// constraints can be computed.
        /// This is used as the ordering and upper-bound function in
        /// the branch-and-bound algorithm. 
        /// </summary>
        private class Cell : IComparable<Cell>
        {

            private const double Sqrt2 = 1.4142135623730951;

            public Cell(double x, double y, double hSide, double distanceToConstraints)
            {
                X = x; // cell center x
                Y = y; // cell center y
                HSide = hSide; // half the cell size

                // the distance from cell center to constraints
                Distance = distanceToConstraints;

                /*
                 * The maximum possible distance to the constraints for points in this cell
                 * is the center distance plus the radius (half the diagonal length).
                 */
                MaxDistance = Distance + hSide * Sqrt2;
            }

            public bool IsFullyOutside => MaxDistance < 0;

            public bool IsOutside => Distance < 0;

            public double MaxDistance { get; }

            public double Distance { get; }

            public double HSide { get; }

            public double X { get; }

            public double Y { get; }

            /// <summary>
            /// For maximum efficieny sort the PriorityQueue with largest maxDistance at front.
            /// Since AlternativePriorityQueue sorts least-first, need to invert the comparison
            /// </summary>
            public int CompareTo(Cell o)
            {
                return -MaxDistance.CompareTo(o.MaxDistance);
            }
        }

    }
}
