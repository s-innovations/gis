using SInnovations.Gis.Vector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace SInnovations.Gis.VectorTiles.Host.WebApi
{
    public class db : MsSQLContext
    {

        public db()
            : base("testconn")
        {

        }
    }

    public class VectorTilesController : ApiController
    {
        IDataSource db;
        public VectorTilesController(IDataSource db)
        {
            this.db = db;
        }
        [HttpGet]
        [Route("tiles/{layer}/{z}/{x}/{y}.{format}")]
        public IHttpActionResult Tiles(string layer, int z, int x, int y, string format)
        {

                var tiles = new VectorTilesCreator<OgrEntity>(db.GetLayerContext(layer));

                return tiles.Tile(z, x, y, format);

            

        }
    }
}
