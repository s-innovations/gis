using System;
using System.Collections.Generic;
using System.Data.Entity.Spatial;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.SqlServer.Types;

namespace SInnovations.Gis.Vector
{


    /// <summary>
    /// The <see cref="DbGeography"/> GeoJSON converter
    /// </summary>
    public class DbGeographyGeoJsonConverter : JsonConverter
    {
        /// <summary>
        /// Well-known-binary point value.
        /// </summary>
        private const int PointWkb = 1;

        /// <summary>
        /// Well-known-binary line value.
        /// </summary>
        private const int LineStringWkb = 2;

        /// <summary>
        /// Well-known-binary polygon value.
        /// </summary>
        private const int PolygonWkb = 3;

        /// <summary>
        /// Well-known-binary multi-point value.
        /// </summary>
        private const int MultiPointWkb = 4;

        /// <summary>
        /// Well-known-binary multi-line value.
        /// </summary>
        private const int MultiLineStringWkb = 5;

        /// <summary>
        /// Well-known-binary multi-polygon value.
        /// </summary>
        private const int MultiPolygonWkb = 6;

        /// <summary>
        /// Well-known-binary geometry collection value.
        /// </summary>
        private const int GeometryCollectionWkb = 7;

        /// <summary>
        /// Well-known-binary line value.
        /// </summary>
        private static readonly Dictionary<int, string> WkbTypes =
        new Dictionary<int, string>
		{
			{ PointWkb, "Point" },
			{ LineStringWkb, "LineString" },
			{ PolygonWkb, "Polygon" },
			{ MultiPointWkb, "MultiPoint" },
			{ MultiLineStringWkb, "MultiLineString" },
			{ MultiPolygonWkb, "MultiPolygon" },
			{ GeometryCollectionWkb, "GeometryCollection" }
		};

        /// <summary>
        /// The types derived from <see cref="GeoBase"/> accessed by name.
        /// </summary>
        protected static readonly Dictionary<string, Func<GeoBase>> GeoBases =
        new Dictionary<string, Func<GeoBase>> //CHange to ctor
		{
			{ "Point", () => new Point() },
			{ "LineString", () => new LineString() },
			{ "Polygon", () => new Polygon() },
			{ "MultiPoint", () => new MultiPoint() },
			{ "MultiLineString", () => new MultiLineString() },
			{ "MultiPolygon", () => new MultiPolygon() },
			{ "GeometryCollection", () => new Collection() },
		};



        /// <summary>
        /// The indexes per point.
        /// </summary>
        public enum IndexesPerPoint
        {
            /// <summary>
            /// Two indexes per point.
            /// </summary>
            Two,

            /// <summary>
            /// Three indexes per point.
            /// </summary>
            Three,

            /// <summary>
            /// Four indexes per point.
            /// </summary>
            Four
        }

        /// <summary>
        /// Read the GeoJSON type value.
        /// </summary>
        /// <param name="jsonObject">
        /// The JSON object.
        /// </param>
        /// <param name="coordinateSystem">
        /// The coordinate System.
        /// </param>
        /// <returns>
        /// A function that can read the value.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Unexpected JSON.
        /// </exception>
        /// <remarks>
        /// Leaves the reader positioned where the value should start.
        /// </remarks>
        public static GeoBase ParseJsonObjectToGeoBase(DbGeographyGeoJsonConverter converter, JObject jsonObject, out int? coordinateSystem)
        {
            var type = jsonObject["type"];
            if (type == null)
            {
                throw new ArgumentException(string.Format("Expected a \"type\" property, found [{0}]", string.Join(", ", jsonObject.Properties().Select(p => p.Name))));
            }

            if (type.Type != JTokenType.String)
            {
                throw new ArgumentException(string.Format("Expected a string token for the type of the GeoJSON type, got {0}", type.Type), "jsonObject");
            }

            var crs = jsonObject["crs"];
            coordinateSystem = crs != null ? converter.GetCoordinateSystem(crs.Value<JObject>()) : null;


            Func<GeoBase> geoType;
            if (!GeoBases.TryGetValue(type.Value<string>(), out geoType))
            {
                throw new ArgumentException(
                string.Format(
                "Got unsupported GeoJSON object type {0}. Expected one of [{1}]",
                type.Value<string>(),
                string.Join(", ", GeoBases.Keys)),
                "jsonObject");
            }

            var geo = geoType();
            geo.CoordinateSystem = coordinateSystem;
            var isCollection = typeof(Collection).IsAssignableFrom(geo.GetType());
            var geoObject = isCollection ? jsonObject["geometries"] : jsonObject["coordinates"];
            if (geoObject == null)
            {
                throw new ArgumentException(
                string.Format(
                "Expected a field named \"{0}\", found [{1}]",
                isCollection ? "geometries" : "coordinates",
                string.Join(", ", jsonObject.Properties().Select(p => p.Name))),
                "jsonObject");
            }

           
            geo.ParseJson(converter, geoObject.Value<JArray>());

            coordinateSystem = geo.CoordinateSystem;
            return geo;
        }



        /// <inheritdoc/>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            BinaryReader lebr;
            BinaryReader bebr;
            var geographyValue = value as DbGeography;
            int coordinateSystemId;
            if (geographyValue != null)
            {
                var sqlGeography = geographyValue.ProviderValue as SqlGeography;
                var br = new BinaryReader(new MemoryStream(sqlGeography != null ? sqlGeography.AsBinaryZM().Value : geographyValue.AsBinary()));
                lebr = BitConverter.IsLittleEndian ? br : new ReverseEndianBinaryReader(br.BaseStream);
                bebr = BitConverter.IsLittleEndian ? new ReverseEndianBinaryReader(br.BaseStream) : br;
                coordinateSystemId = geographyValue.CoordinateSystemId;
            }
            else
            {
                var geometryValue = value as DbGeometry;
                if (geometryValue != null)
                {
                    var sqlGeometry = geometryValue.ProviderValue as SqlGeometry;
                    var br = new BinaryReader(new MemoryStream(sqlGeometry != null ? sqlGeometry.AsBinaryZM().Value : geometryValue.AsBinary()));
                    lebr = BitConverter.IsLittleEndian ? br : new ReverseEndianBinaryReader(br.BaseStream);
                    bebr = BitConverter.IsLittleEndian ? new ReverseEndianBinaryReader(br.BaseStream) : br;
                    coordinateSystemId = geometryValue.CoordinateSystemId;
                }
                else
                {
                    throw new ArgumentException(
                    string.Format("Expecting DbGeography or DbGeometry, got {0}", value.GetType().Name), "value");
                }
            }

            var jsonObject = WriteObject(lebr, bebr);
            jsonObject.Add(
            "crs",
            new JObject
			{
				new JProperty("type", "name"),
				new JProperty(
				"properties",
				new JObject { new JProperty("name", string.Format("EPSG:{0}", coordinateSystemId)) })
			});
            writer.WriteRawValue(jsonObject.ToString(Formatting.None));
        }

        /// <inheritdoc/>
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            // Load JObject from stream
            JObject jsonObject = JObject.Load(reader);

            // Create target object based on JObject
            object target = CreateDbGeo(this, jsonObject, objectType);

            // Populate the object properties
            serializer.Populate(jsonObject.CreateReader(), target);

            return target;
        }

        /// <inheritdoc/>
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(DbGeography) || objectType == typeof(DbGeometry);
        }

        /// <summary>
        /// Parse the coordinate system object and return it's value.
        /// </summary>
        /// <param name="jsonObject">
        /// The JSON object.
        /// </param>
        /// <returns>
        /// The coordinate system value; null if couldn't parse it (only a couple EPSG-style values).
        /// </returns>
        protected virtual int? GetCoordinateSystem(JObject jsonObject)
        {
            var properties = jsonObject["properties"];
            if (properties != null && properties.Type == JTokenType.Object)
            {
                var p = properties.Value<JObject>();
                var name = p["name"];
                if (name != null)
                {
                    var s = name.Value<string>();
                    if (!string.IsNullOrWhiteSpace(s))
                    {
                        var m = Regex.Match(
                        s,
                        @"^\s*(urn\s*:\s*ogc\s*:\s*def\s*:crs\s*:EPSG\s*:\s*[\d.]*\s*:|EPSG\s*:)\s*(?<value>\d+)\s*$",
                        RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase);
                        if (m.Success)
                        {
                            return int.Parse(m.Groups["value"].Value);
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Get well known binary from a <see cref="JsonReader"/>.
        /// </summary>
        /// <param name="jsonObject">
        /// The JSON object.
        /// </param>
        /// <param name="defaultCoordinateSystemId">
        /// The default coordinate system id.
        /// </param>
        /// <returns>
        /// A tuple of the well-known-binary and the coordinate system identifier.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Unexpected JSON.
        /// </exception>
        private static Tuple<byte[], int> GetWellKnownBinary(DbGeographyGeoJsonConverter converter, JObject jsonObject, int defaultCoordinateSystemId)
        {
            var ob = new MemoryStream();

            int? coordinateSystemId;
            var geoBase = ParseJsonObjectToGeoBase(converter, jsonObject, out coordinateSystemId);
            geoBase.WellKnownBinary(ob);
            return new Tuple<byte[], int>(ob.ToArray(), coordinateSystemId.HasValue ? coordinateSystemId.Value : defaultCoordinateSystemId);
        }

        /// <summary>
        /// Write a well-known binary object to JSON.
        /// </summary>
        /// <param name="lebr">
        /// The little-endian binary reader.
        /// </param>
        /// <param name="bebr">
        /// The big-endian binary reader.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Unexpected well-known binary.
        /// </exception>
        /// <returns>
        /// The <see cref="JObject"/> for the given binary data.
        /// </returns>
        private static JObject WriteObject(BinaryReader lebr, BinaryReader bebr)
        {
            var jsonObject = new JObject();
            var br = lebr.ReadByte() == 0 ? bebr : lebr;
            IndexesPerPoint ipp;
            int gtype = JsonSafeWellKnownBinaryGeographicType(br, out ipp);
            gtype %= 1000;
            string objTypeName;
            if (!WkbTypes.TryGetValue(gtype, out objTypeName))
            {
                throw new ArgumentException(
                string.Format(
                "Unsupported type {0}. Supported types: {1}",
                gtype,
                string.Join(", ", WkbTypes.Select(kv => string.Format("({0}, {1}", kv.Key, kv.Value)))));
            }

            jsonObject.Add("type", objTypeName);

            if (ipp == IndexesPerPoint.Two)
            {
                WriteXy(jsonObject, gtype, br, lebr, bebr);
            }
            else if (ipp == IndexesPerPoint.Three)
            {
                WriteXyz(jsonObject, gtype, br, lebr, bebr);
            }
            else
            {
                WriteXyzm(jsonObject, gtype, br, lebr, bebr);
            }

            return jsonObject;
        }

        /// <summary>
        /// Write elements that don't have altitude information.
        /// </summary>
        /// <param name="jsonObject">
        /// The JSON object.
        /// </param>
        /// <param name="gtype">
        /// The type to write.
        /// </param>
        /// <param name="br">
        /// The binary reader.
        /// </param>
        /// <param name="lebr">
        /// The little-endian binary reader.
        /// </param>
        /// <param name="bebr">
        /// The big-endian binary reader.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Something like a multi-line string isn't made up of line strings.
        /// </exception>
        private static void WriteXy(JObject jsonObject, int gtype, BinaryReader br, BinaryReader lebr, BinaryReader bebr)
        {
            if (gtype == GeometryCollectionWkb)
            {
                var array = new JArray();
                int count = br.ReadInt32();
                for (int i = 0; i < count; ++i)
                {
                    array.Add(WriteObject(lebr, bebr));
                }

                jsonObject.Add("geometries", array);
            }
            else
            {
                var array = new JArray();
                switch (gtype)
                {
                    case PointWkb:
                        array.Add(br.ReadDouble());
                        array.Add(br.ReadDouble());
                        break;
                    case LineStringWkb:
                        foreach (var a in WriteLineXy(br))
                        {
                            array.Add(a);
                        }

                        break;
                    case PolygonWkb:
                        foreach (var a in WritePolygonXy(br))
                        {
                            array.Add(a);
                        }

                        break;
                    case MultiPointWkb:
                        int pointCount = br.ReadInt32();
                        for (int i = 0; i < pointCount; ++i)
                        {
                            br = lebr.ReadByte() == 0 ? bebr : lebr;
                            IndexesPerPoint ipp;
                            gtype = JsonSafeWellKnownBinaryGeographicType(br, out ipp);
                            if ((gtype % 1000) != PointWkb)
                            {
                                throw new ArgumentException(string.Format("Expected a type of 1, got {0}", gtype), "lebr");
                            }

                            array.Add(
                            ipp == IndexesPerPoint.Two
                            ? new JArray { br.ReadDouble(), br.ReadDouble() }
                            : ipp == IndexesPerPoint.Three
                            ? new JArray { br.ReadDouble(), br.ReadDouble(), br.ReadDouble() }
                            : new JArray { br.ReadDouble(), br.ReadDouble(), br.ReadDouble(), br.ReadDouble() });
                        }

                        break;
                    case MultiLineStringWkb:
                        int lineCount = br.ReadInt32();
                        for (int i = 0; i < lineCount; ++i)
                        {
                            br = lebr.ReadByte() == 0 ? bebr : lebr;
                            IndexesPerPoint ipp;
                            gtype = JsonSafeWellKnownBinaryGeographicType(br, out ipp);
                            if ((gtype % 1000) != LineStringWkb)
                            {
                                throw new ArgumentException(string.Format("Expected a type of 2, got {0}", gtype), "lebr");
                            }

                            var lineArray = new JArray();
                            foreach (var a in ipp == IndexesPerPoint.Two ? WriteLineXy(br) : ipp == IndexesPerPoint.Three ? WriteLineXyz(br) : WriteLineXyzm(br))
                            {
                                lineArray.Add(a);
                            }

                            array.Add(lineArray);
                        }

                        break;
                    case MultiPolygonWkb:
                        int polygonCount = br.ReadInt32();
                        for (int i = 0; i < polygonCount; ++i)
                        {
                            br = lebr.ReadByte() == 0 ? bebr : lebr;
                            IndexesPerPoint ipp;
                            gtype = JsonSafeWellKnownBinaryGeographicType(br, out ipp);
                            if ((gtype % 1000) != PolygonWkb)
                            {
                                throw new ArgumentException(string.Format("Expected a type of 3, got {0}", gtype), "lebr");
                            }

                            var polygonArray = new JArray();
                            foreach (var a in ipp == IndexesPerPoint.Two ? WritePolygonXy(br) : ipp == IndexesPerPoint.Three ? WritePolygonXyz(br) : WritePolygonXyzm(br))
                            {
                                polygonArray.Add(a);
                            }

                            array.Add(polygonArray);
                        }

                        break;
                    default:
                        throw new ArgumentException(string.Format("Unsupported geo-type {0}", gtype), "lebr");
                }

                jsonObject.Add("coordinates", array);
            }
        }

        /// <summary>
        /// Write elements that have altitude information.
        /// </summary>
        /// <param name="jsonObject">
        /// The JSON object.
        /// </param>
        /// <param name="gtype">
        /// The type to write.
        /// </param>
        /// <param name="br">
        /// The binary reader.
        /// </param>
        /// <param name="lebr">
        /// The little-endian binary reader.
        /// </param>
        /// <param name="bebr">
        /// The big-endian binary reader.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Something like a multi-line string isn't made up of line strings.
        /// </exception>
        private static void WriteXyz(JObject jsonObject, int gtype, BinaryReader br, BinaryReader lebr, BinaryReader bebr)
        {
            if (gtype == GeometryCollectionWkb)
            {
                var array = new JArray();
                int count = br.ReadInt32();
                for (int i = 0; i < count; ++i)
                {
                    array.Add(WriteObject(lebr, bebr));
                }

                jsonObject.Add("geometries", array);
            }
            else
            {
                var array = new JArray();
                switch (gtype)
                {
                    case PointWkb:
                        array.Add(br.ReadDouble());
                        array.Add(br.ReadDouble());
                        array.Add(br.ReadDouble());
                        break;
                    case LineStringWkb:
                        foreach (var a in WriteLineXyz(br))
                        {
                            array.Add(a);
                        }

                        break;
                    case PolygonWkb:
                        foreach (var a in WritePolygonXyz(br))
                        {
                            array.Add(a);
                        }

                        break;
                    case MultiPointWkb:
                        int pointCount = br.ReadInt32();
                        for (int i = 0; i < pointCount; ++i)
                        {
                            br = lebr.ReadByte() == 0 ? bebr : lebr;
                            IndexesPerPoint ipp;
                            gtype = JsonSafeWellKnownBinaryGeographicType(br, out ipp);
                            if ((gtype % 1000) != PointWkb)
                            {
                                throw new ArgumentException(string.Format("Expected a type of 1, got {0}", gtype), "lebr");
                            }

                            array.Add(
                            ipp == IndexesPerPoint.Four
                            ? new JArray { br.ReadDouble(), br.ReadDouble(), br.ReadDouble(), br.ReadDouble() }
                            : ipp == IndexesPerPoint.Three
                            ? new JArray { br.ReadDouble(), br.ReadDouble(), br.ReadDouble() }
                            : new JArray { br.ReadDouble(), br.ReadDouble() });
                        }

                        break;
                    case MultiLineStringWkb:
                        int lineCount = br.ReadInt32();
                        for (int i = 0; i < lineCount; ++i)
                        {
                            br = lebr.ReadByte() == 0 ? bebr : lebr;
                            IndexesPerPoint ipp;
                            gtype = JsonSafeWellKnownBinaryGeographicType(br, out ipp);

                            if ((gtype % 1000) != LineStringWkb)
                            {
                                throw new ArgumentException(string.Format("Expected a type of 2, got {0}", gtype), "lebr");
                            }

                            var lineArray = new JArray();
                            foreach (var a in ipp == IndexesPerPoint.Two ? WriteLineXy(br) : ipp == IndexesPerPoint.Three ? WriteLineXyz(br) : WriteLineXyzm(br))
                            {
                                lineArray.Add(a);
                            }

                            array.Add(lineArray);
                        }

                        break;
                    case MultiPolygonWkb:
                        int polygonCount = br.ReadInt32();
                        for (int i = 0; i < polygonCount; ++i)
                        {
                            br = lebr.ReadByte() == 0 ? bebr : lebr;
                            IndexesPerPoint ipp;
                            gtype = JsonSafeWellKnownBinaryGeographicType(br, out ipp);
                            if ((gtype % 1000) != PolygonWkb)
                            {
                                throw new ArgumentException(string.Format("Expected a type of 3, got {0}", gtype), "lebr");
                            }

                            var polygonArray = new JArray();
                            foreach (var a in ipp == IndexesPerPoint.Two ? WritePolygonXy(br) : ipp == IndexesPerPoint.Three ? WritePolygonXyz(br) : WritePolygonXyzm(br))
                            {
                                polygonArray.Add(a);
                            }

                            array.Add(polygonArray);
                        }

                        break;
                    default:
                        throw new ArgumentException(string.Format("Unsupported geo-type {0}", gtype), "lebr");
                }

                jsonObject.Add("coordinates", array);
            }
        }

        /// <summary>
        /// Write elements that have altitude information.
        /// </summary>
        /// <param name="jsonObject">
        /// The JSON object.
        /// </param>
        /// <param name="gtype">
        /// The type to write.
        /// </param>
        /// <param name="br">
        /// The binary reader.
        /// </param>
        /// <param name="lebr">
        /// The little-endian binary reader.
        /// </param>
        /// <param name="bebr">
        /// The big-endian binary reader.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Something like a multi-line string isn't made up of line strings.
        /// </exception>
        private static void WriteXyzm(JObject jsonObject, int gtype, BinaryReader br, BinaryReader lebr, BinaryReader bebr)
        {
            if (gtype == GeometryCollectionWkb)
            {
                var array = new JArray();
                int count = br.ReadInt32();
                for (int i = 0; i < count; ++i)
                {
                    array.Add(WriteObject(lebr, bebr));
                }

                jsonObject.Add("geometries", array);
            }
            else
            {
                var array = new JArray();
                switch (gtype)
                {
                    case PointWkb:
                        array.Add(br.ReadDouble());
                        array.Add(br.ReadDouble());
                        array.Add(br.ReadDouble());
                        array.Add(br.ReadDouble());
                        break;
                    case LineStringWkb:
                        foreach (var a in WriteLineXyzm(br))
                        {
                            array.Add(a);
                        }

                        break;
                    case PolygonWkb:
                        foreach (var a in WritePolygonXyzm(br))
                        {
                            array.Add(a);
                        }

                        break;
                    case MultiPointWkb:
                        int pointCount = br.ReadInt32();
                        for (int i = 0; i < pointCount; ++i)
                        {
                            br = lebr.ReadByte() == 0 ? bebr : lebr;
                            IndexesPerPoint ipp;
                            gtype = JsonSafeWellKnownBinaryGeographicType(br, out ipp);
                            if ((gtype % 1000) != PointWkb)
                            {
                                throw new ArgumentException(string.Format("Expected a type of 1, got {0}", gtype), "lebr");
                            }

                            array.Add(
                            ipp == IndexesPerPoint.Four
                            ? new JArray { br.ReadDouble(), br.ReadDouble(), br.ReadDouble(), br.ReadDouble() }
                            : ipp == IndexesPerPoint.Three
                            ? new JArray { br.ReadDouble(), br.ReadDouble(), br.ReadDouble() }
                            : new JArray { br.ReadDouble(), br.ReadDouble() });
                        }

                        break;
                    case MultiLineStringWkb:
                        int lineCount = br.ReadInt32();
                        for (int i = 0; i < lineCount; ++i)
                        {
                            br = lebr.ReadByte() == 0 ? bebr : lebr;
                            IndexesPerPoint ipp;
                            gtype = JsonSafeWellKnownBinaryGeographicType(br, out ipp);
                            if ((gtype % 1000) != LineStringWkb)
                            {
                                throw new ArgumentException(string.Format("Expected a type of 2, got {0}", gtype), "lebr");
                            }

                            var lineArray = new JArray();
                            foreach (var a in ipp == IndexesPerPoint.Two ? WriteLineXy(br) : ipp == IndexesPerPoint.Three ? WriteLineXyz(br) : WriteLineXyzm(br))
                            {
                                lineArray.Add(a);
                            }

                            array.Add(lineArray);
                        }

                        break;
                    case MultiPolygonWkb:
                        int polygonCount = br.ReadInt32();
                        for (int i = 0; i < polygonCount; ++i)
                        {
                            br = lebr.ReadByte() == 0 ? bebr : lebr;
                            IndexesPerPoint ipp;
                            gtype = JsonSafeWellKnownBinaryGeographicType(br, out ipp);
                            if ((gtype % 1000) != PolygonWkb)
                            {
                                throw new ArgumentException(string.Format("Expected a type of 3, got {0}", gtype), "lebr");
                            }

                            var polygonArray = new JArray();
                            foreach (var a in ipp == IndexesPerPoint.Two ? WritePolygonXy(br) : ipp == IndexesPerPoint.Three ? WritePolygonXyz(br) : WritePolygonXyzm(br))
                            {
                                polygonArray.Add(a);
                            }

                            array.Add(polygonArray);
                        }

                        break;
                    default:
                        throw new ArgumentException(string.Format("Unsupported geo-type {0}", gtype), "lebr");
                }

                jsonObject.Add("coordinates", array);
            }
        }

        /// <summary>
        /// The GeoJSON safe well-known-binary type.
        /// </summary>
        /// <param name="br">
        /// The binary reader to get the geographic type from.
        /// </param>
        /// <param name="indexesPerPoint">
        /// The indexes per point.
        /// </param>
        /// <returns>
        /// The <see cref="int"/>.
        /// </returns>
        /// <exception cref="NotSupportedException">
        /// The geographic type read from <paramref name="br"/> isn't supported by GeoJSON.
        /// </exception>
        private static int JsonSafeWellKnownBinaryGeographicType(BinaryReader br, out IndexesPerPoint indexesPerPoint)
        {
            int ret = br.ReadInt32();
            int gtype = ret % 1000;
            if (WkbTypes.ContainsKey(gtype))
            {
                int mod = ret / 1000;
                switch (mod)
                {
                    case 0:
                        indexesPerPoint = IndexesPerPoint.Two;
                        return ret;
                    case 1:
                        indexesPerPoint = IndexesPerPoint.Three;
                        return ret;
                    case 2:
                        throw new NotSupportedException(
                        "Found point with XYM instead of XYZM format. GeoJSON does not support having an M value in the point without having a Z coordinate");
                    case 3:
                        indexesPerPoint = IndexesPerPoint.Four;
                        return ret;
                }

                throw new NotSupportedException(
                string.Format(
                "Got {0} with modifier {1} ({2}). Don't know how to deal with modifier {1}.",
                WkbTypes[gtype],
                mod,
                ret));
            }

            throw new NotSupportedException(
            string.Format(
            "Found well-known-binary type {0}, GeoJSON only handles values: {1}",
            gtype,
            string.Join(", ", WkbTypes.Select(kv => string.Format("{0}({1})", kv.Key, kv.Value)))));
        }

        /// <summary>
        /// Write a JSON polygon from well-known binary.
        /// </summary>
        /// <param name="br">
        /// Read from this.
        /// </param>
        /// <returns>
        /// The <see cref="JArray"/> enumerable for the polygon.
        /// </returns>
        private static IEnumerable<JArray> WritePolygonXy(BinaryReader br)
        {
            var ret = new List<JArray>();
            int ringCount = br.ReadInt32();
            for (int ri = 0; ri < ringCount; ++ri)
            {
                var array = new JArray();
                foreach (var a in WriteLineXy(br))
                {
                    array.Add(a);
                }

                ret.Add(array);
            }

            return ret;
        }

        /// <summary>
        /// Write a JSON polygon from well-known binary.
        /// </summary>
        /// <param name="br">
        /// Read from this.
        /// </param>
        /// <returns>
        /// The <see cref="JArray"/> enumerable for the polygon.
        /// </returns>
        private static IEnumerable<JArray> WritePolygonXyz(BinaryReader br)
        {
            var ret = new List<JArray>();
            int ringCount = br.ReadInt32();
            for (int ri = 0; ri < ringCount; ++ri)
            {
                var array = new JArray();
                foreach (var a in WriteLineXyz(br))
                {
                    array.Add(a);
                }

                ret.Add(array);
            }

            return ret;
        }

        /// <summary>
        /// Write a JSON polygon from well-known binary.
        /// </summary>
        /// <param name="br">
        /// Read from this.
        /// </param>
        /// <returns>
        /// The <see cref="JArray"/> enumerable for the polygon.
        /// </returns>
        private static IEnumerable<JArray> WritePolygonXyzm(BinaryReader br)
        {
            var ret = new List<JArray>();
            int ringCount = br.ReadInt32();
            for (int ri = 0; ri < ringCount; ++ri)
            {
                var array = new JArray();
                foreach (var a in WriteLineXyzm(br))
                {
                    array.Add(a);
                }

                ret.Add(array);
            }

            return ret;
        }

        /// <summary>
        /// Write a JSON line from well-known binary.
        /// </summary>
        /// <param name="br">
        /// Read from this.
        /// </param>
        /// <returns>
        /// The <see cref="JArray"/> enumerable for the line.
        /// </returns>
        private static IEnumerable<JArray> WriteLineXy(BinaryReader br)
        {
            var ret = new List<JArray>();
            int count = br.ReadInt32() * 2;
            for (int i = 0; i < count; i += 2)
            {
                var array = new JArray { br.ReadDouble(), br.ReadDouble() };
                ret.Add(array);
            }

            return ret;
        }

        /// <summary>
        /// Write a JSON line from well-known binary.
        /// </summary>
        /// <param name="br">
        /// Read from this.
        /// </param>
        /// <returns>
        /// The <see cref="JArray"/> enumerable for the line.
        /// </returns>
        private static IEnumerable<JArray> WriteLineXyz(BinaryReader br)
        {
            var ret = new List<JArray>();
            int count = br.ReadInt32() * 2;
            for (int i = 0; i < count; i += 2)
            {
                var array = new JArray { br.ReadDouble(), br.ReadDouble(), br.ReadDouble() };
                ret.Add(array);
            }

            return ret;
        }

        /// <summary>
        /// Write a JSON line from well-known binary.
        /// </summary>
        /// <param name="br">
        /// Read from this.
        /// </param>
        /// <returns>
        /// The <see cref="JArray"/> enumerable for the line.
        /// </returns>
        private static IEnumerable<JArray> WriteLineXyzm(BinaryReader br)
        {
            var ret = new List<JArray>();
            int count = br.ReadInt32() * 2;
            for (int i = 0; i < count; i += 2)
            {
                var array = new JArray { br.ReadDouble(), br.ReadDouble(), br.ReadDouble(), br.ReadDouble() };
                ret.Add(array);
            }

            return ret;
        }

        /// <summary>
        /// Create a <see cref="DbGeography"/> or <see cref="DbGeometry"/> from <paramref name="jsonObject"/>.
        /// </summary>
        /// <param name="jsonObject">
        /// The JSON object.
        /// </param>
        /// <param name="objectType">
        /// The object type.
        /// </param>
        /// <returns>
        /// The <see cref="DbGeography"/> or <see cref="DbGeometry"/> 
        /// </returns>
        /// <exception cref="ArgumentException">
        /// <paramref name="objectType"/> is not a <see cref="DbGeography"/> or <see cref="DbGeometry"/>.
        /// </exception>
        private static object CreateDbGeo(DbGeographyGeoJsonConverter converter, JObject jsonObject, Type objectType)
        {
            Func<Tuple<byte[], int>, object> returnValue;
            int defaultCoordinateSystemId;
            if (typeof(DbGeography).IsAssignableFrom(objectType))
            {
                returnValue = x => (object)DbGeography.FromBinary(x.Item1, x.Item2);
                defaultCoordinateSystemId = DbGeography.DefaultCoordinateSystemId;
            }
            else if (typeof(DbGeometry).IsAssignableFrom(objectType))
            {
                returnValue = x => (object)DbGeometry.FromBinary(x.Item1, x.Item2);
                defaultCoordinateSystemId = DbGeometry.DefaultCoordinateSystemId;
            }
            else
            {
                throw new ArgumentException(string.Format("Expected a DbGeography or DbGeometry objectType. Got {0}", objectType.Name), "objectType");
            }

            return jsonObject.Type == JTokenType.Null || jsonObject.Type == JTokenType.None ? null : returnValue(GetWellKnownBinary(converter, jsonObject, defaultCoordinateSystemId));
        }

        /// <summary>
        /// A <see cref="BinaryReader"/> that expects byte-reversed numeric values.
        /// </summary>
        public class ReverseEndianBinaryReader : BinaryReader
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="ReverseEndianBinaryReader"/> class.
            /// </summary>
            /// <param name="stream">
            /// The stream.
            /// </param>
            public ReverseEndianBinaryReader(Stream stream)
                : base(stream)
            {
            }

            /// <inheritdoc/>
            public override short ReadInt16()
            {
                return BitConverter.ToInt16(this.ReadBytes(2).Reverse().ToArray(), 0);
            }

            /// <inheritdoc/>
            public override int ReadInt32()
            {
                return BitConverter.ToInt32(this.ReadBytes(4).Reverse().ToArray(), 0);
            }

            /// <inheritdoc/>
            public override long ReadInt64()
            {
                return BitConverter.ToInt64(this.ReadBytes(8).Reverse().ToArray(), 0);
            }

            /// <inheritdoc/>
            public override ushort ReadUInt16()
            {
                return BitConverter.ToUInt16(this.ReadBytes(2).Reverse().ToArray(), 0);
            }

            /// <inheritdoc/>
            public override uint ReadUInt32()
            {
                return BitConverter.ToUInt32(this.ReadBytes(4).Reverse().ToArray(), 0);
            }

            /// <inheritdoc/>
            public override ulong ReadUInt64()
            {
                return BitConverter.ToUInt64(this.ReadBytes(8).Reverse().ToArray(), 0);
            }

            /// <inheritdoc/>
            public override float ReadSingle()
            {
                return BitConverter.ToSingle(this.ReadBytes(4).Reverse().ToArray(), 0);
            }

            /// <inheritdoc/>
            public override double ReadDouble()
            {
                return BitConverter.ToDouble(this.ReadBytes(8).Reverse().ToArray(), 0);
            }
        }

        /// <summary>
        /// Base class for the types that know how to parse JSON and write well-known binary.
        /// </summary>
        public abstract class GeoBase
        {
           
            /// <summary>
            /// The point well-known bytes descriptor.
            /// </summary>
            protected static readonly byte[] PointXyWkbs = BitConverter.GetBytes(PointWkb);

            /// <summary>
            /// The line-string well-known bytes descriptor.
            /// </summary>
            protected static readonly byte[] LineStringXyWkbs = BitConverter.GetBytes(LineStringWkb);

            /// <summary>
            /// The polygon well-known bytes descriptor.
            /// </summary>
            protected static readonly byte[] PolygonXyWkbs = BitConverter.GetBytes(PolygonWkb);

            /// <summary>
            /// The multi-point well-known bytes descriptor.
            /// </summary>
            protected static readonly byte[] MultiPointXyWkbs = BitConverter.GetBytes(MultiPointWkb);

            /// <summary>
            /// The multi-line-string well-known bytes descriptor.
            /// </summary>
            protected static readonly byte[] MultiLineStringXyWkbs = BitConverter.GetBytes(MultiLineStringWkb);

            /// <summary>
            /// The multi-polygon well-known bytes descriptor.
            /// </summary>
            protected static readonly byte[] MultiPolygonXyWkbs = BitConverter.GetBytes(MultiPolygonWkb);

            /// <summary>
            /// The collection well-known bytes descriptor.
            /// </summary>
            protected static readonly byte[] GeometryCollectionXyWkbs = BitConverter.GetBytes(GeometryCollectionWkb);

            /// <summary>
            /// The point well-known bytes descriptor.
            /// </summary>
            protected static readonly byte[] PointXyzWkbs = BitConverter.GetBytes(PointWkb + 1000);

            /// <summary>
            /// The line-string well-known bytes descriptor.
            /// </summary>
            protected static readonly byte[] LineStringXyzWkbs = BitConverter.GetBytes(LineStringWkb + 1000);

            /// <summary>
            /// The polygon well-known bytes descriptor.
            /// </summary>
            protected static readonly byte[] PolygonXyzWkbs = BitConverter.GetBytes(PolygonWkb + 1000);

            /// <summary>
            /// The multi-point well-known bytes descriptor.
            /// </summary>
            protected static readonly byte[] MultiPointXyzWkbs = BitConverter.GetBytes(MultiPointWkb + 1000);

            /// <summary>
            /// The multi-line-string well-known bytes descriptor.
            /// </summary>
            protected static readonly byte[] MultiLineStringXyzWkbs = BitConverter.GetBytes(MultiLineStringWkb + 1000);

            /// <summary>
            /// The multi-polygon well-known bytes descriptor.
            /// </summary>
            protected static readonly byte[] MultiPolygonXyzWkbs = BitConverter.GetBytes(MultiPolygonWkb + 1000);

            /// <summary>
            /// The collection well-known bytes descriptor.
            /// </summary>
            protected static readonly byte[] GeometryCollectionXyzWkbs = BitConverter.GetBytes(GeometryCollectionWkb + 1000);

            /// <summary>
            /// The point well-known bytes descriptor.
            /// </summary>
            protected static readonly byte[] PointXyzmWkbs = BitConverter.GetBytes(PointWkb + 3000);

            /// <summary>
            /// The line-string well-known bytes descriptor.
            /// </summary>
            protected static readonly byte[] LineStringXyzmWkbs = BitConverter.GetBytes(LineStringWkb + 3000);

            /// <summary>
            /// The polygon well-known bytes descriptor.
            /// </summary>
            protected static readonly byte[] PolygonXyzmWkbs = BitConverter.GetBytes(PolygonWkb + 3000);

            /// <summary>
            /// The multi-point well-known bytes descriptor.
            /// </summary>
            protected static readonly byte[] MultiPointXyzmWkbs = BitConverter.GetBytes(MultiPointWkb + 3000);

            /// <summary>
            /// The multi-line-string well-known bytes descriptor.
            /// </summary>
            protected static readonly byte[] MultiLineStringXyzmWkbs = BitConverter.GetBytes(MultiLineStringWkb + 3000);

            /// <summary>
            /// The multi-polygon well-known bytes descriptor.
            /// </summary>
            protected static readonly byte[] MultiPolygonXyzmWkbs = BitConverter.GetBytes(MultiPolygonWkb + 3000);

            /// <summary>
            /// The collection well-known bytes descriptor.
            /// </summary>
            protected static readonly byte[] GeometryCollectionXyzmWkbs = BitConverter.GetBytes(GeometryCollectionWkb + 3000);


            public int? CoordinateSystem { get; set; }

            /// <summary>
            /// Helper function to parse a <see cref="List{T}"/> of <see cref="Position"/>.
            /// </summary>
            /// <param name="array">
            /// Get JSON from this.
            /// </param>
            /// <returns>
            /// The parsed JSON.
            /// </returns>
            /// <exception cref="ArgumentException">
            /// Unexpected JSON.
            /// </exception>
            public static List<Position> ParseListPosition(JArray array)
            {
                if (array.Cast<JArray>().Any(l => l.Count < 2))
                {
                    throw new ArgumentException(
                    string.Format(
                    "Expected all points to have greater than two points, got {0} with zero and {1} with one",
                    array.Cast<JArray>().Count(l => l.Count == 0),
                    array.Cast<JArray>().Count(l => l.Count == 1)),
                    "array");
                }

                return array.Select(l => new Position(l)).ToList();
            }

            /// <summary>
            /// Helper function to parse a <see cref="List{T}"/> of <see cref="List{T}"/> of <see cref="Position"/>.
            /// </summary>
            /// <param name="array">
            /// Get JSON from this.
            /// </param>
            /// <returns>
            /// The parsed JSON.
            /// </returns>
            /// <exception cref="ArgumentException">
            /// Unexpected JSON.
            /// </exception>
            public static List<List<Position>> ParseListListPosition(JArray array)
            {
                if (array.Cast<JArray>().Any(r => r.Cast<JArray>().Any(l => l.Count < 2)))
                {
                    throw new ArgumentException(
                    string.Format(
                    "Expected all points to have greater than two points, got {0} with zero and {1} with one",
                    array.Cast<JArray>().Sum(r => r.Cast<JArray>().Count(l => l.Count == 0)),
                    array.Cast<JArray>().Sum(r => r.Cast<JArray>().Count(l => l.Count == 1))),
                    "array");
                }

                return array.Select(r => r.Select(l => new Position(l)).ToList()).ToList();
            }

            /// <summary>
            /// Helper function to parse a <see cref="List{T}"/> of <see cref="List{T}"/> of <see cref="List{T}"/> of <see cref="Position"/>.
            /// </summary>
            /// <param name="array">
            /// Get JSON from this.
            /// </param>
            /// <returns>
            /// The parsed JSON.
            /// </returns>
            /// <exception cref="ArgumentException">
            /// Unexpected JSON.
            /// </exception>
            public static List<List<List<Position>>> ParseListListListPosition(JArray array)
            {
                if (array.Cast<JArray>().Any(p => p.Cast<JArray>().Any(r => r.Cast<JArray>().Any(l => l.Count < 2))))
                {
                    throw new ArgumentException(
                    string.Format(
                    "Expected all points to have greater than two points, got {0} with zero and {1} with one",
                    array.Cast<JArray>().Sum(p => p.Cast<JArray>().Sum(r => r.Cast<JArray>().Count(l => l.Count == 0))),
                    array.Cast<JArray>().Sum(p => p.Cast<JArray>().Sum(r => r.Cast<JArray>().Count(l => l.Count == 1)))),
                    "array");
                }

                return array.Select(p => p.Select(r => r.Select(l => new Position(l)).ToList()).ToList()).ToList();
            }

            /// <summary>
            /// Write the contents to <paramref name="sout"/> in well-known 
            /// binary format.
            /// </summary>
            /// <param name="sout">
            /// The stream to write the position to.
            /// </param>
            public abstract void WellKnownBinary(Stream sout);

            /// <summary>
            /// Parse JSON into the <see cref="GeoBase"/>-derived type.
            /// </summary>
            /// <param name="array">
            /// Get JSON from this.
            /// </param>
            public abstract void ParseJson(DbGeographyGeoJsonConverter converter, JArray array);
        }

        /// <summary>
        /// The position.
        /// </summary>
        public class Position : GeoBase
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="Position"/> class.
            /// </summary>
            /// <param name="token">
            /// The <see cref="JToken"/> holding 2 or three doubles.
            /// </param>
            /// <exception cref="ArgumentException">
            /// If <paramref name="token"/> holds less than 2 values.
            /// </exception>
            public Position(JToken token)
            {
                if (token.Count() < 2)
                {
                    throw new ArgumentException(
                    string.Format("Expected at least 2 elements, got {0}", token.Count()),
                    "token");
                }

                this.P1 = (double)token[0];
                this.P2 = (double)token[1];
                if (token.Count() > 2)
                {
                    this.P3 = (double)token[2];
                    if (token.Count() > 3)
                    {
                        this.P4 = (double)token[3];
                    }
                }
            }

            /// <summary>
            /// Initializes a new instance of the <see cref="Position"/> class.
            /// </summary>
            /// <param name="array">
            /// The <see cref="JToken"/> holding 2 or three doubles.
            /// </param>
            /// <exception cref="ArgumentException">
            /// If <paramref name="array"/> holds less than 2 values.
            /// </exception>
            public Position(JArray array)
            {
                if (array.Count() < 2)
                {
                    throw new ArgumentException(
                    string.Format("Expected at least 2 elements, got {0}", array.Count()),
                    "array");
                }

                this.P1 = (double)array[0];
                this.P2 = (double)array[1];
                if (array.Count() > 2)
                {
                    this.P3 = (double)array[2];
                    if (array.Count() > 3)
                    {
                        this.P4 = (double)array[3];
                    }
                }
            }

            public Position(double[] array)
            {
                if (array.Length < 2)
                {
                    throw new ArgumentException(
                    string.Format("Expected at least 2 elements, got {0}", array.Length),
                    "array");
                }

                this.P1 = array[0];
                this.P2 = array[1];
                if (array.Length > 2)
                {
                    this.P3 = array[2];
                    if (array.Length > 3)
                    {
                        this.P4 = array[3];
                    }
                }
            }

            /// <summary>
            /// Gets or sets the first value of the position.
            /// </summary>
            public double P1 { get; set; }

            /// <summary>
            /// Gets or sets the second value of the position.
            /// </summary>
            public double P2 { get; set; }

            /// <summary>
            /// Gets or sets the third value of the position.
            /// </summary>
            public double? P3 { get; set; }

            /// <summary>
            /// Gets or sets the fourth value of the position.
            /// </summary>
            public double? P4 { get; set; }

            /// <summary>
            /// Gets or sets the indexes per point. Can only reduce the 
            /// indexes per point on set, it is an error to try to increase the
            /// value.
            /// </summary>
            public IndexesPerPoint IndexesPerPoint
            {
                get
                {
                    return this.P4.HasValue
                    ? IndexesPerPoint.Four
                    : this.P3.HasValue ? IndexesPerPoint.Three : IndexesPerPoint.Two;
                }

                set
                {
                    switch (IndexesPerPoint)
                    {
                        case IndexesPerPoint.Two:
                            switch (value)
                            {
                                case IndexesPerPoint.Four:
                                    this.P4 = double.NaN;
                                    goto case IndexesPerPoint.Three;
                                case IndexesPerPoint.Three:
                                    this.P3 = double.NaN;
                                    break;
                            }

                            break;
                        case IndexesPerPoint.Three:
                            switch (value)
                            {
                                case IndexesPerPoint.Two:
                                    this.P3 = null;
                                    break;
                                case IndexesPerPoint.Four:
                                    this.P4 = double.NaN;
                                    break;
                            }

                            break;
                        case IndexesPerPoint.Four:
                            switch (value)
                            {
                                case IndexesPerPoint.Two:
                                    this.P3 = null;
                                    goto case IndexesPerPoint.Three;
                                case IndexesPerPoint.Three:
                                    this.P4 = null;
                                    break;
                            }

                            break;
                    }
                }
            }

            /// <inheritdoc/>
            public override void WellKnownBinary(Stream sout)
            {
                sout.Write(BitConverter.GetBytes(this.P1), 0, 8);
                sout.Write(BitConverter.GetBytes(this.P2), 0, 8);
                if (this.P3.HasValue)
                {
                    sout.Write(BitConverter.GetBytes(this.P3.Value), 0, 8);
                    if (this.P4.HasValue)
                    {
                        sout.Write(BitConverter.GetBytes(this.P4.Value), 0, 8);
                    }
                }
            }

            /// <inheritdoc/>
            public override void ParseJson(DbGeographyGeoJsonConverter converter,JArray array)
            {
                if (array.Count < 2)
                {
                    throw new ArgumentException(string.Format("Expected at least 2 points for a position, got {0}", array.Count), "array");
                }

                this.P1 = (double)array[0];
                this.P2 = (double)array[1];
                if (array.Count > 2)
                {
                    this.P3 = (double)array[2];
                    if (array.Count > 3)
                    {
                        this.P4 = (double)array[3];
                    }
                }
            }
        }

        // ReSharper disable RedundantNameQualifier

        /// <summary>
        /// The point.
        /// </summary>
        public class Point : GeoBase
        {
            /// <summary>
            /// Gets or sets the position.
            /// </summary>
            public Position Position { get; set; }

            /// <inheritdoc/>
            public override void WellKnownBinary(Stream sout)
            {
                sout.WriteByte(BitConverter.IsLittleEndian ? (byte)1 : (byte)0);
                sout.Write(
                this.Position.P3.HasValue
                ? this.Position.P4.HasValue ? GeoBase.PointXyzmWkbs : GeoBase.PointXyzWkbs
                : GeoBase.PointXyWkbs,
                0,
                4);
                this.Position.WellKnownBinary(sout);
            }

            /// <inheritdoc/>
            public override void ParseJson(DbGeographyGeoJsonConverter converter,JArray array)
            {
                this.Position = new Position(array);
            }
        }

        /// <summary>
        /// The line string.
        /// </summary>
        public class LineString : GeoBase
        {
            /// <summary>
            /// Gets or sets the points.
            /// </summary>
            public List<Point> Points { get; set; }

            /// <inheritdoc/>
            public override void WellKnownBinary(Stream sout)
            {
                sout.WriteByte(BitConverter.IsLittleEndian ? (byte)1 : (byte)0);
                sout.Write(this.Normalize(), 0, 4);
                sout.Write(BitConverter.GetBytes(this.Points.Count), 0, 4);

                foreach (var point in this.Points)
                {
                    point.Position.WellKnownBinary(sout);
                }
            }

            /// <inheritdoc/>
            public override void ParseJson(DbGeographyGeoJsonConverter converter,JArray array)
            {
                if (array.Cast<JArray>().Any(l => l.Count < 2))
                {
                    throw new ArgumentException(
                    string.Format(
                    "Expected all points to have greater than two points, got {0} with zero and {1} with one",
                    array.Cast<JArray>().Count(l => l.Count == 0),
                    array.Cast<JArray>().Count(l => l.Count == 1)),
                    "array");
                }

                this.Points = array.Cast<JArray>().Select(l => new Point { Position = new Position(l) }).ToList();
            }

            /// <summary>
            /// Validate all the positions have the same number of indexes and return the well-known-bytes for that number.
            /// </summary>
            /// <returns>
            /// The well-known-bytes describing the geographic type.
            /// </returns>
            private byte[] Normalize()
            {
                if (this.Points.Any())
                {
                    var low = IndexesPerPoint.Four;
                    var high = IndexesPerPoint.Two;
                    foreach (var point in this.Points)
                    {
                        if (point.Position.IndexesPerPoint > high)
                        {
                            high = point.Position.IndexesPerPoint;
                        }

                        if (point.Position.IndexesPerPoint < low)
                        {
                            low = point.Position.IndexesPerPoint;
                        }
                    }

                    if (high != low)
                    {
                        foreach (var point in this.Points)
                        {
                            point.Position.IndexesPerPoint = high;
                        }
                    }

                    return low == IndexesPerPoint.Two
                    ? GeoBase.LineStringXyWkbs
                    : low == IndexesPerPoint.Three ? GeoBase.LineStringXyzWkbs : GeoBase.LineStringXyzmWkbs;
                }

                return GeoBase.LineStringXyWkbs;
            }
        }

        /// <summary>
        /// The polygon.
        /// </summary>
        public class Polygon : GeoBase
        {
            /// <summary>
            /// Gets or sets the rings.
            /// </summary>
            public List<List<Position>> Rings { get; set; }

            /// <inheritdoc/>
            public override void WellKnownBinary(Stream sout)
            {
                sout.WriteByte(BitConverter.IsLittleEndian ? (byte)1 : (byte)0);
                sout.Write(this.Normalize(), 0, 4);
                sout.Write(BitConverter.GetBytes(this.Rings.Count), 0, 4);
                foreach (var ring in this.Rings)
                {
                    sout.Write(BitConverter.GetBytes(ring.Count), 0, 4);
                    foreach (var position in ring)
                    {
                        position.WellKnownBinary(sout);
                    }
                }
            }

            /// <inheritdoc/>
            public override void ParseJson(DbGeographyGeoJsonConverter converter,JArray array)
            {
                this.Rings = GeoBase.ParseListListPosition(array);
            }

            /// <summary>
            /// Validate all the positions have the same number of indexes and return the well-known-bytes for that number.
            /// </summary>
            /// <returns>
            /// The well-known-bytes describing the geographic type.
            /// </returns>
            private byte[] Normalize()
            {
                if (this.Rings.Any())
                {
                    var low = IndexesPerPoint.Four;
                    var high = IndexesPerPoint.Two;
                    foreach (var position in this.Rings.SelectMany(r => r))
                    {
                        if (position.IndexesPerPoint > high)
                        {
                            high = position.IndexesPerPoint;
                        }

                        if (position.IndexesPerPoint < low)
                        {
                            low = position.IndexesPerPoint;
                        }
                    }

                    if (high != low)
                    {
                        foreach (var position in this.Rings.SelectMany(r => r))
                        {
                            position.IndexesPerPoint = high;
                        }
                    }

                    return low == IndexesPerPoint.Two
                    ? GeoBase.PolygonXyWkbs
                    : low == IndexesPerPoint.Three ? GeoBase.PolygonXyzWkbs : GeoBase.PolygonXyzmWkbs;
                }

                return GeoBase.PolygonXyWkbs;
            }
        }

        /// <summary>
        /// The multi-point.
        /// </summary>
        public class MultiPoint : GeoBase
        {
            /// <summary>
            /// Gets or sets the points.
            /// </summary>
            public List<Position> Points { get; set; }

            /// <inheritdoc/>
            public override void WellKnownBinary(Stream sout)
            {
                byte order = BitConverter.IsLittleEndian ? (byte)1 : (byte)0;
                sout.WriteByte(order);
                sout.Write(this.MaxWkbsType(), 0, 4);
                sout.Write(BitConverter.GetBytes(this.Points.Count), 0, 4);
                foreach (var point in this.Points)
                {
                    sout.WriteByte(order);
                    var ipp = point.IndexesPerPoint;
                    sout.Write(ipp == IndexesPerPoint.Two ? GeoBase.PointXyWkbs : ipp == IndexesPerPoint.Three ? GeoBase.PointXyzWkbs : GeoBase.PointXyzmWkbs, 0, 4);
                    point.WellKnownBinary(sout);
                }
            }

            /// <inheritdoc/>
            public override void ParseJson(DbGeographyGeoJsonConverter converter,JArray array)
            {
                this.Points = GeoBase.ParseListPosition(array);
            }

            /// <summary>
            /// Validate all the positions have the same number of indexes and return the well-known-bytes for that number.
            /// </summary>
            /// <returns>
            /// The well-known-bytes describing the geographic type.
            /// </returns>
            private byte[] MaxWkbsType()
            {
                var high = IndexesPerPoint.Two;
                foreach (var position in this.Points)
                {
                    if (position.IndexesPerPoint > high)
                    {
                        high = position.IndexesPerPoint;
                    }
                }

                return high == IndexesPerPoint.Two
                ? GeoBase.MultiPointXyWkbs
                : high == IndexesPerPoint.Three ? GeoBase.MultiPointXyzWkbs : GeoBase.MultiPointXyzmWkbs;
            }
        }

        /// <summary>
        /// The multi-line.
        /// </summary>
        public class MultiLineString : GeoBase
        {
            /// <summary>
            /// Gets or sets the line strings.
            /// </summary>
            public List<List<Position>> LineStrings { get; set; }

            /// <inheritdoc/>
            public override void WellKnownBinary(Stream sout)
            {
                byte order = BitConverter.IsLittleEndian ? (byte)1 : (byte)0;
                sout.WriteByte(order);
                // ReSharper disable once RedundantNameQualifier
                sout.Write(this.WkbsType(), 0, 4);
                sout.Write(BitConverter.GetBytes(this.LineStrings.Count), 0, 4);
                foreach (var lineString in this.LineStrings)
                {
                    sout.WriteByte(order);
                    var ipp = lineString.Any() ? lineString.First().IndexesPerPoint : IndexesPerPoint.Two;
                    sout.Write(ipp == IndexesPerPoint.Two ? GeoBase.LineStringXyWkbs : ipp == IndexesPerPoint.Three ? GeoBase.LineStringXyzWkbs : GeoBase.LineStringXyzmWkbs, 0, 4);
                    sout.Write(BitConverter.GetBytes(lineString.Count), 0, 4);
                    foreach (var position in lineString)
                    {
                        position.WellKnownBinary(sout);
                    }
                }
            }

            /// <inheritdoc/>
            public override void ParseJson(DbGeographyGeoJsonConverter converter,JArray array)
            {
                this.LineStrings = GeoBase.ParseListListPosition(array);
            }

            /// <summary>
            /// Validate all the positions have the same number of indexes and return the well-known-bytes for that number.
            /// </summary>
            /// <returns>
            /// The well-known-bytes describing the geographic type.
            /// </returns>
            private byte[] WkbsType()
            {
                if (this.LineStrings.Any())
                {
                    var maxIpp = IndexesPerPoint.Two;
                    foreach (var lineString in this.LineStrings)
                    {
                        var low = IndexesPerPoint.Four;
                        var high = IndexesPerPoint.Two;
                        foreach (var position in lineString)
                        {
                            if (position.IndexesPerPoint > high)
                            {
                                high = position.IndexesPerPoint;
                            }

                            if (position.IndexesPerPoint < low)
                            {
                                low = position.IndexesPerPoint;
                            }
                        }

                        if (high != low)
                        {
                            foreach (var position in lineString)
                            {
                                position.IndexesPerPoint = high;
                            }
                        }

                        if (low > maxIpp)
                        {
                            maxIpp = low;
                        }
                    }

                    return maxIpp == IndexesPerPoint.Two
                    ? GeoBase.MultiLineStringXyWkbs
                    : maxIpp == IndexesPerPoint.Three ? GeoBase.MultiLineStringXyzWkbs : GeoBase.MultiLineStringXyzmWkbs;
                }

                return GeoBase.MultiLineStringXyWkbs;
            }
        }

        /// <summary>
        /// The multi-polygon.
        /// </summary>
        public class MultiPolygon : GeoBase
        {
            /// <summary>
            /// Gets or sets the polygons.
            /// </summary>
            public List<List<List<Position>>> Polygons { get; set; }

            /// <inheritdoc/>
            public override void WellKnownBinary(Stream sout)
            {
                byte order = BitConverter.IsLittleEndian ? (byte)1 : (byte)0;
                sout.WriteByte(order);
                sout.Write(this.WkbsType(), 0, 4);
                sout.Write(BitConverter.GetBytes(this.Polygons.Count), 0, 4);
                foreach (var polygon in this.Polygons)
                {
                    sout.WriteByte(order);
                    var ipp = polygon.Any() && polygon.First().Any()
                    ? polygon.First().First().IndexesPerPoint
                    : IndexesPerPoint.Two;
                    sout.Write(ipp == IndexesPerPoint.Two ? GeoBase.PolygonXyWkbs : ipp == IndexesPerPoint.Three ? GeoBase.PolygonXyzWkbs : GeoBase.PolygonXyzmWkbs, 0, 4);
                    sout.Write(BitConverter.GetBytes(polygon.Count), 0, 4);
                    foreach (var ring in polygon)
                    {
                        sout.Write(BitConverter.GetBytes(ring.Count), 0, 4);
                        foreach (var position in ring)
                        {
                            position.WellKnownBinary(sout);
                        }
                    }
                }
            }

            /// <inheritdoc/>
            public override void ParseJson(DbGeographyGeoJsonConverter converter,JArray array)
            {
                this.Polygons = GeoBase.ParseListListListPosition(array);
            }

            /// <summary>
            /// Validate all the positions have the same number of indexes and return the well-known-bytes for that number.
            /// </summary>
            /// <returns>
            /// The well-known-bytes describing the geographic type.
            /// </returns>
            private byte[] WkbsType()
            {
                if (this.Polygons.Any())
                {
                    var maxIpp = IndexesPerPoint.Two;
                    foreach (var polygon in this.Polygons)
                    {
                        var low = IndexesPerPoint.Four;
                        var high = IndexesPerPoint.Two;
                        foreach (var position in polygon.SelectMany(r => r))
                        {
                            if (position.IndexesPerPoint > high)
                            {
                                high = position.IndexesPerPoint;
                            }

                            if (position.IndexesPerPoint < low)
                            {
                                low = position.IndexesPerPoint;
                            }
                        }

                        if (high != low)
                        {
                            foreach (var position in polygon.SelectMany(r => r))
                            {
                                position.IndexesPerPoint = high;
                            }
                        }

                        if (high > maxIpp)
                        {
                            maxIpp = high;
                        }
                    }

                    return maxIpp == IndexesPerPoint.Two
                    ? GeoBase.MultiPolygonXyWkbs
                    : maxIpp == IndexesPerPoint.Three ? GeoBase.MultiPolygonXyzWkbs : GeoBase.MultiPolygonXyzmWkbs;
                }

                return GeoBase.MultiPolygonXyWkbs;
            }
        }

        /// <summary>
        /// The <see cref="GeoBase"/> collection.
        /// </summary>
        public class Collection : GeoBase
        {
            /// <summary>
            /// Gets or sets the entries.
            /// </summary>
            public List<GeoBase> Entries { get; set; }

            /// <inheritdoc/>
            public override void WellKnownBinary(Stream o)
            {
                o.WriteByte(BitConverter.IsLittleEndian ? (byte)1 : (byte)0);
                o.Write(GeoBase.GeometryCollectionXyWkbs, 0, 4);
                o.Write(BitConverter.GetBytes(this.Entries.Count), 0, 4);
                foreach (var entry in this.Entries)
                {
                    entry.WellKnownBinary(o);
                }
            }

            /// <inheritdoc/>
            public override void ParseJson(DbGeographyGeoJsonConverter converter, JArray array)
            {
                this.Entries = new List<GeoBase>();
                foreach (var elem in array)
                {
                    if (elem.Type != JTokenType.Object)
                    {
                        throw new ArgumentException(
                        string.Format("Expected object elements of the collection array, got {0}", elem.Type),
                        "array");
                    }

                    int? dummyCoordinateSystem;
                    this.Entries.Add(DbGeographyGeoJsonConverter.ParseJsonObjectToGeoBase(converter, (JObject)elem, out dummyCoordinateSystem));
                }
            }
        }

        // ReSharper restore RedundantNameQualifier
    }

    public class OgrEntityConverter : JsonConverter
    {
        private bool doEnumerate = true;
        public override bool CanConvert(Type objectType)
        {
            if (typeof(OgrEntity).IsAssignableFrom(objectType))
                return true;
            //    else if (typeof(DbGeometry).IsAssignableFrom(objectType))
            //        return true;
            else if (typeof(IEnumerable<OgrEntity>).IsAssignableFrom(objectType))
                return doEnumerate && true;
            return false;
        }


        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var entity = value as OgrEntity;

            if (entity != null)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("type"); writer.WriteValue("feature");
                writer.WritePropertyName("geometry"); serializer.Serialize(writer, entity.Geometry);
                writer.WritePropertyName("properties");
                // writer.WriteStartObject();
                //  serializer.Serialize(writer, entity);
                var child = JsonSerializer.Create();
                child.Serialize(writer, entity);
                //  writer.WriteEndObject();
                writer.WriteEndObject();


                //var geoFeature = new JObject(
                //                        new JProperty("type", "feature"),
                //                        new JProperty("geometry", new JObject(
                //                            new JProperty("type", "polygon"),
                //                            new JProperty("coordinates", feature.Geometry.WellKnownValue.WellKnownText),
                //                            new JProperty("properties",
                //                                    JObject.FromObject(feature)
                //                                )
                //                        )));


                // Base serialization is fine
                //    serializer.Serialize(writer, geoFeature);
                return;
            }
            var geometry = value as DbGeometry;
            if (geometry != null)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("type"); writer.WriteValue(geometry.SpatialTypeName);
                //       writer.writ
                writer.WriteEndObject();
                return;
            }
            var features = value as IEnumerable<OgrEntity>;
            if (features != null)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("type"); writer.WriteValue("FeatureCollection");
                writer.WritePropertyName("features");
                writer.WriteStartArray();
                foreach (var el in features)
                    serializer.Serialize(writer, el);
                writer.WriteEndArray();
                writer.WriteEndObject();
            }

        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JToken jsonToken = JToken.Load(reader);
            var jsonObject = jsonToken as JObject;
            var jsonArray = jsonToken as JArray;

            if (typeof(IEnumerable<OgrEntity>).IsAssignableFrom(objectType))
                if (jsonObject != null)
                {
                    var features = jsonObject.GetValue("features");
                    return features.ToObject(objectType, serializer);
                }
                else if (jsonArray != null)
                {
   
                    doEnumerate = false;
                    var arr = jsonArray.ToObject(objectType,serializer);
                    doEnumerate = true;
                    return arr;
                }


            var geom = jsonObject.GetValue("geometry");
            var dbGeom = geom.ToObject<DbGeometry>(serializer);
            var probs = jsonObject.GetValue("properties");

            var obj = probs.ToObject(objectType) as OgrEntity;
            obj.Geometry = dbGeom;
            return obj;
        }
    }
}
