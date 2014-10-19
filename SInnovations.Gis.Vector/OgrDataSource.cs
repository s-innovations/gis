using SInnovations.Gis.OgrHelpers;
using SInnovations.Gis.Vector.Layers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SInnovations.Gis.Vector
{
    public class OgrLayer<T> : ILayerContext<T>
    {

        public OgrLayer(OgrDataSource source, string name)
        {
          
            LayerName = name;

            ExtentAsync = source.OgrHelper.GetExtentAsync(source.Source, name);
            Proj4TextAsync = source.OgrHelper.GetProj4TextAsync(source.Source, name);
        }

        public string LayerName
        {
            get;
            set;
        }

        public Task<double[]> ExtentAsync
        {
            get;
            private set;
        }
        public Task<string> Proj4TextAsync
        {
            get;
            private set;
        }

        public IQueryable<T> GetRegion(System.Data.Entity.Spatial.DbGeometry bbox)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }


        public int SaveChanges()
        {
            throw new NotImplementedException();
        }

        public void Add(T entity)
        {
            throw new NotImplementedException();
        }

        public void Delete(int id)
        {
            throw new NotImplementedException();
        }

        public void Update(T entity)
        {
            throw new NotImplementedException();
        }

        public void Add(Newtonsoft.Json.Linq.JToken obj)
        {
            throw new NotImplementedException();
        }
    }

    public class OgrDataSource : IDataSource
    {

        public OgrHelper OgrHelper { get; set; }
         public OgrDataSource(OgrHelper helper, string source)
        {
            Source = source;
            OgrHelper = helper;
        }
        public ILayerContext<T> GetLayerContext<T>(string name)
        {
            return new OgrLayer<T>(this,name);
        }

        public ILayerContext<OgrEntity> GetLayerContext(string name)
        {
            return GetLayerContext<OgrEntity>(name);
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public string CacheDir { get; set; }
        public string Source { get; set; }
    }
}
