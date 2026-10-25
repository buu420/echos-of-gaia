using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using SimpleJSON;
using Memoria.Prime;
namespace Memoria.Accessibility.Navigation
{
    public static class FieldExitEvidence
    {
        private static readonly Dictionary<string, ExitPresentation> _reviewed = Load();
        private static readonly Dictionary<string,List<BoundaryReview>> _boundaries = LoadBoundaries();
        private static readonly Dictionary<string,List<FieldExitSurfaceReview>> _surfaces=LoadSurfaces();
        public static bool TryResolve(string sceneKey, int triggerId, out ExitPresentation evidence) { return _reviewed.TryGetValue(sceneKey + ":" + triggerId, out evidence); }
        public static bool HasBoundaryReview(string sceneKey,int scriptId) {return _boundaries.ContainsKey(sceneKey+":"+scriptId);}
        public static bool HasSurfaceReview(string sceneKey,int scriptId) {return _surfaces.ContainsKey(sceneKey+":"+scriptId);}
        public static bool TrySurface(string sceneKey,int scriptId,int camera,string scriptHash,string backgroundHash,string walkmeshHash,NavigationPoint[] region,out FieldExitSurfaceReview review)
        {
            review=null;List<FieldExitSurfaceReview> records;if(!_surfaces.TryGetValue(sceneKey+":"+scriptId,out records))return false;
            foreach(var item in records)if(item.Matches(sceneKey,scriptId,camera,scriptHash,backgroundHash,walkmeshHash,region)){review=item;return true;}return false;
        }
        private static Dictionary<string,List<FieldExitSurfaceReview>> LoadSurfaces()
        {
            var result=new Dictionary<string,List<FieldExitSurfaceReview>>();
            try {
                using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Memoria.Accessibility.FieldExitEvidence.json")) {
                    if(stream==null)return result;using(var reader=new StreamReader(stream)) {
                        var records=JSON.Parse(reader.ReadToEnd())["surfaceReviewed"];
                        for(int i=0;i<records.Count;i++) {
                            var item=records[i];string key=item["sceneKey"]+":"+item["scriptId"].AsInt;List<FieldExitSurfaceReview> list;
                            if(!result.TryGetValue(key,out list)){list=new List<FieldExitSurfaceReview>();result[key]=list;}
                            var polygon=item["triggerPolygon"];var points=new NavigationPoint[polygon.Count];for(int p=0;p<points.Length;p++)points[p]=new NavigationPoint(polygon[p][0].AsFloat,0,polygon[p][1].AsFloat);
                            var overlays=item["overlayIds"];var ids=new int[overlays.Count];for(int p=0;p<ids.Length;p++)ids[p]=overlays[p].AsInt;
                            list.Add(new FieldExitSurfaceReview{Scene=item["sceneKey"],ScriptId=item["scriptId"].AsInt,Camera=item["camera"].AsInt,Script=item["sourceFingerprint"],Background=item["backgroundFingerprint"],Walkmesh=item["walkmeshFingerprint"],Cue=item["visibleCue"],Region=points,OverlayIds=ids});
                        }
                    }
                }
            }catch(Exception exception){Log.Message("[FF9Access] Reviewed trigger surface evidence unavailable: {0}",exception.Message);}
            return result;
        }
        public static bool TryBoundary(string sceneKey,int scriptId,int camera,string scriptHash,string backgroundHash,string walkmeshHash,out string cue)
        {
            cue=null;List<BoundaryReview> records;if(!_boundaries.TryGetValue(sceneKey+":"+scriptId,out records))return false;
            foreach(var item in records)if(item.Camera==camera && item.Script==scriptHash && item.Background==backgroundHash && item.Walkmesh==walkmeshHash){cue=item.Cue;return true;}
            return false;
        }
        private sealed class BoundaryReview {public int Camera;public string Script,Background,Walkmesh,Cue;}
        private static Dictionary<string,List<BoundaryReview>> LoadBoundaries()
        {
            var result=new Dictionary<string,List<BoundaryReview>>();
            try {
                using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Memoria.Accessibility.FieldExitEvidence.json")) {
                    if(stream==null)return result;using(var reader=new StreamReader(stream)) {
                        var records=JSON.Parse(reader.ReadToEnd())["boundaryReviewed"];
                        for(int i=0;i<records.Count;i++) {
                            var item=records[i];string key=item["sceneKey"]+":"+item["scriptId"].AsInt;List<BoundaryReview> list;
                            if(!result.TryGetValue(key,out list)){list=new List<BoundaryReview>();result[key]=list;}
                            list.Add(new BoundaryReview{Camera=item["camera"].AsInt,Script=item["sourceFingerprint"],Background=item["backgroundFingerprint"],Walkmesh=item["walkmeshFingerprint"],Cue=item["visibleCue"]});
                        }
                    }
                }
            }catch(Exception exception){Log.Message("[FF9Access] Reviewed boundary evidence unavailable: {0}",exception.Message);}
            return result;
        }
        private static Dictionary<string, ExitPresentation> Load()
        {
            var result = new Dictionary<string, ExitPresentation>();
            try {
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Memoria.Accessibility.FieldExitEvidence.json")) {
                    if (stream == null) return result;
                    using (var reader = new StreamReader(stream)) {
                        var records = JSON.Parse(reader.ReadToEnd())["reviewed"];
                        for (int i = 0; i < records.Count; i++) {
                            var item = records[i];
                            result[item["sceneKey"] + ":" + item["triggerId"].AsInt] = new ExitPresentation { Visible = true, ApproachEnabled = true, Cue = item["visibleCue"], SourceFingerprint = item["sourceFingerprint"], Approach = new NavigationPoint(item["x"].AsFloat,item["y"].AsFloat,item["z"].AsFloat) };
                        }
                    }
                }
            } catch (Exception exception) { Log.Message("[FF9Access] Reviewed entrance evidence unavailable: {0}", exception.Message); }
            return result;
        }
    }
}
