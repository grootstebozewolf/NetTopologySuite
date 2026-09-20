using System;
using System.IO;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Curves;

namespace NetTopologySuite.IO
{
    /// <summary>
    /// Converts a Well-Known Binary byte data to a <c>Geometry</c>.
    /// </summary>
    /// <remarks>
    /// This class reads the format describe in {@link WKBWriter}.
    /// It partially handles the<b>Extended WKB</b> format used by PostGIS,
    /// by parsing and storing optional SRID values.
    /// If a SRID is not specified in an element geometry, it is inherited
    /// from the parent's SRID.
    /// The default SRID value depends on <see cref="NtsGeometryServices.DefaultSRID"/>.
    /// <para/>
    /// Although not defined in the WKB spec, empty points
    /// are handled if they are represented as a Point with <c>NaN</c> X and Y ordinates.
    /// <para/>
    /// The reader repairs structurally-invalid input
    /// (specifically, LineStrings and LinearRings which contain
    /// too few points have vertices added,
    /// and non-closed rings are closed).
    /// <para/>
    /// The reader handles most errors caused by malformed or malicious WKB data.
    /// It checks for obviously excessive values of the fields
    /// <c>numElems</c>, <c>numRings</c>, and <c>numCoords</c>.
    /// It also checks that the reader does not read beyond the end of the data supplied.
    /// A <see cref="ParseException"/> is thrown if this situation is detected.
    /// </remarks>
    public class WKBReader
    {
        /// <summary>
        /// Converts a hexadecimal string to a byte array.
        /// The hexadecimal digit symbols are case-insensitive.
        /// </summary>
        /// <param name="hex">A string containing hex digits</param>
        /// <returns>An array of bytes with the value of the hex string</returns>
        public static byte[] HexToBytes(string hex)
        {
            int byteLen = hex.Length / 2;
            byte[] bytes = new byte[byteLen];

            for (int i = 0; i < hex.Length / 2; i++)
            {
                int i2 = 2 * i;
                if (i2 + 1 > hex.Length)
                    throw new ArgumentException("Hex string has odd length");

                int nib1 = HexToInt(hex[i2]);
                int nib0 = HexToInt(hex[i2 + 1]);
                bytes[i] = (byte)((nib1 << 4) + (byte)nib0);
            }
            return bytes;
        }

        private static int HexToInt(char hex)
        {
            switch (hex)
            {
                case '0':
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9':
                    return hex - '0';
                case 'A':
                case 'B':
                case 'C':
                case 'D':
                case 'E':
                case 'F':
                    return hex - 'A' + 10;
                case 'a':
                case 'b':
                case 'c':
                case 'd':
                case 'e':
                case 'f':
                    return hex - 'a' + 10;
            }
            throw new ArgumentException("Invalid hex digit: " + hex);
        }

        private const string FieldNumCoords = "numCoords";
        private const string FieldNumRings = "numRings";
        private const string FieldNumElements = "numElements";


        private readonly CoordinateSequenceFactory _sequenceFactory;
        private readonly PrecisionModel _precisionModel;

        private readonly NtsGeometryServices _geometryServices;

        /*
         * true if structurally invalid input should be reported rather than repaired.
         */
        private bool _isStrict;

        /// <summary>
        /// Initialize reader with a standard <see cref="NtsGeometryServices"/>.
        /// </summary>
        public WKBReader() : this(NtsGeometryServices.Instance) { }

        /// <summary>
        /// Creates an instance of this class using the provided <c>NtsGeometryServices</c>
        /// </summary>
        /// <param name="services"></param>
        public WKBReader(NtsGeometryServices services)
        {
            services = services ?? NtsGeometryServices.Instance;
            _geometryServices = services;
            _precisionModel = services.DefaultPrecisionModel;
            _sequenceFactory = services.DefaultCoordinateSequenceFactory;

            HandleSRID = true;
            HandleOrdinates = AllowedOrdinates;
        }

        /// <summary>
        /// Reads a <see cref="Geometry"/> in binary WKB format from an array of <see cref="byte"/>s.
        /// </summary>
        /// <param name="data">The byte array to read from</param>
        /// <returns>The geometry read</returns>
        /// <exception cref="ParseException"> if the WKB data is ill-formed.</exception>
        public Geometry Read(byte[] data)
        {
            using (Stream stream = new MemoryStream(data))
                return Read(stream);
        }

        /// <summary>
        /// Reads a <see cref="Geometry"/> in binary WKB format from an <see cref="Stream"/>.
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        /// <returns>The geometry read</returns>
        /// <exception cref="ParseException"> if the WKB data is ill-formed.</exception>
        public virtual Geometry Read(Stream stream)
        {
            using (var reader = new BiEndianBinaryReader(stream))
                return Read(reader);
        }

        /// <summary>
        /// WKB Coordinate Systems
        /// </summary>
        protected enum CoordinateSystem
        {
            /// <summary>
            /// 2D coordinate system
            /// </summary>
            XY = 1,
            /// <summary>
            /// 3D coordinate system
            /// </summary>
            XYZ = 2,
            /// <summary>
            /// 2D coordinate system with additional measure value
            /// </summary>
            XYM = 3,
            /// <summary>
            /// 3D coordinate system with additional measure value
            /// </summary>
            XYZM = 4
        };

        /// <summary>
        ///
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        protected Geometry Read(BinaryReader reader)
        {
            try
            {
                ReadByteOrder(reader);
                int srid = _geometryServices.DefaultSRID;
                var geometryType = ReadGeometryType(reader, out var cs, ref srid);
                switch (geometryType)
                {
                    //Point
                    case WKBGeometryTypes.WKBPoint:
                    case WKBGeometryTypes.WKBPointZ:
                    case WKBGeometryTypes.WKBPointM:
                    case WKBGeometryTypes.WKBPointZM:
                        return ReadPoint(reader, cs, srid);
                    //Line String
                    case WKBGeometryTypes.WKBLineString:
                    case WKBGeometryTypes.WKBLineStringZ:
                    case WKBGeometryTypes.WKBLineStringM:
                    case WKBGeometryTypes.WKBLineStringZM:
                        return ReadLineString(reader, cs, srid);
                    //Polygon
                    case WKBGeometryTypes.WKBPolygon:
                    case WKBGeometryTypes.WKBPolygonZ:
                    case WKBGeometryTypes.WKBPolygonM:
                    case WKBGeometryTypes.WKBPolygonZM:
                        return ReadPolygon(reader, cs, srid);
                    //Multi Point
                    case WKBGeometryTypes.WKBMultiPoint:
                    case WKBGeometryTypes.WKBMultiPointZ:
                    case WKBGeometryTypes.WKBMultiPointM:
                    case WKBGeometryTypes.WKBMultiPointZM:
                        return ReadMultiPoint(reader, cs, srid);
                    //Multi Line String
                    case WKBGeometryTypes.WKBMultiLineString:
                    case WKBGeometryTypes.WKBMultiLineStringZ:
                    case WKBGeometryTypes.WKBMultiLineStringM:
                    case WKBGeometryTypes.WKBMultiLineStringZM:
                        return ReadMultiLineString(reader, cs, srid);
                    //Multi Polygon
                    case WKBGeometryTypes.WKBMultiPolygon:
                    case WKBGeometryTypes.WKBMultiPolygonZ:
                    case WKBGeometryTypes.WKBMultiPolygonM:
                    case WKBGeometryTypes.WKBMultiPolygonZM:
                        return ReadMultiPolygon(reader, cs, srid);
                    //Geometry Collection
                    case WKBGeometryTypes.WKBGeometryCollection:
                    case WKBGeometryTypes.WKBGeometryCollectionZ:
                    case WKBGeometryTypes.WKBGeometryCollectionM:
                    case WKBGeometryTypes.WKBGeometryCollectionZM:
                        return ReadGeometryCollection(reader, cs, srid);
                    // SQL/MM curves (GEOS / ISO 13249-3)
                    case WKBGeometryTypes.WKBCircularString:
                        return ReadCircularString(reader, cs, srid);
                    case WKBGeometryTypes.WKBCompoundCurve:
                        return ReadCompoundCurve(reader, cs, srid);
                    case WKBGeometryTypes.WKBCurvePolygon:
                        return ReadCurvePolygon(reader, cs, srid);
                    case WKBGeometryTypes.WKBMultiCurve:
                        return ReadMultiCurve(reader, cs, srid);
                    case WKBGeometryTypes.WKBMultiSurface:
                        return ReadMultiSurface(reader, cs, srid);
                    case WKBGeometryTypes.WKBTin:
                        return ReadTin(reader, cs, srid);
                    case WKBGeometryTypes.WKBCircle:
                        return ReadCircle(reader, cs, srid);
                    default:
                        throw new ArgumentException("Geometry type not recognized. GeometryCode: " + geometryType);
                }
            }
            catch(Exception ex)
            {
                throw new ParseException(ex);
            }
            //catch(IOException io)
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="reader"></param>
        private void ReadByteOrder(BinaryReader reader)
        {
            var byteOrder = (ByteOrder)reader.ReadByte();
            if (_isStrict && byteOrder != ByteOrder.BigEndian && byteOrder != ByteOrder.LittleEndian)
                throw new ParseException($"Unknown geometry byte order (not LittleEndian or BigEndian): {byteOrder}");

            ((BiEndianBinaryReader)reader).Endianess = byteOrder;
        }

        private WKBGeometryTypes ReadGeometryType(BinaryReader reader, out CoordinateSystem coordinateSystem, ref int srid)
        {
            uint type = reader.ReadUInt32();
            //Determine coordinate system
            if ((type & (0x80000000 | 0x40000000)) == (0x80000000 | 0x40000000))
                coordinateSystem = CoordinateSystem.XYZM;
            else if ((type & 0x80000000) == 0x80000000)
                coordinateSystem = CoordinateSystem.XYZ;
            else if ((type & 0x40000000) == 0x40000000)
                coordinateSystem = CoordinateSystem.XYM;
            else
                coordinateSystem = CoordinateSystem.XY;

            //Has SRID
            int newSrid = (type & 0x20000000) != 0 ? reader.ReadInt32() : -1;
            if (HandleSRID && newSrid >= 0) srid = newSrid;

            // ISO/IEC 13249-3 5.1.68 Table 15 lists an alternate code series
            // for the curved types (1000001-1000005 = CircularString ..
            // MultiSurface) alongside the base codes 8-12; accepted on read
            // since the masked "% 1000" scheme below cannot recover them
            // (1000001 & 0xffff == 16961). The writer emits base codes only
            // (NetTopologySuite.Proofs #615, ticket 615-i).
            uint plainType = type & 0x1FFFFFFF;
            if (plainType >= 1000001 && plainType <= 1000005)
                return (WKBGeometryTypes)(plainType - 1000001 + (uint)WKBGeometryTypes.WKBCircularString);

            //Get cs from prefix
            uint ordinate = (type & 0xffff) / 1000;
            switch (ordinate)
            {
                case 1:
                    coordinateSystem = CoordinateSystem.XYZ;
                    break;
                case 2:
                    coordinateSystem = CoordinateSystem.XYM;
                    break;
                case 3:
                    coordinateSystem = CoordinateSystem.XYZM;
                    break;
            }

            return (WKBGeometryTypes)((type & 0xffff) % 1000);
        }

        private static int ReasonableNumElements(Stream stream)
        {
            int remainingBytes = (int)(stream.Length - stream.Position) - 4;
            if (remainingBytes < 0) return int.MaxValue;

            return remainingBytes / 8;
        }

        private static int ReasonableNumCoordinates(Stream stream, CoordinateSystem cs)
        {
            int remainingBytes = (int)(stream.Length - stream.Position) - 4;
            if (remainingBytes < 0) return int.MaxValue;

            int size = 16;

            switch (cs)
            {
                case CoordinateSystem.XYM:
                case CoordinateSystem.XYZ:
                    size = 24;
                    break;
                case CoordinateSystem.XYZM:
                    size = 32;
                    break;
            }

            return remainingBytes / size;
        }

        private int ReadNumField(BinaryReader reader, string fieldName, int reasonableNumField = int.MaxValue)
        {
            // num field is unsigned int, but int should do
            int num = reader.ReadInt32();
            if (num < 0 || num > reasonableNumField)
            {
                throw new ParseException(fieldName + " value is too large");
            }
            return (int)num;
        }

        /// <summary>
        /// Function to read a coordinate sequence.
        /// </summary>
        /// <param name="reader">The reader</param>
        /// <param name="size">The number of ordinates</param>
        /// <param name="cs">The coordinate system</param>
        /// <returns>The read coordinate sequence.</returns>
        protected CoordinateSequence ReadCoordinateSequence(BinaryReader reader, int size, CoordinateSystem cs)
        {
            var sequence = _sequenceFactory.Create(size, ToOrdinates(cs));
            for (int i = 0; i < size; i++)
            {
                double x = reader.ReadDouble();
                double y = reader.ReadDouble();

                if (_precisionModel != null) x = _precisionModel.MakePrecise(x);
                if (_precisionModel != null) y = _precisionModel.MakePrecise(y);

                sequence.SetOrdinate(i, 0, x);
                sequence.SetOrdinate(i, 1, y);

                switch (cs)
                {
                    case CoordinateSystem.XY:
                        continue;
                    case CoordinateSystem.XYZ:
                        double z = reader.ReadDouble();
                        if (HandleOrdinate(Ordinate.Z))
                            sequence.SetOrdinate(i, 2, z);
                        break;
                    case CoordinateSystem.XYM:
                        double m = reader.ReadDouble();
                        if (HandleOrdinate(Ordinate.M))
                            sequence.SetOrdinate(i, 2, m);
                        break;
                    case CoordinateSystem.XYZM:
                        z = reader.ReadDouble();
                        if (HandleOrdinate(Ordinate.Z))
                            sequence.SetOrdinate(i, 2, z);
                        m = reader.ReadDouble();
                        if (HandleOrdinate(Ordinate.M))
                            sequence.SetOrdinate(i, 3, m);
                        break;
                    default:
                        throw new ArgumentException(string.Format("Coordinate system not supported: {0}", cs));
                }
            }
            return sequence;
        }

        /// <summary>
        /// Function to read a coordinate sequence that is supposed to form a ring.
        /// </summary>
        /// <param name="reader">The reader</param>
        /// <param name="size">The number of ordinates</param>
        /// <param name="cs">The coordinate system</param>
        /// <returns>The read coordinate sequence.</returns>
        protected CoordinateSequence ReadCoordinateSequenceRing(BinaryReader reader, int size, CoordinateSystem cs)
        {
            var seqence = ReadCoordinateSequence(reader, size, cs);
            if (_isStrict)
                return seqence;
            if (CoordinateSequences.IsRing(seqence))
                return seqence;
            return CoordinateSequences.EnsureValidRing(_sequenceFactory, seqence);
        }

        /// <summary>
        /// Function to read a coordinate sequence that is supposed to serve a line string.
        /// </summary>
        /// <param name="reader">The reader</param>
        /// <param name="size">The number of ordinates</param>
        /// <param name="cs">The coordinate system</param>
        /// <returns>The read coordinate sequence.</returns>
        protected CoordinateSequence ReadCoordinateSequenceLineString(BinaryReader reader, int size, CoordinateSystem cs)
        {
            var seq = ReadCoordinateSequence(reader, size, cs);
            if (_isStrict) return seq;
            if (seq.Count == 0 || seq.Count >= 2) return seq;
            return CoordinateSequences.Extend(_geometryServices.DefaultCoordinateSequenceFactory, seq, 2);
        }

        /// <summary>
        /// Function to convert from <see cref="CoordinateSystem"/> to <see cref="Ordinates"/>
        /// </summary>
        /// <param name="cs">The coordinate system</param>
        /// <returns>The corresponding <see cref="Ordinates"/></returns>
        private static Ordinates ToOrdinates(CoordinateSystem cs)
        {
            var res = Ordinates.XY;
            if (cs == CoordinateSystem.XYM)
                res |= Ordinates.M;
            if (cs == CoordinateSystem.XYZ)
                res |= Ordinates.Z;
            if (cs == CoordinateSystem.XYZM)
                res |= (Ordinates.M | Ordinates.Z);
            return res;
        }

        /// <summary>
        /// Reads a <see cref="Point"/> geometry.
        /// </summary>
        /// <param name="reader">The reader</param>
        /// <param name="cs">The coordinate system</param>
        /// <param name="srid">The spatial reference id for the geometry.</param>
        /// <returns>A <see cref="Point"/> geometry</returns>
        protected Geometry ReadPoint(BinaryReader reader, CoordinateSystem cs, int srid)
        {
            var factory = _geometryServices.CreateGeometryFactory(_precisionModel, srid, _sequenceFactory);
            var seq = ReadCoordinateSequence(reader, 1, cs);
            if (double.IsNaN(seq.GetX(0)) || double.IsNaN(seq.GetY(0)))
                return factory.CreatePoint();
            return factory.CreatePoint(seq);
        }

        /// <summary>
        /// Reads a <see cref="LineString"/> geometry.
        /// </summary>
        /// <param name="reader">The reader</param>
        /// <param name="cs">The coordinate system</param>
        /// <param name="srid">The spatial reference id for the geometry.</param>
        /// <returns>A <see cref="LineString"/> geometry</returns>
        protected Geometry ReadLineString(BinaryReader reader, CoordinateSystem cs, int srid)
        {
            var factory = _geometryServices.CreateGeometryFactory(_precisionModel, srid, _sequenceFactory);
            int numPoints = ReadNumField(reader, FieldNumCoords, ReasonableNumCoordinates(reader.BaseStream, cs));
            var sequence = ReadCoordinateSequenceLineString(reader, numPoints, cs);
            return factory.CreateLineString(sequence);
        }


        /// <summary>
        /// Reads a <see cref="LinearRing"/> geometry.
        /// </summary>
        /// <param name="reader">The reader</param>
        /// <param name="cs">The coordinate system</param>
        /// <param name="srid">The spatial reference id for the geometry.</param>
        /// <returns>A <see cref="LinearRing"/> geometry</returns>
        protected LinearRing ReadLinearRing(BinaryReader reader, CoordinateSystem cs, int srid)
        {
            var factory = _geometryServices.CreateGeometryFactory(_precisionModel, srid, _sequenceFactory);
            int numPoints = ReadNumField(reader, FieldNumCoords, ReasonableNumCoordinates(reader.BaseStream, cs));
            var sequence = ReadCoordinateSequenceRing(reader, numPoints, cs);
            return factory.CreateLinearRing(sequence);
        }
        /// <summary>
        /// Reads a <see cref="Polygon"/> geometry.
        /// </summary>
        /// <param name="reader">The reader</param>
        /// <param name="cs">The coordinate system</param>
        /// <param name="srid">The spatial reference id for the geometry.</param>
        /// <returns>A <see cref="Polygon"/> geometry</returns>
        protected Geometry ReadPolygon(BinaryReader reader, CoordinateSystem cs, int srid)
        {
            var factory = _geometryServices.CreateGeometryFactory(_precisionModel, srid, _sequenceFactory);
            int reasonable = ReasonableNumElements(reader.BaseStream);
            if (_isStrict) reasonable /= 2;
            int numRings = ReadNumField(reader, FieldNumRings, reasonable);
            if (numRings == 0)
                return factory.CreatePolygon();
            
            var exteriorRing = ReadLinearRing(reader, cs, srid);
            var interiorRings = new LinearRing[numRings - 1];
            for (int i = 0; i < numRings - 1; i++)
                interiorRings[i] = ReadLinearRing(reader, cs, srid);
            
            return factory.CreatePolygon(exteriorRing, interiorRings);
        }

        /// <summary>
        /// Reads a <see cref="MultiPoint"/> geometry.
        /// </summary>
        /// <param name="reader">The reader</param>
        /// <param name="cs">The coordinate system</param>
        /// <param name="srid">The spatial reference id for the geometry.</param>
        /// <returns>A <see cref="MultiPoint"/> geometry</returns>
        protected Geometry ReadMultiPoint(BinaryReader reader, CoordinateSystem cs, int srid)
        {
            var factory = _geometryServices.CreateGeometryFactory(_precisionModel, srid, _sequenceFactory);
            int numGeometries = ReadNumField(reader, FieldNumElements, ReasonableNumCoordinates(reader.BaseStream, cs));
            var points = new Point[numGeometries];
            for (int i = 0; i < numGeometries; i++)
            {
                ReadByteOrder(reader);
                CoordinateSystem cs2;
                int srid2 = srid;
                var geometryType = ReadGeometryType(reader, out cs2, ref srid2);//(WKBGeometryTypes)reader.ReadInt32();
                if (geometryType != WKBGeometryTypes.WKBPoint)
                    throw new ArgumentException("Point feature expected");
                points[i] = ReadPoint(reader, cs2, srid2) as Point;
            }
            return factory.CreateMultiPoint(points);
        }

        /// <summary>
        /// Reads a <see cref="MultiLineString"/> geometry.
        /// </summary>
        /// <param name="reader">The reader</param>
        /// <param name="cs">The coordinate system</param>
        /// <param name="srid">The spatial reference id for the geometry.</param>
        /// <returns>A <see cref="MultiLineString"/> geometry</returns>
        protected Geometry ReadMultiLineString(BinaryReader reader, CoordinateSystem cs, int srid)
        {
            var factory = _geometryServices.CreateGeometryFactory(_precisionModel, srid, _sequenceFactory);
            int numGeometries = ReadNumField(reader, FieldNumElements, ReasonableNumElements(reader.BaseStream));
            var strings = new LineString[numGeometries];
            for (int i = 0; i < numGeometries; i++)
            {
                ReadByteOrder(reader);
                CoordinateSystem cs2;
                int srid2 = srid;
                var geometryType = ReadGeometryType(reader, out cs2, ref srid2);//(WKBGeometryTypes)reader.ReadInt32();
                if (srid2 < 0) srid2 = srid;
                if (geometryType != WKBGeometryTypes.WKBLineString)
                    throw new ArgumentException("LineString feature expected");
                strings[i] = ReadLineString(reader, cs2, srid2) as LineString;
            }
            return factory.CreateMultiLineString(strings);
        }

        /// <summary>
        /// Reads a <see cref="MultiPolygon"/> geometry.
        /// </summary>
        /// <param name="reader">The reader</param>
        /// <param name="cs">The coordinate system</param>
        /// <param name="srid">The spatial reference id for the geometry.</param>
        /// <returns>A <see cref="MultiPolygon"/> geometry</returns>
        protected Geometry ReadMultiPolygon(BinaryReader reader, CoordinateSystem cs, int srid)
        {
            var factory = _geometryServices.CreateGeometryFactory(_precisionModel, srid, _sequenceFactory);
            int numGeometries = ReadNumField(reader, FieldNumElements, ReasonableNumElements(reader.BaseStream));
            var polygons = new Polygon[numGeometries];
            for (int i = 0; i < numGeometries; i++)
            {
                ReadByteOrder(reader);
                CoordinateSystem cs2;
                int srid2 = srid;
                var geometryType = ReadGeometryType(reader, out cs2, ref srid2);//(WKBGeometryTypes)reader.ReadInt32();
                if (geometryType != WKBGeometryTypes.WKBPolygon)
                    throw new ArgumentException("Polygon feature expected");
                polygons[i] = ReadPolygon(reader, cs2, srid2) as Polygon;
            }
            return factory.CreateMultiPolygon(polygons);
        }

        /// <summary>
        /// Reads a <see cref="GeometryCollection"/> geometry.
        /// </summary>
        /// <param name="reader">The reader</param>
        /// <param name="cs">The coordinate system</param>
        /// <param name="srid">The spatial reference id for the geometry.</param>
        /// <returns>A <see cref="GeometryCollection"/> geometry</returns>
        protected Geometry ReadGeometryCollection(BinaryReader reader, CoordinateSystem cs, int srid)
        {
            var factory = _geometryServices.CreateGeometryFactory(_precisionModel, srid, _sequenceFactory);

            int numGeometries = ReadNumField(reader, FieldNumElements, ReasonableNumElements(reader.BaseStream));
            var geometries = new Geometry[numGeometries];

            for (int i = 0; i < numGeometries; i++)
            {
                ReadByteOrder(reader);
                CoordinateSystem cs2;
                int srid2 = srid;
                var geometryType = ReadGeometryType(reader, out cs2, ref srid2);//(WKBGeometryTypes)reader.ReadInt32();

                switch (geometryType)
                {
                    //Point
                    case WKBGeometryTypes.WKBPoint:
                    case WKBGeometryTypes.WKBPointZ:
                    case WKBGeometryTypes.WKBPointM:
                    case WKBGeometryTypes.WKBPointZM:
                        geometries[i] = ReadPoint(reader, cs2, srid2);
                        break;

                    //Line String
                    case WKBGeometryTypes.WKBLineString:
                    case WKBGeometryTypes.WKBLineStringZ:
                    case WKBGeometryTypes.WKBLineStringM:
                    case WKBGeometryTypes.WKBLineStringZM:
                        geometries[i] = ReadLineString(reader, cs2, srid2);
                        break;

                    //Polygon
                    case WKBGeometryTypes.WKBPolygon:
                    case WKBGeometryTypes.WKBPolygonZ:
                    case WKBGeometryTypes.WKBPolygonM:
                    case WKBGeometryTypes.WKBPolygonZM:
                        geometries[i] = ReadPolygon(reader, cs2, srid2);
                        break;

                    //Multi Point
                    case WKBGeometryTypes.WKBMultiPoint:
                    case WKBGeometryTypes.WKBMultiPointZ:
                    case WKBGeometryTypes.WKBMultiPointM:
                    case WKBGeometryTypes.WKBMultiPointZM:
                        geometries[i] = ReadMultiPoint(reader, cs2, srid2);
                        break;

                    //Multi Line String
                    case WKBGeometryTypes.WKBMultiLineString:
                    case WKBGeometryTypes.WKBMultiLineStringZ:
                    case WKBGeometryTypes.WKBMultiLineStringM:
                    case WKBGeometryTypes.WKBMultiLineStringZM:
                        geometries[i] = ReadMultiLineString(reader, cs2, srid2);
                        break;

                    //Multi Polygon
                    case WKBGeometryTypes.WKBMultiPolygon:
                        geometries[i] = ReadMultiPolygon(reader, cs2, srid2);
                        break;

                    //Geometry Collection
                    case WKBGeometryTypes.WKBGeometryCollection:
                    case WKBGeometryTypes.WKBGeometryCollectionZ:
                    case WKBGeometryTypes.WKBGeometryCollectionM:
                    case WKBGeometryTypes.WKBGeometryCollectionZM:
                        geometries[i] = ReadGeometryCollection(reader, cs2, srid2);
                        break;

                    case WKBGeometryTypes.WKBCircularString:
                        geometries[i] = ReadCircularString(reader, cs2, srid2);
                        break;
                    case WKBGeometryTypes.WKBCompoundCurve:
                        geometries[i] = ReadCompoundCurve(reader, cs2, srid2);
                        break;
                    case WKBGeometryTypes.WKBCurvePolygon:
                        geometries[i] = ReadCurvePolygon(reader, cs2, srid2);
                        break;
                    case WKBGeometryTypes.WKBMultiCurve:
                        geometries[i] = ReadMultiCurve(reader, cs2, srid2);
                        break;
                    case WKBGeometryTypes.WKBMultiSurface:
                        geometries[i] = ReadMultiSurface(reader, cs2, srid2);
                        break;
                    case WKBGeometryTypes.WKBTin:
                        geometries[i] = ReadTin(reader, cs2, srid2);
                        break;
                    case WKBGeometryTypes.WKBCircle:
                        geometries[i] = ReadCircle(reader, cs2, srid2);
                        break;

                    default:
                        throw new ArgumentException("Should never reach here!");
                }
            }
            return factory.CreateGeometryCollection(geometries);
        }

        /// <summary>
        /// Gets or sets a value indicating if a possibly encoded SRID value should be handled.
        /// </summary>
        public bool HandleSRID { get; set; }

        /// <summary>
        /// Gets a value indicating which ordinates can be handled.
        /// </summary>
        public Ordinates AllowedOrdinates => Ordinates.XYZM & _sequenceFactory.Ordinates;

        private Ordinates _handleOrdinates;

        /// <summary>
        /// Gets a value indicating which ordinates should be handled.
        /// </summary>
        public Ordinates HandleOrdinates
        {
            get => _handleOrdinates;
            set
            {
                value = Ordinates.XY | (AllowedOrdinates & value);
                _handleOrdinates = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating if the reader should attempt to repair malformed input.
        /// </summary>
        /// <remarks>
        /// <i>Malformed</i> in this case means the ring has too few points (4),
        /// or is not closed.
        /// </remarks>
        public bool IsStrict
        {
            get => _isStrict;
            set => _isStrict = value;
        }

        /// <summary>
        /// Gets or sets whether invalid linear rings should be fixed
        /// </summary>
        [Obsolete("Use !IsStrict")]
        public bool RepairRings
        {
            get => !IsStrict;
            set => IsStrict = !value;
        }

        /// <summary>
        /// Reads a SQL/MM CircularString (GEOS/ISO WKB type 8).
        /// </summary>
        /// <param name="reader">The reader</param>
        /// <param name="cs">The coordinate system</param>
        /// <param name="srid">The spatial reference id for the geometry.</param>
        /// <returns>A <see cref="CircularString"/> geometry</returns>
        protected Geometry ReadCircularString(BinaryReader reader, CoordinateSystem cs, int srid)
        {
            var factory = _geometryServices.CreateGeometryFactory(_precisionModel, srid, _sequenceFactory);
            int numPoints = ReadNumField(reader, FieldNumCoords, ReasonableNumCoordinates(reader.BaseStream, cs));
            var sequence = ReadCoordinateSequence(reader, numPoints, cs);
            return new CircularString(sequence, factory);
        }

        /// <summary>
        /// Reads an ISO/IEC 13249-3 Circle (WKB type 18).
        /// The payload is three circumference points, or EMPTY (type 18 with
        /// zero points — same empty-body house style as CircularString).
        /// Collinear three-point intake is refused by
        /// <see cref="Circle"/> (ISO/IEC 13249-3 §4.2.7). Z/M/ZM use the
        /// same ISO +1000/+2000/+3000 table as types 8–16 (recovered as
        /// type 18 by the <c>(type &amp; 0xffff) % 1000</c> reducer only;
        /// there are no <c>WKBCircleZ|M|ZM</c> enum arms).
        /// </summary>
        /// <param name="reader">The reader</param>
        /// <param name="cs">The coordinate system</param>
        /// <param name="srid">The spatial reference id for the geometry.</param>
        /// <returns>A <see cref="Circle"/> geometry</returns>
        protected Geometry ReadCircle(BinaryReader reader, CoordinateSystem cs, int srid)
        {
            var factory = _geometryServices.CreateGeometryFactory(_precisionModel, srid, _sequenceFactory);
            int numPoints = ReadNumField(reader, FieldNumCoords, ReasonableNumCoordinates(reader.BaseStream, cs));
            var sequence = ReadCoordinateSequence(reader, numPoints, cs);
            return new Circle(sequence, factory);
        }

        /// <summary>
        /// Reads a SQL/MM CompoundCurve (GEOS/ISO WKB type 9).
        /// Nested <see cref="CompoundCurve"/> members are accepted and flattened
        /// (ADR-0005). Year-1 <see cref="CurvePolygon"/> rings and
        /// <see cref="MultiCurve"/> members reject nested type 9 instead of
        /// flattening (Ticket 1 / Ticket 4 lock).
        /// </summary>
        /// <param name="reader">The reader</param>
        /// <param name="cs">The coordinate system</param>
        /// <param name="srid">The spatial reference id for the geometry.</param>
        /// <returns>A <see cref="CompoundCurve"/> geometry</returns>
        protected Geometry ReadCompoundCurve(BinaryReader reader, CoordinateSystem cs, int srid)
            => ReadCompoundCurve(reader, cs, srid, year1Members: false);

        /// <summary>
        /// Reads a SQL/MM CompoundCurve (GEOS/ISO WKB type 9).
        /// </summary>
        /// <param name="reader">The reader</param>
        /// <param name="cs">The coordinate system</param>
        /// <param name="srid">The spatial reference id for the geometry.</param>
        /// <param name="year1Members">
        /// When <c>true</c>, this CompoundCurve is a Year-1 CurvePolygon ring
        /// or MultiCurve member: members must be LineString (2) or
        /// CircularString (8) only. Nested CompoundCurve (9) is rejected, not
        /// flattened — same lock as Ticket 1 / Ticket 4 WKT.
        /// </param>
        /// <returns>A <see cref="CompoundCurve"/> geometry</returns>
        private CompoundCurve ReadCompoundCurve(BinaryReader reader, CoordinateSystem cs, int srid, bool year1Members)
        {
            var factory = _geometryServices.CreateGeometryFactory(_precisionModel, srid, _sequenceFactory);
            int numCurves = ReadNumField(reader, FieldNumElements, ReasonableNumElements(reader.BaseStream));
            var curves = new Curve[numCurves];
            for (int i = 0; i < numCurves; i++)
            {
                curves[i] = year1Members
                    ? ReadYear1CompoundCurveMember(reader, srid)
                    : ReadCurveMember(reader, srid);
            }
            return new CompoundCurve(curves, factory);
        }

        /// <summary>
        /// Reads a SQL/MM CurvePolygon (GEOS/ISO WKB type 10).
        /// Year-1 rings are nested WKB LineString (2) | CircularString (8) |
        /// CompoundCurve (9) | Circle (18) (ISO/IEC 13249-3 §8.2 / Ticket 1
        /// grammar; Circle is Ticket 19 object-model / Ticket 20 WKB).
        /// Z/M/ZM use the same ISO +1000/+2000/+3000 table as types 8–9
        /// (recovered as type 10).
        /// </summary>
        /// <param name="reader">The reader</param>
        /// <param name="cs">The coordinate system</param>
        /// <param name="srid">The spatial reference id for the geometry.</param>
        /// <returns>A <see cref="CurvePolygon"/> geometry</returns>
        protected Geometry ReadCurvePolygon(BinaryReader reader, CoordinateSystem cs, int srid)
        {
            var factory = _geometryServices.CreateGeometryFactory(_precisionModel, srid, _sequenceFactory);
            int numRings = ReadNumField(reader, FieldNumRings, ReasonableNumElements(reader.BaseStream));
            if (numRings == 0)
                return new CurvePolygon(null, factory);

            var shell = ReadCurvePolygonRing(reader, srid);
            var holes = new Curve[numRings - 1];
            for (int i = 0; i < numRings - 1; i++)
                holes[i] = ReadCurvePolygonRing(reader, srid);
            return new CurvePolygon(shell, holes, factory);
        }

        /// <summary>
        /// Reads a SQL/MM MultiCurve (GEOS/ISO WKB type 11).
        /// Year-1 members are nested WKB LineString (2) | CircularString (8) |
        /// CompoundCurve (9) | Circle (18) (ISO/IEC 13249-3 §5.1.67 g4
        /// <c>curveMember</c>, Ticket 4 WKT lock; Circle is Ticket 19
        /// object-model / Ticket 20 WKB). Nested CompoundCurve inside a
        /// CompoundCurve member is rejected, not flattened (ADR-0005 applies
        /// only to standalone CompoundCurve). Z/M/ZM use the same ISO
        /// +1000/+2000/+3000 table as types 8–10 (recovered as type 11).
        /// </summary>
        /// <param name="reader">The reader</param>
        /// <param name="cs">The coordinate system</param>
        /// <param name="srid">The spatial reference id for the geometry.</param>
        /// <returns>A <see cref="MultiCurve"/> geometry</returns>
        protected Geometry ReadMultiCurve(BinaryReader reader, CoordinateSystem cs, int srid)
        {
            var factory = _geometryServices.CreateGeometryFactory(_precisionModel, srid, _sequenceFactory);
            int numGeometries = ReadNumField(reader, FieldNumElements, ReasonableNumElements(reader.BaseStream));
            var curves = new Curve[numGeometries];
            for (int i = 0; i < numGeometries; i++)
                curves[i] = ReadMultiCurveMember(reader, srid);
            return new MultiCurve(curves, factory);
        }

        /// <summary>
        /// Reads a SQL/MM MultiSurface (GEOS/ISO WKB type 12).
        /// Year-1 members are nested WKB Polygon (3) | CurvePolygon (10)
        /// only (ISO/IEC 13249-3 <c>surfaceMember</c> = polygonText |
        /// curvePolygonGeometry, Ticket 7 WKT lock). CurvePolygon members
        /// reuse <see cref="ReadCurvePolygon"/>, so their rings obey the
        /// Year-1 LineString (2) | CircularString (8) | CompoundCurve (9) |
        /// Circle (18) lock (Ticket 2 / Ticket 20). Circle is not itself a
        /// surface member. Omitted surface types (TRIANGLE, TIN,
        /// POLYHEDRALSURFACE, COMPOUNDSURFACE) and Year-2 curve codes are
        /// refused by type code — WKB has no keyword list. Z/M/ZM use the
        /// same ISO +1000/+2000/+3000 table as types 8–11 (recovered as
        /// type 12).
        /// </summary>
        /// <param name="reader">The reader</param>
        /// <param name="cs">The coordinate system</param>
        /// <param name="srid">The spatial reference id for the geometry.</param>
        /// <returns>A <see cref="MultiSurface"/> geometry</returns>
        protected Geometry ReadMultiSurface(BinaryReader reader, CoordinateSystem cs, int srid)
        {
            var factory = _geometryServices.CreateGeometryFactory(_precisionModel, srid, _sequenceFactory);
            int numGeometries = ReadNumField(reader, FieldNumElements, ReasonableNumElements(reader.BaseStream));
            var surfaces = new Geometry[numGeometries];
            for (int i = 0; i < numGeometries; i++)
                surfaces[i] = ReadMultiSurfaceMember(reader, srid);
            return new MultiSurface(surfaces, factory);
        }

        /// <summary>
        /// Reads a SQL/MM TIN (ISO/IEC 13249-3 / OGC SFA-CA type 16).
        /// Year-1 WKB 16 is complete (Tickets 16–18; no longer partial);
        /// named WKT fields (<c>PATCHES</c> / <c>ELEMENTS</c> /
        /// <c>MAXSIDELENGTH</c>) remain omitted. Year-1 members are nested
        /// WKB Polygon (3) only (ISO/IEC 13249-3 g4 <c>polygonText</c>,
        /// Ticket 16 WKT lock). Each Polygon is wrapped as the existing
        /// <see cref="Geometries.Curves.Triangle"/> (no second Triangle
        /// type). Nested Triangle (17), PolyhedralSurface (15), Circle (18)
        /// and any other non-3 code are refused by numeric type — WKB has
        /// no keyword list. EMPTY is a type-16 header with zero patches.
        /// Z/M/ZM use the same ISO +1000/+2000/+3000 table as types 8–12
        /// (recovered as type 16 by the <c>(type &amp; 0xffff) % 1000</c>
        /// reducer only).
        /// </summary>
        /// <param name="reader">The reader</param>
        /// <param name="cs">The coordinate system</param>
        /// <param name="srid">The spatial reference id for the geometry.</param>
        /// <returns>A <see cref="Tin"/> geometry</returns>
        protected Geometry ReadTin(BinaryReader reader, CoordinateSystem cs, int srid)
        {
            var factory = _geometryServices.CreateGeometryFactory(_precisionModel, srid, _sequenceFactory);
            int numGeometries = ReadNumField(reader, FieldNumElements, ReasonableNumElements(reader.BaseStream));
            var triangles = new Geometries.Curves.Triangle[numGeometries];
            for (int i = 0; i < numGeometries; i++)
                triangles[i] = ReadTinMember(reader, srid);
            return new Tin(triangles, factory);
        }

        /// <summary>
        /// Reads a nested curve member (byte-order + type + body).
        /// Used for standalone CompoundCurve; nested CompoundCurve is
        /// accepted and flattened (ADR-0005). Year-1 MultiCurve members
        /// use <see cref="ReadMultiCurveMember"/> instead.
        /// </summary>
        /// <param name="reader">The reader</param>
        /// <param name="srid">The spatial reference id for the geometry.</param>
        /// <returns>A <see cref="Curve"/> geometry</returns>
        private Curve ReadCurveMember(BinaryReader reader, int srid)
        {
            ReadByteOrder(reader);
            int srid2 = srid;
            var geometryType = ReadGeometryType(reader, out var cs2, ref srid2);
            if (srid2 < 0) srid2 = srid;
            switch (geometryType)
            {
                case WKBGeometryTypes.WKBLineString:
                case WKBGeometryTypes.WKBLineStringZ:
                case WKBGeometryTypes.WKBLineStringM:
                case WKBGeometryTypes.WKBLineStringZM:
                    return (Curve)ReadLineString(reader, cs2, srid2);
                case WKBGeometryTypes.WKBCircularString:
                    return (Curve)ReadCircularString(reader, cs2, srid2);
                case WKBGeometryTypes.WKBCompoundCurve:
                    return (Curve)ReadCompoundCurve(reader, cs2, srid2);
                default:
                    throw new ArgumentException("LineString, CircularString or CompoundCurve expected as curve member");
            }
        }

        /// <summary>
        /// Reads a Year-1 <see cref="CurvePolygon"/> ring: nested WKB
        /// LineString (2) | CircularString (8) | CompoundCurve (9) |
        /// Circle (18). Any other nested type code is rejected. ISO
        /// Z/M/ZM variants of types 8 / 9 / 18 arrive as 8 / 9 / 18 after
        /// <see cref="ReadGeometryType"/> (<c>% 1000</c>), matching the
        /// table used for those types.
        /// </summary>
        /// <param name="reader">The reader</param>
        /// <param name="srid">The spatial reference id for the geometry.</param>
        /// <returns>A Year-1 ring curve</returns>
        /// <exception cref="ArgumentException">
        /// When the nested type is not 2, 8, 9 or 18.
        /// </exception>
        private Curve ReadCurvePolygonRing(BinaryReader reader, int srid)
        {
            ReadByteOrder(reader);
            int srid2 = srid;
            var geometryType = ReadGeometryType(reader, out var cs2, ref srid2);
            if (srid2 < 0) srid2 = srid;
            switch (geometryType)
            {
                case WKBGeometryTypes.WKBLineString:
                case WKBGeometryTypes.WKBLineStringZ:
                case WKBGeometryTypes.WKBLineStringM:
                case WKBGeometryTypes.WKBLineStringZM:
                    return (Curve)ReadLineString(reader, cs2, srid2);
                case WKBGeometryTypes.WKBCircularString:
                    return (Curve)ReadCircularString(reader, cs2, srid2);
                case WKBGeometryTypes.WKBCompoundCurve:
                    return ReadCompoundCurve(reader, cs2, srid2, year1Members: true);
                case WKBGeometryTypes.WKBCircle:
                    return (Curve)ReadCircle(reader, cs2, srid2);
                default:
                    throw new ArgumentException(
                        "Unexpected CurvePolygon ring WKB type " + (int)geometryType +
                        ": Year-1 ST_CurvePolygon ring production " +
                        "(ISO/IEC 13249-3 §8.2) is LineString (2) | CircularString (8) | CompoundCurve (9) | Circle (18) only.");
            }
        }

        /// <summary>
        /// Reads a Year-1 <see cref="MultiSurface"/> member: nested WKB
        /// Polygon (3) | CurvePolygon (10) only (ISO/IEC 13249-3
        /// <c>surfaceMember</c> = polygonText | curvePolygonGeometry,
        /// Ticket 7). Any other nested type code is rejected, including
        /// MultiPolygon (6), MultiCurve (11), MultiSurface (12),
        /// PolyhedralSurface (15), TIN (16), Triangle (17) and Circle (18).
        /// CurvePolygon members reuse <see cref="ReadCurvePolygon"/> so
        /// their rings obey the Year-1 LS|CS|CC lock. Nested CompoundCurve
        /// inside a CurvePolygon ring is rejected (Ticket 2).
        /// </summary>
        /// <param name="reader">The reader</param>
        /// <param name="srid">The spatial reference id for the geometry.</param>
        /// <returns>A Year-1 MultiSurface member</returns>
        /// <exception cref="ArgumentException">
        /// When the nested type is not 3 or 10.
        /// </exception>
        private Geometry ReadMultiSurfaceMember(BinaryReader reader, int srid)
        {
            ReadByteOrder(reader);
            int srid2 = srid;
            var geometryType = ReadGeometryType(reader, out var cs2, ref srid2);
            if (srid2 < 0) srid2 = srid;
            switch (geometryType)
            {
                case WKBGeometryTypes.WKBPolygon:
                case WKBGeometryTypes.WKBPolygonZ:
                case WKBGeometryTypes.WKBPolygonM:
                case WKBGeometryTypes.WKBPolygonZM:
                    return ReadPolygon(reader, cs2, srid2);
                case WKBGeometryTypes.WKBCurvePolygon:
                    return ReadCurvePolygon(reader, cs2, srid2);
                default:
                    throw new ArgumentException(
                        "Unexpected MultiSurface member WKB type " + (int)geometryType +
                        ": Year-1 ST_MultiSurface member production " +
                        "(ISO/IEC 13249-3 surfaceMember = polygonText | curvePolygonGeometry) is Polygon (3) | CurvePolygon (10) only.");
            }
        }

        /// <summary>
        /// Reads a Year-1 <see cref="MultiCurve"/> member: nested WKB
        /// LineString (2) | CircularString (8) | CompoundCurve (9) |
        /// Circle (18) (ISO/IEC 13249-3 §5.1.67 g4 <c>curveMember</c>,
        /// Ticket 4 / Ticket 20). Any other nested type code is rejected,
        /// including CurvePolygon (10) and MultiSurface (12). Nested
        /// CompoundCurve inside a CompoundCurve member is rejected (no
        /// ADR-0005 flatten).
        /// </summary>
        /// <param name="reader">The reader</param>
        /// <param name="srid">The spatial reference id for the geometry.</param>
        /// <returns>A Year-1 MultiCurve member</returns>
        /// <exception cref="ArgumentException">
        /// When the nested type is not 2, 8, 9 or 18.
        /// </exception>
        private Curve ReadMultiCurveMember(BinaryReader reader, int srid)
        {
            ReadByteOrder(reader);
            int srid2 = srid;
            var geometryType = ReadGeometryType(reader, out var cs2, ref srid2);
            if (srid2 < 0) srid2 = srid;
            switch (geometryType)
            {
                case WKBGeometryTypes.WKBLineString:
                case WKBGeometryTypes.WKBLineStringZ:
                case WKBGeometryTypes.WKBLineStringM:
                case WKBGeometryTypes.WKBLineStringZM:
                    return (Curve)ReadLineString(reader, cs2, srid2);
                case WKBGeometryTypes.WKBCircularString:
                    return (Curve)ReadCircularString(reader, cs2, srid2);
                case WKBGeometryTypes.WKBCompoundCurve:
                    return ReadCompoundCurve(reader, cs2, srid2, year1Members: true);
                case WKBGeometryTypes.WKBCircle:
                    return (Curve)ReadCircle(reader, cs2, srid2);
                default:
                    throw new ArgumentException(
                        "Unexpected MultiCurve member WKB type " + (int)geometryType +
                        ": Year-1 ST_MultiCurve member production " +
                        "(ISO/IEC 13249-3 §5.1.67 g4 curveMember) is LineString (2) | CircularString (8) | CompoundCurve (9) | Circle (18) only.");
            }
        }

        /// <summary>
        /// Reads a Year-1 <see cref="Tin"/> member: nested WKB Polygon (3)
        /// only (ISO/IEC 13249-3 g4 <c>polygonText</c>, Ticket 16). The
        /// reduced type must be 3 — ISO Z/M/ZM variants of Polygon arrive
        /// as 3 after <see cref="ReadGeometryType"/> (<c>% 1000</c>). Any
        /// other nested type code is rejected by number, including
        /// PolyhedralSurface (15), Triangle (17) and Circle (18). Triangle
        /// (17) is refused even though the object model stores
        /// <see cref="Geometries.Curves.Triangle"/> — Year-1 wire format
        /// is Polygon 3 only. A Polygon with interior rings is not a
        /// triangle patch (same lock as Ticket 16 WKT).
        /// </summary>
        /// <param name="reader">The reader</param>
        /// <param name="srid">The spatial reference id for the geometry.</param>
        /// <returns>A Year-1 TIN patch as <see cref="Geometries.Curves.Triangle"/></returns>
        /// <exception cref="ArgumentException">
        /// When the nested type is not 3, or the Polygon has interior rings.
        /// </exception>
        private Geometries.Curves.Triangle ReadTinMember(BinaryReader reader, int srid)
        {
            ReadByteOrder(reader);
            int srid2 = srid;
            var geometryType = ReadGeometryType(reader, out var cs2, ref srid2);
            if (srid2 < 0) srid2 = srid;
            if (geometryType != WKBGeometryTypes.WKBPolygon)
            {
                throw new ArgumentException(
                    "Unexpected TIN member WKB type " + (int)geometryType +
                    ": Year-1 ST_TIN member production " +
                    "(ISO/IEC 13249-3 g4 polygonText) is Polygon (3) only.");
            }

            var polygon = (Polygon)ReadPolygon(reader, cs2, srid2);
            if (polygon.NumInteriorRings != 0)
                throw new ArgumentException("A TRIANGLE within a TIN must not contain interior rings");

            var factory = _geometryServices.CreateGeometryFactory(_precisionModel, srid2, _sequenceFactory);
            return new Geometries.Curves.Triangle((LinearRing)polygon.ExteriorRing, factory);
        }

        /// <summary>
        /// Reads one member of a Year-1 CompoundCurve (CurvePolygon ring or
        /// MultiCurve member). LineString (2) and CircularString (8) only;
        /// nested CompoundCurve (9) is rejected (Ticket 1 / Ticket 4 WKT lock,
        /// not flattened).
        /// </summary>
        /// <param name="reader">The reader</param>
        /// <param name="srid">The spatial reference id for the geometry.</param>
        /// <returns>A LineString or CircularString member</returns>
        /// <exception cref="ArgumentException">
        /// When the nested type is CompoundCurve or any non-LS/CS code.
        /// </exception>
        private Curve ReadYear1CompoundCurveMember(BinaryReader reader, int srid)
        {
            ReadByteOrder(reader);
            int srid2 = srid;
            var geometryType = ReadGeometryType(reader, out var cs2, ref srid2);
            if (srid2 < 0) srid2 = srid;
            switch (geometryType)
            {
                case WKBGeometryTypes.WKBLineString:
                case WKBGeometryTypes.WKBLineStringZ:
                case WKBGeometryTypes.WKBLineStringM:
                case WKBGeometryTypes.WKBLineStringZM:
                    return (Curve)ReadLineString(reader, cs2, srid2);
                case WKBGeometryTypes.WKBCircularString:
                    return (Curve)ReadCircularString(reader, cs2, srid2);
                case WKBGeometryTypes.WKBCompoundCurve:
                    throw new ArgumentException(
                        "Nested COMPOUNDCURVE is not a Year-1 CompoundCurve member " +
                        "(ISO/IEC 13249-3: Year-1 CompoundCurve members are contiguous LineString | CircularString only).");
                default:
                    throw new ArgumentException(
                        "A Year-1 CompoundCurve admits only LineString (2) and CircularString (8) members, got WKB type "
                        + (int)geometryType + ".");
            }
        }

        /// <summary>
        /// Function to determine whether an ordinate should be handled or not.
        /// </summary>
        /// <param name="ordinate">The ordinate to check</param>
        /// <returns><c>true</c> if the ordinate should be handled; otherwise <c>false</c></returns>
        private bool HandleOrdinate(Ordinate ordinate)
        {
            switch (ordinate)
            {
                case Ordinate.X:
                case Ordinate.Y:
                    return true;
                case Ordinate.M:
                    return (HandleOrdinates & Ordinates.M) != Ordinates.None;
                case Ordinate.Z:
                    return (HandleOrdinates & Ordinates.Z) != Ordinates.None;
                default:
                    return false;
            }
        }
    }
}
