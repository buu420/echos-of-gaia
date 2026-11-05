using System;
namespace UnityEngine
{
    public enum RuntimePlatform { WindowsPlayer }
    public static class Random { public static int Range(int minimum,int maximum){throw new NotSupportedException();} }
    public static class Application { public static string dataPath="",persistentDataPath="",temporaryCachePath="",streamingAssetsPath="";public static RuntimePlatform platform=RuntimePlatform.WindowsPlayer; }
    public struct Vector4 { public float x,y,z,w;public Vector4(float x,float y,float z,float w){this.x=x;this.y=y;this.z=z;this.w=w;}public static Vector4 zero{get{return new Vector4();}}public float this[int i]{get{return i==0?x:i==1?y:i==2?z:w;}set{if(i==0)x=value;else if(i==1)y=value;else if(i==2)z=value;else w=value;}} }
    // Unity's Vector3 arithmetic used by the real Math3D/QuadCircle/QuadPos helpers.
    public struct Vector3
    {
        public float x,y,z;
        public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
        public void Set(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
        public float sqrMagnitude{get{return x*x+y*y+z*z;}}
        public float magnitude{get{return (float)Math.Sqrt(sqrMagnitude);}}
        public Vector3 normalized{get{float m=magnitude;return m>1e-5f?this/m:new Vector3();}}
        public static Vector3 operator+(Vector3 a,Vector3 b){return new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);}
        public static Vector3 operator-(Vector3 a,Vector3 b){return new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);}
        public static Vector3 operator-(Vector3 a){return new Vector3(-a.x,-a.y,-a.z);}
        public static Vector3 operator*(Vector3 a,float d){return new Vector3(a.x*d,a.y*d,a.z*d);}
        public static Vector3 operator*(float d,Vector3 a){return a*d;}
        public static Vector3 operator/(Vector3 a,float d){return new Vector3(a.x/d,a.y/d,a.z/d);}
        public static float Dot(Vector3 a,Vector3 b){return a.x*b.x+a.y*b.y+a.z*b.z;}
        public static Vector3 Cross(Vector3 a,Vector3 b){return new Vector3(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);}
        public static Vector3 Scale(Vector3 a,Vector3 b){return new Vector3(a.x*b.x,a.y*b.y,a.z*b.z);}
        public static float Distance(Vector3 a,Vector3 b){return (a-b).magnitude;}
        public static Vector3 Project(Vector3 v,Vector3 n){float s=Dot(n,n);return s<1e-15f?new Vector3():n*(Dot(v,n)/s);}
    }
    public static class Mathf
    {
        public static float Abs(float v){return Math.Abs(v);}
        public static float Max(float a,float b){return Math.Max(a,b);}
        public static float Min(float a,float b){return Math.Min(a,b);}
        public static bool Approximately(float a,float b){return Math.Abs(b-a)<Math.Max(1e-6f*Math.Max(Math.Abs(a),Math.Abs(b)),float.Epsilon*8);}
    }
    public sealed class Transform{public Vector3 position;}
    public sealed class GameObject{public readonly Transform transform=new Transform();}
}
