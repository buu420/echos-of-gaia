using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.Navigation
{
    public enum StoryPrerequisiteKind {FossilJunction,FossilBoarding,Pinnacle,Gulug}
    public sealed class StoryPrerequisiteField
    {
        public int Field,Floor,Ceiling,Milestone,FlowerByte=-1;
        public StoryPrerequisiteKind Kind;public string Director,Bgi;
        public Dictionary<int,string> Entries,Players;
        public int[] Components;public Dictionary<int,int> Fragments;
        public StoryPrerequisiteRoute[] Routes;
    }
    public sealed class StoryPrerequisiteRoute {public int Flags,Component;public string Action;public int[] Sids;}
    public sealed class StoryPrerequisiteObject {public int Sid,Model;public string Hash;}
    public sealed class StoryPrerequisiteEvidence
    {
        public int Field,PlayerSid,ActiveTriangle=-1;public string PlayerHash,DirectorHash,BgiHash;
        public Func<int,int?> GlobalByte,MapByte;
        public Func<TargetIdentity,StoryPrerequisiteObject> Object;
    }
    // Consumes current native state and existing physical targets; never creates a hidden approach.
    public sealed class StoryPrerequisiteCatalog
    {
        private readonly Dictionary<int,StoryPrerequisiteField> _fields=new Dictionary<int,StoryPrerequisiteField>();
        public StoryPrerequisiteCatalog(StoryPrerequisiteField[] fields){foreach(var field in fields)_fields.Add(field.Field,field);}
        public bool HasScope(int field,int progress)
        {StoryPrerequisiteField rule;return _fields.TryGetValue(field,out rule) && progress>=rule.Floor && progress<rule.Ceiling;}
        public StoryPrerequisiteField Field(int field){StoryPrerequisiteField rule;return _fields.TryGetValue(field,out rule)?rule:null;}
        public bool TryAppend(NavigationContext context,CatalogResult catalog,StoryPrerequisiteEvidence evidence)
        {
            if(context==null || catalog==null || evidence==null || !HasScope(evidence.Field,context.StoryProgress))return false;
            var rule=_fields[evidence.Field];string expected;
            if(!context.Foreground || (!context.CanInspect && !context.CanControl) || evidence.DirectorHash!=rule.Director ||
                !rule.Players.TryGetValue(evidence.PlayerSid,out expected) || evidence.PlayerHash!=expected)return Unavailable(context,catalog,rule);
            int flags=0;string action=null,label=null;int[] sids=null;
            if(rule.Kind==StoryPrerequisiteKind.Pinnacle) {
                int? value=Read(evidence.GlobalByte,266);if(!value.HasValue || (value.Value & ~31)!=0)return Unavailable(context,catalog,rule);
                flags=value.Value;if(flags==31)return false;
                action="fragment";label="Collect a story fragment";
            }else if(rule.Kind==StoryPrerequisiteKind.Gulug) {
                int? complete=Read(evidence.GlobalByte,407);if(!complete.HasValue)return Unavailable(context,catalog,rule);
                if((complete.Value & 128)!=0)return false;
                int? count=Read(evidence.GlobalByte,401);if(!count.HasValue || count.Value>2)return Unavailable(context,catalog,rule);
                action="lever";sids=new[]{8};label="Use the lever to open the lower route";
            }else {
                int component=-1;
                if(rule.Kind==StoryPrerequisiteKind.FossilJunction) {
                    int? value=Read(evidence.GlobalByte,404);
                    if(!value.HasValue || evidence.BgiHash!=rule.Bgi || evidence.ActiveTriangle<0 || evidence.ActiveTriangle>=rule.Components.Length)return Unavailable(context,catalog,rule);
                    flags=value.Value & 240;component=rule.Components[evidence.ActiveTriangle];
                }else {
                    int? value=Read(evidence.MapByte,rule.FlowerByte),switches=Read(evidence.GlobalByte,404);
                    if(!value.HasValue || !switches.HasValue || evidence.BgiHash!=rule.Bgi || evidence.ActiveTriangle<0 || evidence.ActiveTriangle>=rule.Components.Length)return Unavailable(context,catalog,rule);
                    flags=(switches.Value & 240)|((value.Value & 128)!=0?1:0);component=rule.Components[evidence.ActiveTriangle];
                }
                foreach(var route in rule.Routes)if(route.Flags==flags && route.Component==component){action=route.Action;sids=route.Sids;break;}
                if(action==null)return Unavailable(context,catalog,rule);label=FossilLabel(action,flags);
            }
            var chosen=new List<NavigationTarget>();
            foreach(var target in catalog.Targets) {
                if(target.Category==NavigationCategory.Story || target.Generation!=context.Generation || target.Id.MapKey!=context.LogicalMapKey || !target.IsInteractive || target.ApproachCandidates!=null && target.ApproachCandidates.Length==0)continue;
                var obj=evidence.Object==null?null:evidence.Object(target.PhysicalIdentity);
                if(obj==null || !rule.Entries.TryGetValue(obj.Sid,out expected) || expected!=obj.Hash)continue;
                if(rule.Kind==StoryPrerequisiteKind.Pinnacle) {
                    int mask;if(obj.Model!=102 || !rule.Fragments.TryGetValue(obj.Sid,out mask) || (flags & mask)!=0)continue;
                }else if(!Contains(sids,obj.Sid))continue;
                chosen.Add(target);
            }
            if(chosen.Count==0) {
                string help=rule.Kind==StoryPrerequisiteKind.Pinnacle?"Explore the area for the remaining fragments. No verified walking target is currently presented.":"No verified walking target is currently presented. Use the ordinary game interaction; this step may require manual movement.";
                catalog.Targets.Add(Information(context,rule,label,help));return true;
            }
            foreach(var physical in chosen)catalog.Targets.Add(StoryTargetCatalog.Wrap(context,rule.Milestone,physical,label+" with "+physical.Label));
            return true;
        }
        private static int? Read(Func<int,int?> read,int index){int? value=read==null?null:read(index);return value.HasValue && value.Value>=0 && value.Value<=255?value:null;}
        private static bool Contains(int[] values,int value){if(values!=null)foreach(int item in values)if(item==value)return true;return false;}
        private static bool Unavailable(NavigationContext c,CatalogResult catalog,StoryPrerequisiteField rule)
        {catalog.Diagnostics.Add("Story prerequisite state unverified: field="+rule.Field);catalog.Targets.Add(Information(c,rule,"Continue the current story","Current puzzle state is unavailable. Use the game's shown interactions; no verified walking target is available."));return true;}
        private static NavigationTarget Information(NavigationContext c,StoryPrerequisiteField rule,string label,string help)
        {return new NavigationTarget{Id=new TargetIdentity(TargetKind.Story,c.LogicalMapKey,-200000-rule.Field),Category=NavigationCategory.Story,Generation=c.Generation,Position=c.Position,Label=label,Availability=help,ApproachCandidates=new NavigationPoint[0]};}
        private static string FossilLabel(string action,int flags)
        {
            switch(action) {
                case "switch1":return (flags & 192)==192?"Restore the first route switch":"Change the first route switch";
                case "switch2":return "Change the second route switch";
                case "switch3":return "Change the final route switch";
                case "switch4-return-course":return "Restore the switch to the main route";
                case "north-route":return "Head toward the first route switch";
                case "south-route":return "Continue toward the next route";
                case "enter-hub":return "Enter the junction";
                case "enter-central-hub":return "Enter the central junction";
                case "enter-far-hub":return "Enter the next junction";
                case "return-hub":return "Return to the junction";
                case "return-switch4":return "Return to the side-route switch";
                case "return-switch3":return "Return to the final route switch";
                case "toward-switch1":return "Continue toward the first route switch";
                case "return-switch1":return "Return to the first route switch";
                case "toward-switch2":return "Continue toward the second route switch";
                case "toward-switch3":return "Continue toward the final route switch";
                case "leave-switch1":return "Leave the first switch";
                case "leave-switch2":return "Leave the second switch";
                case "continue-main-course":return "Continue along the main route";
                case "exit-gargant-route":return "Take the gargant toward the exit";
                case "pick-flower":return "Pick a flower for the gargant";
                case "feed-gargant":return "Feed the flower to the gargant";
                default:return "Return to the main route";
            }
        }
    }
}
