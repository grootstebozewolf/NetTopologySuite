using System;
using System.Collections;
using System.Text;

using GisSharpBlog.NetTopologySuite.Geometries;
using GisSharpBlog.NetTopologySuite.Geometries.Utilities;

namespace GisSharpBlog.NetTopologySuite.Simplify
{
    /// <summary>
    /// Simplifies a <c>Geometry</c> using the standard Douglas-Peucker algorithm.
    /// Ensures that any polygonal geometries returned are valid.
    /// Simple lines are not guaranteed to remain simple after simplification.
    /// Note that in general D-P does not preserve topology -
    /// e.g. polygons can be split, collapse to lines or disappear
    /// holes can be created or disappear,
    /// and lines can cross.
    /// To simplify point while preserving topology use TopologySafeSimplifier.
    /// (However, using D-P is significantly faster).
    /// </summary>
    public class DouglasPeuckerSimplifier
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="geom"></param>
        /// <param name="distanceTolerance"></param>
        /// <returns></returns>
        public static Geometry Simplify(Geometry geom, double distanceTolerance)
        {
            DouglasPeuckerSimplifier tss = new DouglasPeuckerSimplifier(geom);
            tss.DistanceTolerance = distanceTolerance;
            return tss.GetResultGeometry();
        }

        private Geometry inputGeom;
        private double distanceTolerance;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="inputGeom"></param>
        public DouglasPeuckerSimplifier(Geometry inputGeom)
        {
            this.inputGeom = inputGeom;
        }

        /// <summary>
        /// 
        /// </summary>
        public virtual double DistanceTolerance
        {
            get
            {
                return distanceTolerance; 
            }
            set
            {
                distanceTolerance = value; 
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public virtual Geometry GetResultGeometry()
        {
            return (new DPTransformer(this)).Transform(inputGeom);
        }

        /// <summary>
        /// 
        /// </summary>
        private class DPTransformer : GeometryTransformer
        {
            private DouglasPeuckerSimplifier container = null;

            /// <summary>
            /// 
            /// </summary>
            /// <param name="container"></param>
            public DPTransformer(DouglasPeuckerSimplifier container)
            {
                this.container = container;
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="coords"></param>
            /// <param name="parent"></param>
            /// <returns></returns>
            protected override ICoordinateSequence TransformCoordinates(ICoordinateSequence coords, Geometry parent)
            {
                Coordinate[] inputPts = coords.ToCoordinateArray();
                Coordinate[] newPts = DouglasPeuckerLineSimplifier.Simplify(inputPts, container.DistanceTolerance);
                return factory.CoordinateSequenceFactory.Create(newPts);
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="geom"></param>
            /// <param name="parent"></param>
            /// <returns></returns>
            protected override Geometry TransformPolygon(Polygon geom, Geometry parent)
            {
                Geometry roughGeom = base.TransformPolygon(geom, parent);
                // don't try and correct if the parent is going to do this
                if (parent is MultiPolygon) 
                    return roughGeom;            
                return CreateValidArea(roughGeom);
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="geom"></param>
            /// <param name="parent"></param>
            /// <returns></returns>
            protected override Geometry TransformMultiPolygon(MultiPolygon geom, Geometry parent)
            {
                Geometry roughGeom = base.TransformMultiPolygon(geom, parent);
                return CreateValidArea(roughGeom);
            }

            /// <summary>
            /// Creates a valid area point from one that possibly has
            /// bad topology (i.e. self-intersections).
            /// Since buffer can handle invalid topology, but always returns
            /// valid point, constructing a 0-width buffer "corrects" the
            /// topology.
            /// Note this only works for area geometries, since buffer always returns
            /// areas.  This also may return empty geometries, if the input
            /// has no actual area.
            /// </summary>
            /// <param name="roughAreaGeom">An area point possibly containing self-intersections.</param>
            /// <returns>A valid area point.</returns>
            private Geometry CreateValidArea(Geometry roughAreaGeom)
            {
                return roughAreaGeom.Buffer(0.0);
            }
        }
    }
}