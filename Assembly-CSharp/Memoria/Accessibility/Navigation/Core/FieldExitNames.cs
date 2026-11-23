using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.Navigation
{
    // The current native destination proof of one presented exit region: for each destination the
    // approach points whose detached trigger path reaches it, and its native location name (null
    // when the native text is unavailable). Presentation is the observation's native identity.
    public sealed class FieldExitRoute
    {
        public int Presentation;
        public bool ConfirmPrompt;
        internal FieldExitContinuation Continuation;
        public bool RequireInwardApproach;
        public readonly Dictionary<int,NavigationPoint[]> Approaches=new Dictionary<int,NavigationPoint[]>();
        public readonly Dictionary<int,string> Names=new Dictionary<int,string>();
        // Destinations reached only after a shown prompt the player may decline.
        public readonly HashSet<int> Prompted=new HashSet<int>();
        // Destinations reached only while the player presses these buttons (EventInput mask) at the region.
        public readonly Dictionary<int,uint> Buttons=new Dictionary<int,uint>();
        // Approaches where the player's choice in a shown menu decides where the exit leads.
        public NavigationPoint[] Menu=new NavigationPoint[0];
        // Per destination, only the points whose current menu path offers it.
        // These are internal routing edges; the game presents the choices.
        public readonly Dictionary<int,List<NavigationPoint>> MenuApproaches=new Dictionary<int,List<NavigationPoint>>();
        // Buttons the region waits for before its menu opens (EventInput mask), 0 when none.
        public uint MenuButtons;
        // Reviewed function of a choice exit from its native prompt (FieldExitFunctions), or null.
        public string Function;
        // Native names of every place any branch of the exit can reach (FieldExitFunctions.Possible).
        public readonly List<string> PossibleNames=new List<string>();
    }
    // A native doorway may enter a scene that returns control in a different field.
    // The first map jump remains the key for live proof and movement.
    internal sealed class FieldExitContinuation
    {
        public readonly int FirstField;
        private readonly int _arrival;

        internal FieldExitContinuation(int firstField,int arrival)
        {FirstField=firstField;_arrival=arrival;}

        internal int Arrival(int destination)
        {return destination==FirstField?_arrival:destination;}
    }
    public static partial class FieldExitFunctions
    {
        private static string Key(int field,int sid,string hash){return field+":"+sid+":"+hash;}
        private static readonly FieldExitContinuation _businessStation=new FieldExitContinuation(561,551);
        internal static FieldExitContinuation SceneContinuation(int field,int sid,int uid,int tag,string hash,Func<int,string> eventHash)
        {
            // 552:22:2 @382 enters Vivi's Shopping at entrance 1.
            // That version of 561 returns to the same station entrance as 552:22:2 @636.
            if(field==552 && sid==22 && uid==22 && tag==2 &&
                hash=="e8a19cfa44dd00b56a87265ac78199ce45f5e81304e76ffbca649dc20999c3f0" &&
                eventHash?.Invoke(561)=="403e6c1d51c881d53087f66c11c430dfdf8a295df3c9921225e7831241b67b88")
                return _businessStation;
            return null;
        }
        public static string Function(int field,int sid,string hash,bool confirm=false)
        {string label;return hash!=null && (confirm?_confirmFunctions:_functions).TryGetValue(Key(field,sid,hash),out label)?label:null;}
        public static int[] Possible(int field,int sid,string hash,bool confirm=false)
        {int[] fields;return hash!=null && (confirm?_confirmPossible:_possible).TryGetValue(Key(field,sid,hash),out fields)?(int[])fields.Clone():new int[0];}
        public static IEnumerable<string> Keys(bool confirm=false){return (confirm?_confirmPossible:_possible).Keys;}
    }
    // Routes of the current field read, published by the native reader for the catalog that follows it.
    public static class FieldExitRoutes
    {
        private static string _map;private static long _generation;
        private static Dictionary<TargetIdentity,FieldExitRoute> _routes=new Dictionary<TargetIdentity,FieldExitRoute>();
        public static void Publish(string map,long generation,Dictionary<TargetIdentity,FieldExitRoute> routes)
        {_map=map;_generation=generation;_routes=routes??new Dictionary<TargetIdentity,FieldExitRoute>();}
        public static FieldExitRoute For(NavigationContext context,TargetIdentity id,int presentation)
        {
            FieldExitRoute route;
            if(context==null || _map!=context.LogicalMapKey || _generation!=context.Generation || !_routes.TryGetValue(id,out route) || route==null || route.Presentation!=presentation)return null;
            return route;
        }
    }
    public static class FieldExitNames
    {
        public const string WorldMap="World Map";
        // Native field ids are below 9000; WMAPJUMP world destinations are 9000 and above.
        public static string Destination(int field,Func<int,string> location)
        {
            if(field>=9000)return WorldMap;
            string name=location==null?null:location(field);
            return String.IsNullOrEmpty(name)?null:name;
        }
        public const string MenuExit="Exit with a destination menu";
        public const string MenuAvailability="the destination is chosen from the menu the game shows";
        public const string PossibleAvailability="the only place this exit can lead; the game decides whether it opens now";
        // Split targets keep the region's NativeId; the Variant is the place's first native field + 1.
        private const int MenuPart=9999;
        // Fields are the real native destinations a part proves now (Story routing); Variant is its stable
        // split identity (first field + 1, or the menu/fallback variant) and never a destination.
        private sealed class Part{public string Name,Label,Availability;public int Variant;public bool IsMenu;public List<int> Fields=new List<int>();public List<NavigationPoint> Points=new List<NavigationPoint>();public HashSet<NavigationPoint> PointSet=new HashSet<NavigationPoint>();public Dictionary<int,NavigationPoint[]> Destinations=new Dictionary<int,NavigationPoint[]>();}
        // Names presented exits from their own current proof and splits a region whose approaches lead
        // to different named places into one target per place. A split target keeps the region as its
        // physical identity (native trigger, exit observation, Story wrapping) and only that place's
        // approaches. Exits without a proven, named destination keep the presented side label.
        public static List<NavigationTarget> Apply(IList<NavigationTarget> exits,Func<NavigationTarget,FieldExitRoute> routeOf,Func<NavigationTarget,string> sideOf)
        {return Apply(exits,routeOf,sideOf,null);}
        // An exit whose proven place is the current place (a maze loop or a reload of this screen) says so.
        public static List<NavigationTarget> Apply(IList<NavigationTarget> exits,Func<NavigationTarget,FieldExitRoute> routeOf,Func<NavigationTarget,string> sideOf,string currentLocation)
        {
            var result=new List<NavigationTarget>();var sides=new Dictionary<NavigationTarget,string>();var named=new HashSet<NavigationTarget>();
            foreach(var exit in exits) {
                string side=sideOf==null?null:sideOf(exit);if(String.IsNullOrEmpty(side))side="Exit";
                var route=routeOf==null?null:routeOf(exit);
                var parts=Parts(exit,route);
                if(route!=null && route.RequireInwardApproach && !parts.Exists(part=>part.Points.Count>0)) {
                    exit.ApproachCandidates=new NavigationPoint[0];
                    if(String.IsNullOrEmpty(exit.Availability))exit.Availability="current doorway route unavailable";
                }
                if(parts.Count==0){result.Add(exit);sides[exit]=side;continue;}
                foreach(var part in parts) {
                    var target=parts.Count==1 && !part.IsMenu?exit:Split(exit,part);
                    target.SelectDestinationInMenu=part.IsMenu;
                    target.RequireInwardApproach=route.RequireInwardApproach && !part.IsMenu && part.Points.Count>0;
                    target.Label=part.Label??"Exit to "+part.Name+(!String.IsNullOrEmpty(currentLocation) && part.Name==currentLocation?", this same area":"");
                    if(part.Availability!=null)target.Availability=String.IsNullOrEmpty(target.Availability)?part.Availability:target.Availability+"; "+part.Availability;
                    if(part.Points.Count!=0){target.ApproachCandidates=part.Points.ToArray();target.Position=part.Points[0];}
                    // The actual current proof can come from a requested
                    // helper even when the old direct-jump hints were empty.
                    // Naming-only fallbacks contribute no Story edge.
                    target.NextFields=part.Fields.ToArray();
                    target.ExitContinuation=!part.IsMenu && route.Continuation!=null && part.Fields.Contains(route.Continuation.FirstField)?route.Continuation:null;
                    target.DestinationApproaches=FieldExitDestinations.Copy(part.Destinations);
                    result.Add(target);sides[target]=side;named.Add(target);
                }
            }
            Disambiguate(result,sides,named);
            return result;
        }
        private static List<Part> Parts(NavigationTarget exit,FieldExitRoute route)
        {
            var parts=new List<Part>();if(route==null)return parts;
            if(exit.ApproachCandidates==null || exit.ApproachCandidates.Length==0)return Fallback(route,parts);
            // A doorway can have hundreds of approved points. Index the current
            // presentation once, preserving the first occurrence's native rank.
            var approachOrder=new Dictionary<NavigationPoint,int>();
            for(int i=0;i<exit.ApproachCandidates.Length;i++)if(!approachOrder.ContainsKey(exit.ApproachCandidates[i]))approachOrder.Add(exit.ApproachCandidates[i],i);
            var menu=new List<NavigationPoint>();var menuSet=new HashSet<NavigationPoint>();
            foreach(var point in route.Menu??new NavigationPoint[0])if(approachOrder.ContainsKey(point) && menuSet.Add(point))menu.Add(point);
            var fields=new List<int>(route.Approaches.Keys);fields.Sort();
            foreach(int field in fields) {
                string name;if(!route.Names.TryGetValue(field,out name) || String.IsNullOrEmpty(name))continue;
                var points=new List<NavigationPoint>();var pointSet=new HashSet<NavigationPoint>();
                foreach(var point in route.Approaches[field]??new NavigationPoint[0])if(approachOrder.ContainsKey(point) && pointSet.Add(point))points.Add(point);
                if(points.Count==0)continue;
                Part part=null;foreach(var existing in parts)if(existing.Name==name)part=existing;
                if(part==null){part=new Part{Name=name,Variant=field+1};parts.Add(part);}
                part.Fields.Add(field);part.Destinations[field]=points.ToArray();foreach(var point in points)if(part.PointSet.Add(point))part.Points.Add(point);
                uint buttons;string button=route.Buttons.TryGetValue(field,out buttons)?Button(buttons):null;
                if(button!=null && part.Label==null)part.Label="Exit to "+name+", press "+button+(route.Prompted.Contains(field)?", then it asks":"");
                else if(route.Prompted.Contains(field) && part.Label==null)part.Label="Exit to "+name+", asks first";
            }
            if(menu.Count!=0) {
                var part=new Part{Name=route.Function??MenuExit,Label=route.Function??MenuExit,Availability=MenuAvailability,Variant=MenuPart+1,IsMenu=true};part.Points.AddRange(menu);
                var offered=new List<int>(route.MenuApproaches.Keys);offered.Sort();
                foreach(int field in offered) {
                    string name;if(!route.Names.TryGetValue(field,out name) || String.IsNullOrEmpty(name))continue;
                    var offeredPoints=new HashSet<NavigationPoint>(route.MenuApproaches[field]);
                    var points=new List<NavigationPoint>();foreach(var point in menu)if(offeredPoints.Contains(point))points.Add(point);
                    if(points.Count!=0){part.Fields.Add(field);part.Destinations[field]=points.ToArray();}
                }
                string press=Button(route.MenuButtons);if(press!=null)part.Availability="press "+press+" there; "+part.Availability;
                parts.Add(part);
            }
            if(parts.Count==0)Fallback(route,parts);
            // Keep the candidate order (nearest/approved first) within each place.
            foreach(var part in parts)part.Points.Sort((a,b)=>approachOrder[a].CompareTo(approachOrder[b]));
            return parts;
        }
        // Nothing proven now (or no approach is enabled): a reviewed function, or the one place any native
        // branch can reach, still names the exit. Several possible places are never narrowed to one.
        private static List<Part> Fallback(FieldExitRoute route,List<Part> parts)
        {
            if(route.Function!=null)parts.Add(new Part{Name=route.Function,Label=route.Function,Variant=MenuPart+1});
            else if(route.PossibleNames.Count==1)parts.Add(new Part{Name=route.PossibleNames[0],Availability=PossibleAvailability,Variant=MenuPart+2,
                Label=route.ConfirmPrompt?"Exit to "+route.PossibleNames[0]+", press Confirm":null});
            return parts;
        }
        // The game's control name for an EventInput mask (Confirm before the other controls it accepts).
        public static string Button(uint mask)
        {
            if((mask&0x20000u)!=0)return "Confirm";if((mask&0x10000u)!=0)return "Cancel";if((mask&0x1000000u)!=0)return "Menu";
            if((mask&0x80000u)!=0)return "Special";return mask==0?null:"the button the game asks for";
        }
        private static NavigationTarget Split(NavigationTarget exit,Part part)
        {
            var physical=exit.PhysicalIdentity;
            return new NavigationTarget{Id=new TargetIdentity(exit.Id.Kind,exit.Id.MapKey,exit.Id.NativeId,part.Variant),PhysicalId=physical,Category=exit.Category,Availability=exit.Availability,
                Evidence=exit.Evidence,Generation=exit.Generation,IsInteractive=exit.IsInteractive,NativeLocationId=exit.NativeLocationId,UseApprovedApproaches=exit.UseApprovedApproaches,
                InteractionUid=exit.InteractionUid,InteractionRegion=exit.InteractionRegion==null?null:(NavigationPoint[])exit.InteractionRegion.Clone(),UseViewingPoint=exit.UseViewingPoint,
                StoryMilestones=exit.StoryMilestones==null?null:(int[])exit.StoryMilestones.Clone(),NextFields=exit.NextFields==null?null:(int[])exit.NextFields.Clone(),
                StoryAction=exit.StoryAction,StoryStepMilestone=exit.StoryStepMilestone,StoryVerb=exit.StoryVerb};
        }
        // Equal labels: add the presented screen side when that separates them, then a doorway number in
        // native region order. Never ids or letters.
        private static void Disambiguate(List<NavigationTarget> targets,Dictionary<NavigationTarget,string> sides,HashSet<NavigationTarget> named)
        {
            var groups=new Dictionary<string,List<NavigationTarget>>(StringComparer.Ordinal);
            foreach(var target in targets) {
                string label=named.Contains(target)?target.Label:target.Label=sides[target];
                List<NavigationTarget> group;if(!groups.TryGetValue(label,out group)){group=new List<NavigationTarget>();groups[label]=group;}
                group.Add(target);
            }
            foreach(var pair in groups) {
                var group=pair.Value;if(group.Count<2)continue;
                bool isNamed=named.Contains(group[0]);
                var bySide=new Dictionary<string,List<NavigationTarget>>(StringComparer.Ordinal);
                foreach(var target in group) {
                    string side=isNamed?FieldExitLabel.Phrase(sides[target]):null;string key=side??"";
                    List<NavigationTarget> same;if(!bySide.TryGetValue(key,out same)){same=new List<NavigationTarget>();bySide[key]=same;}
                    same.Add(target);
                }
                foreach(var side in bySide) {
                    var same=side.Value;
                    same.Sort((a,b)=>{int c=a.PhysicalIdentity.NativeId.CompareTo(b.PhysicalIdentity.NativeId);return c!=0?c:a.Id.NativeId.CompareTo(b.Id.NativeId);});
                    for(int i=0;i<same.Count;i++) {
                        string label=pair.Key;
                        if(side.Key.Length!=0 && bySide.Count>1 || side.Key.Length!=0 && same.Count>1)label+=", "+side.Key;
                        if(same.Count>1)label+=", doorway "+(i+1);
                        same[i].Label=label;
                    }
                }
            }
        }
    }
}
