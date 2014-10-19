using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data.Entity.Spatial;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SInnovations.Gis.Vector.Layers
{
    public interface ILayerContext : ILayerContext<OgrEntity>
    {
    }
    public interface ILayerContext<T> : IDisposable
    {
        string LayerName { get; set; }
        IQueryable<T> GetRegion(DbGeometry bbox);
        int SaveChanges();
        void Add(T entity);
        void Delete(int id);
        void Update(T entity);
        void Add(JToken obj);
    }
}
