using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using System.Data.Entity.Core.Metadata.Edm;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SInnovations.Gis.VectorTiles.EntityFramework
{
    public class EntityFrameworkSchemaColumn
    {
        public string Name { get; set; }
        public string Type { get; set; }

    }
    public class EntityFrameworkSchemaResult
    {
        public bool Success { get; set; }
        public IList<EdmProperty> Columns { get; set; }
    }
    [Table("geometry_columns")]
    public class GeometryColumn
    {
        [Key]
        [Column("f_table_catalog", Order = 1)]

        public string TableCatalog { get; set; }
        [Key]
        [Column("f_table_schema", Order = 2)]
        public string TableSchema { get; set; }
        [Key]
        [Column("f_table_name", Order = 3)]
        public string TableName { get; set; }
        [Key]
        [Column("f_geometry_column", Order = 4)]
        public string GeometryColumnName { get; set; }
    }
    public static class EntityFrameworkSchema
    {
        public static EntityFrameworkSchemaResult GetColumns<T>(this T dbContext) where T : DbContext
        {

            var columnsSet = dbContext.Set<GeometryColumn>();
            var columns = columnsSet.First();

            var KeyPropertiesNames = new Dictionary<string, string[]>();
            var KeyPropertiesTypes = new Dictionary<string, string[]>();
            var TableSchema = new Dictionary<string, Tuple<string, string>>();

            var ConnectionString = dbContext.Database.Connection.ConnectionString;
            var metadata = ((IObjectContextAdapter)dbContext).ObjectContext.MetadataWorkspace;

            var tables1 = (from meta in metadata.GetItems(DataSpace.SSpace)
                           where meta.BuiltInTypeKind == BuiltInTypeKind.EntityType
                           select (meta as EntityType).Name).ToArray();
            var tables = metadata.GetItemCollection(DataSpace.SSpace)
              .GetItems<EntityContainer>()
              .Single()
              .BaseEntitySets
              .OfType<EntitySet>()
              .Where(s => !s.MetadataProperties.Contains("Type")
                || s.MetadataProperties["Type"].ToString() == "Tables").ToArray();
            //var types = metadata.GetItemCollection(DataSpace.SSpace)
            //  .GetItems<EntityContainer>()
            //  .Single()
            //  .BaseEntitySets
            //  .OfType<EntitySet>()
            //  .Where(s => !s.MetadataProperties.Contains("Type")
            //    || s.MetadataProperties["Type"].ToString() == "Tables").ToArray();

            // if (tables.Length != types.Length)
            //     return new EntityFrameworkSchemaResult();

            for (int i = 0; i < tables.Length; ++i)
            {
                var table = tables[i];
                //  var type = types[i];
                //  var type = types.Single(t => t.ElementType t.Name == table.Name);

                var tableName = table.MetadataProperties.Contains("Table")
                        && table.MetadataProperties["Table"].Value != null
                      ? table.MetadataProperties["Table"].Value.ToString()
                      : table.Name;

                var tableSchema = table.MetadataProperties["Schema"].Value.ToString();

                //   if (!(tableName == targetTableName && targetSchema == tableSchema))
                //       continue;

                //     KeyPropertiesNames.Add(table.ElementType.Name, type.ElementType.KeyProperties.Select(m => m.Name).ToArray());
                //     KeyPropertiesTypes.Add(table.ElementType.Name, type.ElementType.KeyProperties.Select(m => m.TypeName).ToArray());
                //     TableSchema.Add(table.ElementType.Name, new Tuple<string, string>(tableName, tableSchema));
                var query = (from meta in metadata.GetItems(DataSpace.SSpace)
              .Where(m => m.BuiltInTypeKind == BuiltInTypeKind.EntityType)
                             let properties = meta is EntityType ? (meta as EntityType).Properties : null
                             select new
                             {
                                 TableName = (meta as EntityType).Name,
                                 Fields = from p in properties
                                          select new
                                          {
                                              FielName = p.Name,
                                              DbType = p.TypeUsage.EdmType.Name
                                          }
                             }).ToArray();

              
                return new EntityFrameworkSchemaResult() { Success = true, Columns = new List<EdmProperty>() };

            }

            return new EntityFrameworkSchemaResult();
        }
    }
}
