using System;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Memoria.Accessibility.Navigation
{
    public static partial class NativeFieldTargets
    {
        private const string TrenoDock = "field:916:FBG_N20_TRNO_MAP403_TR_WHF_0";
        private const string TotTower = "field:914:FBG_N20_TRNO_MAP415_TR_TOT_0";

        private sealed class TrenoLadder
        {
            public string Map, Mesh, Background, LadderScript, PlayerScript;
            public int LadderUid, PlayerUid, FirstStory, LastStory, Milestone;
            public int[] Before, Beyond;
            public string BeforeLabel, BeyondLabel;
        }

        // The stock ladder script moves Dagger between disconnected support.
        // An XZ point alone can fall in the quad on the level below the ladder.
        private static readonly TrenoLadder DockLadder = new TrenoLadder
        {
            Map = TrenoDock, LadderUid = 5, PlayerUid = 10, FirstStory = 4442, LastStory = 4450, Milestone = 4445,
            Mesh = "4234aaadd80f8e9cb432b94a6fcc2ec6ed36c21855f2f1b2faff63b306135091",
            Background = "df0fa0fe240337a4eece72e64d3214da9c01edc0a07a9c14a9861782681a9abd",
            LadderScript = "5856d65382048422345414b99f1c05f5f29337a3bb7aeb37764c595d4ecfd895",
            PlayerScript = "2705b478963a3df8a5fd70c06903f01f8ecf0e52cee331247372d29f7ddb348f",
            Before = new[] { 49, 55 }, Beyond = new[] { 16, 25, 26 },
            BeforeLabel = "Dock ladder. Hold Down after Confirm",
            BeyondLabel = "Dock ladder. Hold Up after Confirm"
        };
        private static readonly TrenoLadder TowerLadder = new TrenoLadder
        {
            Map = TotTower, LadderUid = 10, PlayerUid = 13, FirstStory = 4470, LastStory = 4500, Milestone = 4500,
            Mesh = "7727529a2181e88a03bf2d89ac561b3cd8687a8c0f108ef88fefb27e96bb1b55",
            Background = "c96b4aeb723a85db23e4a5121c34eda44c4f0a98341847fca4ce5918b1f55610",
            LadderScript = "46a73a98b39044a158135ef3a5f01bb856f4240f362114dfded8d342b7013699",
            PlayerScript = "0258bc05c50832796d07415384517dc8dcca12dc3d95f4a1e4671d9afb8c1b46",
            Before = new[] { 58, 59, 60, 61 }, Beyond = new[] { 75, 76, 77, 83, 85, 99, 100 },
            BeforeLabel = "Ladder to the hatch. Hold Up after Confirm",
            BeyondLabel = "Ladder down to the tower room. Hold Down after Confirm"
        };

        private static TrenoLadder CurrentTrenoLadder(NavigationContext context)
        {
            var ladder = context.LogicalMapKey == TrenoDock ? DockLadder : context.LogicalMapKey == TotTower ? TowerLadder : null;
            return ladder != null && context.CanControl && context.StoryProgress >= ladder.FirstStory &&
                context.StoryProgress < ladder.LastStory ? ladder : null;
        }

        private static bool TrenoLadderAvailable(TrenoLadder ladder, NavigationContext context, EventEngine engine, FieldMap field, Actor player, Quad quad)
        {
            return ReviewedPromptQuad(quad, ladder.LadderUid, ladder.LadderScript) &&
                ReviewedPromptScene(context, engine, field, player, ladder.PlayerUid, ladder.PlayerScript, ladder.Mesh, ladder.Background);
        }

        private static int[] TrenoLadderSide(TrenoLadder ladder, NavigationContext context, FieldGeometrySnapshot geometry)
        {
            var triangle = geometry?.Find(geometry.ActiveTriangle);
            if (triangle == null || !geometry.Permitted(triangle) || !triangle.Contains(context.Position) ||
                Math.Abs(triangle.Height(context.Position) - context.Position.Y) > 1) return null;
            var connected = FieldActorApproaches.Connected(geometry);
            bool before = ladder.Before.Any(connected.Contains);
            bool beyond = ladder.Beyond.Any(connected.Contains);
            if (before == beyond) return null;
            return before ? ladder.Before : ladder.Beyond;
        }

        private static FieldTargetObservation ReadTrenoLadder(NavigationContext context, EventEngine engine, FieldMap field,
            Actor player, Quad quad, ref FieldGeometrySnapshot geometry, ref bool geometryRead)
        {
            var ladder = CurrentTrenoLadder(context);
            if (ladder == null || quad.uid != ladder.LadderUid || !TrenoLadderAvailable(ladder, context, engine, field, player, quad) ||
                !EventCollision.NavigationCanApproachTalkQuad(player, quad) && !IsQueuedStockQuadPush(engine, quad)) return null;
            if (!geometryRead)
            {
                geometry = NativeFieldGeometry.Read(context);
                geometryRead = true;
            }
            var side = TrenoLadderSide(ladder, context, geometry);
            if (side == null) return null;
            var mesh = geometry;
            var surface = Surface(mesh, quad);
            float margin = FieldAnalogMovement.ArrivalRadius(geometry.Radius, 1) + 0.5f;
            var points = FieldStoryRegionApproaches.Read(true, true, surface.Approaches,
                point => engine.NavigationTalkQuadAt(point.X, point.Z) == quad && engine.NavigationTouchQuadAt(point.X, point.Z) == quad,
                point => Array.IndexOf(side, mesh.At(point)?.Id ?? -1) >= 0 &&
                    FieldRegionApproaches.CanStopInside(surface.Region, point, margin) && StorySurfacePresented(field, point),
                surface.Region[0]);
            if (points.Length == 0) return null;
            return new FieldTargetObservation
            {
                Id = new TargetIdentity(TargetKind.FieldActor, context.LogicalMapKey, quad.uid),
                LabelIdentity = ladder.LadderScript,
                NativePresentationIdentity = RuntimeHelpers.GetHashCode(quad),
                Visible = true, HasInteraction = true, ShowsIcon = PresentedStoryPrompt(quad),
                VisibleLabel = side == ladder.Before ? ladder.BeforeLabel : ladder.BeyondLabel,
                Position = points[0], InteractionUid = quad.uid, InteractionRegion = surface.Region,
                ApproachCandidates = points, UseApprovedApproaches = true
            };
        }

        internal static InteractionReadiness ReadTrenoClimbReadiness(NavigationContext context, EventEngine engine, Actor player, Quad quad)
        {
            var ladder = CurrentTrenoLadder(context);
            if (ladder == null || quad.uid != ladder.LadderUid) return null;
            if (!TrenoLadderAvailable(ladder, context, engine, player?.fieldMapActorController?.fieldMap, player, quad))
                return new InteractionReadiness();
            return ReadPresentedStoryPrompt(engine, player, quad, ladder.Before, ladder.Beyond);
        }

        internal static bool ReviewTrenoLadders(NavigationContext context, CatalogResult catalog)
        {
            var ladder = CurrentTrenoLadder(context);
            if (ladder == null) return false;
            var engine = NativeSceneReferences.Read<EventEngine>();
            var player = engine?.GetControlChar() as Actor;
            var quad = engine?.GetObjUID(ladder.LadderUid) as Quad;
            if (!TrenoLadderAvailable(ladder, context, engine, player?.fieldMapActorController?.fieldMap, player, quad)) return false;
            var side = TrenoLadderSide(ladder, context, ReadGeometryForStory(context));
            if (side == null) return false;

            NavigationTarget jump = catalog.Targets.FirstOrDefault(target => target.Category == NavigationCategory.Objects &&
                target.PhysicalIdentity.NativeId == ladder.LadderUid && target.InteractionUid == ladder.LadderUid &&
                target.UseApprovedApproaches && target.ApproachCandidates?.Length > 0);
            if (jump != null) AliasTrenoLadderDestinations(engine, catalog, ladder, side, jump);
            if (side == ladder.Before && jump != null)
            {
                int milestone = ladder == DockLadder && context.StoryProgress >= 4445 ? 4450 : ladder.Milestone;
                catalog.Targets.Add(StoryTargetCatalog.Wrap(context, milestone, jump, ladder == DockLadder ?
                    "Climb down to the dock. Hold Down after Confirm" : "Climb up to the hatch. Hold Up after Confirm"));
                return true;
            }

            if (ladder == DockLadder && side == ladder.Beyond && context.StoryProgress >= 4445 && TrenoDockContact(engine))
            {
                var person = catalog.Targets.FirstOrDefault(target => target.Category == NavigationCategory.People &&
                    target.PhysicalIdentity.NativeId == 3 && target.IsInteractive && target.ApproachCandidates?.Length > 0);
                if (person != null)
                {
                    catalog.Targets.Add(StoryTargetCatalog.Wrap(context, 4450, person, "Talk to " + person.Label + " at the dock"));
                    return true;
                }
            }
            return false;
        }

        // A destination keeps its own list entry while the first leg uses the native ladder.
        private static void AliasTrenoLadderDestinations(EventEngine engine, CatalogResult catalog, TrenoLadder ladder, int[] side, NavigationTarget jump)
        {
            for (int i = 0; i < catalog.Targets.Count; i++)
            {
                var target = catalog.Targets[i];
                var id = target.PhysicalIdentity;
                string instruction = null;
                if (ladder == TowerLadder && side == ladder.Before && id.Kind == TargetKind.FieldActor && id.NativeId == 8 &&
                    ReviewedStockEntry(engine.GetObjUID(8), 8, TotHatchScript))
                    instruction = "Use the ladder to the hatch. Hold Up after Confirm";
                if (ladder == DockLadder && side == ladder.Before && id.Kind == TargetKind.FieldActor &&
                    (id.NativeId == 6 && ReviewedStockEntry(engine.GetObjUID(6), 6, "ba413df391cc4d583f011d8954b030a1e840ba422d944eb5ba27d87f905887d2") ||
                    id.NativeId == 3 && TrenoDockContact(engine)))
                    instruction = "Use the dock ladder. Hold Down after Confirm";
                if (ladder == DockLadder && side == ladder.Beyond && id.Kind == TargetKind.FieldExit && id.NativeId == 7 &&
                    ReviewedStockEntry(engine.GetObjUID(7), 7, "f5a6be20c6483d32ac6f17888d752199188d3313abe1fce2615262b43ba7d191"))
                    instruction = "Use the dock ladder. Hold Up after Confirm";
                if (instruction == null) continue;

                var via = FieldTargetCatalog.Copy(jump);
                via.Id = target.Id;
                via.Category = target.Category;
                via.PhysicalId = jump.PhysicalIdentity;
                via.Label = target.Label + ". " + instruction;
                catalog.Targets[i] = via;
            }
        }

        private static bool TrenoDockContact(EventEngine engine)
        {
            var person = engine.GetObjUID(3) as Actor;
            return person?.model == 5464 && ReviewedStockEntry(person, 3, "e8a440ddf30892951d5a2d0dd8030ca698840ee1742604da3fc4a7d20a6fee75");
        }
    }
}
