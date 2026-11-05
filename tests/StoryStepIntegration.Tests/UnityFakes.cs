using System;
namespace UnityEngine
{
    public enum RuntimePlatform { WindowsPlayer }
    public static class Random { public static int Range(int minimum,int maximum){throw new NotSupportedException();} }
    public static class Application { public static string dataPath="",persistentDataPath="",temporaryCachePath="",streamingAssetsPath="";public static RuntimePlatform platform=RuntimePlatform.WindowsPlayer; }
    public struct Vector4 { public float x,y,z,w;public Vector4(float x,float y,float z,float w){this.x=x;this.y=y;this.z=z;this.w=w;}public static Vector4 zero{get{return new Vector4();}}public float this[int i]{get{return i==0?x:i==1?y:i==2?z:w;}set{if(i==0)x=value;else if(i==1)y=value;else if(i==2)z=value;else w=value;}} }
}
