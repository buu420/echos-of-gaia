using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.Navigation
{
    public enum HotColdCue { Unknown, NothingNearby, Far, Close, Located }
    public enum HotColdDialogRole { Other, FoundCount, DepthLabel, DepthNumber, Cry, Result }
    // Values here are snapshots of shown UI, never event-script variables or treasure state.
    public sealed class HotColdFrame
    {
        public long Generation;
        public string MapKey;
        public bool Active, RoundActive, CanSpeak, TimerVisible, TimerRunning;
        public double NowMilliseconds, TimerSeconds;
        public int? Depth, Found;
    }
    public sealed class ChocoboHotColdReader
    {
        private HotColdFrame _frame;
        private HotColdCue _cue,_announcedCue;
        private int? _depth, _found, _previousSeconds;
        private bool _pendingCue, _pendingDepth, _pendingFound, _pendingWarning;
        private double _cueAt, _depthAt, _foundAt, _warningAt, _lastSpeech=-10000;
        private string _lastOutput;private int _lastPriority;
        private NavigationAnnouncement _proposal;
        private double _proposalAt,_nextAttempt;
        private HotColdCue _proposalCue;
        private int? _proposalDepth,_proposalFound;
        private int _proposalResultCount;
        private readonly List<string> _results=new List<string>();
        public bool HasPendingResults {get{return _results.Count>0;}}
        public void Acknowledge(NavigationAnnouncement announcement,bool accepted)
        {
            if(announcement==null || !ReferenceEquals(announcement,_proposal))return;
            _proposal=null;
            if(!accepted){_nextAttempt=_proposalAt+250;return;}
            _nextAttempt=0;_lastSpeech=_proposalAt;_lastOutput=announcement.Text;_lastPriority=announcement.Priority;
            if(announcement.Priority==80) {
                _results.RemoveRange(0,Math.Min(_proposalResultCount,_results.Count));ClearPassive();
            }else if(announcement.Priority==90) {
                ClearPassive();_announcedCue=_proposalCue;
            }else if(announcement.Priority==60) {
                _announcedCue=_proposalCue;if(_cue==_proposalCue)_pendingCue=false;
            }else if(announcement.Priority==30)_pendingWarning=false;
            if(_proposalDepth.HasValue && _depth==_proposalDepth)_pendingDepth=false;
            if(_proposalFound.HasValue && _found==_proposalFound)_pendingFound=false;
        }
        public void Update(HotColdFrame frame)
        {
            if(frame==null || !frame.Active){Reset();return;}
            if(_frame!=null && (_frame.Generation!=frame.Generation || _frame.MapKey!=frame.MapKey))Reset();
            bool newRound=frame.RoundActive && (_frame==null || !_frame.RoundActive);
            bool ended=_frame!=null && _frame.RoundActive && !frame.RoundActive;
            if(newRound || ended){ClearPassive();_cue=_announcedCue=HotColdCue.Unknown;_depth=_found=_previousSeconds=null;}
            if(newRound)_results.Clear();
            _frame=frame;
            int? depth=frame.Depth.HasValue && frame.Depth.Value>=0?frame.Depth:null;
            int? found=frame.Found.HasValue && frame.Found.Value>=0?frame.Found:null;
            if(!frame.CanSpeak) {
                ClearPassive();_results.Clear();_cue=_announcedCue=HotColdCue.Unknown;_lastOutput=null;_proposal=null;
                _depth=depth;_found=found;_previousSeconds=DisplayedSeconds(frame);return;
            }
            if(frame.RoundActive) {
                if(found.HasValue && found!=_found && found.Value>0) {
                    _pendingFound=true;_foundAt=frame.NowMilliseconds;
                }
                if(depth!=_depth){_pendingDepth=depth.HasValue;_depthAt=frame.NowMilliseconds;}
                if(!found.HasValue)_pendingFound=false;
                UpdateWarnings(frame);
            }
            _depth=depth;_found=found;
        }
        public void ObserveCue(HotColdCue cue)
        {
            if(_frame==null || !_frame.CanSpeak || !_frame.RoundActive || cue==HotColdCue.Unknown)return;
            bool changed=cue!=_cue;_cue=cue;
            if(changed || cue!=_announcedCue){_pendingCue=true;_cueAt=_frame.NowMilliseconds;}
        }
        public void ObserveResult(string shownText)
        {
            if(_frame==null || !_frame.CanSpeak || String.IsNullOrEmpty(shownText))return;
            ClearPassive();_cue=_announcedCue=HotColdCue.Unknown;
            // The native caller deduplicates presentation instances, not text: identical
            // rewards on different digs must still be read. One round has at most eight finds.
            _results.Add(shownText);
        }
        public void ObserveOtherDialog(){ClearPassive();_cue=_announcedCue=HotColdCue.Unknown;_lastOutput=null;}
        public NavigationAnnouncement TakeSpeech(SpeechState speech, bool status, bool repeat)
        {
            if(_frame==null || !_frame.CanSpeak)return null;
            speech=speech??new SpeechState();double now=speech.NowMilliseconds;
            if(status)return Output(Status(),90,true,now);
            if(repeat && !String.IsNullOrEmpty(speech.LastAnnouncement))return Output(speech.LastAnnouncement,100,true,now);
            if(speech.DialogueStarting || now<_nextAttempt)return null;
            bool ownSpeech=_lastOutput!=null && speech.LastAnnouncement==_lastOutput && _lastPriority<=60;
            if(_results.Count>0) {
                if(speech.Busy && !ownSpeech)return null;
                string text=String.Join(" ",_results.ToArray());
                return Output(text,80,ownSpeech,now);
            }
            if(speech.DialogueActive){ClearPassive();return null;}
            if(!_frame.RoundActive)return null;
            if(now-_cueAt>2000)_pendingCue=false;
            if(now-_depthAt>2000)_pendingDepth=false;
            if(now-_foundAt>2000)_pendingFound=false;
            if(now-_warningAt>2000)_pendingWarning=false;
            if(now-_lastSpeech<300)return null;
            if(_pendingCue) {
                if(speech.Busy && !ownSpeech)return null;
                string text=CueText(_cue);
                if(_pendingDepth && _depth.HasValue)text+=" Depth "+_depth.Value+".";
                if(_pendingFound && _found.HasValue)text+=" Found "+_found.Value+".";
                return Output(text,60,ownSpeech,now);
            }
            // Numeric updates and the clock wait until speech is idle. Rapid pecks
            // replace the pending depth instead of creating a queue of old numbers.
            if(speech.Busy)return null;
            if(_pendingDepth || _pendingFound) {
                var parts=new List<string>();
                if(_pendingDepth && _depth.HasValue)parts.Add("Depth "+_depth.Value+".");
                if(_pendingFound && _found.HasValue)parts.Add("Found "+_found.Value+".");
                if(parts.Count>0)return Output(String.Join(" ",parts.ToArray()),50,false,now);
            }
            int? seconds=DisplayedSeconds(_frame);
            if(_pendingWarning && _frame.TimerRunning && seconds.HasValue && seconds.Value>0) {
                return Output(TimeText(seconds.Value),30,false,now);
            }
            return null;
        }
        private void UpdateWarnings(HotColdFrame frame)
        {
            int? seconds=DisplayedSeconds(frame);
            if(!seconds.HasValue || !frame.TimerRunning){_previousSeconds=null;_pendingWarning=false;return;}
            if(_previousSeconds.HasValue) {
                if(seconds>_previousSeconds)_pendingWarning=false; // Native bonus time rearms crossings.
                if(seconds.Value>0 && ((_previousSeconds>10 && seconds<=10) || (_previousSeconds>5 && seconds<=5))) {
                    _pendingWarning=true;_warningAt=frame.NowMilliseconds;
                }
            }
            _previousSeconds=seconds;
            if(seconds==0)_pendingWarning=false;
        }
        private string Status()
        {
            int? seconds=DisplayedSeconds(_frame);
            string text="Chocobo Hot and Cold. "+(seconds.HasValue?TimeText(seconds.Value):"Time unavailable.");
            if(_frame.RoundActive && _cue!=HotColdCue.Unknown)text+=" "+CueText(_cue);
            if(_depth.HasValue)text+=" Depth "+_depth.Value+".";
            text+=" "+(_found.HasValue?"Found "+_found.Value+".":"Found count unavailable.");
            return text;
        }
        // TimerUI rounds to milliseconds through TimeSpan, then truncates its digits.
        // Its two minute digits wrap at 100. Invalid or unshown time is unavailable.
        private static int? DisplayedSeconds(HotColdFrame frame)
        {
            double time=frame.TimerSeconds;
            if(!frame.TimerVisible || Double.IsNaN(time) || Double.IsInfinity(time) || time<0 || time>5999.999)return null;
            return ((int)Math.Floor(Math.Floor(time*1000+0.5)/1000))%6000;
        }
        private static string TimeText(int seconds)
        {
            if(seconds<60)return seconds+(seconds==1?" second remaining.":" seconds remaining.");
            int minutes=seconds/60,remainder=seconds%60;
            return minutes+(minutes==1?" minute":" minutes")+" "+remainder+(remainder==1?" second remaining.":" seconds remaining.");
        }
        private static string CueText(HotColdCue cue)
        {
            switch(cue){case HotColdCue.NothingNearby:return "Nothing nearby.";case HotColdCue.Far:return "Far.";case HotColdCue.Close:return "Close.";case HotColdCue.Located:return "Treasure located; keep digging.";default:return "";}
        }
        private NavigationAnnouncement Output(string text,int priority,bool interrupt,double now)
        {
            _proposalAt=now;_proposalCue=_cue;_proposalResultCount=priority==80?_results.Count:0;
            _proposalDepth=(priority==50 || priority==60) && _pendingDepth?_depth:null;
            _proposalFound=(priority==50 || priority==60) && _pendingFound?_found:null;
            return _proposal=new NavigationAnnouncement{Text=text,Priority=priority,Interrupt=interrupt};
        }
        private void ClearPassive(){_pendingCue=_pendingDepth=_pendingFound=_pendingWarning=false;}
        public void Reset()
        {
            _frame=null;_cue=_announcedCue=HotColdCue.Unknown;_depth=_found=_previousSeconds=null;
            ClearPassive();_results.Clear();_lastOutput=null;_lastSpeech=-10000;_proposal=null;_nextAttempt=0;
        }
    }
    public static class HotColdPresentationBindings
    {
        public static HotColdDialogRole Classify(string symbol, int textId, string initialText, string cleanText, out HotColdCue cue)
        {
            cue=HotColdCue.Unknown;
            if(String.IsNullOrEmpty(initialText))return HotColdDialogRole.Other;
            bool english=symbol=="US" || symbol=="UK";
            bool japanese=symbol=="JP";
            if(!english && !japanese && symbol!="FR" && symbol!="GR" && symbol!="IT" && symbol!="ES")return HotColdDialogRole.Other;
            int found=english || japanese?274:275;
            if(textId==found && Has(initialText,"[NUMB=7]") && Has(initialText,"[MPOS=5,30]"))return HotColdDialogRole.FoundCount;
            string labelPosition=english?"[MPOS=4,54]":japanese?"[MPOS=5,57]":"[MPOS=5,54]";
            string numberPosition=english?"[MPOS=41,54]":japanese?"[MPOS=30,57]":symbol=="FR"?"[MPOS=78,54]":symbol=="GR"?"[MPOS=38,54]":symbol=="IT"?"[MPOS=70,54]":"[MPOS=79,54]";
            if(textId==found+1 && Has(initialText,labelPosition))return HotColdDialogRole.DepthLabel;
            if(textId==found+2 && Has(initialText,"[NUMB=6]") && Has(initialText,numberPosition))return HotColdDialogRole.DepthNumber;
            int firstCry=found+4,result=english?294:japanese?299:300;
            if(textId==found+3 || textId>=result && textId<=result+26)return HotColdDialogRole.Result;
            if(textId<firstCry || textId>=result)return HotColdDialogRole.Other;
            // Verified against the installed native text and its instructions, not
            // punctuation guesses. Extra cold variants collapse to the same spoken cue.
            string[] cries;
            switch(symbol) {
                case "US":case "UK":cries=new[]{"Kweh.","Kweh!?","Kwehhh!?","K-KWEHHH!!!"};break;
                case "JP":cries=new[]{"クェ…","クェ","クェッ!?","クェェッ!!?","ク、クェーッ!!"};break;
                case "FR":cries=new[]{"Kwoa...","Kwoa","Kwoaa ?!","Kwoaaa ?!","Kwo... Kwoaa !!"};break;
                case "GR":cries=new[]{"Krä!","Kräh!","Kräähh!","Käräh!!","Kärääh!!"};break;
                case "IT":cries=new[]{"Kué...","Kué!","Kuéh!","Kuééh??!","K-Kuééh!!"};break;
                default:cries=new[]{"Kue...","Kue.","¡¿Kue?!","¡¡¿Kuee?!!","¡¡Ku... kueee!!"};break;
            }
            for(int i=0;i<cries.Length;i++)if(cleanText==cries[i]) {
                cue=english?(HotColdCue)(i+1):i<=1?HotColdCue.NothingNearby:(HotColdCue)i;
                return HotColdDialogRole.Cry;
            }
            return HotColdDialogRole.Other;
        }
        private static bool Has(string text,string tag){return text.IndexOf(tag,StringComparison.Ordinal)>=0;}
    }
}
