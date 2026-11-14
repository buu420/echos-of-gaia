using System;
using System.Collections.Generic;
using UnityEngine;

namespace Memoria.Accessibility.Navigation
{
    internal static class NativeWorldLandings
    {
        private const string World03BoatInput = "e85c8f8310dc248763ab5c96163276c0a3c1a55537e39ee675b6191c8549162f";
        private const string World09BoatInput = "75178a6849056d0649124285a6df16758c10325e298959f66091c2a1bf54e530";
        private static WMWorld _seenWorld;
        private static long _seenGeneration;
        private static string _seenSave;
        private static TransportIdentity _seenTransport;
        private static int _seenLanding = -1;
        private static bool _seen;

        internal static void Append(List<WorldTargetObservation> result, NavigationContext context, EventEngine engine, WorldGeometrySnapshot geometry)
        {
            int id = WorldBoatLanding.Destination(context.StoryProgress);
            var world = ff9.world;
            if (_seenWorld != world || _seenGeneration != context.Generation || _seenSave != context.SaveIdentity ||
                !_seenTransport.Equals(context.Transport) || _seenLanding != id)
            {
                _seen = false;
                _seenWorld = world;
                _seenGeneration = context.Generation;
                _seenSave = context.SaveIdentity;
                _seenTransport = context.Transport;
                _seenLanding = id;
            }
            if (id < 0 || !IsBlueNarciss(context, engine, geometry))
                return;
            if (world == null || world.InitialBlocks == null)
                return;

            var point = WorldBoatLanding.LandingPoint(id);
            int x = (int)(point.X / 64), z = (int)(-point.Z / 64);
            var block = world.InitialBlocks[x, z];
            if (block == null || !block.IsReady || block.Form != 1 || block.Transform == null)
                return;

            var shore = geometry.Probe(point, geometry.Rules.CurrentTerrainId, null);
            if (shore == null || WorldTerrainRules.Topography(shore.TerrainId) != WorldBoatLanding.BoatTerrain)
                return;
            if (!_seen && WorldCoordinates.Distance(context.Position, point) <= 160f)
                _seen = NativeWorldLandmarks.OnScreen(block.Transform.TransformPoint(new Vector3(point.X - x * 64, point.Y, point.Z + z * 64)));
            if (!_seen)
                return;

            result.Add(new WorldTargetObservation
            {
                Id = new TargetIdentity(TargetKind.WorldLocation, context.LogicalMapKey, id),
                Label = id == WorldBoatLanding.VillageShore ? "Black Mage Village Shore" : "Desert Palace Shore",
                Position = point, Available = true, Visible = true
            });
        }

        internal static bool? ArrivalReady(NavigationContext context, NavigationTarget target, WorldGeometrySnapshot geometry)
        {
            var physical = target.PhysicalIdentity;
            int id = physical.NativeId;
            if (physical.Kind != TargetKind.WorldLocation || !WorldBoatLanding.IsLanding(id))
                return null;
            if (WorldBoatLanding.Destination(context.StoryProgress) != id || !WorldBoatLanding.IsNearBeach(context.Position, id) ||
                WorldTerrainRules.Topography(geometry?.Rules?.CurrentTerrainId ?? -1) != WorldBoatLanding.BoatTerrain)
                return false;

            var engine = UnityEngine.Object.FindObjectOfType<EventEngine>();
            if (!context.CanControl || ff9.w_framePhase != 2 || !IsBlueNarciss(context, engine, geometry))
                return false;
            var map = engine.GetMapVar();
            if (map == null || map.Length <= 37 || map[35] != 0 || map[37] != 0)
                return false;

            var controls = ff9.w_moveCHRControl;
            if (controls == null || controls.Length <= 7)
                return false;
            var foot = controls[0];
            var boat = controls[7];
            if (foot?.limit == null || foot.limit.Length != 2 || foot.flg_gake != 0 || boat == null || boat.radius <= 0)
                return false;
            return WorldBoatLanding.TryGetOff(geometry, context.Position, geometry.Rules.CurrentTerrainId, ff9.w_moveActorPtr.rot[1],
                boat.radius, foot.limit[0], foot.limit[1], ff9.w_movementGroundStatus[0], ff9.w_movementGroundStatus[1]);
        }

        private static bool IsBlueNarciss(NavigationContext context, EventEngine engine, WorldGeometrySnapshot geometry)
        {
            if (context.Transport.Kind != TransportKind.Boat || context.Transport.NativeStatus != 8 ||
                ff9.w_moveCHRControl_No != 7 || ff9.w_frameDisc != 1 || ff9.w_moveActorPtr == null ||
                !ff9.w_blockReady || !ff9.w_frameEventEnable || ff9.w_naviMode == 2 || engine == null || engine.gMode != 3 ||
                geometry == null || !geometry.Complete || geometry.Rules == null || geometry.Rules.ControlMode != 7 || geometry.Rules.ControlType != 2)
                return false;

            int worldMap = FF9StateSystem.Common.FF9.wldMapNo;
            string expected = worldMap == 9003 ? World03BoatInput : worldMap == 9009 ? World09BoatInput : null;
            if (expected == null)
                return false;
            var input = engine.FindObjByUID(3);
            if (input == null || input.sid != 3 || input.ebData == null)
                return false;
            return NativeFieldTargets.Fingerprint(input.ebData) == expected;
        }
    }
}
