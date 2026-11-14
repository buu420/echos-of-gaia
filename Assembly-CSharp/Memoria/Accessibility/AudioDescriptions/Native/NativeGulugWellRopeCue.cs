using Assets.Scripts.Common;
using Memoria.Accessibility.Navigation;
using Memoria.ScreenReader;
using UnityEngine;

namespace Memoria.Accessibility.AudioDescriptions
{
    internal static class NativeGulugWellRopeCue
    {
        private const int FieldId = 2361;
        private const string DirectorScript = "16863723339146ba4653ea660110426aa1274832e1a6b1a7e517b0f041f50b0f";
        private const string PlayerScript = "c35c252d6b09708574b366f8998ca6b9e6486cb916b585d7cba418c0e8653e10";
        private static readonly GulugWellRopeCue Cue = new GulugWellRopeCue();
        private static Actor _player;
        private static float _retryAfter;

        internal static void Tick(FieldMap field)
        {
            if (FF9StateSystem.Common.FF9.fldMapNo != FieldId)
            {
                if (_player != null)
                    Reset(null);
                return;
            }

            try
            {
                var engine = PersistenSingleton<EventEngine>.Instance;
                var player = engine.GetControlChar() as Actor;
                if (!ReferenceEquals(player, _player))
                    Reset(player);
                var controller = player?.fieldMapActorController;
                bool climbing = engine.gMode == 1 && ReferenceEquals(engine.fieldmap, field) && field.camIdx == 0 && field.curCamIdx == 0 &&
                    player != null && player.sid == 10 && player.uid == 10 && player.ebData != null &&
                    ReferenceEquals(player.currentByte, player.ebData) && GulugWellRopeCue.AcceptsDirection(player.ip, player.anim) &&
                    controller != null && (controller.charFlags & 1) == 0 && controller.activeTri == -1 &&
                    NativeFieldTargets.Fingerprint(player.ebData) == PlayerScript && IsDirector(engine.GetObjUID(0));
                if (!climbing)
                {
                    Cue.Observe(false, Speak);
                    return;
                }

                var ui = UIManager.Instance;
                if (!engine.GetUserControl() && EventInput.IsProcessingInput && ui.State == UIManager.UIState.FieldHUD &&
                    !ui.IsPause && !ui.IsLoading && player.go != null && player.go.activeInHierarchy && player.frontCamera &&
                    engine.objIsVisible(player) && HonoInputManager.ApplicationIsActivated() && ScreenReaderManager.Instance.Enabled &&
                    !SceneDirector.IsScreenObscured && !PersistenSingleton<SceneDirector>.Instance.IsFading &&
                    FF9StateSystem.Field.FF9Field.loc.map.nextMode != 2)
                    Cue.Observe(true, Speak);
            }
            catch (System.Exception)
            {
                Reset(null);
            }
        }

        private static bool IsDirector(Obj director)
        {
            return director != null && director.sid == 0 && director.uid == 0 && director.ebData != null &&
                NativeFieldTargets.Fingerprint(director.ebData) == DirectorScript;
        }

        private static void Reset(Actor player)
        {
            _player = player;
            _retryAfter = 0f;
            Cue.Observe(false, Speak);
        }

        private static bool Speak(string text)
        {
            float now = Time.realtimeSinceStartup;
            if (now < _retryAfter)
                return false;
            bool accepted = ScreenReaderManager.Instance.TrySpeak(text);
            if (!accepted)
                _retryAfter = now + .1f;
            return accepted;
        }
    }
}
