using System;
using System.Globalization;
using System.Reflection;
using System.Text;
namespace Memoria.Accessibility.Navigation
{
    // Serialize detached navigation values without framework dependencies absent from Unity's Mono.
    public static class FieldRouteDiagnostics
    {
        public static string Serialize(NavigationContext context,NavigationTarget target,FieldGeometrySnapshot geometry,RoutePlan route)
        {
            return SerializeWithNativeCapture(context,target,geometry,route,null);
        }
        public static string SerializeWithNativeCapture(NavigationContext context,NavigationTarget target,FieldGeometrySnapshot geometry,RoutePlan route,FieldNativeCapture native)
        {
            var text=new StringBuilder("{\"Schema\":1,\"Context\":");Write(text,context,0);
            text.Append(",\"Target\":");Write(text,target,0);text.Append(",\"Geometry\":");Write(text,geometry,0);
            text.Append(",\"Route\":");Write(text,route,0);
            if(native!=null){text.Append(",\"Native\":");Write(text,native,0);}return text.Append('}').ToString();
        }
        private static void Write(StringBuilder text,object value,int depth)
        {
            if(depth>16)throw new InvalidOperationException("Field snapshot exceeds the detached value depth.");
            if(value==null){text.Append("null");return;}
            if(value is string){Quote(text,(string)value);return;}
            if(value is bool){text.Append((bool)value?"true":"false");return;}
            var type=value.GetType();
            if(type.IsEnum){text.Append(Convert.ToInt64(value,CultureInfo.InvariantCulture));return;}
            if(value is float || value is double) {
                double number=Convert.ToDouble(value,CultureInfo.InvariantCulture);
                if(double.IsNaN(number)||double.IsInfinity(number))throw new InvalidOperationException("Field snapshot contains a nonfinite coordinate.");
                text.Append(value is float?((float)value).ToString("R",CultureInfo.InvariantCulture):number.ToString("R",CultureInfo.InvariantCulture));return;
            }
            if(type.IsPrimitive || value is decimal){text.Append(Convert.ToString(value,CultureInfo.InvariantCulture));return;}
            var array=value as Array;
            if(array!=null) {text.Append('[');for(int i=0;i<array.Length;i++){if(i>0)text.Append(',');Write(text,array.GetValue(i),depth+1);}text.Append(']');return;}
            if(type.Namespace!=typeof(NavigationContext).Namespace)throw new InvalidOperationException("Field snapshot must contain navigation values only.");
            var fields=type.GetFields(BindingFlags.Public|BindingFlags.Instance);
            Array.Sort(fields,delegate(FieldInfo a,FieldInfo b){return String.CompareOrdinal(a.Name,b.Name);});
            text.Append('{');for(int i=0;i<fields.Length;i++){if(i>0)text.Append(',');Quote(text,fields[i].Name);text.Append(':');Write(text,fields[i].GetValue(value),depth+1);}text.Append('}');
        }
        private static void Quote(StringBuilder text,string value)
        {
            text.Append('"');foreach(char c in value) {
                if(c=='"'||c=='\\'){text.Append('\\').Append(c);}
                else if(c<32){text.Append("\\u").Append(((int)c).ToString("x4",CultureInfo.InvariantCulture));}
                else text.Append(c);
            }text.Append('"');
        }
    }
}
