using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DotSpatial.Projections;
using System.Linq;
using System.Diagnostics;
namespace SInnovations.Gis.Tests
{
    [TestClass]
    public class UnitTest2
    {
        [TestMethod]
        public void TestMethod1()
        {

            var projectionInfo = ProjectionInfo.FromEpsgCode(21781);
            var epsg25832extent = new double[] { -1878007.03, 3932282.86, 832014.23, 9436480.79 };
            Console.Write(projectionInfo.ToString());
            //epsg:25832 extent from epsg.io
           var resolutions= TileGrid.TileGrid.ResolutionsFromExtent(
               new double[] { -1878007.03, 3932282.86, 832014.23, 9436480.79 },maxZoom:40, basePow: Math.Sqrt(2));
           Console.WriteLine(string.Join("\n", resolutions));
           Console.WriteLine();
           resolutions = TileGrid.TileGrid.ResolutionsFromExtent(
   new double[] { -1878007.03, 3932282.86, 832014.23, 9436480.79 });
           Console.WriteLine(string.Join("\n", resolutions));

           var grid = TileGrid.TileGrid.CreateForExtent(new double[] { -1878007.03, 3932282.86, 832014.23, 9436480.79 });
          var range =  grid.GetTileRangeForExtentAndZ(epsg25832extent, 2);

          var ecwExtent = new double[] { 527000.000, 6076000.000, 569000.001, 6108000.000 };
            var z = grid.GetZForResolution(0.10);
            var resolution = grid.GetResolution(z);
            var tilesize = 256 * resolution;
            

          var range1 = grid.GetTileRangeForExtentAndResolution(ecwExtent, 0.10);
            var range2 = grid.GetTileRangeForExtentAndZ(ecwExtent,z);

            var xtiles = range1.MaxX - range1.MinX;
            var ytiles = range1.MaxY - range1.MinY;

            var targetMachines = Math.Sqrt(10);
            var tiles = xtiles * ytiles;
            var xtarget = (int)(xtiles / targetMachines);
            var ytarget =(int)(ytiles / targetMachines);

            xtarget = (int)Math.Pow(2, (int)Math.Log(xtarget, 2));
            ytarget = (int)Math.Pow(2, (int)Math.Log(ytarget, 2));
            while (xtarget * 256 > 65000) xtarget >>= 1;
            while (ytarget * 256 > 65000) ytarget >>= 1;
            bool yflip = true;
            var tileBlocks = range1.Split(xtarget,ytarget).ToArray();
            foreach (var block in tileBlocks)
            {
                var blockExtent = grid.GetTileRangeExtent(z, block);
                Console.WriteLine(string.Join(", ", grid.GetTileRangeExtent(z, block)));
                Trace.TraceInformation(@"%GDAL%/gdal_translate -of GTiff -co COMPRESS=JPEG -co JPEG_QUALITY=90 e-a_srs EPSG:25832 -projwin {0} ""J:\ortofoto\1083_SYDDANMARK\540_ECW_UTM32-EUREF89\540_ECW_UTM32-EUREF89.ecw"" ""J:\ortofoto\1083_SYDDANMARK\540_ECW_UTM32-EUREF89\540_ECW_UTM32-EUREF89.ecw.vrt"" ",
                    string.Format("{0} {1} {2} {3}",blockExtent[0],
                    yflip?blockExtent[3]:blockExtent[1]
                ,blockExtent[2],
                yflip?blockExtent[1]:blockExtent[3]));


            }
            
            

       //     var extent = grid.GetTileCoordExtent(ecw);
        }
    }
}
