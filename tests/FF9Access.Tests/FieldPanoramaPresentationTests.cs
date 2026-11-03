using System.Linq;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class FieldPanoramaPresentationTests
    {
        private static FieldCameraPanorama Street()
        {return new FieldCameraPanorama{HalfWidth=160,HalfHeight=112,MinX=160,MaxX=160,MinY=112,MaxY=480};}
        public static void TheUpperMainStreetPassageRemainsPresentedBelowTheCameraPan()
        {
            // Original100 camera0:320x592, native panY112..480.
            var p=new NavigationPoint(-87.0763f,60.5191f,14313);
            TestAssert.True(FieldPanoramaPresentation.Contains(Street(),p));
        }
        public static void TheLeftStreetPassageRemainsPresentedAfterAHorizontalPan()
        {
            // Original101 camera0:640x224, native panX160..480.
            var c=new FieldCameraPanorama{HalfWidth=160,HalfHeight=112,MinX=160,MaxX=480,MinY=112,MaxY=112};
            TestAssert.True(FieldPanoramaPresentation.Contains(c,new NavigationPoint(-119.34f,-65.62f,2800)));
        }
        public static void NativeCameraClampsExcludeUnreachablePartsOfTheBackdrop()
        {
            var c=Street();c.MaxY=250;
            TestAssert.False(FieldPanoramaPresentation.Contains(c,new NavigationPoint(0,-300,1000)));
            TestAssert.True(FieldPanoramaPresentation.Contains(c,new NavigationPoint(0,-200,1000)));
            c.MinY=c.MaxY=112;TestAssert.False(FieldPanoramaPresentation.Contains(c,new NavigationPoint(0,0,1000)));
        }
        public static void InvalidDepthProjectionDimensionsAndBoundsNeverPresentAnExit()
        {
            foreach(var p in new[]{new NavigationPoint(float.NaN,0,100),new NavigationPoint(0,float.PositiveInfinity,100),new NavigationPoint(0,0,-1),new NavigationPoint(0,0,0),new NavigationPoint(0,113,100),new NavigationPoint(161,0,100)})
                TestAssert.False(FieldPanoramaPresentation.Contains(Street(),p));
            var c=Street();c.HalfWidth=0;TestAssert.False(FieldPanoramaPresentation.Contains(c,new NavigationPoint(0,0,100)));
            c=Street();c.MinY=c.MaxY+1;TestAssert.False(FieldPanoramaPresentation.Contains(c,new NavigationPoint(0,0,100)));
            TestAssert.False(FieldPanoramaPresentation.Contains(null,new NavigationPoint(0,0,100)));
        }
        public static void OnlyAProvenPlainPassageOnActiveBackdropCanUseThePanorama()
        {
            var p=new NavigationPoint(-87.0763f,60.5191f,14313);
            TestAssert.True(FieldPanoramaPresentation.PresentsPassage(true,false,true,Street(),p));
            TestAssert.False(FieldPanoramaPresentation.PresentsPassage(false,false,true,Street(),p));
            TestAssert.False(FieldPanoramaPresentation.PresentsPassage(true,false,false,Street(),p));
            TestAssert.False(FieldPanoramaPresentation.PresentsPassage(true,false,true,null,p));
        }
        public static void NativeStreetScriptsProvePlainWalkingPassages()
        {
            var ops=new FieldDoorOpcodes{MapJump=43,WorldJump=182,JumpPosition=164,MoveQuad=158,Move=35,Requests=new[]{16,18,20},Animations=new[]{95,96,97,99,100,101,202,231},InteractionBarriers=new[]{31,32,149,150,208,84,124}};
            using(var scripts=OpeningNativeFixture.Scripts()) {
                foreach(var leg in new[]{new[]{100,15,101},new[]{101,16,102},new[]{102,8,103}}) {
                    var row=scripts.RootElement.GetProperty("Functions").EnumerateArray().Single(r=>r.GetProperty("Field").GetInt32()==leg[0] && r.GetProperty("Sid").GetInt32()==leg[1]);
                    bool plain=FieldDoorEvidence.HasPlainWalkOut(OpeningNativeFixture.Code(row),null,ops);TestAssert.True(plain);
                    var evidence=OpeningNativeFixture.Read(scripts.RootElement,row,255);
                    TestAssert.True(evidence.Destinations.Contains(leg[2]));
                }
            }
        }
    }
}
