using System;
using System.Collections.Generic;
using Memoria.Accessibility.Navigation;
using UnityEngine;
namespace RegionSelectionIntegration.Tests
{
    // Finding7: a prospective approach must be selected exactly as the native TreadQuad would select
    // the region at that point, per tag: Talk (mode 4, tag 3) and Push (mode 2, tag 2) differ.
    internal static class Program
    {
        private const int Push=2,Talk=3;
        private static int Main()
        {
            var tests=new List<KeyValuePair<string,Action>>{
                Case("First Talk and first Push differ when overlapping regions own different tags",OverlappingTagsSelectPerTag),
                Case("Activation order decides the native first region for each tag",ActivationOrderDecides),
                Case("Containment is not native Confirm selection",ContainmentIsNotSelection),
                Case("Hotfix circles replace or extend the region exactly as TreadQuad",HotfixMatchesTreadQuad),
                Case("Non-region objects never select",OnlyRegionsSelect),
                Case("Read-only selectors equal TreadQuad on a sweep of points and orders",SweepEqualsTreadQuad),
                Case("Failed Confirm gate with Push gate selects only the Push walk-in",ConfirmGateFalsePushGateTrue),
                Case("Confirm path approaches are filtered by the native first Talk region",ConfirmApproachesUseFirstTalk)
            };
            int failed=0;
            foreach(var test in tests) {
                try{test.Value();Console.WriteLine("PASS "+test.Key);}
                catch(Exception exception){failed++;Console.WriteLine("FAIL "+test.Key+": "+exception.Message);}
            }
            Console.WriteLine((tests.Count-failed)+" passed, "+failed+" failed");
            return failed==0?0:1;
        }
        private static KeyValuePair<string,Action> Case(string name,Action action){return new KeyValuePair<string,Action>(name,action);}
        private static void Equal(object expected,object actual,string what){if(!Equals(expected,actual))throw new Exception(what+": expected "+(expected??"null")+", got "+(actual??"null"));}
        private static byte[] Script(params int[] tags)
        {
            var data=new byte[2+4*tags.Length+4];data[1]=(byte)tags.Length;
            for(int i=0;i<tags.Length;i++){data[2+4*i]=(byte)tags[i];data[4+4*i]=(byte)(4*tags.Length+i);}
            return data;
        }
        private static Quad Region(int uid,float minX,float minZ,float maxX,float maxZ,params int[] tags)
        {
            var corners=new[]{new[]{minX,minZ},new[]{maxX,minZ},new[]{maxX,maxZ},new[]{minX,maxZ}};
            var q=new QuadPos[4];for(int i=0;i<4;i++)q[i]=new QuadPos{X=(short)corners[i][0],Z=(short)corners[i][1]};
            return new Quad{cid=3,sid=(byte)uid,uid=(byte)uid,n=4,q=q,ebData=Script(tags)};
        }
        private static PosObj At(float x,float z){var po=new PosObj();po.go.transform.position=new Vector3(x,0,z);return po;}
        private static EventEngine Engine(params Obj[] active){var engine=new EventEngine{gMode=1};engine.SetActive(active);EventEngineUtils.QuadCircleData.Clear();FF9StateSystem.Common.FF9.fldMapNo=757;return engine;}
        private static void OverlappingTagsSelectPerTag()
        {
            var push=Region(5,0,0,100,100,Push);var talk=Region(6,0,0,100,100,Talk);
            var engine=Engine(push,talk);
            Equal(push,engine.NavigationTouchQuadAt(50,50),"first Push");
            Equal(talk,engine.NavigationTalkQuadAt(50,50),"first Talk");
            Equal(engine.TreadQuad(At(50,50),2),engine.NavigationTouchQuadAt(50,50),"Push equals TreadQuad mode 2");
            Equal(engine.TreadQuad(At(50,50),4),engine.NavigationTalkQuadAt(50,50),"Talk equals TreadQuad mode 4");
        }
        private static void ActivationOrderDecides()
        {
            var both=Region(5,0,0,100,100,Push,Talk);var talk=Region(6,0,0,100,100,Talk);var push=Region(7,0,0,100,100,Push);
            var engine=Engine(talk,both,push);
            Equal(talk,engine.NavigationTalkQuadAt(50,50),"Talk first in order");
            Equal(both,engine.NavigationTouchQuadAt(50,50),"Push first in order");
            engine=Engine(both,talk,push);
            Equal(both,engine.NavigationTalkQuadAt(50,50),"Talk after reorder");
            Equal(both,engine.NavigationTouchQuadAt(50,50),"Push after reorder");
        }
        private static void ContainmentIsNotSelection()
        {
            var earlier=Region(4,0,0,100,100,Talk);var story=Region(5,50,0,150,100,Push,Talk);
            var engine=Engine(earlier,story);
            Equal(true,engine.NavigationContainsQuad(story,75,50),"contained");
            Equal(earlier,engine.NavigationTalkQuadAt(75,50),"an earlier Talk region owns Confirm here");
            Equal(story,engine.NavigationTalkQuadAt(125,50),"only the story region contains this point");
        }
        private static void HotfixMatchesTreadQuad()
        {
            var region=Region(5,0,0,100,100,Talk);var engine=Engine(region);
            EventEngineUtils.QuadCircleData[EMinigame.CreateNPCID(757,5)]=new QuadCircle(new Vector3(300,0,300),20);
            Equal(null,engine.NavigationTalkQuadAt(50,50),"replacement circle excludes polygon");
            Equal(region,engine.NavigationTalkQuadAt(305,300),"replacement circle selects");
            Equal(engine.TreadQuad(At(305,300),4),engine.NavigationTalkQuadAt(305,300),"TreadQuad hotfix");
            EventEngineUtils.QuadCircleData[EMinigame.CreateNPCID(757,5)]=new QuadCircle(new Vector3(300,0,300),20,true);
            Equal(region,engine.NavigationTalkQuadAt(50,50),"original area kept");
            Equal(region,engine.NavigationTalkQuadAt(305,300),"circle extends");
            engine.gMode=2;
            Equal(null,engine.NavigationTalkQuadAt(305,300),"hotfix applies only on fields");
            Equal(engine.TreadQuad(At(305,300),4),engine.NavigationTalkQuadAt(305,300),"TreadQuad outside fields");
        }
        private static void OnlyRegionsSelect()
        {
            var actor=Region(4,0,0,100,100,Push,Talk);actor.cid=4;var region=Region(5,0,0,100,100,Talk);
            var engine=Engine(actor,region);
            Equal(region,engine.NavigationTalkQuadAt(50,50),"actor skipped");
            Equal(null,engine.NavigationTouchQuadAt(50,50),"no Push region");
        }
        private static void SweepEqualsTreadQuad()
        {
            var regions=new[]{Region(1,0,0,100,100,Push),Region(2,40,40,160,160,Talk),Region(3,80,0,200,90,Push,Talk),Region(4,-50,60,60,220,Talk,Push)};
            var orders=new[]{new[]{0,1,2,3},new[]{3,2,1,0},new[]{1,3,0,2},new[]{2,0,3,1}};
            int checks=0;
            foreach(var order in orders) {
                var engine=Engine(regions[order[0]],regions[order[1]],regions[order[2]],regions[order[3]]);
                for(int x=-60;x<=210;x+=15)for(int z=-10;z<=230;z+=15) {
                    Equal(engine.TreadQuad(At(x,z),2),engine.NavigationTouchQuadAt(x,z),"Push at "+x+","+z);
                    Equal(engine.TreadQuad(At(x,z),4),engine.NavigationTalkQuadAt(x,z),"Talk at "+x+","+z);
                    checks++;
                }
            }
            if(checks<1000)throw new Exception("sweep too small: "+checks);
        }
        // Field 757 / SID 5: the region's Push handler shows the icon while its Talk handler advances
        // the story. When native Talk is not permitted now, only the walk-in path may be offered, and its
        // approaches use the Push selector; the Confirm step itself is not authorized by Push.
        private static void ConfirmGateFalsePushGateTrue()
        {
            var region=Region(5,0,0,100,100,Push,Talk);var engine=Engine(region);
            var paths=FieldStoryRegionApproaches.Triggers(true,false,true);
            Equal(1,paths.Length,"paths");Equal(StoryRegionTrigger.Walk,paths[0],"walk only");
            Equal(Push,FieldStoryRegionApproaches.Tag(paths[0]),"walk-in reads the Push handler");
            Equal(0,FieldStoryRegionApproaches.Triggers(true,false,false).Length,"no Push gate either");
        }
        private static void ConfirmApproachesUseFirstTalk()
        {
            var earlier=Region(4,0,0,100,100,Talk);var story=Region(5,50,0,150,100,Push,Talk);
            var engine=Engine(earlier,story);
            var points=new[]{new NavigationPoint(75,0,50),new NavigationPoint(125,0,50),new NavigationPoint(140,0,90)};
            var paths=FieldStoryRegionApproaches.Triggers(true,true,true);
            Equal(StoryRegionTrigger.Confirm,paths[0],"Confirm first");
            var confirm=FieldStoryRegionApproaches.Read(true,true,points,p=>FieldStoryRegionApproaches.Selects(paths[0],engine.NavigationTalkQuadAt(p.X,p.Z),engine.NavigationTouchQuadAt(p.X,p.Z),story),p=>true,new NavigationPoint(0,0,0));
            Equal(2,confirm.Length,"Confirm approaches");Equal(125f,confirm[0].X,"nearest selected");
            var walk=FieldStoryRegionApproaches.Read(true,true,points,p=>FieldStoryRegionApproaches.Selects(paths[1],engine.NavigationTalkQuadAt(p.X,p.Z),engine.NavigationTouchQuadAt(p.X,p.Z),story),p=>true,new NavigationPoint(0,0,0));
            Equal(3,walk.Length,"walk-in approaches use first Push");
        }
    }
}
