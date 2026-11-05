using System;
using System.Collections.Generic;
namespace UnityEngine
{
    public enum RuntimePlatform { WindowsPlayer }
    public static class Random { public static int Range(int minimum,int maximum){throw new NotSupportedException();} }
    public static class Application { public static string dataPath="",persistentDataPath="",temporaryCachePath="",streamingAssetsPath="";public static RuntimePlatform platform=RuntimePlatform.WindowsPlayer; }
    public struct Vector4 { public float x,y,z,w;public Vector4(float x,float y,float z,float w){this.x=x;this.y=y;this.z=z;this.w=w;}public static Vector4 zero{get{return new Vector4();}}public float this[int i]{get{return i==0?x:i==1?y:i==2?z:w;}set{if(i==0)x=value;else if(i==1)y=value;else if(i==2)z=value;else w=value;}} }
    public struct Vector2 { public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;} }
    public struct Vector3
    {
        public float x,y,z;
        public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
        public static Vector3 zero{get{return new Vector3();}}
    }
    public struct Matrix4x4 { public static Matrix4x4 identity{get{return new Matrix4x4();}} }
    public static class Time { public static int frameCount; }
    public static class Screen { public static int width=1920,height=1080; }
    public class Object { }
    public class Component:Object { public GameObject gameObject; }
    public class Renderer:Component { public bool enabled=true; }
    public sealed class Camera:Object { public Vector3 Projected=new Vector3(960,540,0);public Vector3 WorldToScreenPoint(Vector3 point){return Projected;} }
    public sealed class GameObject:Object
    {
        private static int _next=1000;private readonly int _id=_next++;
        public bool activeInHierarchy=true;public readonly List<Renderer> Renderers=new List<Renderer>{new Renderer()};
        public int GetInstanceID(){return _id;}
        public T[] GetComponentsInChildren<T>() where T:Component{var result=new List<T>();foreach(var renderer in Renderers)if(renderer is T)result.Add((T)(Component)renderer);return result.ToArray();}
    }
}
