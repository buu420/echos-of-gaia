using System;
using Assets.Scripts.Common;
using Memoria.Accessibility.Navigation;
using Memoria.ScreenReader;
using UnityEngine;

namespace Memoria.Accessibility.AudioDescriptions
{
    internal static class NativePandemoniumLightCue
    {
        private const int FieldId = 2709;
        private const int PinCount = 26;
        private const string Background = "439e9a83b80dd2f6cc7da5cdcf7065ebbc9ec34b1e56d1229486b524a1a1b374";
        private const string Director = "3655ed3e4dcbc8b148ee2af789b8ca65732df059ab2e997157f75d0ca8ed1317";
        private const string LightLoopOne = "0526d4ed39280d365dd45e69cca1ac16b67c0c5b35b61c7cdf01594c87dca487";
        private const string LightLoopTwo = "4c604ea0949ad3afeff7d8092a031c0482ee323f826c460797d0b34d02a064a2";
        private const string LightShader = "PSX/FieldMap_Abr_1";

        private static readonly PandemoniumLightCue Cue = new PandemoniumLightCue();
        private static readonly Func<string, bool> SpeakCurrent = Speak;
        private static BGSCENE_DEF _scene;
        private static float _retryAfter;

        internal static void Tick(FieldMap field)
        {
            if (FF9StateSystem.Common.FF9.fldMapNo != FieldId)
            {
                if (_scene != null)
                    Reset(null);
                return;
            }

            try
            {
                if (!ReferenceEquals(_scene, field.scene))
                    Reset(field.scene);

                var engine = PersistenSingleton<EventEngine>.Instance;
                var ui = UIManager.Instance;
                var player = engine.GetControlChar() as Actor;
                var camera = field.GetMainCamera();
                var backgroundCamera = field.GetCurrentBgCamera();

                if (engine.gMode != 1 || !ReferenceEquals(engine.fieldmap, field) || field.camIdx != 0 || field.curCamIdx != 0 ||
                    !CanObserve(engine, ui) || player == null || player.fieldMapActorController == null || player.go == null ||
                    !player.go.activeInHierarchy || !engine.objIsVisible(player) || !player.frontCamera || camera == null || !camera.isActiveAndEnabled || backgroundCamera == null ||
                    _scene?.ebgBin == null || _scene.overlayList.Count != PinCount + 3 || NativeFieldTargets.Fingerprint(_scene.ebgBin) != Background ||
                    !IsScript(engine, 0, Director) || !IsScript(engine, 1, LightLoopOne) || !IsScript(engine, 2, LightLoopTwo))
                {
                    Reset(field.scene);
                    return;
                }

                Vector3 projected = PSX.CalculateGTE_RTPT(player.fieldMapActorController.curPos, Matrix4x4.identity,
                    backgroundCamera.GetMatrixRT(), backgroundCamera.GetViewDistance(), field.GetProjectionOffset());
                NavigationPoint playerPosition = ViewportPoint(camera, projected);
                if (!PandemoniumLightCue.InView(playerPosition))
                {
                    Reset(field.scene);
                    return;
                }

                int visibleLights = 0;
                int directions = 0;
                bool litOutsideProof = false;
                for (int index = 0; index < PinCount; index++)
                {
                    BGOVERLAY_DEF overlay = _scene.overlayList[index];
                    if ((overlay.flags & BGOVERLAY_DEF.OVERLAY_FLAG.Active) == 0 || overlay.transform == null ||
                        !overlay.transform.gameObject.activeInHierarchy)
                        continue;

                    // These points fall in nontransparent pixels of the original pin artwork.
                    float x = overlay.w * .5f;
                    float y = overlay.h * .625f;
                    BGSPRITE_LOC_DEF tile = PinTile(overlay, x, y);
                    var renderer = overlay.transform.GetComponent<Renderer>();
                    if (renderer == null && tile?.transform != null)
                        renderer = tile.transform.GetComponent<Renderer>();

                    Material material = renderer == null ? null : renderer.sharedMaterial;
                    if (tile == null || overlay.camNdx != field.camIdx || overlay.isMemoria || renderer == null || material == null ||
                        material.shader == null || material.shader.name != LightShader || !material.HasProperty("_Color"))
                    {
                        litOutsideProof = true;
                        continue;
                    }

                    Color tint = material.GetColor("_Color");
                    if (tint.a <= 0 || tint.maxColorComponent <= 0 || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                        (camera.cullingMask & (1 << renderer.gameObject.layer)) == 0)
                        continue;

                    Vector3 anchor = overlay.transform.TransformPoint(new Vector3(x, y, tile.depth));
                    int direction = PandemoniumLightCue.Direction(ViewportPoint(camera, anchor), playerPosition, camera.aspect);
                    if (direction < 0 || !renderer.isVisible)
                    {
                        litOutsideProof = true;
                        continue;
                    }

                    visibleLights |= 1 << index;
                    directions |= 1 << direction;
                }

                if (visibleLights == 0 && litOutsideProof)
                    Reset(field.scene);
                else
                    Cue.Observe(visibleLights, directions, SpeakCurrent);
            }
            catch (Exception)
            {
                Reset(null);
            }
        }

        private static bool CanObserve(EventEngine engine, UIManager ui)
        {
            int progress = FF9StateSystem.EventState.ScenarioCounter;
            return progress >= 10950 && progress < 10970 && TimerUI.Enable && TimerUI.Play && TimerUI.GetDisplay() && TimerUI.Time > 0 &&
                ui.State == UIManager.UIState.FieldHUD && !ui.IsPause && !ui.IsLoading && ui.IsPlayerControlEnable &&
                engine.GetUserControl() && EventInput.IsMovementControl && HonoInputManager.ApplicationIsActivated() &&
                FF9StateSystem.Field.FF9Field.loc.map.nextMode != 2 && !SceneDirector.IsScreenObscured &&
                !PersistenSingleton<SceneDirector>.Instance.IsFading && ScreenReaderManager.Instance.Enabled;
        }

        private static BGSPRITE_LOC_DEF PinTile(BGOVERLAY_DEF overlay, float x, float y)
        {
            foreach (BGSPRITE_LOC_DEF tile in overlay.spriteList)
            {
                if (x >= tile.offX && x < tile.offX + 16 && y >= tile.offY && y < tile.offY + 16)
                    return tile;
            }
            return null;
        }

        private static NavigationPoint ViewportPoint(Camera camera, Vector3 point)
        {
            Vector3 viewport = camera.WorldToViewportPoint(point);
            if (viewport.z < camera.nearClipPlane || viewport.z > camera.farClipPlane)
                return default;
            return new NavigationPoint(viewport.x, viewport.y, viewport.z);
        }

        private static bool IsScript(EventEngine engine, int id, string hash)
        {
            Obj entry = engine.GetObjUID(id);
            return entry != null && entry.sid == id && entry.ebData != null && NativeFieldTargets.Fingerprint(entry.ebData) == hash;
        }

        private static bool Speak(string text)
        {
            float now = Time.realtimeSinceStartup;
            if (now < _retryAfter)
                return false;
            bool accepted = ScreenReaderManager.Instance.TrySpeak(text, true);
            if (!accepted)
                _retryAfter = now + .1f;
            return accepted;
        }

        private static void Reset(BGSCENE_DEF scene)
        {
            _scene = scene;
            _retryAfter = 0;
            Cue.Reset();
        }
    }
}
