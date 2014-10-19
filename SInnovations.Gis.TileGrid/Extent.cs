using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SInnovations.Gis.TileGrid
{
          


    public static class Extent
    {
        public enum Corner
        {
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight,
        }

        public static double[] CreateOrUpdate(double minX, double minY, double maxX, double maxY, double[] opt_extent)
        {
            return new double[] { minX, minY, maxX, maxY };
        }

        public static double GetHeight(double[] extent)
        {
            return extent[3] - extent[1];
        }

        public static double GetWidth(double[] extent)
        {
            return extent[2] - extent[0];
        }


        public static double[] GetCorner(this double[] extent, Corner corner) {
          double[] coordinate=null;
          if (corner == Corner.BottomLeft) {
            coordinate = GetBottomLeft(extent);
          } else if (corner == Corner.BottomRight) {
            coordinate = GetBottomRight(extent);
          } else if (corner == Corner.TopLeft) {
            coordinate = GetTopLeft(extent);
          } else if (corner == Corner.TopRight) {
            coordinate = GetTopRight(extent);
          }
        
          return coordinate;
        }

        public static double[] GetTopLeft(this double[] extent) {
          return new double[]{extent[0], extent[3]};
        }
                
        public static double[] GetTopRight(this double[] extent) {
          return new double[]{extent[2], extent[3]};
        }




        public static double[] GetBottomRight(this double[] extent)
        {
            return new double[] { extent[2], extent[1] };
        }

        public static double[] GetBottomLeft(this double[] extent)
        {
            return new double[] { extent[0], extent[1] };
        }

    

    }
}
