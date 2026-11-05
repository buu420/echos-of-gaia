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
