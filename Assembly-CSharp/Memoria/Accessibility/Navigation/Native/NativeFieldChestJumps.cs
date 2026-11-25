using System;
using System.Collections.Generic;
using System.Linq;

namespace Memoria.Accessibility.Navigation
{
    public static partial class NativeFieldTargets
    {
        private const string IcePath = "field:301:FBG_N05_ICCV_MAP086_IC_STP_0";
        private const string IcePathMesh = "e1abe574e0a29885f8cbe01cc14bd4c7e0a50c42dab9604417c76c1c475b384a";
        private const string IceChest = "2d9dd7f794687c2ccecd0660e0889e6bb6a7c2724f3bd0b4c588d90beffd5f60";
        private const int IceChestUid = 15;
        private const int IceChestCollected = 7271;
        private static readonly KeyValuePair<int, string>[] IceChestJumps =
        {
            new KeyValuePair<int, string>(9, "ec743a5dc1030a907c2801a287fea4bf50abce0051b8ff93c41dd1f07b735f3d"),
            new KeyValuePair<int, string>(11, "b0b6701c99a11218823e278484eedeb8791ca7bb6a8f2b08a1cd1b39b4048042")
        };

        internal static void ReviewChestJumps(NavigationContext context, CatalogResult catalog)
        {
            if (context.LogicalMapKey != IcePath || !context.CanControl) return;
            var target = catalog.Targets.FirstOrDefault(item => item.Category == NavigationCategory.Objects &&
                item.PhysicalIdentity.Kind == TargetKind.FieldActor && item.PhysicalIdentity.NativeId == IceChestUid);
            if (target == null) return;

            var engine = NativeSceneReferences.Read<EventEngine>();
            var player = engine?.GetControlChar() as Actor;
            var field = player?.fieldMapActorController?.fieldMap;
            var chest = engine?.GetObjUID(IceChestUid) as Actor;
            var globals = FF9StateSystem.EventState.gEventGlobal;
            if (engine == null || engine.gMode != 1 || field == null || CurrentBgiHash(field) != IcePathMesh ||
                !MatchesChestJump(chest, IceChestUid, IceChest) || chest.model != 75 ||
                globals == null || globals.Length <= (IceChestCollected >> 3) ||
                (globals[IceChestCollected >> 3] & (1 << (IceChestCollected & 7))) != 0) return;

            // The approaches on the trail can be inside the chest's contact ring while
            // the chest itself is locked. The stock jumps request tag 32 to enable it.
            int instanceOffset = 4 * chest.vofs;
            if (chest.buffer == null || instanceOffset < 0 || instanceOffset >= chest.buffer.Length || chest.buffer[instanceOffset] != 0)
                return;

            var geometry = ReadGeometryForStory(context);
            if (geometry == null) return;
            var connected = FieldActorApproaches.Connected(geometry);
            if (connected == null) return;

            Quad offered = null;
            NavigationPoint[] approaches = null;
            foreach (var jump in IceChestJumps)
            {
                var quad = engine.GetObjUID(jump.Key) as Quad;
                if (!MatchesChestJump(quad, jump.Key, jump.Value) || !NativeFieldTalkability.IsTalkable(quad) ||
                    !EventCollision.NavigationCanApproachTalkQuad(player, quad) && !IsQueuedStockQuadPush(engine, quad)) continue;
                var surface = Surface(geometry, quad);
                float margin = FieldAnalogMovement.ArrivalRadius(geometry.Radius, 1) + 0.5f;
                var candidates = FieldStoryRegionApproaches.Read(true, true, surface.Approaches,
                    point => engine.NavigationTalkQuadAt(point.X, point.Z) == quad && engine.NavigationTouchQuadAt(point.X, point.Z) == quad,
                    point => OnChestJumpSurface(geometry, connected, field, surface.Region, point, margin), context.Position);
                if (candidates.Length == 0 || approaches != null &&
                    NavigationPoint.Distance(context.Position, candidates[0]) >= NavigationPoint.Distance(context.Position, approaches[0])) continue;
                offered = quad;
                approaches = candidates;
            }
            if (offered == null) return;

            // Keep the selection on the same chest when the jump finishes and
            // normal chest contact becomes available on the landing.
            target.PhysicalId = new TargetIdentity(TargetKind.FieldActor, context.LogicalMapKey, offered.uid);
            target.Label = "Chest on the ledge. Jump to it";
            target.Position = approaches[0];
            target.ApproachCandidates = approaches;
            target.InteractionUid = offered.uid;
            target.InteractionRegion = Surface(geometry, offered).Region;
            target.UseApprovedApproaches = true;
            target.UseViewingPoint = false;
            target.IsInteractive = true;
            target.Availability = null;
            target.Evidence = EIcon.NavigationPresentedUid == offered.uid ? VisibilityEvidence.NativeInteractionIcon : VisibilityEvidence.ReviewedBackground;
        }

        private static bool OnChestJumpSurface(FieldGeometrySnapshot geometry, HashSet<int> connected, FieldMap field,
            NavigationPoint[] region, NavigationPoint point, float margin)
        {
            var triangle = geometry.At(point);
            return triangle != null && connected.Contains(triangle.Id) &&
                FieldRegionApproaches.CanStopInside(region, point, margin) && StorySurfacePresented(field, point);
        }

        private static bool MatchesChestJump(Obj obj, int uid, string hash)
        {
            return obj != null && obj.uid == uid && obj.sid == uid && obj.ebData != null && Fingerprint(obj.ebData) == hash;
        }

        internal static InteractionReadiness ReadIceChestJumpReadiness(NavigationContext context, EventEngine engine, Actor player, Quad quad)
        {
            if (context.LogicalMapKey != IcePath) return null;
            foreach (var jump in IceChestJumps)
            {
                if (!MatchesChestJump(quad, jump.Key, jump.Value)) continue;
                if (!QueuedIceChestJump(engine, player, quad)) return null;
                var readiness = FieldInteractionRules.PushReadiness(true, true, true, true);
                readiness.Evidence = "The original ice ledge jump prompt is displayed. Confirm remains player controlled.";
                return readiness;
            }
            return null;
        }

        private static bool QueuedIceChestJump(EventEngine engine, Actor player, Quad quad)
        {
            // These original Push handlers show the exclamation and immediately return.
            // The next queued Push consumes a stack frame until the event code runs again.
            return quad.level == 1 && EIcon.NavigationPresentedUid == quad.uid &&
                EIcon.NavigationScriptIconUid == quad.uid && EIcon.SFIconType == BubbleUI.IconType.Exclamation &&
                engine.TreadQuad(player, 2) == quad && engine.TreadQuad(player, 4) == quad &&
                EventCollision.NavigationHasTouchFacing(player, quad) && IsQueuedStockQuadPush(engine, quad);
        }
    }
}
