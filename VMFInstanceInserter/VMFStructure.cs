using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

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
                { "angles", TransformType.Angle },
                { "targetname", TransformType.EntityName },
                { "parentname", TransformType.EntityName }
            } },
            { VMFStructureType.DispInfo, new Dictionary<String, TransformType>
            {
                { "startposition", TransformType.Position }
            } },
            { VMFStructureType.Normals, new Dictionary<String, TransformType>
            {
                { "row[0-9]+", TransformType.Offset }
            } },
            { VMFStructureType.Offset_Normals, new Dictionary<String, TransformType>
            {
                { "row[0-9]+", TransformType.Offset }
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

        private static string TrimFGDLine(String line)
        {
            line = line.Trim();

            bool escaped = false;
            bool inString = false;
            for (int i = 0; i < line.Length; ++i) {
                char c = line[i];
                if (escaped) {
                    escaped = false; break;
                }
                
                if (c == '\\') {
                    escaped = true;
                } else if (c == '"') {
                    inString = !inString;
                } else if (!inString && c == '/') {
                    if (i < line.Length - 1 && line[i + 1] == '/') {
                        return line.Substring(0, i).TrimEnd();
                    }
                }
            }

            return line;
        }

        private static readonly Regex _sIncludeRegex = new Regex("^@include \"[^\"]+\"");
        private static readonly Regex _sClassTypeRegex = new Regex("^@[A-Z][a-z]*Class( |=)");
        private static readonly Regex _sBaseDefRegex = new Regex("base\\(\\s*[A-Za-z0-9_]+(\\s*,\\s*[A-Za-z0-9_]+)*\\s*\\)");
        private static readonly Regex _sParamDefRegex = new Regex("^[a-zA-Z0-9_]+\\s*\\(\\s*[A-Za-z0-9_]+\\s*\\)\\s*:.*$");
        public static void ParseFGD(String path)
        {
            Console.WriteLine("Loading {0}", Path.GetFileName(path));

            if (!File.Exists(path)) {
                Console.WriteLine("File does not exist!");
                return;
            }

            StreamReader reader = new StreamReader(path);

            String curName = null;
            Dictionary<String, TransformType> curDict = null;

            while (!reader.EndOfStream) {
                String line = TrimFGDLine(reader.ReadLine());
                if (line.Length == 0) continue;
                while ((line.EndsWith("+") || line.EndsWith(":")) && !reader.EndOfStream) {
                    line = line.TrimEnd('+', ' ', '\t') + TrimFGDLine(reader.ReadLine());
                }

                if (_sIncludeRegex.IsMatch(line)) {
                    int start = line.IndexOf('"') + 1;
                    int end = line.IndexOf('"', start);
                    ParseFGD(line.Substring(start, end - start));                    
                } else if (_sClassTypeRegex.IsMatch(line)) {
                    int start = line.IndexOf('=') + 1;
                    int end = Math.Max(line.IndexOf(':', start), line.IndexOf('[', start));
                    if (end == -1) end = line.Length;
                    curName = line.Substring(start, end - start).Trim();

                    if (!stEntitiesDict.ContainsKey(curName)) {
                        stEntitiesDict.Add(curName, new Dictionary<string,TransformType>());
                    }

                    curDict = stEntitiesDict[curName];

                    var basesMatch = _sBaseDefRegex.Match(line);
                    while (basesMatch.Success && basesMatch.Index < start) {
                        int baseStart = basesMatch.Value.IndexOf('(') + 1;
                        int baseEnd = basesMatch.Value.IndexOf(')', baseStart);
                        var bases = basesMatch.Value.Substring(baseStart, baseEnd - baseStart).Split(',');

                        foreach (var baseName in bases) {
                            var trimmed = baseName.Trim();
                            if (stEntitiesDict.ContainsKey(trimmed)) {
                                foreach (var keyVal in stEntitiesDict[trimmed]) {
                                    if (!curDict.ContainsKey(keyVal.Key)) {
                                        curDict.Add(keyVal.Key, keyVal.Value);
                                    }
                                }
                            } else {
                                Console.WriteLine("Undefined parent for class {0} : {1}", curName, trimmed);
                            }
                        }

                        basesMatch = basesMatch.NextMatch();
                    }
                } else if (curDict != null && _sParamDefRegex.IsMatch(line)) {
                    int start = line.IndexOf('(') + 1;
                    int end = line.IndexOf(')', start);
                    string name = line.Substring(0, start - 1).TrimEnd();
                    string typeName = line.Substring(start, end - start).Trim().ToLower();
                    TransformType type = TransformType.None;
                    switch (typeName) {
                        case "angle":
                            type = TransformType.Angle;
                            break;
                        case "origin":
                            type = TransformType.Position;
                            break;
                        case "target_destination":
                        case "target_source":
                            type = TransformType.EntityName;
                            break;
                        case "vector":
                            // Temporary hack to fix mistake on valve's part
                            if (curName == "func_useableladder" && (name == "point0" || name == "point1")) {
                                type = TransformType.Position;
                            } else {
                                type = TransformType.Offset;
                            }
                            break;
                        case "sidelist":
                            type = TransformType.Identifier;
                            break;
                    }

                    if (!curDict.ContainsKey(name)) {
                        curDict.Add(name, type);
                    } else {
                        curDict[name] = type;
                    }
                }
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
            if (fixupStyle == TargetNameFixupStyle.None || targetName == null || name.StartsWith("@") || name.StartsWith("!"))
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
        private VMFStructure(VMFStructure clone, int idOffset, int nodeOffset, TargetNameFixupStyle fixupStyle, String targetName,
            List<KeyValuePair<String, String>> replacements, List<KeyValuePair<String, String>> matReplacements)
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
                String str = keyVal.Value.String;
                bool fixup = true;
                if (replacements != null && str.Contains("$")) {
                    fixup = false;
                    foreach (KeyValuePair<String, String> repKeyVal in replacements)
                        str = str.Replace(repKeyVal.Key, repKeyVal.Value);
                }

                KeyValuePair<String, VMFValue> kvClone;

                if (keyVal.Value is VMFVector3ArrayValue) {
                    kvClone = new KeyValuePair<string, VMFValue>(keyVal.Key, new VMFVector3ArrayValue() { String = str } );
                } else {
                    kvClone = new KeyValuePair<string, VMFValue>(keyVal.Key, VMFValue.Parse(str));
                }

                if (Type == VMFStructureType.Connections) {
                    if (fixup && fixupStyle != TargetNameFixupStyle.None && targetName != null) {
                        String[] split = kvClone.Value.String.Split(',');
                        split[0] = FixupName(split[0], fixupStyle, targetName);
                        if (stInputsDict.ContainsKey(split[1])) {
                            switch (stInputsDict[split[1]]) {
                                case TransformType.EntityName:
                                    split[2] = FixupName(split[2], fixupStyle, targetName);
                                    break;
                                // add more later
                            }
                        }
                        kvClone.Value.String = String.Join(",", split);
                    }
                } else {
                    if (Type == VMFStructureType.Side && matReplacements != null && kvClone.Key == "material") {
                        var material = kvClone.Value.String;
                        foreach (KeyValuePair<String, String> repKeyVal in matReplacements) {
                            if (material == repKeyVal.Key) {
                                ((VMFStringValue) kvClone.Value).String = repKeyVal.Value;
                                break;
                            }
                        }
                    } else if (kvClone.Key == "groupid") {
                        ((VMFNumberValue) kvClone.Value).Value += idOffset;
                    } else if (kvClone.Key == "nodeid") {
                        ((VMFNumberValue) kvClone.Value).Value += nodeOffset;
                    } else if (Type == VMFStructureType.Entity) {
                        TransformType trans = entDict.ContainsKey(kvClone.Key) ? entDict[kvClone.Key] : TransformType.None;

                        if (trans == TransformType.Identifier) {
                            kvClone.Value.OffsetIdentifiers(idOffset);
                        } else if (fixup && (kvClone.Key == "targetname" || trans == TransformType.EntityName) && fixupStyle != TargetNameFixupStyle.None && targetName != null) {
                            ((VMFStringValue) kvClone.Value).String = FixupName(((VMFStringValue) kvClone.Value).String, fixupStyle, targetName);
                        }
                    }
                }

                Properties.Add(kvClone);
            }

            foreach (VMFStructure structure in clone.Structures)
                Structures.Add(new VMFStructure(structure, idOffset, nodeOffset, fixupStyle, targetName, replacements, matReplacements));

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

                    KeyValuePair<String, VMFValue> keyVal;
                    
                    if (Type == VMFStructureType.Normals || Type == VMFStructureType.Offsets || Type == VMFStructureType.Offset_Normals) {
                        keyVal = new KeyValuePair<string,VMFValue>(pair[0], new VMFVector3ArrayValue() { String = pair[1] });
                    } else {
                        keyVal = new KeyValuePair<string,VMFValue>(pair[0], VMFValue.Parse(pair[1]));
                    }

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

        public VMFStructure Clone(int idOffset = 0, int nodeOffset = 0, TargetNameFixupStyle fixupStyle = TargetNameFixupStyle.None,
            String targetName = null, List<KeyValuePair<String, String>> replacements = null, List<KeyValuePair<String, String>> matReplacements = null)
        {
            return new VMFStructure(this, idOffset, nodeOffset, fixupStyle, targetName, replacements, matReplacements);
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

                    if (transDict != null) {
                        foreach (String key in transDict.Keys) {
                            if (Regex.IsMatch(keyVal.Key, key)) {
                                trans = transDict[key];
                            }
                        }
                    }

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

        public int GetLastNodeID()
        {
            int max = ContainsKey("nodeid") ? (int) ((VMFNumberValue) this["nodeid"]).Value : 0;

            foreach (VMFStructure structure in Structures)
                max = Math.Max(structure.GetLastNodeID(), max);

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
