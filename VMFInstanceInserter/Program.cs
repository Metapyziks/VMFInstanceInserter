using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace VMFInstanceInserter
{
    class Program
    {
        static void Main( string[] args )
        {
            List<String> paths = new List<string>();
            bool cleanup = false;

            foreach ( String arg in args )
            {
                if ( !arg.StartsWith( "-" ) )
                    paths.Add( arg );
                else
                {
                    switch ( arg.Substring( 1 ).ToLower() )
                    {
                        case "c":
                        case "cleanup":
                            cleanup = true;
                            break;
                    }
                }
            }

            if( paths.Count < 1 )
            {
                Console.WriteLine( "Unexpected arguments. Aborting..." );
                return;
            }

            String vmf = paths[ 0 ];
            String rootName = Path.GetDirectoryName( vmf ) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension( vmf );
            String dest = ( paths.Count >= 2 ? paths[ 1 ] : rootName + ".temp.vmf" );

            if ( cleanup )
            {
                if ( File.Exists( dest ) )
                {
                    Console.WriteLine( "Deleting " + dest );
                    File.Delete( dest );
                }

                String prt = rootName + ".prt";
                String tempPrt = rootName + ".temp.prt";

                if( File.Exists( tempPrt ) )
                {
                    if ( File.Exists( prt ) )
                    {
                        Console.WriteLine( "Deleting " + prt );
                        File.Delete( prt );
                    }

                    Console.WriteLine( "Renaming " + tempPrt + " to " + prt );
                    File.Move( tempPrt, prt );
                }
            }
            else
            {

                VMFFile file = new VMFFile( vmf );
                file.ResolveInstances();
                file.Save( dest );
            }

#if DEBUG
            Console.WriteLine( "Press any key to exit..." );
            Console.ReadKey();
#endif
        }
    }
}
