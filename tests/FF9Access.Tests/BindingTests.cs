using System.Collections.Generic;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class BindingsTests
    {
        public static void ShiftedBottomRowProducesNavigationCommands()
        {
            var broker = new NavigationInputBroker(NavigationBindings.Parse(null).Bindings);
            foreach (var item in new[] {
                new {Key="K", Command=NavigationCommand.PreviousTarget},
                new {Key="L", Command=NavigationCommand.Status},
                new {Key="Semicolon", Command=NavigationCommand.NextTarget}
            }) {
                var raw = new PhysicalInputSnapshot(); raw.KeysHeld.Add(item.Key);
                var commands = broker.Update(TestFixtures.Context(), raw);
                TestAssert.Equal(1, commands.Length); TestAssert.Equal(item.Command, commands[0]);
                TestAssert.True(broker.IsKeyCaptured(item.Key));
                broker.Update(TestFixtures.Context(), new PhysicalInputSnapshot());
            }
            var camera = new PhysicalInputSnapshot(); camera.KeysHeld.Add("J");
            TestAssert.Equal(0, broker.Update(TestFixtures.Context(), camera).Length);
            TestAssert.False(broker.IsKeyCaptured("J"));
        }
        public static void PreviousDefaultProfileMigratesToShiftedKeys()
        {
            var parsed = NavigationBindings.Parse("PreviousCategory=U|NextCategory=O|PreviousTarget=LeftBracket|NextTarget=RightBracket|Status=K|ToggleGuidance=I|ToggleAutomatic=P|Repeat=R|DecreaseInterval=Shift+I|IncreaseInterval=Shift+P");
            var broker = new NavigationInputBroker(parsed.Bindings);
            var raw = new PhysicalInputSnapshot(); raw.KeysHeld.Add("K");
            var commands = broker.Update(TestFixtures.Context(), raw);
            TestAssert.True(parsed.Success); TestAssert.Equal(NavigationCommand.PreviousTarget, commands[0]);
        }
        public static void SemicolonAliasRoundTripsWithoutIniComment()
        {
            var parsed = NavigationBindings.Parse("NextTarget=;");
            TestAssert.True(parsed.Success);
            TestAssert.Equal("Semicolon", parsed.Bindings.Get(NavigationCommand.NextTarget).Key);
            var saved = parsed.Bindings.Serialize();
            TestAssert.False(saved.Contains(";"));
            var broker = new NavigationInputBroker(NavigationBindings.Parse(saved).Bindings);
            var raw = new PhysicalInputSnapshot(); raw.KeysHeld.Add("Semicolon");
            TestAssert.Equal(NavigationCommand.NextTarget, broker.Update(TestFixtures.Context(), raw)[0]);
        }
        public static void JConflictLeavesR2Available()
        {
            var parsed = NavigationBindings.Parse("PreviousTarget=J;NextTarget=L");
            var native = new List<GameBinding> { new GameBinding("R2", "J") };
            var conflicts = parsed.Bindings.FindConflicts(native);
            TestAssert.Equal(1, conflicts.Count);
            TestAssert.False(parsed.Bindings.Get(NavigationCommand.PreviousTarget).IsEnabled);
            var broker = new NavigationInputBroker(parsed.Bindings); var raw = new PhysicalInputSnapshot(); raw.KeysHeld.Add("J");
            TestAssert.Equal(0, broker.Update(TestFixtures.Context(), raw).Length);
            TestAssert.False(broker.IsKeyCaptured("J"));
        }
        public static void DuplicateBindingsAreRejected()
        {
            TestAssert.False(NavigationBindings.Parse("PreviousTarget=K;Status=K").Success);
            TestAssert.False(NavigationBindings.Parse("ToggleAutomatic=NotAKey").Success);
            TestAssert.False(NavigationBindings.Parse("-1=Q").Success);
        }
        public static void NativeBaseKeyConflictsWithModifiedNavigation()
        {
            var parsed = NavigationBindings.Parse("IncreaseInterval=Shift+P");
            var conflicts = parsed.Bindings.FindConflicts(new List<GameBinding> {new GameBinding("Confirm", "P")});
            TestAssert.True(conflicts.Count >= 1);
            TestAssert.False(parsed.Bindings.Get(NavigationCommand.IncreaseInterval).IsEnabled);
        }
        public static void IntervalMigrationAndClamping()
        {
            TestAssert.Equal(1000, NavigationControls.IntervalMilliseconds(0, 1));
            TestAssert.Equal(3000, NavigationControls.IntervalMilliseconds(0, 3));
            TestAssert.Equal(500, NavigationControls.IntervalMilliseconds(100, 1));
            TestAssert.Equal(10000, NavigationControls.IntervalMilliseconds(15000, 1));
            TestAssert.Equal(1500, NavigationControls.IntervalMilliseconds(1490, 1));
        }
        public static void InvalidImportKeepsPreviousValidBindings()
        {
            var previous = NavigationBindings.Parse("PreviousTarget=Q").Bindings;
            var imported = NavigationBindings.ParseOrKeep("PreviousTarget=UnknownKey", previous);
            TestAssert.False(imported.Success);
            TestAssert.Equal("Q", imported.Bindings.Get(NavigationCommand.PreviousTarget).Key);
        }
    }
}
