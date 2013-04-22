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

            if (cleanup) {
                if (File.Exists(dest)) {
                    Console.WriteLine("Deleting " + dest);
                    File.Delete(dest);
                }

                String prt = rootName + ".prt";
                String tempPrt = rootName + ".temp.prt";

                if (File.Exists(tempPrt)) {
                    if (File.Exists(prt)) {
                        Console.WriteLine("Deleting " + prt);
                        File.Delete(prt);
                    }

                    Console.WriteLine("Renaming " + tempPrt + " to " + prt);
                    File.Move(tempPrt, prt);
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
