using System;
using System.Linq;
using Memoria.Accessibility.Navigation;

internal static partial class Program
{
    private static void DaliCases()
    {
        const string town="30b14b9539658278f3068e2ac9992e66304949e76e85060024385ad666bacfc2";
        const string inn="29ecb80a474fe80f68080ed19ae5d3aa5c159628cce20fe5690d8e566e2af57d";
        const string lift="9b21370a239c088c1b714b9529cdcd7ca3ba5174c10f54a6af432d1dbc982bc3";
        const string field="572a92530aeb80cf2e35568ec6b32bace909ba163039f1279062bbbcd8675560";
        const string vivi="52f84a822b4e0dd83bab5f4e3898c8265f38c6ad5d155b102e5df6a7cc4678aa";
        var engine=new EventEngine();engine.Objects[0]=new Obj{sid=0,uid=0};engine.Objects[34]=new Obj{sid=34,uid=34};
        Func<int,string> townHashes=s=>s==0?town:s==34?vivi:null;
        var globals=FF9StateSystem.EventState.gEventGlobal;Array.Clear(globals,0,globals.Length);
        Action<int> setBit=bit=>globals[bit>>3]|=(byte)(1<<(bit&7));
        SetCounter(2600);globals[297]=1;NativeStorySteps.RefreshTransitState();
        var stage=NativeStorySteps.SelectPhase(2600,350,townHashes);
        Check(stage!=null && stage.ActionUids.SequenceEqual(new[]{34}),"Dali starts with Vivi near the windmill, not an empty weapon shop");
        Check(NativeStorySteps.Candidates(engine,350,2600,34,vivi,townHashes,uid:34).Any(s=>s.Source=="reviewed-prefix"),"the actual Vivi conversation entry binds before the later shop scene");
        setBit(2055);NativeStorySteps.RefreshTransitState();
        stage=NativeStorySteps.SelectPhase(2600,350,townHashes);
        Check(stage!=null && stage.GoalFields.SequenceEqual(new[]{450}) && stage.ReviewedLinks.Any(e=>e.From==350 && e.To==450 && e.ActionUid==32),"after Vivi, leave to the north field along its real exit");
        stage=NativeStorySteps.SelectPhase(2600,450,s=>s==0?field:null);
        Check(stage!=null && stage.GoalFields.SequenceEqual(new[]{350}) && stage.ReviewedLinks.Any(e=>e.From==450 && e.To==350 && e.ActionUid==19),"the north-field return trigger starts the game's own timing gate");
        setBit(2079);NativeStorySteps.RefreshTransitState();
        stage=NativeStorySteps.SelectPhase(2600,350,townHashes);
        Check(stage!=null && stage.GoalFields.SequenceEqual(new[]{354}),"the weapon shop becomes current only when the native gate is ready");
        SetCounter(2610);NativeStorySteps.RefreshTransitState();
        stage=NativeStorySteps.SelectPhase(2610,352,s=>s==0?inn:null);
        Check(stage!=null && stage.GoalFields.SequenceEqual(new[]{352}) && stage.ActionUids.SequenceEqual(new[]{19}),"the bedroom conversation precedes the later scene that writes 2640");
        SetCounter(2700);NativeStorySteps.RefreshTransitState();
        string expectedWalkmesh="292a9ac9d0b88d6829582209553ebd6f328bf508534a56205c717814a3ef1414";
        stage=NativeStorySteps.SelectPhase(2700,404,s=>s==0?lift:null,l=>l.Walkmesh==expectedWalkmesh && l.ActiveTriangles.Contains(31));
        Check(stage!=null && stage.ActionUids.SequenceEqual(new[]{4}),"actual installed upper lift component selects the down lever");
        stage=NativeStorySteps.SelectPhase(2700,404,s=>s==0?lift:null,l=>l.Walkmesh==expectedWalkmesh && l.ActiveTriangles.Contains(59));
        Check(stage!=null && stage.GoalFields.SequenceEqual(new[]{406}) && stage.ReviewedLinks.Single().ActionUid==6,"actual lower lift component selects the tunnel exit at the same counter");
        stage=NativeStorySteps.SelectPhase(2700,404,s=>s==0?lift:null,l=>null);
        Check(stage!=null && stage.InformationOnly,"changed lift geometry cannot substitute the lower route for an unknown floor");
    }
}
