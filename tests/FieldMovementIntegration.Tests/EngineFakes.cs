using System;
using System.Collections.Generic;
using Memoria.Accessibility.Navigation;

namespace UnityEngine
{
    public class Object
    {
        public static EventEngine Engine;
        public static T FindObjectOfType<T>() where T:class { return Engine as T; }
    }
    public struct Vector3
    {
        public float x,y,z;
        public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
        public float sqrMagnitude {get{return x*x+y*y+z*z;}}
        public static Vector3 operator -(Vector3 a,Vector3 b){return new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);}
    }
}
public class EventEngine
{
    public const int stateRunning=1;
    public Actor Player;
    public object GetControlChar(){return Player;}
}
public class Actor
{
    public int uid=7,state=EventEngine.stateRunning,speed=30;
    public PosObj coll;
    public float colldist;
    public FieldMapActorController fieldMapActorController;
}
public class PosObj {public int uid,collRad;public float[] pos=new float[3];}
public class WalkMesh
{
    public PosObj Contact;
    public bool FailProbe;
    public PosObj Collision(FieldMapActorController controller,int mode,out float distance){if(FailProbe)throw new Exception("Injected probe failure");distance=-2;return Contact;}
}
public class FieldMapActorController
{
    public Actor originalActor;
    public bool isPlayer=true,hasTarget,isRunning;
    public int activeTri,lastTri;
    public float speed=30;
    public WalkMesh walkMesh;
    public long AccessibilityPathOwner;
    public UnityEngine.Vector3 moveTarget,curPos,stillPos,lastPos;
    public List<UnityEngine.Vector3> movePaths=new List<UnityEngine.Vector3>();
    public int totalPathCount,stillCount;
    public float totalPathLengthSq,radius=80;
    public void ClearMoveTarget(){hasTarget=false;moveTarget=new UnityEngine.Vector3();}
}
namespace Memoria.Accessibility.Navigation
{
    public static class NavigationLifecycleBridge
    {
        public static long Generation=1;
        public static event Action<StopReason> Releasing;
        public static void Release(StopReason reason){if(Releasing!=null)Releasing(reason);}
    }
    public static class NativeNavigationContext
    {
        public static NavigationContext Context;
        public static NavigationContext Read(){return Context;}
    }
    public static class NativeInteractionProbe
    {
        public static InteractionReadiness Read(NavigationContext context,NavigationTarget target){return null;}
    }
    public static class NativeFieldGeometry
    {
        public static int Interruptions;
        public static bool FailCapture,ReleasedWhenCaptured;
        public static object State;
        public static FieldGeometrySnapshot Read(NavigationContext context){return new FieldGeometrySnapshot{Fingerprint="mesh"};}
        public static void CaptureInterruption(NavigationContext context,NavigationTarget target,RoutePlan route,object state)
        {
            Interruptions++;State=state;
            ReleasedWhenCaptured=UnityEngine.Object.Engine.Player.fieldMapActorController.AccessibilityPathOwner==0;
            if(FailCapture)throw new Exception("Injected capture failure");
        }
    }
}
namespace Memoria.Prime {public static class Log {public static bool FailWrite;public static void Message(string format,params object[] values){if(FailWrite)throw new Exception("Injected log failure");}}}
