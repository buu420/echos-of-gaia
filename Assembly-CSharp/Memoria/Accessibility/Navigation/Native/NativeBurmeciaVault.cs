using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Memoria.Accessibility.Navigation
{
    public static partial class NativeFieldTargets
    {
        private const string BurmeciaVaultMap = "field:764:FBG_N13_BRMC_MAP275_BU_TRE_0";
        private const string BurmeciaVaultBackground = "5db4b69b866733efa9283116e69709fcb7d711fda7763b743b4c5f085bee28bc";
        private const string BurmeciaVaultMesh = "a932d3a2aaf3b73ca1dc3f21969e41d288e627dac7ee8eedd547db7b0759e971";
        private const string BurmeciaVaultChestScript = "5e70ea80d1549dd5088c830448d142889e2b4cb088d97bdea6947f8be05feb2a";
        private const string BurmeciaVaultPlayerScript = "2a00317371ef49feb9fbac2bc2a8b0013029a6d6bf04ab661bd40fbcca17295a";
        private const int BurmeciaVaultChestUid = 1;
        private const int BurmeciaVaultPlayerUid = 12;
        private const int BurmeciaVaultChestCollected = 7309;
        private static readonly int[] BurmeciaVaultChestLayers = { 0 };

        // The closed chest is drawn in camera 0, centered around native image pixel (187, 124).
        private static readonly Vector3 BurmeciaVaultChestArt = new Vector3(80, 572, 0);

        internal static FieldTargetObservation ReadBurmeciaVaultChest(NavigationContext context, EventEngine engine,
            FieldMap field, Actor player, Quad quad, ref FieldGeometrySnapshot geometry, ref bool geometryRead)
        {
            if (context?.LogicalMapKey != BurmeciaVaultMap || quad?.uid != BurmeciaVaultChestUid ||
                !BurmeciaVaultChestAvailable(context, engine, field, player, quad))
                return null;

            if (!EventCollision.NavigationCanApproachTalkQuad(player, quad) && !IsQueuedStockQuadPush(engine, quad))
                return null;

            if (!geometryRead)
            {
                geometry = NativeFieldGeometry.Read(context);
                geometryRead = true;
            }
            if (geometry == null) return null;

            var surface = Surface(geometry, quad);
            float margin = FieldAnalogMovement.ArrivalRadius(geometry.Radius, 1) + 0.5f;
            var approaches = FieldStoryRegionApproaches.Read(true, true, surface.Approaches,
                point => engine.NavigationTalkQuadAt(point.X, point.Z) == quad && engine.NavigationTouchQuadAt(point.X, point.Z) == quad,
                point => FieldRegionApproaches.CanStopInside(surface.Region, point, margin) && StorySurfacePresented(field, point),
                context.Position);
            if (approaches.Length == 0) return null;

            return new FieldTargetObservation
            {
                Id = new TargetIdentity(TargetKind.FieldActor, BurmeciaVaultMap, quad.uid),
                NativePresentationIdentity = RuntimeHelpers.GetHashCode(quad),
                Position = approaches[0],
                Visible = true,
                VisibleLabel = "Chest held by the statue",
                LabelIdentity = BurmeciaVaultChestScript,
                ShowsIcon = BurmeciaVaultChestPrompt(quad),
                HasInteraction = true,
                InteractionUid = quad.uid,
                InteractionRegion = surface.Region,
                ApproachCandidates = approaches,
                UseApprovedApproaches = true
            };
        }

        internal static InteractionReadiness ReadBurmeciaVaultChestReadiness(NavigationContext context, NavigationTarget target,
            EventEngine engine, Actor player, Quad quad)
        {
            if (context?.LogicalMapKey != BurmeciaVaultMap || quad?.uid != BurmeciaVaultChestUid ||
                target?.Id.Kind != TargetKind.FieldActor || target.Id.NativeId != BurmeciaVaultChestUid)
                return null;

            var controller = player?.fieldMapActorController;
            if (!BurmeciaVaultChestAvailable(context, engine, controller?.fieldMap, player, quad) ||
                !BurmeciaVaultChestPrompt(quad) || engine.TreadQuad(player, 2) != quad || engine.TreadQuad(player, 4) != quad ||
                !engine.NavigationContainsQuad(quad, controller.curPos.x, controller.curPos.z))
                return new InteractionReadiness();

            // The event pass displays the exclamation and returns from tag 2. Collision can queue
            // that same short handler again before navigation observes the idle request level.
            bool ready = EventCollision.NavigationCanTalkQuad(player, quad) ||
                (EventCollision.NavigationHasTouchFacing(player, quad) && IsQueuedStockQuadPush(engine, quad));
            return FieldInteractionRules.PushReadiness(true, true, ready, true);
        }

        private static bool BurmeciaVaultChestAvailable(NavigationContext context, EventEngine engine, FieldMap field,
            Actor player, Quad quad)
        {
            if (!context.CanControl || FF9StateSystem.Common.FF9.fldMapNo != 764 || engine == null || engine.gMode != 1 ||
                !engine.GetUserControl() || field == null || field.camIdx != 0 || field.curCamIdx != 0 ||
                field.GetCurrentBgCamera() == null || field.scene?.ebgBin == null || field.scene.overlayList == null ||
                player == null || player.uid != context.PlayerIdentity || player.model != 98 || player.level <= 4 ||
                !ReferenceEquals(player.fieldMapActorController?.fieldMap, field) ||
                !BurmeciaVaultStockEntry(player, BurmeciaVaultPlayerUid, BurmeciaVaultPlayerScript) ||
                !BurmeciaVaultStockEntry(quad, BurmeciaVaultChestUid, BurmeciaVaultChestScript) ||
                quad.q == null || quad.n != 4 || quad.n > quad.q.Length || quad.wait != 0 ||
                !NativeFieldTalkability.IsTalkable(quad) || engine.GetIP(player.sid, 11, player.ebData) == engine.nil)
                return false;

            var globals = FF9StateSystem.EventState.gEventGlobal;
            int index = BurmeciaVaultChestCollected >> 3;
            int mask = 1 << (BurmeciaVaultChestCollected & 7);
            if (globals == null || index >= globals.Length || (globals[index] & mask) != 0) return false;

            return Fingerprint(field.scene.ebgBin) == BurmeciaVaultBackground && CurrentBgiHash(field) == BurmeciaVaultMesh &&
                EntranceOnScreen(field, BurmeciaVaultChestArt) &&
                RenderedBackgroundAt(field, BurmeciaVaultChestArt, BurmeciaVaultChestLayers);
        }

        private static bool BurmeciaVaultStockEntry(Obj obj, int uid, string hash)
        {
            return obj != null && obj.uid == uid && obj.sid == uid && obj.state == EventEngine.stateRunning &&
                obj.ebData != null && !obj.isAdditionCommand && ReferenceEquals(obj.currentByte, obj.ebData) &&
                Fingerprint(obj.ebData) == hash;
        }

        private static bool BurmeciaVaultChestPrompt(Quad quad)
        {
            return EIcon.NavigationPresentedUid == quad.uid && EIcon.NavigationScriptIconUid == quad.uid &&
                EIcon.SFIconType == BubbleUI.IconType.Exclamation;
        }
    }
}
