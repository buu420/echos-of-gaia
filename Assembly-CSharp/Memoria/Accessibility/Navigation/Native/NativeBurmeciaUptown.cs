using System;

namespace Memoria.Accessibility.Navigation
{
    internal static class NativeBurmeciaUptown
    {
        private const int Courtyard = 759, EarlyBalconies = 761, Balconies = 762, Square = 763, PalaceEntrance = 766;
        private const int StatueFalls = 3880, PalaceScene = 3900;
        private const int CrossingDoor = 12, NearDoor = 11, SquareDoor = 7, LeftReturn = 5, RightReturn = 6;
        private const string BalconyMesh = "e86c61f200a45fff6320cfc04159952f54313422caee47987467f8d91a2661a5";
        private const string EarlySquareScript = "d97fa0db3b2263c2ea3a2e2782264c6893a7afd6bd952c4c53d03afad4cde581";
        private const string SquareScript = "f8d939eb763ca9ec070dede4aa9db644f1c8b52a0ef4bce73b652e7f9a5a5717";
        private static readonly int[] RightBalcony = {
            20, 22, 23, 26, 27, 28, 29, 34, 35, 36, 37, 39, 42, 43, 44, 45, 46, 47, 48, 49, 62, 68, 69, 70, 71, 83, 85
        };
        private static readonly string[] EarlyChests = {
            "bb62d80b8610f0b3d641386f513765d52c021c774763fe76e6c2c3102780f862",
            "4a7d1ac3b289420ef522bf7bebdb49fce9fede8e19672f461a98dbbd27a5a90a",
            "2ef7c937f0aac093d9a2ce9fce44304e3d28df673fdd58c4957a17c01663b0cd"
        };
        private static readonly string[] Chests = {
            "66689368cee095dd09271ab0acce1d51a85e08f1561493c599aaa7d5ee5c5e0e",
            "296cd52d37429e1b0649842ea13af55434b98b587e7952c19f60adfb1bf07a28",
            "08e696bc66d2e864abd60e4c4865403e9c4b71c3e1fdd28279d2923581e2f03d"
        };

        // Returns true when this pass supplied the current Story target.
        internal static bool Review(NavigationContext context, CatalogResult catalog)
        {
            int map = FF9StateSystem.Common.FF9.fldMapNo;
            bool storyVisit = context.StoryProgress < PalaceScene;
            bool statueFallen = context.StoryProgress >= StatueFalls;
            if (!context.CanControl || context.StoryProgress < 3870 ||
                (map != Courtyard && map != EarlyBalconies && map != Balconies && !(map == Square && storyVisit && statueFallen)))
                return false;

            var engine = NativeSceneReferences.Read<EventEngine>();
            var player = engine?.GetControlChar() as Actor;
            var controller = player?.fieldMapActorController;
            if (controller?.fieldMap == null || engine.gMode != 1 || context.PlayerIdentity != player.uid)
                return false;

            if (map == Square)
            {
                var palace = Exit(catalog, engine, 11, "1cb20dc508663ff2e5df89ab92557d1fb6de55b38a8f4e38556351a3b2d547b2");
                return GoesTo(palace, PalaceEntrance) &&
                    AddStory(context, catalog, palace, PalaceScene, "Continue to the palace entrance");
            }
            if (map == Courtyard)
            {
                if (!statueFallen || !Matches(player, 16, "1ed7a54256148fa922b8fadbc86c9b2f597d3c9b00e86b40e7d11be1c285bda2") ||
                    !BurmeciaStatuePassage.IsOpen(NativeFieldTargets.ReadGeometryForStory(context)))
                    return false;

                var left = Exit(catalog, engine, CrossingDoor, "f7ccfb042b118ad635270ef4ff86cce12933a59437c9d994765ecc8dcf461c2e");
                var right = Exit(catalog, engine, NearDoor, "65a0e41340768f3fde3968d9051b718d832e8b5cc51a9e5e6555435fe6b17269");
                if (right != null) right.Label = "Right balcony door";
                if (left == null) return false;
                left.Label = "Left balcony door";
                return storyVisit && GoesTo(left, Balconies) &&
                    AddStory(context, catalog, left, PalaceScene, "Go through the left balcony door toward Burmecia, Square");
            }

            bool early = map == EarlyBalconies;
            string playerScript = early ? "130c940c9de4e268f70de3e531ce1e6c0daa5136f5ae8dbd3c2c81c9f52ee465"
                                        : "394b3c3fe5a4481aebb8fe6a3a14c5f241115c937559db411f637fc6aec95bc9";
            if (!Matches(player, 8, playerScript) || NativeFieldTargets.CurrentBgiHash(controller.fieldMap) != BalconyMesh)
                return false;

            var geometry = NativeFieldTargets.ReadGeometryForStory(context);
            var support = geometry?.Find(controller.activeTri);
            if (support == null || !geometry.Permitted(support) || !support.Contains(context.Position) ||
                Math.Abs(support.Height(context.Position) - context.Position.Y) > 1)
                return false;

            bool rightSide = Array.IndexOf(RightBalcony, support.Id) >= 0;
            var leftReturn = Exit(catalog, engine, LeftReturn, "e6dd5a32a7368a615ab822cc5a74f68de3f521aa0febc48167a7812460dcd6c7");
            var rightReturn = Exit(catalog, engine, RightReturn, "cda484f7b7f47894945f15b11116481eb5354d5382ce4452da46408f4c7acd4e");
            var square = Exit(catalog, engine, SquareDoor, early ? EarlySquareScript : SquareScript);
            var next = rightSide ? rightReturn : leftReturn;
            bool courtyardExit = GoesTo(next, Courtyard);
            if (courtyardExit) next.Label = "Exit to the courtyard";
            var oppositeDoor = rightSide ? leftReturn : rightReturn;
            if (oppositeDoor != null)
            {
                oppositeDoor.Label = "Courtyard door on the other balcony";
                oppositeDoor.Availability = "Use the courtyard door on this balcony to go outside.";
                oppositeDoor.ApproachCandidates = new NavigationPoint[0];
            }

            var scripts = early ? EarlyChests : Chests;
            for (int index = 0; index < catalog.Targets.Count; index++)
            {
                var target = catalog.Targets[index];
                int uid = target.PhysicalIdentity.NativeId;
                bool chest =
                    target.Category == NavigationCategory.Objects && target.PhysicalIdentity.Kind == TargetKind.FieldActor &&
                    target.IsInteractive && uid >= 2 && uid <= 4 &&
                    engine.GetObjUID(uid) is Actor actor && actor.model == 75 && Matches(actor, uid, scripts[uid - 2]);
                if (chest && uid <= 3 && ReviewChest(target, geometry, uid, rightSide)) continue;
                if (rightSide && courtyardExit && (chest || ReferenceEquals(target, square)))
                {
                    // Keep the presented destination's selection and use the door on this balcony.
                    var via = FieldTargetCatalog.Copy(next);
                    via.Id = target.Id;
                    via.Category = target.Category;
                    via.PhysicalId = next.PhysicalIdentity;
                    via.Label = target.Label + (statueFallen
                        ? ". Return to the courtyard and cross the fallen statue"
                        : ". Return to the courtyard and take the ground-floor hall entrance");
                    catalog.Targets[index] = via;
                }
            }
            if (!statueFallen)
                return storyVisit && courtyardExit &&
                    AddStory(context, catalog, next, StatueFalls, "Return to the courtyard for the ground-floor hall entrance");
            if (!rightSide)
                return storyVisit && GoesTo(square, Square) &&
                    AddStory(context, catalog, square, PalaceScene, "Continue to Burmecia, Square");
            return storyVisit && courtyardExit &&
                AddStory(context, catalog, next, PalaceScene, "Return to the courtyard to reach Burmecia, Square");
        }

        private static bool ReviewChest(NavigationTarget target, FieldGeometrySnapshot geometry, int uid, bool rightSide)
        {
            int collected = uid == 2 ? 7296 : 7311;
            var globals = FF9StateSystem.EventState.gEventGlobal;
            if (globals != null && globals.Length > collected / 8 && (globals[collected / 8] & 1 << (collected % 8)) != 0)
            {
                target.IsInteractive = false;
                target.UseApprovedApproaches = true;
                target.ApproachCandidates = new NavigationPoint[0];
                target.Availability = "Already opened";
                return true;
            }
            if (rightSide) return false;

            // On the inner ring, the neighboring chest's native Push takes the Confirm.
            // The west aisle has a clear standing spot within only the intended chest's range.
            float z = uid == 2 ? -1469 : -1721;
            if (Math.Abs(target.Position.X + 812) > 1 || Math.Abs(target.Position.Z - z) > 1 ||
                Math.Abs(target.Position.Y - 801) > 1 || target.ActorInteractionRange.ContactLimit != 346 ||
                target.ActorInteractionRange.BodyClearance != 256)
                return false;
            var point = new NavigationPoint(-1122, 781, z);
            if (!geometry.IsSegmentClear(point, point)) return false;
            target.UseApprovedApproaches = true;
            target.ApproachCandidates = new[] { point };
            return true;
        }

        private static bool Matches(Obj obj, int uid, string script)
        {
            return obj != null && obj.uid == uid && obj.sid == uid && obj.ebData != null &&
                NativeFieldTargets.Fingerprint(obj.ebData) == script;
        }

        private static NavigationTarget Exit(CatalogResult catalog, EventEngine engine, int uid, string script)
        {
            if (!(engine.GetObjUID(uid) is Quad quad) || !Matches(quad, uid, script)) return null;
            foreach (var target in catalog.Targets)
                if (target.Category == NavigationCategory.Exits && target.PhysicalIdentity.Kind == TargetKind.FieldExit &&
                    target.PhysicalIdentity.NativeId == uid && target.ApproachCandidates?.Length > 0)
                    return target;
            return null;
        }

        private static bool GoesTo(NavigationTarget target, int field)
        {
            return target?.NextFields != null && Array.IndexOf(target.NextFields, field) >= 0;
        }

        private static bool AddStory(NavigationContext context, CatalogResult catalog, NavigationTarget target, int milestone, string label)
        {
            catalog.Targets.Add(StoryTargetCatalog.Wrap(context, milestone, target, label));
            return true;
        }
    }
}
