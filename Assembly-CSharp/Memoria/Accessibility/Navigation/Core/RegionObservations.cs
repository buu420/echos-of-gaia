using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
namespace Memoria.Accessibility.Navigation
{
    public sealed class RegionObservations
    {
        private sealed class Region { public string Map,Label;public int Id;public readonly List<NavigationPoint> Points=new List<NavigationPoint>(); }
        private readonly Dictionary<string,Region> _regions=new Dictionary<string,Region>();private readonly string _directory;private string _save;
        public RegionObservations(string directory) { _directory=directory; }
        public static bool ShouldDisplay(int region,int topography) { return region!=0 || topography==0 || topography==37; }
        public void SwitchSave(string identity)
        {
            _regions.Clear();_save=identity;if(string.IsNullOrEmpty(identity) || string.IsNullOrEmpty(_directory))return;
            string path=PathFor(identity);if(!File.Exists(path))return;
            try { foreach(string line in File.ReadAllLines(path)) {
                string[] values=line.Split('\t');if(values.Length!=7)throw new FormatException("Region observation record");
                string map=Decode(values[0]),label=Decode(values[2]);int id=int.Parse(values[1],CultureInfo.InvariantCulture);string key=map+":"+id;
                Region region;if(!_regions.TryGetValue(key,out region))_regions[key]=region=new Region{Map=map,Label=label,Id=id};
                var point=new NavigationPoint(float.Parse(values[3],CultureInfo.InvariantCulture),float.Parse(values[4],CultureInfo.InvariantCulture),float.Parse(values[5],CultureInfo.InvariantCulture));
                if(point.IsFinite && region.Points.Count<16)region.Points.Add(point);
            } } catch(IOException) {_regions.Clear();} catch(FormatException) {_regions.Clear();} catch(ArgumentException) {_regions.Clear();}
        }
        public void Observe(string saveIdentity,string mapKey,int nativeRegionId,string nativeLabel,NavigationPoint position,TransportIdentity transport)
        {
            if(saveIdentity!=_save)SwitchSave(saveIdentity);
            if(string.IsNullOrEmpty(mapKey) || string.IsNullOrEmpty(nativeLabel) || nativeRegionId<0 || nativeRegionId>=64 || !position.IsFinite)return;
            position=WorldCoordinates.Canonical(position);string key=mapKey+":"+nativeRegionId;
            Region region;if(!_regions.TryGetValue(key,out region))_regions[key]=region=new Region{Map=mapKey,Label=nativeLabel,Id=nativeRegionId};
            region.Label=nativeLabel;
            foreach(var point in region.Points)if(WorldCoordinates.Distance(point,position)<32)return;
            if(region.Points.Count==16)region.Points.RemoveAt(0);region.Points.Add(position);
        }
        public IList<NavigationTarget> BuildTargets(NavigationContext context)
        {
            var result=new List<NavigationTarget>();foreach(var region in _regions.Values) {
                if(region.Map!=context.LogicalMapKey || region.Points.Count==0)continue;
                NavigationPoint nearest=region.Points[0];foreach(var point in region.Points)if(WorldCoordinates.Distance(context.Position,point)<WorldCoordinates.Distance(context.Position,nearest))nearest=point;
                result.Add(new NavigationTarget {Id=new TargetIdentity(TargetKind.RegionPoint,region.Map,region.Id),Category=NavigationCategory.Regions,Label=region.Label+", previously visited point",Position=nearest,ApproachCandidates=region.Points.ToArray(),Evidence=VisibilityEvidence.ObservedRegion,Generation=context.Generation});
            }result.Sort((a,b)=>a.Id.NativeId.CompareTo(b.Id.NativeId));return result;
        }
        public void CommitSave(string identity)
        {
            _save=identity;if(string.IsNullOrEmpty(identity) || string.IsNullOrEmpty(_directory))return;
            Directory.CreateDirectory(_directory);var lines=new List<string>();
            foreach(var region in _regions.Values)foreach(var point in region.Points)lines.Add(Encode(region.Map)+"\t"+region.Id+"\t"+Encode(region.Label)+"\t"+point.X.ToString("R",CultureInfo.InvariantCulture)+"\t"+point.Y.ToString("R",CultureInfo.InvariantCulture)+"\t"+point.Z.ToString("R",CultureInfo.InvariantCulture)+"\t1");
            string path=PathFor(identity),temporary=path+".tmp";File.WriteAllLines(temporary,lines.ToArray());if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
        }
        private string PathFor(string identity) { using(var sha=SHA256.Create())return Path.Combine(_directory,BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(identity))).Replace("-","").ToLowerInvariant()+".regions"); }
        private static string Encode(string value) { return Convert.ToBase64String(Encoding.UTF8.GetBytes(value)); }private static string Decode(string value) { return Encoding.UTF8.GetString(Convert.FromBase64String(value)); }
    }
}
