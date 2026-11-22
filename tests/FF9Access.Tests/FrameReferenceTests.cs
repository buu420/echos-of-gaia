using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class FrameReferenceTests
    {
        private sealed class SceneObject {public bool Alive=true;public int Control;}
        public static void PositiveLookupIsSharedWithinFrameButMutableStateStaysLive()
        {
            int calls=0;var current=new SceneObject();
            var cache=new FrameReference<SceneObject>(()=>{calls++;return current;},value=>value!=null && value.Alive);
            var first=cache.Read(5,1);first.Control=7;
            TestAssert.Equal(7,cache.Read(5,1).Control);TestAssert.Equal(1,calls);
            var next=new SceneObject();current=next;
            TestAssert.Equal(next,cache.Read(6,1));TestAssert.Equal(2,calls);
            current=new SceneObject();TestAssert.Equal(current,cache.Read(6,2));TestAssert.Equal(3,calls);
        }
        public static void DestroyedReferencesAndMissingObjectsAreNeverReused()
        {
            int calls=0;var current=new SceneObject();
            var cache=new FrameReference<SceneObject>(()=>{calls++;return current;},value=>value!=null && value.Alive);
            var first=cache.Read(5,1);first.Alive=false;current=null;
            TestAssert.Equal(null,cache.Read(5,1));
            current=new SceneObject();TestAssert.Equal(current,cache.Read(5,1));TestAssert.Equal(3,calls);
        }
    }
}
