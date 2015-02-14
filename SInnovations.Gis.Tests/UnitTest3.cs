using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
namespace SInnovations.Gis.Tests
{
    using SInnovations.Gis.TileGrid;
    using System.Data.Entity.Spatial;
    using SInnovations.Gis.OgrHelpers;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.IO;
    using System.Threading.Tasks.Dataflow;
    using Microsoft.WindowsAzure.Storage.File;
    using Microsoft.WindowsAzure.Storage;
    using System.Text;
    using System.Diagnostics;


    class KeyTuple
    {
        public int count { get; set; }
        public int[] tilecoord { get; set; }
        public List<string> Tiles = new List<string>();
    }
    [TestClass]
    public class UnitTest3
    {
        [TestMethod]
        public async Task TestMethod1()
        {
            var Root = @"C:\ascend-vd\Sjaeland\11 - kbh-holbaek-lumaas";
            var TilesDir = "test1_cache_EPSG25832";
            var baseResolution = 0.059904600000000;
            var indexVrt = @"C:\ascend-vd\Sjaeland\11 - kbh-holbaek-lumaas\all.vrt";
            var jpgs = Directory.GetFiles(Root, "*.jpg");
            var outputtype = "gtiff";
            var extension = ".tif";
            var tileSize = 2048;

            var zStart = 10;
            var resolutions = new List<double>();
            for (var z = zStart; z >= 0; z--)
            {
                resolutions.Add((1 << z) * baseResolution);
            }

            var helper = new OgrHelper(new DefaultEnvironmentVariableProvider( 
                new Dictionary<string, string>{
               { "gdal" , @"C:\python\WinPython-64bit-2.7.6.4-gdal\tools\gdal"},
               { "GDAL_DATA",@"C:\python\WinPython-64bit-2.7.6.4-gdal\tools\gdal-data"}
               }));

            var test = await helper.GetGDALInfoAsync(indexVrt);


            var epsg25832extent = new double[] { -1878007.03, 3932282.86, 832014.23, 9436480.79 };
            var epsg25832Grid = new TileGrid(new TileGridOptions
            {
                Extent = epsg25832extent,
                Resolutions = resolutions.ToArray(),
                Origin = Extent.GetCorner(epsg25832extent, Extent.Corner.BottomLeft),
                TileSize = tileSize,
            });

            var testRange = epsg25832Grid.GetTileRangeForExtentAndZ(new double[] { -1878007.03, 3932282.86, 832014.23, 9436480.79 }, epsg25832Grid.MaxZoom);



            var tiles = await GetTilesForBaseDirectory(baseResolution, jpgs, helper, epsg25832Grid);

            //WriteTilesExtentFile(Root, epsg25832Grid, tiles);
            await CreateDirectories(tiles,Root,TilesDir, createShares: false);

            await ExtractBaseLayerTiles(Root, TilesDir, indexVrt, helper, epsg25832Grid, tiles, outputtype,extension);
            {
                var z = epsg25832Grid.MaxZoom;
                var dict = GetNextLevelTiles(z, tiles,Root,TilesDir,extension);
                await GenerateTilesLvl(helper, epsg25832Grid, dict, Root, TilesDir, outputtype,extension);

                while (--z > 0)
                {
                    dict = GetNextLevelTiles(z, dict.Values.Select(s => s.tilecoord).ToList(),Root,TilesDir,extension);
                    await GenerateTilesLvl(helper, epsg25832Grid, dict, Root, TilesDir,outputtype,extension);
                }
            }


            // dict = GetNextLevelTiles(z - 2, dict.Values.Select(s => s.tilecoord).ToList());
            // await GenerateTilesLvl(helper, epsg25832Grid, dict, Root, TilesDir);
            // dict = GetNextLevelTiles(z - 3, dict.Values.Select(s => s.tilecoord).ToList());
            // await GenerateTilesLvl(helper, epsg25832Grid, dict, Root, TilesDir);
            // dict = GetNextLevelTiles(z - 4, dict.Values.Select(s => s.tilecoord).ToList());
            // await GenerateTilesLvl(helper, epsg25832Grid, dict,Root,TilesDir);

            //sb = new StringBuilder();
            //foreach (var tile in Directory.GetFiles(@"C:\ascend-vd\Sjaeland\11 - kbh-holbaek-lumaas\tiles\", "*.tiff",SearchOption.AllDirectories))
            //{
            //    sb.AppendLine(tile);

            //}


            //    uploadBlock.Complete();
            //     await uploadBlock.Completion;




        }

        private static async Task ExtractBaseLayerTiles(string Root, string TilesDir, string indexVrt, OgrHelper helper, TileGrid epsg25832Grid, List<int[]> tiles,string outputformat,string extension)
        {
            var block = new TransformBlock<int[], string>((tile) =>
                ExtractTileExtent(helper, epsg25832Grid, tile, indexVrt, Root, TilesDir,outputformat,extension)
            , new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 4 });

            int i = 0; var stopwatch = Stopwatch.StartNew();
            var emptyBlock = new ActionBlock<string>((s) => {
                i++;
                Debug.WriteLine(string.Format("{2}: {0}/{1} -> {3}",
                    i, tiles.Count, stopwatch.ElapsedMilliseconds,
                    new TimeSpan((long)(((double)stopwatch.ElapsedMilliseconds / i) * tiles.Count)).TotalMinutes));
            });
            block.LinkTo(emptyBlock);

            foreach (var tile in tiles)
                block.Post(tile);

            block.Complete();
            await block.Completion;
        }

        private async Task CreateDirectories(List<int[]> tiles, string root, string tileshare, bool createShares=false)
        {
            var distincts = tiles.Select(tile =>
            {
                var file = Path.Combine(tile[0].ToString("D2")
                , ((int)(tile[1] / 1000000)).ToString("D3")
                , (((int)(tile[1] / 1000)) % 1000).ToString("D3")
                , (((int)(tile[1])) % 1000).ToString("D3")
                , (((int)(tile[2] / 1000000))).ToString("D3")
                , (((int)(tile[2] / 1000)) % 1000).ToString("D3")
                );
                return file;
            }).Distinct(StringComparer.InvariantCultureIgnoreCase).ToArray();

            var dirshare = createShares ? GetShare() : null;
            var dirfolder = new DirectoryInfo(Path.Combine(root, tileshare));
            var basedir = distincts.GroupBy(dir => dir.Split('\\').First());
            await CreateDirectories(dirshare, dirfolder, basedir);
        }

        private static async Task<List<int[]>> GetTilesForBaseDirectory(double baseResolution, string[] jpgs, OgrHelper helper, TileGrid epsg25832Grid)
        {
            var tiles = new List<int[]>();
            foreach (var jpg in jpgs)
            {
                var imgExtent = await helper.GetGdalExtentAsync(jpg);
                var range = epsg25832Grid.GetTileRangeForExtentAndResolution(imgExtent, baseResolution);
                var extent = epsg25832Grid.GetTileRangeExtent(epsg25832Grid.MaxZoom, range);
                var lookup = tiles.ToLookup(t => string.Join("", t));
                tiles.AddRange(range.GetTiles(epsg25832Grid.MaxZoom).Where(t => !lookup.Contains(string.Join("", t))));

            }
            return tiles;
        }

        private static void WriteTilesExtentFile(string Root, TileGrid epsg25832Grid, List<int[]> tiles)
        {
            var sb = new StringBuilder();

            foreach (var tile in tiles)
            {
                var extent = epsg25832Grid.GetTileCoordExtent(tile);
                sb.AppendLine(string.Join(";", string.Format("{0}{1}{2}", tile[0], tile[1], tile[2]), extent[0], extent[2], extent[1], extent[3]));

            }
            File.WriteAllText(Path.Combine(Root, "tiles.idx"), sb.ToString());
        }

        private static async Task GenerateTilesLvl(OgrHelper helper, TileGrid epsg25832Grid, Dictionary<string, KeyTuple> dict, string root, string tilesdir,string outputype, string extension)
        {
            int parallel = 3;
         //   
            var block = new ActionBlock<Tuple<KeyTuple, string>>(async (t) =>
            {
               
              
                var path = GetFile(t.Item1.tilecoord, root, tilesdir, extension);
                if (File.Exists(path))
                    return;

                var indexbuilder = new StringBuilder();
                foreach (var file in t.Item1.Tiles)
                    indexbuilder.AppendLine(file);

                File.WriteAllText(Path.Combine(root, tilesdir,t.Item2.ToString()+ "index.txt"), indexbuilder.ToString());

               
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                await helper.BuildVrtFileAsync(Path.Combine(root, tilesdir, t.Item2.ToString() + "index.txt"), 
                    Path.Combine(root, tilesdir, t.Item2.ToString() + "combine.vrt"));
                
                var tileExtent = epsg25832Grid.GetTileCoordExtent(t.Item1.tilecoord);
                await helper.GdalExtractWithTranslate(Path.Combine(root, tilesdir, t.Item2.ToString() + "combine.vrt"),
                path,
                string.Format("{0} {1} {2} {3}", tileExtent[0], tileExtent[3], tileExtent[2], tileExtent[1]), outputype, epsg25832Grid.GetTileSize(t.Item1.tilecoord[0]));

            }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = parallel });
            foreach (var newtile in dict.Keys.Select((t, i) => new Tuple<KeyTuple, string>(dict[t], t)))
                block.Post(newtile);

            block.Complete();
            while(!block.Completion.IsCompleted)
            {
                Debug.WriteLine(block.InputCount);
                await Task.Delay(5000);
            }
            await block.Completion;

        }

        private static Dictionary<string, KeyTuple> GetNextLevelTiles(int z, List<int[]> tiles, string rootdir, string tilesdir,string extension)
        {
            var dict = new Dictionary<string, KeyTuple>();
            foreach (var tileCoord in tiles)
            {
                var tileRange = TileRange.CreateOrUpdate(
                    0, tileCoord[1], 0, tileCoord[2], null);
                {
                    tileRange.MinX = tileRange.MaxX >>= 1;
                    tileRange.MinY = tileRange.MaxY >>= 1;
                    var key = "" + (z - 1) + tileRange.MinX + tileRange.MinY;

                    if (!dict.ContainsKey(key))
                        dict[key] = new KeyTuple { tilecoord = new int[] { z - 1, tileRange.MinX, tileRange.MinY }, count = 0 };
                    dict[key].count++;
                    dict[key].Tiles.Add(GetFile(tileCoord, rootdir, tilesdir,extension));
                }
            };
            return dict;
        }

        private static async Task CreateDirectories(CloudFileDirectory dirshare, DirectoryInfo dirfolder, IEnumerable<IGrouping<string, string>> basedir)
        {
           
            foreach (var createdir in basedir)
            {
                if (dirshare != null)
                {
                    dirshare = dirshare.GetDirectoryReference(createdir.Key);
                    await dirshare.CreateIfNotExistsAsync();
                }
                var nfolder = new DirectoryInfo(Path.Combine(dirfolder.FullName, createdir.Key));
                nfolder.Create();

                var distincts = createdir.Where(s => s.Length > createdir.Key.Length).Select(s => s.Substring(createdir.Key.Length + 1)).GroupBy(dir => dir.Split('\\').First());
                if(distincts.Any())
                    await CreateDirectories(dirshare, nfolder, distincts);
             
            }
        }

        private static async Task UploadToShare(CloudFileDirectory share, string s,string rootdir, string tilesdir)
        {
            var target = s.Substring(Path.Combine(rootdir,tilesdir).Length+1);

            var dirs = Path.GetDirectoryName(target).Split('\\').ToArray();
            var dir = share;//.GetDirectoryReference(dirs.First());
            while (dirs.Any())
            {
                dir = dir.GetDirectoryReference(dirs.First());
                dirs = dirs.Skip(1).ToArray();

            }


            var file = dir.GetFileReference(Path.GetFileName(s));
            Console.WriteLine(file.Parent);
            if(!await file.ExistsAsync())
                await file.UploadFromFileAsync(s, FileMode.Open);
            Console.WriteLine(file.Parent);
        }

        private static async Task<string> ExtractTileExtent(OgrHelper helper, TileGrid epsg25832Grid, int[] tile,string vrt,string root,string dir,string outputformat, string extension)
        {
            var tileExtent = epsg25832Grid.GetTileCoordExtent(tile);
            var height = Extent.GetHeight(tileExtent);
            var width = Extent.GetWidth(tileExtent);
            Console.WriteLine("{0} {1} {2}", string.Join(", ", tileExtent), height, width);


            var file = GetFile(tile, root,dir,extension);


            if (File.Exists(file))
                return file;

            await helper.GdalExtractWithTranslate(vrt,
                file,
                string.Format("{0} {1} {2} {3}", tileExtent[0], tileExtent[3], tileExtent[2], tileExtent[1]), outputformat);
            // ulx uly lrx lry
            return file;
        }

        private static string GetFile(int[] tile, string root, string tilesdir, string extension)
        {
            var file = Path.Combine(root,tilesdir, (tile[0].ToString("D2"))
                , ((int)(tile[1] / 1000000)).ToString("D3")
                , (((int)(tile[1] / 1000)) % 1000).ToString("D3")
                , (((int)(tile[1])) % 1000).ToString("D3")
                , (((int)(tile[2] / 1000000))).ToString("D3")
                , (((int)(tile[2] / 1000)) % 1000).ToString("D3")
                , (((int)(tile[2])) % 1000).ToString("D3") + extension);
            return file;
        }

        //private static async Task CreateDirectoriesAction(CloudFileDirectory share, string localdir)
        //{
        //  //  var file = Path.Combine(@"C:\ascend-vd\Sjaeland\11 - kbh-holbaek-lumaas\tiles\" + zfolder
        //      //, ((int)(tile[1] / 1000000)).ToString("D3")
        //      //, (((int)(tile[1] / 1000)) % 1000).ToString("D3")
        //      //, (((int)(tile[1])) % 1000).ToString("D3")
        //      //, (((int)(tile[2] / 1000000))).ToString("D3")
        //      //, (((int)(tile[2] / 1000)) % 1000).ToString("D3")
        //      //);

        //    Directory.CreateDirectory(localdir);

        //    var target = localdir.Substring(@"C:\ascend-vd\Sjaeland\11 - kbh-holbaek-lumaas\tiles\".Length);

        //    var dirs = target.Split('\\').ToArray();
        //    var dir = share;//.GetDirectoryReference(dirs.First());

        //    while (dirs.Any())
        //    {
        //        dir = dir.GetDirectoryReference(dirs.First());
        //        await dir.CreateIfNotExistsAsync();
        //        dirs = dirs.Skip(1).ToArray();

        //    }
        //    await dir.CreateIfNotExistsAsync();
          
        //}

        /// <summary>
        /// A mock up while waiting for Niels to add file shares on production environment
        /// </summary>
        /// <returns></returns>
        public CloudFileDirectory GetShare()
        {
            var azureFilesConn = File.ReadAllText(@"C:\Users\PoulKjeldager\Desktop\storageaccount.txt");
            CloudStorageAccount account = CloudStorageAccount.Parse(azureFilesConn);
            CloudFileClient client = account.CreateCloudFileClient();
            CloudFileShare share = client.GetShareReference("mapproxy-odin");
            var root = share.GetRootDirectoryReference();
            var dir= root.GetDirectoryReference("cachedir");
            dir.CreateIfNotExists();
            return dir;
            
        }
    }
}
