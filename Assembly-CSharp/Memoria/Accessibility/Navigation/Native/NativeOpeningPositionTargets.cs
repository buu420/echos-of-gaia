using System;
using System.Collections.Generic;
using UnityEngine;
namespace Memoria.Accessibility.Navigation
{
    // These are current story walking goals, not undiscovered people or props.
    // The native script still takes control when its ordinary position wait ends.
    public static class NativeOpeningPositionTargets
    {
        private static string _key;
        private static int _sceneIdentity;
        private static bool _expanded;
        private static readonly NavigationPoint[] Empty=new NavigationPoint[0];
        private static NavigationPoint[] _candidates=Empty;
        private static string _presentedScope;
        private static int _presentedSceneIdentity,_targetIdentity=-1000000000;
        private static NavigationPoint[] _presentedPoints=Empty;
        public static bool TryAppend(NavigationContext context,CatalogResult catalog)
        {
            if(context==null || catalog==null || !context.Foreground || !context.CanControl)return false;
            var goal=NativeStoryTargets.CurrentPositionGoal(context);
            if(goal==null){_key=null;_sceneIdentity=0;_candidates=Empty;_presentedScope=null;_presentedPoints=Empty;return false;}
            var field=UnityEngine.Object.FindObjectOfType<FieldMap>();var engine=UnityEngine.Object.FindObjectOfType<EventEngine>();
            var geometry=field==null || engine==null?null:NativeFieldTargets.ReadGeometryForStory(context);
            var points=new List<NavigationPoint>();
            string key=context.Generation+":"+context.LogicalMapKey+":"+context.SaveIdentity+":"+context.PlayerIdentity+":"+goal.Phase+":"+goal.Axis+":"+goal.Minimum+":"+goal.Maximum+":"+(geometry==null?"":geometry.Fingerprint)+":"+(field==null?-1:field.camIdx);
            int sceneIdentity=field==null || field.scene==null?0:System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(field.scene);
            if(geometry!=null && !goal.Satisfied(context.Position)) {
                if(_key!=key || _sceneIdentity!=sceneIdentity) {
                    _candidates=OpeningPositionApproaches.Read(goal,geometry,context.Position);_key=key;_sceneIdentity=sceneIdentity;_expanded=false;
                }
                // Retain the same floor anchors as the player walks. Only a real
                // geometry, phase or presentation change chooses different goals.
                points=Presented(goal,engine,field);
                if(points.Count==0 && !_expanded) {
                    _candidates=OpeningPositionApproaches.Read(goal,geometry,context.Position,false);_expanded=true;
                    points=Presented(goal,engine,field);
                }
            }
            // A planner can choose any approved point, not just Position. Any
            // change to that set releases the old owned route. Ordinary player
            // movement retains its identity and anchors while the set is equal.
            if(_presentedScope!=key || _presentedSceneIdentity!=sceneIdentity || !SamePoints(_presentedPoints,points)) {
                _targetIdentity--;_presentedScope=key;_presentedSceneIdentity=sceneIdentity;_presentedPoints=points.ToArray();
            }
            var id=new TargetIdentity(TargetKind.Story,context.LogicalMapKey,_targetIdentity);
            var target=new NavigationTarget{Id=id,Category=NavigationCategory.Story,Label=goal.Action,StoryAction=goal.Action,Generation=context.Generation,
                Position=points.Count>0?points[0]:context.Position,Evidence=VisibilityEvidence.ObservedRegion,ApproachCandidates=points.ToArray(),StoryMilestones=new[]{goal.Milestone}};
            if(points.Count>0)target.PhysicalId=new TargetIdentity(TargetKind.FieldActor,context.LogicalMapKey,4096+goal.Phase);
            else target.Availability=goal.Satisfied(context.Position)?"Continue the current scene.":"The next walking approach is not currently available on the presented floor.";
            catalog.Targets.Add(target);return true;
        }
        private static bool SamePoints(NavigationPoint[] previous,List<NavigationPoint> current)
        {
            if(previous.Length!=current.Count)return false;
            foreach(var point in previous)if(!current.Contains(point))return false;
            return true;
        }
        private static List<NavigationPoint> Presented(OpeningPositionGoal goal,EventEngine engine,FieldMap field)
        {
            var points=new List<NavigationPoint>();
            foreach(var point in _candidates)if(goal.Satisfied(point) && engine.NavigationTouchQuadAt(point.X,point.Z)==null && NativeFieldTargets.StorySurfacePresented(field,point)) {
                points.Add(point);if(points.Count==16)break;
            }
            return points;
        }
    }
}
