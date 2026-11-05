using System;
using Memoria.Accessibility.Navigation;
using Memoria.ScreenReader;
using UnityEngine;

namespace Memoria.Accessibility.AudioDescriptions
{
    internal static class NativeTelescopeCue
    {
        private const string DirectorScript="6e3e999302d319e2fe7e57a017c0babdd3dcf96af43271a39c54260fd1f49fc6";
        private const string PanScript="f3331cf4a9728b91da65c5a94aae1ca4cbc745390a3fb1348bd43f1a2a79f49f";
        private static byte[] _memory;
        private static int _previous;
        private static float _retryAt;

        internal static void Tick(EventEngine engine)
        {
            int view=0;
            try {
                if(engine!=null && engine.gMode==1 && FF9StateSystem.Common.FF9.fldMapNo==606 &&
                    EventHUD.CurrentHUD==MinigameHUD.Telescope && UIManager.Field.IsDisplayTelescopeHUD() &&
                    HonoInputManager.ApplicationIsActivated() && !UIManager.Instance.IsPause &&
                    UIManager.Instance.State==UIManager.UIState.FieldHUD) {
                    var map=engine.GetMapVar();var director=engine.GetObjUID(0);var pan=engine.GetObjUID(2);
                    if(!ReferenceEquals(_memory,map)){_memory=map;_previous=0;_retryAt=0;}
                    if(map!=null && map.Length>33 && map[32]==1 &&
                        director!=null && director.sid==0 && director.ebData!=null && NativeFieldTargets.Fingerprint(director.ebData)==DirectorScript &&
                        pan!=null && pan.sid==2 && pan.ebData!=null && NativeFieldTargets.Fingerprint(pan.ebData)==PanScript)
                        view=TelescopeViews.Current(engine.GetSysvar(12),engine.GetSysvar(13),map[33]);
                }else _memory=null;
            }catch(Exception){view=0;}
            if(view==0){_previous=0;return;}
            if(view==_previous || Time.realtimeSinceStartup<_retryAt)return;
            if(ScreenReaderManager.Instance.TrySpeak(TelescopeViews.Label(view)+". Confirm.",true))_previous=view;
            else _retryAt=Time.realtimeSinceStartup+.15f;
        }
    }
}
