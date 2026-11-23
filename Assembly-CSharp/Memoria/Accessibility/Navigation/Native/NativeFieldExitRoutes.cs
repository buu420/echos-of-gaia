using System;
using System.Collections.Generic;
using UnityEngine;
namespace Memoria.Accessibility.Navigation
{
    public static partial class NativeFieldTargets
    {
        private static int _exitRouteFrame=-1;
        private static Dictionary<TargetIdentity,FieldExitRoute> _exitRoutes=new Dictionary<TargetIdentity,FieldExitRoute>();
        // Keyed by the same full roster dependency memory as Story region proofs (StoryProofMemory: global and
        // map variables, every active entry's uid/sid/IP/level/flags/state/script and instance bytes, and the
        // player position), so a requested director or wait owner changing state invalidates the proof.
        private static readonly StoryRegionProofCache<FieldExitPathEvidence> _exitRouteProofs=new StoryRegionProofCache<FieldExitPathEvidence>(30);
        private const int ExitRoutePoints=32;
        // Integration hook (NativeFieldTargets.Read, after Annotate(context,engine,quad,exitObservation)):
        // proves where each current exit approach leads now and publishes its native location names for
        // FieldTargetCatalog. Every exit observation gets a route (possibly empty) so that a stale proof
        // from an earlier frame is never reused. Reads only detached copies; nothing is requested.
        internal static void AnnotateExitRoute(NavigationContext context,EventEngine engine,Quad quad,FieldTargetObservation observation,FieldGeometrySnapshot geometry,int trigger=EventEngine.tagPush)
        {
            if(context==null || engine==null || quad==null || observation==null)return;
            if(_exitRouteFrame!=Time.frameCount){_exitRouteFrame=Time.frameCount;_exitRoutes=new Dictionary<TargetIdentity,FieldExitRoute>();FieldExitRoutes.Publish(context.LogicalMapKey,context.Generation,_exitRoutes);}
            var route=new FieldExitRoute{Presentation=observation.NativePresentationIdentity,ConfirmPrompt=trigger==EventEngine.tagTalk};_exitRoutes[observation.Id]=route;
            try {
                string hash=Fingerprint(quad.ebData);int field=FF9StateSystem.Common.FF9.fldMapNo;
                // The reviewed function and every place any native branch can reach name the exit even
                // when its current path cannot be proven (FieldExitFunctions.Generated.cs).
                bool confirm=trigger==EventEngine.tagTalk;
                route.RequireInwardApproach=!confirm && FieldExitDestinations.RequiresInwardApproach(field,quad.sid,quad.uid,hash);
                route.Function=FieldExitFunctions.Function(field,quad.sid,hash,confirm);
                foreach(int possible in FieldExitFunctions.Possible(field,quad.sid,hash,confirm)) {
                    string name=FieldExitNames.Destination(possible,NativeFieldAreaNames.Destination);
                    if(name==null){route.PossibleNames.Clear();break;}
                    if(!route.PossibleNames.Contains(name))route.PossibleNames.Add(name);
                }
                var exit=observation.Exit;if(exit==null || !exit.ApproachEnabled)return;
                var points=exit.ApproachCandidates!=null && exit.ApproachCandidates.Length!=0?exit.ApproachCandidates:new[]{exit.Approach};
                var groups=new Dictionary<int,List<NavigationPoint>>();var menu=new List<NavigationPoint>();
                var screens=new Dictionary<int,int?>();
                if(observation.DestinationApproaches!=null) {
                    foreach(var group in observation.DestinationApproaches)if(group.Value!=null)groups[group.Key]=new List<NavigationPoint>(group.Value);
                }else {
                    var prove=ExitProof(context,engine,quad,hash,geometry,trigger);
                    int count=0;
                    foreach(var point in points) {
                        if(!point.IsFinite || count++>=ExitRoutePoints)continue;
                        var evidence=prove(point);
                        // A choice that changes state but always leads to one place (a card or item choice
                        // before leaving) asks first; only a reviewed service menu (air cab, trolley, lift) or
                        // a choice between places is a destination menu.
                        bool asks=evidence.Kind==FieldExitPathKind.ChoiceMenu && route.Function==null && evidence.Destinations.Count==1;
                        bool harborPrompt=route.Function=="Harbor boarding" && evidence.Kind==FieldExitPathKind.ChoiceSingle && evidence.UsesChoice;
                        if(evidence.Kind==FieldExitPathKind.ChoiceMenu && !asks || harborPrompt) {
                            menu.Add(point);route.MenuButtons|=evidence.Buttons|(confirm?0x20000u:0u);
                            foreach(int offered in evidence.Destinations) {
                                List<NavigationPoint> approaches;
                                if(!route.MenuApproaches.TryGetValue(offered,out approaches)){approaches=new List<NavigationPoint>();route.MenuApproaches[offered]=approaches;}
                                if(!approaches.Contains(point))approaches.Add(point);
                            }
                            continue;
                        }
                        if(evidence.Kind!=FieldExitPathKind.Determined && evidence.Kind!=FieldExitPathKind.ChoiceSingle && !asks)continue;
                        foreach(int destination in evidence.Destinations) {
                            List<NavigationPoint> list;if(!groups.TryGetValue(destination,out list)){list=new List<NavigationPoint>();groups[destination]=list;}
                            list.Add(point);
                            int? entrance,previous;
                            if(evidence.ArrivalEntrances.TryGetValue(destination,out entrance))
                                screens[destination]=screens.TryGetValue(destination,out previous) && previous!=entrance?null:entrance;
                            // A ledge that jumps only while a button is pressed says which; a shown prompt asks first.
                            if(confirm || evidence.Kind==FieldExitPathKind.ChoiceSingle && evidence.Buttons!=0)
                                route.Buttons[destination]=evidence.Buttons|(confirm?0x20000u:0u);
                            if(evidence.Kind==FieldExitPathKind.ChoiceSingle && evidence.UsesChoice || asks)route.Prompted.Add(destination);
                        }
                    }
                }
                route.Menu=menu.ToArray();
                foreach(int offered in route.MenuApproaches.Keys)if(!route.Names.ContainsKey(offered))route.Names[offered]=FieldExitNames.Destination(offered,NativeFieldAreaNames.Destination);
                foreach(var group in groups) {
                    if(group.Value.Count==0)continue;
                    route.Approaches[group.Key]=group.Value.ToArray();
                    int? entrance;string name=screens.TryGetValue(group.Key,out entrance) && entrance.HasValue?NativeFieldAreaNames.Screen(group.Key,entrance.Value):null;
                    route.Names[group.Key]=name??FieldExitNames.Destination(group.Key,NativeFieldAreaNames.Destination);
                }
            }catch(Exception exception){route.Approaches.Clear();route.Names.Clear();route.Prompted.Clear();route.Menu=new NavigationPoint[0];route.MenuApproaches.Clear();route.Buttons.Clear();route.MenuButtons=0;if(_cueLogs.Add("exit-route:"+exception.GetType().Name))Memoria.Prime.Log.Message("[FF9Access] Exit destination proof unavailable: {0}",exception.Message);}
        }
        // Per-approach proofs of one region, cached on the full roster dependency memory.
        private static Func<NavigationPoint,FieldExitPathEvidence> ExitProof(NavigationContext context,EventEngine engine,Quad quad,string hash,FieldGeometrySnapshot geometry,int trigger=EventEngine.tagPush)
        {
            return _exitRouteProofs.Begin(quad.uid+":exit:"+trigger,context.Generation+":"+context.LogicalMapKey+":"+context.SaveIdentity+":"+context.PlayerIdentity+":"+hash+":"+geometry?.Fingerprint,quad,
                StoryProofMemory(context,engine),Time.frameCount,point=>ReadExitPaths(context,engine,quad,point,trigger,hash,geometry),
                proof => !proof.ReadsApproachPosition);
        }
        private static readonly int[] _dialogueOpcodes={(int)EBin.event_code_binary.MES,(int)EBin.event_code_binary.MESN,(int)EBin.event_code_binary.MESA,(int)EBin.event_code_binary.MESAN,(int)EBin.event_code_binary.MESB};
        // EventEngine.Request: an entry runs a requested function only when the requested level is below its
        // current level. The region itself runs its touch handler at level 1 when the touch fires.
        private static bool? Requestable(EventEngine engine,Quad quad,int uid,int level,int caller)
        {
            var target=uid==255?engine.GetObjUID(caller):uid==250?engine.GetControlChar():engine.GetObjUID(uid);
            if(target==null || target.ebData==null)return null;
            int current=ReferenceEquals(target,quad)?1:target.level;
            return level<current;
        }
        // The region's own touch path and any entry it requests, with the controlled player standing at
        // the prospective approach. Button waits and shown choices are replayed per alternative; the live
        // party answers B_PARTYCHK. Unknown branches, native mutations or moved positions stop the proof.
        private static FieldExitPathEvidence ReadExitPaths(NavigationContext context,EventEngine engine,Quad quad,NavigationPoint point,int trigger,string hash,FieldGeometrySnapshot geometry)
        {
            Func<int,int,int,ScriptSnapshot,ScriptStoryFunction> requested=(uid,tag,callerIdentity,caller)=> {
                var called=uid==255?engine.GetObjUID(callerIdentity/256):uid==250?engine.GetControlChar():engine.GetObjUID(uid);
                if(called==null || called.ebData==null)return null;
                return new ScriptStoryFunction{Identity=called.uid*256+tag,ScriptId=called.sid,Tag=tag,Code=Decode(engine,called,tag),Snapshot=caller.WithInstance(ScriptSnapshot.InstanceBytes(called.buffer,called.vofs))};
            };
            Func<ScriptStoryFunction> start=()=>new ScriptStoryFunction{Identity=quad.uid*256+trigger,ScriptId=quad.sid,Tag=trigger,Code=Decode(engine,quad,trigger),
                Snapshot=new ScriptSnapshot(FF9StateSystem.EventState.gEventGlobal,engine.GetMapVar(),ScriptSnapshot.InstanceBytes(quad.buffer,quad.vofs)){UserControl=true}};
            int field=FF9StateSystem.Common.FF9.fldMapNo;
            bool staysInLift=quad.uid==quad.sid && (field==601 && quad.sid==5 || field==2151 && quad.sid==3) &&
                FieldExitFunctions.Function(field,quad.sid,hash,trigger==EventEngine.tagTalk)=="Castle lift";
            Func<ScriptStoryFunction,bool> visualOnly=null;
            // The harbor boarding menu first animates an actor before selecting its native destination.
            if(field==2173 && quad.uid==8 && quad.sid==8 &&
                hash=="e08da3455151f370252f49f6a27740fbfc5394d4991593c156fd5cd749fdb0f5") {
                var guide=engine.GetObjUID(9);
                if(guide?.ebData!=null &&
                    Fingerprint(guide.ebData)=="1cae55032892d17cb0111e9ff32ede5df914078f7fc0d8cf80ffc6c9b5a3c72e")
                    visualOnly=called=>called.Identity==9*256+15 && called.ScriptId==9 && called.Tag==15;
            }
            NavigationPoint? jump=null;int? heading=null;
            Func<int,int,int> angle=(x,z)=>EventEngineUtils.ConvertFloatAngleToFixedPoint(engine.eBin.angleAsm(x,z))>>4;
            if(quad.n>=2 && quad.q!=null && quad.q.Length>=2 && point.IsFinite) {
                var a=quad.q[0];var b=quad.q[1];
                jump=FieldExitDestinations.JumpPoint(point,a.X,a.Z,b.X,b.Z);
                if(field==552 && a.X==1231 && a.Z==1556 && b.X==1291 && b.Z==1376)
                    jump=new NavigationPoint(1226,point.Y,1430);
                int? px=FieldExitDestinations.PlayerProperty(context.PlayerIdentity,point,250,0),pz=FieldExitDestinations.PlayerProperty(context.PlayerIdentity,point,250,2);
                if(FieldExitDestinations.ReviewedEntryFacing(field,quad.sid,quad.uid,hash) && px.HasValue && pz.HasValue && (jump.Value.X!=px.Value || jump.Value.Z!=pz.Value))
                    heading=angle((int)jump.Value.X-px.Value,(int)jump.Value.Z-pz.Value)&255;
            }
            return FieldExitPaths.Read(start,requested,_opcodes.MapJump,_opcodes.WorldJump,_opcodes.Requests,(uid,index)=>FieldExitDestinations.PlayerProperty(context.PlayerIdentity,point,uid,index),
                character=>engine.partychk(character),_opcodes.DisableControl,_opcodes.EnableControl,new[]{_opcodes.Move,_opcodes.MoveQuad},
                new FieldExitPathOptions{DialogueOpcodes=_dialogueOpcodes,AsyncRequest=(int)EBin.event_code_binary.REQ,PlayerUid=context.PlayerIdentity,
                    CalculateExitPosition=_opcodes.JumpPosition,ExitPosition=jump,EntryFacing=heading,Angle2=angle,Sine=ff9.rsin,Cosine=ff9.rcos,
                    ObjectFloor=uid=> {
                        if(uid==context.PlayerIdentity)return geometry?.At(point)?.Floor;
                        var actor=engine.GetObjUID(uid) as Actor;
                        int floor=actor?.fieldMapActorController?.activeFloor??-1;
                        return floor>=0?(int?)floor:null;
                    },
                    TrackInFieldTransfer=staysInLift,VisualOnlyRequest=visualOnly,
                    Requestable=(uid,level,caller)=>Requestable(engine,quad,uid,level,caller)});
        }
    }
}
