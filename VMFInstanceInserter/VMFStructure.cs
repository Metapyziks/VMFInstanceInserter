using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace VMFInstanceInserter
{
    enum VMFStructureType
    {
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
        Cordon
    }

    class VMFStructure
    {
        private static readonly Dictionary<String, VMFStructureType> stTypeDict;

        static VMFStructure()
        {
            stTypeDict = new Dictionary<string, VMFStructureType>();

            foreach ( String name in Enum.GetNames( typeof( VMFStructureType ) ) )
                stTypeDict.Add( name.ToLower(), (VMFStructureType) Enum.Parse( typeof( VMFStructureType ), name ) );
        }

        public VMFStructureType Type { get; private set; }

        public List<KeyValuePair<String, VMFValue>> Properties { get; private set; }
        public List<VMFStructure> Structures { get; private set; }

        public VMFStructure( String type, StreamReader reader )
        {
            Type = stTypeDict[ type ];

            Properties = new List<KeyValuePair<String, VMFValue>>();
            Structures = new List<VMFStructure>();

            reader.ReadLine(); // {

            String line;
            while ( ( line = reader.ReadLine().Trim() ) != "}" )
            {
                if ( line.Length == 0 )
                    continue;

                if ( line[ 0 ] == '"' )
                {
                    String[] pair = line.Trim( '"' ).Split( new String[] { "\" \"" }, StringSplitOptions.None );
                    if ( pair.Length != 2 )
                        continue;

                    Properties.Add( new KeyValuePair<String, VMFValue>( pair[ 0 ], VMFValue.Parse( pair[ 1 ] ) ) );
                }
                else
                {
                    Structures.Add( new VMFStructure( line, reader ) );
                }
            }
        }

        public void Write( StreamWriter writer, int depth = 0 )
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

        public override string ToString()
        {
            return Type + " {}";
        }
    }
}
