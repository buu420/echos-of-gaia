using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.Navigation
{
    public sealed class SpeechState {public double NowMilliseconds;public bool DialogueActive,DialogueStarting,Busy,NarrationActive;public string LastAnnouncement;}
    public sealed class NavigationAnnouncement {public string Text;public int Priority;public bool Interrupt;}
    public sealed class NavigationAnnouncements
    {
        private double _lastGuidance=-10000;private string _lastRegion,_lastDiagnostic;
        private string _lastGuidanceIdentity,_lastDirection,_lastGuidanceText;private int _lastEndWaypoint=-1;private RoutePlan _lastRoute;
        private readonly List<string> _pending=new List<string>();
        private sealed class GuidanceDelivery
        {
            public NavigationAnnouncement Announcement;public string Identity,Direction,Text;public int EndWaypoint;public RoutePlan Route;public double Time;
        }
        private GuidanceDelivery _offered;
        public void Acknowledge(NavigationAnnouncement announcement,bool accepted)
        {
            var delivery=_offered;if(delivery==null || delivery.Announcement!=announcement)return;_offered=null;
            if(!accepted)return;
            _lastGuidance=delivery.Time;_lastGuidanceIdentity=delivery.Identity;_lastDirection=delivery.Direction;_lastEndWaypoint=delivery.EndWaypoint;_lastRoute=delivery.Route;_lastGuidanceText=delivery.Text;
        }
        public void ResetPending(){_pending.Clear();ResetGuidance();}
        private void ResetGuidance(){_lastGuidance=-10000;_lastGuidanceIdentity=_lastDirection=_lastGuidanceText=null;_lastEndWaypoint=-1;_lastRoute=null;_offered=null;}
        public IList<NavigationAnnouncement> Format(NavigationUpdate update,int intervalMilliseconds,SpeechState speech)
        {
            _offered=null;var result=new List<NavigationAnnouncement>();if(update==null)return result;speech=speech??new SpeechState();
            foreach(string text in update.Messages)if(!_pending.Contains(text))_pending.Add(text);
            if(_pending.Count>4)_pending.RemoveRange(0,_pending.Count-4);
            bool guiding=update.Context.CanControl && update.GuidanceEnabled && (update.Session.Mode==RouteMode.Guiding || update.Session.Mode==RouteMode.Moving) && update.Selected!=null;
            if(!guiding || !update.Context.Foreground || speech.DialogueActive || speech.DialogueStarting)ResetGuidance();
            if(!update.Context.Foreground || speech.DialogueActive || speech.DialogueStarting)return result;
            if(update.RequestRepeat && !String.IsNullOrEmpty(speech.LastAnnouncement)){result.Add(Announcement(speech.LastAnnouncement,100,true));return result;}
            if(update.RequestStatus){string status=Status(update);result.Add(guiding?Offer(update,RouteInstruction.Resolve(update),speech.NowMilliseconds,status,90,true):Announcement(status,90,true));return result;}
            if(speech.NarrationActive)return result;
            if(_pending.Count>0){result.Add(Announcement(String.Join(" ",_pending.ToArray()),80,true));_pending.Clear();return result;}
            var instruction=guiding?RouteInstruction.Resolve(update):null;
            bool changed=guiding && (Identity(update)!=_lastGuidanceIdentity || Route(update,instruction)!=_lastRoute || instruction.Direction!=_lastDirection || instruction.EndWaypointIndex!=_lastEndWaypoint);
            bool replaceOwn=changed && !String.IsNullOrEmpty(_lastGuidanceText) && speech.LastAnnouncement==_lastGuidanceText;
            if(speech.Busy && !replaceOwn)return result;
            if(!speech.Busy && update.Diagnostics.Count>0) {string diagnostic=String.Join(" ",new List<string>(update.Diagnostics).ToArray());if(diagnostic!=_lastDiagnostic){_lastDiagnostic=diagnostic;result.Add(Announcement(diagnostic,60,false));return result;}}
            if(!speech.Busy && update.Context.CanControl && !String.IsNullOrEmpty(update.Context.CurrentRegion)) {
                string region=update.Context.LogicalMapKey+":"+update.Context.CurrentRegion;
                if(region!=_lastRegion){_lastRegion=region;result.Add(Announcement(update.Context.CurrentRegion,40,false));return result;}
            }
            if(guiding && (changed || speech.NowMilliseconds<_lastGuidance || speech.NowMilliseconds-_lastGuidance>=NavigationControls.IntervalMilliseconds(intervalMilliseconds,1))) {
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
            _offered=new GuidanceDelivery{Announcement=announcement,Time=now,Identity=Identity(update),Direction=instruction.Direction,EndWaypoint=instruction.EndWaypointIndex,Route=Route(update,instruction),Text=text};
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
        private static NavigationAnnouncement Announcement(string text,int priority,bool interrupt){return new NavigationAnnouncement{Text=text,Priority=priority,Interrupt=interrupt};}
    }
}
