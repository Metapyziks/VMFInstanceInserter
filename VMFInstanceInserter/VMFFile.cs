using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;

namespace VMFInstanceInserter
{
    class VMFFile
    {
        private static Dictionary<String, VMFFile> stVMFCache = new Dictionary<string, VMFFile>();

        public String OriginalPath { get; private set; }
        public VMFStructure Root { get; private set; }
        public VMFStructure World { get; private set; }

        public int LastID { get; private set; }
        public int LastNodeID { get; private set; }

        public VMFFile(String path, String rootDir = null)
        {
            Console.WriteLine("Parsing " + path + "...");

            OriginalPath = (rootDir != null ? rootDir + Path.DirectorySeparatorChar : "") + path;

            if (!File.Exists(OriginalPath)) {
                if (rootDir != null && path.Contains('/') && path.Substring(0, path.IndexOf('/')) == rootDir.Substring(rootDir.LastIndexOf('\\') + 1))
                    OriginalPath = rootDir + Path.DirectorySeparatorChar + path.Substring(path.IndexOf('/') + 1);

                if (!File.Exists(OriginalPath)) {
                    Console.WriteLine("File \"" + path + "\" not found!");
                    return;
                }
            }

#if !DEBUG
            try {
#endif
            using (FileStream stream = new FileStream(OriginalPath, FileMode.Open, FileAccess.Read))
                Root = new VMFStructure("file", new StreamReader(stream));
#if !DEBUG
            }
            catch( Exception e )
            {
                Console.WriteLine( "Error while parsing file!" );
                Console.WriteLine( e.ToString() );
                return;
            }
#endif

            foreach (VMFStructure stru in Root) {
                if (stru.Type == VMFStructureType.World) {
                    World = stru;
                    break;
                }
            }

            LastID = Root.GetLastID();
            LastNodeID = Root.GetLastNodeID();

            stVMFCache.Add(path, this);
        }

        public void ResolveInstances()
        {
            Console.WriteLine("Resolving instances for " + OriginalPath + "...");
            List<VMFStructure> structures = Root.Structures;

            int autoName = 0;

            for (int i = structures.Count - 1; i >= 0; --i) {
                VMFStructure structure = structures[i];

                if (structure.Type == VMFStructureType.Entity) {
                    VMFValue classnameVal = structure["classname"];
                    if (classnameVal != null && classnameVal.String == "func_instance") {
                        structures.RemoveAt(i);

                        VMFStringValue fileVal = structure["file"] as VMFStringValue;
                        VMFVector3Value originVal = (structure["origin"] as VMFVector3Value) ?? new VMFVector3Value { X = 0, Y = 0, Z = 0 };
                        VMFVector3Value anglesVal = (structure["angles"] as VMFVector3Value) ?? new VMFVector3Value { Pitch = 0, Roll = 0, Yaw = 0 };
                        VMFNumberValue fixup_styleVal = (structure["fixup_style"] as VMFNumberValue) ?? new VMFNumberValue { Value = 0 };
                        VMFValue targetnameVal = structure["targetname"];

                        Regex pattern = new Regex("^replace[0-9]*$");
                        List<KeyValuePair<String, String>> replacements = new List<KeyValuePair<String, String>>();

                        foreach (KeyValuePair<String, VMFValue> keyVal in structure.Properties) {
                            if (pattern.IsMatch(keyVal.Key)) {
                                String[] split = keyVal.Value.String.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                if (split.Length < 1)
                                    continue;

                                if (!split[0].StartsWith("$")) {
                                    Console.WriteLine("Invalid property replacement name \"{0}\" - needs to begin with a $", split[0]);
                                    continue;
                                }

                                replacements.Add(new KeyValuePair<String, String>(split[0], keyVal.Value.String.Substring(split[0].Length + 1).TrimStart()));
                            }
                        }

                        replacements = replacements.OrderByDescending(x => x.Key.Length).ToList();

                        TargetNameFixupStyle fixupStyle = (TargetNameFixupStyle) fixup_styleVal.Value;
                        String targetName = (targetnameVal != null ? targetnameVal.String : null);

                        if (fixupStyle != TargetNameFixupStyle.None && targetName == null)
                            targetName = "AutoInstance" + (autoName++);

                        if (fileVal == null) {
                            Console.WriteLine("Invalid instance at (" + originVal.String + ")");
                            continue;
                        }

                        Console.WriteLine("Inserting instance of {0} at ({1}), ({2})", fileVal.String, originVal.String, anglesVal.String);

                        String file = fileVal.String;
                        VMFFile vmf = null;

                        if (stVMFCache.ContainsKey(file))
                            vmf = stVMFCache[file];
                        else {
                            vmf = new VMFFile(file, Path.GetDirectoryName(OriginalPath));
                            if (vmf.Root != null)
                                vmf.ResolveInstances();
                        }

                        if (vmf.Root == null) {
                            Console.WriteLine("Could not insert!");
                            continue;
                        }

                        foreach (VMFStructure worldStruct in vmf.World) {
                            if (worldStruct.Type == VMFStructureType.Group || worldStruct.Type == VMFStructureType.Solid) {
                                VMFStructure clone = worldStruct.Clone(LastID, LastNodeID, fixupStyle, targetName, replacements);
                                clone.Transform(originVal, anglesVal);
                                World.Structures.Add(clone);
                            }
                        }

                        int index = i;

                        foreach (VMFStructure rootStruct in vmf.Root) {
                            if (rootStruct.Type == VMFStructureType.Entity) {
                                VMFStructure clone = rootStruct.Clone(LastID, LastNodeID, fixupStyle, targetName, replacements);
                                clone.Transform(originVal, anglesVal);
                                Root.Structures.Insert(index++, clone);
                            }
                        }

                        LastID = Root.GetLastID();
                        LastNodeID = Root.GetLastNodeID();
                    }
                }
            }

            Console.WriteLine("Instances resolved.");
        }

        public void Save(String path)
        {
            Console.WriteLine("Saving to " + path + "...");

            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write))
                Root.Write(new StreamWriter(stream));
        }
    }
}
