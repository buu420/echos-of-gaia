using System;
using System.Linq;
using Memoria.Accessibility.Navigation;
internal static partial class Program
{
    private static int failures,passes;
    private static void Check(bool condition,string name){if(condition){passes++;Console.WriteLine("PASS "+name);}else{failures++;Console.WriteLine("FAIL "+name);}}
    private static int Main()
    {
        // The embedded generated catalog loads with typed transit.
        var transit=NativeStorySteps.Transit;
        Check(transit!=null && transit.Length>1000,"generated catalog loads typed transit");
        Check(transit.Any(t=>t.From==166 && t.To==55 && t.Kind==StoryTransitKind.Scripted),"scripted tower-top scene edge is typed");
        Check(transit.Any(t=>t.From==150 && t.To==153 && t.Kind==StoryTransitKind.Player),"guard-room door is a player edge");
        // Burmecia house (757): region 5 Confirm while 3840 <= counter < 3850.
        var engine=new EventEngine();
        const string region="ffa1cb1d93d58e1c9b5a121e0542b629fc8d2b4957134aa5ef11332f23906ae1";
        engine.Objects[5]=new Obj{uid=5,sid=5};
        Func<int,string> hashes=sid=>sid==5?region:null;
        SetCounter(3840);
        var current=NativeStorySteps.Current(engine,757,3840,5,region,hashes);
        Check(current.Any(step=>step.Kind==StoryStepKind.RegionConfirm && step.Milestone==3850 &&
            step.Source=="reviewed-prefix" && step.TargetLabel=="Search spot behind the bed"),"Burmecia Confirm step is current from live guards");
        SetCounter(3850);
        Check(NativeStorySteps.Current(engine,757,3850,5,region,hashes).Count==0,"step retires once the native guard fails");
        SetCounter(3840);engine.Objects.Remove(5);
        Check(NativeStorySteps.Current(engine,757,3840,5,region,hashes).Count==0,"absent guard owner fails closed");
        // Castle hall (153) stairs: Zidane's phase-6 wait (MAP5[24]==6).
        const string zidane="aa0452c6d45bab20aac744146d789380fe1cb31debe05a983645a584873d5057";
        engine.Objects[3]=new Obj{uid=3,sid=3,ip=3841};engine.MapVars[24]=6;SetCounter(1190);
        var stairs=NativeStorySteps.CurrentPositions(engine,153,1190,3,sid=>sid==3?zidane:null);
        Check(stairs.Count==1 && stairs[0].Axis==OpeningGoalAxis.Y && stairs[0].Minimum==450,"castle stairs height wait is current in phase 6");
        engine.MapVars[24]=7;
        Check(NativeStorySteps.CurrentPositions(engine,153,1190,3,sid=>sid==3?zidane:null).Count==0,"look-out detour phase has no stairs goal");
        Check(NativeStorySteps.CurrentPositions(engine,153,1190,32,sid=>sid==3?zidane:null).Count==0,"Steiner never inherits Zidane's wait");
        // Two consecutive native waits share the same phase, but not an IP.
        const string sequential="361d25a5e136bf04034598f2368660fc3ed77aad37604438646702eb2e2e4a0b";
        engine.Objects.Clear();engine.Objects[0]=new Obj{sid=0,uid=0,ip=1415};engine.Objects[12]=new Obj{sid=12,uid=12};engine.MapVars[35]=1;
        Func<int,string> sequentialHash=sid=>sid==0?sequential:null;
        var first=NativeStorySteps.CurrentPositions(engine,204,1800,12,sequentialHash);
        Check(first.Count==1 && first[0].Maximum==650,"first of consecutive waits is bound to the actual native instruction pointer");
        engine.Objects[0].ip=1444;
        var second=NativeStorySteps.CurrentPositions(engine,204,1800,12,sequentialHash);
        Check(second.Count==1 && second[0].Maximum==400,"the next wait becomes current only at its own instruction pointer");
        engine.Objects[0].ip=1443;
        Check(NativeStorySteps.CurrentPositions(engine,204,1800,12,sequentialHash).Count==0,"an unknown between-wait position supplies no later sibling");
        engine.Objects[0].ip=1415;engine.Objects[99]=new Obj{sid=12,uid=99};
        Check(NativeStorySteps.CurrentPositions(engine,204,1800,99,sequentialHash).Count==0,"literal watched UID cannot bind another instance of the same script");
        engine.Objects[128]=new Obj{sid=0,uid=128,ip=1415};
        Check(NativeStorySteps.CurrentPositions(engine,204,1800,12,sequentialHash).Count==1,"an explicitly bound wait owner stays distinct from another same-script instance");
        Check(NativeStorySteps.FindOwner(engine,0,-1)==null,"an unbound same-script wait owner remains ambiguous");
        // Native Pina creates script 2 twice, as objects 128 and 129. Local
        // variables belong to that instance, not to an arbitrary same-SID actor.
        engine.Objects.Clear();engine.Objects[128]=new Obj{sid=2,uid=128};engine.Objects[129]=new Obj{sid=2,uid=129};
        engine.Objects[128].buffer[0]=3;engine.Objects[129].buffer[0]=7;
        var localGuard=new StoryStepGuard{Sid=2,Uid=129,Expression=new byte[]{0xda,0,0x7d,7,0,32,0x7f}};
        Check(NativeStorySteps.Evaluate(engine,localGuard)==1,"guard reads the requested UID's instance even when its script is shared");
        localGuard.Uid=128;Check(NativeStorySteps.Evaluate(engine,localGuard)==0,"another same-script instance has independent local variables");
        localGuard.Uid=-1;Check(!NativeStorySteps.Evaluate(engine,localGuard).HasValue,"unguarded shared SID remains ambiguous");
        localGuard.Uid=130;engine.Objects[130]=new Obj{sid=8,uid=130};Check(!NativeStorySteps.Evaluate(engine,localGuard).HasValue,"a reused UID with a different SID cannot satisfy an old owner guard");
        foreach(int owner in new[]{128,129}) {
            var self=new ScriptStoryFunction{Identity=owner*256+10,ScriptId=2,Tag=10,Snapshot=new ScriptSnapshot(null,null,null),Code=new[]{new ScriptInstruction(0,1,5){Expression=new byte[]{0x78,255,1,0x7d,0,0,24,0x7f}},new ScriptInstruction(1,2,2){Argument=1},new ScriptInstruction(2,3,43){Argument=153},new ScriptInstruction(3,4,4)}};
            var proof=ScriptStoryAnalyzer.Read(self,null,43,182,new[]{16,18,20},(uid,property)=>FieldExitDestinations.PlayerProperty(129,new NavigationPoint(0,500,0),uid,property));
            Check(proof.Destinations.Count==(owner==129?1:0),"self position reads only the executing controlled instance "+owner);
        }
        engine.Objects.Clear();engine.Objects[0]=new Obj{uid=0,sid=0,ip=1519};engine.Objects[9]=new Obj{uid=9,sid=9};engine.Objects[13]=new Obj{uid=13,sid=13};engine.MapVars[30]=2;engine.MapVars[31]=0;SetCounter(2100);
        const string baku="daa0b2f579366fed0b25c97ba941a446cefafc623a015a4d56eb032f1cca62ca",director="2d79cbcb4f8cfc8d587419eab3e6ffcfa789d53a504f34ede3c0cd731c4b5103";
        Func<int,string> bakuHashes=sid=>sid==9?baku:sid==0?director:null;
        var challenge=NativeStorySteps.Candidates(engine,202,2100,9,baku,bakuHashes,13,null,9).SingleOrDefault(step=>step.Source=="reviewed-prefix");
        Check(challenge!=null && challenge.Action=="Talk to Baku and challenge him","Baku's offered challenge is a current action before the player answers its choice");
        var prefix=new ScriptStoryEvidence();prefix.Reached.Add(new StoryEffectSite(3,9,3,355,9));
        Check(challenge!=null && StoryStepCatalog.HasEffect(challenge,prefix,bakuHashes),"Baku's native Talk entry proves the reviewed opener without assuming a dialogue choice");
        engine.MapVars[31]=1;Check(!NativeStorySteps.Candidates(engine,202,2100,9,baku,bakuHashes,13,null,9).Any(step=>step.Source=="reviewed-prefix"),"accepting Baku's challenge retires its conversation step");engine.MapVars[31]=0;
        Check(!NativeStorySteps.Candidates(engine,202,2200,9,baku,bakuHashes,13,null,9).Any(step=>step.Source=="reviewed-prefix"),"Baku's challenge never returns after its battle");
        engine.Objects[0].ip=1518;Check(!NativeStorySteps.Candidates(engine,202,2100,9,baku,bakuHashes,13,null,9).Any(step=>step.Source=="reviewed-prefix"),"Baku's opener requires the actual waiting director instruction");
        GrottoCases();
        DaliCases();
        IifaCases();
        LaterChainCases();
        GateAndDetourCases();
        PalaceResourceCases();
        SpeakerCases();
        Console.WriteLine("StoryStepIntegration: "+passes+" passed, "+failures+" failed");
        return failures==0?0:1;
    }
    private static void SetCounter(int value){FF9StateSystem.EventState.gEventGlobal[0]=(byte)value;FF9StateSystem.EventState.gEventGlobal[1]=(byte)(value>>8);}
    private static void PalaceResourceCases()
    {
        var globals=FF9StateSystem.EventState.gEventGlobal;Array.Clear(globals,0,globals.Length);SetCounter(9825);globals[2]=62;
        NativeStorySteps.RefreshTransitState();
        var exit=NativeStorySteps.SelectPhase(9825,2213,s=>s==0?"ac64a4a2d86b1e9ad39cb197f2435b8d410702f5e96ae132cefa4ee9424f5d8f":null);
        Check(exit!=null && exit.GoalFields.SequenceEqual(new[]{2214}) && exit.ReviewedLinks.Single().ActionUid==2,
            "Palace pack loads the exact Lobby door into the real Story loader");
        var loop=new FieldTargetObservation();
        const string hash="6f301cbf36b39fcd321c6c87cc9b63be731d5de162aaa9231073e2dc0b707e42";
        NativeStorySteps.AnnotateLoop(2215,9825,1,1,hash,11,111,220,loop);
        Check(loop.StoryStepMilestone==9830,"a verified required Palace loop can be wrapped as the current Story action");
        loop=new FieldTargetObservation();NativeStorySteps.AnnotateLoop(2215,9830,1,1,hash,11,111,220,loop);
        Check(loop.StoryStepMilestone==Int32.MinValue,"an old Palace loop remains an object after the boss trigger");

        // Arrival 94 is the staircase. The player can walk onto the lower floor
        // and toggle a lamp, but the engine keeps the original arrival code.
        globals[2]=94;
        foreach(int bit in new[]{3560,3562,3563,3564,3565,3567})
            globals[bit>>3]|=(byte)(1<<(bit&7));
        NativeStorySteps.RefreshTransitState();
        const string shadow="fe542ff51e6d94b68ab42c2714a11c9d70e2eb5d581e342a01ff83aed52f8e78";
        const string mesh="be8e79099954eec5659f4577745665e2da8d15661647dd0fd261746b412a76af";
        Func<int,string> shadowScripts=s=>s==0?shadow:null;
        Func<StoryPhaseLocal,bool?> safeGround=r=>r.Field==2221 && r.Walkmesh==mesh && r.ActiveTriangles.Contains(111);
        var wrongLamp=NativeStorySteps.SelectPhase(9825,2221,shadowScripts,safeGround);
        Check(wrongLamp!=null && wrongLamp.ActionUids.SequenceEqual(new[]{3}) && wrongLamp.Instruction=="Put out the candelabrum 2",
            "from the lower floor after returning, the wrong lamp takes priority over the disabled stairs");
        var disabledLanding=NativeStorySteps.SelectPhase(9825,2221,shadowScripts,r=>r.Walkmesh==mesh && r.ActiveTriangles.Contains(164));
        Check(disabledLanding!=null && disabledLanding.InformationOnly && disabledLanding.ActionUids==null,
            "a hypothetical arrival on the disabled staircase cannot target the lamp across disconnected geometry");
        var unknownFloor=NativeStorySteps.SelectPhase(9825,2221,shadowScripts);
        Check(unknownFloor!=null && unknownFloor.InformationOnly,"recovery requires a verified current walkmesh position");
        globals[3563>>3]&=(byte)(255^(1<<(3563&7)));
        globals[3564>>3]&=(byte)(255^(1<<(3564&7)));
        NativeStorySteps.RefreshTransitState();
        var relight=NativeStorySteps.SelectPhase(9825,2221,shadowScripts,safeGround);
        Check(relight!=null && relight.ActionUids.SequenceEqual(new[]{2}) && relight.Instruction=="Light the candelabrum 1",
            "after the wrong lamp is off, a missing route lamp can be relit from the safe floor");
        globals[3564>>3]|=(byte)(1<<(3564&7));
        NativeStorySteps.RefreshTransitState();
        var repaired=NativeStorySteps.SelectPhase(9825,2221,shadowScripts,safeGround);
        Check(repaired!=null && repaired.GoalFields.SequenceEqual(new[]{2222}) && repaired.ReviewedLinks.Single().ActionUid==13,
            "the top level exit becomes the next action after the native lamps reopen the stairs");
    }
}
