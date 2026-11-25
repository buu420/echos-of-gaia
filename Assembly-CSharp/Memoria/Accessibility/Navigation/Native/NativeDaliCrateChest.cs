using System;
using UnityEngine;

namespace Memoria.Accessibility.Navigation
{
    public static partial class NativeFieldTargets
    {
        private const string DaliStorageMap = "field:407:FBG_N08_UDFT_MAP122_UF_STO_0";
        private const string DaliStorageMesh = "4bf3ef14f878ba181d48b8b73ccb282d9d95bc8bc6e185b7a7f71a3ffe9a303a";
        private const string DaliStorageBackground = "f0819b56c4185feaf9c1f7a0dec1f732a8b1f1c859323383b157e82f78ce0282";
        private const string DaliStoragePlayer = "b3262355636645f728f58ed8fb227877b9c3d1b9c61f4ba433b8d8eb39a37c44";
        private const string DaliStorageChest = "ab0cc81ccb2cd42bf163990f68f7d7d4628c1c78c392fbc2820ab5de27b7bcce";
        private const int DaliChestUid = 22;
        private const int DaliChestCollected = 7283;

        private sealed class DaliCrateJump
        {
            internal readonly int Uid, Floor, Heading, MaximumHeight, CallbackTag, LandingTriangle;
            internal readonly string Hash, Direction;
            internal readonly NavigationPoint Approach, Landing;

            internal DaliCrateJump(int uid, string hash, int floor, int heading, int maximumHeight, int callbackTag,
                string direction, NavigationPoint approach, NavigationPoint landing, int landingTriangle)
            {
                Uid = uid;
                Hash = hash;
                Floor = floor;
                Heading = heading;
                MaximumHeight = maximumHeight;
                CallbackTag = callbackTag;
                Direction = direction;
                Approach = approach;
                Landing = landing;
                LandingTriangle = landingTriangle;
            }

            internal bool HeadingAllowed(int actual) { return actual > Heading - 24 && actual < Heading + 24; }
        }

        // Both original Push handlers return immediately after polling their exclamation.
        // Confirm on the lower crate's other heading calls tag 23 and jumps back down.
        private static readonly DaliCrateJump DaliFirstCrate = new DaliCrateJump(16,
            "b9a14b5c597c9c331aaf25031a4163fc3d86b37fd0368f7b6dec76bf3556771b", 5, 64, 200, 20, "left",
            new NavigationPoint(-433, -16, 4), new NavigationPoint(-675, 285, -29), 76);
        private static readonly DaliCrateJump DaliSecondCrate = new DaliCrateJump(17,
            "36067f94ee18d87cec1a12faf3d3ed954c6948b937dabe2882334ef77feff58d", 6, 128, 700, 21, "up",
            new NavigationPoint(-702, 285, -74), new NavigationPoint(-838, 833, 165), 74);

        internal static void ReviewDaliCrateChest(NavigationContext context, CatalogResult catalog)
        {
            if (context?.LogicalMapKey != DaliStorageMap || !context.CanControl || catalog == null) return;
            var target = catalog.Find(new TargetIdentity(TargetKind.FieldActor, DaliStorageMap, DaliChestUid));
            if (target == null || target.Evidence != VisibilityEvidence.RenderedActor && target.Evidence != VisibilityEvidence.NativeInteractionIcon)
                return;

            var engine = NativeSceneReferences.Read<EventEngine>();
            var player = engine?.GetControlChar() as Actor;
            if (!DaliCrateAvailable(context, engine, player)) return;
            var field = player.fieldMapActorController.fieldMap;
            var geometry = ReadGeometryForStory(context);
            var support = geometry?.Find(player.fieldMapActorController.activeTri);
            if (support == null || !geometry.Permitted(support) || support.Id == 74 || support.Id == 77) return;

            var jump = support.Id == 75 || support.Id == 76 ? DaliSecondCrate : DaliFirstCrate;
            var quad = engine.GetObjUID(jump.Uid) as Quad;
            string direction = DaliCrateDirection(jump);
            if (direction == null || !DaliCrateMatches(engine, player, quad, jump)) return;
            var landing = geometry.At(jump.Landing);
            if (landing == null || landing.Id != jump.LandingTriangle) return;

            var connected = FieldActorApproaches.Connected(geometry);
            if (connected == null) return;
            var surface = Surface(geometry, quad);
            float margin = FieldAnalogMovement.ArrivalRadius(geometry.Radius, 1) + 0.5f;
            var approaches = FieldStoryRegionApproaches.Read(true, true, surface.Approaches,
                point => engine.NavigationTalkQuadAt(point.X, point.Z) == quad && engine.NavigationTouchQuadAt(point.X, point.Z) == quad,
                point =>
                {
                    var floor = geometry.At(point);
                    return DaliCrateSupport(floor, jump, point) && connected.Contains(floor.Id) &&
                        FieldRegionApproaches.CanStopInside(surface.Region, point, margin) && StorySurfacePresented(field, point);
                }, jump.Approach);
            if (approaches.Length == 0) return;

            target.PhysicalId = new TargetIdentity(TargetKind.FieldActor, DaliStorageMap, quad.uid);
            target.Label = "Chest on the crates. At the jump point, hold " + direction + " on the D-pad or arrow keys until the prompt appears";
            target.Position = approaches[0];
            target.ApproachCandidates = approaches;
            target.InteractionUid = quad.uid;
            target.InteractionRegion = surface.Region;
            target.UseApprovedApproaches = true;
            target.UseViewingPoint = false;
            target.IsInteractive = true;
            target.Availability = null;
            target.Evidence = DaliCratePrompt(quad, jump, player) ? VisibilityEvidence.NativeInteractionIcon : VisibilityEvidence.ReviewedBackground;
        }

        internal static InteractionReadiness ReadDaliCrateChestReadiness(NavigationContext context, NavigationTarget target,
            EventEngine engine, Actor player, Quad quad)
        {
            if (context?.LogicalMapKey != DaliStorageMap || target == null || target.Id.Kind != TargetKind.FieldActor ||
                target.Id.NativeId != DaliChestUid || quad == null || target.PhysicalIdentity.NativeId != quad.uid) return null;

            var jump = quad.uid == DaliFirstCrate.Uid ? DaliFirstCrate : quad.uid == DaliSecondCrate.Uid ? DaliSecondCrate : null;
            if (jump == null) return null;
            string direction = DaliCrateDirection(jump);
            if (direction == null || !DaliCrateAvailable(context, engine, player) || !DaliCrateMatches(engine, player, quad, jump))
                return new InteractionReadiness();

            var geometry = ReadGeometryForStory(context);
            var support = geometry?.Find(player.fieldMapActorController.activeTri);
            if (support == null || !geometry.Permitted(support) || !DaliCrateSupport(support, jump, context.Position) ||
                geometry.At(jump.Landing)?.Id != jump.LandingTriangle ||
                Mathf.RoundToInt(player.pos[1]) >= jump.MaximumHeight ||
                engine.TreadQuad(player, 2) != quad || engine.TreadQuad(player, 4) != quad ||
                !engine.NavigationContainsQuad(quad, context.Position.X, context.Position.Z)) return new InteractionReadiness();

            if (!jump.HeadingAllowed(DaliCrateHeading(player.rotAngle[1])))
            {
                // The ascent region already overlaps the first landing. Walk to
                // the reviewed stance before asking for a turn, so the player
                // has room to face the next crate while remaining in the region.
                if (FieldGeometrySnapshot.Horizontal(context.Position, target.Position) > 8f)
                    return new InteractionReadiness();
                return new InteractionReadiness { InRange = true, SelectedCollision = true, Talkable = true,
                    Evidence = "Hold " + direction + " until the crate jump prompt appears." };
            }

            bool ready = DaliCratePrompt(quad, jump, player) && (EventCollision.NavigationCanTalkQuad(player, quad) ||
                EventCollision.NavigationHasTouchFacing(player, quad) && IsQueuedStockQuadPush(engine, quad));
            var result = FieldInteractionRules.PushReadiness(true, true, ready, ready);
            result.Evidence = "The native crate ascent prompt is displayed. Confirm remains player controlled.";
            return result;
        }

        private static bool DaliCrateAvailable(NavigationContext context, EventEngine engine, Actor player)
        {
            if (!context.CanControl || FF9StateSystem.Common.FF9.fldMapNo != 407 || engine == null || engine.gMode != 1 ||
                !engine.GetUserControl() || !DaliCrateEntry(player, 23, DaliStoragePlayer) || player.uid != context.PlayerIdentity || player.level <= 4) return false;

            var field = player.fieldMapActorController?.fieldMap;
            if (field == null || field.camIdx != 0 || field.curCamIdx != 0 || field.scene?.ebgBin == null ||
                Fingerprint(field.scene.ebgBin) != DaliStorageBackground || CurrentBgiHash(field) != DaliStorageMesh) return false;

            var chest = engine.GetObjUID(DaliChestUid) as Actor;
            var globals = FF9StateSystem.EventState.gEventGlobal;
            if (!DaliCrateEntry(chest, DaliChestUid, DaliStorageChest) || chest.model != 75 || globals == null ||
                globals.Length <= (DaliChestCollected >> 3) || (globals[DaliChestCollected >> 3] & (1 << (DaliChestCollected & 7))) != 0) return false;
            int variable = 4 * chest.vofs;
            return chest.buffer != null && variable >= 0 && variable < chest.buffer.Length && chest.buffer[variable] == 1;
        }

        private static bool DaliCrateMatches(EventEngine engine, Actor player, Quad quad, DaliCrateJump jump)
        {
            return DaliCrateEntry(quad, jump.Uid, jump.Hash) && quad.q != null && quad.n >= 3 && quad.n <= quad.q.Length && NativeFieldTalkability.IsTalkable(quad) &&
                (EventCollision.NavigationCanApproachTalkQuad(player, quad) || IsQueuedStockQuadPush(engine, quad)) &&
                engine.GetIP(player.sid, jump.CallbackTag, player.ebData) != engine.nil;
        }

        private static string DaliCrateDirection(DaliCrateJump jump)
        {
            // Follow the controller's digital-input orientation. Cardinal directions
            // reach the strict arc boundary with absolute keys, requiring diagonals.
            bool absolute = Configuration.AnalogControl.Enabled && Configuration.AnalogControl.UseAbsoluteOrientationKeys;
            var twist = FF9StateSystem.Field.twist;
            int heading = jump.Heading + (absolute ? 32 : 0);
            int settled = DaliCrateHeading(heading * (360f / 256) + (absolute ? twist.x : twist.y));
            if (!jump.HeadingAllowed(settled - 1) || !jump.HeadingAllowed(settled + 1)) return null;
            if (!absolute) return jump.Direction;
            return jump == DaliFirstCrate ? "up and left" : "up and right";
        }

        private static bool DaliCrateSupport(FieldTriangle support, DaliCrateJump jump, NavigationPoint point)
        {
            return support != null && support.Floor == jump.Floor && support.Contains(point) &&
                Math.Abs(support.Height(point) - point.Y) <= 1f && Mathf.RoundToInt(point.Y) < jump.MaximumHeight &&
                (jump == DaliFirstCrate || support.Id == 75 || support.Id == 76);
        }

        private static int DaliCrateHeading(float degrees) { return (EventEngineUtils.ConvertFloatAngleToFixedPoint(degrees) >> 4) & 255; }

        private static bool DaliCrateEntry(Obj obj, int uid, string hash)
        {
            return obj != null && obj.uid == uid && obj.sid == uid && obj.state == EventEngine.stateRunning &&
                obj.ebData != null && !obj.isAdditionCommand && ReferenceEquals(obj.currentByte, obj.ebData) && Fingerprint(obj.ebData) == hash;
        }

        private static bool DaliCratePrompt(Quad quad, DaliCrateJump jump, Actor player)
        {
            return jump.HeadingAllowed(DaliCrateHeading(player.rotAngle[1])) && EIcon.NavigationPresentedUid == quad.uid &&
                EIcon.NavigationScriptIconUid == quad.uid && EIcon.SFIconType == BubbleUI.IconType.Exclamation;
        }
    }
}
