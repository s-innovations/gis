using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SInnovations.Gis.TileGrid
{
    public class TileRange
    {
        public int MinX { get; set; }

        public int MaxX { get; set; }

        public int MinY { get; set; }

        public int MaxY { get; set; }

        internal static TileRange CreateOrUpdate(int minX, int p1, int minY, int p2, TileRange opt_tileRange)
        {
            throw new NotImplementedException();
        }
    }
}
