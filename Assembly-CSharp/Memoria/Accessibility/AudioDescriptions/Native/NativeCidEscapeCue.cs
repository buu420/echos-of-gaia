using System;
using Memoria.Accessibility.Navigation;
using Memoria.ScreenReader;
using UnityEngine;

namespace Memoria.Accessibility.AudioDescriptions
{
    internal static class NativeCidEscapeCue
    {
        private static readonly TimedTurtleCue Cue=new TimedTurtleCue();
        private const string TurtleScript="a025712ee0133e87b353dfb87bb0eaf5bf7dd1e18fc46f92ba6ec2c46d8356db";
        private const string WatchScript="84d1d8a2ff3b7e98e2a89f4b825e24d387c0ab10610cf915e697eb23feb10526";
        private static float _retryAt;
        private static byte[] _mapMemory;

        internal static bool IsKeySequence(EventEngine engine)
        {
            if(engine==null || engine.gMode!=1 || FF9StateSystem.Common.FF9.fldMapNo!=2204 ||
                FF9StateSystem.EventState.ScenarioCounter!=9812 || EventHUD.CurrentHUD!=MinigameHUD.GetTheKey ||
                !UIManager.Field.IsDisplayGetTheKeyHUD() || !TimerUI.Play || TimerUI.Time<=0f)return false;
            var globals=FF9StateSystem.EventState.gEventGlobal;
            return globals!=null && globals.Length>449 && (globals[449]&128)==0;
        }

        internal static void Tick(EventEngine engine)
        {
            int? facing=null;
            try {
                if(IsKeySequence(engine) && HonoInputManager.ApplicationIsActivated() &&
                    UIManager.Instance.State==UIManager.UIState.FieldHUD && !UIManager.Instance.IsPause) {
                    var turtle=engine.GetObjUID(3) as Actor;var watch=engine.GetObjUID(4);
                    var map=engine.GetMapVar();
                    if(!ReferenceEquals(_mapMemory,map)){_mapMemory=map;Cue.Observe(null,null);_retryAt=0;}
                    if(turtle!=null && turtle.sid==3 && turtle.model==575 && turtle.ebData!=null &&
                        turtle.go!=null && turtle.go.activeInHierarchy && (turtle.flags&EventEngine.flagShow)!=0 &&
                        NativeFieldTargets.Fingerprint(turtle.ebData)==TurtleScript && watch!=null && watch.sid==4 &&
                        watch.ebData!=null && NativeFieldTargets.Fingerprint(watch.ebData)==WatchScript && map!=null && map.Length>27)
                        // Field 2204, turtle tag 12: 434 sets this after its
                        // turn toward Cid, 612 clears it during the turn away.
                        // Entry 4 uses that same byte to detect moving while watched.
                        facing=map[27];
                }
            }catch(Exception){facing=null;}
            if(!facing.HasValue){_retryAt=0;_mapMemory=null;}
            Cue.Observe(facing,text=> {
                float now=Time.realtimeSinceStartup;
                if(now<_retryAt)return false;
                bool spoken=ScreenReaderManager.Instance.TrySpeak(text,true);
                if(!spoken)_retryAt=now+.1f;
                return spoken;
            });
        }
    }
}
