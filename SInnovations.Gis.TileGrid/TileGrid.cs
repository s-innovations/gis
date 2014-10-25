using DotSpatial.Projections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace SInnovations.Gis.TileGrid
{

    public class TileGridOptions
    {
        public double[] Origin { get; set; }
    
        public  int MinZoom { get; set; }
    
        public  double[] Resolutions { get; set; }
        public double[] Extent { get; set; }


        public int? MaxZoom { get; set; }
    
public  int TileSize { get; set; }}

    public class CoordinateTreeWalker : IPropagatorBlock<TileRange,TileRange>
    {
        public IPropagatorBlock<int[], TileRange>[] coords { get; set; }
        public IPropagatorBlock<TileRange, int[]>[] tiles { get; set; }

        public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader, TileRange messageValue, ISourceBlock<TileRange> source, bool consumeToAccept)
        {
            return block.OfferMessage(messageHeader, messageValue, source, consumeToAccept);
        }

        //public override string ToString()
        //{
        //    var builder = new StringBuilder();
        //    for(var i = 0; i< coords.Length;++i)
        //    {
        //        builder.AppendLine(string.Format("{0}",tiles[i].))
        //    }
        //    return builder.ToString();
        //}

        public void Complete()
        {
            block.Complete();
        }

        public Task Completion
        {
            get { return block.Completion; }
        }

        public void Fault(Exception exception)
        {
            block.Fault(exception);
        }

        public TileRange ConsumeMessage(DataflowMessageHeader messageHeader, ITargetBlock<TileRange> target, out bool messageConsumed)
        {
            return block.ConsumeMessage(messageHeader, target, out messageConsumed);
        }

        public IDisposable LinkTo(ITargetBlock<TileRange> target, DataflowLinkOptions linkOptions)
        {
            return block.LinkTo(target, linkOptions);
        }

        public void ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<TileRange> target)
        {
            block.ReleaseReservation(messageHeader, target);
        }

        public bool ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<TileRange> target)
        {
            return block.ReserveMessage(messageHeader, target);
        }

        public IPropagatorBlock<TileRange, TileRange> block { get; set; }
    }

    public class TileGrid
    {
        private double[] _origin { get; set; }
        private IList<double[]> _origins { get; set; }
        private IList<int> _tileSizes { get; set; }
        private int? _tileSize { get; set; }

        protected int MinZoom { get; set; }
        protected int MaxZoom { get; set; }
        protected double[] Resolutions { get; set; }


        public TileGrid(TileGridOptions options)
        {
           MinZoom = options.MinZoom;
           Resolutions = options.Resolutions;
           MaxZoom =  options.MaxZoom?? Resolutions.Length -1;
           _origin = options.Origin;
           _tileSize = 256;
        }


        public virtual Func<int[], ProjectionInfo, int[], int[]> CreateTileCoordTransform()
        {
            return null;
        }

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
 * @param {ol.Extent} extent Extent.
 * @param {number=} opt_maxZoom Maximum zoom level (default is
 *     ol.DEFAULT_MAX_ZOOM).
 * @param {number=} opt_tileSize Tile size (default uses ol.DEFAULT_TILE_SIZE).
 * @param {ol.extent.Corner=} opt_corner Extent corner (default is
 *     ol.extent.Corner.BOTTOM_LEFT).
 * @return {ol.tilegrid.TileGrid} TileGrid instance.
 */
        public static TileGrid CreateForExtent(double[] extent, int maxZoom=32, int tileSize=256, Extent.Corner corner=Extent.Corner.BottomLeft) {

 

  var resolutions = TileGrid.ResolutionsFromExtent(
      extent, maxZoom, tileSize);

    return new TileGrid( new TileGridOptions{
        Origin= Extent.GetCorner(extent, corner),
        Resolutions= resolutions,
        TileSize= tileSize
        });
    }


        /**
         * Create a resolutions array from an extent.  A zoom factor of 2 is assumed.
         * @param {ol.Extent} extent Extent.
         * @param {number=} opt_maxZoom Maximum zoom level (default is
         *     ol.DEFAULT_MAX_ZOOM).
         * @param {number=} opt_tileSize Tile size (default uses ol.DEFAULT_TILE_SIZE).
         * @return {!Array.<number>} Resolutions array.
         */
        public static double[] ResolutionsFromExtent(double[] extent, int maxZoom=31, int tileSize=256, double basePow=2) {
         

          var height =Extent.GetHeight(extent);
          var width = Extent.GetWidth(extent);
         

          var maxResolution = Math.Max(
              width / tileSize, height / tileSize);

          var length = maxZoom + 1;
          var resolutions = new double[length];
          for (var z = 0; z < length; ++z) {
              resolutions[z] = maxResolution / Math.Pow(basePow, z);
          }
          return resolutions;
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
        public double[] GetTileRangeExtent(int z, TileRange tileRange, double[] opt_extent =null) {
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

            return this.Resolutions.Select((r, i) => new { r = Math.Abs(r - resolution), i }).OrderBy(r => r.r).First().i;
        //  return ol.array.linearFindNearest(this.Resolutions, resolution, 0);
            var resol = this.Resolutions.Reverse().ToArray();
            var idx = resol.Length- Array.BinarySearch(resol, resolution)-1;
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

          return TileCoord.CreateOrUpdate(z, (int)tileCoordX,(int) tileCoordY, opt_tileCoord);
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

        /**
         * @param {ol.TileCoord} tileCoord Tile coordinate.
         * @param {ol.TileRange=} opt_tileRange Temporary ol.TileRange object.
         * @param {ol.Extent=} opt_extent Temporary ol.Extent object.
         * @return {ol.TileRange} Tile range.
         */
        public virtual TileRange GetTileCoordChildTileRange (int[] tileCoord, TileRange opt_tileRange=null, double[] opt_extent=null) {
          if (tileCoord[0] < this.MaxZoom) {
            var tileCoordExtent = this.GetTileCoordExtent(tileCoord, opt_extent);
            return this.GetTileRangeForExtentAndZ(
                tileCoordExtent, tileCoord[0] + 1, opt_tileRange);
          } else {
            return null;
          }
        }
       public virtual bool ForEachTileCoordParentTileRange(int[] tileCoord, Func<int, TileRange, bool> callback, TileRange opt_tileRange = null, double[] opt_extent = null) 
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

        public IPropagatorBlock<TileRange,TileRange> CreateCoordinateTreeWalker(double[]originalExtent, 
            Func<int[],int[],double[],Task> callback,int?startz=null,int?endz=null)
       {
           var minzoom = startz ?? MinZoom;
           var maxzoom = (endz ?? MaxZoom);

           var coords = new IPropagatorBlock<int[], TileRange>[maxzoom - minzoom + 1];
           var tiles = new IPropagatorBlock<TileRange, int[]>[maxzoom - minzoom + 1];
          
            var coordTransform = CreateTileCoordTransform();
            var to = ProjectionInfo.FromEpsgCode(3857);
           Func<int[], Task<TileRange>> creator = async (coord) =>
           {
               var parentCoord = new int[] { coord[0] - 1, coord[1] >> 1, coord[2] >> 1 };
               var xyz = coordTransform(coord, null, null);
               var parentXYZ = coordTransform(parentCoord, null, null);
             //  Console.WriteLine(String.Join(", ", xyz));
               var tileExtent = GetTileCoordExtent(coord);


               await callback(xyz, parentXYZ, tileExtent);
               var range = GetTileCoordChildTileRange(coord);
               return range;
           };

           var source = new BufferBlock<TileRange>();

           for (int z = minzoom; z <= maxzoom; ++z)
           {
                //int zoom = z;
               int zoomIdx = z - minzoom;
               tiles[zoomIdx] = TileRange.CreateTileRangePropagatorBlock(z + 1, GetTileRangeForExtentAndZ(originalExtent, z + 1));
               coords[zoomIdx] = new TransformBlock<int[], TileRange>(creator);
               tiles[zoomIdx].LinkTo(coords[zoomIdx]);
               tiles[zoomIdx].Completion.ContinueWith(delegate
               {
                   Console.WriteLine("Completing coords[{0}]", zoomIdx + minzoom);
                   coords[zoomIdx].Complete();
               });

               if (z != minzoom)
               {
                   coords[zoomIdx - 1].LinkTo(tiles[zoomIdx]);
                   coords[zoomIdx - 1].Completion.ContinueWith(delegate
                   {
                       Console.WriteLine("Completing tiles[{0}]", zoomIdx + minzoom);
                       tiles[zoomIdx].Complete();
                   });
               }

               if (z == maxzoom)
               {
                   coords[zoomIdx].LinkTo(source);
                   coords[zoomIdx].Completion.ContinueWith(delegate
                   {
                       Console.WriteLine("Completing source[{0}]", zoomIdx + minzoom);
                       source.Complete();
                   });
               }

           }

           return new CoordinateTreeWalker
           {
               coords = coords,
               tiles = tiles,
               block = DataflowBlock.Encapsulate(tiles[0], source)
           };

       }

    }
}
