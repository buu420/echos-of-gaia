using System.Collections.Generic;
using Memoria.Accessibility.Navigation;

namespace UnityEngine
{
    public static class Object
    {
        public static T FindObjectOfType<T>() where T : class => EventEngine.Instance as T;
    }
    public struct Vector3 { public float x, y, z; }
    public sealed class Transform { public Vector3 position; }
    public sealed class GameObject { public bool activeInHierarchy = true; }
}

public sealed class WorldActor
{
    public UnityEngine.Vector3 RealPosition;
    public UnityEngine.Transform transform = new UnityEngine.Transform();
}
public sealed class Actor
{
    public int index, uid, talkRad, speed;
    public bool isEnableRenderer;
    public UnityEngine.GameObject go;
    public WorldActor wmActor;
}
public sealed class ObjList { public Actor obj; public ObjList next; }
public sealed class EventEngine
{
    public static readonly EventEngine Instance = new EventEngine();
    public Actor GetControlChar() => null;
    public ObjList GetActiveObjList() => null;
    public bool objIsVisible(Actor actor) => true;
}
public static class ff9
{
    public struct navipos { public int vx, vy, tx, ty; }
    public static navipos[,] w_naviLocationPos = new navipos[1, 64];
    public static WorldActor w_movePlanePtr, w_moveChocoboPtr;
    public static float S(int value) => value / 256f;
}
public static class WMUIData
{
    public static int ActiveMapNo => 0;
    public static bool PlaneIsAvailable => false;
    public static bool ChocoboIsAvailable => false;
    public static bool Marked;
    public static bool LocationAvailable(int id) => Marked;
}
public static class FF9TextTool
{
    public static string[] GetTableText(uint table)
    {
        var text = new string[65];
        text[4] = text[12] = "Ice Cavern";
        return text;
    }
}
namespace Assets.Sources.Scripts.UI.Common { }
namespace Memoria.Accessibility.Navigation
{
    internal static class NativeWorldEntrances
    {
        internal static bool Open;
        internal static void Read(EventEngine engine, NavigationContext context) { }
        internal static int[] OpenTags(int place) =>
            place == 4 || place == 12 ? (Open ? new[] { place == 4 ? 0x9895 : 0x9a91 } : new int[0]) : null;
    }
    internal static class NativeWorldLandmarks
    {
        internal static bool InCamera;
        internal static bool VisibleEntrance(int[] tags, NavigationPoint marker, NavigationContext context, WorldGeometrySnapshot geometry) => InCamera;
        internal static bool OnScreen(UnityEngine.Vector3 point) => InCamera;
        internal static void Append(List<WorldTargetObservation> result, NavigationContext context, EventEngine engine, string[] text) { }
    }
    internal static class NativeWorldLandings
    {
        internal static void Append(List<WorldTargetObservation> result, NavigationContext context, EventEngine engine, WorldGeometrySnapshot geometry) { }
    }
}
