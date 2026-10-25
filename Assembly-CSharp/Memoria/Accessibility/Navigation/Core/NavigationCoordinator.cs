using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.Navigation
{
    public enum MovementOutcome {None,Moving,ReadyToInteract,ApproachReached,ArrivedExit,TurnNeeded,Interrupted,TargetMoved,Unavailable,RouteChanged}
    public sealed class MovementObservation
    {
        public MovementOutcome Outcome;public StopReason Reason;public string Evidence,Notice;
        public bool IsPlanning,RouteAvailable;public RouteFailure? RouteFailure;public NavigationPoint? RoutePoint;public RoutePlan PlannedRoute;
        public bool IsTerminal {get{return Outcome!=MovementOutcome.None && Outcome!=MovementOutcome.Moving;}}
    }
    public enum RouteOperation {None,PlanGuidance,StartAutomatic,ReplanAutomatic}
    public sealed class NavigationUpdate
    {
        public NavigationContext Context;public RouteDecision Session;public NavigationTarget Selected;
        public NavigationCategory Category;public RouteOperation RouteOperation;public bool RequestStatus,RequestRepeat,GuidanceEnabled,MenuOpened,IsPlanning;
        public MovementObservation Movement;public string ControllerNaming="Xbox";
        public readonly IList<string> Messages=new List<string>();public readonly IList<string> Diagnostics=new List<string>();
    }
    public sealed class NavigationCoordinator
    {
        public readonly NavigationSession Session;private TargetIdentity _selected;private bool _hasSelection,_lost;
        private NavigationCategory _category;private string _map,_save;private TransportIdentity _transport;
        public NavigationCoordinator():this(new NavigationSession()){}public NavigationCoordinator(NavigationSession session){Session=session;}
        public NavigationUpdate Tick(NavigationContext context,NavigationCommand[] commands,CatalogResult catalog,MovementObservation movement)
        {
            context=context??new NavigationContext();catalog=catalog??new CatalogResult();commands=commands??new NavigationCommand[0];
            var update=new NavigationUpdate{Context=context,Category=_category,Movement=movement,IsPlanning=movement!=null && movement.IsPlanning};
            RouteMode old=Session.Mode;var decision=Session.Update(context,NavigationSignal.None);
            if(context.CanControl && (_map!=context.LogicalMapKey || _save!=context.SaveIdentity || !_transport.Equals(context.Transport))) {bool followStory=_map!=null && _category==NavigationCategory.Story;_map=context.LogicalMapKey;_save=context.SaveIdentity;_transport=context.Transport;_category=followStory?NavigationCategory.Story:_map.StartsWith("world:")?NavigationCategory.Towns:NavigationCategory.Exits;_hasSelection=_lost=false;}
            if(!_hasSelection)SelectFirst(catalog);
            update.Selected=_hasSelection?catalog.Find(_selected):null;
            if(context.CanControl && _hasSelection && update.Selected==null && _category==NavigationCategory.Story) {
                Merge(ref decision,Session.Update(context,NavigationSignal.TargetChanged));_hasSelection=false;SelectFirst(catalog);update.Selected=_hasSelection?catalog.Find(_selected):null;
                update.Messages.Add(SelectionText(catalog));
            }
            if(update.Selected!=null && update.Selected.Category!=_category){_category=update.Selected.Category;update.Messages.Add(SelectionText(catalog));}
            if(context.CanControl && _hasSelection && update.Selected==null) {
                Merge(ref decision,Session.Update(context,NavigationSignal.TargetLost));
                if(!_lost)update.Messages.Add("Selected target is no longer presented. Choose another target.");_lost=true;
            }else if(update.Selected!=null)_lost=false;
            if(old!=RouteMode.Paused && Session.Mode==RouteMode.Paused)update.Messages.Add("Navigation paused. I resumes guidance; P resumes automatic movement.");
            if(decision.RequireReplan && update.Selected!=null)update.RouteOperation=Session.Mode==RouteMode.Moving?RouteOperation.ReplanAutomatic:RouteOperation.PlanGuidance;
            if(movement!=null && old==RouteMode.Guiding && Session.Mode==RouteMode.Guiding && OnlyInformationalCommands(commands) && !movement.IsPlanning && (movement.Outcome==MovementOutcome.TargetMoved || movement.Outcome==MovementOutcome.RouteChanged)) {
                update.RouteOperation=RouteOperation.PlanGuidance;
                update.Messages.Add("Route changed. Refreshing directions.");
            }
            if(movement!=null && old==RouteMode.Moving && Session.Mode==RouteMode.Moving && OnlyInformationalCommands(commands) && !movement.IsPlanning) {
                NavigationSignal signal=NavigationSignal.None;
                switch(movement.Outcome) {
                    case MovementOutcome.ReadyToInteract:signal=NavigationSignal.Arrived;update.Messages.Add("Ready to interact with "+Label(update.Selected)+". Press Confirm.");break;
                    case MovementOutcome.TurnNeeded:signal=NavigationSignal.Arrived;update.Messages.Add("Approach reached. Turn to face "+Label(update.Selected)+"; use Confirm when ready.");break;
                    case MovementOutcome.ApproachReached:signal=NavigationSignal.Arrived;update.Messages.Add(movement.PlannedRoute!=null && movement.PlannedRoute.ViewingPoint?"Reached a viewing point for "+Label(update.Selected)+". This object has no interaction.":"Approach reached: "+Label(update.Selected)+". Use the ordinary game action to continue.");break;
                    case MovementOutcome.ArrivedExit:signal=NavigationSignal.Arrived;update.Messages.Add("Selected exit reached.");break;
                    case MovementOutcome.Interrupted:signal=NavigationSignal.RouteFailed;update.Messages.Add("Automatic movement stopped: "+(movement.Evidence??movement.Reason.ToString())+".");break;
                    case MovementOutcome.TargetMoved:signal=NavigationSignal.StartAutomatic;update.RouteOperation=RouteOperation.ReplanAutomatic;update.Messages.Add((update.Selected!=null && update.Selected.Id.Kind==TargetKind.Vehicle?"Vehicle":"Target")+" moved. Finding a fresh approach.");break;
                    case MovementOutcome.Unavailable:signal=NavigationSignal.RouteFailed;update.Messages.Add("Automatic route unavailable: "+(movement.Evidence??"native route could not be established")+".");break;
                }
                if(signal!=NavigationSignal.None)Merge(ref decision,Session.Update(context,signal));
            }
            if(movement!=null && !String.IsNullOrEmpty(movement.Notice))update.Messages.Add(movement.Notice);
            foreach(var command in commands) {
                NavigationSignal signal=NavigationSignal.None;
                if(command==NavigationCommand.PreviousCategory || command==NavigationCommand.NextCategory) {
                    var categories=Categories(context);int index=Array.IndexOf(categories,_category);_category=categories[(index+(command==NavigationCommand.NextCategory?1:categories.Length-1))%categories.Length];
                    _hasSelection=false;SelectFirst(catalog);update.Selected=_hasSelection?catalog.Find(_selected):null;signal=NavigationSignal.TargetChanged;update.Messages.Add(SelectionText(catalog));
                }else if(command==NavigationCommand.PreviousTarget || command==NavigationCommand.NextTarget) {
                    var choices=Choices(catalog);int index=-1;for(int i=0;i<choices.Count;i++)if(_hasSelection && choices[i].Id.Equals(_selected))index=i;
                    if(choices.Count>0){if(index<0)index=command==NavigationCommand.NextTarget?choices.Count-1:0;index=(index+(command==NavigationCommand.NextTarget?1:choices.Count-1)+choices.Count)%choices.Count;_selected=choices[index].Id;_hasSelection=true;_lost=false;}
                    update.Selected=_hasSelection?catalog.Find(_selected):null;signal=NavigationSignal.TargetChanged;update.Messages.Add(SelectionText(catalog));
                }else if(command==NavigationCommand.ToggleGuidance || command==NavigationCommand.StartGuidance) {
                    if(update.Selected==null){update.Messages.Add(SelectionText(catalog));continue;}
                    signal=command==NavigationCommand.StartGuidance?NavigationSignal.StartGuidance:NavigationSignal.ToggleGuidance;
                }else if(command==NavigationCommand.ToggleAutomatic || command==NavigationCommand.StartAutomatic) {
                    if(command==NavigationCommand.ToggleAutomatic && Session.Mode==RouteMode.Moving)signal=NavigationSignal.Stop;
                    else if(update.Selected==null){update.Messages.Add(SelectionText(catalog));continue;}else signal=NavigationSignal.StartAutomatic;
                }else if(command==NavigationCommand.Stop)signal=NavigationSignal.Stop;
                else if(command==NavigationCommand.ManualMovement)signal=NavigationSignal.ManualMovement;
                else if(command==NavigationCommand.Confirm)signal=NavigationSignal.Confirm;
                else if(command==NavigationCommand.MenuOpened){
                    if(_hasSelection && catalog.Find(_selected)==null){_hasSelection=false;SelectFirst(catalog);update.Selected=_hasSelection?catalog.Find(_selected):null;_lost=false;}
                    signal=NavigationSignal.OpenMenu;update.MenuOpened=true;update.Messages.Add("Navigation menu. "+SelectionText(catalog)+" "+MenuHelp(update.ControllerNaming));
                }
                else if(command==NavigationCommand.Disconnected) {var disconnected=new NavigationContext{Generation=context.Generation,Foreground=context.Foreground,GateReason=GateReason.Disconnected};Merge(ref decision,Session.Update(disconnected,NavigationSignal.None));update.Messages.Add("Controller disconnected. Navigation paused.");}
                else if(command==NavigationCommand.Status)update.RequestStatus=true;
                else if(command==NavigationCommand.Repeat)update.RequestRepeat=true;
                if(signal!=NavigationSignal.None) {
                    bool wasAutomatic=Session.Mode==RouteMode.Moving;Merge(ref decision,Session.Update(context,signal));
                    if(signal==NavigationSignal.StartAutomatic){update.RouteOperation=RouteOperation.StartAutomatic;update.Messages.Add("Finding an automatic route to "+Label(update.Selected)+".");}
                    else if(signal==NavigationSignal.StartGuidance || signal==NavigationSignal.ToggleGuidance){if(Session.Mode==RouteMode.Guiding)update.RouteOperation=RouteOperation.PlanGuidance;update.Messages.Add(Session.GuidanceEnabled?"Guidance on.":"Guidance off.");}
                    else if(signal==NavigationSignal.Stop)update.Messages.Add("Navigation stopped.");
                    else if(signal==NavigationSignal.ManualMovement && wasAutomatic)update.Messages.Add("Automatic movement stopped. "+(Session.GuidanceEnabled?"Guidance on.":"Guidance off."));
                    else if(signal==NavigationSignal.Confirm && wasAutomatic)update.Messages.Add("Navigation paused for interaction.");
                }
            }
            foreach(string diagnostic in catalog.Diagnostics)update.Diagnostics.Add(diagnostic);
            if(update.Movement!=null && update.Movement.RouteAvailable && !GuidanceRouteValidation.Matches(update.Movement.PlannedRoute,context,update.Selected)) {
                update.Movement.RouteAvailable=false;update.Movement.RoutePoint=null;
            }
            update.Category=_category;update.Session=decision;update.GuidanceEnabled=Session.GuidanceEnabled;
            return update;
        }
        private void SelectFirst(CatalogResult catalog){foreach(var target in catalog.Targets)if(target.Category==_category){_selected=target.Id;_hasSelection=true;break;}}
        private List<NavigationTarget> Choices(CatalogResult catalog){var result=new List<NavigationTarget>();foreach(var target in catalog.Targets)if(target.Category==_category)result.Add(target);return result;}
        private string SelectionText(CatalogResult catalog){var choices=Choices(catalog);if(choices.Count==0)return catalog.UnavailableReason??("No "+_category.ToString().ToLowerInvariant()+" currently presented.");for(int i=0;i<choices.Count;i++)if(choices[i].Id.Equals(_selected))return _category+". "+SelectionLabel(choices[i])+", "+(i+1)+" of "+choices.Count+".";return "Selected target unavailable.";}
        public static string SelectionLabel(NavigationTarget target){return target.Label+(target.Evidence==VisibilityEvidence.ObservedFieldLandmark?", last seen":"");}
        private static string Label(NavigationTarget target){return target==null?"the selected target":target.Label;}
        private static NavigationCategory[] Categories(NavigationContext context){return context.LogicalMapKey!=null && context.LogicalMapKey.StartsWith("world:")?new[]{NavigationCategory.Story,NavigationCategory.Towns,NavigationCategory.Regions,NavigationCategory.Vehicles}:new[]{NavigationCategory.Story,NavigationCategory.Exits,NavigationCategory.People,NavigationCategory.Objects,NavigationCategory.Items};}
        public static string MenuHelp(string naming){return naming=="PlayStation"?"L1 and R1 categories; up and down targets; left status; right repeat; Cross guidance; Square auto-walk; Circle stop; stick-click closes.":naming=="Generic"?"Left and right shoulder categories; up and down targets; left status; right repeat; bottom face button guidance; left face button auto-walk; right face button stop; stick-click closes.":"LB and RB categories; up and down targets; left status; right repeat; A guidance; X auto-walk; B stop; stick-click closes.";}
        private static void Merge(ref RouteDecision previous,RouteDecision next){next.ReleaseMovement|=previous.ReleaseMovement;next.RequireReplan|=previous.RequireReplan;previous=next;}
        private static bool OnlyInformationalCommands(NavigationCommand[] commands)
        {
            foreach(var command in commands)if(command!=NavigationCommand.Status && command!=NavigationCommand.Repeat && command!=NavigationCommand.DecreaseInterval && command!=NavigationCommand.IncreaseInterval)return false;
            return true;
        }
    }
}
