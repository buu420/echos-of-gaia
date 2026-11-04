using System.Collections.Generic;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class FieldAreaNameTests
    {
        private static FieldAreaName Entry(int field,string scene,string name){return new FieldAreaName{Field=field,Scene=scene,Name=name};}
        public static void SameNameRoomsGetStableLettersInNativeFieldOrder()
        {
            var names=FieldAreaNames.Build(new[]{Entry(221,"path-b","Evil Forest/Trail"),Entry(220,"path-a","Evil Forest/Trail"),Entry(222,"spring","Evil Forest/Spring")});
            TestAssert.Equal("Evil Forest, Trail, Area A",names[220]);
            TestAssert.Equal("Evil Forest, Trail, Area B",names[221]);
            TestAssert.Equal("Evil Forest, Spring",names[222]);
        }
        public static void LaterStoryVariantsShareTheSameAreaLetter()
        {
            var names=FieldAreaNames.Build(new[]{Entry(1800,"alley","Alexandria/Alley"),Entry(106,"alley-b","Alexandria/Alley"),Entry(105,"alley","Alexandria/Alley")});
            TestAssert.Equal(names[105],names[1800]);
            TestAssert.Equal("Alexandria, Alley, Area A",names[105]);
            TestAssert.Equal("Alexandria, Alley, Area B",names[106]);
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
    }
}
