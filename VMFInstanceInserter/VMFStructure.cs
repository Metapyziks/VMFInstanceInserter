using System;
using System.Collections.Generic;
using System.Linq;
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
        EntityName = 4,
        Identifier = 5
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
        private static readonly Dictionary<String, TransformType> stInputsDict = new Dictionary<String, TransformType>();

        static VMFStructure()
        {
            stTypeDict = new Dictionary<string, VMFStructureType>();

            foreach (String name in Enum.GetNames(typeof(VMFStructureType)))
                stTypeDict.Add(name.ToLower(), (VMFStructureType) Enum.Parse(typeof(VMFStructureType), name));

            String infoPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + Path.DirectorySeparatorChar + "entities.txt";

            if (File.Exists(infoPath)) {
                Console.WriteLine("Loading entities.txt");

                try {
                    var objs = Info.ParseFile(infoPath).Select(
                        x => new KeyValuePair<String, InfoObject>(x.Name, x)).ToList();

                    var inps = new List<KeyValuePair<string, TransformType>>();

                    if (objs.Count == 1 && objs[0].Key == "unnamed") {
                        var props = objs[0].Value["properties"] as InfoObject;
                        var inpts = objs[0].Value["inputs"] as InfoObject;
                        if (props != null) {
                            objs = props.Select(x => new KeyValuePair<String, InfoObject>(x.Key, (InfoObject) x.Value)).ToList();
                        } else {
                            objs = new List<KeyValuePair<string, InfoObject>>();
                        }
                        if (inpts != null) {
                            inps = inpts.Select(x => new KeyValuePair<String, TransformType>(
                                x.Key, ParseTransformType(x.Value.AsString()))).ToList();
                        }
                    }

                    foreach (var obj in objs) {
                        if (!stEntitiesDict.ContainsKey(obj.Key))
                            stEntitiesDict.Add(obj.Key, new Dictionary<string, TransformType>());

                        foreach (KeyValuePair<String, InfoValue> keyVal in obj.Value) {
                            if (keyVal.Value is InfoString) {
                                TransformType trans = ParseTransformType(keyVal.Value.AsString());
                                if (stEntitiesDict[obj.Key].ContainsKey(keyVal.Key)) {
                                    if (trans == TransformType.None) {
                                        stEntitiesDict[obj.Key].Remove(keyVal.Key);
                                    } else {
                                        stEntitiesDict[obj.Key][keyVal.Key] = trans;
                                    }
                                } else if (trans != TransformType.None) {
                                    stEntitiesDict[obj.Key].Add(keyVal.Key, trans);
                                }
                            }
                        }
                    }

                    foreach (var inp in inps) {
                        if (!stInputsDict.ContainsKey(inp.Key))
                            stInputsDict.Add(inp.Key, inp.Value);
                        else
                            stInputsDict[inp.Key] = inp.Value;
                    }
                } catch {
                    Console.WriteLine("Error while reading entities.txt");
                }
            } else {
                Console.WriteLine("File entities.txt not found!");
            }
        }

        private static TransformType ParseTransformType(String str)
        {
            switch (str.ToLower()) {
                case "n":
                case "null":
                case "nil":
                case "none":
                    return TransformType.None;
                case "o":
                case "off":
                case "offset":
                    return TransformType.Offset;
                case "a":
                case "ang":
                case "angle":
                    return TransformType.Angle;
                case "p":
                case "pos":
                case "position":
                    return TransformType.Position;
                case "e":
                case "ent":
                case "entity":
                    return TransformType.EntityName;
                case "i":
                case "id":
                case "ident":
                case "identifier":
                    return TransformType.Identifier;
                default:
                    Console.WriteLine("Bad transform type: " + str);
                    return TransformType.None;
            }
        }

        private static String FixupName(String name, TargetNameFixupStyle fixupStyle, String targetName)
        {
            if (fixupStyle == TargetNameFixupStyle.None || targetName == null || name.StartsWith("@"))
                return name;

            switch (fixupStyle) {
                case TargetNameFixupStyle.Postfix:
                    return name + targetName;
                case TargetNameFixupStyle.Prefix:
                    return targetName + name;
                default:
                    return name;
            }
        }

        private int myIDIndex;

        public VMFStructureType Type { get; private set; }
        public int ID
        {
            get
            {
                if (myIDIndex == -1)
                    return 0;

                return (int) (Properties[myIDIndex].Value as VMFNumberValue).Value;
            }
            set
            {
                if (myIDIndex == -1)
                    return;

                (Properties[myIDIndex].Value as VMFNumberValue).Value = value;
            }
        }

        public List<KeyValuePair<String, VMFValue>> Properties { get; private set; }
        public List<VMFStructure> Structures { get; private set; }

        public VMFValue this[String key]
        {
            get
            {
                foreach (KeyValuePair<String, VMFValue> keyVal in Properties)
                    if (keyVal.Key == key)
                        return keyVal.Value;

                return null;
            }
        }

        private static readonly Dictionary<String, TransformType> stDefaultEntDict = new Dictionary<string,TransformType>();
        private VMFStructure(VMFStructure clone, int idOffset, TargetNameFixupStyle fixupStyle, String targetName)
        {
            Type = clone.Type;

            Properties = new List<KeyValuePair<string, VMFValue>>();
            Structures = new List<VMFStructure>();

            myIDIndex = clone.myIDIndex;

            Dictionary<String, TransformType> entDict = stDefaultEntDict;

            if (Type == VMFStructureType.Entity) {
                String className = clone["classname"].String;
                if (className != null && stEntitiesDict.ContainsKey(className))
                    entDict = stEntitiesDict[className];
            }

            foreach (KeyValuePair<String, VMFValue> keyVal in clone.Properties) {
                KeyValuePair<String, VMFValue> kvClone = new KeyValuePair<string, VMFValue>(keyVal.Key, keyVal.Value.Clone());

                if (Type == VMFStructureType.Connections) {
                    if (fixupStyle != TargetNameFixupStyle.None && targetName != null) {
                        String[] split = kvClone.Value.String.Split(',');
                        split[0] = FixupName(split[0], fixupStyle, targetName);
                        kvClone.Value.String = String.Join(",", split);
                    }
                } else {
                    if (Type == VMFStructureType.Side && clone.ID == 143)
                        myIDIndex = myIDIndex;

                    if (kvClone.Key == "groupid")
                        ((VMFNumberValue) kvClone.Value).Value += idOffset;
                    else if (Type == VMFStructureType.Entity) {
                        TransformType trans = entDict.ContainsKey(kvClone.Key) ? entDict[kvClone.Key] : TransformType.None;

                        if (trans == TransformType.Identifier) {
                            kvClone.Value.OffsetIdentifiers(idOffset);
                        } else if ((kvClone.Key == "targetname" || trans == TransformType.EntityName) && fixupStyle != TargetNameFixupStyle.None && targetName != null) {
                            ((VMFStringValue) kvClone.Value).String = FixupName(((VMFStringValue) kvClone.Value).String, fixupStyle, targetName);
                        }
                    }
                }

                Properties.Add(kvClone);
            }

            foreach (VMFStructure structure in clone.Structures)
                Structures.Add(new VMFStructure(structure, idOffset, fixupStyle, targetName));

            ID += idOffset;
        }

        public VMFStructure(String type, StreamReader reader)
        {
            if (stTypeDict.ContainsKey(type))
                Type = stTypeDict[type];
            else
                Type = VMFStructureType.Unknown;

            Properties = new List<KeyValuePair<String, VMFValue>>();
            Structures = new List<VMFStructure>();

            myIDIndex = -1;

            String line;
            while (!reader.EndOfStream && (line = reader.ReadLine().Trim()) != "}") {
                if (line == "{" || line.Length == 0)
                    continue;

                if (line[0] == '"') {
                    String[] pair = line.Trim('"').Split(new String[] { "\" \"" }, StringSplitOptions.None);
                    if (pair.Length != 2)
                        continue;

                    KeyValuePair<String, VMFValue> keyVal = new KeyValuePair<string, VMFValue>(pair[0], VMFValue.Parse(pair[1]));

                    if (keyVal.Key == "id" && keyVal.Value is VMFNumberValue)
                        myIDIndex = Properties.Count;

                    Properties.Add(keyVal);
                } else {
                    Structures.Add(new VMFStructure(line, reader));
                }
            }
        }

        public void Write(StreamWriter writer, int depth = 0)
        {
            if (Type == VMFStructureType.File) {
                foreach (VMFStructure structure in Structures)
                    structure.Write(writer, depth);
            } else {
                String indent = "";
                for (int i = 0; i < depth; ++i)
                    indent += "\t";

                writer.WriteLine(indent + Type.ToString().ToLower());
                writer.WriteLine(indent + "{");
                foreach (KeyValuePair<String, VMFValue> keyVal in Properties)
                    writer.WriteLine(indent + "\t\"" + keyVal.Key + "\" \"" + keyVal.Value.String + "\"");
                foreach (VMFStructure structure in Structures)
                    structure.Write(writer, depth + 1);
                writer.WriteLine(indent + "}");
            }

            writer.Flush();
        }

        public VMFStructure Clone(int idOffset = 0, TargetNameFixupStyle fixupStyle = TargetNameFixupStyle.None, String targetName = null)
        {
            return new VMFStructure(this, idOffset, fixupStyle, targetName);
        }

        public void ReplaceProperties(List<KeyValuePair<String, String>> dict)
        {
            if (Type == VMFStructureType.Entity || Type == VMFStructureType.Connections) {
                for (int i = 0; i < Properties.Count; ++i) {
                    String str = Properties[i].Value.String;

                    if (str.Contains('$')) {
                        foreach (KeyValuePair<String, String> keyVal in dict)
                            str = str.Replace(keyVal.Key, keyVal.Value);

                        Properties[i] = new KeyValuePair<string, VMFValue>(Properties[i].Key, VMFValue.Parse(str));
                    }
                }

                if (Type == VMFStructureType.Entity)
                    foreach (VMFStructure strct in Structures)
                        if (strct.Type == VMFStructureType.Connections)
                            strct.ReplaceProperties(dict);
            }
        }

        public void Transform(VMFVector3Value translation, VMFVector3Value rotation)
        {
            Dictionary<String, TransformType> transDict = null;
            Dictionary<String, TransformType> entDict = null;

            if (stTransformDict.ContainsKey(Type))
                transDict = stTransformDict[Type];

            if (Type == VMFStructureType.Entity) {
                String className = this["classname"].String;
                if (className != null && stEntitiesDict.ContainsKey(className))
                    entDict = stEntitiesDict[className];
            }

            if (transDict != null || entDict != null) {
                foreach (KeyValuePair<String, VMFValue> keyVal in Properties) {
                    TransformType trans = TransformType.None;

                    if (transDict != null && transDict.ContainsKey(keyVal.Key))
                        trans = transDict[keyVal.Key];

                    if (entDict != null && entDict.ContainsKey(keyVal.Key))
                        trans = entDict[keyVal.Key];

                    switch (trans) {
                        case TransformType.Offset:
                            keyVal.Value.Rotate(rotation);
                            break;
                        case TransformType.Angle:
                            keyVal.Value.AddAngles(rotation);
                            break;
                        case TransformType.Position:
                            keyVal.Value.Rotate(rotation);
                            keyVal.Value.Offset(translation);
                            break;
                    }
                }
            }

            foreach (VMFStructure structure in Structures)
                structure.Transform(translation, rotation);
        }

        public int GetLastID()
        {
            int max = ID;

            foreach (VMFStructure structure in Structures)
                max = Math.Max(structure.GetLastID(), max);

            return max;
        }

        public bool ContainsKey(String key)
        {
            foreach (KeyValuePair<String, VMFValue> keyVal in Properties)
                if (keyVal.Key == key)
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
