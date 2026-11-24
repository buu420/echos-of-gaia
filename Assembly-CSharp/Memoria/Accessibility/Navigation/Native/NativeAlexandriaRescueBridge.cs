namespace Memoria.Accessibility.Navigation
{
    internal static class NativeAlexandriaRescueBridge
    {
        private const int Field = 1202;
        private const string Scene = "field:1202:FBG_N02_ALXC_MAP869_AC_STB_0";
        private const string DirectorHash = "fc461b5cda54b04fb5d93c00b0a9ce75b49eb3e9eef0a29d830406a2ebba6593";
        private const string PlayerHash = "bf8357dad7e08edf96b01a6c3edd9418d7e2a5803b64fd1480e70eb0052b66f9";
        private const string AlternatePlayerHash = "d67435646a8c040cbd1d64229f5b3c0b6bbd8551467963d3c0595438f8e6e960";
        private const string EntranceHash = "f6f332d08c5c4ea5a12684be73b7fab8224088f293e4ce972b89c658e11a43e3";
        private const string MiddleHash = "59a4409ee1f3de0adf8a2705656e12b1b7013f12a85cac42ef0f9c315395b6ec";
        private const string WalkmeshHash = "25d23be99e7605e4279d7c380dffc6abf96e15f1246ca2cfd566ec96aae6b1a9";

        private static bool Matches(Obj obj, int uid, string hash)
        {
            return obj != null && obj.uid == uid && obj.sid == uid && obj.ebData != null &&
                NativeFieldTargets.Fingerprint(obj.ebData) == hash;
        }

        private static byte[] Read(EventEngine engine)
        {
            var globals = FF9StateSystem.EventState.gEventGlobal;
            if (globals == null || globals.Length < 2 || engine == null || engine.gMode != 1 ||
                !Matches(engine.GetObjUID(0), 0, DirectorHash))
                return null;
            int progress = globals[0] | (globals[1] << 8);
            if (progress < 5075 || progress >= 5080) return null;
            var player = engine.GetControlChar();
            if (!Matches(player, 4, PlayerHash) && !Matches(player, 21, AlternatePlayerHash)) return null;
            var map = engine.GetMapVar();
            return map != null && map.Length > 42 ? map : null;
        }

        // The caller is presenting an ordinary exclamation bubble. The moving
        // platform accepts native Confirm even while walking is disabled.
        internal static string Prompt(int shownUid)
        {
            if ((shownUid != 4 && shownUid != 21 && shownUid != 14 && shownUid != 16) || FF9StateSystem.Common.FF9.fldMapNo != Field)
                return null;
            var engine = NativeSceneReferences.Read<EventEngine>();
            var map = Read(engine);
            if (map == null) return null;
            if (map[40] == 2 && shownUid == engine.GetControlUID())
                return AlexandriaRescueBridge.DismountCue(shownUid, map[42], map[41], map[34]);
            if (map[40] == 0 && (shownUid == 14 || shownUid == 16) &&
                Matches(engine.GetObjUID(shownUid), shownUid, shownUid == 14 ? EntranceHash : MiddleHash))
                return "Board the rotating bridge. Confirm.";
            return null;
        }

        internal static bool TryAppend(NavigationContext context, CatalogResult catalog)
        {
            if (context == null || catalog == null || !context.Foreground || context.LogicalMapKey != Scene ||
                context.StoryProgress < 5075 || context.StoryProgress >= 5080)
                return false;
            var engine = NativeSceneReferences.Read<EventEngine>();
            var map = Read(engine);
            if (map == null) return false;
            if (map[40] == 2)
            {
                Append(catalog, context, 4, context.Position, "Ride the rotating bridge",
                    "Press Confirm when the cue says to jump to the stairs.", new NavigationPoint[0]);
                return true;
            }
            if (!context.CanControl || map[40] != 0) return false;
            var actor = engine.GetControlChar() as Actor;
            var controller = actor?.fieldMapActorController;
            var field = controller?.fieldMap;
            if (field == null || NativeFieldTargets.CurrentBgiHash(field) != WalkmeshHash) return false;
            int uid = AlexandriaRescueBridge.BoardingRegion(controller.activeTri);
            if (uid == 0) return false;

            var quad = engine.GetObjUID(uid) as Quad;
            if (!Matches(quad, uid, uid == 14 ? EntranceHash : MiddleHash)) return false;
            var geometry = NativeFieldTargets.ReadGeometryForStory(context);
            var anchor = uid == 14 ? new NavigationPoint(-1460, -877, 30) : new NavigationPoint(0, -873, 1570);
            var support = geometry?.At(anchor);
            if (support == null || AlexandriaRescueBridge.BoardingRegion(support.Id) != uid ||
                engine.NavigationTalkQuadAt(anchor.X, anchor.Z) != quad || !NativeFieldTargets.StorySurfacePresented(field, anchor))
                return false;
            var approach = new NavigationPoint(anchor.X, support.Height(anchor), anchor.Z);
            string advice = "Wait for the boarding cue, then press Confirm.";
            if (engine.NavigationTalkQuadAt(context.Position.X, context.Position.Z) == quad)
                Append(catalog, context, uid, context.Position, "Wait for the rotating bridge", advice, new NavigationPoint[0]);
            else
                Append(catalog, context, uid, approach, "Rotating bridge boarding point", advice, new[] { approach });
            return true;
        }

        private static void Append(CatalogResult catalog, NavigationContext context, int uid, NavigationPoint position,
            string label, string advice, NavigationPoint[] approaches)
        {
            catalog.Targets.Add(new NavigationTarget {
                Id = new TargetIdentity(TargetKind.Story, context.LogicalMapKey, uid, 1),
                PhysicalId = approaches.Length == 0 ? (TargetIdentity?)null : new TargetIdentity(TargetKind.RegionPoint, context.LogicalMapKey, uid),
                Category = NavigationCategory.Story, Generation = context.Generation,
                Position = position, Label = label, Availability = advice, ApproachCandidates = approaches,
                UseApprovedApproaches = true, Evidence = VisibilityEvidence.ReviewedBackground
            });
        }
    }
}
