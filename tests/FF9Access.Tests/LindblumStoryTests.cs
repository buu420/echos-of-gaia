using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Memoria.Accessibility.Navigation;

namespace FF9Access.Tests
{
    internal static class LindblumStoryTests
    {
        public static void BlueNarcissStoryFollowsTheLiftTrolleyAndHarbor()
        {
            var catalog = ReadCatalog(9370, 9400, "lindblum-return-reviewed-blue-narciss-");
            ExpectRouteAt(catalog, 9370, 2169, 12, 2170);
            ExpectRouteAt(catalog, 9370, 2170, 8, 2168, (7, 2150));
            ExpectRouteAt(catalog, 9370, 2168, 6, 2151, (4, 2170));
            ExpectRouteAt(catalog, 9370, 2151, 3, 2153, (4, 2168));
            ExpectRouteAt(catalog, 9370, 2153, 6, 2155, (7, 2152));
            ExpectRouteAt(catalog, 9370, 2155, 2, 2173, (4, 2153));
            ExpectRouteAt(catalog, 9370, 2173, 8, 2855, (7, 2155));
        }

        public static void BlueNarcissStoryUsesTheCurrentHarborChoiceWithItsAtePending()
        {
            var catalog = ReadCatalog(9370, 9400, "lindblum-return-reviewed-blue-narciss-", atePending: true);
            ExpectRouteAt(catalog, 9370, 2173, 8, 2105, (7, 2155));
        }

        public static void FirstVisitStoryFindsTantalusFromTheInnAndAirCab()
        {
            var catalog = ReadCatalog(3110, 3115, "lindblum-first-reviewed-find-tantalus");
            ExpectRouteAt(catalog, 3110, 554, 3, 553);
            ExpectRouteAt(catalog, 3110, 553, 7, 552, (8, 554));
            ExpectRouteAt(catalog, 3110, 552, 22, 551, (21, 553));
            ExpectRouteAt(catalog, 3110, 551, 13, 563, (13, 572), (11, 552));
            ExpectRouteAt(catalog, 3110, 563, 14, 566, (15, 551));
            ExpectRouteAt(catalog, 3110, 566, 23, 567, (22, 563));
            ExpectRouteAt(catalog, 3110, 567, 22, 565, (21, 566));
        }


        public static void StationDoorwayNamesTheSamePlaceBeforeAndAfterVivisShopping()
        {
            foreach (int nativeDestination in new[] { 561, 551 })
            {
                var targets = ReadStationDoorway(552, 22, 22, StationDoorwayHash, false, nativeDestination, ShoppingEventHash);
                var exit = targets.Targets.Single(target => target.Category == NavigationCategory.Exits);
                TestAssert.Equal("Exit to Lindblum, B.D. Station", exit.Label);
                TestAssert.Equal(nativeDestination, exit.NextFields.Single());
            }
        }

        public static void StationDoorwayStoryUsesTheProvenFirstHopDuringVivisShopping()
        {
            foreach (int nativeDestination in new[] { 561, 551 })
            {
                var targets = ReadStationDoorway(552, 22, 22, StationDoorwayHash, false, nativeDestination, ShoppingEventHash);
                var story = targets.Targets.Single(target => target.Category == NavigationCategory.Story);
                TestAssert.True(story.PhysicalId.HasValue);
                TestAssert.Equal(22, story.PhysicalIdentity.NativeId);
                TestAssert.Equal(nativeDestination, story.NextFields.Single());
                TestAssert.True(story.Label.Contains("Lindblum, B.D. Station"));
                TestAssert.False(story.Label.Contains("Item Shop"));
                TestAssert.True(story.ApproachCandidates.SequenceEqual(story.DestinationApproaches[nativeDestination]));
            }
        }

        public static void TheGenuineItemShopRemainsASeparateDoorway()
        {
            const string shopDoorway = "63b24c7216a5e50bf553813994243e602f55a05a267877e95e47ecf2d59e0103";
            var targets = ReadStationDoorway(559, 26, 26, shopDoorway, false, 561, ShoppingEventHash);
            var exit = targets.Targets.Single(target => target.Category == NavigationCategory.Exits);
            TestAssert.Equal("Exit to Lindblum, Item Shop", exit.Label);
            TestAssert.Equal(561, exit.NextFields.Single());
            TestAssert.False(targets.Targets.Single(target => target.Category == NavigationCategory.Story).PhysicalId.HasValue);
        }

        public static void StationContinuationRequiresTheVerifiedDoorwayAndArrivalScene()
        {
            var mismatches = new[]
            {
                (552, 22, 22, StationDoorwayHash + "0", false, ShoppingEventHash),
                (552, 21, 22, StationDoorwayHash, false, ShoppingEventHash),
                (552, 22, 21, StationDoorwayHash, false, ShoppingEventHash),
                (552, 22, 22, StationDoorwayHash, true, ShoppingEventHash),
                (552, 22, 22, StationDoorwayHash, false, ShoppingEventHash + "0"),
                (552, 22, 22, StationDoorwayHash, false, null)
            };
            foreach (var (field, sid, uid, hash, confirm, script) in mismatches)
            {
                var targets = ReadStationDoorway(field, sid, uid, hash, confirm, 561, script);
                var exit = targets.Targets.Single(target => target.Category == NavigationCategory.Exits);
                TestAssert.Equal("Exit to Lindblum, Item Shop", exit.Label);
                TestAssert.False(targets.Targets.Single(target => target.Category == NavigationCategory.Story).PhysicalId.HasValue);
            }
        }

        public static void StationNamingAloneDoesNotOfferAnUnprovenStoryWalk()
        {
            var targets = ReadStationDoorway(552, 22, 22, StationDoorwayHash, false, 561, ShoppingEventHash, proved: false);
            var exit = targets.Targets.Single(target => target.Category == NavigationCategory.Exits);
            TestAssert.Equal("Exit to Lindblum, B.D. Station", exit.Label);
            TestAssert.Equal(0, exit.NextFields.Length);
            TestAssert.False(targets.Targets.Single(target => target.Category == NavigationCategory.Story).PhysicalId.HasValue);
        }

        private const string StationDoorwayHash = "e8a19cfa44dd00b56a87265ac78199ce45f5e81304e76ffbca649dc20999c3f0";
        private const string ShoppingEventHash = "403e6c1d51c881d53087f66c11c430dfdf8a295df3c9921225e7831241b67b88";

        private static CatalogResult ReadStationDoorway(int field, int sid, int uid, string hash, bool confirm,
            int nativeDestination, string arrivalScript, bool proved = true)
        {
            string Location(int map) => map == 551 ? "Lindblum, B.D. Station"
                : map == 561 ? "Lindblum, Item Shop" : null;

            var context = TestFixtures.Context("field:" + field);
            context.StoryProgress = 3110;
            var point = TestFixtures.P(555.5f, 1173);
            var exit = new NavigationTarget
            {
                Id = new TargetIdentity(TargetKind.FieldExit, context.LogicalMapKey, uid),
                Category = NavigationCategory.Exits, Generation = context.Generation, ApproachCandidates = new[] { point }
            };
            var route = new FieldExitRoute
            {
                ConfirmPrompt = confirm,
                Continuation = FieldExitFunctions.SceneContinuation(field, sid, uid, confirm ? 3 : 2, hash,
                    map => map == 561 ? arrivalScript : null)
            };
            string Name(int map) => FieldExitNames.Destination(route.Continuation?.Arrival(map) ?? map, Location);
            foreach (int possible in FieldExitFunctions.Possible(field, sid, hash, confirm))
            {
                string name = Name(possible);
                if (name != null && !route.PossibleNames.Contains(name)) route.PossibleNames.Add(name);
            }
            if (proved)
            {
                route.Names[nativeDestination] = Name(nativeDestination);
                route.Approaches[nativeDestination] = new[] { point };
            }
            var targets = new CatalogResult();
            foreach (var named in FieldExitNames.Apply(new[] { exit }, _ => route, _ => "Exit"))
                targets.Targets.Add(named);
            var story = ReadCatalog(3110, 3115, "lindblum-first-reviewed-find-tantalus");
            story.Append(context, targets, field, Location);
            return targets;
        }

        public static void PostHuntStoryFollowsTheCastleLiftAndDragonsGateTrolley()
        {
            var catalog = ReadCatalog(3700, 3710, "lindblum-first-reviewed-post-hunt-departure");
            ExpectRoute(catalog, 618, 14, 619);
            ExpectRoute(catalog, 619, 8, 617);
            ExpectRoute(catalog, 617, 11, 601);
            ExpectRoute(catalog, 601, 5, 603, (6, 617));
            ExpectRoute(catalog, 603, 7, 602, (6, 605));
            ExpectRoute(catalog, 602, 7, 9000, (14, 605));
        }

        public static void PostHuntStoryOffersTheTownGateWhenItsExitIsPresented()
        {
            var catalog = ReadCatalog(3700, 3710, "lindblum-first-reviewed-post-hunt-departure");
            ExpectRoute(catalog, 572, 15, 551, (15, 563), (13, 569));
            ExpectRoute(catalog, 563, 15, 551, (15, 572), (14, 566));
            ExpectRoute(catalog, 551, 11, 552, (13, 610));
            ExpectRoute(catalog, 552, 23, 550, (21, 553));
            ExpectRoute(catalog, 550, 17, 9010, (18, 552));

            var blocked = ReadRoute(catalog, 550, new[] { (18, 552) });
            TestAssert.False(blocked.PhysicalId.HasValue);
        }

        public static void ReviewedStoryDepartureUsesOnlyPresentedPortalsAtListedGates()
        {
            var stage = new StoryStage
            {
                Milestone = 3710,
                Goal = "Gizamaluke's Grotto",
                WorldDepartureFields = new[] { 602 },
                ReviewedLinks = new[] { new StoryTransit { From = 603, To = 602, ActionUid = 7 } }
            };
            var catalog = new StoryTargetCatalog(new[] { stage }, null);
            ExpectRoute(catalog, 603, 7, 602, (3, 9005));
            ExpectRoute(catalog, 602, 7, 9005);
            TestAssert.False(ReadRoute(catalog, 603, new[] { (3, 9005) }).PhysicalId.HasValue);
            TestAssert.False(ReadRoute(catalog, 602, Array.Empty<(int, int)>()).PhysicalId.HasValue);
        }

        private static void ExpectRoute(StoryTargetCatalog catalog, int field, int uid, int destination, params (int, int)[] alternatives)
            => ExpectRouteAt(catalog, 3700, field, uid, destination, alternatives);

        private static void ExpectRouteAt(StoryTargetCatalog catalog, int counter, int field, int uid, int destination, params (int, int)[] alternatives)
        {
            var route = ReadRoute(catalog, field, alternatives.Prepend((uid, destination)), counter);
            if (!route.PhysicalId.HasValue)
                throw new InvalidOperationException($"Field {field} did not offer its live route to {destination}: {route.Availability}");
            TestAssert.Equal(uid, route.PhysicalId.Value.NativeId);
            TestAssert.Equal(destination, route.NextFields.Single());
            if (field == 552 && uid == 22) TestAssert.True(route.RequireInwardApproach);
        }

        private static NavigationTarget ReadRoute(StoryTargetCatalog catalog, int field, IEnumerable<(int, int)> entries, int counter = 3700)
        {
            var context = TestFixtures.Context("field:" + field);
            context.StoryProgress = counter;
            var targets = new CatalogResult();
            foreach (var exit in entries.GroupBy(entry => entry.Item1))
            {
                int uid = exit.Key;
                var points = new[] { TestFixtures.P(120, 180) };
                int[] destinations = exit.Select(entry => entry.Item2).Distinct().ToArray();
                targets.Targets.Add(new NavigationTarget
                {
                    Id = new TargetIdentity(TargetKind.FieldExit, context.LogicalMapKey, uid),
                    Category = NavigationCategory.Exits, Generation = context.Generation,
                    Label = "Native exit " + uid, ApproachCandidates = points, NextFields = destinations,
                    SelectDestinationInMenu = destinations.Length > 1,
                    RequireInwardApproach = field == 552 && uid == 22,
                    DestinationApproaches = destinations.ToDictionary(destination => destination, _ => points)
                });
            }
            catalog.Append(context, targets, field);
            return targets.Targets.Single(target => target.Category == NavigationCategory.Story);
        }

        private static StoryTargetCatalog ReadCatalog(int counter, int milestone, string phasePrefix, bool atePending = false)
        {
            using var stages = Resource("story-navigation.json");
            using var transit = Resource("story-steps.json");
            using var review = Resource("story-action-chains.json");
            var departure = stages.RootElement.GetProperty("stages").EnumerateArray()
                .Single(row => row.GetProperty("milestone").GetInt32() == milestone);
            var baseStage = new StoryStage
            {
                Milestone = milestone, Goal = departure.GetProperty("goal").GetString(),
                GoalFields = Ints(departure, "fields"), WorldDepartureFields = Ints(departure, "worldDepartureFields")
            };
            var edges = transit.RootElement.GetProperty("transit").EnumerateArray().Select(row => new StoryTransit
            {
                From = row.GetProperty("source").GetInt32(), To = row.GetProperty("destination").GetInt32(),
                Kind = StoryStepCatalog.ParseTransit(row.GetProperty("kind").GetString()), Scope = row.GetProperty("scope").GetString(),
                Floor = Number(row, "floor"), Ceiling = Number(row, "ceiling"), Guards = Guards(row)
            }).ToArray();
            var catalog = new StoryTargetCatalog(new[] { baseStage }, edges);
            var globals = new byte[2048];
            globals[0] = (byte)counter;
            globals[1] = (byte)(counter >> 8);
            if (atePending) globals[3616 / 8] |= 1;
            var snapshot = new ScriptSnapshot(globals, null, null) { UserControl = true };
            bool? Evaluate(byte[] bytes) => ExitScriptAnalyzer.TryExpression(bytes, snapshot, out int? result) && result.HasValue ? result.Value != 0 : (bool?)null;
            catalog.SetTransitEvaluator(edge => edge.Guards.All(bytes => Evaluate(bytes) == true));

            var phases = review.RootElement.GetProperty("phases").EnumerateArray()
                .Where(row => row.GetProperty("id").GetString().StartsWith(phasePrefix, StringComparison.Ordinal))
                .Select(phase =>
                {
                    var gates = Ints(phase, "worldDepartureFields");
                    return new StoryPhase
                    {
                        Floor = Number(phase, "floor").Value, Ceiling = Number(phase, "ceiling").Value,
                        Directors = phase.GetProperty("rooms").EnumerateArray().ToDictionary(
                            row => Number(row, "field").Value, row => row.GetProperty("directorHash").GetString()),
                        Guards = Guards(phase), ScopeFields = Ints(phase, "scope"),
                        Stage = new StoryStage
                        {
                            Milestone = Number(phase, "milestone").Value, Goal = phase.GetProperty("goal").GetString(),
                            GoalFields = gates == null ? new[] { Number(phase, "field").Value } : null, WorldDepartureFields = gates,
                            ReviewedLinks = phase.GetProperty("links").EnumerateArray().Select(row => new StoryTransit
                            {
                                From = Number(row, "from").Value, To = Number(row, "to").Value, ActionUid = Number(row, "uid").Value,
                                Kind = StoryTransitKind.Player, Scope = "reviewed", Guards = Guards(row)
                            }).ToArray()
                        }
                    };
                }).ToArray();
            var directors = phases.SelectMany(phase => phase.Directors).GroupBy(room => room.Key)
                .ToDictionary(group => group.Key, group => group.First().Value);
            var phasePlan = new StoryPhasePlan(phases);
            catalog.SetPhaseSelector((sc, field, _) => directors.TryGetValue(field, out string hash)
                ? phasePlan.Select(sc, field, sid => sid == 0 ? hash : null, Evaluate) : null);
            return catalog;
        }

        private static int? Number(JsonElement row, string key) => row.TryGetProperty(key, out var value) ? value.GetInt32() : (int?)null;

        private static int[] Ints(JsonElement row, string key) => row.TryGetProperty(key, out var value)
            ? value.EnumerateArray().Select(item => item.GetInt32()).ToArray() : null;

        private static byte[][] Guards(JsonElement row) => row.GetProperty("guards").EnumerateArray().Select(value => Convert.FromHexString(value.GetString())).ToArray();

        private static JsonDocument Resource(string name)
        {
            foreach (string origin in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
                for (var dir = new DirectoryInfo(origin); dir != null; dir = dir.Parent)
                {
                    string path = Path.Combine(dir.FullName, "docs", "accessibility", name);
                    if (File.Exists(path)) return JsonDocument.Parse(File.ReadAllText(path));
                }
            throw new FileNotFoundException(name);
        }
    }
}
