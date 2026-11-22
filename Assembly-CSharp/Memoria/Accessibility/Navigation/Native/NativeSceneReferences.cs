using UnityEngine;
namespace Memoria.Accessibility.Navigation
{
    // Scene lookups are shared only within this render frame and lifecycle
    // generation. Unity's destroyed-object check prevents reuse after teardown.
    // No singleton accessor is used: missing native objects are never created.
    internal static class NativeSceneReferences
    {
        private static class Cache<T> where T:Object
        {
            // This intentionally uses Unity's destroyed-object null comparison.
            internal static readonly FrameReference<T> Reference=new FrameReference<T>(Find<T>,value=>value!=null);
        }
        private static T Find<T>() where T:Object
        {return Object.FindObjectOfType<T>();}
        internal static T Read<T>() where T:Object
        {return Cache<T>.Reference.Read(Time.frameCount,NavigationLifecycleBridge.Generation);}
    }
}
