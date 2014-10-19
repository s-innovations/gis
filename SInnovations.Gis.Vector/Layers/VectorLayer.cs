using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Spatial;
using System.Data.Entity.SqlServer;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SInnovations.Gis.Vector.Layers
{
    public static  class VectorLayerHelperExtensions
    {
        public static IEnumerable<OgrEntity> Reduce(this IQueryable<OgrEntity> query, double threshold)
        {
            return query.Select(s => new
                      {
                          g = SqlSpatialFunctions.Reduce(s.Geometry, threshold),
                          s
                      }).AsEnumerable().Select(t => { t.s.Geometry = t.g; return t.s; });
        }
    }
    public class VectorLayer<T,T1> : DbContext, ILayerContext<T1> where T : OgrEntity ,T1
    {
      //  private string tableName;
        private string idColumn;
        private JsonSerializerSettings settings;
     //   private string geomName;
        public VectorLayer(string conn, string tableName, string idColumn, string geom)
            : base(conn)
        {
          //  this.tableName = tableName;
            this.LayerName = tableName;
            this.idColumn = idColumn;
            this.GeomColumn = geom;

            //Used for Deserialzation
            this.settings = new JsonSerializerSettings();
            settings.Converters.Add(new DbGeographyGeoJsonConverter());
            settings.Converters.Add(new OgrEntityConverter());
         
        }

        public string LayerName { get; set; }

        public string GeomColumn { get; set; }
        
        public DbSet<T> Layer { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<T>().Property(t => t.Id).HasColumnName(this.idColumn);
            modelBuilder.Entity<T>().HasKey(t => t.Id);
            modelBuilder.Entity<T>().ToTable(this.LayerName);
            modelBuilder.Entity<T>().Property(t => t.Geometry).HasColumnName(this.GeomColumn);


            base.OnModelCreating(modelBuilder);
        }
        public IQueryable<T1> GetRegion(DbGeometry bbox)
        {
            return Layer.Where(c => c.Geometry.Intersects(bbox));
        }
        
   
        public void Add(JToken obj)
        {
            if(obj.Type == JTokenType.Array)
                Layer.AddRange(obj.ToObject<T[]>(JsonSerializer.Create(settings)));
            else
                Layer.Add(obj.ToObject<T>(JsonSerializer.Create(settings)));
        }
      
    }
}
