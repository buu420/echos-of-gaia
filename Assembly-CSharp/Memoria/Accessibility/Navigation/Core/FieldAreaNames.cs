using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.Navigation
{
    public sealed class FieldAreaName
    {
        public int Field;
        public string Scene,Name;
    }
    // Physical place of a native background scene (FieldPhysicalAreas.Generated.cs). Story and disc
    // variants of one place share its canonical scene; anything unknown is its own place.
    public static partial class FieldPhysicalAreas
    {
        public static string Of(string scene)
        {string canonical;return scene!=null && _aliases.TryGetValue(scene,out canonical)?canonical:scene;}
    }
    // A repeated native location name gets one stable number per physical place, in native field
    // order (the first field showing that place), e.g. "Ice Cavern, Ice Path, Area 2".
    public static class FieldAreaNames
    {
        // Iifa's two spiral scripts reuse one field ID for several physical
        // screens. Both directions into a screen must produce the same name.
        // These are the director's entrance switches, not camera headings.
        public static int SpiralArea(int field,int entrance)
        {
            if(field==1651) {
                switch(entrance) {case 6:case 9:return 2;case 7:case 11:return 3;case 10:case 13:return 4;default:return 1;}
            }
            if(field==1758) {
                switch(entrance) {case 16:case 19:return 2;case 17:case 21:return 3;case 20:case 23:return 4;case 12:case 22:return 5;default:return 1;}
            }
            return 0;
        }
        public static string SpiralName(int field,int entrance,string nativeName)
        {
            int area=SpiralArea(field,entrance);
            if(area==0 || String.IsNullOrEmpty(nativeName))return null;
            string name=Clean(nativeName);
            return name.Length==0?null:name+", Spiral area "+area;
        }
        // Installed Pandemonium warp maze BGI. One field and camera can
        // show several levels; only the player's supporting platform counts.
        private const string WarpMazeMesh="13f9aed7dda2b59aadc528212f8428213c0b9f5e829dc84790d1abeaac827abd";
        private static readonly byte[] _warpMazeAreas={
            1,1,1,1,2,2,2,2,3,3,3,3,4,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,5,5,
            4,4,3,3,5,5,5,5,6,6,6,6,6,6,6,6,6,6,5,5,6,6,5,5,5,5,5,5,5,5,5,5,
            7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,8,8,8,8,3,3,3,3,3,
            3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,5,3,3,3,3,7,7,8,8,8,8,4,
            4,6,6,6,6,5,5,1,1,9,9,9,9,2,2,2,2,2,2,3,3,9,9,4,9,9,4,4,4,4,3,3,
            3,3,3,3,9,9,9,3,3,4,4
        };
        public static int WarpMazeArea(int field,string walkmesh,int support)
        {return field==2714 && walkmesh==WarpMazeMesh && support>=0 && support<_warpMazeAreas.Length?_warpMazeAreas[support]:0;}
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
                string place=FieldPhysicalAreas.Of(entry.Scene);if(!scenes.Contains(place))scenes.Add(place);
            }
            foreach(var entry in ordered) {
                string label=Clean(entry.Name);if(label.Length==0)continue;
                List<string> scenes;
                if(!String.IsNullOrEmpty(entry.Scene) && groups.TryGetValue(label,out scenes) && scenes.Count>1)
                    label+=", Area "+(scenes.IndexOf(FieldPhysicalAreas.Of(entry.Scene))+1);
                result[entry.Field]=label;
            }
            return result;
        }
        private static string Clean(string name)
        {
            var parts=new List<string>();foreach(string part in name.Split('/'))if(part.Trim().Length>0)parts.Add(part.Trim());
            return String.Join(", ",parts.ToArray());
        }
    }
}
