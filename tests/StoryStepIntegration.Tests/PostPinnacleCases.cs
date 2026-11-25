using System;
using System.Linq;
using Memoria.Accessibility.Navigation;

internal static partial class Program
{
    private static void PostPinnacleCases()
    {
        const int field = 1307;
        const int minister = 4;
        const int zidane = 14;
        const int progress = 5670;
        const string director = "7f24c40fdb798da400dab5107b3c9fb118484c58d90cf965a216a5d7c822b6e8";
        const string actor = "1c4daf0407e52763a559b58b50269505de248aadfe5165ee317d72337fc59fa5";
        const string player = "50b2c489df5e400f3722e509841a1beb369bc60f95500651fa70ed64133cd719";
        var engine = new EventEngine();
        foreach (int uid in new[] { 0, minister, zidane })
            engine.Objects[uid] = new Obj { sid = uid, uid = uid };
        Func<int, string> hashes = sid => sid == 0 ? director : sid == minister ? actor : sid == zidane ? player : null;

        SetCounter(progress);
        NativeStorySteps.RefreshTransitState();
        var step = NativeStorySteps.Candidates(engine, field, progress, minister, actor, hashes, zidane, uid: minister)
            .SingleOrDefault(candidate => candidate.Source == "reviewed-prefix");
        // The currently talkable actor reaches offset 114 before turning
        // toward Zidane or opening the native choice. No counter is written.
        var prefix = new ScriptStoryEvidence();
        prefix.Reached.Add(new StoryEffectSite(3, minister, 3, 114, minister));
        Check(StoryStepCatalog.HasEffect(step, prefix, hashes),
            "post-Pinnacle Story offers the fountain conversation before the player makes its native choice");

        Check(!NativeStorySteps.Candidates(engine, field, 5660, minister, actor, hashes, zidane, uid: minister)
            .Any(candidate => candidate.Source == "reviewed-prefix"),
            "the fountain departure conversation is unavailable before the minister returns");
        SetCounter(5680);
        Check(!NativeStorySteps.Candidates(engine, field, 5680, minister, actor, hashes, zidane, uid: minister)
            .Any(candidate => candidate.Source == "reviewed-prefix"),
            "the fountain departure conversation retires once accepted");

        QuMarshEntryCases();
    }

    private static void QuMarshEntryCases()
    {
        const int progress = 5690;
        SetCounter(progress);
        NativeStorySteps.RefreshTransitState();

        var entryRoutes = new[] {
            (Field: 650, Uid: 6, Next: 652, Director: "1eb37d5986d65a35ba9ce4e9e923e126e3d848f62cf183bfe704fc46810acaf2"),
            (Field: 651, Uid: 8, Next: 655, Director: "658b3d54eecf21906abdc9c4812002e025fc9c11f4a035491a6931db9bc59e0b"),
            (Field: 652, Uid: 10, Next: 655, Director: "c59af16b0f1501c0d930cabac1a9b0b660239750c6820c66fc09fb505696d6af"),
            (Field: 653, Uid: 9, Next: 655, Director: "ca54d1b52822af88b590b03ff4fa9c77c661285299646e45292fda78294600a1"),
            (Field: 654, Uid: 8, Next: 655, Director: "8267ebe0046478489c626e58d6fdfe9820dfdee428746dd64bc4bcaad9bd5d92")
        };
        foreach (var leg in entryRoutes)
        {
            var story = new StoryTargetCatalog(
                new[] { new StoryStage { Milestone = 5900, GoalFields = new[] { 1422 }, Goal = "Fossil Roo" } },
                NativeStorySteps.Transit);
            story.SetTransitEvaluator(NativeStorySteps.EvaluateTransit);
            story.SetPhaseSelector((counter, field, scripts) => NativeStorySteps.SelectPhase(counter, field, scripts));

            string map = "field:" + leg.Field + ":test";
            var point = new NavigationPoint(4, 0, 1);
            var context = new NavigationContext { LogicalMapKey = map, StoryProgress = progress, Generation = 1 };
            var target = new NavigationTarget {
                Id = new TargetIdentity(TargetKind.FieldExit, map, leg.Uid), Category = NavigationCategory.Exits,
                Position = point, ApproachCandidates = new[] { point }, Label = "Presented marsh path",
                IsInteractive = true, Generation = 1, NextFields = new[] { leg.Next }
            };
            var result = new CatalogResult();
            result.Targets.Add(target);
            story.Append(context, result, leg.Field, scripts: sid => sid == 0 ? leg.Director : null);
            Check(result.Targets.Any(row => row.Category == NavigationCategory.Story && row.PhysicalIdentity.Equals(target.Id)),
                "Qu’s Marsh Story follows the native route from field " + leg.Field + " to " + leg.Next);
        }
        Check(NativeStorySteps.SelectPhase(progress, 650, _ => "different script")?.InformationOnly == true,
            "Qu’s Marsh Story does not offer a reviewed route with an unknown director");
    }
}
