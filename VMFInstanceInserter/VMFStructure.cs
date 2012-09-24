using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;

using ResourceLib;

namespace VMFInstanceInserter
{
    enum VMFStructureType
    {
        File,
        VersionInfo,
        VisGroups,
        ViewSettings,
        World,
        Solid,
        Side,
        Editor,
        Entity,
        Connections,
        Group,
        Cameras,
        Camera,
        Cordon,
        Visgroup,
        DispInfo,
        Hidden,
        Normals,
        Distances,
        Offsets,
        Offset_Normals,
        Alphas,
        Triangle_Tags,
        Allowed_Verts,
        Unknown
    }

    enum TransformType
    {
        None = 0,
        Offset = 1,
        Angle = 2,
        Position = 3,
        EntityName = 4
    }

    enum TargetNameFixupStyle
    {
        Prefix = 0,
        Postfix = 1,
        None = 2
    }

    class VMFStructure : IEnumerable<VMFStructure>
    {
        private static readonly Dictionary<String, VMFStructureType> stTypeDict;

        private static readonly Dictionary<VMFStructureType, Dictionary<String, TransformType>> stTransformDict = new Dictionary<VMFStructureType, Dictionary<string, TransformType>>
        {
            { VMFStructureType.Side, new Dictionary<String, TransformType>
            {
                { "plane", TransformType.Position },
                { "uaxis", TransformType.Position },
                { "vaxis", TransformType.Position }
            } },
            { VMFStructureType.Entity, new Dictionary<String, TransformType>
            {
                { "origin", TransformType.Position },
                { "_shadoworiginoffset", TransformType.Offset },
                { "angles", TransformType.Angle }
            } }
        };

        private static readonly Dictionary<String, Dictionary<String, TransformType>> stEntitiesDict = new Dictionary<String, Dictionary<string, TransformType>>();

        static VMFStructure()
        {
            stTypeDict = new Dictionary<string, VMFStructureType>();

            foreach ( String name in Enum.GetNames( typeof( VMFStructureType ) ) )
                stTypeDict.Add( name.ToLower(), (VMFStructureType) Enum.Parse( typeof( VMFStructureType ), name ) );

            String infoPath = Path.GetDirectoryName( Assembly.GetExecutingAssembly().Location ) + Path.DirectorySeparatorChar + "entities.txt";

            if ( File.Exists( infoPath ) )
            {
                Console.WriteLine( "Loading entities.txt" );

                try
                {
                    foreach ( InfoObject obj in Info.ParseFile( infoPath ) )
                    {
                        if ( !stEntitiesDict.ContainsKey( obj.Name ) )
                            stEntitiesDict.Add( obj.Name, new Dictionary<string, TransformType>() );

                        foreach ( KeyValuePair<String, InfoValue> keyVal in obj )
                        {
                            if ( keyVal.Value is InfoString )
                            {
                                String str = keyVal.Value.AsString().ToLower();
                                switch ( str )
                                {
                                    case "n":
                                    case "null":
                                    case "nil":
                                    case "none":
                                        if ( stEntitiesDict[ obj.Name ].ContainsKey( keyVal.Key ) )
                                            stEntitiesDict[ obj.Name ].Remove( keyVal.Key );
                                        break;
                                    case "o":
                                    case "off":
                                    case "offset":
                                        if ( stEntitiesDict[ obj.Name ].ContainsKey( keyVal.Key ) )
                                            stEntitiesDict[ obj.Name ][ keyVal.Key ] = TransformType.Offset;
                                        else
                                            stEntitiesDict[ obj.Name ].Add( keyVal.Key, TransformType.Offset );
                                        break;
                                    case "a":
                                    case "ang":
                                    case "angle":
                                        if ( stEntitiesDict[ obj.Name ].ContainsKey( keyVal.Key ) )
                                            stEntitiesDict[ obj.Name ][ keyVal.Key ] = TransformType.Angle;
                                        else
                                            stEntitiesDict[ obj.Name ].Add( keyVal.Key, TransformType.Angle );
                                        break;
                                    case "p":
                                    case "pos":
                                    case "position":
                                        if ( stEntitiesDict[ obj.Name ].ContainsKey( keyVal.Key ) )
                                            stEntitiesDict[ obj.Name ][ keyVal.Key ] = TransformType.Position;
                                        else
                                            stEntitiesDict[ obj.Name ].Add( keyVal.Key, TransformType.Position );
                                        break;
                                    case "e":
                                    case "ent":
                                    case "entity":
                                        if ( stEntitiesDict[ obj.Name ].ContainsKey( keyVal.Key ) )
                                            stEntitiesDict[ obj.Name ][ keyVal.Key ] = TransformType.EntityName;
                                        else
                                            stEntitiesDict[ obj.Name ].Add( keyVal.Key, TransformType.EntityName );
                                        break;
                                    default:
                                        Console.WriteLine( "Bad definition for " + obj.Name + "." + keyVal.Key + ": " + str );
                                        break;
                                }
                            }
                        }
                    }
                }
                catch
                {
                    Console.WriteLine( "Error while reading entities.txt" );
                }
            }
            else
            {
                Console.WriteLine( "File entities.txt not found!" );
            }
        }

        private int myIDIndex;

        public VMFStructureType Type { get; private set; }
        public int ID
        {
            get
            {
                if ( myIDIndex == -1 )
                    return 0;

                return (int) ( Properties[ myIDIndex ].Value as VMFNumberValue ).Value;
            }
            set
            {
                if ( myIDIndex == -1 )
                    return;

                ( Properties[ myIDIndex ].Value as VMFNumberValue ).Value = value;
            }
        }

        public List<KeyValuePair<String, VMFValue>> Properties { get; private set; }
        public List<VMFStructure> Structures { get; private set; }

        public VMFValue this[ String key ]
        {
            get
            {
                foreach ( KeyValuePair<String, VMFValue> keyVal in Properties )
                    if ( keyVal.Key == key )
                        return keyVal.Value;

                return null;
            }
        }

        private VMFStructure( VMFStructure clone, int idOffset, TargetNameFixupStyle fixupStyle, String targetName )
        {
            Type = clone.Type;

            Properties = new List<KeyValuePair<string, VMFValue>>();
            Structures = new List<VMFStructure>();

            myIDIndex = clone.myIDIndex;

            Dictionary<String, TransformType> entDict = null;

            if ( Type == VMFStructureType.Entity )
            {
                String className = clone[ "classname" ].String;
                if ( className != null && stEntitiesDict.ContainsKey( className ) )
                    entDict = stEntitiesDict[ className ];
            }

            foreach ( KeyValuePair<String, VMFValue> keyVal in clone.Properties )
            {
                KeyValuePair<String, VMFValue> kvClone = new KeyValuePair<string, VMFValue>( keyVal.Key, keyVal.Value.Clone() );

                if ( Type == VMFStructureType.Connections )
                {
                    if ( fixupStyle != TargetNameFixupStyle.None && targetName != null )
                    {
                        String[] split = kvClone.Value.String.Split( ',' );
                        if ( split[ 0 ].Length > 0 )
                        {
                            switch ( fixupStyle )
                            {
                                case TargetNameFixupStyle.Prefix:
                                    split[ 0 ] = targetName + split[ 0 ];
                                    break;
                                case TargetNameFixupStyle.Postfix:
                                    split[ 0 ] = split[ 0 ] + targetName;
                                    break;
                            }
                        }
                        kvClone.Value.String = String.Join( ",", split );
                    }
                }
                else
                {
                    if ( kvClone.Key == "groupid" )
                        ( (VMFNumberValue) kvClone.Value ).Value += idOffset;
                    else if ( ( kvClone.Key == "targetname" || ( entDict != null && entDict.ContainsKey( keyVal.Key ) && entDict[ keyVal.Key ] == TransformType.EntityName ) )
                        && fixupStyle != TargetNameFixupStyle.None && targetName != null )
                    {
                        switch ( fixupStyle )
                        {
                            case TargetNameFixupStyle.Prefix:
                                ( (VMFStringValue) kvClone.Value ).String = targetName + ( (VMFStringValue) kvClone.Value ).String;
                                break;
                            case TargetNameFixupStyle.Postfix:
                                ( (VMFStringValue) kvClone.Value ).String = ( (VMFStringValue) kvClone.Value ).String + targetName;
                                break;
                        }
                    }
                }

                Properties.Add( kvClone );
            }

            foreach ( VMFStructure structure in clone.Structures )
                Structures.Add( new VMFStructure( structure, idOffset, fixupStyle, targetName ) );

            ID += idOffset;
        }

        public VMFStructure( String type, StreamReader reader )
        {
            if ( stTypeDict.ContainsKey( type ) )
                Type = stTypeDict[ type ];
            else
                Type = VMFStructureType.Unknown;

            Properties = new List<KeyValuePair<String, VMFValue>>();
            Structures = new List<VMFStructure>();

            myIDIndex = -1;

            String line;
            while ( !reader.EndOfStream && ( line = reader.ReadLine().Trim() ) != "}" )
            {
                if ( line == "{" || line.Length == 0 )
                    continue;

                if ( line[ 0 ] == '"' )
                {
                    String[] pair = line.Trim( '"' ).Split( new String[] { "\" \"" }, StringSplitOptions.None );
                    if ( pair.Length != 2 )
                        continue;

                    KeyValuePair<String, VMFValue> keyVal = new KeyValuePair<string, VMFValue>( pair[ 0 ], VMFValue.Parse( pair[ 1 ] ) );

                    if ( keyVal.Key == "id" && keyVal.Value is VMFNumberValue )
                        myIDIndex = Properties.Count;

                    Properties.Add( keyVal );
                }
                else
                {
                    Structures.Add( new VMFStructure( line, reader ) );
                }
            }
        }

        public void Write( StreamWriter writer, int depth = 0 )
        {
            if ( Type == VMFStructureType.File )
            {
                foreach ( VMFStructure structure in Structures )
                    structure.Write( writer, depth );
            }
            else
            {
                String indent = "";
                for ( int i = 0; i < depth; ++i )
                    indent += "\t";

                writer.WriteLine( indent + Type.ToString().ToLower() );
                writer.WriteLine( indent + "{" );
                foreach ( KeyValuePair<String, VMFValue> keyVal in Properties )
                    writer.WriteLine( indent + "\t\"" + keyVal.Key + "\" \"" + keyVal.Value.String + "\"" );
                foreach ( VMFStructure structure in Structures )
                    structure.Write( writer, depth + 1 );
                writer.WriteLine( indent + "}" );
            }

            writer.Flush();
        }

        public VMFStructure Clone( int idOffset = 0, TargetNameFixupStyle fixupStyle = TargetNameFixupStyle.None, String targetName = null )
        {
            return new VMFStructure( this, idOffset, fixupStyle, targetName );
        }

        public void ReplaceProperties( Dictionary<String, VMFValue> dict )
        {
            if ( Type == VMFStructureType.Entity )
            {
                for ( int i = 0; i < Properties.Count; ++i )
                {
                    KeyValuePair<String, VMFValue> keyVal = Properties[ i ];
                    if ( dict.ContainsKey( keyVal.Key ) )
                        Properties[ i ] = new KeyValuePair<string, VMFValue>( keyVal.Key, dict[ keyVal.Key ].Clone() );
                }
            }
        }

        public void Transform( VMFVector3Value translation, VMFVector3Value rotation )
        {
            Dictionary<String, TransformType> transDict = null;
            Dictionary<String, TransformType> entDict = null;

            if ( stTransformDict.ContainsKey( Type ) )
                transDict = stTransformDict[ Type ];

            if ( Type == VMFStructureType.Entity )
            {
                String className = this[ "classname" ].String;
                if ( className != null && stEntitiesDict.ContainsKey( className ) )
                    entDict = stEntitiesDict[ className ];
            }

            if ( transDict != null || entDict != null )
            {
                foreach ( KeyValuePair<String, VMFValue> keyVal in Properties )
                {
                    TransformType trans = TransformType.None;

                    if ( transDict != null && transDict.ContainsKey( keyVal.Key ) )
                        trans = transDict[ keyVal.Key ];

                    if ( entDict != null && entDict.ContainsKey( keyVal.Key ) )
                        trans = entDict[ keyVal.Key ];

                    switch ( trans )
                    {
                        case TransformType.Offset:
                            keyVal.Value.Rotate( rotation );
                            break;
                        case TransformType.Angle:
                            keyVal.Value.AddAngles( rotation );
                            break;
                        case TransformType.Position:
                            keyVal.Value.Rotate( rotation );
                            keyVal.Value.Offset( translation );
                            break;
                    }
                }
            }

            foreach ( VMFStructure structure in Structures )
                structure.Transform( translation, rotation );
        }

        public int GetLastID()
        {
            int max = ID;

            foreach ( VMFStructure structure in Structures )
                max = Math.Max( structure.GetLastID(), max );

            return max;
        }

        public bool ContainsKey( String key )
        {
            foreach ( KeyValuePair<String, VMFValue> keyVal in Properties )
                if ( keyVal.Key == key )
                    return true;

            return false;
        }

        public override string ToString()
        {
            return Type + " {}";
        }

        public IEnumerator<VMFStructure> GetEnumerator()
        {
            return Structures.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return Structures.GetEnumerator();
        }
    }
}
