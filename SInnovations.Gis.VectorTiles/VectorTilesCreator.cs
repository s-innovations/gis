using DotSpatial.Projections;
using Newtonsoft.Json;
using SInnovations.Gis.TileGrid;
using SInnovations.Gis.Vector;
using SInnovations.Gis.Vector.Layers;
using SInnovations.Gis.VectorTiles.WebApi;
using System;
using System.Collections.Generic;
using System.Data.Entity.Spatial;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace SInnovations.Gis.VectorTiles
{
    public class VectorTilesCreator<TEntity>
    {
                //  private db db;
        private string _cacheDir;
        private TileSystem _tilesystem;
        private ILayerContext<TEntity> _layer;
        
        public VectorTilesCreator(ILayerContext<TEntity> layerContext, int tilesize = 256)
        {
            //  this.db = new db();
            _cacheDir = "c:\\geojsonTiles";
            _tilesystem = new TileSystem(tilesize);
            _layer = layerContext;

        }
      //  [HttpGet]
     //   [Route("tiles/{layer}/{z}/{x}/{y}.{format}")]
        public IHttpActionResult Tile(int z, int x, int y, string format)
        {

            //    var vectorlayerProvider = db.GetOrCreateLayerProvider(layer);
           // using (var db = _datasources.GetDataSourceForLayer(layer))
           // {
                int nwX;
                int nwY;

                _tilesystem.TileXYToPixelXY(x, y, out nwX, out nwY);

                double[] nw = new double[2] { 0, 0 };
                double[] se = new double[2] { 0, 0 };


                _tilesystem.PixelsToMeters(nwX, nwY, z, out nw[0], out nw[1]);
                _tilesystem.PixelsToMeters(nwX + 256, nwY - 256, z, out se[0], out se[1]);

                var from = ProjectionInfo.FromEpsgCode(3857);
                var to = ProjectionInfo.FromEpsgCode(25832);
                double[] zo = new double[] { 0 };
                Reproject.ReprojectPoints(nw, null, from, to, 0, 1);
                Reproject.ReprojectPoints(se, null, from, to, 0, 1);
                var boundingBox = GetBoundingBox(nw[0], nw[1], se[0], se[1], 25832);//4326,25832

                //"POLYGON ((549096.184052115 6091165.87690635, 549096.184052115 6090471.7759259, 549807.355168824 6090471.7759259 0, 549807.355168824 6091165.87690635 0, 549096.184052115 6091165.87690635 0))"
                //549096.184052115 6091165.87690635 549807.355168824 6090471.7759259
                var path = Path.Combine(_cacheDir, string.Format("{0}/{1}/{2}/{3}.{4}", _layer.LayerName, z, x, y, format));
                if (File.Exists(path))
                    return new FileResult(path, "application/json");

               // using (var test = db.GetLayerContext<TEntity>(layer))
               // {
                    var resolution = _tilesystem.Resolution(z);
                    var arealLimit = resolution * resolution;
                  //  var query = test.GetRegion(boundingBox).Where(t => t.Areal > arealLimit);
                    var query = _layer.GetRegion(boundingBox);

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

              //  }
          //  }
        }
        

        private static DbGeometry GetBoundingBox(double nwX, double nwY, double seX, double seY, int epsg)
        {
            CultureInfo ci = new CultureInfo("en-US");

            DbGeometry boundingBox = DbGeometry.FromText(string.Format("POLYGON(({0} {1}, {0} {2}, {3} {2}, {3} {1}, {0} {1}))",
               nwX.ToString(ci),
               nwY.ToString(ci),
                seY.ToString(ci),
                seX.ToString(ci))
                , epsg);
            return boundingBox;
        }

      
    }
}
