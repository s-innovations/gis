using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.Entity.Spatial;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SInnovations.Gis.VectorTiles
{
    /// <summary>
    /// The base entity for OGR2OGR MS SQL feature layer table.
    /// Each table is a layer with a id column and an geometry column of either DbGeometry or DbGeography. 
    /// For now only DbGeometry is supported
    /// </summary>
    public class OgrEntity
    {
        public OgrEntity()
        {

        }
        [JsonIgnore]
        public DbGeometry Geometry { get; set; }

        [JsonIgnore]
        public int Id { get; set; }

    }
}
