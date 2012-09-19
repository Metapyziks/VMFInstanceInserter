using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace VMFInstanceInserter
{
    class VMFFile
    {
        public String Path { get; private set; }
        public List<VMFStructure> Structures { get; private set; }

        public VMFFile( String path )
        {
            Path = path;
            Structures = new List<VMFStructure>();

            using ( FileStream stream = new FileStream( path, FileMode.Open, FileAccess.Read ) )
            {
                StreamReader reader = new StreamReader( stream );
                while ( !reader.EndOfStream )
                    Structures.Add( new VMFStructure( reader.ReadLine(), reader ) );
            }
        }

        public void Save( String path )
        {
            using ( FileStream stream = new FileStream( path, FileMode.Create, FileAccess.Write ) )
            {
                StreamWriter writer = new StreamWriter( stream );
                foreach ( VMFStructure structure in Structures )
                {
                    structure.Write( writer );
                    writer.Flush();
                }
            }
        }
    }
}
