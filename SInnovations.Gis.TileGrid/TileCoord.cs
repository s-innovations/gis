using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SInnovations.Gis.TileGrid
{
    public static class TileCoord
    {
        internal static int[] CreateOrUpdate(int z, int tileCoordX, int tileCoordY, int[] opt_tileCoord)
        {
            if(opt_tileCoord !=null)
            {
                opt_tileCoord[0] = z; opt_tileCoord[1] = tileCoordX; opt_tileCoord[2] = tileCoordY;
                return opt_tileCoord;
            }
            return new int[] { z, tileCoordX, tileCoordY };
        }
    }
}
