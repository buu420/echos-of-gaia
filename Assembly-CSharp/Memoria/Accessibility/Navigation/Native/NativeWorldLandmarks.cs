using System;
using System.Collections.Generic;
using UnityEngine;

namespace Memoria.Accessibility.Navigation
{
    internal static class NativeWorldLandmarks
    {
        internal const int MemoriaId = 53;
        private const float PortalAltitude = 7300f / 256;
        internal const float MemoriaFlightHeight = 32f;
        private static readonly Vector3[] PitOffsets =
        {
            new Vector3(8f, 1.66f, -60f),
            new Vector3(23.68f, 1.18f, -23.98f),
            new Vector3(4f, 1.18f, -16f),
            new Vector3(28f, 1.18f, -4f)
        };
        private static readonly int[] PitBlocks = { 91, 115, 115, 115 };
        private static readonly int[] PitTags = { 0x8799, 0x8899, 0x8899, 0x8899 };
        private static readonly Transform[] PitObjects = new Transform[4];
        private static readonly bool[] ObservedPit = new bool[4];
        private static readonly bool[] ObservedPlume = new bool[4];
        private static long _generation = -1;
        private static string _save;
        private static bool _observedMemoria;
        private static WMWorld _observedWorld;
        private static int _frame = -1;

        internal static void Append(List<WorldTargetObservation> result, NavigationContext context, EventEngine engine, string[] text)
        {
            var world = ff9.world;
            if (world == null || world.InitialBlocks == null || world.MainCamera == null || engine == null || engine.gMode != 3 ||
                !ff9.w_blockReady || !ff9.w_frameEventEnable || ff9.w_naviMode == 2)
                return;

            if (_observedWorld != world || _generation != context.Generation || _save != context.SaveIdentity)
            {
                _observedWorld = world;
                _generation = context.Generation;
                _save = context.SaveIdentity;
                Array.Clear(PitObjects, 0, PitObjects.Length);
                Array.Clear(ObservedPit, 0, ObservedPit.Length);
                Array.Clear(ObservedPlume, 0, ObservedPlume.Length);
                _observedMemoria = false;
                _frame = -1;
            }

            if (MemoriaAvailable(context, engine))
            {
                var portal = ff9.w_effectLastPos;
                string label = NativeWorldTargets.WorldLabel(text, MemoriaId);
                result.Add(new WorldTargetObservation
                {
                    Id = new TargetIdentity(TargetKind.WorldLocation, context.LogicalMapKey, MemoriaId),
                    Position = new NavigationPoint(portal.x, portal.y, portal.z), Label = label,
                    Available = true, Visible = true, Availability = "Fly up to the portal and use Confirm when its prompt appears."
                });
            }

            if (context.Transport.Kind == TransportKind.Foot &&
                WorldConfiguration.UseWorldEffect(WorldEffect.SandPit) && context.StoryProgress >= 9450 && context.StoryProgress < 9890)
                AppendSandPits(result, context, world);
        }

        private static bool MemoriaAvailable(NavigationContext context, EventEngine engine)
        {
            var world = ff9.world;
            var portal = world == null ? null : world.kWorldPackEffectBlack;
            if (context.Transport.Kind != TransportKind.Invincible || !context.Transport.IsAirship ||
                context.StoryProgress < 11100 ||
                engine == null || !WorldConfiguration.UseWorldEffect(WorldEffect.Memoria) || portal == null ||
                ff9.w_frameGetParameter(204) >= 15)
                return false;
            int map = FF9StateSystem.Common.FF9.wldMapNo;
            if (map != 9008 && map != 9009)
                return false;
            var director = engine.FindObjByUID(0);
            var entry = engine.FindObjByUID(map == 9008 ? 6 : 7);
            if (director == null || director.ebData == null || entry == null || entry.ebData == null)
                return false;
            const string world08 = "e4070871731525c50bad4c47bfa383538e5cf96a2260e9fa1f3bf25b13323425";
            const string world09 = "a98a4cfab5d909a9a048809d2d2a21de1d86de1eae6d36acbd4dfae946fcd747";
            const string flight08 = "f0d579ca7cee4a81bc5e690ffe732b38663c8ce157583904bfcf79b6348afd02";
            const string flight09 = "e61f3856d63651a42ccd90ea0bafe81c8daf816d545f79455cce8ebcf3e7e0d3";
            if (NativeFieldTargets.Fingerprint(director.ebData) != (map == 9008 ? world08 : world09) ||
                NativeFieldTargets.Fingerprint(entry.ebData) != (map == 9008 ? flight08 : flight09))
                return false;
            if (WorldCoordinates.Distance(context.Position, new NavigationPoint(ff9.w_effectLastPos.x, 0, ff9.w_effectLastPos.z)) > 220f)
                return false;
            var renderer = portal.GetComponentInChildren<Renderer>();
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                return false;
            _observedMemoria |= OnScreen(portal.position);
            return _observedMemoria;
        }

        internal static bool IsMemoria(NavigationTarget target)
        {
            return target != null && target.NativeLocationId < 0 && target.PhysicalIdentity.Kind == TargetKind.WorldLocation &&
                target.PhysicalIdentity.NativeId == MemoriaId;
        }

        internal static bool MemoriaPrompt(NavigationContext context)
        {
            if (context.Position.Y <= PortalAltitude || ff9.w_frameGetParameter(204) >= 15 ||
                WorldCoordinates.Distance(context.Position, new NavigationPoint(ff9.w_effectLastPos.x, 0, ff9.w_effectLastPos.z)) > 2f)
                return false;
            var engine = UnityEngine.Object.FindObjectOfType<EventEngine>();
            var vars = engine == null || engine.gMode != 3 ? null : engine.GetMapVar();
            return vars != null && vars.Length > 35 && vars[35] == 0 && vars[28] == (context.StoryProgress < 11200 ? 53 : 54);
        }

        private static void AppendSandPits(List<WorldTargetObservation> result, NavigationContext context, WMWorld world)
        {
            if (WorldCoordinates.Distance(context.Position, new NavigationPoint(1200, 0, -280)) > 110f)
                return;
            bool samplePuffs = _frame != Time.frameCount;
            _frame = Time.frameCount;
            for (int i = 0; i < PitOffsets.Length; i++)
            {
                var block = world.InitialBlocks[PitBlocks[i] % 24, PitBlocks[i] / 24];
                if (block == null || !block.IsReady)
                    continue;
                var pit = PitObjects[i];
                if (pit == null)
                {
                    foreach (Transform child in block.Transform)
                        if (child.name.StartsWith("Quicksand", StringComparison.Ordinal) && (child.localPosition - PitOffsets[i]).sqrMagnitude < 0.001f)
                        {
                            PitObjects[i] = pit = child;
                            break;
                        }
                }
                if (pit == null || !pit.gameObject.activeInHierarchy)
                    continue;

                bool onScreen = OnScreen(pit.position);
                ObservedPit[i] |= onScreen;
                if (!ObservedPit[i])
                    continue;

                if (onScreen && samplePuffs && !ObservedPlume[i] && world.WorldSPSSystem != null)
                {
                    foreach (var sps in world.WorldSPSSystem.SpsList)
                        if (sps.worldSpsId == SPSConst.WorldSPSEffect.SMOKE_SAND_PIT && sps.spsBin != null && sps.meshRenderer.enabled &&
                            (new Vector2(pit.position.x - sps.transform.position.x, pit.position.z - sps.transform.position.z)).sqrMagnitude < 36f &&
                            OnScreen(sps.transform.position))
                        {
                            ObservedPlume[i] = true;
                            break;
                        }
                }

                var offset = PitOffsets[i];
                var point = new NavigationPoint(block.InitialX * 64 + offset.x, offset.y, -block.InitialY * 64 + offset.z);
                // The rendered plumes are observations. Silence never identifies
                // a correct pit or labels an unseen plume.
                string label = "Sand Pit " + (i + 1) + (ObservedPlume[i] ? ", sand plume seen" : "");
                result.Add(new WorldTargetObservation
                {
                    Id = new TargetIdentity(TargetKind.WorldLocation, context.LogicalMapKey, 64 + i),
                    Position = point, Label = label, Available = true, Visible = true,
                    WorldEntranceTags = new[] { PitTags[i] }, WorldEntranceRadius = 7f
                });
            }
        }

        internal static bool OnScreen(Vector3 point)
        {
            var world = ff9.world;
            var camera = world == null ? null : world.MainCamera;
            if (camera == null)
                return false;
            var view = camera.WorldToViewportPoint(point);
            return view.z >= camera.nearClipPlane && view.z <= camera.farClipPlane &&
                view.x >= 0 && view.x <= 1 && view.y >= 0 && view.y <= 1;
        }

    }
}
