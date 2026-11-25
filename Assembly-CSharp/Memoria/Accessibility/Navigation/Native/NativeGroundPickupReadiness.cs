using System;
using System.Collections.Generic;
using UnityEngine;

namespace Memoria.Accessibility.Navigation
{
    public static partial class NativeFieldTargets
    {
        // Installed field entries whose Push only presents an exclamation and returns,
        // and whose native Confirm grants the pickup there or through its bound callback.
        // Keys use the game's field * 1000 + SID convention.
        private static readonly Dictionary<int, string> GroundPickupEntries = new Dictionary<int, string>
        {
            { 350027, "098cae098437bcfdae61dbe4d8e06afd7acc958c83b820a02a47c2ba60349161" },
            { 455004, "e943a3bf80cd6b228351f1772f6206ac9c169870ee08e25a9155e51cc34aa02c" },
            { 850004, "9a72575c889fe4f0e471ad6f696ebbdddd78243fb6ae0efe3633aca93b78fcc5" },
            { 850005, "a832f30dd6ea09fa7e4891b867851c7165f4a4557bedc36273d8894191ea80b8" },
            { 854001, "59e6e7d32ba9cdf708db19c220cf4616c10084772bfa7d17dde91f08c42fe9c3" },
            { 854002, "f70b98c7e30115991a1e318ee341d46579c852a94345ac4edb106cf20b11f07f" },
            { 906007, "f1cfa482345e961f99af32058aad27b805a09bcc22eddb164bee6db359b0357d" },
            { 907016, "a4b6fa93874e39dfdd653b029472ed956b1d55f5bebab6749cc3bd608d60744b" },
            { 912015, "60d994e19f5dedce879f68ef10dd3393b9099e59514b216f6f79ceef52edee1e" },
            { 1417006, "331c139a53bf7675d4a9e57314fd488396ede8618b1a09c8422a795d645047dc" },
            { 1422011, "4c4d6f6bdf212c554d5b5c50fda5670bef647c396789b7fdbbdbac1bbb825586" },
            { 1450011, "a8cfe1830dbe4a73f09a6e9ff45b78705625427d316f824387e258d98fa9fea1" },
            { 1454008, "5674f37ebfcb3309b593ba2b4e4ee034f09de62ac1338b9e1b420bc80433db86" },
            { 1459008, "ec55bf2825cc86457227f004a43d59e9807482726dac7d47fe7c9f7fc2b437c5" },
            { 1460010, "0d1cdbe89d82fe1577739816f3374fe984fb1b0c310ad1c818218b922955aadc" },
            { 1460013, "9083fcd9af76968e8ddf34f0a4bfa405b0a629790431b0d5f75c9c67d38265b6" },
            { 1501013, "81c0affefd5d933a25043b021f31ef86cd410d5a8d7b515df3c1151ccd5cc6c1" },
            { 1505012, "43eafaf49800a2da6195318cf3647103a3a36d3d5bb1b7cabae8c469f593f1f0" },
            { 1508013, "e9ba51b49746a7a5254f9c42f7384fdbcb1311460a06bd6a09b115f6d4bd0363" },
            { 1509009, "b0501b91b7ac095917ee134627cedbfb2d1a989f630249217a11b0ad8ad603e3" },
            { 1601012, "8df320a30eac26f397cfb63aa40aee22ac3d75d2d3cd363a17de6c7dfe990c7e" },
            { 1607025, "923584366ae904814f643e9746e95f3e8086ad0913c3dee2a74ffb64f6c53fb1" },
            { 1701005, "14edb13a5f5552b028e71aec2e525c6ad85713fc5a8f50a5540b940c2eb37628" },
            { 1706009, "601f8d1a195667816c0de064c9ccc6a08b3ff40b77d4a3b18d243b9899647979" },
            { 1906008, "5f281419b8d531648a48c0e89a83ba4ace6c77d84ffbbeb25a1610c7d95f79c8" },
            { 1907019, "7f85dd2a72be7ebbc021d6a7e1ebc7220f5155d2e7ae23a7f57b7eb300f4f429" },
            { 1912018, "ec8886e5feb179f1e4cdb7dae148f3f514bd65eab0bd70d7e45c4fe25fa254c0" },
            { 1950007, "e91521fe88ae262fcce4ac6ef4634d6d9efa141eedbfef7dc92f641426e6af61" },
            { 2303009, "d131dbf4b040f112fa04b2474beeced6f045df6dd013188c47e26856c145338b" },
            { 2350024, "b0f18eec1420affbc4ee93afba23284ac6d83668810ec41f049a1a294f75ae13" },
            { 2350025, "7f3859da68b451c5534252fe423443d732e420ff7fd48d7f38e81aab827a0824" },
            { 2353006, "ae7158fbe7ab34eadad533dc58ae778052d953f5883b52a001ca0778e9961d46" },
            { 2353007, "f9186a405153f2fdede1108f4cfc606619f90e945976f2a7499bb63a9c3e3c2c" },
            { 2354009, "839fa363731c95b4416b500a85fea5d1d8aad6bbbb3f37b891fb0232bf382900" },
            { 2654012, "3760bf2cfda1a8e9742753038e2ea93271be266bbae45a7038a5acd8aa570adb" },
            { 2658008, "fcb5ee3c1d46940b9bef2364cfd97587e2021e7d0ed427bb5138119c72ad24e5" },
            { 2705005, "f94c2cc115be80a918a45fd3f3bde9e123d173da44430cb015efb758c5481e0e" },
            { 2800023, "98dc242f9db1e2ec5e0984df039d7eb73f02f5a95fe5c89ae31eeadaef3d46f9" },
            { 2853008, "0e344d51128037fb6c854a1cf6075abb5c6ce4d2017a52475b37c396fa803664" },
            { 2908005, "18f5e82c0adbe08d3ca836077bd6382d030dd8d2d3ddb07bf462000d9e65edcc" },
            { 2918007, "03cce1a34d12ac1ee49eb5017b977aabf7c6a68de40e514d120a9ded67bfd3e8" },
            { 3050011, "0561597348a641178c2e2c29ba73cc2c829e80e3f62cffc52961cca0a43479a7" },
            { 3053009, "bb5f9d1b23b0e8eea2a564b7a79a61e414989f6e61a3fb026f41e182e22c356d" },
            { 3058007, "ec55bf2825cc86457227f004a43d59e9807482726dac7d47fe7c9f7fc2b437c5" },
            { 3059011, "7427a09192e3c3c775e361f12b46714aa43006e069d953821969675a05fd82e9" }
        };

        private sealed class GroundPickupCallback
        {
            internal readonly int Uid, Level, Tag;
            internal readonly string Hash;

            internal GroundPickupCallback(int uid, int level, int tag, string hash)
            {
                Uid = uid;
                Level = level;
                Tag = tag;
                Hash = hash;
            }
        }

        // These Confirm handlers each use one synchronous request to an original
        // actor grant routine. Both scripts and the actor's request level must match.
        private static readonly Dictionary<int, GroundPickupCallback> GroundPickupCallbacks = new Dictionary<int, GroundPickupCallback>
        {
            { 850004, new GroundPickupCallback(10, 6, 15, "a731a8e7f036469b0f7ff8705fd2661a7c91b82f200c712695a04ee241243f03") },
            { 850005, new GroundPickupCallback(10, 6, 14, "a731a8e7f036469b0f7ff8705fd2661a7c91b82f200c712695a04ee241243f03") },
            { 854001, new GroundPickupCallback(7, 6, 15, "9d897c2bcb899689e28675ef38301109f1d3d44745cdb52bc15dd131daf933dd") },
            { 854002, new GroundPickupCallback(7, 6, 14, "9d897c2bcb899689e28675ef38301109f1d3d44745cdb52bc15dd131daf933dd") },
            { 1601012, new GroundPickupCallback(17, 6, 17, "71eb69fbd017ff26c55bf1d799512e5e6da8e911cdb2f927657e57a3ac0dfd0c") },
            { 1701005, new GroundPickupCallback(10, 6, 13, "5840e057e9c7f0c374bb19d5827530713a0468b5e16d25638f37bf0e49c836c6") },
            { 2800023, new GroundPickupCallback(27, 2, 27, "d94da0094f8bda71bbb95f59dab323463509df06b707095b16c3631a6cb54db0") }
        };

        private static readonly FieldInteractionOpcodes GroundPickupOpcodes = new FieldInteractionOpcodes { IconOpcode = (int)EBin.event_code_binary.FICON };

        internal static bool IsPresentedGroundPickup(NavigationContext context, EventEngine engine, Quad quad)
        {
            if (!IsGroundPickupRegion(context, engine, quad)) return false;
            return GroundPickupCue(context, engine, engine.GetControlChar() as Actor, quad);
        }

        internal static InteractionReadiness ReadGroundPickupReadiness(NavigationContext context, EventEngine engine, Actor player, Quad quad)
        {
            if (!IsGroundPickupRegion(context, engine, quad)) return null;
            if (!GroundPickupCue(context, engine, player, quad) || !NativeFieldTalkability.IsTalkable(quad)) return new InteractionReadiness();

            bool ready = EventCollision.NavigationCanTalkQuad(player, quad);
            // Event code presents the bubble and returns before CollisionRequest tests Confirm.
            // CollisionRequest then queues the next Push at level 1, where navigation observes it.
            // The saved idle frame proves what will resume when that Push finishes.
            if (quad.level == 1 && EventCollision.NavigationHasTouchFacing(player, quad))
                ready = IsQueuedStockQuadPush(engine, quad);

            var result = FieldInteractionRules.PushReadiness(true, true, ready, true);
            result.Evidence = "Native pickup prompt is displayed. Confirm remains player controlled.";
            return result;
        }

        private static bool IsGroundPickupRegion(NavigationContext context, EventEngine engine, Quad quad)
        {
            if (context == null || engine == null || engine.gMode != 1 || quad == null || quad.ebData == null || quad.uid != quad.sid) return false;
            int field = FF9StateSystem.Common.FF9.fldMapNo;
            string hash;
            return GroundPickupEntries.TryGetValue(field * 1000 + quad.sid, out hash) &&
                context.LogicalMapKey != null && context.LogicalMapKey.StartsWith("field:" + field + ":", StringComparison.Ordinal) &&
                String.Equals(Fingerprint(quad.ebData), hash, StringComparison.Ordinal);
        }

        private static bool GroundPickupCue(NavigationContext context, EventEngine engine, Actor player, Quad quad)
        {
            if (!context.CanControl || !engine.GetUserControl() || player == null || player.uid != context.PlayerIdentity ||
                quad.state != EventEngine.stateRunning || quad.wait != 0 || quad.isAdditionCommand || !ReferenceEquals(quad.currentByte, quad.ebData) ||
                EIcon.NavigationPresentedUid != quad.uid || EIcon.NavigationScriptIconUid != quad.uid || EIcon.SFIconType != BubbleUI.IconType.Exclamation ||
                engine.TreadQuad(player, 2) != quad || engine.TreadQuad(player, 4) != quad ||
                !engine.NavigationContainsQuad(quad, context.Position.X, context.Position.Z)) return false;

            int field = FF9StateSystem.Common.FF9.fldMapNo;
            if (!GroundPickupCallbackIsRequestable(engine, field * 1000 + quad.sid)) return false;
            var snapshot = new ScriptSnapshot(FF9StateSystem.EventState.gEventGlobal, engine.GetMapVar(), null);
            // Quan's cave shares one prompt between two direct grants. Its second arm uses the
            // native read-only HaveItem operation, which the detached expression reader omits.
            if (field == 1950)
                return snapshot.Read(0, 1, 7571) == 0 || snapshot.Read(0, 7, 355) == 4095 && snapshot.Read(0, 1, 7570) == 0 && ff9item.FF9Item_GetCount_Generic(304) > 0;

            var push = Decode(engine, quad, EventEngine.tagPush);
            return FieldInteractionGuard.Evaluate(push, snapshot, GroundPickupOpcodes, true,
                (uid, property) => GroundPickupPlayerProperty(player, uid, property)) == FieldInteractionState.Available;
        }

        private static bool GroundPickupCallbackIsRequestable(EventEngine engine, int key)
        {
            GroundPickupCallback callback;
            if (!GroundPickupCallbacks.TryGetValue(key, out callback)) return true;
            var actor = engine.GetObjUID(callback.Uid) as Actor;
            return actor != null && actor.uid == callback.Uid && actor.sid == callback.Uid && actor.level > callback.Level &&
                actor.state == EventEngine.stateRunning && actor.ebData != null && !actor.isAdditionCommand && ReferenceEquals(actor.currentByte, actor.ebData) &&
                String.Equals(Fingerprint(actor.ebData), callback.Hash, StringComparison.Ordinal) &&
                engine.GetIP(actor.sid, callback.Tag, actor.ebData) != engine.nil;
        }

        private static int? GroundPickupPlayerProperty(Actor player, int uid, int property)
        {
            if (uid != 250 && uid != player.uid) return null;
            // EBin.getvobj uses the actor's rounded, negated height and 256-step facing.
            if (property == 1) return -Mathf.RoundToInt(player.pos[1]);
            if (property == 3) return (EventEngineUtils.ConvertFloatAngleToFixedPoint(player.rotAngle[1]) >> 4) & 255;
            return null;
        }

        private static bool IsQueuedStockQuadPush(EventEngine engine, Quad quad)
        {
            if (quad.state != EventEngine.stateRunning || quad.isAdditionCommand || !ReferenceEquals(quad.currentByte, quad.ebData))
                return false;
            int offset = 4 * quad.sofs;
            if (quad.buffer == null || offset < 0 || offset > quad.buffer.Length - 8) return false;
            int entry = engine.GetIP(quad.sid, EventEngine.tagPush, quad.ebData);
            var queue = new FieldTouchConfirmation
            {
                OriginalBytes = true,
                Running = true,
                Level = quad.level,
                Ip = quad.ip,
                Depth = quad.sx,
                Wait = quad.wait,
                SavedIp = BitConverter.ToInt32(quad.buffer, offset),
                SavedStatus = BitConverter.ToInt32(quad.buffer, offset + 4)
            };
            return queue.IsQueuedPush(entry);
        }
    }
}
