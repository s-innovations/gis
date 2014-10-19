using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SInnovations.Gis.Vector.EntityFramework
{
    // It seems that this need a cross provider solution.
//    geometry_columns
//        {"DataBaseName":"main","ColumnName":"f_table_name","TableName":"geometry_columns","TableSchema":"sqlite_default_schema","IsNullable":false,"DataType":"text"}
//        {"DataBaseName":"main","ColumnName":"f_geometry_column","TableName":"geometry_columns","TableSchema":"sqlite_default_schema","IsNullable":false,"DataType":"text"}
//        {"DataBaseName":"main","ColumnName":"type","TableName":"geometry_columns","TableSchema":"sqlite_default_schema","IsNullable":false,"DataType":"text"}
//        {"DataBaseName":"main","ColumnName":"coord_dimension","TableName":"geometry_columns","TableSchema":"sqlite_default_schema","IsNullable":false,"DataType":"text"}
//        {"DataBaseName":"main","ColumnName":"srid","TableName":"geometry_columns","TableSchema":"sqlite_default_schema","IsNullable":false,"DataType":"integer"}
//        {"DataBaseName":"main","ColumnName":"spatial_index_enabled","TableName":"geometry_columns","TableSchema":"sqlite_default_schema","IsNullable":false,"DataType":"integer"}

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
}
