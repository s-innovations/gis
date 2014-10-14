using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SInnovations.Gis.VectorTiles.EntityFramework
{
    /// <summary>
    /// Entity that can be used to query tables in a MS SQL database usign the build in 
    /// views from the information schema.    /// 
    /// </summary>
    [Table("COLUMNS", Schema = "INFORMATION_SCHEMA")]
    public class TableColumn
    {

        [Column("TABLE_CATALOG")]
        public string DataBaseName { get; set; }
        [Key]
        [Column("COLUMN_NAME")]
        public string ColumnName { get; set; }
        [Column("TABLE_NAME")]
        public string TableName { get; set; }
        [Column("TABLE_SCHEMA")]
        public string TableSchema { get; set; }
        [Column("IS_NULLABLE")]
        public string IsNullable { get; set; }
        [Column("DATA_TYPE")]
        public string DataType { get; set; }

    }
}
