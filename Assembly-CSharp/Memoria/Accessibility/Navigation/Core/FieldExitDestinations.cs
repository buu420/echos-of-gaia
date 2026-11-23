using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.Navigation
{
    // A destination belongs only to the surface points whose native path
    // proves it. A shared region may have different upstairs/downstairs exits.
    public static class FieldExitDestinations
    {
        internal static bool ReviewedEntryFacing(int field,int sid,int uid,string hash)
        {
            if(sid!=uid)return false;
            // The Treno tower trigger is supported on the incoming side of its
            // first edge (installed 905 BGI triangles 8 and 40).
            if(field==905 && sid==10 && hash=="f76d392a68ed0d29ceb0e47f6a2e332b36c81cc6c31662a05716818ea4a75fe8")return true;
            if(RequiresInwardApproach(field,sid,uid,hash))return true;
            return sid==2 && (field==2916 && hash=="68a447aa8fe33dcc3fb1ec101dcba492c7ec85da77c51b59e75ee2cd48b17485" ||
                field==2918 && hash=="48607f555f8dde5c5544e743ff1fbd8c2df86e1630ca9a7e6a45d54070d8ded3");
        }


        // The original Lindblum door scripts expect a normal approach toward the first QUAD edge.
        internal static bool RequiresInwardApproach(int field,int sid,int uid,string hash)
        {
            if(sid!=uid)return false;
            switch(field) {
            case 552: return sid==21 && hash=="e53488fc28a67e690652ac29743782e2f3c60833414417a78efd8e9be46b71ca";
            case 555: return sid==20 && hash=="a3daba2e9f09ccf1cb8d3661e09046915f5aa5938d307fea212971d573f0fc63";
            case 556: return sid==19 && hash=="abadf9abb6d5dbd5bf18fde3b3633f2c46add0ebfdc380c0e7b556ac0ad0bd50";
            case 570: return sid==24 && hash=="d5a671438d494f979fc3b35a22bb09246fc30657f430b30ea4cbdc95f3699d97";
            case 1302: return sid==12 && hash=="cc9171c8d4949cb580c3daad8b8cc7e8aad2d370cc6ad0866ee6580942af4c44";
            case 1305: return sid==14 && hash=="4b1bf936225fb3dd9b21af833e9643538bba38383d00fa307474664d69b3bf60";
            case 2102: return sid==9 && hash=="b5bc9cb45d89eeed1d2c50168644510372700fc934335b89effe3bfa2d8e7b47";
            case 2105: return sid==16 && hash=="bb5c9c5b8e705e94c7d882223c95382a2e5547af1487a16626734865d7d9198f";
            case 2170: return sid==7 && hash=="24c2f539b0f08c00d957cefd8fcd65509016f4dadd82c387c45a251bc49239ac";
            default: return false;
            }
        }

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
