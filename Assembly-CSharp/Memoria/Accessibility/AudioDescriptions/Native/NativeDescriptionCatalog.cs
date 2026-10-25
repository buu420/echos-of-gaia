using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using SimpleJSON;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
namespace Memoria.Accessibility.AudioDescriptions
{
    internal sealed class NativeDescriptionFilm
    {
        internal string Key,SourceSHA256;
        internal long SourceBytes;
        internal DescriptionMovie Timeline;
        internal DescriptionAudioSpec[] Audio;
    }
    internal sealed class NativeDescriptionFieldBank
    {
        internal string Id;
        internal DescriptionAudioSpec[] Audio;
    }
    internal static class NativeDescriptionCatalog
    {
        internal static readonly Dictionary<string,NativeDescriptionFilm> Films=new Dictionary<string,NativeDescriptionFilm>(StringComparer.OrdinalIgnoreCase);
        internal static readonly Dictionary<string,DescriptionAudioSpec> Audio=new Dictionary<string,DescriptionAudioSpec>(StringComparer.Ordinal);
        internal static readonly Dictionary<string,string> Text=new Dictionary<string,string>(StringComparer.Ordinal);
        internal static readonly Dictionary<int,NativeDescriptionFieldBank> FieldBanks=new Dictionary<int,NativeDescriptionFieldBank>();
        internal static readonly Dictionary<string,NativeDescriptionFieldBank> FieldCueBanks=new Dictionary<string,NativeDescriptionFieldBank>(StringComparer.Ordinal);
        internal static DescriptionAudioSpec[] GenericFieldAudio=new DescriptionAudioSpec[0];
        internal static string Failure;
        private static bool _loaded;
        internal static void Initialize()
        {
            if(_loaded)return;_loaded=true;
            try {
                using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Memoria.Accessibility.AudioDescriptions.json")) {
                    if(stream==null)throw new InvalidOperationException("The audio-description catalog is missing");
                    using(var reader=new StreamReader(stream))Load(reader.ReadToEnd());
                }
            }catch(Exception exception){Clear();Failure=exception.Message;}
        }
        // Explicit invariant parsing: SimpleJSON.AsDouble uses the OS culture.
        private static double Number(JSONNode node)
        {
            double value;if(!Double.TryParse(node.Value,NumberStyles.Float,CultureInfo.InvariantCulture,out value) || !DescriptionCue.Finite(value))throw new InvalidOperationException("Invalid description timing");return value;
        }
        private static long Integer(JSONNode node){long value;if(!Int64.TryParse(node.Value,NumberStyles.Integer,CultureInfo.InvariantCulture,out value))throw new InvalidOperationException("Invalid description size");return value;}
        internal static void Load(string json)
        {
            Clear();Failure=null;
            var root=JSON.Parse(json);if(root["schema"].AsInt!=1)throw new InvalidOperationException("Unsupported audio-description catalog");
            var clips=root["clips"];if(!(clips is JSONArray) || clips.Count==0 || clips.Count>4096)throw new InvalidOperationException("Invalid or oversized description catalog");
            for(int i=0;i<clips.Count;i++) {
                var row=clips[i];string id=row["id"],text=row["text"];
                if(String.IsNullOrEmpty(text) || Audio.ContainsKey(id))throw new InvalidOperationException("Invalid or duplicate description text");
                var spec=new DescriptionAudioSpec(id,row["file"],row["sha256"],checked((int)Integer(row["bytes"])),Number(row["duration"]));
                Audio.Add(id,spec);Text.Add(id,text);
            }
            var films=root["films"];
            for(int i=0;i<films.Count;i++) {
                var row=films[i];string key=row["movie"];var cues=new List<DescriptionCue>();var specs=new List<DescriptionAudioSpec>();var seen=new HashSet<string>();
                for(int n=0;n<row["cues"].Count;n++) {
                    var item=row["cues"][n];string id=item["id"];DescriptionAudioSpec spec;
                    if(!Audio.TryGetValue(id,out spec))throw new InvalidOperationException("Description cue has no recording declaration: "+id);
                    cues.Add(new DescriptionCue(id,Number(item["start"]),Number(item["end"]),spec.Duration,Text[id]));if(seen.Add(id))specs.Add(spec);
                }
                if(cues.Count==0)continue;
                Films.Add(key,new NativeDescriptionFilm{Key=key,SourceSHA256=row["sourceSHA256"],SourceBytes=Integer(row["sourceBytes"]),Timeline=new DescriptionMovie(key,cues.ToArray()),Audio=specs.ToArray()});
            }
            var movieIds=new HashSet<string>(StringComparer.Ordinal);
            foreach(var film in Films.Values){ValidateBudget(film.Audio);foreach(var spec in film.Audio)movieIds.Add(spec.Id);}
            LoadFieldBanks(json,movieIds);
            var generic=new List<DescriptionAudioSpec>();
            foreach(var spec in Audio.Values)if(!movieIds.Contains(spec.Id) && !FieldCueBanks.ContainsKey(spec.Id))generic.Add(spec);
            GenericFieldAudio=generic.ToArray();ValidateBudget(GenericFieldAudio);
        }
        private static void LoadFieldBanks(string json,HashSet<string> movieIds)
        {
            // SimpleJSON erases primitive token types. Use the already shipped
            // streaming reader, without JObject/JToken: those LINQ tree types
            // require INotifyPropertyChanging, absent in the original Unity Mono.
            try {
                using(var reader=new JsonTextReader(new StringReader(json))) {
                    reader.DateParseHandling=DateParseHandling.None;
                    if(!ReadToken(reader) || reader.TokenType!=JsonToken.StartObject)throw new InvalidOperationException("Invalid description catalog object");
                    bool found=false;var ids=new HashSet<string>(StringComparer.Ordinal);
                    while(ReadToken(reader) && reader.TokenType!=JsonToken.EndObject) {
                        if(reader.TokenType!=JsonToken.PropertyName)throw new InvalidOperationException("Invalid description catalog property");
                        string property=(string)reader.Value;
                        if(!ReadToken(reader))throw new InvalidOperationException("Incomplete description catalog");
                        if(property!="fieldBanks"){reader.Skip();continue;}
                        if(found || reader.TokenType!=JsonToken.StartArray)throw new InvalidOperationException("fieldBanks must be one array");
                        found=true;
                        while(ReadToken(reader) && reader.TokenType!=JsonToken.EndArray)ReadFieldBank(reader,ids,movieIds);
                        if(reader.TokenType!=JsonToken.EndArray)throw new InvalidOperationException("Incomplete field bank array");
                    }
                    if(reader.TokenType!=JsonToken.EndObject || ReadToken(reader))throw new InvalidOperationException("Invalid description catalog ending");
                }
            }catch(JsonException exception){throw new InvalidOperationException("Invalid field bank JSON",exception);}
        }
        private static bool ReadToken(JsonReader reader)
        {
            while(reader.Read())if(reader.TokenType!=JsonToken.Comment)return true;
            return false;
        }
        private static void ReadFieldBank(JsonReader reader,HashSet<string> ids,HashSet<string> movieIds)
        {
            if(reader.TokenType!=JsonToken.StartObject)throw new InvalidOperationException("Invalid field bank");
            string id=null;List<int> fields=null;List<string> members=null;
            var properties=new HashSet<string>(StringComparer.Ordinal);
            while(ReadToken(reader) && reader.TokenType!=JsonToken.EndObject) {
                if(reader.TokenType!=JsonToken.PropertyName)throw new InvalidOperationException("Invalid field bank property");
                string property=(string)reader.Value;
                if(!properties.Add(property))throw new InvalidOperationException("Duplicate field bank property");
                if(!ReadToken(reader))throw new InvalidOperationException("Incomplete field bank");
                if(property=="id") {
                    if(reader.TokenType!=JsonToken.String)throw new InvalidOperationException("Invalid field bank identity");
                    id=(string)reader.Value;
                } else if(property=="fields") {
                    if(reader.TokenType!=JsonToken.StartArray)throw new InvalidOperationException("Invalid field bank maps");
                    fields=new List<int>();
                    while(ReadToken(reader) && reader.TokenType!=JsonToken.EndArray) {
                        long map;
                        if(reader.TokenType!=JsonToken.Integer || !Int64.TryParse(Convert.ToString(reader.Value,CultureInfo.InvariantCulture),NumberStyles.None,CultureInfo.InvariantCulture,out map) || map<0 || map>Int32.MaxValue)
                            throw new InvalidOperationException("Invalid field bank map identity");
                        fields.Add((int)map);
                    }
                    if(reader.TokenType!=JsonToken.EndArray)throw new InvalidOperationException("Incomplete field bank maps");
                } else if(property=="clips") {
                    if(reader.TokenType!=JsonToken.StartArray)throw new InvalidOperationException("Invalid field bank clips");
                    members=new List<string>();
                    while(ReadToken(reader) && reader.TokenType!=JsonToken.EndArray) {
                        if(reader.TokenType!=JsonToken.String)throw new InvalidOperationException("Invalid field bank clip identity");
                        members.Add((string)reader.Value);
                    }
                    if(reader.TokenType!=JsonToken.EndArray)throw new InvalidOperationException("Incomplete field bank clips");
                } else throw new InvalidOperationException("Unlisted/private field bank metadata");
            }
            if(reader.TokenType!=JsonToken.EndObject || String.IsNullOrEmpty(id) || !Regex.IsMatch(id,@"^[a-z0-9][a-z0-9-]*\z") || !ids.Add(id) || fields==null || fields.Count==0 || members==null || members.Count==0)
                throw new InvalidOperationException("Invalid or duplicate field bank identity/membership");
            var definition=new NativeDescriptionFieldBank{Id=id};var specs=new List<DescriptionAudioSpec>();
            foreach(string cue in members) {
                DescriptionAudioSpec spec;
                if(cue==null || !Audio.TryGetValue(cue,out spec) || movieIds.Contains(cue) || FieldCueBanks.ContainsKey(cue))
                    throw new InvalidOperationException("Invalid or duplicate field bank clip ownership");
                specs.Add(spec);FieldCueBanks.Add(cue,definition);
            }
            definition.Audio=specs.ToArray();ValidateBudget(definition.Audio);
            foreach(int map in fields) {
                if(FieldBanks.ContainsKey(map))throw new InvalidOperationException("Invalid or duplicate field bank map ownership");
                FieldBanks.Add(map,definition);
            }
        }
        private static void ValidateBudget(DescriptionAudioSpec[] specs)
        {
            long total=0;if(specs.Length>512)throw new InvalidOperationException("Description bank is too large");
            foreach(var spec in specs) {
                if(spec.Bytes<46 || spec.Bytes>2097152)throw new InvalidOperationException("Invalid description recording size");
                total+=spec.Bytes;if(total>33554432)throw new InvalidOperationException("Description bank exceeds its memory budget");
            }
        }
        private static void Clear(){Films.Clear();Audio.Clear();Text.Clear();FieldBanks.Clear();FieldCueBanks.Clear();GenericFieldAudio=new DescriptionAudioSpec[0];}
    }
    internal static class NativeDescriptionFiles
    {
        internal static bool VerifyFilm(string path,NativeDescriptionFilm film)
        {
            var before=new FileInfo(path);if(!before.Exists || before.Length!=film.SourceBytes)return false;long stamp=before.LastWriteTimeUtc.Ticks;
            string digest;using(var input=File.OpenRead(path))using(var hash=SHA256.Create())digest=BitConverter.ToString(hash.ComputeHash(input)).Replace("-","");
            var after=new FileInfo(path);return after.Length==film.SourceBytes && after.LastWriteTimeUtc.Ticks==stamp && String.Equals(digest,film.SourceSHA256,StringComparison.OrdinalIgnoreCase);
        }
        internal static byte[] ReadClip(string directory,string file,DescriptionAudioSpec[] specs)
        {
            DescriptionAudioSpec wanted=null;foreach(var spec in specs)if(spec.File==file){wanted=spec;break;}
            if(wanted==null)throw new InvalidOperationException("Unlisted description recording");
            string path=Path.Combine(directory,file);var info=new FileInfo(path);
            if(!info.Exists || info.Length!=wanted.Bytes || info.Length>2097152)throw new InvalidOperationException("Description recording is missing or has changed: "+wanted.Id);
            return File.ReadAllBytes(path);
        }
    }
}
