using System.Linq;
using Memoria.Accessibility.Navigation;

internal static partial class Program
{
    private static void AlexandriaTimedRescue()
    {
        var stages = new[] {
            new StoryStage { Milestone = 5070, Goal = "Alexandria", GoalFields = new[] { 1206 } },
            new StoryStage { Milestone = 5080, Goal = "Alexandria", GoalFields = new[] { 1205 } }
        };
        var catalog = new StoryTargetCatalog(stages, NativeStorySteps.Transit);
        catalog.SetPhaseSelector((counter, field, scripts) => NativeStorySteps.SelectPhase(counter, field, scripts));

        CheckRescueExit(catalog, 5050, 1212, 10, 1222, 0, "c115fdb3507245d43e96f9a2fb138dc7c5657b33dc005ce478b38570aae54f78");
        CheckRescueExit(catalog, 5050, 1222, 3, 1220, 1212, "a7475970471884400d07bc57676d6e9378f8ec43ed6739886562398fe5352f83");
        CheckRescueExit(catalog, 5050, 1221, 3, 1220, 0, "0357cafa3375c03cf0f56b370d95395c251cdace0c30cc90228e640c74194ca0");
        CheckRescueExit(catalog, 5050, 1220, 6, 1219, 1222, "977aa864396be009f0a8b3f393143feda3c1916942cf768014c732a5c3ed1eca");
        CheckRescueExit(catalog, 5050, 1219, 4, 1215, 1220, "4d141a584517e97d3f040a8ada3cb6fb3ec5ebcca32542daf27e81c277cc070e");
        CheckRescueExit(catalog, 5050, 1215, 5, 1214, 1219, "7d39b5641decdb40cc8c7a9c0065c9f245fe45e8ff27d8747e944b89305fd59f");
        CheckRescueExit(catalog, 5050, 1213, 11, 1214, 0, "dfad938d7edb7eaf811064d0ad7c7e02380dc64f3e0f38367db00766e6699c60");
        CheckRescueExit(catalog, 5050, 1214, 6, 1224, 1215, "b50eed4e5260a8407eaf920b1881fef8d4e96d7944d6b8f56beb5ab2f91cee81");
        CheckRescueExit(catalog, 5050, 1224, 7, 1206, 1207, "8945da164046d1767dd99652effe4350f5f3e579df005a703d19e9d868cf100e");
        CheckRescueExit(catalog, 5075, 1206, 13, 1202, 1224, "9ac0674bd6670cc96bd2b8ff9a5d61590c581d8d6349eb3636e4ad9effea260f");
        CheckRescueExit(catalog, 5075, 1202, 20, 1203, 1206, "fc461b5cda54b04fb5d93c00b0a9ce75b49eb3e9eef0a29d830406a2ebba6593");
        CheckRescueExit(catalog, 5075, 1203, 12, 1204, 1202, "db719fa893dd8f02193be49c484e85ae221f2c92106ed17f267570e89c2cedd4");
        CheckRescueExit(catalog, 5075, 1204, 8, 1205, 1203, "73e228ebce3ae3bf326155d21c9e7e1984cc70962d15aa6c10c9ee6979b2aa7d");
        CheckRescueExit(catalog, 5075, 1204, 9, 1205, 1203, "73e228ebce3ae3bf326155d21c9e7e1984cc70962d15aa6c10c9ee6979b2aa7d");
        CheckRescueExit(catalog, 5075, 1201, 8, 1204, 0, "ce13116caaf8f3879b369450e289526e7247e09d9ea27b1975138d0e8f5e0130");
    }

    private static void CheckRescueExit(StoryTargetCatalog catalog, int counter, int room, int uid, int forward, int back, string director)
    {
        var context = new NavigationContext { StoryProgress = counter, LogicalMapKey = "field:" + room, Generation = 1, CanControl = true, Foreground = true };
        NavigationTarget Exit(int destination, int variant)
        {
            var point = new NavigationPoint(variant, 0, 0);
            return new NavigationTarget {
                Id = new TargetIdentity(TargetKind.FieldExit, context.LogicalMapKey, uid, variant),
                Category = NavigationCategory.Exits, Generation = context.Generation, Label = "Exit to " + destination,
                Position = point, ApproachCandidates = new[] { point }, NextFields = new[] { destination },
                DestinationApproaches = new System.Collections.Generic.Dictionary<int, NavigationPoint[]> { { destination, new[] { point } } }
            };
        }
        var path = Exit(forward, 1);
        var choices = new CatalogResult();
        if (back != 0) choices.Targets.Add(Exit(back, 2));
        choices.Targets.Add(path);
        catalog.Append(context, choices, room, scripts: sid => sid == 0 ? director : null);
        var story = choices.Targets.SingleOrDefault(target => target.Category == NavigationCategory.Story);
        Check(story != null && story.PhysicalIdentity.Equals(path.Id) && story.NextFields.SequenceEqual(new[] { forward }),
            "timed Alexandria rescue takes native exit " + room + " to " + forward);
    }
}
