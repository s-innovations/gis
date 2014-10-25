using CommandLine;
using DotSpatial.Projections;
using Microsoft.Owin.Hosting;
using SInnovations.Gis.OgrHelpers;
using SInnovations.Gis.TileGrid;
using SInnovations.Gis.TileGrid.Grids;
using SInnovations.Gis.Vector;
using SInnovations.Gis.VectorTiles.Host.Owin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace SInnovations.Gis.VectorTiles.Host
{
    public class ProgramOptions
    {
        public ProgramOptions()
        {
          //  Items = new List();
        }
        [Option('s', "serve",HelpText="Run a development server for access to tiles")]
        public bool Serve { get; set; }

          
        [Option("cache-dir", HelpText = "CacheDir")]
        public string CacheDir { get; set; }

        [Option('i',"source")]
        public string Source { get; set; }
        [Option("fill-cache")]
        public bool FillCache { get; set; }
         
        [ValueList(typeof(List<string>), MaximumElements = 3)]
        public IList<string> Items { get; set; }
    }
    class Program
    {
        static void Main(string[] args)
        {

            var options = new ProgramOptions();
            if (CommandLine.Parser.Default.ParseArguments(args, options))
            {
                if(options.Source == null && options.Items!=null)
                {
                    options.Source = options.Items.FirstOrDefault();
                    options.Items = options.Items.Skip(1).ToList();
                }


            }

            int x = 17273;

            int z = 15;
            int y = (1 << z) - 10370;

          
           // TestWithSplittingLargeShapeFIle(options, z);
            //Conclussion, Use a DB with proper indexing.

                       
            string baseAddress = "http://localhost:34400/";

            using (WebApp.Start<Startup>(url: baseAddress))
            {
                // Create HttpCient and make a request to api/values 
                HttpClient client = new HttpClient();

                var response = client.GetAsync(baseAddress +
                    string.Format("api/tiles/sonderborg_matrikelkort/{0}/{1}/{2}.geojson", z, x, y)).Result;
                //or topojson
                Console.WriteLine(response);
                Console.WriteLine(response.Content.ReadAsStringAsync().Result);
                Console.WriteLine("Listening on " + baseAddress);
                Console.ReadLine();
            }



        }

        private static void TestWithSplittingLargeShapeFIle(ProgramOptions options, int z)
        {
            var helper = new OgrHelper(new DefaultEnvironmentVariableProvider(
                new Dictionary<string, string>{
               { "gdal" , @"C:\python\WinPython-64bit-2.7.6.4-gdal\tools\gdal"},
               { "GDAL_DATA",@"C:\python\WinPython-64bit-2.7.6.4-gdal\tools\gdal-data"}
               }));

            var source = new OgrDataSource(helper, options.Source);
            source.CacheDir = options.CacheDir;



            var layer = source.GetLayerContext("jordstykke") as OgrLayer<OgrEntity>;
            var extent = layer.ExtentAsync.Result;
            var from = ProjectionInfo.FromProj4String(layer.Proj4TextAsync.Result);
            var to = ProjectionInfo.FromEpsgCode(3857);
            Reproject.ReprojectPoints(extent, null, from, to, 0, 2);

            var grid = new XYZTileGrid(new TileGrid.TileGridOptions
            {
                MinZoom = 0,
            });


            TileRange range;
            do
            {
                range = grid.GetTileRangeForExtentAndZ(extent, z);

            } while (!(range.MinX == range.MaxX && range.MinY == range.MaxY) && z-- > 0);


            range = grid.GetTileCoordChildTileRange(new int[] { z, range.MinX, range.MinY });


            var createOriginalVectorBlock = grid.CreateCoordinateTreeWalker(extent, async (xyz, parent, tileExtent) =>
            {
                var targetPath = string.Format(@"C:\TestCacheDir\{0}\{1}\{2}.shp", xyz[0], xyz[1], xyz[2]);
                var sourcePath = string.Format(@"C:\TestCacheDir\{0}\{1}\{2}.shp", parent[0], parent[1], parent[2]);

                if (!File.Exists(targetPath))
                {
                    if (!File.Exists(sourcePath))
                    {
                        sourcePath = @"C:\dev\DK_SHAPE_UTM32-EUREF89\MINIMAKS\BASIS\JORDSTYKKE.shp";
                        Reproject.ReprojectPoints(tileExtent, null, to, from, 0, 2);
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                    try
                    {
                        var exitcode = await helper.Ogr2OgrClipAsync(
                                sourcePath, targetPath, "epsg:" + to.AuthorityCode,
                                tileExtent);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                    }

                }
            }, z, 5);
            createOriginalVectorBlock.LinkTo(new ActionBlock<TileRange>((r) => { }));
            createOriginalVectorBlock.Post(range);
            createOriginalVectorBlock.Complete();
            createOriginalVectorBlock.Completion.Wait();
            
        }
    }
}
