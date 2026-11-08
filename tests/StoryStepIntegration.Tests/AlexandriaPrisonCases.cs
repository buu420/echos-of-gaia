using System;
using System.Linq;
using Memoria.Accessibility.Navigation;

internal static partial class Program
{
    private static void AlexandriaPrisonLadder()
    {
        const string map = "field:1209:FBG_N02_ALXC_MAP857B_AC_JIL_2";
        const string ladder = "1658b7009de05a0accdbaa76aa36f02a800ebf29beb8f56a77251acc6b654b78";
        const string steiner = "93203b9eaf7e82cd359dc73243ab197de84da932efdbf9b2699861bd16e1f6d3";
        var engine = new EventEngine();
        engine.Objects[5] = new Obj { uid = 5, sid = 5 };
        engine.Objects[9] = new Obj { uid = 9, sid = 9 };
        engine.Objects[12] = new Obj { uid = 12, sid = 12 };
        Func<int, string> hashes = sid => sid == 5 ? ladder : sid == 9 ? steiner : null;
        SetCounter(5030);
        NativeStorySteps.RefreshTransitState();

        var lower = new NavigationPoint(-4928, 4621, 1710);
        var upper = new NavigationPoint(-4928, 9171, 490);
        StoryStep Find(NavigationPoint position, int player = 9, int progress = 5030)
        {
            return NativeStorySteps.Candidates(engine, 1209, progress, 5, ladder, hashes, player, position, 5)
                .SingleOrDefault(step => step.Kind == StoryStepKind.RegionConfirm);
        }
        var climb = Find(lower);
        Check(climb != null && climb.TargetLabel == "Prison ladder" && climb.Action.Contains("hold Up") &&
            climb.Milestone == 5050 && climb.Effects.Single().Offset == 95,
            "below the prison ledge Story identifies the native ladder Confirm action and held Up input");
        Check(Find(upper) == null && Find(lower, 12) == null && Find(lower, progress: 5050) == null,
            "the climb guidance retires above the ladder, outside its counter window, or for another actor");

        var catalog = new StoryTargetCatalog(new[] { new StoryStage { Milestone = 5050, GoalFields = new[] { 1211 }, Goal = "Alexandria" } }, NativeStorySteps.Transit);
        catalog.SetTransitEvaluator(NativeStorySteps.EvaluateTransit);
        int? NextStoryTarget(NavigationPoint position, StoryStep step)
        {
            var context = new NavigationContext { LogicalMapKey = map, Generation = 1, StoryProgress = 5030, PlayerIdentity = 9, Position = position };
            var targets = new CatalogResult();
            targets.Targets.Add(new NavigationTarget { Id = new TargetIdentity(TargetKind.FieldActor, map, 2), Category = NavigationCategory.People, Generation = 1, Label = "Guard" });
            targets.Targets.Add(new NavigationTarget { Id = new TargetIdentity(TargetKind.FieldExit, map, 4), Category = NavigationCategory.Exits, Generation = 1, Label = "Tower exit", NextFields = new[] { 1211 } });
            if (step != null)
                targets.Targets.Add(new NavigationTarget { Id = new TargetIdentity(TargetKind.FieldActor, map, 5), Category = NavigationCategory.Objects, Generation = 1,
                    Label = step.TargetLabel, StoryAction = step.Action, StoryStepMilestone = step.Milestone, IsInteractive = true, InteractionUid = 5 });
            catalog.Append(context, targets, 1209);
            return targets.Targets.SingleOrDefault(target => target.Category == NavigationCategory.Story)?.PhysicalId?.NativeId;
        }
        Check(NextStoryTarget(lower, climb) == 5, "Story routes to the prison ladder before the upper exit");
        Check(NextStoryTarget(upper, Find(upper)) == 4, "after climbing, Story follows the native exit to the tower");
    }
}
