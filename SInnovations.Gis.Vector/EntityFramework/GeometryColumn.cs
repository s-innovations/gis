using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SInnovations.Gis.VectorTiles.EntityFramework
{
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
