using Memoria.Accessibility.Navigation;

namespace FF9Access.Tests
{
    internal static class ChestPushReadinessTests
    {
        private const int PushEntry = 98;
        private const int OpenedBit = 7459;
        private const int ChestUid = 15;
        // Field 611, SID 15, SHA256 6a16918aca8e2555d8e0a45daae42d54020918aef985cd0c8f0315af41b9d8c5.
        private static readonly ScriptInstruction[] PushGuard = {
            new ScriptInstruction(PushEntry, 102, 5, new byte[] { 0x7a, 0x02, 0x7f }),
            new ScriptInstruction(102, 105, 3, 1),
            new ScriptInstruction(105, 106, 4),
            new ScriptInstruction(106, 115, 5, new byte[] { 0xe4, 0x23, 0x1d, 0x0e, 0xd6, 0x00, 0x27, 0x7f }),
            new ScriptInstruction(115, 118, 2, 435),
            new ScriptInstruction(118, 121, 0x68, 1),
            new ScriptInstruction(553, 554, 4)
        };

        public static void QueuedChestPushRetainsItsConfirmOffer()
        {
            var queued = QueuedPush();

            var readiness = Read(queued);

            TestAssert.True(readiness.InRange && readiness.Talkable && readiness.FacingWithinArc);
        }

        public static void RunningChestHandlerDoesNotReuseItsPreviousPrompt()
        {
            var busy = QueuedPush();
            busy.Ip = 121;
            TestAssert.False(Read(busy).Talkable);

            busy.Ip = PushEntry;
            busy.Wait = 1;
            TestAssert.False(Read(busy).Talkable);
        }

        public static void ConsumedChestReturnsBeforeItCanOfferConfirm()
        {
            TestAssert.False(Read(QueuedPush(), opened: true).Talkable);
        }

        public static void QueuedChestRequiresItsOwnPromptAndRenderedBody()
        {
            var queued = QueuedPush();
            TestAssert.False(Read(queued, rendered: false).Talkable);

            queued.ShownUid = 16;
            TestAssert.False(Read(queued).Talkable);
        }

        public static void QueuedChestStillRequiresTheNativeCollisionGate()
        {
            TestAssert.False(Read(QueuedPush(), collisionAllowed: false).Talkable);
        }

        private static FieldTouchConfirmation QueuedPush()
        {
            return new FieldTouchConfirmation {
                Uid = ChestUid, ShownUid = ChestUid, Ip = PushEntry, Level = 1, Depth = 2,
                SavedIp = -1, SavedStatus = 0x18ff0700, CanControl = true, OriginalBytes = true, Running = true
            };
        }

        private static InteractionReadiness Read(FieldTouchConfirmation touch, bool opened = false, bool rendered = true, bool collisionAllowed = true)
        {
            var global = new byte[1024];
            if (opened) global[OpenedBit >> 3] = (byte)(1 << (OpenedBit & 7));
            var snapshot = new ScriptSnapshot(global, null, new byte[] { 1 });
            var guard = FieldInteractionGuard.Evaluate(PushGuard, snapshot, new FieldInteractionOpcodes { IconOpcode = 0x68 }, true);
            var gates = new FieldInteractionGates {
                HasPushHandler = true, MapPushAllowed = collisionAllowed, Level = touch.Level,
                QueuedPushOffer = touch.CanConfirmQueuedPush(PushEntry, rendered, guard)
            };
            bool permitted = FieldInteractionRules.Push(gates, guard) == FieldInteractionState.Available;
            return FieldInteractionRules.PushReadiness(true, true, permitted, touch.ShownUid == touch.Uid);
        }
    }
}
