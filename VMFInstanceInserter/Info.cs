using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace ResourceLib
{
    public class InfoParseException : Exception
    {
        public InfoParseException( String message )
            : base( message )
        {

        }
    }

    #region Value Types
    public class InfoValue
    {
        public static InfoValue ReadFromStream( BinaryReader reader )
        {
            Info.InfoType type = (Info.InfoType) reader.ReadByte();

            switch ( type )
            {
                case Info.InfoType.Object:
                    return new InfoObject( reader );
                case Info.InfoType.Array:
                    return new InfoArray( reader );
                case Info.InfoType.String:
                    return new InfoString( reader );
                case Info.InfoType.Double:
                    return new InfoDouble( reader );
                case Info.InfoType.Integer:
                    return new InfoInteger( reader );
                case Info.InfoType.Boolean:
                    return new InfoBoolean( reader );
                default:
                    return new InfoNull();
            }
        }

        internal InfoValue()
        {

        }

        internal InfoValue( BinaryReader reader )
        {

        }

        public virtual String AsString()
        {
            throw new NotImplementedException();
        }

        public virtual Int64 AsInteger()
        {
            throw new NotImplementedException();
        }

        public virtual Double AsDouble()
        {
            throw new NotImplementedException();
        }

        public virtual Boolean AsBoolean()
        {
            throw new NotImplementedException();
        }

        public virtual InfoValue[] AsArray()
        {
            throw new NotImplementedException();
        }

        public virtual Object AsObject()
        {
            return AsString();
        }

        public virtual void WriteToStream( BinaryWriter writer )
        {
            switch ( GetType().FullName.Split( '.' ).Last() )
            {
                case "InfoObject":
                    writer.Write( (byte) Info.InfoType.Object ); break;
                case "InfoArray":
                    writer.Write( (byte) Info.InfoType.Array ); break;
                case "InfoString":
                    writer.Write( (byte) Info.InfoType.String ); break;
                case "InfoDouble":
                    writer.Write( (byte) Info.InfoType.Double ); break;
                case "InfoInteger":
                    writer.Write( (byte) Info.InfoType.Integer ); break;
                case "InfoBoolean":
                    writer.Write( (byte) Info.InfoType.Boolean ); break;
                default:
                    writer.Write( (byte) Info.InfoType.Null ); break;
            }
        }

        public override string ToString()
        {
            return AsString();
        }
    }

    public class InfoObject : InfoValue, IEnumerable<KeyValuePair<String, InfoValue>>
    {
        private Dictionary<String,InfoValue> myDict;

        public readonly String Type;
        public readonly String Name;

        internal InfoObject( Dictionary<String, InfoValue> vals, String type = "undefined", String name = "unnamed" )
        {
            myDict = vals;

            Type = type;
            Name = name;
        }

        internal InfoObject( BinaryReader reader )
            : base( reader )
        {
            Type = reader.ReadString();
            Name = reader.ReadString();

            int count = reader.ReadInt32();

            myDict = new Dictionary<string, InfoValue>();

            for ( int i = 0; i < count; ++i )
            {
                string key = reader.ReadString();
                InfoValue val = InfoValue.ReadFromStream( reader );
                myDict.Add( key, val );
            }
        }

        public InfoValue this[ String key ]
        {
            get
            {
                if ( myDict.ContainsKey( key ) )
                    return myDict[ key ];

                return null;
            }
        }

        public override void WriteToStream( BinaryWriter writer )
        {
            base.WriteToStream( writer );

            writer.Write( Type );
            writer.Write( Name );

            writer.Write( myDict.Count );

            foreach ( String key in myDict.Keys )
            {
                writer.Write( key );
                myDict[ key ].WriteToStream( writer );
            }
        }

        public override string ToString()
        {
            return "{" + myDict.Count + "}";
        }

        public IEnumerator<KeyValuePair<string, InfoValue>> GetEnumerator()
        {
            return myDict.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return myDict.GetEnumerator();
        }
    }

    public class InfoNull : InfoValue
    {
        public override string AsString()
        {
            return "null";
        }

        public override bool AsBoolean()
        {
            return false;
        }

        public override double AsDouble()
        {
            return default( double );
        }

        public override long AsInteger()
        {
            return default( long );
        }

        public override object AsObject()
        {
            return null;
        }
    }

    public class InfoString : InfoValue
    {
        private String myVal;

        internal InfoString( String value )
        {
            myVal = value;
        }

        internal InfoString( BinaryReader reader )
            : base( reader )
        {
            myVal = reader.ReadString();
        }

        public override string AsString()
        {
            return myVal;
        }

        public override double AsDouble()
        {
            return Double.Parse( myVal );
        }

        public override Int64 AsInteger()
        {
            return Int64.Parse( myVal );
        }

        public override bool  AsBoolean()
        {
 	        return Boolean.Parse( myVal );
        }

        public override object AsObject()
        {
            return AsString();
        }

        public override void WriteToStream( BinaryWriter writer )
        {
            base.WriteToStream( writer );

            writer.Write( myVal );
        }

        public override string ToString()
        {
            return "\"" + AsString() + "\"";
        }
    }

    public class InfoInteger : InfoValue
    {
        private Int64 myVal;

        internal InfoInteger( Int64 value )
        {
            myVal = value;
        }

        internal InfoInteger( BinaryReader reader )
            : base( reader )
        {
            myVal = reader.ReadInt64();
        }

        public override string AsString()
        {
            return myVal.ToString();
        }

        public override double AsDouble()
        {
            return myVal;
        }

        public override long AsInteger()
        {
            return myVal;
        }

        public override bool AsBoolean()
        {
 	        return myVal != 0;
        }

        public override object AsObject()
        {
            return AsInteger();
        }

        public override void WriteToStream( BinaryWriter writer )
        {
            base.WriteToStream( writer );

            writer.Write( myVal );
        }
    }

    public class InfoDouble : InfoValue
    {
        private Double myVal;

        internal InfoDouble( Double value )
        {
            myVal = value;
        }

        internal InfoDouble( BinaryReader reader )
            : base( reader )
        {
            myVal = reader.ReadDouble();
        }

        public override string AsString()
        {
            return myVal.ToString();
        }

        public override double AsDouble()
        {
            return myVal;
        }

        public override long AsInteger()
        {
            return (long) myVal;
        }

        public override bool AsBoolean()
        {
 	        return myVal != 0;
        }

        public override object AsObject()
        {
            return AsDouble();
        }

        public override void WriteToStream( BinaryWriter writer )
        {
            base.WriteToStream( writer );

            writer.Write( myVal );
        }
    }

    public class InfoBoolean : InfoValue
    {
        private Boolean myVal;

        internal InfoBoolean( Boolean value )
        {
            myVal = value;
        }

        internal InfoBoolean( BinaryReader reader )
            : base( reader )
        {
            myVal = reader.ReadBoolean();
        }

        public override string  AsString()
        {
            return myVal.ToString();
        }

        public override bool AsBoolean()
        {
 	        return myVal;
        }

        public override object AsObject()
        {
 	        return AsBoolean();
        }

        public override void WriteToStream( BinaryWriter writer )
        {
            base.WriteToStream( writer );

            writer.Write( myVal );
        }
    }

    public class InfoArray : InfoValue
    {
        private InfoValue[] myVal;

        public int Length
        {
            get
            {
                return myVal.Length;
            }
        }

        internal InfoArray( InfoValue[] value )
        {
            myVal = value;
        }

        internal InfoArray( BinaryReader reader )
            : base( reader )
        {
            int len = reader.ReadInt32();

            myVal = new InfoValue[ len ];

            for ( int i = 0; i < len; ++i )
                myVal[ i ] = InfoValue.ReadFromStream( reader );
        }

        public InfoValue this[ int index ]
        {
            get
            {
                return myVal[ index ];
            }
        }

        public override InfoValue[] AsArray()
        {
            return myVal;
        }

        public override object AsObject()
        {
            return AsArray();
        }

        public override void WriteToStream( BinaryWriter writer )
        {
            base.WriteToStream( writer );

            writer.Write( myVal.Length );

            for ( int i = 0; i < myVal.Length; ++i )
                myVal[ i ].WriteToStream( writer );
        }

        public override string ToString()
        {
            return "[" + myVal.Length + "]";
        }
    }
    #endregion Value Types

    public static class Info
    {
        internal enum InfoType : byte
        {
            Null    = 0,
            Object  = 1,
            Array   = 2,
            Integer = 3,
            Double  = 4,
            String  = 5,
            Boolean = 6
        }

        private static Dictionary<String, Dictionary<String, InfoObject>> myInfos =
            new Dictionary<string, Dictionary<String, InfoObject>>();

        internal static void Register( InfoObject obj )
        {
            if ( !myInfos.ContainsKey( obj.Type ) )
                myInfos.Add( obj.Type, new Dictionary<String, InfoObject>() );

            if ( myInfos[ obj.Type ].ContainsKey( obj.Name ) )
                myInfos[ obj.Type ][ obj.Name ] = obj;
            else
                myInfos[ obj.Type ].Add( obj.Name, obj );
        }

        public static InfoObject[] GetAll( String type )
        {
            if ( !myInfos.ContainsKey( type ) )
                return new InfoObject[ 0 ];

            return myInfos[ type ].Values.ToArray();
        }

        public static InfoObject[] ParseString( String str )
        {
            List<InfoObject> objs = new List<InfoObject>();

            int index = 0;
            while ( str.IndexOf( '{', index ) != -1 )
                objs.Add( ReadObject( str, ref index ) );

            return objs.ToArray();
        }

        public static InfoObject[] ParseFile( String filePath )
        {
            FileStream stream = File.Open( filePath, FileMode.Open, FileAccess.Read );
            InfoObject[] objs = ParseStream( stream );
            stream.Close();

            return objs;
        }

        public static InfoObject[] ParseStream( Stream stream )
        {
            StreamReader reader = new StreamReader( stream );
            String str = reader.ReadToEnd();

            return ParseString( str );
        }

        private static bool CheckCharEscaped( String str, int charindex )
        {
            return ( charindex <= 0 || str[ charindex - 1 ] != '\\' ) ?
                false : !CheckCharEscaped( str, charindex - 1 );
        }

        private static InfoType FindNextType( String str, int startIndex )
        {
            MovePastWhitespace( str, ref startIndex );

            switch ( str[ startIndex ] )
            {
                case '{':
                    return InfoType.Object;
                case '"':
                    return InfoType.String;
                case '[':
                    return InfoType.Array;
                default:
                    string boolTestStr = str.Substring( startIndex, 4 ).ToLower();
                    
                    if ( boolTestStr == "null" )
                        return InfoType.Null;

                    if ( boolTestStr == "true" || boolTestStr == "fals" )
                        return InfoType.Boolean;

                    if ( char.IsNumber( str[ startIndex ] ) || str[ startIndex ] == '.' )
                    {
                        do
                        {
                            if ( str[ startIndex ] == '.' )
                                return InfoType.Double;
                        }
                        while ( char.IsNumber( str[ startIndex ++ ] ) );

                        return InfoType.Integer;
                    }
                    return InfoType.Null;
            }
        }

        private static void MovePastWhitespace( String str, ref int startIndex )
        {
            while ( startIndex < str.Length && char.IsWhiteSpace( str[ startIndex ] ) )
                ++startIndex;
        }

        private static int FindValueEnd( String str, int startIndex )
        {
            int depth = 0;

            while ( startIndex < str.Length )
            {
                if ( str[ startIndex ] == '{' || str[ startIndex ] == '[' )
                    ++depth;
                else if ( str[ startIndex ] == '}' || str[ startIndex ] == ']' )
                    --depth;

                if ( depth == -1 || ( depth == 0 && str[ startIndex ] == ',' ) )
                    break;

                ++startIndex;
            }

            return startIndex;
        }

        private static Dictionary<String, InfoValue> ReadObjectContents( String str, ref int startIndex )
        {
            Dictionary<String,InfoValue> dict = new Dictionary<string, InfoValue>();

            startIndex = str.IndexOf( '{', startIndex ) + 1;

            MovePastWhitespace( str, ref startIndex );

            while ( str[ startIndex ] != '}' )
            {
                String key = ReadString( str, ref startIndex );
                MovePastWhitespace( str, ref startIndex );
                ++startIndex;
                InfoValue val = ReadValue( str, ref startIndex );
                dict.Add( key, val );
            }

            ++startIndex;
            MovePastWhitespace( str, ref startIndex );

            return dict;
        }

        private static InfoObject ReadObject( String str, ref int startIndex )
        {
            int objOpenIndex = str.IndexOf( '{', startIndex );
            int typeOpenIndex = str.IndexOf( '(', startIndex );

            InfoObject obj;

            if ( typeOpenIndex > -1 && typeOpenIndex < objOpenIndex )
            {
                String type = ReadType( str, ref startIndex );
                String name = ReadString( str, ref startIndex );
                obj = new InfoObject( ReadObjectContents( str, ref startIndex ), type, name );
            }
            else
                obj = new InfoObject( ReadObjectContents( str, ref startIndex ) );

            if ( startIndex < str.Length && str[ startIndex ] == ',' )
                ++startIndex;

            return obj;
        }

        private static string ReadType( String str, ref int startIndex )
        {
            startIndex = str.IndexOf( '(', startIndex ) + 1;

            int end;
            do
            {
                end = str.IndexOf( ')', startIndex );
            }
            while ( CheckCharEscaped( str, end ) );

            String output = str.Substring( startIndex, end - startIndex );
            startIndex = end + 1;

            return output;
        }

        private static string ReadString( String str, ref int startIndex )
        {
            startIndex = str.IndexOf( '"', startIndex ) + 1;

            int end = str.IndexOf( '"', startIndex );
            while ( CheckCharEscaped( str, end ) )
                end = str.IndexOf( '"', end + 1 );

            String output = str.Substring( startIndex, end - startIndex );
            startIndex = end + 1;

            return output;
        }

        private static InfoObject ReadObjectValue( String str, ref int startIndex )
        {
            return new InfoObject( ReadObjectContents( str, ref startIndex ) );
        }

        private static InfoArray ReadArrayValue( String str, ref int startIndex )
        {
            List<InfoValue> vals = new List<InfoValue>();

            startIndex = str.IndexOf( '[', startIndex ) + 1;

            MovePastWhitespace( str, ref startIndex );

            while ( str[ startIndex ] != ']' )
                vals.Add( ReadValue( str, ref startIndex ) );

            ++startIndex;
            MovePastWhitespace( str, ref startIndex );

            return new InfoArray( vals.ToArray() );
        }

        private static InfoString ReadStringValue( String str, ref int startIndex )
        {
            return new InfoString( ReadString( str, ref startIndex ) );
        }

        private static InfoDouble ReadDoubleValue( String str, ref int startIndex )
        {
            int end = FindValueEnd( str, startIndex );

            String doubleString = str.Substring( startIndex, end - startIndex ).Trim();
            startIndex = end;

            return new InfoDouble( double.Parse( doubleString ) );
        }

        private static InfoInteger ReadIntegerValue( String str, ref int startIndex )
        {
            int end = FindValueEnd( str, startIndex );

            String longString = str.Substring( startIndex, end - startIndex ).Trim();
            startIndex = end;

            return new InfoInteger( long.Parse( longString ) );
        }

        private static InfoBoolean ReadBooleanValue( String str, ref int startIndex )
        {
            int end = FindValueEnd( str, startIndex );

            String boolString = str.Substring( startIndex, end - startIndex ).Trim();
            startIndex = end;

            return new InfoBoolean( bool.Parse( boolString ) );
        }

        private static InfoValue ReadValue( String str, ref int startIndex )
        {
            InfoValue returnVal;

            switch( FindNextType( str, startIndex ) )
            {
                case InfoType.Object:
                    returnVal = ReadObjectValue( str, ref startIndex ); break;
                case InfoType.Array:
                    returnVal = ReadArrayValue( str, ref startIndex ); break;
                case InfoType.String:
                    returnVal = ReadStringValue( str, ref startIndex ); break;
                case InfoType.Double:
                    returnVal = ReadDoubleValue( str, ref startIndex ); break;
                case InfoType.Integer:
                    returnVal = ReadIntegerValue( str, ref startIndex ); break;
                case InfoType.Boolean:
                    returnVal = ReadBooleanValue( str, ref startIndex ); break;
                default:
                    returnVal = new InfoNull();

                    int comma = str.IndexOf( ',', startIndex );
                    int endo = str.IndexOf( '}', startIndex );
                    int enda = str.IndexOf( ']', startIndex );

                    if ( endo != -1 && ( endo < comma || comma == -1 ) )
                        comma = endo;

                    if ( enda != -1 && ( enda < comma || comma == -1 ) )
                        comma = enda;

                    if ( comma != -1 )
                        startIndex = comma;
                    break;
            }

            MovePastWhitespace( str, ref startIndex );

            if ( str[ startIndex ] == ',' )
                ++startIndex;

            return returnVal;
        }
    }
}
