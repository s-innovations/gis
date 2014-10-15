using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Spatial;
using System.Data.Entity.SqlServer;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SInnovations.Gis.VectorTiles.Layers
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
        private string tableName;
        private string idColumn;
     //   private string geomName;
        public VectorLayer(string conn, string tableName, string idColumn, string geom)
            : base(conn)
        {
            this.tableName = tableName;
            this.idColumn = idColumn;
            this.GeomColumn = geom;

        }
        public string GeomColumn { get; set; }
        
        public DbSet<T> Layer { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<T>().Property(t => t.Id).HasColumnName(this.idColumn);
            modelBuilder.Entity<T>().HasKey(t => t.Id);
            modelBuilder.Entity<T>().ToTable(this.tableName);
            modelBuilder.Entity<T>().Property(t => t.Geometry).HasColumnName(this.GeomColumn);


            base.OnModelCreating(modelBuilder);
        }
        public IQueryable<T1> GetRegion(DbGeometry bbox)
        {
            return Layer.Where(c => c.Geometry.Intersects(bbox));
        }

        private T GetById(int id)
        {
            return Layer.Find(id);
        }

        public void Add(T1 entity)
        {
            var instance = Convert(entity);
            DbEntityEntry dbEntityEntry = Entry(instance);
            if (dbEntityEntry.State != EntityState.Detached)
            {
                dbEntityEntry.State = EntityState.Added;
            }
            else
            {
                Layer.Add(instance);
            }
        }

        private void Delete(T entity)
        {
            DbEntityEntry dbEntityEntry = Entry(entity);
            if (dbEntityEntry.State != EntityState.Deleted)
            {
                dbEntityEntry.State = EntityState.Deleted;
            }
            else
            {
                Layer.Attach(entity);
                Layer.Remove(entity);
            }
        }
        public void Update(T1 entity)
        {
            var instance = Convert(entity);
            DbEntityEntry dbEntityEntry = Entry(instance);
            if (dbEntityEntry.State == EntityState.Detached)
            {
                Layer.Attach(instance);
            }
            dbEntityEntry.State = EntityState.Modified;
        }

        public void Delete(int id)
        {
            var entity = GetById(id);
            if (entity == null) return;
            Delete(entity);
        }

        public override int SaveChanges()
        {
            return base.SaveChanges();
        } 
        
        private T Convert(T1 entity)
        {
            var instance = Activator.CreateInstance(typeof(T)) as T;
            var propsT = typeof(T).GetProperties();
            var propsT1Names = typeof(T1).GetProperties().Select(p => p.Name);
            foreach (var prop in propsT)
            {
                if (propsT1Names.Contains(prop.Name))
                {
                    prop.SetValue(instance, prop.GetValue(entity));
                }
            }

            return instance;
        }
    }
}
