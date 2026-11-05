using Memoria.Accessibility.Navigation;
using Memoria.ScreenReader;
using UnityEngine;

namespace Memoria.Accessibility.AudioDescriptions
{
    internal static class NativeEarthShrineCue
    {
        private const string DirectorScript="a58334f6a96f04f4fa2e510a6304080f82ac96bf8bb7e2cabb07c69ade584d13";
        private const int JumpBubbleSite=811;
        private static Obj _lastDirector;
        private static float _lastPoll=-1f;
        private static bool _spoken;

        internal static bool IsTrapScene(EventEngine engine)
        {
            return engine!=null && engine.gMode==1 && FF9StateSystem.Common.FF9.fldMapNo==2554 &&
                FF9StateSystem.EventState.ScenarioCounter==10670 && IsDirector(engine.GetObjUID(0));
        }

        private static bool IsDirector(Obj director)
        {
            return director!=null && director.sid==0 && director.uid==0 && director.ebData!=null &&
                NativeFieldTargets.Fingerprint(director.ebData)==DirectorScript;
        }

        // Called when the director polls the same jump bubble sighted players
        // use. The event VM still owns the input window, timer and outcome.
        internal static void BubblePolled(EventEngine engine,Obj director,int offset)
        {
            try {
                if(offset!=JumpBubbleSite || !IsTrapScene(engine) || !ReferenceEquals(engine.GetObjUID(0),director) ||
                    UIManager.Instance.State!=UIManager.UIState.FieldHUD || UIManager.Instance.IsPause ||
                    !EIcon.IsProcessingFIcon || EIcon.IsDialogBubble || !HonoInputManager.ApplicationIsActivated())return;
                float now=Time.realtimeSinceStartup;
                if(!ReferenceEquals(_lastDirector,director) || now-_lastPoll>.3f)_spoken=false;
                _lastDirector=director;_lastPoll=now;
                if(!_spoken)_spoken=ScreenReaderManager.Instance.TrySpeak("Jump",true);
            }catch { } // Never delay the event VM during the input window.
        }
    }
}
