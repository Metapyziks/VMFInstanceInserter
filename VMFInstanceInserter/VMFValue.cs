using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Text.RegularExpressions;

namespace VMFInstanceInserter
{
    public abstract class VMFValue
    {
        private static List<Tuple<ConstructorInfo, Regex, int>> stTypes;

        static VMFValue()
        {
            stTypes = new List<Tuple<ConstructorInfo, Regex, int>>();

            foreach ( Type type in Assembly.GetExecutingAssembly().GetTypes() )
            {
                if ( type.BaseType == typeof( VMFValue ) )
                {
                    FieldInfo patternp = type.GetField( "Pattern", BindingFlags.Public | BindingFlags.Static );
                    FieldInfo orderp = type.GetField( "Order", BindingFlags.Public | BindingFlags.Static );

                    String pattern = (String) patternp.GetValue( null );
                    int order = (int) orderp.GetValue( null );

                    int i = 0;
                    while ( i < stTypes.Count && order >= stTypes[ i ].Item3 )
                        ++i;

                    stTypes.Insert( i, new Tuple<ConstructorInfo, Regex, int>( type.GetConstructor( new Type[ 0 ] ), new Regex( "^" + pattern + "$" ), order ) );
                }
            }
        }

        public static VMFValue Parse( String str )
        {
            foreach ( Tuple<ConstructorInfo, Regex, int> type in stTypes )
            {
                if ( type.Item2.IsMatch( str ) )
                {
                    VMFValue val = (VMFValue) type.Item1.Invoke( new object[ 0 ] );
                    val.String = str;
                    return val;
                }
            }

            return new VMFStringValue { String = str };
        }

        public abstract String String { get; set; }

        public override string ToString()
        {
            return String;
        }
    }

    public class VMFStringValue : VMFValue
    {
        public static readonly String Pattern = ".*";
        public static readonly int Order = 6;

        private String myString;

        public override string String
        {
            get { return myString; }
            set { myString = value; }
        }
    }

    public class VMFNumberValue : VMFValue
    {
        public static readonly String Pattern = "-?[0-9]+(\\.[0-9]+)?";
        public static readonly int Order = 5;

        public double Value { get; set; }

        public override string String
        {
            get { return Value.ToString(); }
            set { Value = double.Parse( value ); }
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
            get { return ( myInSqBracs ? "[" : "" ) + X.ToString() + " " + Y.ToString() + ( myInSqBracs ? "]" : "" ); }
            set
            {
                myInSqBracs = value.StartsWith( "[" );

                String[] vals = value.Trim( '[', ']' ).Split( ' ' );

                double x = 0, y = 0;

                if ( vals.Length >= 1 )
                    double.TryParse( vals[ 0 ], out x );
                if ( vals.Length >= 2 )
                    double.TryParse( vals[ 1 ], out y );

                X = x;
                Y = y;
            }
        }
    }

    public class VMFVector3Value : VMFValue
    {
        public static readonly String Pattern = "\\[?" + VMFNumberValue.Pattern + " " + VMFNumberValue.Pattern + " " + VMFNumberValue.Pattern + "\\]?";
        public static readonly int Order = 3;

        private bool myInSqBracs;

        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public override string String
        {
            get { return ( myInSqBracs ? "[" : "" ) + X.ToString() + " " + Y.ToString() + " " + Z.ToString() + ( myInSqBracs ? "]" : "" ); }
            set
            {
                myInSqBracs = value.StartsWith( "[" );

                String[] vals = value.Trim( '[', ']' ).Split( ' ' );

                double x = 0, y = 0, z = 0;

                if ( vals.Length >= 1 )
                    double.TryParse( vals[ 0 ], out x );
                if ( vals.Length >= 2 )
                    double.TryParse( vals[ 1 ], out y );
                if ( vals.Length >= 3 )
                    double.TryParse( vals[ 2 ], out z );

                X = x;
                Y = y;
                Z = z;
            }
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
            get { return ( myInSqBracs ? "[" : "" ) + R.ToString() + " " + G.ToString() + " " + B.ToString() + " " + A.ToString() + ( myInSqBracs ? "]" : "" ); }
            set
            {
                myInSqBracs = value.StartsWith( "[" );

                String[] vals = value.Trim( '[', ']' ).Split( ' ' );

                double r = 0, g = 0, b = 0, a = 0;

                if ( vals.Length >= 1 )
                    double.TryParse( vals[ 0 ], out r );
                if ( vals.Length >= 2 )
                    double.TryParse( vals[ 1 ], out g );
                if ( vals.Length >= 3 )
                    double.TryParse( vals[ 2 ], out b );
                if ( vals.Length >= 4 )
                    double.TryParse( vals[ 3 ], out a );

                R = r;
                G = g;
                B = b;
                A = a;
            }
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
            get { return "[" + Direction.String + " " + Pan.ToString() + "] " + Scale.ToString(); }
            set
            {
                if( Direction == null )
                    Direction = new VMFVector3Value();

                int split0 = value.IndexOf( ' ' );
                int split1 = value.IndexOf( ' ', split0 + 1 );
                int split2 = value.IndexOf( ' ', split1 + 1 );
                int split3 = value.IndexOf( ' ', split2 + 1 );

                Direction.String = value.Substring( 1, split2 - 1 );

                Pan = double.Parse( value.Substring( split2, split3 - split2 - 1 ) );
                Scale = double.Parse( value.Substring( split3 ) );
            }
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
                if ( Vectors.Length == 0 )
                    return "";

                String str = "(" + Vectors[ 0 ].String + ")";
                for ( int i = 1; i < Vectors.Length; ++i )
                    str += " (" + Vectors[ i ].String + ")";

                return str;
            }
            set
            {
                String[] vects = value.Trim( '(', ')' ).Split( new String[]{ ") (" }, StringSplitOptions.None );

                Vectors = new VMFVector3Value[ vects.Length ];
                for ( int i = 0; i < vects.Length; ++i )
                {
                    Vectors[ i ] = new VMFVector3Value();
                    Vectors[ i ].String = vects[ i ];
                }
            }
        }
    }
}
