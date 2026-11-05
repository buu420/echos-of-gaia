using System;
using System.Linq;
using Memoria.Accessibility.Navigation;

internal static partial class Program
{
    private static void LaterChainCases()
    {
        var globals=FF9StateSystem.EventState.gEventGlobal;Array.Clear(globals,0,globals.Length);
        const string trunk="d2312646b5ba5a2370cbbc0b0b4e8a9b77851f2073537e5d527e0016bbdab1f8";
        SetCounter(4660);NativeStorySteps.RefreshTransitState();
        Check(Phase(4660,1002,trunk).ActionUids.SequenceEqual(new[]{4}),"Cleyra starts at the stair lever before the door can be crossed");
        SetBit(3862);NativeStorySteps.RefreshTransitState();
        Check(Phase(4660,1002,trunk).GoalFields.SequenceEqual(new[]{1007}),"the open door leads to the sandfall hole, without a counter change");
        SetBit(3863);NativeStorySteps.RefreshTransitState();
        Check(Phase(4660,1002,trunk).GoalFields.SequenceEqual(new[]{1013}),"the sandfall unlocks the sand fork lever");
        SetBit(3861);NativeStorySteps.RefreshTransitState();
        Check(Phase(4660,1002,trunk).GoalFields.SequenceEqual(new[]{1017}),"the sand fork unlocks the settlement ladder");

        const string hideout="1019d88ba25ec3530885559139bd1c94f4469af71c896bf4c85ce34e7127db56";
        SetCounter(9320);NativeStorySteps.RefreshTransitState();
        var first=NativeStorySteps.SelectPhase(9320,2114,s=>s==0?hideout:null,readItem:id=>0);
        Check(first!=null && first.GoalFields.SequenceEqual(new[]{2114}),"without potions Story leads to the hideout arrival scene");
        var second=NativeStorySteps.SelectPhase(9320,2114,s=>s==0?hideout:null,readItem:id=>id==274?1:0);
        Check(second!=null && second.GoalFields.SequenceEqual(new[]{2107}) && second.ReviewedLinks.Any(e=>e.From==2110 && e.To==2101 && e.ActionUid==8),"owning the hideout potion redirects the same checkpoint through the air cab toward Alice");
        var third=NativeStorySteps.SelectPhase(9320,2114,s=>s==0?hideout:null,readItem:id=>id==276?0:1);
        Check(third!=null && third.GoalFields.SequenceEqual(new[]{2111}) && third.ActionUids.SequenceEqual(new[]{4}),"when only the studio potion is missing, Story starts with the artist");
        SetBit(3546);NativeStorySteps.RefreshTransitState();
        third=NativeStorySteps.SelectPhase(9320,2114,s=>s==0?hideout:null,readItem:id=>id==276?0:1);
        Check(third!=null && third.ActionUids.SequenceEqual(new[]{7}),"the artist's permission selects the actual studio search spot");
        Check(NativeStorySteps.SelectPhase(9320,2114,s=>s==0?hideout:null,readItem:id=>null)?.InformationOnly==true,"unreadable potion inventory does not redirect to another step");

        StoryStage Phase(int progress,int field,string director)=>NativeStorySteps.SelectPhase(progress,field,s=>s==0?director:null);
        void SetBit(int index){globals[index>>3]|=(byte)(1<<(index&7));}
    }
}
