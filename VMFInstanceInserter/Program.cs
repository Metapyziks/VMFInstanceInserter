using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace VMFInstanceInserter
{
    class Program
    {
        static void Main( string[] args )
        {
            // TODO: Add complete argument parsing

            String game = "";
            String vmf = "";

            for ( int i = 0; i < args.Length; ++i )
            {
                String arg = args[ i ];
                if ( arg == "-game" )
                    game = args[ ++i ];
                else
                    vmf = arg;
            }

            VMFFile file = new VMFFile( vmf );
            file.Save( "test.vmf" );

            Console.ReadKey();
        }
    }
}
