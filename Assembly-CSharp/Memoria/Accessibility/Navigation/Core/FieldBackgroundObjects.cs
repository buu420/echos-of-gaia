using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
namespace Memoria.Accessibility.Navigation
{
    // A Confirm region whose object is drawn in the field's own background art. The label describes the
    // drawn form only; it was reviewed on an installed scene render of the listed cameras
    // (docs/accessibility/reviewed-background-objects.json, rows in FieldBackgroundObjectRows.cs).
    public sealed class FieldBackgroundObject
    {
        public readonly int Field,RegionSid;public readonly int[] Cameras;
        public readonly string Scene,RegionHash,Background,Walkmesh,Label;
        // For a marked standing area the supporting point, not the unprojected
        // native polygon center, proves that the current ground is in view.
        public readonly bool GroundCue,TouchCue;
        public readonly int LandmarkSid,LandmarkModel;
        public readonly string LandmarkHash;
        public FieldBackgroundObject(int field,string scene,int regionSid,string regionHash,string background,string walkmesh,int[] cameras,string label,
            bool groundCue=false,int landmarkSid=-1,int landmarkModel=-1,string landmarkHash=null,bool touchCue=false)
        {Field=field;Scene=scene;RegionSid=regionSid;RegionHash=regionHash;Background=background;Walkmesh=walkmesh;Cameras=cameras??new int[0];Label=label;
            GroundCue=groundCue;LandmarkSid=landmarkSid;LandmarkModel=landmarkModel;LandmarkHash=landmarkHash;TouchCue=touchCue;}
    }
    // Detached live facts for one reviewed region.
    public sealed class FieldBackgroundPresence
    {
        public bool CanControl,SceneMatches,CameraMatches,ArtOnScreen,RegionTalkable,MapTalkPermitted;
        public bool CurrentGroundCue,LandmarkRendered;
        public int LiveRegions,RegionLevel,Approaches;
        public FieldInteractionState TalkGuard=FieldInteractionState.Unknown;
    }
    public static partial class FieldBackgroundObjects
    {
        // A presented bubble whose object is not (or not yet) reviewed: the game offers something here,
        // and nothing about it is guessed. Hidden pickups always use this.
        public const string PresentedPrompt="Interaction point";
        public static IList<FieldBackgroundObject> All{get{return new ReadOnlyCollection<FieldBackgroundObject>(_objects);}}
        public static bool HasField(int field){foreach(var row in _objects)if(row.Field==field)return true;return false;}
        public static FieldBackgroundObject Find(int field,string scene,int regionSid,string regionHash)
        {
            foreach(var row in _objects)if(row.Field==field && row.Scene==scene && row.RegionSid==regionSid && row.RegionHash==regionHash)return row;
            return null;
        }
        // The reviewed art is on screen in a reviewed camera of the same scene; the single live region may
        // be requested at Confirm (level, talk bytes, CheckQuadTalk, guard not proven unavailable); and at
        // least one walkable approach has this region as the first Talk region.
        public static bool Present(FieldBackgroundObject row,FieldBackgroundPresence live)
        {
            return row!=null && live!=null && live.CanControl && live.SceneMatches && live.CameraMatches && live.ArtOnScreen && live.LiveRegions==1 &&
                live.RegionLevel>1 && live.RegionTalkable && live.MapTalkPermitted && live.TalkGuard!=FieldInteractionState.Unavailable && live.Approaches>0 &&
                (!row.GroundCue || live.CurrentGroundCue) && (row.LandmarkSid<0 || live.LandmarkRendered);
        }
        public static string PromptLabel(string current){return String.IsNullOrEmpty(current)?PresentedPrompt:current;}
    }
}
