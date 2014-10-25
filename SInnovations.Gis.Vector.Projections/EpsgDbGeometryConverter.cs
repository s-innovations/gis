using DotSpatial.Projections;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.Entity.Spatial;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SInnovations.Gis.Vector.Projections
{

    public class EpsgDbGeometryConverter : DbGeographyGeoJsonConverter
    {
        static EpsgDbGeometryConverter()
        {
            EpsgDbGeometryConverter.GeoBases["Polygon"] = () => new EpgsPolygon();
        }

        public EpsgDbGeometryConverter(int epsg)
        {
            CoordinateSystem = epsg;
        }
        public EpsgDbGeometryConverter()
            : this(3857)
        {

        }
        public int? CoordinateSystem { get; set; }


        protected override int? GetCoordinateSystem(Newtonsoft.Json.Linq.JObject jsonObject)
        {
            return CoordinateSystem;
        }

        public class EpgsPolygon : Polygon
        {
            public override void ParseJson(DbGeographyGeoJsonConverter converter, Newtonsoft.Json.Linq.JArray array)
            {
                var targetCoordinateSystem = (converter as EpsgDbGeometryConverter).CoordinateSystem;
                //Cant convert if source dont have any coordinate system.
                if (!CoordinateSystem.HasValue ||  CoordinateSystem == targetCoordinateSystem)
                {
                    base.ParseJson(converter, array);
                    return;
                }
                Rings = new List<List<Position>>();
                var rings = array.ToObject<double[][][]>();

                var ringSizes = rings.Select(r => r.Length).ToArray();
                var coordinateLength = rings.First().GroupBy(c => c.Length).Single().Key;
                foreach (var ring in rings)
                {
                    var flat = ring.SelectMany(s => s).ToArray();

                    Reproject.ReprojectPoints(flat, null,
                     ProjectionInfo.FromEpsgCode(CoordinateSystem.Value), ProjectionInfo.FromEpsgCode(targetCoordinateSystem.Value), 0, ringSizes[0]);
                
                    var ringList = new List<Position>();
                    for (int i = 0; i < flat.Length; i += coordinateLength)
                        ringList.Add(new Position(flat.Skip(i).Take(coordinateLength).ToArray()));
                    Rings.Add(ringList);
                }
                

            }
        }
        //  public DbGeometry
    }
}
