using System;
using System.Collections.Generic;
using System.Data.Entity.Spatial;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SInnovations.Gis.VectorTiles.Layers
{
    public interface ILayerContext : ILayerContext<OgrEntity>
    {
    }
    public interface ILayerContext<T> : IDisposable
    {
        IQueryable<T> GetRegion(DbGeometry bbox);
    }
}
