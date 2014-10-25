using Microsoft.Owin.Cors;
using Newtonsoft.Json.Serialization;
using Owin;
using SInnovations.Gis.Vector;
using SInnovations.Gis.VectorTiles.Host.WebApi;
using SInnovations.Katana.UnityDependencyResolver.WebApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Formatting;
using System.Text;
using System.Threading.Tasks;
using System.Web.Cors;
using System.Web.Http;
using Microsoft.Practices.Unity;
using Microsoft.Owin;
using System.Data;
using SInnovations.Gis.Vector.EntityFramework;
using Newtonsoft.Json;

namespace SInnovations.Gis.VectorTiles.Host.Owin
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            // For more information on how to configure your application, visit http://go.microsoft.com/fwlink/?LinkID=316888
         //   SqlServerTypes.Utilities.LoadNativeAssemblies(AppDomain.CurrentDomain.BaseDirectory);

            var policy = new CorsPolicy
            {
                AllowAnyHeader = true,
                AllowAnyMethod = true,
                AllowAnyOrigin = true,
                SupportsCredentials = true,

            };
            policy.ExposedHeaders.Add("Location");

            var corsOptions = new CorsOptions
            {
                PolicyProvider = new CorsPolicyProvider
                {
                    PolicyResolver = context => Task.FromResult(policy)
                }
            };
            app.UseCors(corsOptions);
            app.UseUnityContainer();

            var configuration = new HttpConfiguration();
            configuration.MapHttpAttributeRoutes();
            configuration.AddKatanaUnityDependencyResolver();

            var jsonFormatter = new JsonMediaTypeFormatter();
            var settings = jsonFormatter.SerializerSettings;
            settings.ContractResolver = new CamelCasePropertyNamesContractResolver();
            jsonFormatter.SerializerSettings.Converters.Add(new OgrEntityConverter());
            jsonFormatter.SerializerSettings.Converters.Add(new DbGeographyGeoJsonConverter());

            configuration.Services.Replace(typeof(IContentNegotiator), new JsonContentNegotiator(jsonFormatter));

            app.GetUnityContainer().RegisterType<IDataSource>(new HierarchicalLifetimeManager(),
                new InjectionFactory(factory));


            app.Map(new PathString("/api"), builder =>
            {
                builder.UseWebApi(configuration);
            });
        }

        MsSQLContext factory(IUnityContainer container)
        {
            var db = new MsSQLContext(@"test");
            return db;

           // db.Database.Connection.Open();
            //var tables = db.Database.Connection.GetSchema("tables");
            //if (false)
            //    foreach (DataRow row in tables.Rows)
            //    {
            //        Console.WriteLine(string.Join("\t", row.ItemArray));
            //        var tablename = row.ItemArray[2] as string;
            //        //var colums = db.Database.Connection.GetSchema("Columns");
            //        //DataTable myDataTable = db.Database.Connection.GetSchema(
            //        //     "Columns", new string[] { tablename });
            //        Console.WriteLine(tablename);
            //        //foreach (DataColumn column in myDataTable.Columns)
            //        //    Console.WriteLine(string.Format("\t{0}\t{1}\t", column.Caption, column.DataType.ToString()));

            //        Console.WriteLine();
            //        Console.ReadLine();
            //    }

            var colums = db.Database.Connection.GetSchema("Columns");
            foreach (DataColumn column in colums.Columns)
                Console.WriteLine(string.Format("\t{0}\t{1}\t", column.Caption, column.DataType.ToString()));
            Console.ReadKey();

            var tables = colums.Rows.Cast<DataRow>().Select(SchemaToEFColumn).GroupBy(t => new { t.TableName, t.TableSchema }).ToArray();

            foreach (var group in tables)
            {
                var tablename = group.Key.TableName;
                Console.WriteLine(tablename);
                foreach (var row in group)//.Select(row=>new { ColumnName = row.ItemArray[3],  }))
                {
                    Console.Write("\t");
                    Console.WriteLine(JsonConvert.SerializeObject(row));

                }
                Console.WriteLine();
                Console.ReadKey();
            }
            db.Database.Connection.Close();
            return db;
        }

        TableColumn SchemaToEFColumn(DataRow row)
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
    }
}
