using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class TestFixtures
    {
        public static NavigationPoint P(float x, float z) { return new NavigationPoint(x, 0, z); }
        public static NavigationContext Context(string map = "field:100", long generation = 1)
        {
            return new NavigationContext { Generation = generation, LogicalMapKey = map, SaveIdentity = "save-a", PlayerIdentity = 7, Position = P(0, 0), Transport = new TransportIdentity(TransportKind.Foot, 7, 0), CanControl = true, Foreground = true };
        }
    }
}
