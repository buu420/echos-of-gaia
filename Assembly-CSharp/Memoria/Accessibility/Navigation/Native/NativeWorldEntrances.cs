using System;
using System.Collections.Generic;

namespace Memoria.Accessibility.Navigation
{
    internal static class NativeWorldEntrances
    {
        private const EBin.event_code_binary Request = EBin.event_code_binary.REQ;
        private static byte[] _directorScript;
        private static readonly Dictionary<int, List<ScriptInstruction>> _events = new Dictionary<int, List<ScriptInstruction>>();
        private static readonly Dictionary<int, List<int>> _tagsByPlace = new Dictionary<int, List<int>>();
        private static readonly Dictionary<int, int[]> _openByPlace = new Dictionary<int, int[]>();
        private static byte[] _globals, _map;
        private static int _frame = -1, _titleState = -1;

        internal static void Read(EventEngine engine, NavigationContext context)
        {
            if (_frame == UnityEngine.Time.frameCount)
                return;
            _frame = UnityEngine.Time.frameCount;

            var director = engine == null || engine.gMode != 3 ? null : engine.FindObjByUID(0);
            var script = director == null ? null : director.ebData;
            if (!ReferenceEquals(_directorScript, script))
            {
                _directorScript = script;
                _events.Clear();
                _tagsByPlace.Clear();
                _globals = _map = null;
                if (director != null && script != null)
                    ReadDirector(engine, director);
            }

            // Airships retain the native map-pin autopilot. Ground targets
            // are checked against the current world event, independent of map discovery.
            if (director == null || context.Transport.IsAirship || _events.Count == 0)
            {
                _openByPlace.Clear();
                _globals = _map = null;
                return;
            }

            var global = FF9StateSystem.EventState.gEventGlobal;
            var map = engine.GetMapVar();
            if (global == null || map == null)
                return;
            int titleState = ff9.w_naviLocationDraw;
            if (_titleState == titleState && SameState(_globals, global) && SameState(_map, map))
                return;
            _titleState = titleState;
            _globals = (byte[])global.Clone();
            _map = (byte[])map.Clone();
            _openByPlace.Clear();

            var snapshot = new ScriptSnapshot(global, map, null) { WorldLocationTitleState = titleState };
            foreach (var pair in _tagsByPlace)
            {
                var open = new List<int>();
                foreach (int tag in pair.Value)
                {
                    // The native proximity prompt changes M5[24–28] from its
                    // idle value. The candidate should describe arriving from
                    // elsewhere, so clear the previous prompt in the shadow.
                    for (int i = 24; i <= 28; i++)
                        if (snapshot.Read(1, 5, i) is int prompt && (prompt < 90 || (prompt >= 100 && prompt <= 164)))
                            snapshot.Write(1, 5, i, 100);
                    snapshot.Write(1, 5, 39, 0);
                    int? place = WorldEntranceScripts.PresentedPlace(_events[tag], snapshot, (int)Request);
                    if (place == pair.Key)
                        open.Add(tag);
                }
                _openByPlace.Add(pair.Key, open.ToArray());
            }
        }

        internal static int[] OpenTags(int place)
        {
            return _openByPlace.TryGetValue(place, out var tags) ? tags : null;
        }

        private static void ReadDirector(EventEngine engine, Obj director)
        {
            var script = director.ebData;
            if (script.Length < 2 || script.Length < 2 + 4 * script[1])
                return;
            for (int i = 0; i < script[1]; i++)
            {
                int offset = 2 + i * 4;
                int tag = script[offset] | script[offset + 1] << 8;
                int x = (tag >> 2) & 63, z = (tag >> 8) & 63;
                if ((tag & 0xc000) != 0x8000 || (tag & 3) == 0 || x >= 48 || z >= 40 || _events.ContainsKey(tag))
                    continue;

                var function = NativeFieldTargets.Decode(engine, director, tag);
                if (!function.Exists(i => i.Opcode == (int)Request && i.Constant(0) == 6 &&
                    i.Constant(1) == 1 && i.Constant(2) == 11))
                    continue;
                foreach (var instruction in function)
                {
                    var bytes = instruction.Expression;
                    // EXPR assigning the constant one-based place ID to M5[39].
                    if (instruction.Opcode != 5 || bytes == null || bytes.Length != 7 ||
                        bytes[0] != 0xd5 || bytes[1] != 39 || bytes[2] != 0x7d || bytes[5] != 44 || bytes[6] != 0x7f)
                        continue;
                    int place = bytes[3] | bytes[4] << 8;
                    if (place < 1 || place > 64)
                        continue;
                    _events[tag] = function;
                    if (!_tagsByPlace.TryGetValue(place, out var tags))
                        _tagsByPlace.Add(place, tags = new List<int>());
                    if (!tags.Contains(tag))
                        tags.Add(tag);
                }
            }
            foreach (var tags in _tagsByPlace.Values)
                tags.Sort();
        }

        private static bool SameState(byte[] previous, byte[] current)
        {
            if (previous == null || current == null || previous.Length != current.Length)
                return false;
            for (int i = 0; i < previous.Length; i++)
                if (previous[i] != current[i])
                    return false;
            return true;
        }
    }
}
