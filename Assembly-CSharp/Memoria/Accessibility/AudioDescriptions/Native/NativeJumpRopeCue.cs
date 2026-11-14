using System;
using Assets.Scripts.Common;
using Memoria.Accessibility.Navigation;
using Memoria.ScreenReader;
using UnityEngine;

namespace Memoria.Accessibility.AudioDescriptions
{
    internal static class NativeJumpRopeCue
    {
        private static readonly JumpRopeCue Cue = new JumpRopeCue();

        internal static void BubblePolled(EventEngine engine, int field, Obj scriptOwner, int offset)
        {
            try
            {
                if (scriptOwner == null || scriptOwner.sid != scriptOwner.uid || scriptOwner.ebData == null ||
                    !ReferenceEquals(scriptOwner.currentByte, scriptOwner.ebData))
                    return;

                JumpRopePrompt prompt = JumpRopeCue.Identify(field, scriptOwner.sid, offset, NativeFieldTargets.Fingerprint(scriptOwner.ebData));
                if (prompt == JumpRopePrompt.None || !CanPlay(engine))
                    return;

                byte[] map = engine.GetMapVar();
                int countOffset = field == 103 ? 43 : 59;
                if (map == null || map.Length <= countOffset + 2)
                    return;

                // The rope script puts this same count in its native dialogue. Use it
                // only to identify repeated polls within the currently open window.
                int completedJumps = map[countOffset] | (map[countOffset + 1] << 8) | (map[countOffset + 2] << 16);
                if (Cue.Observe(map, prompt, completedJumps, Time.frameCount, true))
                    FF9Sfx.FF9SFX_Play(FF9DBAll.SFX_EVENT_SYSTEM_SE000003);
            }
            catch (Exception)
            {
                // Optional audio feedback must never interrupt event script processing.
            }
        }

        private static bool CanPlay(EventEngine engine)
        {
            var ui = UIManager.Instance;
            if (engine.gMode != 1 || engine.fieldmap == null || !EventInput.IsProcessingInput ||
                ui.State != UIManager.UIState.FieldHUD || ui.IsPause || ui.IsLoading ||
                !HonoInputManager.ApplicationIsActivated() || !ScreenReaderManager.Instance.Enabled ||
                !Configuration.Accessibility.AnnounceIcons || Configuration.Icons.HideExclamation ||
                !EIcon.IsProcessingFIcon || EIcon.IsDialogBubble || SceneDirector.IsScreenObscured ||
                PersistenSingleton<SceneDirector>.Instance.IsFading || SoundLib.SoundEffectIsMute || FF9Sfx.muteSfx ||
                FF9StateSystem.Field.FF9Field.loc.map.nextMode == 2)
                return false;

            // Jump Rope takes movement control while the native KEYON accepts jump input.
            // The native field bubble is drawn above the current actor.
            var player = engine.GetControlChar() as Actor;
            return player != null && player.go != null && player.go.activeInHierarchy && player.frontCamera && engine.objIsVisible(player);
        }
    }
}
