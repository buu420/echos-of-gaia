using System;
using Memoria.Accessibility.Navigation;
using Memoria.ScreenReader;
using UnityEngine;

namespace Memoria.Accessibility.AudioDescriptions
{
    internal static class NativeTelescopeCue
    {
        private const int TelescopeField = 606;
        private const string DirectorScript = "6e3e999302d319e2fe7e57a017c0babdd3dcf96af43271a39c54260fd1f49fc6";
        private const string PanScript = "f3331cf4a9728b91da65c5a94aae1ca4cbc745390a3fb1348bd43f1a2a79f49f";
        private const string IconScript = "d237de1e3cf458ed24b42424d9c52765e46ed4bddcb22fc767ce4999ea588c46";
        private const uint DirectionButtons = EventInput.Up | EventInput.Right | EventInput.Down | EventInput.Left;

        private static byte[] _memory;
        private static Obj _director;
        private static TelescopeGuide _guide = new TelescopeGuide();
        private static bool _canPan;
        private static int _announcedMarker;
        private static TelescopeDirection _announcedDirection;
        private static int _announcedRemaining = -1;
        private static float _nextSpeech;

        internal static uint FilterPan(uint input)
        {
            if (!_canPan || FF9StateSystem.Common.FF9.fldMapNo != TelescopeField || !PanControlsActive())
                return input;
            return _guide.FilterInput(input, DirectionButtons);
        }

        // Called after the native icon update. The icon actor consumes the
        // director's one-frame request byte before it shows the exclamation.
        internal static void Tick(EventEngine engine)
        {
            if (engine.gMode != 1 || FF9StateSystem.Common.FF9.fldMapNo != TelescopeField)
            {
                if (_memory != null) ResetSession(null, null);
                return;
            }

            try
            {
                var map = engine.GetMapVar();
                var director = engine.GetObjUID(0);
                if (!ReferenceEquals(_memory, map) || !ReferenceEquals(_director, director))
                    ResetSession(map, director);

                _canPan = map != null && map.Length > 34 && map[31] == 0 && map[34] == 1 &&
                    PanControlsActive() && Matches(director, 0, DirectorScript) &&
                    Matches(engine.GetObjUID(2), 2, PanScript) &&
                    Matches(engine.GetObjUID(3), 3, IconScript);
                short x = 0, y = 0;
                if (!_canPan || engine.fieldmap == null || engine.fieldmap.EBG_sceneGetVRP(ref x, ref y) == 0)
                {
                    Suspend();
                    return;
                }

                int viewed = map[33];
                bool prompt = TelescopeViews.Current(x, y, viewed) != 0 && EIcon.NavigationPresentedUid == 3;
                _guide.Observe(x, y, viewed, prompt);
                Speak(viewed);
            }
            catch (Exception)
            {
                Suspend();
            }
        }

        private static bool PanControlsActive()
        {
            // The event HUD is active on desktop without the touch overlay.
            return EventHUD.CurrentHUD == MinigameHUD.Telescope &&
                UIManager.Instance.State == UIManager.UIState.FieldHUD &&
                !UIManager.Instance.IsPause && HonoInputManager.ApplicationIsActivated() &&
                ScreenReaderManager.Instance.Enabled;
        }

        private static bool Matches(Obj entry, int id, string hash)
        {
            return entry != null && entry.uid == id && entry.sid == id && entry.ebData != null &&
                NativeFieldTargets.Fingerprint(entry.ebData) == hash;
        }

        private static void Speak(int viewed)
        {
            float now = Time.realtimeSinceStartup;
            if (_guide.Marker != 0)
            {
                if (_guide.Marker == _announcedMarker || now < _nextSpeech) return;
                if (ScreenReaderManager.Instance.TrySpeak(TelescopeViews.Label(_guide.Marker) + ". Confirm.", true))
                {
                    _announcedMarker = _guide.Marker;
                    _announcedDirection = TelescopeDirection.None;
                }
                else
                    _nextSpeech = now + .15f;
                return;
            }

            _announcedMarker = 0;
            int remaining = TelescopeViews.Remaining(viewed);
            if (_guide.Direction == TelescopeDirection.None || now < _nextSpeech ||
                (_guide.Direction == _announcedDirection && remaining == _announcedRemaining))
                return;
            string count = remaining == 1 ? "1 landmark remaining. " : remaining + " landmarks remaining. ";
            string message = (remaining == _announcedRemaining ? "" : count) +
                "Pan " + _guide.Direction.ToString().ToLowerInvariant() + ".";
            bool spoken = ScreenReaderManager.Instance.TrySpeak(message, false);
            _nextSpeech = now + (spoken ? .35f : .15f);
            if (spoken)
            {
                _announcedDirection = _guide.Direction;
                _announcedRemaining = remaining;
            }
        }

        private static void Suspend()
        {
            _canPan = false;
            _guide.Suspend();
            _announcedMarker = 0;
            _announcedDirection = TelescopeDirection.None;
            _nextSpeech = 0f;
        }

        private static void ResetSession(byte[] map, Obj director)
        {
            _memory = map;
            _director = director;
            _guide = new TelescopeGuide();
            _announcedRemaining = -1;
            Suspend();
        }
    }
}
