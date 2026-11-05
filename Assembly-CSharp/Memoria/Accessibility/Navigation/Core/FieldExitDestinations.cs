using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.Navigation
{
    // A destination belongs only to the surface points whose native path
    // proves it. A shared region may have different upstairs/downstairs exits.
    public static class FieldExitDestinations
    {
        // Native MJPOS projects the player onto the first edge of the region,
        // with an 8-bit fixed point fraction and separately truncated products.
        public static NavigationPoint JumpPoint(NavigationPoint point,int ax,int az,int bx,int bz)
        {
            int dx=bx-ax,dz=bz-az,denominator=unchecked((dx*dx+dz*dz)>>8),fraction=0;
            if(denominator!=0)fraction=Math.Max(0,Math.Min(256,((int)(dx*((double)point.X-ax))+(int)(dz*((double)point.Z-az)))/denominator));
            return new NavigationPoint(((fraction*dx)>>8)+ax,point.Y,((fraction*dz)>>8)+az);
        }
        public static Dictionary<int,NavigationPoint[]> Read(NavigationPoint[] points,Func<NavigationPoint,ScriptStoryEvidence> evaluate)
        {
            var groups=new Dictionary<int,List<NavigationPoint>>();
            if(points!=null && evaluate!=null)foreach(var point in points)if(point.IsFinite) {
                var evidence=evaluate(point);if(evidence==null)continue;
                foreach(int destination in evidence.Destinations) {
                    List<NavigationPoint> list;if(!groups.TryGetValue(destination,out list)){list=new List<NavigationPoint>();groups[destination]=list;}
                    if(!list.Contains(point))list.Add(point);
                }
            }
            var result=new Dictionary<int,NavigationPoint[]>();foreach(var group in groups)result[group.Key]=group.Value.ToArray();return result;
        }
        public static Dictionary<int,NavigationPoint[]> Copy(Dictionary<int,NavigationPoint[]> source)
        {
            if(source==null)return null;var result=new Dictionary<int,NavigationPoint[]>();
            foreach(var group in source)result[group.Key]=group.Value==null?new NavigationPoint[0]:(NavigationPoint[])group.Value.Clone();return result;
        }
        public static NavigationPoint[] Select(Dictionary<int,NavigationPoint[]> groups,IList<int> destinations)
        {
            var result=new List<NavigationPoint>();if(groups!=null && destinations!=null)foreach(int destination in destinations) {
                NavigationPoint[] points;if(groups.TryGetValue(destination,out points) && points!=null)foreach(var point in points)if(point.IsFinite && !result.Contains(point))result.Add(point);
            }return result.ToArray();
        }
        public static int? PlayerProperty(int controlledUid,NavigationPoint point,int uid,int property,int? facing=null)
        {
            if(!point.IsFinite || controlledUid<0 || uid!=250 && uid!=controlledUid || property<0 || property>3)return null;
            if(property==3)return facing.HasValue && facing.Value>=0 && facing.Value<=255?facing:null;
            float value=property==0?point.X:property==1?-point.Y:point.Z;
            if(value<=int.MinValue || value>=int.MaxValue)return null;
            return (int)Math.Round(value,MidpointRounding.ToEven);
        }
    }
    public sealed class FieldExitDestinationCache
    {
        private sealed class Entry {public string Script;public byte[][] Memory;public NavigationPoint[] Points;public Dictionary<int,NavigationPoint[]> Result;}
        private string _scope;
        private readonly Dictionary<int,Entry> _entries=new Dictionary<int,Entry>();
        public Dictionary<int,NavigationPoint[]> Read(string scope,int identity,string script,byte[] global,byte[] map,byte[] instance,NavigationPoint[] points,Func<NavigationPoint,ScriptStoryEvidence> evaluate)
        {
            if(_scope!=scope){_entries.Clear();_scope=scope;}
            Entry entry;var memory=new[]{global,map,instance};
            if(_entries.TryGetValue(identity,out entry) && entry.Script==script && SameMemory(entry.Memory,memory) && SamePoints(entry.Points,points))return FieldExitDestinations.Copy(entry.Result);
            var result=FieldExitDestinations.Read(points,evaluate);
            if(_entries.Count>=64)_entries.Clear();
            _entries[identity]=new Entry{Script=script,Memory=new[]{Copy(global),Copy(map),Copy(instance)},Points=points==null?null:(NavigationPoint[])points.Clone(),Result=FieldExitDestinations.Copy(result)};
            return result;
        }
        private static byte[] Copy(byte[] source){return source==null?null:(byte[])source.Clone();}
        private static bool SameMemory(byte[][] a,byte[][] b)
        {
            for(int i=0;i<a.Length;i++) {
                if(a[i]==null || b[i]==null){if(a[i]!=b[i])return false;continue;}
                if(a[i].Length!=b[i].Length)return false;for(int j=0;j<a[i].Length;j++)if(a[i][j]!=b[i][j])return false;
            }return true;
        }
        private static bool SamePoints(NavigationPoint[] a,NavigationPoint[] b)
        {if(a==null || b==null)return a==b;if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(!a[i].Equals(b[i]))return false;return true;}
    }
}
