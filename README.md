gis
===

###Update

I found a cross entity framework provider solution to dynamic looking up tables/layers (before i was relying on SQL server views) and was able to also use SQLite. But with SQLite there is need for creating a seperate implementation that deals with not having DbGeometry.

Also added a command line host using OWIN to test out the tile generation. 

Using a local database, you can import data with ogr2ogr.

```
C:\python\WinPython-64bit-2.7.6.4-gdal\python-2.7.6.amd64>%GDAL%\ogr2ogr -f "MSSQLSpatial" "MSSQL:Server=np:\\.\pipe\LOCALDB#7EA881A9\tsql\query;Database=ogr2ogrtestdb;" "C:\dev\DK_SHAPE_UTM32-EUREF89\MINIMAKS\BASIS\JORDSTYKKE_sonderborg.shp" -t_srs epsg:25832 -overwrite -progress -nln sonderborg_matrikelkort -skipfailures
```
and add a spatial index
```
C:\python\WinPython-64bit-2.7.6.4-gdal\python-2.7.6.amd64>%GDAL%\ogrinfo -sql "create spatial index on sonderborg_matrikelkort" "MSSQL:Server=np:\\.\pipe\LOCALDB#7EA881A9\tsql\query;Database=ogr2ogrtestdb"
```

### Original
My little gis utility project in C#


A small weekend project for working with OGR2OGR imported GIS data to a MSSQL database. Using ENtity Framework one can get quick access to the custom tables and columns created with OGR2OGR.


Below is the test application I used, where i created a VectorTiles server that can return both geojson and topojson. 

First a dbcontext is needed, where the MsSQLContext is used to query for tables and columns in the database dynamic.
```
    public class db : MsSQLContext
    {

        public db()
            : base("testconn")
        {

        }
    }
```

A JsonContentNeotiator was added to webapi (properway to only return json).

```
    public class JsonContentNegotiator : IContentNegotiator
    {
        private readonly JsonMediaTypeFormatter _jsonFormatter;

        public JsonContentNegotiator(JsonMediaTypeFormatter formatter)
        {
            _jsonFormatter = formatter;
        }

        public ContentNegotiationResult Negotiate(Type type, HttpRequestMessage request, IEnumerable<MediaTypeFormatter> formatters)
        {
            var result = new ContentNegotiationResult(_jsonFormatter, new MediaTypeHeaderValue("application/json"));
            return result;
        }
    }
```

then a feature entity was created with the custom column regareal added (OrgEntity has the Geometry Column added dynamic from info avaible in the database).

```
    public class Matrikel : OgrEntity
    {

        [Column("regareal")]
        [JsonProperty("regareal")]
        public double Areal { get; set; }
    }
```
A webApi 2 FileResult HttpActionResult was made to return file streams.
```
    class FileResult : IHttpActionResult
    {
        private readonly string _filePath;
        private readonly string _contentType;

        public FileResult(string filePath, string contentType = null)
        {
            if (filePath == null) throw new ArgumentNullException("filePath");

            _filePath = filePath;
            _contentType = contentType;
        }

        public Task<HttpResponseMessage> ExecuteAsync(CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(File.OpenRead(_filePath))
            };

            var contentType = _contentType;//?? MimeMapping.GetMimeMapping(Path.GetExtension(_filePath));
            response.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

            return Task.FromResult(response);

        }
    }
```
The actual controller that creates geojson and topojson files. (note that I have topojson avaible from node, installed globally such i can call it from cmd).

```
    public class VectorTilesController : ApiController
    {
        //  private db db;
        private string cacheDir;
        public VectorTilesController()
        {
            //  this.db = new db();
            cacheDir = "c:\\geojsonTiles";


        }
        [HttpGet]
        [Route("tiles/{layer}/{z}/{x}/{y}.{format}")]
        public IHttpActionResult Tiles(string layer, int z, int x, int y, string format)
        {

            using (var db = new db())
            {
                int nwX;
                int nwY;

                TileSystem.TileXYToPixelXY(x, y, out nwX, out nwY);

                double[] nw = new double[2] { 0, 0 };
                double[] se = new double[2] { 0, 0 };


                TileSystem.PixelsToMeters(nwX, nwY, z, out nw[0], out nw[1]);
                TileSystem.PixelsToMeters(nwX + 256, nwY - 256, z, out se[0], out se[1]);

                var from = ProjectionInfo.FromEpsgCode(3857);
                var to = ProjectionInfo.FromEpsgCode(25832);
                double[] zo = new double[] { 0 };
                Reproject.ReprojectPoints(nw, null, from, to, 0, 1);
                Reproject.ReprojectPoints(se, null, from, to, 0, 1);
                var boundingBox = GetBoundingBox(nw[0], nw[1], se[0], se[1], 25832);//4326,25832


                var path = Path.Combine(cacheDir, string.Format("{0}/{1}/{2}/{3}.{4}", layer, z, x, y, format));
                if (File.Exists(path))
                    return new FileResult(path, "application/json");

                using (var test = db.GetLayerContext<Matrikel>(layer))
                {
                    var resolution = TileSystem.Resolution(z);
                    var arealLimit = resolution * resolution;
                    var query = test.GetRegion(boundingBox).Where(t => t.Areal > arealLimit);


                    Console.WriteLine("Getting Tile at Resolution {0}", resolution);

                    var result = query.ToArray(); //.Reduce(resolution).ToArray(); ;

                    Directory.CreateDirectory(Path.GetDirectoryName(path));

                    using (FileStream fs = File.Open(Path.ChangeExtension(path, "geojson"), FileMode.Create))
                    using (StreamWriter sw = new StreamWriter(fs))
                    using (JsonWriter jw = new JsonTextWriter(sw))
                    {
                        jw.Formatting = Formatting.Indented;

                        JsonSerializer serializer = new JsonSerializer();
                        serializer.Converters.Add(new OgrEntityConverter());
                        serializer.Converters.Add(new DbGeographyGeoJsonConverter());
                        serializer.Serialize(jw, result);
                    }

                    var p = new ProcessStartInfo("cmd",
                        string.Format("/C topojson -p -o {0}.topojson {0}.geojson", Path.GetFileNameWithoutExtension(path)));
                    p.WorkingDirectory = Path.GetDirectoryName(path);

                    var pro = Process.Start(p);

                    pro.WaitForExit();


                    return new FileResult(path, "application/json");

                }

            }
        }

        private static DbGeometry GetBoundingBox(double nwX, double nwY, double seX, double seY, int epsg)
        {
            CultureInfo ci = new CultureInfo("en-US");

            DbGeometry boundingBox = DbGeometry.FromText(string.Format("POLYGON(({0} {1}, {0} {2}, {3} {2} 0, {3} {1} 0, {0} {1} 0))",
               nwX.ToString(ci),
               nwY.ToString(ci),
                seY.ToString(ci),
                seX.ToString(ci))
                , epsg);
            return boundingBox;
        }
    }
```
Host application for hosting webapi.
```
    public class Program
    {
        static void Main(string[] args)
        {
            int x = 17273;
            int z = 15;
            int y = (1 << z) - 10370;
            string baseAddress = "http://localhost:34400/";

            using (WebApp.Start<Startup>(url: baseAddress))
            {
                // Create HttpCient and make a request to api/values 
                HttpClient client = new HttpClient();

                var response = client.GetAsync(baseAddress +
                    string.Format("api/tiles/sonderborg_airport_matrikelkort1/{0}/{1}/{2}.topojson", z, x, y)).Result;

                Console.WriteLine(response);
                Console.WriteLine(response.Content.ReadAsStringAsync().Result);
                Console.WriteLine("Listening on " + baseAddress);
                Console.ReadLine();
            }


        }

    }
```
