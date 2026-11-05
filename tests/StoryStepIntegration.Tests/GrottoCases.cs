using System;
using System.Linq;
using Memoria.Accessibility.Navigation;

internal static partial class Program
{
    private static void GrottoCases()
    {
        const string director="a5e8d0f27639b82cb10f0cb4230f5542937c33008c6ffc05e2d93a6eadbcaf50";
        const string soldier="34df8a4e325abc3926d00da197b95c8dd84c4ed3e13ddc981445d7665599386c";
        const string door="0ef54fa334f21f1fd790a57909b0b887824761cfe9f3d2f39e8a6dd6a7f53ec2";
        const string entrance="f1263b8dcae693f8e19179b771d6c10dfa30e458a4bf804603ce8187a6cb69e0";
        var engine=new EventEngine();foreach(byte uid in new byte[]{0,5,8})engine.Objects[uid]=new Obj{sid=uid,uid=uid};
        Func<int,string> hashes=sid=>sid==0?director:sid==5?soldier:sid==8?door:null;
        var memory=FF9StateSystem.EventState.gEventGlobal;Array.Clear(memory,0,memory.Length);SetCounter(3710);
        NativeStorySteps.RefreshTransitState();
        var stage=NativeStorySteps.SelectPhase(3710,701,sid=>sid==0?entrance:null);
        Check(stage!=null && stage.GoalFields.Single()==702 && stage.ReviewedLinks.Single().ActionUid==7,"Grotto route takes the actual entrance to the soldier's room before the closed door");
        Check(NativeStorySteps.Candidates(engine,702,3710,5,soldier,hashes,uid:5).Any(s=>s.Source=="reviewed-prefix"),"wounded soldier is current while no bell has been given");
        Check(!NativeStorySteps.Candidates(engine,702,3710,8,door,hashes,uid:8).Any(s=>s.Source=="reviewed-prefix"),"ringing is unavailable without a bell");
        memory[479]=1;memory[3823>>3]|=(byte)(1<<(3823&7));NativeStorySteps.RefreshTransitState();
        stage=NativeStorySteps.SelectPhase(3710,702,hashes);
        Check(stage!=null && stage.ActionUids.SequenceEqual(new[]{8}),"bell acquisition switches Story to the sealed door at the same counter");
        Check(!NativeStorySteps.Candidates(engine,702,3710,5,soldier,hashes,uid:5).Any(s=>s.Source=="reviewed-prefix"),"the wounded soldier's gift retires the conversation objective");
        Check(NativeStorySteps.Candidates(engine,702,3710,8,door,hashes,uid:8).Any(s=>s.Kind==StoryStepKind.RegionConfirm && s.TargetLabel=="Sealed stone door"),"the closed door is an actual Confirm interaction before its walking branch");
        memory[479]=0;memory[3831>>3]|=(byte)(1<<(3831&7));NativeStorySteps.RefreshTransitState();
        stage=NativeStorySteps.SelectPhase(3710,702,hashes);
        Check(stage!=null && stage.GoalFields.Single()==703 && stage.ReviewedLinks.Any(e=>e.From==702 && e.To==703 && e.ActionUid==8),"opening the sealed door makes that same region the next walking exit");
        Check(!NativeStorySteps.Candidates(engine,702,3710,8,door,hashes,uid:8).Any(s=>s.Source=="reviewed-prefix"),"an open door does not ask the player to ring it again");
        stage=NativeStorySteps.SelectPhase(3720,702,hashes);
        Check(stage!=null && stage.Milestone==3730 && stage.GoalFields.Single()==703,"entering the next room retires the first door phase and finds the next bell");

        const string hall="4ff0bedc8ddb8c6e8ad05485ecb08c6a7a03d954fabf9ef238c7b26cfcc212bb";
        const string room="457dcde48f78301b220a341f8ce3f618f6642230e4eedecdc1dbfaf5b315009d";
        const string player="c6b8b6ae8fa6822e6a58b5b9c1c2ad7559f6b45e72ac774df264fab9080131c3";
        Func<int,string> hallHashes=sid=>sid==0?hall:null;
        Func<int,string> roomHashes=sid=>sid==0?room:sid==14?player:null;
        Action<int> setBit=bit=>memory[bit>>3]|=(byte)(1<<(bit&7));
        SetCounter(3730);setBit(3830);NativeStorySteps.RefreshTransitState();
        stage=NativeStorySteps.SelectPhase(3730,704,hallHashes);
        Check(stage!=null && stage.ActionUids.SequenceEqual(new[]{13}),"the bell hall chest is the next available bell source");
        setBit(3819);memory[479]=1;NativeStorySteps.RefreshTransitState();
        stage=NativeStorySteps.SelectPhase(3730,704,hallHashes);
        Check(stage!=null && stage.ActionUids.SequenceEqual(new[]{7}),"the acquired bell leads to the moogles' door before the sacred door");
        setBit(3827);memory[479]=0;NativeStorySteps.RefreshTransitState();
        stage=NativeStorySteps.SelectPhase(3730,704,hallHashes);
        Check(stage!=null && stage.GoalFields.SequenceEqual(new[]{706}) && stage.ActionUids.SequenceEqual(new[]{8}),"the opened moogle room leads to Moguta's conversation");
        engine.Objects[14]=new Obj{sid=14,uid=14,ip=2294};engine.MapVars[24]=0;
        Check(NativeStorySteps.CurrentPositions(engine,706,3730,14,roomHashes).Count==0,"walking toward the exit is not the Holy Bell step before the conversation");
        setBit(3824);NativeStorySteps.RefreshTransitState();
        var walk=NativeStorySteps.CurrentPositions(engine,706,3730,14,roomHashes).SingleOrDefault(s=>s.Source=="reviewed-prefix");
        Check(walk!=null && walk.Axis==OpeningGoalAxis.Z && walk.Maximum==-1201,"answering Moguta starts the actual exitward native position condition");
        engine.Objects[14].ip=2293;
        Check(NativeStorySteps.CurrentPositions(engine,706,3730,14,roomHashes).Count==0,"the Holy Bell position uses the controlled player's real wait IP");
        engine.Objects[14].ip=2294;setBit(3818);NativeStorySteps.RefreshTransitState();
        Check(NativeStorySteps.CurrentPositions(engine,706,3730,14,roomHashes).Count==0,"receiving the Holy Bell retires the position step");
        stage=NativeStorySteps.SelectPhase(3730,706,roomHashes);
        Check(stage!=null && stage.GoalFields.SequenceEqual(new[]{704}) && stage.ActionUids.SequenceEqual(new[]{8}),"after receiving the Holy Bell, go back to the sacred door");
        setBit(3826);NativeStorySteps.RefreshTransitState();
        stage=NativeStorySteps.SelectPhase(3730,704,hallHashes);
        Check(stage!=null && stage.GoalFields.SequenceEqual(new[]{707}),"the sacred door opens the final room, leaving the battle player controlled");
    }
}
