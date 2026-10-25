using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
namespace Memoria.Accessibility.AudioDescriptions
{
    // Explicit, visually reviewed story objects only. This is not a global idle
    // animation catalog: every row pins the installed actor script and field.
    public enum FieldPresentedObjectMode { Static, Animated, Moving }
    public sealed class FieldPresentedObjectDescription
    {
        public readonly int Map,Sid,Model;
        public readonly string EntrySha256,Clip,CueId,VoiceText;
        public readonly FieldPresentedObjectMode Mode;
        public FieldPresentedObjectDescription(int map,int sid,string entrySha256,int model,string clip,string cueId,string voiceText,FieldPresentedObjectMode mode)
        {
            if(map<0 || sid<0 || sid>Byte.MaxValue || model<0 || model>=UInt16.MaxValue || String.IsNullOrEmpty(entrySha256) || entrySha256.Length!=64 ||
                String.IsNullOrEmpty(clip) || String.IsNullOrEmpty(cueId) || String.IsNullOrEmpty(voiceText) || mode<FieldPresentedObjectMode.Static || mode>FieldPresentedObjectMode.Moving)
                throw new ArgumentException("Invalid reviewed object presentation");
            foreach(char c in entrySha256)if(!(c>='0' && c<='9') && !(c>='a' && c<='f'))throw new ArgumentException("Presented object entry hash must be lowercase SHA-256");
            Map=map;Sid=sid;EntrySha256=entrySha256;Model=model;Clip=clip;CueId=cueId;VoiceText=voiceText;Mode=mode;
        }
    }
    public sealed class FieldPresentedObjectCatalog
    {
        private readonly ReadOnlyCollection<FieldPresentedObjectDescription> _entries;
        private readonly HashSet<int> _maps=new HashSet<int>();
        private readonly HashSet<string> _candidates=new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string,FieldPresentedObjectDescription> _identities=new Dictionary<string,FieldPresentedObjectDescription>(StringComparer.Ordinal);
        public IList<FieldPresentedObjectDescription> Entries { get { return _entries; } }
        public FieldPresentedObjectCatalog(IEnumerable<FieldPresentedObjectDescription> entries)
        {
            if(entries==null)throw new ArgumentNullException("entries");
            var copy=new List<FieldPresentedObjectDescription>();var cues=new Dictionary<string,FieldPresentedObjectDescription>(StringComparer.Ordinal);
            foreach(var entry in entries) {
                if(entry==null)throw new ArgumentException("Null presented object declaration");
                string candidate=Candidate(entry.Map,entry.Sid,entry.Model,entry.Clip);
                _identities.Add(candidate+"|"+entry.EntrySha256,entry);_candidates.Add(candidate);_maps.Add(entry.Map);
                FieldPresentedObjectDescription shared;
                if(cues.TryGetValue(entry.CueId,out shared)) {
                    if(shared.VoiceText!=entry.VoiceText || shared.Mode!=entry.Mode)throw new ArgumentException("Ambiguous shared presented object cue");
                }else cues.Add(entry.CueId,entry);
                copy.Add(entry);
            }
            _entries=new ReadOnlyCollection<FieldPresentedObjectDescription>(copy);
        }
        private static string Candidate(int map,int sid,int model,string clip)
        {return map.ToString(CultureInfo.InvariantCulture)+":"+sid.ToString(CultureInfo.InvariantCulture)+":"+model.ToString(CultureInfo.InvariantCulture)+"|"+clip;}
        public bool HasMap(int map){return _maps.Contains(map);}
        public bool CanReview(int map,int sid,int model,string clip)
        {return _maps.Contains(map) && !String.IsNullOrEmpty(clip) && _candidates.Contains(Candidate(map,sid,model,clip));}
        public FieldPresentedObjectDescription Find(int map,int sid,string entrySha256,int model,string clip)
        {
            if(!CanReview(map,sid,model,clip) || String.IsNullOrEmpty(entrySha256))return null;
            FieldPresentedObjectDescription entry;return _identities.TryGetValue(Candidate(map,sid,model,clip)+"|"+entrySha256,out entry)?entry:null;
        }
    }
    public sealed class FieldPresentedObjectSample
    {
        public long Actor,PresentationFrame;
        public int Map,Sid,Model,AnimFrame,FrameCount,InFrame,OutFrame,Speed;
        public string EntrySha256,Clip;
        public bool ClipPresent,Visible,ScreenClear,FullRange,Forward,NonJump,PositionKnown;
        public float X,Y,Z;
    }
}
