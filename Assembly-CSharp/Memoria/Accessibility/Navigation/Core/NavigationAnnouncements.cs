using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.Navigation
{
    public sealed class SpeechState {public double NowMilliseconds;public bool DialogueActive,DialogueStarting,Busy,NarrationActive;public string LastAnnouncement;}
    public sealed class NavigationAnnouncement {public string Text;public int Priority;public bool Interrupt;}
    public sealed class NavigationAnnouncements
    {
        private double _lastGuidance=-10000;private string _lastRegion,_lastDiagnostic;
        private string _lastGuidanceIdentity,_lastDirection,_lastFollowingDirection,_lastGuidanceText,_lastInformationText,_lastInstructionText;private int _lastEndWaypoint=-1;private RoutePlan _lastRoute;
        private bool _lastStableLeg,_lastCorrection;private NavigationPoint _lastLegEnd;
        private readonly List<string> _pending=new List<string>();
        private sealed class GuidanceDelivery
        {
            public NavigationAnnouncement Announcement;public string Identity,Direction,FollowingDirection,Text,InformationText,InstructionText;public int EndWaypoint;public RoutePlan Route;public double Time;
            public bool StableLeg,Correcting;public NavigationPoint LegEnd;
        }
        private GuidanceDelivery _offered;
        private NavigationAnnouncement _offeredRegion;private string _offeredRegionIdentity;
        public void Acknowledge(NavigationAnnouncement announcement,bool accepted)
        {
            if(announcement==_offeredRegion) {if(accepted)_lastRegion=_offeredRegionIdentity;_offeredRegion=null;}
            var delivery=_offered;if(delivery==null || delivery.Announcement!=announcement)return;_offered=null;
            if(!accepted)return;
            _lastGuidance=delivery.Time;_lastGuidanceIdentity=delivery.Identity;_lastDirection=delivery.Direction;_lastEndWaypoint=delivery.EndWaypoint;_lastRoute=delivery.Route;_lastGuidanceText=delivery.Text;_lastInformationText=delivery.InformationText;
            _lastStableLeg=delivery.StableLeg;_lastCorrection=delivery.Correcting;_lastLegEnd=delivery.LegEnd;
            _lastInstructionText=delivery.InstructionText;
            _lastFollowingDirection=delivery.FollowingDirection;
        }
        public void ResetPending(){_pending.Clear();_offeredRegion=null;ResetGuidance();}
        private void ResetGuidance(){_lastGuidance=-10000;_lastGuidanceIdentity=_lastDirection=_lastFollowingDirection=_lastGuidanceText=_lastInformationText=_lastInstructionText=null;_lastEndWaypoint=-1;_lastRoute=null;_offered=null;_lastStableLeg=_lastCorrection=false;}
        public IList<NavigationAnnouncement> Format(NavigationUpdate update,int intervalMilliseconds,SpeechState speech)
        {
            _offered=null;_offeredRegion=null;var result=new List<NavigationAnnouncement>();if(update==null)return result;speech=speech??new SpeechState();
            foreach(string text in update.Messages)if(!_pending.Contains(text))_pending.Add(text);
            if(_pending.Count>4)_pending.RemoveRange(0,_pending.Count-4);
            bool guiding=update.Context.CanControl && update.GuidanceEnabled && (update.Session.Mode==RouteMode.Guiding || update.Session.Mode==RouteMode.Moving) && update.Selected!=null;
            if(!guiding || !update.Context.Foreground || speech.DialogueActive || speech.DialogueStarting)ResetGuidance();
            if(!update.Context.Foreground || speech.DialogueActive || speech.DialogueStarting)return result;
            if(update.RequestRepeat && !String.IsNullOrEmpty(speech.LastAnnouncement)){result.Add(Announcement(speech.LastAnnouncement,100,true));return result;}
            if(update.RequestStatus){string status=Status(update);result.Add(guiding?Offer(update,RouteInstruction.Resolve(update),speech.NowMilliseconds,status,90,true):Announcement(status,90,true));return result;}
            if(speech.NarrationActive)return result;
            if(_pending.Count>0){string text=String.Join(" ",_pending.ToArray());bool informationRead=guiding && InformationOnly(update) && !String.IsNullOrEmpty(update.Selected.Availability) && text.Contains(update.Selected.Availability);result.Add(informationRead?Offer(update,RouteInstruction.Resolve(update),speech.NowMilliseconds,text,80,true):Announcement(text,80,true));_pending.Clear();return result;}
            if(update.RefreshGuidance || update.Movement!=null && update.Movement.IsPlanning && update.Movement.QuietPlanning)return result;
            var instruction=guiding?RouteInstruction.Resolve(update):null;
            bool information=guiding && InformationOnly(update);
            bool legChanged=guiding && (instruction.StableLeg? !_lastStableLeg || instruction.Correcting!=_lastCorrection || LegDistance(update.Context,instruction.LegEnd,_lastLegEnd)>(update.Context.LogicalMapKey.StartsWith("world:")?.05f:4):Route(update,instruction)!=_lastRoute || instruction.EndWaypointIndex!=_lastEndWaypoint);
            bool changed=guiding && (Identity(update)!=_lastGuidanceIdentity || instruction.Direction!=_lastDirection || instruction.FollowingDirection!=_lastFollowingDirection || legChanged || information && Guidance(update)!=_lastInformationText);
            if(guiding && instruction.StableLeg && _lastStableLeg && Identity(update)==_lastGuidanceIdentity && instruction.Text==_lastInstructionText)changed=false;
            bool replaceOwn=changed && !String.IsNullOrEmpty(_lastGuidanceText) && speech.LastAnnouncement==_lastGuidanceText;
            if(speech.Busy && !replaceOwn)return result;
            if(!speech.Busy && update.Diagnostics.Count>0) {string diagnostic=String.Join(" ",new List<string>(update.Diagnostics).ToArray());if(diagnostic!=_lastDiagnostic){_lastDiagnostic=diagnostic;result.Add(Announcement(diagnostic,60,false));return result;}}
            if(!speech.Busy && update.Context.CanControl && !String.IsNullOrEmpty(update.Context.CurrentRegion)) {
                string region=update.Context.LogicalMapKey+":"+update.Context.CurrentRegion;
                if(region!=_lastRegion){_offeredRegionIdentity=region;_offeredRegion=Announcement(update.Context.CurrentRegion,40,false);result.Add(_offeredRegion);return result;}
            }
            bool routeFailed=update.Movement!=null &&
                (update.Movement.RouteFailure.HasValue && update.Movement.RouteFailure.Value!=RouteFailure.None ||
                 update.Movement.PlannedRoute!=null && !update.Movement.PlannedRoute.Success);
            if(guiding && !routeFailed && (changed || !information && (speech.NowMilliseconds<_lastGuidance || speech.NowMilliseconds-_lastGuidance>=NavigationControls.IntervalMilliseconds(intervalMilliseconds,1)))) {
                string text=Guidance(update);result.Add(Offer(update,instruction,speech.NowMilliseconds,text,20,speech.Busy && replaceOwn));
            }return result;
        }
        private static string Identity(NavigationUpdate update)
        {
            var c=update.Context;return c.Generation+"|"+c.LogicalMapKey+"|"+c.SaveIdentity+"|"+c.PlayerIdentity+"|"+c.Transport.Kind+"|"+c.Transport.ActorId+"|"+c.Transport.NativeStatus+"|"+update.Selected.Id;
        }
        private static RoutePlan Route(NavigationUpdate update,RouteInstruction instruction){return instruction.HasRoute?update.Movement.PlannedRoute:null;}
        private NavigationAnnouncement Offer(NavigationUpdate update,RouteInstruction instruction,double now,string text,int priority,bool interrupt)
        {
            var announcement=Announcement(text,priority,interrupt);
            _offered=new GuidanceDelivery{Announcement=announcement,Time=now,Identity=Identity(update),Direction=instruction.Direction,EndWaypoint=instruction.EndWaypointIndex,Route=Route(update,instruction),Text=text,InformationText=InformationOnly(update)?Guidance(update):null,StableLeg=instruction.StableLeg,Correcting=instruction.Correcting,LegEnd=instruction.LegEnd};
            _offered.InstructionText=instruction.Text;
            _offered.FollowingDirection=instruction.FollowingDirection;
            return announcement;
        }
        public static string Status(NavigationUpdate update)
        {
            var c=update.Context;
            if(!c.CanControl) {
                string reason=c.GateReason==GateReason.UnknownActor?"player control not established":c.GateReason==GateReason.Background?"game is in the background":c.GateReason==GateReason.Dialogue?"dialogue active":c.GateReason==GateReason.Menu?"game menu active":c.GateReason==GateReason.Battle?"battle active":"native movement control unavailable";
                return "Navigation unavailable: "+reason+".";
            }
            string location=String.IsNullOrEmpty(c.CurrentLocation)?"Current location":c.CurrentLocation;
            string transport=c.Transport.Kind==TransportKind.Foot?"On foot":c.Transport.Kind==TransportKind.Boat?"Blue Narciss":c.Transport.Kind==TransportKind.HildaGarde?"Hilda Garde III":c.Transport.Kind==TransportKind.Invincible?"Invincible":c.Transport.Kind.ToString();
            string mode=update.IsPlanning?"waiting for route data":update.Session.Mode.ToString().ToLowerInvariant();
            string availability=update.Selected==null || update.Selected.PhysicalIdentity.Kind==TargetKind.Story || String.IsNullOrEmpty(update.Selected.Availability)?"":" "+update.Selected.Availability+".";
            return location+". "+transport+". "+(update.Selected==null?"No target selected.":update.Selected.Label+". ")+"Navigation "+mode+"."+(update.Selected==null?"":" "+Guidance(update))+availability;
        }
        private static string Guidance(NavigationUpdate update)
        {
            if(update.Selected.PhysicalIdentity.Kind==TargetKind.Story)return update.Selected.Label+" "+update.Selected.Availability;
            return RouteInstruction.Resolve(update).Text;
        }
        private static bool InformationOnly(NavigationUpdate update){return update.Selected!=null && update.Selected.PhysicalIdentity.Kind==TargetKind.Story;}
        private static float LegDistance(NavigationContext context,NavigationPoint a,NavigationPoint b){return context.LogicalMapKey.StartsWith("world:")?WorldCoordinates.Distance(a,b):NavigationPoint.Distance(a,b);}
        private static NavigationAnnouncement Announcement(string text,int priority,bool interrupt){return new NavigationAnnouncement{Text=text,Priority=priority,Interrupt=interrupt};}
    }
}
