using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace SInnovations.Gis.TileGrid
{
   
    public class TileRange
    {
        #region TPL Dataflows
        public static IPropagatorBlock<int[],TileRange> CreateTileChildRangePropagatorBlock(TileGrid grid)
        {
            var source = new BufferBlock<TileRange>();
            var target = new ActionBlock<int[]>((coord) =>{
                source.Post(grid.GetTileCoordChildTileRange(coord));
                
            });

            // When the target is set to the completed state, propagate out any 
            // remaining data and set the source to the completed state.
            target.Completion.ContinueWith(delegate
            {

                source.Complete();
            });

            return DataflowBlock.Encapsulate(target, source);
           
        }
        public static IPropagatorBlock<TileRange, int[]> CreateTileRangePropagatorBlock(int z, TileRange bounds=null)
        {
           // var coordTransform = grid.CreateTileCoordTransform();
           

          
            var source = new BufferBlock<int[]>();

            var target = new ActionBlock<TileRange>((tilerange) => {
               
                for(int x =tilerange.MinX;x<=tilerange.MaxX;++x)
                    for(int y = tilerange.MinY;y<=tilerange.MaxY;++y)
                {
                    if (bounds != null && !bounds.ContainsXY(x, y))
                        continue;

                    var tilecoord=new int[] { z, x, y };
                    source.Post(tilecoord);
                }

            });

            // When the target is set to the completed state, propagate out any 
            // remaining data and set the source to the completed state.
            target.Completion.ContinueWith(delegate
            {
               
                source.Complete();
            });

            return DataflowBlock.Encapsulate(target, source);
          

        }
        #endregion
        public TileRange(int minX, int maxX, int minY, int maxY)
        {
            MinX = minX;
            MaxX = maxX;
            MinY = minY;
            MaxY = maxY;
        }
        public int MinX { get; set; }

        public int MaxX { get; set; }

        public int MinY { get; set; }

        public int MaxY { get; set; }

        internal static TileRange CreateOrUpdate(int minX, int maxX, int minY, int maxY, TileRange tileRange = null)
        {
            if (tileRange != null)
            {
                tileRange.MinX = minX;
                tileRange.MaxX = maxX;
                tileRange.MinY = minY;
                tileRange.MaxY = maxY;
                return tileRange;
            }
            else
            {
                return new TileRange(minX, maxX, minY, maxY);
            }
        }
        /**
         * @param {number} x Tile coordinate x.
         * @param {number} y Tile coordinate y.
         * @return {boolean} Contains coordinate.
         */
       public bool ContainsXY(int x, int y) {
          return this.MinX <= x && x <= this.MaxX && this.MinY <= y && y <= this.MaxY;
        }

       public IEnumerable<TileRange> Split(int xtarget, int ytarget)
       {
          for(int x = MinX;x<=MaxX;x+=xtarget+1)
          {
              for(int y = MinY;y<=MaxY;y+=ytarget+1)
              {
                  yield return new TileRange(x, x + xtarget, y, y + ytarget);
              }
          }
       }
    }
}
