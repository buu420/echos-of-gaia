using System;

namespace Memoria.Accessibility.Navigation
{
    internal static class NativePinnacleRocks
    {
        private const int Hole = 1251, MontysPath = 1252, WaterPath = 1253, BridgePath = 1254;
        private const int Beginning = 1, Cooperation = 2, Silence = 4, Human = 8, Hero = 16;
        private const int JumpUid = 11;
        private const int ArmletChestBit = 7557;
        private const string WaterChest = "1ebbf46275b399e251d6ecb7923e8f3708c385ef8d40ae80d98fe11b07b4e822";
        private const string BridgeChest = "e5c5c1545e268e985ba7cdcc6a6a338697ba9bdb5c139a627838e967c38c9697";
        private const string BridgeWaterChest = "1645180872ad2a9f7a7ca55b5825cdbc7a0beeb6c971efdbdc77d716ab12c712";
        private const string MontyScript = "0fed61fd6730d9fc0ead649d0edd92119dfa807f513d2650373085c9a81996ff";
        private const string JumpScript = "3402b3004c70c4df8a1bef35b508391514ac57ea09ac5851573fdd91fb0de48d";

        private static bool Matches(int map, FieldMap field)
        {
            if (field == null) return false;
            string hash = NativeFieldTargets.CurrentBgiHash(field);
            switch (map)
            {
                case Hole: return hash == "617042f4f287511cac79aec3cd104f207e7ddd59f4ae49f0884a0c41f46cdff1";
                case MontysPath: return hash == "96d2e3006185ce7cdf6f3ed5b7ea8e2d9f6cb8e7af98cfa6cb4f393dbe37df1b";
                case WaterPath: return hash == "2e1e36f9830f4a309a10653396a2562d894a827dd5aafbb565098da6a1428b29";
                case BridgePath: return hash == "c38f0bd29ea4279b9cf3d27101a699872d147e84de5487198ed3ae78c2dc57d7";
                default: return false;
            }
        }

        internal static void ReviewTargets(NavigationContext context, CatalogResult catalog)
        {
            int map = FF9StateSystem.Common.FF9.fldMapNo;
            if (map < Hole || map > BridgePath || !context.CanControl || catalog == null) return;
            var engine = NativeSceneReferences.Read<EventEngine>();
            var player = engine?.GetControlChar() as Actor;
            var field = player?.fieldMapActorController?.fieldMap;
            if (engine == null || engine.gMode != 1 || player == null || !Matches(map, field)) return;

            ReviewApparition(context, catalog, engine, player);
            if (map == MontysPath) ReviewMonty(context, catalog, engine, player, field);
            if (map == BridgePath) ReviewBridgeFloor(catalog, engine, player);
            if ((map != WaterPath && map != BridgePath) || !ArmletChestClosed()) return;
            var step = map == WaterPath ? Exit(catalog, 10, BridgePath) : ReadJump(context, engine, player, field);
            if (step == null) return;

            int chestUid = map == WaterPath ? 12 : 18;
            string script = map == WaterPath ? WaterChest : BridgeChest;
            var chest = engine.GetObjUID(chestUid) as Actor;
            bool chestMatches = chest != null && chest.model == 75 && Matches(chest, chestUid, script);
            int jumpIndex = -1;
            int chestIndex = -1;
            for (int index = 0; index < catalog.Targets.Count; index++)
            {
                var item = catalog.Targets[index];
                if (item.Category != NavigationCategory.Objects || item.PhysicalIdentity.Kind != TargetKind.FieldActor) continue;
                if (map == BridgePath && item.PhysicalIdentity.NativeId == JumpUid) jumpIndex = index;
                if (chestMatches && item.PhysicalIdentity.NativeId == chestUid) chestIndex = index;
            }
            if (chestIndex >= 0)
            {
                string label = map == WaterPath ? "Chest on the ledge. Take the upper path" : "Chest on the ledge. Jump down";
                catalog.Targets[chestIndex] = Via(catalog.Targets[chestIndex], step, label);
                if (jumpIndex >= 0) catalog.Targets.RemoveAt(jumpIndex);
            }
            else if (map == BridgePath)
            {
                if (jumpIndex >= 0) catalog.Targets[jumpIndex] = step;
                else catalog.Targets.Add(step);
            }
        }

        private static void ReviewApparition(NavigationContext context, CatalogResult catalog, EventEngine engine, Actor player)
        {
            int uid = EIcon.NavigationPresentedUid;
            if (uid < 0) return;
            foreach (var target in catalog.Targets)
            {
                if (target.IsInteractive || target.Evidence != VisibilityEvidence.NativeInteractionIcon ||
                    target.PhysicalIdentity.Kind != TargetKind.FieldActor || target.PhysicalIdentity.NativeId != uid) continue;
                if (ReadApparitionOffer(context, engine, player, engine.GetObjUID(uid) as Actor) == null) return;
                var mesh = NativeFieldTargets.ReadGeometryForStory(context);
                if (mesh == null || !mesh.IsSegmentClear(context.Position, context.Position)) return;
                target.IsInteractive = true;
                target.UseViewingPoint = false;
                target.UseApprovedApproaches = true;
                target.ApproachCandidates = new[] { context.Position };
                target.Availability = null;
                return;
            }
        }

        internal static InteractionReadiness ReadApparitionOffer(NavigationContext context, EventEngine engine, Actor player, Actor actor)
        {
            int map = FF9StateSystem.Common.FF9.fldMapNo;
            if (map < Hole || map > BridgePath || actor == null || actor.uid != EIcon.NavigationPresentedUid ||
                (actor.flags & (EventEngine.flagShow | EventEngine.flagTalkInh)) != EventEngine.flagShow ||
                !NativeStoryPrerequisites.IsCurrentPinnacleSpeaker(context, map, actor) ||
                !NativeFieldTalkability.IsTalkable(actor)) return null;

            // Collision queues Push after the interpreter runs. The visible apparition's Push
            // returns next frame before the native Confirm check gets its turn.
            int push = engine.GetIP(actor.sid, EventEngine.tagPush, actor.ebData);
            int offset = actor.sofs * 4;
            if (push < 0 || actor.level != 1 || actor.ip != push || actor.sx != 2 || actor.wait != 0 ||
                actor.isAdditionCommand || actor.state != EventEngine.stateRunning || !ReferenceEquals(actor.currentByte, actor.ebData) ||
                actor.buffer == null || offset < 0 || offset > actor.buffer.Length - 8 ||
                (BitConverter.ToInt32(actor.buffer, offset + 4) & 0xffffff) != 0xff0700) return null;
            var controller = player.fieldMapActorController;
            if (!ReferenceEquals(controller.walkMesh.Collision(controller, 4, out float distance), actor) || distance >= 0)
                return null;
            int angle = EventCollision.CollisionAngle(player, actor);
            if (angle <= -1024 || angle >= 1024) return null;
            return new InteractionReadiness {
                InRange = true, SelectedCollision = true, Talkable = true, FacingWithinArc = true, FacingError = angle,
                Evidence = "Native talk prompt and queued proximity handler"
            };
        }

        private static void ReviewMonty(NavigationContext context, CatalogResult catalog, EventEngine engine, Actor player, FieldMap field)
        {
            const int uid = 10;
            var moogle = engine.GetObjUID(uid) as Actor;
            if (moogle == null || moogle.model != 220 || !Matches(moogle, uid, MontyScript)) return;
            foreach (var target in catalog.Targets)
            {
                if (target.Category != NavigationCategory.People || !target.IsInteractive ||
                    target.PhysicalIdentity.Kind != TargetKind.FieldActor || target.PhysicalIdentity.NativeId != uid) continue;
                var mesh = NativeFieldTargets.ReadGeometryForStory(context);
                var hillside = mesh?.Find(2);
                if (mesh == null || !mesh.Permitted(hillside)) return;
                // Both stops are on the trail facing the moogle across the gap.
                var points = new System.Collections.Generic.List<NavigationPoint>();
                float talkRange = 4 * (player.talkRad + moogle.talkRad) + moogle.speed + 60;
                foreach (var xz in new[] { new NavigationPoint(-340, 0, -570), new NavigationPoint(-350, 0, -580) })
                {
                    if (!hillside.Contains(xz)) continue;
                    var point = new NavigationPoint(xz.X, hillside.Height(xz), xz.Z);
                    if (Math.Abs(point.Y - target.Position.Y) < 390 &&
                        FieldGeometrySnapshot.Horizontal(point, target.Position) < talkRange - 12 &&
                        mesh.IsSegmentClear(point, point) && NativeFieldTargets.StorySurfacePresented(field, point))
                        points.Add(point);
                }
                if (points.Count != 0)
                {
                    target.ApproachCandidates = points.ToArray();
                    target.UseApprovedApproaches = true;
                }
                return;
            }
        }

        private static void ReviewBridgeFloor(CatalogResult catalog, EventEngine engine, Actor player)
        {
            bool onLedge = player.fieldMapActorController.activeFloor == 1;
            var waterExit = Exit(catalog, onLedge ? 15 : 13, WaterPath);
            if (waterExit == null) return;
            var chest = engine.GetObjUID(19) as Actor;
            bool chestMatches = chest != null && chest.model == 75 && Matches(chest, 19, BridgeWaterChest);
            for (int index = catalog.Targets.Count - 1; index >= 0; index--)
            {
                var target = catalog.Targets[index];
                int uid = target.PhysicalIdentity.NativeId;
                if (target.Category == NavigationCategory.Exits && uid == (onLedge ? 13 : 15))
                {
                    catalog.Targets.RemoveAt(index);
                }
                else if (target.Category == NavigationCategory.Exits && onLedge && uid == 12)
                {
                    catalog.Targets[index] = Via(target, waterExit, "Exit to Pinnacle Rocks, Path, Area 1 via the water path");
                }
                else if (target.Category == NavigationCategory.Objects && target.PhysicalIdentity.Kind == TargetKind.FieldActor &&
                    uid == 19 && chestMatches && target.IsInteractive)
                {
                    catalog.Targets[index] = Via(target, waterExit, "Chest by the water. Take the water path");
                }
            }
        }

        private static NavigationTarget Via(NavigationTarget target, NavigationTarget step, string label)
        {
            var route = FieldTargetCatalog.Copy(step);
            route.Id = target.Id;
            route.Category = target.Category;
            route.PhysicalId = step.PhysicalIdentity;
            route.Label = label;
            return route;
        }

        internal static NavigationTarget NextStoryStep(NavigationContext context, CatalogResult catalog, Actor player, int map, int flags)
        {
            var field = player?.fieldMapActorController?.fieldMap;
            if (!context.CanControl || !Matches(map, field)) return null;
            switch (map)
            {
                case Hole:
                    return (flags & Hero) == 0
                        ? WalkTo(context, field, 12, "Follow the path into the cave")
                        : Exit(catalog, 10, MontysPath) ?? Exit(catalog, 11, MontysPath);
                case MontysPath:
                    if ((flags & Hero) == 0) return Exit(catalog, 15, Hole);
                    if ((flags & Beginning) == 0) return WalkTo(context, field, 95, "Follow the upper branch");
                    if ((flags & Human) == 0) return WalkTo(context, field, 66, "Follow the lower path under the tree");
                    return (flags & Cooperation) == 0
                        ? Exit(catalog, 17, WaterPath) ?? WalkTo(context, field, 66, "Follow the lower path under the tree")
                        : Exit(catalog, 16, BridgePath) ?? WalkTo(context, field, 95, "Follow the upper branch");
                case WaterPath:
                    if ((flags & Cooperation) == 0) return WalkTo(context, field, 175, "Follow the path by the water");
                    if ((flags & (Hero | Beginning | Human)) != (Hero | Beginning | Human)) return Exit(catalog, 9, MontysPath);
                    return (flags & Silence) == 0 ? Exit(catalog, 11, BridgePath) : Exit(catalog, 10, BridgePath);
                case BridgePath:
                    bool onLedge = player.fieldMapActorController.activeFloor == 1;
                    if (onLedge)
                        return (flags & Silence) == 0
                            ? WalkTo(context, field, 125, "Follow the lower ledge")
                            : Exit(catalog, 15, WaterPath);
                    if (flags == 31) return WalkTo(context, field, 29, "Follow the path toward the exit");
                    if ((flags & (Hero | Beginning | Human)) != (Hero | Beginning | Human))
                        return Exit(catalog, 12, MontysPath);
                    return Exit(catalog, 13, WaterPath) ?? ((flags & Silence) == 0 ? FindJump(catalog) : null);
                default: return null;
            }
        }

        private static NavigationTarget Exit(CatalogResult catalog, int uid, int destination)
        {
            foreach (var target in catalog.Targets)
                if (target.Category == NavigationCategory.Exits && target.PhysicalIdentity.Kind == TargetKind.FieldExit &&
                    target.Id.NativeId == uid && target.PhysicalIdentity.NativeId == uid && target.NextFields != null &&
                    Array.IndexOf(target.NextFields, destination) >= 0 && target.ApproachCandidates?.Length > 0)
                    return target;
            return null;
        }

        private static NavigationTarget FindJump(CatalogResult catalog)
        {
            foreach (var target in catalog.Targets)
                if (target.Category == NavigationCategory.Objects && target.PhysicalIdentity.Kind == TargetKind.FieldActor &&
                    target.PhysicalIdentity.NativeId == JumpUid && target.InteractionUid == JumpUid && target.ApproachCandidates?.Length > 0)
                    return target;
            return null;
        }

        private static NavigationTarget WalkTo(NavigationContext context, FieldMap field, int triangle, string label)
        {
            var mesh = NativeFieldTargets.ReadGeometryForStory(context);
            var support = mesh?.Find(triangle);
            if (mesh == null || !mesh.Permitted(support)) return null;
            var point = support.Center;
            if (!mesh.IsSegmentClear(point, point) || !NativeFieldTargets.StorySurfacePresented(field, point))
            {
                bool found = false;
                foreach (var alternative in FieldRegionApproaches.Interaction(support.Vertices, mesh))
                {
                    if (mesh.At(alternative)?.Id != triangle || !NativeFieldTargets.StorySurfacePresented(field, alternative)) continue;
                    point = alternative;
                    found = true;
                    break;
                }
                if (!found) return null;
            }
            return new NavigationTarget {
                Id = new TargetIdentity(TargetKind.RegionPoint, context.LogicalMapKey, triangle),
                Category = NavigationCategory.Objects, Label = label, Position = point, Generation = context.Generation,
                Evidence = VisibilityEvidence.ReviewedBackground, UseApprovedApproaches = true, ApproachCandidates = new[] { point }
            };
        }

        private static NavigationTarget ReadJump(NavigationContext context, EventEngine engine, Actor player, FieldMap field)
        {
            if (player.fieldMapActorController.activeFloor == 1) return null;
            var quad = engine.GetObjUID(JumpUid) as Quad;
            if (!Matches(quad, JumpUid, JumpScript) || !NativeFieldTalkability.IsTalkable(quad) ||
                !EventCollision.NavigationCanApproachTalkQuad(player, quad)) return null;
            var mesh = NativeFieldTargets.ReadGeometryForStory(context);
            if (mesh == null) return null;
            var points = NativeFieldTargets.PinnacleJumpApproaches(context, mesh, engine, field, quad);
            if (points.Length == 0) return null;
            return new NavigationTarget {
                Id = new TargetIdentity(TargetKind.FieldActor, context.LogicalMapKey, JumpUid),
                Category = NavigationCategory.Objects, Label = "Jump down to the ledge", Position = points[0],
                Evidence = VisibilityEvidence.ReviewedBackground, Generation = context.Generation,
                IsInteractive = true, InteractionUid = JumpUid, UseApprovedApproaches = true, ApproachCandidates = points
            };
        }

        private static bool ArmletChestClosed()
        {
            var globals = FF9StateSystem.EventState.gEventGlobal;
            return globals != null && globals.Length > (ArmletChestBit >> 3) &&
                (globals[ArmletChestBit >> 3] & (1 << (ArmletChestBit & 7))) == 0;
        }

        private static bool Matches(Obj obj, int uid, string hash)
            => obj != null && obj.uid == uid && obj.sid == uid && obj.ebData != null && NativeFieldTargets.Fingerprint(obj.ebData) == hash;
    }

    public static partial class NativeFieldTargets
    {
        internal static NavigationPoint[] PinnacleJumpApproaches(NavigationContext context, FieldGeometrySnapshot mesh,
            EventEngine engine, FieldMap field, Quad quad)
        {
            var surface = Surface(mesh, quad, field);
            return FieldStoryRegionApproaches.Read(true, true, surface.Approaches,
                point => engine.NavigationTalkQuadAt(point.X, point.Z) == quad,
                point => mesh.At(point)?.Floor == 0 && FieldRegionApproaches.CanStopInside(surface.Region, point, 20) &&
                    StorySurfacePresented(field, point), context.Position);
        }
    }
}
