using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SInnovations.Gis.Vector.EntityFramework
{
    /// <summary>
    /// Entity that can be used to query tables in a MS SQL database usign the build in 
    /// views from the information schema. 
    /// 
    /// It apears that a cross dataprovider solution is avaible using db.connection.getSchema.
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
        public bool IsNullable { get; set; }
        [Column("DATA_TYPE")]
        public string DataType { get; set; }

    }

        //    TABLE_CATALOG   System.String
        //TABLE_SCHEMA    System.String
        //TABLE_NAME      System.String
        //COLUMN_NAME     System.String
        //COLUMN_GUID     System.Guid
        //COLUMN_PROPID   System.Int64
        //ORDINAL_POSITION        System.Int32
        //COLUMN_HASDEFAULT       System.Boolean
        //COLUMN_DEFAULT  System.String
        //COLUMN_FLAGS    System.Int64
        //IS_NULLABLE     System.Boolean
        //DATA_TYPE       System.String
        //TYPE_GUID       System.Guid
        //CHARACTER_MAXIMUM_LENGTH        System.Int32
        //CHARACTER_OCTET_LENGTH  System.Int32
        //NUMERIC_PRECISION       System.Int32
        //NUMERIC_SCALE   System.Int32
        //DATETIME_PRECISION      System.Int64
        //CHARACTER_SET_CATALOG   System.String
        //CHARACTER_SET_SCHEMA    System.String
        //CHARACTER_SET_NAME      System.String
        //COLLATION_CATALOG       System.String
        //COLLATION_SCHEMA        System.String
        //COLLATION_NAME  System.String
        //DOMAIN_CATALOG  System.String
        //DOMAIN_NAME     System.String
        //DESCRIPTION     System.String
        //PRIMARY_KEY     System.Boolean
        //EDM_TYPE        System.String
        //AUTOINCREMENT   System.Boolean
        //UNIQUE  System.Boolean
}
