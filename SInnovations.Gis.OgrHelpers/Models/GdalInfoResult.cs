using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SInnovations.Gis.OgrHelpers.Models
{
    public class GdalInfoResult
    {
        public double[] UpperLeft { get; set; }
        public double[] UpperRight { get; set; }
        public double[] LowerLeft { get; set; }
        public double[] LowerRight { get; set; }
        public double[] Center { get; set; }

        public double[] PixelSize { get; set; }
        public double[] Extent { get; set; }

    }
}
