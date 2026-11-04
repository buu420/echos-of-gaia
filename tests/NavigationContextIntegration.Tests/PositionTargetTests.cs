using System;
using Memoria.Accessibility.Navigation;
internal static class PositionTargetTests
{
    private static int _visit;
    private sealed class Fixture
    {
        public NavigationContext Context;
        public FieldGeometrySnapshot Geometry;
        public EventEngine Engine;
        public Fixture()
        {
            Context=new NavigationContext{Generation=++_visit,LogicalMapKey=OpeningRooftopEvidence.Map,SaveIdentity="position-"+_visit,PlayerIdentity=21,Foreground=true,CanControl=true,Position=P(1500,100),Transport=new TransportIdentity(TransportKind.Foot,21,0)};
            Geometry=new FieldGeometrySnapshot{Generation=Context.Generation,Fingerprint="floor",Radius=10,ActiveTriangle=0,Triangles=new[]{new FieldTriangle{Id=0,Vertices=new[]{P(0,0),P(2000,0),P(0,4000)}}}};
            Engine=new EventEngine();UnityEngine.Object.Set(Engine);UnityEngine.Object.Set(new FieldMap{scene=new object()});
            NativeFieldTargets.PositionGeometry=Geometry;NativeFieldTargets.PositionPresented=p=>true;NativeFieldTargets.PositionGeometryReads=0;
            NativeStoryTargets.PositionGoal=new OpeningPositionGoal{Field=116,Phase=4,Milestone=1155,Map=Context.LogicalMapKey,Axis=OpeningGoalAxis.X,Maximum=900,Action="Follow the boy across the rooftop"};
            NativeStoryTargets.PositionGoalReads=0;
        }
        public NavigationTarget Read(){var catalog=new CatalogResult();Require(NativeOpeningPositionTargets.TryAppend(Context,catalog),"native position goal was not appended");Require(catalog.Targets.Count==1,"position goal duplicated a category");return catalog.Targets[0];}
    }
    private static NavigationPoint P(float x,float z){return new NavigationPoint(x,0,z);}
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
    public static void CurrentPositionGoalProvidesAWalkableStoryTarget()
    {
        var f=new Fixture();var target=f.Read();Require(target.Category==NavigationCategory.Story && target.PhysicalId.HasValue,"walking goal became an information-only target");
        Require(target.ApproachCandidates.Length>0 && target.ApproachCandidates.Length<=16,"unbounded or absent floor approaches");
        foreach(var point in target.ApproachCandidates)Require(point.X<=892,"native arrival tolerance can stop before the story boundary");
        Require(new FieldRoutePlanner().Plan(f.Context,target,f.Geometry).Success,"native adapter target cannot be planned");
    }
    public static void ManualMovementKeepsTheSameRooftopAnchors()
    {
        var f=new Fixture();var first=f.Read();f.Context.Position=P(1400,200);var second=f.Read();
        Require(first.Id.Equals(second.Id) && first.Position.Equals(second.Position),"ordinary movement rotated the synthetic story goal");
    }
    public static void CurrentHeightGoalPlansToTheStairSurface()
    {
        var f=new Fixture();f.Context.LogicalMapKey=OpeningCastleStory.HallMap;f.Context.PlayerIdentity=3;f.Context.Position=new NavigationPoint(10,50,10);
        f.Geometry=new FieldGeometrySnapshot{Generation=f.Context.Generation,Fingerprint="stairs",Radius=2,ActiveTriangle=0,Triangles=new[]{new FieldTriangle{Id=0,Vertices=new[]{new NavigationPoint(0,0,0),new NavigationPoint(200,1000,0),new NavigationPoint(0,0,200)}}}};
        NativeFieldTargets.PositionGeometry=f.Geometry;
        NativeStoryTargets.PositionGoal=new OpeningPositionGoal{Field=153,Map=f.Context.LogicalMapKey,Phase=6,Milestone=1190,Axis=OpeningGoalAxis.Y,Minimum=450,Action="Climb the stairs to the royal box"};
        var target=f.Read();Require(target.PhysicalId.HasValue && target.ApproachCandidates.Length>0,"height goal lost its native floor approaches");
        var route=new FieldRoutePlanner().Plan(f.Context,target,f.Geometry);
        Require(route.Success && route.ApproachPoint.Y>=490-.01f,"actual position adapter planned below the vertical wait/arrival margin");
    }
    public static void LosingOnlyThePlannedEndpointReplacesTheOwnedTarget()
    {
        var f=new Fixture();f.Read();f.Context.Position=P(1100,56.3157f);var first=f.Read();
        var route=new FieldRoutePlanner().Plan(f.Context,first,f.Geometry);
        Require(route.Success && !route.ApproachPoint.Equals(first.Position),"fixture did not select a secondary approach");
        NativeFieldTargets.PositionPresented=p=>!p.Equals(route.ApproachPoint);var second=f.Read();
        Require(second.ApproachCandidates.Length>0 && first.Position.Equals(second.Position),"fixture removed all approaches or the primary anchor");
        Require(!first.Id.Equals(second.Id),"partially lost presentation retained a route to an excluded endpoint");
        var fresh=new FieldRoutePlanner().Plan(f.Context,second,f.Geometry);
        Require(fresh.Success && !fresh.ApproachPoint.Equals(route.ApproachPoint),"replacement target still plans to the excluded endpoint");
        Require(second.Id.Equals(f.Read().Id),"unchanged presentation kept rotating target identity");
    }
    public static void SatisfiedOrUnpresentedGoalReplacesTheOwnedTargetWithInformation()
    {
        var f=new Fixture();var first=f.Read();f.Context.Position=P(800,100);var ended=f.Read();
        Require(!ended.PhysicalId.HasValue && !ended.Id.Equals(first.Id),"satisfied goal retained an owned walking target");
        f=new Fixture();first=f.Read();NativeFieldTargets.PositionPresented=p=>false;ended=f.Read();
        Require(!ended.PhysicalId.HasValue && ended.ApproachCandidates.Length==0 && !ended.Id.Equals(first.Id),"unpresented floor retained walking points");
    }
    public static void BlockingNativeRegionCannotBecomeARooftopGoal()
    {
        var f=new Fixture();f.Engine.PositionTouch=(x,z)=>new Quad();var target=f.Read();
        Require(!target.PhysicalId.HasValue,"position guidance led into an unrelated native region");
    }
    public static void BattleAndNoControlFramesDoNoPositionWork()
    {
        var f=new Fixture();f.Context.CanControl=false;var catalog=new CatalogResult();
        Require(!NativeOpeningPositionTargets.TryAppend(f.Context,catalog),"no-control frame received a walking target");
        Require(NativeStoryTargets.PositionGoalReads==0 && NativeFieldTargets.PositionGeometryReads==0,"battle/no-control frame prepared position geometry");
    }
}
