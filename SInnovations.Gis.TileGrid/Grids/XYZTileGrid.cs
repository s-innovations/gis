using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SInnovations.Gis.TileGrid.Grids
{
    public class XYZTileGrid : TileGrid
    {
        private const int radius = 6378137;
        private const double half_size = radius * Math.PI;

        private static TileGridOptions defaultOptions(TileGridOptions options)
        {
            options.Extent = options.Extent ?? new Double[]{
                -half_size,-half_size,half_size,half_size
            };
            options.Resolutions = TileGrid.ResolutionsFromExtent(options.Extent);
            options.Origin = Extent.GetCorner(options.Extent, Extent.Corner.TopLeft);
            return options;
        }
        public XYZTileGrid(TileGridOptions options)
            : base(defaultOptions(options))
        {

        }

        public override Func<int[], DotSpatial.Projections.ProjectionInfo, int[],int[]> CreateTileCoordTransform()
        {
            var minZ = this.MinZoom;
            var maxZ = this.MaxZoom;
            TileRange[] tileRangeByZ = null;//new TileRange[maxZ + 1];
            if (false) //https://github.com/openlayers/ol3/blob/afd43687f2b9c1f686c6588aaa50bb1cc1457f21/src/ol/tilegrid/xyztilegrid.js#L46
            {
                 tileRangeByZ = new TileRange[maxZ + 1];
                for (var z = 0; z <= maxZ; ++z)
            {
                if (z < minZ)
                {
                    tileRangeByZ[z] = null;
                }
                else
                {
                    tileRangeByZ[z] = GetTileRangeForExtentAndZ(null, z);
                }
                }
            }

            return (tileCoord,projection,opt_tileCoord) => {
                var z = tileCoord[0];
                if (z < minZ || maxZ < z) {
                  return null;
                }
                var n = 1<< z;
                var x = tileCoord[1];
                if (true){ //wrapx) {
                  x = (x%n);
                } else if (x < 0 || n <= x) {
                  return null;
                }
                var y = tileCoord[2];
                if (y < -n || -1 < y) {
                  return null;
                }
                if (tileRangeByZ!=null) {
                  if (!tileRangeByZ[z].ContainsXY(x, y)) {
                    return null;
                  }
                }
                return TileCoord.CreateOrUpdate(z, x, -y - 1, opt_tileCoord);
            };
        
        }

        /**
         * @inheritDoc
         */
        public override TileRange GetTileCoordChildTileRange(int[] tileCoord, TileRange opt_tileRange = null, double[] opt_extent = null)
        {
          if (tileCoord[0] < this.MaxZoom) {
            var doubleX = 2 * tileCoord[1];
            var doubleY = 2 * tileCoord[2];
            return TileRange.CreateOrUpdate(
                doubleX, doubleX + 1,
                doubleY, doubleY + 1,
                opt_tileRange);
          } else {
            return null;
          }
        }


        /**
         * @inheritDoc
         */
        public override bool ForEachTileCoordParentTileRange(int[] tileCoord, Func<int, TileRange, bool> callback, TileRange opt_tileRange = null, double[] opt_extent = null) {
          var tileRange = TileRange.CreateOrUpdate(
              0, tileCoord[1], 0, tileCoord[2], opt_tileRange);
          int z;
          for (z = tileCoord[0] - 1; z >= this.MinZoom; --z) {
            tileRange.MinX = tileRange.MaxX >>= 1;
            tileRange.MinY = tileRange.MaxY >>= 1;
            if (callback(z, tileRange)) {
              return true;
            }
          }
          return false;
        }
    }
}
