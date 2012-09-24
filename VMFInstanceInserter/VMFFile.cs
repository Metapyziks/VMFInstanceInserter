using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;

namespace VMFInstanceInserter
{
    class VMFFile
    {
        private static Dictionary<String, VMFFile> stVMFCache = new Dictionary<string, VMFFile>();

        public String OriginalPath { get; private set; }
        public String DestinationPath { get; set; }
        public VMFStructure Root { get; private set; }
        public VMFStructure World { get; private set; }

        public int LastID { get; private set; }

        public VMFFile( String path )
        {
            Console.WriteLine( "Parsing " + path + "..." );

            OriginalPath = path;
            DestinationPath = Path.GetDirectoryName( path ) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension( path ) + ".temp.vmf";

            try
            {
                using ( FileStream stream = new FileStream( path, FileMode.Open, FileAccess.Read ) )
                    Root = new VMFStructure( "file", new StreamReader( stream ) );
            }
            catch
            {
                Console.WriteLine( "Error while parsing file!" );
                return;
            }

            foreach ( VMFStructure stru in Root )
            {
                if ( stru.Type == VMFStructureType.World )
                {
                    World = stru;
                    break;
                }
            }

            LastID = Root.GetLastID();

            stVMFCache.Add( Path.GetFileName( path ), this );
        }

        public void ResolveInstances()
        {
            Console.WriteLine( "Resolving instances for " + OriginalPath + "..." );
            List<VMFStructure> structures = Root.Structures;

            for( int i = structures.Count - 1; i >= 0; --i )
            {
                VMFStructure structure = structures[ i ];

                if ( structure.Type == VMFStructureType.Entity )
                {
                    VMFValue classnameVal = structure[ "classname" ];
                    if ( classnameVal != null && classnameVal.String == "func_instance" )
                    {
                        structures.RemoveAt( i );

                        VMFStringValue fileVal = structure[ "file" ] as VMFStringValue;
                        VMFVector3Value originVal = ( structure[ "origin" ] as VMFVector3Value ) ?? new VMFVector3Value { X = 0, Y = 0, Z = 0 };
                        VMFVector3Value anglesVal = ( structure[ "angles" ] as VMFVector3Value ) ?? new VMFVector3Value { X = 0, Y = 0, Z = 0 };
                        VMFNumberValue fixup_styleVal = ( structure[ "fixup_style" ] as VMFNumberValue ) ?? new VMFNumberValue { Value = 0 };
                        VMFValue targetnameVal = structure[ "targetname" ];

                        Regex pattern = new Regex( "^replace[0-9]*$" );
                        Dictionary<String, VMFValue> replacements = new Dictionary<string, VMFValue>();

                        foreach ( KeyValuePair<String, VMFValue> keyVal in structure.Properties )
                        {
                            if ( pattern.IsMatch( keyVal.Key ) )
                            {
                                String[] split = keyVal.Value.String.Split( new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries );
                                for ( int j = 0; j < split.Length; ++j )
                                {
                                    if ( split[ j ].StartsWith( "$" ) )
                                        continue;

                                    VMFValue replacement = VMFValue.Parse( String.Join( " ", split, j, split.Length - j ) );

                                    for ( int k = 0; k < j; ++k )
                                        replacements.Add( split[ k ].Substring( 1 ), replacement );

                                    break;
                                }
                            }
                        }

                        TargetNameFixupStyle fixupStyle = (TargetNameFixupStyle) fixup_styleVal.Value;
                        String targetName = ( targetnameVal != null ? targetnameVal.String : null );

                        if ( fileVal == null )
                        {
                            Console.WriteLine( "Invalid instance at (" + originVal.String + ")" );
                            continue;
                        }

                        Console.WriteLine( "Inserting instance of " + fileVal.String + " at (" + originVal.String + ")" );

                        String file = fileVal.String;
                        file = file.Substring( file.IndexOf( '/' ) + 1 );

                        VMFFile vmf = null;

                        if ( stVMFCache.ContainsKey( file ) )
                            vmf = stVMFCache[ file ];
                        else
                        {
                            vmf = new VMFFile( Path.GetDirectoryName( OriginalPath ) + Path.DirectorySeparatorChar + file );
                            if ( vmf.Root != null )
                                vmf.ResolveInstances();
                        }

                        if ( vmf.Root == null )
                        {
                            Console.WriteLine( "Could not insert!" );
                            continue;
                        }

                        foreach ( VMFStructure worldStruct in vmf.World )
                        {
                            if ( worldStruct.Type == VMFStructureType.Group || worldStruct.Type == VMFStructureType.Solid )
                            {
                                VMFStructure clone = worldStruct.Clone( LastID, fixupStyle, targetName );
                                clone.Transform( originVal, anglesVal );
                                World.Structures.Add( clone );
                                LastID = Math.Max( LastID, clone.GetLastID() ); // Probably don't need the Max()
                            }
                        }

                        int index = i;

                        foreach ( VMFStructure rootStruct in vmf.Root )
                        {
                            if ( rootStruct.Type == VMFStructureType.Entity )
                            {
                                VMFStructure clone = rootStruct.Clone( LastID, fixupStyle, targetName );
                                clone.ReplaceProperties( replacements );
                                clone.Transform( originVal, anglesVal );
                                Root.Structures.Insert( index++, clone );
                                LastID = Math.Max( LastID, clone.GetLastID() ); // Probably don't need the Max()
                            }
                        }
                    }
                }
            }

            Console.WriteLine( "Instances resolved." );
        }

        public void Save( String path = null )
        {
            if ( path == null )
                path = DestinationPath;

            Console.WriteLine( "Saving to " + path + "..." );

            using ( FileStream stream = new FileStream( path, FileMode.Create, FileAccess.Write ) )
                Root.Write( new StreamWriter( stream ) );
        }
    }
}
