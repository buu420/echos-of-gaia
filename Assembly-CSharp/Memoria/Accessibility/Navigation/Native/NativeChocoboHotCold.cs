using System;
using System.Collections.Generic;
using Assets.Sources.Scripts.UI.Common;
using Memoria.Assets;
using Memoria.Prime;
using Memoria.ScreenReader;
using UnityEngine;
namespace Memoria.Accessibility.Navigation
{
    public static class NativeChocoboHotCold
    {
        private sealed class Presentation { public TextParser Parser; public HotColdDialogRole Role; }
        private sealed class DisplayedNumber
        {
            public Dialog Dialog;public TextParser Parser;public int? Value;
            public int? Read(Dialog dialog,int variable)
            {
                if(dialog==null){Dialog=null;Parser=null;Value=null;return null;}
                TextParser parser=dialog.CurrentParser;
                if(Dialog!=dialog || !ReferenceEquals(Parser,parser)){Dialog=dialog;Parser=parser;Value=null;}
                // A numeric refresh temporarily clears the parser. Retain only this
                // same window's last rendered value until its replacement is rendered.
                int number;
                if(parser!=null && parser.Step>=TextParser.ParseStep.Render && parser.Vertices.size>0 && parser.VariableMessageValues.TryGetValue(variable,out number))Value=number;
                return Value;
            }
        }
        private static readonly ChocoboHotColdReader Reader=new ChocoboHotColdReader();
        private static readonly DisplayedNumber Depth=new DisplayedNumber(),Found=new DisplayedNumber();
        private static readonly Dictionary<Dialog,Presentation> Opened=new Dictionary<Dialog,Presentation>();
        private static readonly NavigationCommand[] NoCommands=new NavigationCommand[0];
        private static UIManager _ui;private static DialogManager _dialogs;
        private static NavigationContext _context;
        private static bool _knownField,_active,_round,_hasChoice,_otherDialog,_openingDialog,_hasPresentation,_reported,_failed,_failureAnnounced;
        private static string _map;private static long _generation;

        // Reuse the references already acquired by navigation. No scene searches,
        // asset loads, event-variable queries or movement/dig commands belong here.
        public static bool ObserveContext(NavigationContext context,UIManager ui,DialogManager dialogs,int mode,bool pendingBattle)
        {
            try {
                if(_map!=context.LogicalMapKey || _generation!=context.Generation)Reset();
                _map=context.LogicalMapKey;_generation=context.Generation;_context=context;_ui=ui;_dialogs=dialogs;
                _knownField=mode==1 && !pendingBattle && KnownField();
                bool readout=false,result=false;_hasChoice=_otherDialog=_openingDialog=false;
                if(_knownField && dialogs!=null)foreach(Dialog dialog in dialogs.ActiveDialogList) {
                    if(!Presenting(dialog))continue;
                    HotColdDialogRole role=Role(dialog);
                    readout|=Shown(dialog) && role==HotColdDialogRole.FoundCount;
                    result|=Shown(dialog) && role==HotColdDialogRole.Result;
                    _hasChoice|=dialog.StartChoiceRow>=0;
                    _otherDialog|=role==HotColdDialogRole.Other;
                    _openingDialog|=role==HotColdDialogRole.Other && dialog.CurrentState!=Dialog.State.CompleteAnimation;
                }
                _round=_knownField && TimerUI.Enable && (EventHUD.CurrentHUD==MinigameHUD.ChocoHot || TimerUI.Time>0 && (TimerUI.GetDisplay() || readout));
                _active=_knownField && (_round || result || Reader.HasPendingResults);
                // Speech already shown can outlive its window without holding
                // ordinary navigation or controls in the minigame inspection mode.
                return _knownField && (_round || result);
            }catch(Exception exception){Reset();Report(exception);return false;}
        }
        public static bool CanInspect
        {
            get{return _active && _ui!=null && _ui.State==UIManager.UIState.FieldHUD && !_ui.IsPause && !_ui.IsLoading && !_hasChoice;}
        }
        private static bool KnownField()
        {
            if(FF9TextTool.FieldZoneId!=945)return false;
            int field=FF9StateSystem.Common.FF9.fldMapNo;string scene=FF9StateSystem.Field.SceneName;
            return field==2950 && scene=="FBG_N14_CHCB_MAP300_CH_FST_0" ||
                field==2951 && scene=="FBG_N14_CHCB_MAP301_CH_HLG_0" ||
                field==2952 && scene=="FBG_N14_CHCB_MAP302_CH_FGD_0";
        }
        private static bool Shown(Dialog dialog)
        {
            return dialog!=null && dialog.IsActive && dialog.CurrentState==Dialog.State.CompleteAnimation && dialog.gameObject.activeInHierarchy &&
                dialog.PhraseLabel!=null && dialog.PhraseLabel.enabled && dialog.PhraseLabel.gameObject.activeInHierarchy && dialog.PhraseLabel.alpha>0;
        }
        private static bool Presenting(Dialog dialog)
        {
            return dialog!=null && dialog.IsActive && dialog.gameObject.activeInHierarchy &&
                (dialog.CurrentState==Dialog.State.OpenAnimation || dialog.CurrentState==Dialog.State.TextAnimation || dialog.CurrentState==Dialog.State.CompleteAnimation);
        }
        private static HotColdDialogRole Role(Dialog dialog)
        {
            TextParser parser=dialog.CurrentParser;if(parser==null)return HotColdDialogRole.Other;
            Presentation opened;
            if(Opened.TryGetValue(dialog,out opened) && ReferenceEquals(opened.Parser,parser))return opened.Role;
            HotColdCue cue;
            return HotColdPresentationBindings.Classify(Localization.CurrentSymbol,dialog.TextId,parser.InitialText,null,out cue);
        }
        private static HotColdFrame ReadFrame(bool openedResult)
        {
            Dialog depth=null,found=null;bool label=false,ambiguousDepth=false,ambiguousFound=false;
            if(_dialogs!=null)foreach(Dialog dialog in _dialogs.ActiveDialogList) {
                if(!Shown(dialog))continue;
                HotColdDialogRole role=Role(dialog);
                if(role==HotColdDialogRole.DepthLabel)label=true;
                else if(role==HotColdDialogRole.DepthNumber){ambiguousDepth|=depth!=null;depth=dialog;}
                else if(role==HotColdDialogRole.FoundCount){ambiguousFound|=found!=null;found=dialog;}
            }
            bool canSpeak=_active && _ui!=null && _ui.State==UIManager.UIState.FieldHUD && !_ui.IsPause && !_ui.IsLoading && HonoInputManager.ApplicationIsActivated();
            return new HotColdFrame{Active=_active || openedResult,RoundActive=_round,CanSpeak=canSpeak,
                Generation=_generation,MapKey=_map,NowMilliseconds=Time.realtimeSinceStartup*1000d,
                TimerVisible=TimerUI.Enable && TimerUI.GetDisplay(),TimerRunning=TimerUI.Play,TimerSeconds=TimerUI.Time,
                Depth=Depth.Read(label && !ambiguousDepth?depth:null,6),Found=Found.Read(!ambiguousFound?found:null,7)};
        }
        // Called only by the existing dialog-complete announcement, after the
        // game's replacement and rendering. Returning false preserves its reader.
        public static bool TryHandleOpenedDialog(Dialog dialog,string cleanText)
        {
            try {
                if(_failed || !_knownField || _ui==null || _ui.State!=UIManager.UIState.FieldHUD || _context==null ||
                    _context.GateReason==GateReason.Battle || _generation!=NavigationLifecycleBridge.Generation || !KnownField() || !Shown(dialog))return false;
                HotColdCue cue;TextParser parser=dialog.CurrentParser;
                HotColdDialogRole role=HotColdPresentationBindings.Classify(Localization.CurrentSymbol,dialog.TextId,parser==null?null:parser.InitialText,cleanText,out cue);
                if(role==HotColdDialogRole.Other){if(_active)Reader.ObserveOtherDialog();return false;}
                if(!_active && role!=HotColdDialogRole.Result)return false;
                Presentation opened;
                if(Opened.TryGetValue(dialog,out opened) && ReferenceEquals(opened.Parser,parser))return true;
                Opened[dialog]=new Presentation{Parser=parser,Role=role};
                // A result can open in the same native call that ends the round.
                // Treat that shown presentation as inspection, not as new gameplay.
                if(role==HotColdDialogRole.Result && !_active){_active=true;_hasChoice=dialog.StartChoiceRow>=0;}
                Reader.Update(ReadFrame(role==HotColdDialogRole.Result));
                _hasPresentation=true;
                if(role==HotColdDialogRole.Cry)Reader.ObserveCue(cue);
                else if(role==HotColdDialogRole.Result)Reader.ObserveResult(cleanText);
                return true;
            }catch(Exception exception){Report(exception);return false;}
        }
        public static void Tick(NavigationContext context,NavigationCommand[] commands)
        {
            try {
                bool status=false,repeat=false;
                foreach(NavigationCommand command in commands){status|=command==NavigationCommand.Status;repeat|=command==NavigationCommand.Repeat;}
                var facade=ScreenReaderManager.Instance;
                if(_failed) {
                    if(context.CanInspect && (!_failureAnnounced || status)) {
                        _failureAnnounced=facade.TrySpeak("Hot and Cold status is unavailable. Ordinary dialog speech remains available.",true);
                    }
                    return;
                }
                Reader.Update(ReadFrame(false));
                _hasPresentation=true;
                var announcement=Reader.TakeSpeech(new SpeechState{NowMilliseconds=Time.realtimeSinceStartup*1000d,Busy=facade.IsSpeaking(),LastAnnouncement=facade.LastAnnouncement,DialogueActive=_otherDialog,DialogueStarting=_openingDialog},status,repeat);
                if(announcement!=null)Reader.Acknowledge(announcement,facade.TrySpeak(announcement.Text,announcement.Interrupt));
            }catch(Exception exception){Reader.Reset();Report(exception);}
        }
        public static void DrainResults(NavigationContext context)
        {
            if(_active && Reader.HasPendingResults)Tick(context,NoCommands);
            else if(!_active && _hasPresentation)ClearPresentation();
        }
        private static void ClearPresentation()
        {
            Reader.Reset();Depth.Read(null,6);Found.Read(null,7);Opened.Clear();_hasPresentation=false;
        }
        public static void Reset()
        {
            ClearPresentation();_active=_round=_knownField=_hasChoice=_otherDialog=_openingDialog=false;
            _map=null;_generation=0;_context=null;_ui=null;_dialogs=null;
        }
        private static void Report(Exception exception)
        {
            _failed=true;
            if(_reported)return;_reported=true;
            try{Log.Message("[FF9Access] Hot and Cold reader unavailable: {0}",exception);}catch(Exception) { }
        }
    }
}
