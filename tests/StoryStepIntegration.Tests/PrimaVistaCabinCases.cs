using System;
using System.Linq;
using Memoria.Accessibility.Navigation;

internal static partial class Program
{
    private static void PrimaVistaCabinDescent()
    {
        const string director = "05c593f3dd98e836d2b47044a9ff9de478d93c45f50d7ded80dab0876d9ff5ba";
        const string player = "7e6244ab948bf283556772ad1e153a0504bb1a2c7fadd9cd177cade63a87c5ab";
        const string ladder = "b5c064a6fcdeadade170322523797e965f90e72ea0f8d7c017458a69cf6e7b11";
        const string mesh = "9a46fd8d36e31b7f7e1cb691c3df2141b62d9da06040f017587667dfaf005a6b";
        var engine = new EventEngine();
        engine.Objects[0] = new Obj { sid = 0, uid = 0 };
        engine.Objects[10] = new Obj { sid = 10, uid = 10 };
        engine.Objects[15] = new Obj { sid = 15, uid = 15 };
        engine.MapVars[32] = 2;
        SetCounter(2055);
        NativeStorySteps.RefreshTransitState();
        Func<int, string> hashes = sid => sid == 0 ? director : sid == 10 ? ladder : sid == 15 ? player : null;
        StoryStep Climb(NavigationPoint position) =>
            NativeStorySteps.Candidates(engine, 207, 2055, 10, ladder, hashes, 15, position, 10)
                .SingleOrDefault(step => step.Kind == StoryStepKind.RegionConfirm);
        StoryStage Stage(int triangle) =>
            NativeStorySteps.SelectPhase(2055, 207, hashes, local => local.Field == 207 && local.Walkmesh == mesh &&
                local.ActiveTriangles.Contains(triangle));

        var step = Climb(new NavigationPoint(541, 476, 234));
        Check(step != null && step.Action.Contains("hold Down") && step.Effects.Single().Offset == 95,
            "Vivi's Story identifies the native Confirm then Down interaction on the upper platform");
        Check(Climb(new NavigationPoint(96, 12, -55)) == null,
            "the climb-down action is unavailable on the crew cabin floor");
        Check(Stage(72)?.ActionUids.SequenceEqual(new[] { 10 }) == true && Stage(83)?.ActionUids.SequenceEqual(new[] { 16 }) == true,
            "the cabin Story changes from climb down to talk to Vivi on the lower floor");
        engine.MapVars[32] = 3;
        Check(Climb(new NavigationPoint(541, 476, 234)) == null, "the Vivi detour requires the native crew cabin phase");
    }
}
