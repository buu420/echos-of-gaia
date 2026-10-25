using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
namespace Memoria.Accessibility.AudioDescriptions
{
    public sealed class DescriptionAudioSpec
    {
        public readonly string Id,File,SHA256;
        public readonly int Bytes;
        public readonly double Duration;
        public DescriptionAudioSpec(string id,string file,string sha256,int bytes,double duration){Id=id;File=file;SHA256=sha256;Bytes=bytes;Duration=duration;}
    }
    // Detached file/hash/WAV work. A replaced or reset source cannot publish
    // into the current scene, even if an old OS read finishes much later.
    public sealed class DescriptionAudioBank
    {
        private sealed class State {internal long Owner;internal bool Verified;internal string Error;internal Dictionary<string,byte[]> Clips;}
        private readonly object _sync=new object();
        private readonly Action<Action> _queue;
        private State _current;
        public DescriptionAudioBank():this(run=>{if(!ThreadPool.QueueUserWorkItem(_=>run()))throw new InvalidOperationException("Description worker unavailable");}){}
        public DescriptionAudioBank(Action<Action> queue){if(queue==null)throw new ArgumentNullException("queue");_queue=queue;}
        public void Prepare(long owner,DescriptionAudioSpec[] specifications,Func<bool> verifySource,Func<string,byte[]> read)
        {
            if(specifications==null || verifySource==null || read==null)throw new ArgumentNullException("Description bank input");
            var specs=(DescriptionAudioSpec[])specifications.Clone();var identities=new HashSet<string>(StringComparer.Ordinal);int total=0;
            if(specs.Length>512)throw new ArgumentException("Description bank is too large");
            foreach(var spec in specs) {
                if(spec==null || String.IsNullOrEmpty(spec.Id) || !Regex.IsMatch(spec.Id,"^[a-z0-9][a-z0-9-]*$") || spec.File!=spec.Id+".wav" ||
                    spec.SHA256==null || !Regex.IsMatch(spec.SHA256,"^[a-fA-F0-9]{64}$") || spec.Bytes<46 || spec.Bytes>2097152 ||
                    !DescriptionCue.Finite(spec.Duration) || spec.Duration<=0 || !identities.Add(spec.Id))throw new ArgumentException("Invalid description audio manifest");
                total+=spec.Bytes;if(total>33554432)throw new ArgumentException("Description bank exceeds its memory budget");
            }
            var state=new State{Owner=owner};lock(_sync)_current=state;
            try {_queue(()=>{
                try {
                    if(!Current(state))return;
                    if(!verifySource())throw new InvalidOperationException("The described visual source does not match the reviewed source");
                    lock(_sync){if(_current!=state)return;state.Verified=true;}
                    var clips=new Dictionary<string,byte[]>(StringComparer.Ordinal);
                    foreach(var spec in specs) {
                        if(!Current(state))return;byte[] bytes=read(spec.File);
                        if(bytes==null || bytes.Length!=spec.Bytes)throw new InvalidOperationException("Description recording size mismatch: "+spec.Id);
                        using(var hash=SHA256.Create())if(!String.Equals(BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-",""),spec.SHA256,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Description recording hash mismatch: "+spec.Id);
                        if(Math.Abs(DescriptionWaveOutput.Duration(bytes)-spec.Duration)>1d/24000+.000001)throw new InvalidOperationException("Description recording duration mismatch: "+spec.Id);
                        clips.Add(spec.Id,bytes);
                    }
                    lock(_sync){if(_current==state)state.Clips=clips;}
                } catch(Exception exception){lock(_sync){if(_current==state)state.Error=exception.Message;}}
            });}catch(Exception exception){lock(_sync){if(_current==state)state.Error=exception.Message;}}
        }
        private bool Current(State state){lock(_sync)return _current==state;}
        public bool TryGet(long owner,string id,out byte[] wave)
        {
            lock(_sync){wave=null;return _current!=null && _current.Owner==owner && _current.Clips!=null && _current.Clips.TryGetValue(id,out wave);}
        }
        public bool SourceVerified(long owner){lock(_sync)return _current!=null && _current.Owner==owner && _current.Verified;}
        public string Failure(long owner){lock(_sync)return _current!=null && _current.Owner==owner?_current.Error:null;}
        public void Reset(){lock(_sync)_current=null;}
    }
}
