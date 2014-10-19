using SInnovations.Gis.TileGrid;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


namespace SInnovations.Gis.OgrHelpers
{
    public class OgrHelper
    {
        public IEnvironmentVariablesProvider EnvironmentVariables { get; set; }
        public OgrHelper(IEnvironmentVariablesProvider EnvironmentVariables = null)
        {
            this.EnvironmentVariables = EnvironmentVariables;
        }
        public Task<double[]> GetExtentAsync(string source, string layer=null)
        {
            var process = new AsyncProcess<double[]>(@"%GDAL%\ogrInfo", parseExtent) {  EnvironmentVariables = EnvironmentVariables};
            return process.RunAsync(string.Format(@"-so -al ""{0}"" {1}", source, layer));        
        }

        public Task<string> GetProj4TextAsync(string source, string layer = null)
        {
            var path = Path.ChangeExtension(source, "prj");
            var process = new AsyncProcess<string>(@"%GDAL%\gdalsrsinfo", parseProj4) { EnvironmentVariables = EnvironmentVariables };
            return process.RunAsync(string.Format(@"""{0}""", path));  
        }


        public Task<int> Ogr2OgrClipAsync(string source,string target, string t_srs, double[] extent)
        {
            var process = new AsyncProcess(@"%GDAL%\ogr2ogr") { EnvironmentVariables = EnvironmentVariables };

            return process.RunAsync(string.Format(@"{0} {1} -t_srs {2} -spat {3}",
                target, source, t_srs, string.Join(" ", extent)));  
        }

        public Task<int> AddSpatialIndexToMSQL(string connectionstring,string tablename)
        {
            var process = new AsyncProcess(@"%GDAL%\ogrinfo") { EnvironmentVariables = EnvironmentVariables };
            return process.RunAsync(string.Format(@" ""{1}"" -sql ""create spatial index on {0}""", tablename, connectionstring));
        }


        private static double[] parseExtent(Process p, string str, string err)
        {
            var extent = Regex.Match(str, @"Extent: \((.*),(.*)\) - \((.*),(.*)\)");

            return new double[] { double.Parse(extent.Groups[1].Value), double.Parse(extent.Groups[2].Value), double.Parse(extent.Groups[3].Value), double.Parse(extent.Groups[4].Value) };
        }
        private static string parseProj4(Process p, string str, string err)
        {
            var extent = Regex.Match(str, @"PROJ.4 : '(.*)'");

            return extent.Groups[1].Value;

        }
    }
}
