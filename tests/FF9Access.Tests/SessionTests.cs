using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class SessionTests
    {
        public static void FocusOrDisconnectDuringDetachedRandomBattleRequiresResume()
        {
            foreach(var signal in new[]{NavigationSignal.StartAutomatic,NavigationSignal.StartGuidance}) {
                var session=new NavigationSession();session.Update(TestFixtures.Context(),signal);
                var battle=TestFixtures.Context();battle.CanControl=false;battle.GateReason=GateReason.Battle;battle.BattleTransition=BattleTransition.RandomStarted;
                session.Update(battle,NavigationSignal.SceneDetach);session.RevokeAutomaticResume();
                var returned=TestFixtures.Context(generation:3);returned.BattleTransition=BattleTransition.RandomReturned;
                var decision=session.Update(returned,NavigationSignal.None);
                TestAssert.Equal(RouteMode.Paused,decision.Mode);TestAssert.False(decision.IsAutomatic);
                TestAssert.Equal(RouteMode.Guiding,session.Update(returned,NavigationSignal.StartGuidance).Mode);
            }
        }
        public static void RandomBattleRebuildRebindsWithoutOldReferences()
        {
            var session = new NavigationSession();
            session.Update(TestFixtures.Context(), NavigationSignal.StartAutomatic);
            var interrupted = TestFixtures.Context(); interrupted.CanControl = false; interrupted.BattleTransition = BattleTransition.RandomStarted;
            TestAssert.True(session.Update(interrupted, NavigationSignal.SceneDetach).ReleaseMovement);
            var rebuilt = TestFixtures.Context(generation: 3); rebuilt.BattleTransition = BattleTransition.RandomReturned;
            var result = session.Update(rebuilt, NavigationSignal.None);
            TestAssert.Equal(RouteMode.Moving, result.Mode);
            TestAssert.True(result.RequireReplan);
        }
        public static void ScriptedBattleRequiresResume()
        {
            var session = new NavigationSession(); var context = TestFixtures.Context();
            session.Update(context, NavigationSignal.StartAutomatic);
            context.CanControl = false; context.BattleTransition = BattleTransition.ScriptedStarted;
            TestAssert.True(session.Update(context, NavigationSignal.SceneDetach).ReleaseMovement);
            var rebuilt = TestFixtures.Context(generation: 3); rebuilt.BattleTransition = BattleTransition.UncertainReturn;
            TestAssert.Equal(RouteMode.Paused, session.Update(rebuilt, NavigationSignal.None).Mode);
        }
        public static void ControlUnknownDoesNotChooseFirstActor()
        {
            var context = TestFixtures.Context(); context.PlayerIdentity = -1; context.CanControl = false; context.GateReason = GateReason.UnknownActor;
            var decision = new NavigationSession().Update(context, NavigationSignal.StartAutomatic);
            TestAssert.False(decision.Mode == RouteMode.Moving);
            TestAssert.True(decision.ReleaseMovement);
        }
        public static void FocusReturnDoesNotAutoResume()
        {
            var session = new NavigationSession(); var context = TestFixtures.Context();
            session.Update(context, NavigationSignal.StartAutomatic);
            context.Foreground = false;
            TestAssert.True(session.Update(context, NavigationSignal.None).ReleaseMovement);
            context.Foreground = true;
            TestAssert.False(session.Update(context, NavigationSignal.None).Mode == RouteMode.Moving);
            TestAssert.Equal(RouteMode.Guiding, session.Update(context, NavigationSignal.StartGuidance).Mode);
        }
        public static void PausedBeforeRandomBattleRemainsPaused()
        {
            var session = new NavigationSession(); var context = TestFixtures.Context();
            session.Update(context, NavigationSignal.StartAutomatic);
            session.Update(context, NavigationSignal.OpenMenu);
            context.CanControl = false; context.GateReason = GateReason.Battle; context.BattleTransition = BattleTransition.RandomStarted;
            session.Update(context, NavigationSignal.SceneDetach);
            var rebuilt = TestFixtures.Context(generation: 3); rebuilt.BattleTransition = BattleTransition.RandomReturned;
            TestAssert.Equal(RouteMode.Paused, session.Update(rebuilt, NavigationSignal.None).Mode);
        }
        public static void SaveSlotReplacementClearsKnowledge()
        {
            var store = new ObservedIdentityStore();
            store.SwitchSave("slot-1-content-a"); store.Record(new TargetIdentity(TargetKind.FieldActor, "field:100", 20), "Visible person");
            store.SwitchSave("slot-1-content-b"); TestAssert.Equal(0, store.KnownCount);
        }
        public static void SuccessfulOrdinarySaveCarriesOnlyCurrentLineage()
        {
            var store = new ObservedIdentityStore(); var id = new TargetIdentity(TargetKind.FieldActor, "field:100", 20);
            store.SwitchSave("save-a"); store.Record(id, "Observed person"); store.CommitSave("save-a-new-content");
            TestAssert.Equal(1, store.KnownCount);
            store.SwitchSave("unrelated-content"); TestAssert.Equal(0, store.KnownCount);
        }
        public static void UnsavedKnowledgeCannotLeakBackToOlderSave()
        {
            var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ff9-observed-test-" + System.Guid.NewGuid().ToString("N"));
            try {
                var store = new ObservedIdentityStore(directory); var id = new TargetIdentity(TargetKind.FieldActor, "field:100", 20);
                store.CommitSave("old-content"); store.Record(id, "Later person");
                store.SwitchSave("old-content"); TestAssert.Equal(0, store.KnownCount);
                store.Record(id, "Current person"); store.CommitSave("new-content");
                store.SwitchSave("old-content"); TestAssert.Equal(0, store.KnownCount);
                store.SwitchSave("new-content"); TestAssert.Equal(1, store.KnownCount);
            } finally { if (System.IO.Directory.Exists(directory)) System.IO.Directory.Delete(directory, true); }
        }
        public static void MapOrTransportChangesRevokeRoute()
        {
            var session = new NavigationSession(); session.Update(TestFixtures.Context(), NavigationSignal.StartAutomatic);
            var next = TestFixtures.Context("field:101", 2);
            var decision = session.Update(next, NavigationSignal.None);
            TestAssert.True(decision.ReleaseMovement); TestAssert.Equal(RouteMode.Idle, decision.Mode);
        }
        public static void ManualTakeoverCanKeepPassiveGuidance()
        {
            var session = new NavigationSession(); var context = TestFixtures.Context();
            session.Update(context, NavigationSignal.StartAutomatic);
            var result = session.Update(context, NavigationSignal.ManualMovement);
            TestAssert.Equal(RouteMode.Guiding, result.Mode); TestAssert.False(result.IsAutomatic); TestAssert.True(result.ReleaseMovement);
        }
    }
}
