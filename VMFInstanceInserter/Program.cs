using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VMFInstanceInserter
{
    class Program
    {
        static void Main(string[] args)
        {
#if !DEBUG
            Directory.SetCurrentDirectory(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location));
#endif

            List<String> paths = new List<string>();
            bool cleanup = false;

            string[] fgdpaths = new string[0];

            for (int i = 0; i < args.Length; ++i) {
                string arg = args[i];
                if (!arg.StartsWith("-"))
                    paths.Add(arg);
                else {
                    switch (arg.Substring(1).ToLower()) {
                        case "c":
                        case "-cleanup":
                            cleanup = true;
                            break;
                        case "d":
                        case "-fgd":
                            fgdpaths = args[++i].Split(',').Select(x => x.Trim()).ToArray();
                            break;
                    }
                }
            }

            if (paths.Count < 1) {
                Console.WriteLine("Unexpected arguments. Aborting...");
                return;
            }

            String vmf = paths[0];
            String rootName = Path.GetDirectoryName(vmf) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(vmf);
            String dest = (paths.Count >= 2 ? paths[1] : rootName + ".temp.vmf");
            String del = "Deleting ";
            String renaming = "Renaming {0} to {1}";

            if (cleanup) {
                if (File.Exists(dest)) {
                    Console.WriteLine(del + dest);
                    File.Delete(dest);
                }

                String prt = rootName + ".prt";
                String tempPrt = rootName + ".temp.prt";

                if (File.Exists(tempPrt)) {
                    if (File.Exists(prt)) {
                        Console.WriteLine(del + prt);
                        File.Delete(prt);
                    }

                    Console.WriteLine(renaming, tempPrt, prt);
                    File.Move(tempPrt, prt);
                }

                String lin = rootName + ".lin";
                String tempLin = rootName + ".temp.lin";

                if (File.Exists(lin))
                {
                    Console.WriteLine(del + lin);
                    File.Delete(lin);
                }

                if (File.Exists(tempLin))
                {
                    Console.WriteLine(renaming, tempLin, lin);
                    File.Move(tempLin, lin);
                }
            } else {
                foreach (String path in fgdpaths) {
                    VMFStructure.ParseFGD(path);
                }

                VMFFile file = new VMFFile(vmf);
                file.ResolveInstances();
                file.Save(dest);
            }

#if DEBUG
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
#endif
        }
    }
}
