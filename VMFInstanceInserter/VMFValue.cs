using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Globalization;

namespace VMFInstanceInserter
{
    public abstract class VMFValue
    {
        protected static readonly CultureInfo CultureInfo = CultureInfo.GetCultureInfo("en-US");

        private static List<Tuple<ConstructorInfo, Regex, int>> stTypes;

        static VMFValue()
        {
            stTypes = new List<Tuple<ConstructorInfo, Regex, int>>();

            foreach (Type type in Assembly.GetExecutingAssembly().GetTypes()) {
                if (type.BaseType == typeof(VMFValue)) {
                    FieldInfo patternp = type.GetField("Pattern", BindingFlags.Public | BindingFlags.Static);
                    FieldInfo orderp = type.GetField("Order", BindingFlags.Public | BindingFlags.Static);

                    String pattern = (String) patternp.GetValue(null);
                    int order = (int) orderp.GetValue(null);

                    int i = 0;
                    while (i < stTypes.Count && order >= stTypes[i].Item3)
                        ++i;

                    ConstructorInfo cons = type.GetConstructor(new Type[0]);
                    Regex regex = new Regex("^" + pattern + "$");

                    if (cons != null) {
                        stTypes.Insert(i, new Tuple<ConstructorInfo, Regex, int>(cons, regex, order));
                    } else {
                        Console.WriteLine("Could not find parse constructor for type \"" + type.FullName + "\"!");
                    }
                }
            }
        }

        public static VMFValue Parse(String str)
        {
            foreach (Tuple<ConstructorInfo, Regex, int> type in stTypes) {
                if (type.Item2.IsMatch(str)) {
                    VMFValue val = (VMFValue) type.Item1.Invoke(new object[0]);
                    try {
                        val.String = str;
                    } catch {
                        Console.WriteLine("Error while parsing \"" + str + "\"!");
                    }
                    return val;
                }
            }

            return new VMFStringValue { String = str };
        }

        public abstract String String { get; set; }

        public virtual VMFValue Clone()
        {
            return Parse(String);
        }

        public virtual void Offset(VMFVector3Value vector)
        {
            return;
        }

        public virtual void Rotate(VMFVector3Value angles)
        {
            return;
        }

        public virtual void AddAngles(VMFVector3Value angles)
        {
            return;
        }

        public virtual void OffsetIdentifiers(int offset)
        {
            return;
        }

        public override string ToString()
        {
            return String;
        }
    }

    public class VMFStringValue : VMFValue
    {
        public static readonly String Pattern = ".*";
        public static readonly int Order = int.MaxValue;

        private String myString;

        public override string String
        {
            get { return myString; }
            set { myString = value; }
        }

        public override VMFValue Clone()
        {
            return new VMFStringValue { String = this.String };
        }
    }

    public class VMFNumberValue : VMFValue
    {
        public static readonly String Pattern = "-?[0-9]+(\\.[0-9]+)?(e-?[0-9]+)?";
        public static readonly int Order = 5;

        public double Value { get; set; }

        public override string String
        {
            get { return Value.ToString(CultureInfo); }
            set { Value = double.Parse(value, CultureInfo); }
        }

        public override VMFValue Clone()
        {
            return new VMFNumberValue { Value = this.Value };
        }

        public override void OffsetIdentifiers(int offset)
        {
            Value += offset;
        }
    }

    public class VMFVector2Value : VMFValue
    {
        public static readonly String Pattern = "\\[?" + VMFNumberValue.Pattern + " " + VMFNumberValue.Pattern + "\\]?";
        public static readonly int Order = 4;

        private bool myInSqBracs;

        public double X { get; set; }
        public double Y { get; set; }

        public override string String
        {
            get { return (myInSqBracs ? "[" : "") + X.ToString(CultureInfo) + " " + Y.ToString(CultureInfo) + (myInSqBracs ? "]" : ""); }
            set
            {
                myInSqBracs = value.StartsWith("[");

                String[] vals = value.Trim('[', ']').Split(' ');

                double x = 0, y = 0;

                if (vals.Length >= 1)
                    double.TryParse(vals[0], NumberStyles.Number, CultureInfo, out x);
                if (vals.Length >= 2)
                    double.TryParse(vals[1], NumberStyles.Number, CultureInfo, out y);

                X = x;
                Y = y;
            }
        }

        public override VMFValue Clone()
        {
            return new VMFVector2Value { X = this.X, Y = this.Y, myInSqBracs = this.myInSqBracs };
        }

        public override void OffsetIdentifiers(int offset)
        {
            X += offset;
            Y += offset;
        }
    }

    public class VMFVector3Value : VMFValue
    {
        public static readonly String Pattern = "\\[?" + VMFNumberValue.Pattern + " " + VMFNumberValue.Pattern + " " + VMFNumberValue.Pattern + "\\]?";
        public static readonly int Order = 3;

        private bool myInSqBracs;
        private double[,] myRotationMatrix;

        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public double Pitch
        {
            get { return X; }
            set { X = value; }
        }
        public double Yaw
        {
            get { return Y; }
            set { Y = value; }
        }
        public double Roll
        {
            get { return Z; }
            set { Z = value; }
        }

        public double[,] RotationMatrix
        {
            get
            {
                if (myRotationMatrix == null)
                    GenerateRotationMatrix();

                return myRotationMatrix;
            }
        }

        public override string String
        {
            get { return (myInSqBracs ? "[" : "") + X.ToString(CultureInfo) + " " + Y.ToString(CultureInfo) + " " + Z.ToString(CultureInfo) + (myInSqBracs ? "]" : ""); }
            set
            {
                myInSqBracs = value.StartsWith("[");

                String[] vals = value.Trim('[', ']').Split(' ');

                double x = 0, y = 0, z = 0;

                if (vals.Length >= 1)
                    double.TryParse(vals[0], NumberStyles.Number, CultureInfo, out x);
                if (vals.Length >= 2)
                    double.TryParse(vals[1], NumberStyles.Number, CultureInfo, out y);
                if (vals.Length >= 3)
                    double.TryParse(vals[2], NumberStyles.Number, CultureInfo, out z);

                X = x;
                Y = y;
                Z = z;
            }
        }

        public override VMFValue Clone()
        {
            return new VMFVector3Value { X = this.X, Y = this.Y, Z = this.Z, myInSqBracs = this.myInSqBracs };
        }

        private void GenerateRotationMatrix()
        {
            double cosA, sinA, cosB, sinB, cosC, sinC;

            GetCosAndSin(360 - Yaw, out cosA, out sinA);
            GetCosAndSin(Pitch, out cosB, out sinB);
            GetCosAndSin(Roll, out cosC, out sinC);

            myRotationMatrix = new double[,]
            {
                { cosA * cosB, cosA * sinB * sinC - sinA * cosC, cosA * sinB * cosC + sinA * sinC },
                { sinA * cosB, sinA * sinB * sinC + cosA * cosC, sinA * sinB * cosC - cosA * sinC },
                { -sinB, sinB * sinC, cosB * cosC }
            };
        }

        private void GetCosAndSin(double angle, out double cos, out double sin)
        {
            angle -= Math.Floor(angle / 360.0) * 360.0;
            if (angle == Math.Round(angle)) {
                switch ((int) angle) {
                    case 0:
                        cos = 1; sin = 0; return;
                    case 90:
                        cos = 0; sin = 1; return;
                    case 180:
                        cos = -1; sin = 0; return;
                    case 270:
                        cos = 0; sin = -1; return;
                }
            }

            angle = angle * Math.PI / 180;

            cos = Math.Cos(angle); sin = Math.Sin(angle);
            return;
        }

        public double Dot(VMFVector3Value vector)
        {
            return this.X * vector.X + this.Y * vector.Y + this.Z * vector.Z;
        }

        public override void Offset(VMFVector3Value vector)
        {
            X += vector.X;
            Y += vector.Y;
            Z += vector.Z;
        }

        public override void Rotate(VMFVector3Value angles)
        {
            double[ , ] mat = angles.RotationMatrix;

            double yaw = Yaw, pitch = Pitch, roll = Roll;

            Yaw = yaw * mat[0, 0] + pitch * mat[0, 1] + roll * mat[0, 2];
            Pitch = yaw * mat[1, 0] + pitch * mat[1, 1] + roll * mat[1, 2];
            Roll = yaw * mat[2, 0] + pitch * mat[2, 1] + roll * mat[2, 2];
        }

        public override void AddAngles(VMFVector3Value angles)
        {
            Pitch += angles.Pitch;
            Roll += angles.Roll;
            Yaw += angles.Yaw;

            Pitch -= Math.Floor(Pitch / 360.0) * 360.0;
            Roll -= Math.Floor(Roll / 360.0) * 360.0;
            Yaw -= Math.Floor(Yaw / 360.0) * 360.0;
        }

        public override void OffsetIdentifiers(int offset)
        {
            X += offset;
            Y += offset;
            Z += offset;
        }
    }

    public class VMFVector4Value : VMFValue
    {
        public static readonly String Pattern = "\\[?" + VMFNumberValue.Pattern + " " + VMFNumberValue.Pattern + " " + VMFNumberValue.Pattern + " " + VMFNumberValue.Pattern + "\\]?";
        public static readonly int Order = 2;

        private bool myInSqBracs;

        public double R { get; set; }
        public double G { get; set; }
        public double B { get; set; }
        public double A { get; set; }

        public override string String
        {
            get
            {
                return (myInSqBracs ? "[" : "") + R.ToString(CultureInfo)
                    + " " + G.ToString(CultureInfo) + " " + B.ToString(CultureInfo)
                    + " " + A.ToString(CultureInfo) + (myInSqBracs ? "]" : "");
            }
            set
            {
                myInSqBracs = value.StartsWith("[");

                String[] vals = value.Trim('[', ']').Split(' ');

                double r = 0, g = 0, b = 0, a = 0;

                if (vals.Length >= 1)
                    double.TryParse(vals[0], NumberStyles.Number, CultureInfo, out r);
                if (vals.Length >= 2)
                    double.TryParse(vals[1], NumberStyles.Number, CultureInfo, out g);
                if (vals.Length >= 3)
                    double.TryParse(vals[2], NumberStyles.Number, CultureInfo, out b);
                if (vals.Length >= 4)
                    double.TryParse(vals[3], NumberStyles.Number, CultureInfo, out a);

                R = r;
                G = g;
                B = b;
                A = a;
            }
        }

        public override VMFValue Clone()
        {
            return new VMFVector4Value { R = this.R, G = this.G, B = this.B, A = this.A, myInSqBracs = this.myInSqBracs };
        }

        public override void OffsetIdentifiers(int offset)
        {
            R += offset;
            G += offset;
            B += offset;
            A += offset;
        }
    }

    public class VMFTextureInfoValue : VMFValue
    {
        public static readonly String Pattern = "\\[" + VMFNumberValue.Pattern + " " + VMFNumberValue.Pattern + " " + VMFNumberValue.Pattern + " " + VMFNumberValue.Pattern + "\\] " + VMFNumberValue.Pattern;
        public static readonly int Order = 1;

        public VMFVector3Value Direction { get; set; }
        public double Pan { get; set; }
        public double Scale { get; set; }

        public override string String
        {
            get { return "[" + Direction.String + " " + Pan.ToString(CultureInfo) + "] " + Scale.ToString(CultureInfo); }
            set
            {
                if (Direction == null)
                    Direction = new VMFVector3Value();

                int split0 = value.IndexOf(' ');
                int split1 = value.IndexOf(' ', split0 + 1);
                int split2 = value.IndexOf(' ', split1 + 1);
                int split3 = value.IndexOf(' ', split2 + 1);

                Direction.String = value.Substring(1, split2 - 1);

                try {
                    Pan = double.Parse(value.Substring(split2, split3 - split2 - 1), CultureInfo);
                    Scale = double.Parse(value.Substring(split3), CultureInfo);
                } catch {
                    Console.WriteLine(": --");
                    Console.WriteLine(": " + value);
                    Console.WriteLine(": " + split0 + ", " + split1 + ", " + split2 + ", " + split3);
                    Console.WriteLine(": " + Direction.String);
                    Console.WriteLine(value.Substring(split2 + 1, split3 - split2 - 2));
                    Console.WriteLine(value.Substring(split3 + 1));
                }
            }
        }

        public override void Offset(VMFVector3Value vector)
        {
            Pan -= Direction.Dot(vector) / Scale;
        }

        public override void Rotate(VMFVector3Value angles)
        {
            Direction.Rotate(angles);
        }

        public override VMFValue Clone()
        {
            return new VMFTextureInfoValue { Direction = (VMFVector3Value) this.Direction.Clone(), Pan = this.Pan, Scale = this.Scale };
        }
    }

    public class VMFVector3ArrayValue : VMFValue
    {
        public static readonly String Pattern = "\\(" + VMFVector3Value.Pattern + "\\)( \\(" + VMFVector3Value.Pattern + "\\))*";
        public static readonly int Order = 0;

        public VMFVector3Value[] Vectors { get; set; }

        public override string String
        {
            get
            {
                if (Vectors.Length == 0)
                    return "";

                String str = "(" + Vectors[0].String + ")";
                for (int i = 1; i < Vectors.Length; ++i)
                    str += " (" + Vectors[i].String + ")";

                return str;
            }
            set
            {
                String[] vects = value.Trim('(', ')').Split(new String[] { ") (" }, StringSplitOptions.None);

                Vectors = new VMFVector3Value[vects.Length];
                for (int i = 0; i < vects.Length; ++i) {
                    Vectors[i] = new VMFVector3Value();
                    Vectors[i].String = vects[i];
                }
            }
        }

        public override VMFValue Clone()
        {
            VMFVector3ArrayValue arr = new VMFVector3ArrayValue();
            arr.Vectors = new VMFVector3Value[Vectors.Length];
            for (int i = 0; i < Vectors.Length; ++i)
                arr.Vectors[i] = (VMFVector3Value) Vectors[i].Clone();
            return arr;
        }

        public override void Offset(VMFVector3Value vector)
        {
            foreach (VMFVector3Value vec in Vectors)
                vec.Offset(vector);
        }

        public override void AddAngles(VMFVector3Value angles)
        {
            foreach (VMFVector3Value vec in Vectors)
                vec.AddAngles(angles);
        }

        public override void Rotate(VMFVector3Value angles)
        {
            foreach (VMFVector3Value vec in Vectors)
                vec.Rotate(angles);
        }
    }

    public class VMFIdentifierListValue : VMFValue
    {
        public static readonly String Pattern = "([0-9]+( [0-9]+)*)?";
        public static readonly int Order = 6;

        public int[] IDs { get; set; }

        public override string String
        {
            get { return String.Join(" ", IDs); }
            set
            {
                String[] split = value.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                IDs = new int[split.Length];
                for (int i = IDs.Length - 1; i >= 0; --i)
                    IDs[i] = Int32.Parse(split[i]);
            }
        }

        public override VMFValue Clone()
        {
            return new VMFIdentifierListValue{ String = String };
        }

        public override void OffsetIdentifiers(int offset)
        {
            for (int i = IDs.Length - 1; i >= 0; --i)
                IDs[i] += offset;
        }

        public VMFIdentifierListValue()
        {
            IDs = new int[0];
        }

        public VMFIdentifierListValue(params int[] ids)
        {
            IDs = ids;
        }
    }
}
