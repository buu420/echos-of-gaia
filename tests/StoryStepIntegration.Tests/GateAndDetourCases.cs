using System;
using System.Linq;
using Memoria.Accessibility.Navigation;

internal static partial class Program
{
    private static void GateAndDetourCases()
    {
        var globals=FF9StateSystem.EventState.gEventGlobal;Array.Clear(globals,0,globals.Length);
        const string bridge="0898631036b9b67234be7ff23a977c7162bb85f1082680a5919a0bd616c5dbdf";
        SetCounter(4230);NativeStorySteps.RefreshTransitState();
        Func<int,string> bridgeScripts=sid=>sid==0?bridge:null;
        var approach=NativeStorySteps.SelectPhase(4230,805,bridgeScripts,local=>local.ActiveTriangles.Contains(68));
        Check(approach?.GoalFields.SequenceEqual(new[]{805})==true && approach.ActionUids.SequenceEqual(new[]{3}),
            "South Gate entry side selects the broken bridge Confirm jump");
        var landing=NativeStorySteps.SelectPhase(4230,805,bridgeScripts,local=>local.ActiveTriangles.Contains(115));
        Check(landing?.GoalFields.SequenceEqual(new[]{807})==true && Selects(landing,805,2,807),
            "South Gate landing side routes through the far exit to Treno Gate without a counter change");
        Check(NativeStorySteps.SelectPhase(4230,805,bridgeScripts,local=>null)?.InformationOnly==true,
            "unreadable bridge floor does not suggest walking through the gap");
        Check(!Selects(landing,805,1,807),"Story does not reuse the return exit as the forward bridge hop");
        var cable = NativeStorySteps.SelectPhase(4230, 815,
            sid => sid == 0 ? "9c3f9939204a0222380f01753c254c71d219e23f2ad168d18c2f5f67d4880eb1" : null);
        Check(cable?.InformationOnly == false && cable.ActionUids.SequenceEqual(new[] { 13 }),
            "the Berkmea cable car phase offers Marcus as its Story action");

        Check(Route(4430, 903, "8d7f894502665d00d4522ccd027c265d5d362361fba081881aeb4b61f9e8939f", 9, 904),
            "Story uses the Card Stadium exit toward the Auction House reunion");
        Check(Route(4430, 908, "564573a4b269c57ff93549167639e8cd98ff191789cb22edac251e6fde6b6a94", 13, 912),
            "Story uses the Gate exit to the Slums toward the Auction House");
        Check(Route(4430, 906, "275d4f8d854ac7a340b0ae970c14902df0690cfde878fbcae2626f6fc82f1a4a", 5, 909) &&
            FieldExitDestinations.RequiresInwardApproach(906, 5, 5, "8587120b87e0898c55de292d8e5e28ac88ad799d07672f18d78772e5c121c8f1"),
            "the Auction House front door requires a staged approach through its native facing guard");
        Check(Route(4430, 902, "2c900cc02a8fd9a9b2043c218059728d4a86a198387ff8cd0a06f07dc6a099fb", 5, 901),
            "the back room of the Synthesis Shop rejoins the Auction House route through its hall");
        Check(Route(4435, 910, "6e74188f252dac3d14e5f5723a544571cbe16868d4317944b0b3047f670a6026", 4, 904),
            "the Weapon Shop has a Story route back to Marcus in the Pub");
        Check(Route(4450, 911, "645afdca8728e5b2594ae83efc017a091f2075d04bfa408be45dacc76beed873", 5, 907),
            "the Queen's House rejoins the route to Doctor Tot's tower");

        Check(Route(4470,905,"fa518bb4a073db9fbbc521fd72403954c598b2aa1beab0a8346f0946752c5c45",10,913),
            "Story can return through Treno's tower entrance and stairwell to the hatch");
        Check(Route(3170,568,"36eba4e87dc6d2ef428d86908411b2a0b3eefd9f52532de49d47602307a66681",23,567),
            "the Tantalus detour keeps a way back to the Festival station");
        Check(Route(10920,2658,"03f5ac4a12b8410dec23eb33e8eafa62f4461cdaf71e144b8461d189a7894500",7,2656),
            "the Bran Bal lab has a Story route back to the gate");
        Check(Route(5100,1205,"e0280ce1c94aba31eb6d1676e1f730ebc2be6dd0595ed79d624f789354a19406",19,1204),
            "after the Alexandria rescue, the altar exit starts the route upstairs");
        Check(Route(5900,1422,"e8962e384e885f8d0bf1e25f2d874f1d3b89faf8b6b1f12710b199a820931bdc",14,1423),
            "Fossil Roo begins with the walkable cave exit before the Gargant system");
        var harbor=NativeStorySteps.SelectPhase(7030,1817,sid=>sid==0?"1de78285a18ab80653b38ba1b52dddd4ddf3919f2c429275a104be74b2e4c4df":null);
        Check(harbor?.ActionUids.SequenceEqual(new[]{5})==true && harbor.Instruction.Contains("choose Yes"),
            "the harbor detour points to the native lift prompt and its own choice");
        var boat=new FieldTargetObservation();
        NativeStorySteps.AnnotateLoop(1866,7200,17,17,"59fb653fe422f17266fc8d42346d297d7f6012f08a2a284df4693fc2e4624be8",15,41,107,boat);
        Check(boat.StoryStepMilestone==7300 && boat.StoryAction.Contains("gondola"),
            "the native gondola boarding prompt is a Story action before the castle visit");

        bool Route(int counter,int field,string director,int uid,int destination)
        {
            SetCounter(counter);NativeStorySteps.RefreshTransitState();
            var stage=NativeStorySteps.SelectPhase(counter,field,sid=>sid==0?director:null);
            return Selects(stage,field,uid,destination);
        }
        bool Selects(StoryStage stage,int field,int uid,int destination)
        {
            if(stage==null)return false;
            var map="field:"+field+":test";var point=new NavigationPoint(4,0,1);
            var result=new CatalogResult();var door=new NavigationTarget{Id=new TargetIdentity(TargetKind.FieldExit,map,uid),
                Category=NavigationCategory.Exits,Label="Exit",Position=point,ApproachCandidates=new[]{point},
                Generation=1,IsInteractive=true,NextFields=new[]{destination}};
            result.Targets.Add(door);
            var catalog=new StoryTargetCatalog(new[]{stage},NativeStorySteps.Transit);
            catalog.SetTransitEvaluator(NativeStorySteps.EvaluateTransit);catalog.SetPhaseSelector((p,f,s)=>stage);
            catalog.Append(new NavigationContext{LogicalMapKey=map,StoryProgress=globals[0]+(globals[1]<<8),Generation=1},result,field);
            return result.Targets.Any(t=>t.Category==NavigationCategory.Story && t.PhysicalIdentity.Equals(door.Id));
        }
    }
}
