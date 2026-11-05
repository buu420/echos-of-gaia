using System.Collections.Generic;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class FieldAreaNameTests
    {
        private static FieldAreaName Entry(int field,string scene,string name){return new FieldAreaName{Field=field,Scene=scene,Name=name};}
        public static void SameNameRoomsGetStableNumbersInNativeFieldOrder()
        {
            var names=FieldAreaNames.Build(new[]{Entry(221,"path-b","Evil Forest/Trail"),Entry(220,"path-a","Evil Forest/Trail"),Entry(222,"spring","Evil Forest/Spring")});
            TestAssert.Equal("Evil Forest, Trail, Area 1",names[220]);
            TestAssert.Equal("Evil Forest, Trail, Area 2",names[221]);
            TestAssert.Equal("Evil Forest, Spring",names[222]);
        }
        public static void LaterStoryVariantsShareTheSameAreaNumber()
        {
            var names=FieldAreaNames.Build(new[]{Entry(1800,"alley","Alexandria/Alley"),Entry(106,"alley-b","Alexandria/Alley"),Entry(105,"alley","Alexandria/Alley")});
            TestAssert.Equal(names[105],names[1800]);
            TestAssert.Equal("Alexandria, Alley, Area 1",names[105]);
            TestAssert.Equal("Alexandria, Alley, Area 2",names[106]);
        }
        public static void UniqueNamesAndSameSceneAliasesHaveNoUnnecessarySuffix()
        {
            var names=FieldAreaNames.Build(new[]{Entry(1,"one","Steeple"),Entry(2,"one","Steeple"),Entry(3,"two","Square")});
            TestAssert.Equal("Steeple",names[1]);TestAssert.Equal("Steeple",names[2]);TestAssert.Equal("Square",names[3]);
        }
        public static void MissingNativeNamesAndUnknownScenesNeverBecomeNumericLabels()
        {
            var names=FieldAreaNames.Build(new[]{Entry(1,"one",null),Entry(2,null,"Forest"),Entry(3,"three","Forest")});
            TestAssert.False(names.ContainsKey(1));TestAssert.Equal("Forest",names[2]);TestAssert.Equal("Forest",names[3]);
        }
        public static void IifaSpiralNamesFollowThePhysicalScreenInBothDirectionsAndStoryVariants()
        {
            const string name="Iifa Tree/Roots";
            var second=FieldAreaNames.SpiralName(1651,6,name);
            TestAssert.Equal("Iifa Tree, Roots, Spiral area 2",second);
            TestAssert.Equal(second,FieldAreaNames.SpiralName(1651,9,name));
            TestAssert.Equal(second,FieldAreaNames.SpiralName(1758,19,name));
            TestAssert.Equal("Iifa Tree, Roots, Spiral area 4",FieldAreaNames.SpiralName(1758,23,name));
            TestAssert.Equal("Iifa Tree, Roots, Spiral area 5",FieldAreaNames.SpiralName(1758,22,name));
        }
        public static void WarpMazeAreaNamesFollowTheActualLandingPlatform()
        {
            const string mesh="13f9aed7dda2b59aadc528212f8428213c0b9f5e829dc84790d1abeaac827abd";
            TestAssert.Equal(3,FieldAreaNames.WarpMazeArea(2714,mesh,119));
            TestAssert.Equal(9,FieldAreaNames.WarpMazeArea(2714,mesh,137));
            TestAssert.Equal(7,FieldAreaNames.WarpMazeArea(2714,mesh,122));
            TestAssert.Equal(0,FieldAreaNames.WarpMazeArea(2714,"different walkmesh",122));
        }
    }
}
