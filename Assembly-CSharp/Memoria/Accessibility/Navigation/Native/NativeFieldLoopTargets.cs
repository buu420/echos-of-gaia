using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Memoria.Prime;
using SimpleJSON;
using UnityEngine;
namespace Memoria.Accessibility.Navigation
{
    public static partial class NativeFieldTargets
    {
        private sealed class LoopBinding
        {
            public int Field,Sid,Uid,Floor,Ceiling,Entry,Prompt,Resume,HalfWidth;
            public int SpriteIndex=-1,SpriteEffect=-1;
            public bool PromptOnly;
            public string Scene,Hash,Background,Walkmesh,Label;
            public NavigationPoint Anchor;
        }
        private sealed class LoopSurface {public string Geometry;public NavigationPoint[] Points;}
        private static bool _loopsLoaded;
        private static long _loopGeneration=Int64.MinValue;
        private static readonly Dictionary<int,List<LoopBinding>> _loopBindings=new Dictionary<int,List<LoopBinding>>();
        private static readonly Dictionary<byte[],FieldLoopPrompt> _loopPrograms=new Dictionary<byte[],FieldLoopPrompt>();
        private static readonly Dictionary<LoopBinding,LoopSurface> _loopSurfaces=new Dictionary<LoopBinding,LoopSurface>();
        private static readonly StoryRegionProofCache<object> _loopProofs=new StoryRegionProofCache<object>(30);
        private static readonly object _loopPromptPresent=new object();

        private static void LoadLoopBindings()
        {
            if(_loopsLoaded)return;_loopsLoaded=true;
            try {
                using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Memoria.Accessibility.FieldLoopPrompts.json")) {
                    if(stream==null)throw new InvalidOperationException("Missing reviewed field loop prompts");
                    using(var reader=new StreamReader(stream)) {
                        var root=JSON.Parse(reader.ReadToEnd());if(root["schema"].AsInt!=1)throw new InvalidOperationException("Unsupported field loop prompt schema");
                        var rows=root["objects"];
                        for(int i=0;i<rows.Count;i++) {
                            var row=rows[i];var sprite=row["sprite"];
                            var binding=new LoopBinding{Field=row["field"].AsInt,Sid=row["sid"].AsInt,Uid=row["uid"].AsInt,
                                Floor=row["floor"].AsInt,Ceiling=row["ceiling"].AsInt,Entry=row["entry"].AsInt,Prompt=row["prompt"].AsInt,Resume=row["resume"].AsInt,HalfWidth=row["halfWidth"].AsInt,PromptOnly=row["promptOnly"].AsBool,
                                Scene=row["scene"],Hash=row["hash"],Background=row["background"],Walkmesh=row["walkmesh"],Label=row["label"]};
                            if(!binding.PromptOnly) {
                                var anchor=row["anchor"] as JSONArray;
                                if(anchor==null || anchor.Count!=3)
                                    throw new InvalidOperationException("Invalid reviewed loop anchor");
                                binding.Anchor=new NavigationPoint(anchor[0].AsInt,anchor[1].AsInt,anchor[2].AsInt);
                            }
                            if(sprite.Count!=0) {
                                if(sprite.Count!=2 || binding.PromptOnly || sprite[0].AsInt<0 || sprite[1].AsInt<0)
                                    throw new InvalidOperationException("Invalid reviewed loop sprite");
                                binding.SpriteIndex=sprite[0].AsInt;binding.SpriteEffect=sprite[1].AsInt;
                            }
                            if((!binding.PromptOnly && (binding.Uid<0 || binding.Uid>=250 || binding.HalfWidth<=0 || binding.HalfWidth>1024 || !binding.Anchor.IsFinite)) ||
                                binding.Floor>=binding.Ceiling || String.IsNullOrEmpty(binding.Label))
                                throw new InvalidOperationException("Invalid reviewed loop object");
                            List<LoopBinding> list;if(!_loopBindings.TryGetValue(binding.Field,out list)){list=new List<LoopBinding>();_loopBindings[binding.Field]=list;}
                            if(list.Exists(other=>other.PromptOnly==binding.PromptOnly && (binding.PromptOnly?other.Sid==binding.Sid:other.Uid==binding.Uid)))throw new InvalidOperationException("Duplicate reviewed loop object");
                            list.Add(binding);
                        }
                    }
                }
            }catch(Exception exception){_loopBindings.Clear();Log.Message("[FF9Access] Reviewed field loop prompts unavailable: {0}",exception.Message);}
        }
        private static Obj LoopOwner(NavigationContext context,EventEngine engine,FieldMap field,LoopBinding binding,int shown=-1)
        {
            if(context==null || !context.CanControl || engine==null || engine.gMode!=1 || field==null || field.scene==null || field.scene.ebgBin==null ||
                context.StoryProgress<binding.Floor || context.StoryProgress>=binding.Ceiling || context.LogicalMapKey!="field:"+binding.Field+":"+binding.Scene ||
                !binding.PromptOnly && (Fingerprint(field.scene.ebgBin)!=binding.Background || CurrentBgiHash(field)!=binding.Walkmesh))return null;
            int uid=binding.PromptOnly?shown:binding.Uid;if(uid<0 || uid>=250)return null;
            var owner=engine.GetObjUID(uid);
            if(owner==null || owner.sid!=binding.Sid || owner.uid!=uid || owner is Actor || owner is Quad || owner.ebData==null || Fingerprint(owner.ebData)!=binding.Hash ||
                owner.state!=EventEngine.stateRunning || !binding.PromptOnly && (owner.level!=EventEngine.cEventLevelN-1 || owner.ip!=binding.Resume && owner.ip!=binding.Entry))return null;
            return owner;
        }
        private static bool LoopPromptAt(NavigationContext context,EventEngine engine,Obj owner,LoopBinding binding,NavigationPoint point,byte[] local)
        {
            if(!point.IsFinite)return false;
            FieldLoopPrompt program;
            if(!_loopPrograms.TryGetValue(owner.ebData,out program)){
                program=new FieldLoopPrompt(Decode(engine,owner,EventEngine.tagDefault));_loopPrograms[owner.ebData]=program;
            }
            var snapshot=new ScriptSnapshot(FF9StateSystem.EventState.gEventGlobal,engine.GetMapVar(),local){UserControl=context.CanControl};
            return program.Presented(binding.Entry,binding.Prompt,(int)EBin.event_code_binary.FICON,snapshot,
                (uid,property)=>FieldExitDestinations.PlayerProperty(context.PlayerIdentity,point,uid==255?owner.uid:uid,property));
        }
        private static void ReadLoopTargets(NavigationContext context,EventEngine engine,FieldMap field,Actor player,List<FieldTargetObservation> result,ref FieldGeometrySnapshot geometry,ref bool geometryRead)
        {
            if(!context.CanControl || player==null || player.uid!=context.PlayerIdentity)return;
            LoadLoopBindings();List<LoopBinding> rows;
            if(!_loopBindings.TryGetValue(FF9StateSystem.Common.FF9.fldMapNo,out rows))return;
            if(_loopGeneration!=context.Generation){_loopGeneration=context.Generation;_loopSurfaces.Clear();_loopPrograms.Clear();}
            int shown=EIcon.NavigationPresentedUid;
            foreach(var row in rows) {
                if(row.PromptOnly && shown<0)continue;
                var owner=LoopOwner(context,engine,field,row,shown);if(owner==null)continue;
                if(row.PromptOnly) {
                    if(OnScreen(field,Vector(context.Position)))result.Add(new FieldTargetObservation{Id=new TargetIdentity(TargetKind.FieldActor,context.LogicalMapKey,owner.uid),
                        NativePresentationIdentity=System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(owner),Position=context.Position,LabelIdentity=row.Hash,VisibleLabel=row.Label,
                        ShowsIcon=true,HasInteraction=true,InteractionUid=owner.uid,ApproachCandidates=new[]{context.Position},UseApprovedApproaches=true});
                    continue;
                }
                Vector3 effectPosition;
                bool visible=row.SpriteIndex<0?StorySurfacePresented(field,row.Anchor):
                    engine.fieldSps!=null && engine.fieldSps.TryGetNavigationEffectPosition(field,row.SpriteIndex,row.SpriteEffect,out effectPosition)
                    && EntranceOnScreen(field,effectPosition);
                bool icon=shown==row.Uid;
                if(!visible && !icon)continue;
                if(!geometryRead){geometry=NativeFieldGeometry.Read(context);geometryRead=true;}
                if(geometry==null)continue;
                LoopSurface surface;
                if(!_loopSurfaces.TryGetValue(row,out surface) || surface.Geometry!=geometry.Fingerprint) {
                    var a=row.Anchor;int w=row.HalfWidth;
                    var footprint=new[]{new NavigationPoint(a.X-w,a.Y,a.Z-w),new NavigationPoint(a.X+w,a.Y,a.Z-w),new NavigationPoint(a.X+w,a.Y,a.Z+w),new NavigationPoint(a.X-w,a.Y,a.Z+w)};
                    var points=FieldRegionApproaches.Interaction(footprint,geometry);
                    Array.Sort(points,(p,q)=>NavigationPoint.Distance(a,p).CompareTo(NavigationPoint.Distance(a,q)));
                    surface=new LoopSurface{Geometry=geometry.Fingerprint,Points=points};_loopSurfaces[row]=surface;
                }
                var local=ScriptSnapshot.InstanceBytes(owner.buffer,owner.vofs);
                // Only the loop's own detached instance, map and persistent
                // bytes plus the proposed player point are read by this proof.
                var prove=_loopProofs.Begin(row.Field+":"+row.Uid,context.Generation+":"+context.SaveIdentity+":"+context.PlayerIdentity+":"+row.Hash,surface,
                    new[]{FF9StateSystem.EventState.gEventGlobal,engine.GetMapVar(),local},UnityEngine.Time.frameCount,point=>LoopPromptAt(context,engine,owner,row,point,local)?_loopPromptPresent:null);
                var approaches=new List<NavigationPoint>();int checkedPoints=0;
                foreach(var point in surface.Points) {
                    if(checkedPoints++>=48 || approaches.Count>=16)break;
                    if(StorySurfacePresented(field,point) && prove(point)!=null)approaches.Add(point);
                }
                bool here=icon && prove(context.Position)!=null;if(here && !approaches.Contains(context.Position))approaches.Insert(0,context.Position);
                if(approaches.Count==0)continue;
                var observation=new FieldTargetObservation{Id=new TargetIdentity(TargetKind.FieldActor,context.LogicalMapKey,row.Uid),Position=row.Anchor,
                    NativePresentationIdentity=System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(owner),LabelIdentity=row.Hash,VisibleLabel=row.Label,
                    Visible=visible,ShowsIcon=here,HasInteraction=true,InteractionUid=row.Uid,ApproachCandidates=approaches.ToArray(),UseApprovedApproaches=true};
                NativeStorySteps.AnnotateLoop(row.Field,context.StoryProgress,row.Sid,row.Uid,row.Hash,row.Entry,row.Prompt,row.Resume,observation);
                result.Add(observation);
            }
        }
        internal static bool TryLoopPromptReadiness(NavigationContext context,NavigationTarget target,EventEngine engine,Actor player,out InteractionReadiness result)
        {
            result=null;List<LoopBinding> rows;
            if(target==null || target.InteractionUid!=target.PhysicalIdentity.NativeId || !_loopBindings.TryGetValue(FF9StateSystem.Common.FF9.fldMapNo,out rows))return false;
            var selected=engine.GetObjUID(target.InteractionUid);
            foreach(var row in rows)if(row.PromptOnly?selected!=null && selected.sid==row.Sid:row.Uid==target.InteractionUid) {
                result=new InteractionReadiness();
                var field=player==null || player.fieldMapActorController==null?null:player.fieldMapActorController.fieldMap;
                var owner=LoopOwner(context,engine,field,row,target.InteractionUid);
                if(owner!=null && EIcon.NavigationPresentedUid==owner.uid && (row.PromptOnly || LoopPromptAt(context,engine,owner,row,context.Position,ScriptSnapshot.InstanceBytes(owner.buffer,owner.vofs)))) {
                    result.SelectedCollision=result.InRange=result.Talkable=result.FacingWithinArc=true;
                    result.Evidence="The native proximity prompt is presented. Confirm remains player controlled.";
                }
                return true;
            }
            return false;
        }
    }
}
