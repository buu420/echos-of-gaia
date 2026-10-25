using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using SimpleJSON;
using Memoria.Prime;
using Assets.Sources.Scripts.UI.Common;
namespace Memoria.Accessibility.Navigation
{
    public static class NativeStoryTargets
    {
        private sealed class Binding {internal string Hash;internal int[] Milestones,Destinations;internal bool Pickup;}
        private sealed class Listener {internal int PlayerSid,Minimum,Maximum;internal int[] Milestones;internal string Hash,PlayerHash,Action;}
        private static readonly Dictionary<int,Dictionary<int,Binding>> _fields=new Dictionary<int,Dictionary<int,Binding>>();
        private static readonly Dictionary<int,Dictionary<int,Listener>> _listeners=new Dictionary<int,Dictionary<int,Listener>>();
        private static StoryTargetCatalog _catalog;private static bool _loaded;
        private static long _generation;private static string _save;private static int _player=-1;
        private static readonly Dictionary<TargetIdentity,int> _observedItems=new Dictionary<TargetIdentity,int>();
        public static void Initialize()
        {
            if(_loaded)return;_loaded=true;
            try {
                using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Memoria.Accessibility.StoryNavigation.json")) {
                    if(stream==null)throw new InvalidOperationException("Missing Story navigation catalog");
                    using(var reader=new StreamReader(stream)) {
                        var root=JSON.Parse(reader.ReadToEnd());if(root["schema"].AsInt!=1)throw new InvalidOperationException("Unsupported Story navigation catalog");
                        var stages=new List<StoryStage>();var rows=root["stages"];
                        for(int i=0;i<rows.Count;i++)stages.Add(new StoryStage{Milestone=rows[i]["milestone"].AsInt,GoalFields=Integers(rows[i]["fields"]),DepartureFields=Integers(rows[i]["departureFields"]),WorldLocations=Integers(rows[i]["worldLocations"]),Goal=rows[i]["goal"],Instruction=rows[i]["instruction"],WorldPlaces=Strings(rows[i]["worldPlaces"]),RequiredVehicle=rows[i]["vehicle"]});
                        var transit=new List<StoryTransit>();rows=root["transit"];
                        for(int i=0;i<rows.Count;i++)transit.Add(new StoryTransit{From=rows[i]["source"].AsInt,To=rows[i]["destination"].AsInt});
                        rows=root["fields"];for(int i=0;i<rows.Count;i++) {
                            var entries=new Dictionary<int,Binding>();var data=rows[i]["entries"];
                            for(int j=0;j<data.Count;j++)entries[data[j]["sid"].AsInt]=new Binding{Hash=data[j]["hash"],Milestones=Integers(data[j]["milestones"]),Destinations=Integers(data[j]["destinations"]),Pickup=data[j]["pickup"].AsBool};
                            _fields[rows[i]["field"].AsInt]=entries;
                        }
                        rows=root["listeners"];for(int i=0;i<rows.Count;i++) {
                            int field=rows[i]["field"].AsInt;Dictionary<int,Listener> listeners;
                            if(!_listeners.TryGetValue(field,out listeners)){listeners=new Dictionary<int,Listener>();_listeners[field]=listeners;}
                            listeners[rows[i]["sid"].AsInt]=new Listener{PlayerSid=rows[i]["playerSid"].AsInt,Minimum=rows[i]["minimum"].AsInt,Maximum=rows[i]["maximum"].AsInt,Milestones=Integers(rows[i]["milestones"]),Hash=rows[i]["hash"],PlayerHash=rows[i]["playerHash"],Action=rows[i]["action"]};
                        }
                        _catalog=new StoryTargetCatalog(stages.ToArray(),transit.ToArray(),OpeningStoryCheckpoints.All);
                    }
                }
            }catch(Exception exception){_fields.Clear();_listeners.Clear();Log.Message("[FF9Access] Story catalog unavailable: {0}",exception.Message);}
        }
        private static int[] Integers(JSONNode node){var result=new int[node.Count];for(int i=0;i<result.Length;i++)result[i]=node[i].AsInt;return result;}
        private static string[] Strings(JSONNode node){var result=new string[node.Count];for(int i=0;i<result.Length;i++)result[i]=node[i];return result;}
        public static bool Matches(NavigationContext context,int scriptId,string scriptHash)
        {
            Initialize();Dictionary<int,Binding> entries;Binding binding;
            return _fields.TryGetValue(FieldNumber(context),out entries) && entries.TryGetValue(scriptId,out binding) && binding.Hash==scriptHash;
        }
        public static bool ReviewedListener(NavigationContext context,int scriptId,string scriptHash,Func<int,string,bool> playerMatches)
        {
            Initialize();Dictionary<int,Listener> listeners;Listener listener;
            return _listeners.TryGetValue(FieldNumber(context),out listeners) && listeners.TryGetValue(scriptId,out listener) && listener.Hash==scriptHash && context.StoryProgress>=listener.Minimum && context.StoryProgress<listener.Maximum && playerMatches!=null && playerMatches(listener.PlayerSid,listener.PlayerHash);
        }
        public static void Annotate(NavigationContext context,int scriptId,string scriptHash,FieldTargetObservation observation,Func<ScriptStoryEvidence> read,Func<int,string,bool> playerMatches)
        {
            observation.StoryMilestones=new int[0];observation.NextFields=new int[0];observation.StoryAction=null;
            Initialize();int field=FieldNumber(context);Dictionary<int,Binding> entries;Binding binding;
            if(!_fields.TryGetValue(field,out entries) || !entries.TryGetValue(scriptId,out binding) || binding.Hash!=scriptHash)return;
            if(_generation!=context.Generation || _save!=context.SaveIdentity || _player!=context.PlayerIdentity){_observedItems.Clear();_generation=context.Generation;_save=context.SaveIdentity;_player=context.PlayerIdentity;}
            if((binding.Milestones.Length!=0 || binding.Destinations.Length!=0) && (observation.Visible || observation.ShowsIcon || observation.OutOfView || observation.Exit!=null && observation.Exit.Visible)) {
                var evidence=read();observation.StoryMilestones=new int[evidence.Milestones.Count];evidence.Milestones.CopyTo(observation.StoryMilestones);
                observation.NextFields=new int[evidence.Destinations.Count];evidence.Destinations.CopyTo(observation.NextFields);
            }
            Dictionary<int,Listener> listeners;Listener listener;
            if(ReviewedListener(context,scriptId,scriptHash,playerMatches) && _listeners.TryGetValue(field,out listeners) && listeners.TryGetValue(scriptId,out listener)) {
                observation.HasInteraction=true;observation.StoryMilestones=listener.Milestones;observation.StoryAction=listener.Action;
            }
            // A hidden script reward is not a presented pickup. Learn pickup
            // classification from the ordinary interaction cue, never its value.
            // Field 50 candle regions (MAP5[38] setters) during the native candle phase only.
            if(field==OpeningStoryCheckpoints.CargoField && scriptId>=4 && scriptId<=7 && context.StoryProgress>=OpeningStoryCheckpoints.Floor && context.StoryProgress<OpeningStoryCheckpoints.Ceiling &&
               OpeningStoryCheckpoints.IsCandleRegion(field,scriptId,scriptHash,context.StoryProgress,MapByte(OpeningStoryCheckpoints.PhaseByte))) {
                observation.StoryMilestones=new[]{OpeningStoryCheckpoints.CandleMilestone};observation.StoryAction=OpeningStoryCheckpoints.CandleAction;
            }
            int identity=observation.NativePresentationIdentity^scriptHash.GetHashCode(),observed;
            if(binding.Pickup && !observation.IsPerson && observation.ShowsIcon)_observedItems[observation.Id]=identity;
            observation.IsItem=!observation.IsPerson && _observedItems.TryGetValue(observation.Id,out observed) && observed==identity;
        }
        // Read-only copies of the current field's native state for reviewed checkpoints.
        private static int? MapByte(int index)
        {
            try {
                var engine=UnityEngine.Object.FindObjectOfType<EventEngine>();if(engine==null || engine.gMode!=1)return null;
                var map=engine.GetMapVar();return map!=null && index>=0 && index<map.Length?(int?)map[index]:null;
            }catch(Exception){return null;}
        }
        private static readonly Dictionary<byte[],string> _entryHashes=new Dictionary<byte[],string>();
        private static string EntryHash(int sid)
        {
            try {
                var engine=UnityEngine.Object.FindObjectOfType<EventEngine>();if(engine==null || engine.gMode!=1)return null;
                var obj=engine.GetObjUID(sid);if(obj==null || obj.sid!=sid || obj.ebData==null)return null;
                string hash;if(_entryHashes.TryGetValue(obj.ebData,out hash))return hash;
                if(_entryHashes.Count>256)_entryHashes.Clear();
                using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(obj.ebData)).Replace("-","").ToLowerInvariant();
                _entryHashes[obj.ebData]=hash;return hash;
            }catch(Exception){return null;}
        }
        private static int FieldNumber(NavigationContext context)
        {
            int field=-1;if(context==null || context.LogicalMapKey==null || !context.LogicalMapKey.StartsWith("field:"))return field;
            var parts=context.LogicalMapKey.Split(':');if(parts.Length<=1 || !Int32.TryParse(parts[1],out field))return -1;return field;
        }
        public static void Append(NavigationContext context,CatalogResult catalog)
        {
            Initialize();if(_catalog!=null)_catalog.Append(context,catalog,FieldNumber(context),id=>FF9TextTool.RemoveOpCode(FF9TextTool.LocationName(id)),MapByte,EntryHash);
            else if(context!=null && context.StoryProgress>=0)catalog.Targets.Add(new NavigationTarget{Id=new TargetIdentity(TargetKind.Story,context.LogicalMapKey,-1),Category=NavigationCategory.Story,Generation=context.Generation,Position=context.Position,Label="Story guidance unavailable.",Availability="The Story catalog could not be read.",ApproachCandidates=new NavigationPoint[0]});
        }
    }
}
