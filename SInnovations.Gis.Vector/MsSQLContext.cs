using SInnovations.Gis.Vector.EntityFramework;
using SInnovations.Gis.Vector.Layers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using PropertyAttributes = System.Reflection.PropertyAttributes;

namespace SInnovations.Gis.Vector
{
    public interface IDataSource : IDisposable
    {
        ILayerContext<T> GetLayerContext<T>(string table);
        ILayerContext<OgrEntity> GetLayerContext(string table);
    }

   
    public class MsSQLContext : DbContext, IDataSource
    {
        private static ConcurrentDictionary<string, Tuple<Type, string, string>> types = new ConcurrentDictionary<string, Tuple<Type, string, string>>();
        private string conn;
        private bool isSqlLite = false;
        public MsSQLContext(string conn,bool isSqlLite=false)
            : base(conn)
        {
            this.conn = conn;
            this.isSqlLite = isSqlLite;
           
        }
        public DbSet<GeometryColumn> GeometryColumns { get; set; }
      //  public DbSet<TableColumn> Columns { get; set; }

        public ILayerContext<OgrEntity> GetLayerContext(string table)
        {
            return GetLayerContext<OgrEntity>(table);
        }
        public ILayerContext<T> GetLayerContext<T>(string table)
        {

            var entity = types.GetOrAdd(table, (key) =>
            {
                var ignores = new List<string>();
                if(typeof(T) != typeof(OgrEntity))
                {
                    var props = typeof(T).GetProperties().Where(
                        prop => Attribute.IsDefined(prop, typeof(ColumnAttribute)));
                    ignores.AddRange(props.Select(p => p.GetCustomAttribute<ColumnAttribute>().Name));
                }

                TypeBuilder builder = CreateTypeBuilder<T>(
                        "MyDynamicAssembly", "MyModule",key.Replace("_",""));


                Database.Connection.Open();
                var colums = Database.Connection.GetSchema("Columns");
               
              //  var tables = Columns.GroupBy(t => new { t.TableName, t.TableSchema }).ToArray();
                var tables = isSqlLite ? 
                    colums.Rows.Cast<DataRow>().Select(SQLiteSchemaToEFColumn).GroupBy(t => new { t.TableName, t.TableSchema }).ToArray() :
                    colums.Rows.Cast<DataRow>().Select(MSSQLSchemaToEFColumn).GroupBy(t => new { t.TableName, t.TableSchema }).ToArray();
                

                Database.Connection.Close();

#if DEBUG
                Console.WriteLine(string.Join("\n",
                    tables.Select(d => string.Format("{0}:\n\t{1}",
                    d.Key.TableName, string.Join("\n\t",
                        d.Select(column => string.Format("{0}\t{1}", column.ColumnName, column.DataType)))))));
#endif

                var columns = tables.FirstOrDefault(g => g.Key.TableName == table);
                //   var ignores = new List<string> { "ogr_fid", "ogr_geometry" };
                var geom_column = "";
                string id_column = null;
                foreach (var column in columns)
                {

                    if (column.DataType.Equals("geometry", StringComparison.OrdinalIgnoreCase) || column.DataType.Equals("multipolygon", StringComparison.OrdinalIgnoreCase))
                    {
                        geom_column = column.ColumnName;
                        continue;
                    }
                    else if (IsIdColumn(column) && id_column == null)
                    {
                        id_column = column.ColumnName;
                        continue;
                    }
                    if (ignores.Contains(column.ColumnName))
                        continue;

                    CreateAutoImplementedProperty(builder, column.ColumnName,
                        dbtypetoclrtype(column.DataType, column.IsNullable));
                }



                return new Tuple<Type, string, string>(builder.CreateType(), id_column, geom_column);
               
            });
            var type = typeof(VectorLayer<,>).MakeGenericType(entity.Item1,typeof(T));

            return Activator.CreateInstance(type,this.conn, table, entity.Item2, entity.Item3) as ILayerContext<T>;
        }

        private static bool IsIdColumn(TableColumn column)
        {
            return (column.DataType == "int" || column.DataType == "integer") && !column.IsNullable;
        }

        private static TypeBuilder CreateTypeBuilder<T>(
           string assemblyName, string moduleName, string typeName)
        {
            TypeBuilder typeBuilder = AppDomain
                .CurrentDomain
                .DefineDynamicAssembly(new AssemblyName(assemblyName),
                                       AssemblyBuilderAccess.Run)
                .DefineDynamicModule(moduleName)
                .DefineType(typeName, TypeAttributes.Public, typeof(T));
            typeBuilder.DefineDefaultConstructor(MethodAttributes.Public);

            return typeBuilder;
        }

        private static void CreateAutoImplementedProperty(
            TypeBuilder builder, string propertyName, Type propertyType)
        {
            const string PrivateFieldPrefix = "m_";
            const string GetterPrefix = "get_";
            const string SetterPrefix = "set_";

            // Generate the field.
            FieldBuilder fieldBuilder = builder.DefineField(
                string.Concat(PrivateFieldPrefix, propertyName),
                              propertyType, FieldAttributes.Private);

            // Generate the property
            PropertyBuilder propertyBuilder = builder.DefineProperty(
                propertyName, PropertyAttributes.HasDefault, propertyType, null);

            // Property getter and setter attributes.
            MethodAttributes propertyMethodAttributes =
                MethodAttributes.Public | MethodAttributes.SpecialName |
                MethodAttributes.HideBySig;

            // Define the getter method.
            MethodBuilder getterMethod = builder.DefineMethod(
                string.Concat(GetterPrefix, propertyName),
                propertyMethodAttributes, propertyType, Type.EmptyTypes);


            // Emit the IL code.
            // ldarg.0
            // ldfld,_field
            // ret
            ILGenerator getterILCode = getterMethod.GetILGenerator();
            getterILCode.Emit(OpCodes.Ldarg_0);
            getterILCode.Emit(OpCodes.Ldfld, fieldBuilder);
            getterILCode.Emit(OpCodes.Ret);

            // Define the setter method.
            MethodBuilder setterMethod = builder.DefineMethod(
                string.Concat(SetterPrefix, propertyName),
                propertyMethodAttributes, null, new Type[] { propertyType });

            // Emit the IL code.
            // ldarg.0
            // ldarg.1
            // stfld,_field
            // ret
            ILGenerator setterILCode = setterMethod.GetILGenerator();
            setterILCode.Emit(OpCodes.Ldarg_0);
            setterILCode.Emit(OpCodes.Ldarg_1);
            setterILCode.Emit(OpCodes.Stfld, fieldBuilder);
            setterILCode.Emit(OpCodes.Ret);

            propertyBuilder.SetGetMethod(getterMethod);
            propertyBuilder.SetSetMethod(setterMethod);

        }


        private static System.Type dbtypetoclrtype(string p, bool isnullable)
        {

            switch (p)
            {
                case "varchar":
                    return typeof(string);
                case "float":
                    return isnullable ? typeof(double?) : typeof(double);
                case "date":
                    return isnullable ? typeof(DateTime?) : typeof(DateTime);
                case "numeric":
                    return isnullable? typeof(Decimal?) : typeof(Decimal);
                case "int":
                case "integer":
                    return isnullable? typeof(int?) : typeof(int);
                default:
                    return typeof(string);
            }
        }
        TableColumn SQLiteSchemaToEFColumn(DataRow row)
        {
            return new TableColumn
            {
                DataBaseName = row.Field<string>("TABLE_CATALOG"),
                ColumnName = row.Field<string>("COLUMN_NAME"),
                TableName = row.Field<string>("TABLE_NAME"),
                DataType = row.Field<string>("DATA_TYPE"),
                IsNullable = row.Field<bool>("IS_NULLABLE"),
                TableSchema = row.Field<string>("TABLE_SCHEMA")

            };
        }
        TableColumn MSSQLSchemaToEFColumn(DataRow row)
        {
            return new TableColumn
            {
                DataBaseName = row.Field<string>("TABLE_CATALOG"),
                ColumnName = row.Field<string>("COLUMN_NAME"),
                TableName = row.Field<string>("TABLE_NAME"),
                DataType = row.Field<string>("DATA_TYPE"),
                IsNullable = row.Field<string>("IS_NULLABLE").Equals("YES",StringComparison.OrdinalIgnoreCase),
                TableSchema = row.Field<string>("TABLE_SCHEMA")

            };
        }
    }
}
