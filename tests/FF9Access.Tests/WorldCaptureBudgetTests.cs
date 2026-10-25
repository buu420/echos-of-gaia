using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class WorldCaptureBudgetTests
    {
        public static void RepeatedReadersAndIdentityReplacementShareOneFrameBudget()
        {
            var budget=new WorldCaptureBudget();int copied=0;
            copied+=budget.Take(40,24,480);copied+=budget.Take(40,24,456);copied+=budget.Take(40,24,480);
            TestAssert.Equal(24,copied);TestAssert.Equal(24,budget.Take(41,24,456));TestAssert.Equal(0,budget.Take(41,24,432));
            TestAssert.Equal(7,budget.Take(42,24,7));TestAssert.Equal(17,budget.Take(42,24,480));TestAssert.Equal(0,budget.Take(42,24,480));
        }
    }
}
