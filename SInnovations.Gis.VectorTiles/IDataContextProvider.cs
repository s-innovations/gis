using SInnovations.Gis.Vector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SInnovations.Gis.VectorTiles
{
    public interface IDataContextProvider
    {
        IDataSource GetDataSourceForLayer(string layer);
    }
}
