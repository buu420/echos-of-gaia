using System;
using System.Linq;
using Memoria.Accessibility.Navigation;

internal static class Program
{
    private static int Main()
    {
        try
        {
            // The two actual native map pins, with their discovery bits still unset.
            ff9.w_naviLocationPos[0, 3] = new ff9.navipos { vx = 1, vy = 1, tx = 306501, ty = -204700 };
            ff9.w_naviLocationPos[0, 11] = new ff9.navipos { vx = 1, vy = 1, tx = 297056, ty = -215824 };
            var context = new NavigationContext
            {
                LogicalMapKey = "world:0", Generation = 1, SaveIdentity = "unvisited-cavern",
                PlayerIdentity = 12, Transport = new TransportIdentity(TransportKind.Foot, 12, 0),
                Position = new NavigationPoint(311341f / 256, 3.377f, -191812f / 256)
            };
            NativeWorldEntrances.Open = true;
            WMUIData.Marked = false;
            NativeWorldLandmarks.InCamera = false;
            Require(NativeWorldTargets.Read(context, null).Count == 2,
                "An open Ice Cavern entrance was hidden until its mouth entered the camera.");
            var catalog = new WorldTargetCatalog().Build(context, NativeWorldTargets.Read(context, null), null);
            Require(catalog.Targets.Count == 2, "The entity catalog dropped the native entrances before reachability filtering.");
            Require(catalog.Targets.Select(target => target.Label).Distinct().Count() == 2,
                "The two Ice Cavern entrances lost their side labels.");
            Console.WriteLine("PASS unvisited Ice Cavern enters Towns before camera discovery");

            context.Generation++;
            Require(NativeWorldTargets.Read(context, null).Count == 2, "A battle return reset the unvisited entrance list.");
            Console.WriteLine("PASS open native entrances survive a world generation change");

            NativeWorldEntrances.Open = false;
            Require(NativeWorldTargets.Read(context, null).Count == 0, "A closed native branch offered an entrance.");
            Console.WriteLine("PASS closed native branches remain excluded");

            context.Transport = new TransportIdentity(TransportKind.HildaGarde, 12, 9);
            NativeWorldEntrances.Open = true;
            Require(NativeWorldTargets.Read(context, null).Count == 0, "Airship pins bypassed native map availability.");
            WMUIData.Marked = true;
            Require(NativeWorldTargets.Read(context, null).Count == 2, "Marked native airship pins disappeared.");
            Console.WriteLine("PASS airships continue to use native available map pins");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("FAIL world target presentation: " + error.Message);
            return 1;
        }
    }
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
