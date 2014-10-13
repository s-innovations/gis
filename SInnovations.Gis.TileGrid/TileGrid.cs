using DotSpatial.Projections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SInnovations.Gis.TileGrid
{

    public class TileGridOptions
    {
        public double[] Origin { get; set; }
    
        public  int MinZoom { get; set; }
    
public  IList<int> Resolutions { get; set; }}
    public abstract class TileGrid
    {
        private double[] _origin { get; set; }
        private IList<double[]> _origins { get; set; }
        private IList<int> _tileSizes { get; set; }
        private int? _tileSize { get; set; }

        protected int MinZoom { get; set; }
        protected int MaxZoom { get; set; }
        protected IList<int> Resolutions { get; set; }


        public TileGrid(TileGridOptions options)
        {
           MinZoom = options.MinZoom;
           Resolutions = options.Resolutions;
           MaxZoom = Resolutions.Count -1;
           _origin = options.Origin;
        }


        public abstract Func<int[], ProjectionInfo, int[]> CreateTileCoordTransform();

        public double[] GetOrigin(int z){
          
            if (this._origin != null) {
            return this._origin;
            } else {
            //   goog.asserts.assert(!goog.isNull(this.origins_));
            //   goog.asserts.assert(this.minZoom <= z && z <= this.maxZoom);
            return this._origins[z];
            }
            
        }
        /**
         * @param {number} z Z.
         * @return {number} Resolution.
         * @api stable
         */
        public double GetResolution(int z) {
         // goog.asserts.assert(this.minZoom <= z && z <= this.maxZoom);
          return this.Resolutions[z];
        }

        /**
         * @param {number} z Z.
         * @return {number} Tile size.
         * @api stable
         */
        public int GetTileSize(int z) {
          if (_tileSize.HasValue) {
            return this._tileSize.Value;
          } else {
         //   goog.asserts.assert(!goog.isNull(this.tileSizes_));
         //   goog.asserts.assert(this.minZoom <= z && z <= this.maxZoom);
            return this._tileSizes[z];
          }
        }
        /**
         * @param {number} z Z.
         * @param {ol.TileRange} tileRange Tile range.
         * @param {ol.Extent=} opt_extent Temporary ol.Extent object.
         * @return {ol.Extent} Extent.
         */
        public double[] GetTileRangeExtent(int z, TileRange tileRange, double[] opt_extent) {
          var origin = this.GetOrigin(z);
          var resolution = this.GetResolution(z);
          var tileSize = this.GetTileSize(z);
          var minX = origin[0] + tileRange.MinX * tileSize * resolution;
          var maxX = origin[0] + (tileRange.MaxX + 1) * tileSize * resolution;
          var minY = origin[1] + tileRange.MinY * tileSize * resolution;
          var maxY = origin[1] + (tileRange.MaxY + 1) * tileSize * resolution;
          return Extent.CreateOrUpdate(minX, minY, maxX, maxY, opt_extent);
        }
        /**
         * @param {number} resolution Resolution.
         * @return {number} Z.
         */
        public int GetZForResolution(double resolution) {
        //  return ol.array.linearFindNearest(this.Resolutions, resolution, 0);
           var idx = Array.BinarySearch(this.Resolutions.ToArray(),resolution);
            if (idx < 0){
                idx = ~idx;
                return this.Resolutions[idx]-resolution < resolution - this.Resolutions[idx-1] ? idx:idx-1;
            }
           return idx;
        }
        /**
         * @param {number} x X.
         * @param {number} y Y.
         * @param {number} resolution Resolution.
         * @param {boolean} reverseIntersectionPolicy Instead of letting edge
         *     intersections go to the higher tile coordinate, let edge intersections
         *     go to the lower tile coordinate.
         * @param {ol.TileCoord=} opt_tileCoord Temporary ol.TileCoord object.
         * @return {ol.TileCoord} Tile coordinate.
         * @private
         */
        private int[] GetTileCoordForXYAndResolution_(double x, double y, double resolution, bool reverseIntersectionPolicy, int[] opt_tileCoord=null) {
          var z = this.GetZForResolution(resolution);
          var scale = resolution / this.GetResolution(z);
          var origin = this.GetOrigin(z);
          var tileSize = this.GetTileSize(z);

          var tileCoordX = scale * (x - origin[0]) / (resolution * tileSize);
          var tileCoordY = scale * (y - origin[1]) / (resolution * tileSize);

          if (reverseIntersectionPolicy) {
            tileCoordX = Math.Ceiling(tileCoordX) - 1;
            tileCoordY = Math.Ceiling(tileCoordY) - 1;
          } else {
            tileCoordX = Math.Floor(tileCoordX);
            tileCoordY = Math.Floor(tileCoordY);
          }

          return TileCoord.CreateOrUpdate(z, tileCoordX, tileCoordY, opt_tileCoord);
        }

        /**
         * @param {ol.Extent} extent Extent.
         * @param {number} resolution Resolution.
         * @param {ol.TileRange=} opt_tileRange Temporary tile range object.
         * @return {ol.TileRange} Tile range.
         */
        public TileRange GetTileRangeForExtentAndResolution(double[] extent, double resolution, TileRange opt_tileRange=null) {
          var tileCoord = new int[3]{0,0,0};
          this.GetTileCoordForXYAndResolution_(
              extent[0], extent[1], resolution, false, tileCoord);
          var minX = tileCoord[1];
          var minY = tileCoord[2];
          this.GetTileCoordForXYAndResolution_(
              extent[2], extent[3], resolution, true, tileCoord);
          return TileRange.CreateOrUpdate(
              minX, tileCoord[1], minY, tileCoord[2], opt_tileRange);
        }
        /**
         * @param {ol.Extent} extent Extent.
         * @param {number} z Z.
         * @param {ol.TileRange=} opt_tileRange Temporary tile range object.
         * @return {ol.TileRange} Tile range.
         */
        public TileRange GetTileRangeForExtentAndZ(double[] extent, int z, TileRange opt_tileRange = null) {
          var resolution = this.GetResolution(z);
          return this.GetTileRangeForExtentAndResolution(
              extent, resolution, opt_tileRange);
        }
        /**
         * @param {ol.TileCoord} tileCoord Tile coordinate.
         * @param {ol.Extent=} opt_extent Temporary extent object.
         * @return {ol.Extent} Extent.
         */
       public double[] GetTileCoordExtent(int[] tileCoord, double[] opt_extent=null) {
          var origin = this.GetOrigin(tileCoord[0]);
          var resolution = this.GetResolution(tileCoord[0]);
          var tileSize = this.GetTileSize(tileCoord[0]);
          var minX = origin[0] + tileCoord[1] * tileSize * resolution;
          var minY = origin[1] + tileCoord[2] * tileSize * resolution;
          var maxX = minX + tileSize * resolution;
          var maxY = minY + tileSize * resolution;
          return Extent.CreateOrUpdate(minX, minY, maxX, maxY, opt_extent);
        }

        public bool ForEachTileCoordParentTileRange(int[] tileCoord, Func<int,TileRange,bool> callback, TileRange opt_tileRange = null, double[] opt_extent=null) 
        {
  
            var tileCoordExtent = this.GetTileCoordExtent(tileCoord, opt_extent);
            var z = tileCoord[0] - 1;
            while (z >= this.MinZoom) {
                if (callback(z,
                    this.GetTileRangeForExtentAndZ(tileCoordExtent, z, opt_tileRange)))
                {
                    return true;
                }
                --z;
            }
            return false;
        }
    }
}
