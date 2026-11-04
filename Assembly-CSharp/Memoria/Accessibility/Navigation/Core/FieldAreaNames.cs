using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.Navigation
{
    public sealed class FieldAreaName
    {
        public int Field;
        public string Scene,Name;
    }
    public static class FieldAreaNames
    {
        public static Dictionary<int,string> Build(IList<FieldAreaName> source)
        {
            var result=new Dictionary<int,string>();
            if(source==null)return result;
            var ordered=new List<FieldAreaName>();
            foreach(var entry in source)if(entry!=null && !String.IsNullOrEmpty(entry.Name))ordered.Add(entry);
            ordered.Sort((a,b)=>a.Field.CompareTo(b.Field));
            var groups=new Dictionary<string,List<string>>(StringComparer.Ordinal);
            foreach(var entry in ordered) {
                string label=Clean(entry.Name);if(label.Length==0 || String.IsNullOrEmpty(entry.Scene))continue;
                List<string> scenes;if(!groups.TryGetValue(label,out scenes)){scenes=new List<string>();groups[label]=scenes;}
                if(!scenes.Contains(entry.Scene))scenes.Add(entry.Scene);
            }
            foreach(var entry in ordered) {
                string label=Clean(entry.Name);if(label.Length==0)continue;
                List<string> scenes;
                if(!String.IsNullOrEmpty(entry.Scene) && groups.TryGetValue(label,out scenes) && scenes.Count>1)
                    label+=", Area "+Letters(scenes.IndexOf(entry.Scene));
                result[entry.Field]=label;
            }
            return result;
        }
        private static string Clean(string name)
        {
            var parts=new List<string>();foreach(string part in name.Split('/'))if(part.Trim().Length>0)parts.Add(part.Trim());
            return String.Join(", ",parts.ToArray());
        }
        private static string Letters(int value)
        {string text="";do{text=(char)('A'+value%26)+text;value=value/26-1;}while(value>=0);return text;}
    }
}
