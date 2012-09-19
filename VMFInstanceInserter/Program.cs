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
            // TODO: Add complete argument parsing

            if ( args.Length < 1 )
            {
                Console.WriteLine( "Unexpected arguments. Aborting..." );
                return;
            }

            String vmf = args[ 0 ];
            String dest = ( args.Length >= 2 ? args[ 1 ] : null );

            VMFFile file = new VMFFile( vmf );
            file.ResolveInstances();
            file.Save( dest );
#if DEBUG
            Console.WriteLine( "Press any key to exit..." );
            Console.ReadKey();
#endif
        }
    }
}
