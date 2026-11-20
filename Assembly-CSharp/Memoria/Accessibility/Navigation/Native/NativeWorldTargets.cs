using System;
using System.Collections.Generic;
using UnityEngine;
using Assets.Sources.Scripts.UI.Common;

namespace Memoria.Accessibility.Navigation
{
    public static class NativeWorldTargets
    {
        public static IList<WorldTargetObservation> Read(NavigationContext context, WorldGeometrySnapshot geometry)
        {
            var result = new List<WorldTargetObservation>();
            if (context == null || !context.LogicalMapKey.StartsWith("world:"))
                return result;

            var engine = UnityEngine.Object.FindObjectOfType<EventEngine>();
            int map = WMUIData.ActiveMapNo;
            var positions = ff9.w_naviLocationPos;
            var text = FF9TextTool.GetTableText(0u);
            bool ground = context.Transport.Kind == TransportKind.Foot || context.Transport.Kind == TransportKind.Chocobo;
            if (ground)
                NativeWorldEntrances.Read(engine, context);
            if (positions != null && map >= 0 && map < positions.GetLength(0))
            {
                for (int id = 0; id < positions.GetLength(1) && id < 64; id++)
                {
                    var position = positions[map, id];
                    if ((position.vx | position.vy) == 0 || (position.tx | position.ty) == 0)
                        continue;

                    int[] entrances = ground ? NativeWorldEntrances.OpenTags(id + 1) : null;
                    bool marked = WMUIData.LocationAvailable(id);
                    if (!marked && (entrances == null || entrances.Length == 0))
                        continue;
                    var point = new NavigationPoint(ff9.S(position.tx), 0, ff9.S(position.ty));
                    string label = WorldLabel(text, id);
                    if (string.IsNullOrEmpty(label))
                        continue;
                    result.Add(new WorldTargetObservation
                    {
                        Id = new TargetIdentity(TargetKind.WorldLocation, context.LogicalMapKey, id),
                        Label = DescribeEntrance(label, id), Position = point, Available = true,
                        Marked = marked, NativeLocationId = id, WorldEntranceTags = entrances
                    });
                }
            }
            NativeWorldLandmarks.Append(result, context, engine, text);
            NativeWorldLandings.Append(result, context, engine, geometry);
            if (engine == null)
                return result;

            var player = engine.GetControlChar() as Actor;
            for (var list = engine.GetActiveObjList(); list != null; list = list.next)
            {
                var actor = list.obj as Actor;
                if (actor == null || actor.index < 3 || actor.index > 10 || actor.uid == context.PlayerIdentity || actor.wmActor == null)
                    continue;
                var vehicle = actor.wmActor;
                bool marked = (vehicle == ff9.w_movePlanePtr && WMUIData.PlaneIsAvailable) ||
                    (vehicle == ff9.w_moveChocoboPtr && WMUIData.ChocoboIsAvailable);
                bool visible = engine.objIsVisible(actor) && actor.isEnableRenderer && actor.go != null &&
                    actor.go.activeInHierarchy && NativeWorldLandmarks.OnScreen(vehicle.transform.position);
                if (!marked && !visible)
                    continue;

                var position = vehicle.RealPosition;
                var point = new NavigationPoint(position.x, position.y, position.z);
                string label = actor.index == 8 ? "Blue Narciss" : actor.index == 9 ? "Hilda Garde III" :
                    actor.index == 10 ? "Invincible" : "Chocobo";
                float radius = player == null ? 0 : (4 * actor.talkRad + 4 * player.talkRad + actor.speed + 60) / 256f * 0.75f;
                var approaches = new List<NavigationPoint> { point };
                for (int i = 0; i < 8 && radius > 0; i++)
                {
                    double angle = i * Math.PI / 4;
                    approaches.Add(WorldCoordinates.Canonical(new NavigationPoint(point.X + (float)Math.Sin(angle) * radius,
                        point.Y, point.Z + (float)Math.Cos(angle) * radius)));
                }
                result.Add(new WorldTargetObservation
                {
                    Id = new TargetIdentity(TargetKind.Vehicle, context.LogicalMapKey, actor.uid), Label = label,
                    Position = point, ApproachCandidates = approaches.ToArray(), Available = true,
                    Visible = visible, Marked = marked, NativeIndex = actor.index
                });
            }
            return result;
        }

        internal static string WorldLabel(string[] text, int id)
        {
            int textId = id == 63 ? 49 : id;
            return text != null && textId + 1 < text.Length ? text[textId + 1] : null;
        }

        private static string DescribeEntrance(string label, int id)
        {
            switch ((WorldPlace)(id + 1))
            {
                case WorldPlace.IceCavern_Bottom: return label + ", Lower Entrance";
                case WorldPlace.IceCavern_Top: return label + ", Upper Entrance";
                case WorldPlace.NorthGate_East: return label + ", East Entrance";
                case WorldPlace.NorthGate_West: return label + ", West Entrance";
                case WorldPlace.SouthGate_NorthBottom: return label + ", North Side Lower Entrance";
                case WorldPlace.SouthGate_NorthEast: return label + ", Northeast Entrance";
                case WorldPlace.SouthGate_NorthWest: return label + ", Northwest Entrance";
                case WorldPlace.SouthGate_SouthTop: return label + ", South Side Upper Entrance";
                case WorldPlace.SouthGate_SouthBottom: return label + ", South Side Lower Entrance";
                case WorldPlace.GizamalukeGrotto_North: return label + ", North Entrance";
                case WorldPlace.GizamalukeGrotto_South: return label + ", South Entrance";
                case WorldPlace.GizamalukeGrotto_Top: return label + ", Upper Entrance";
                case WorldPlace.MountainPath_North: return label + ", Madain Sari Side";
                case WorldPlace.MountainPath_West: return label + ", Iifa Tree Side";
                case WorldPlace.QuMarsh_Mist: return label + ", Mist Continent";
                case WorldPlace.QuMarsh_Outer: return label + ", Outer Continent";
                case WorldPlace.QuMarsh_Archipelago: return label + ", Salvage Archipelago";
                case WorldPlace.QuMarsh_Forgotten: return label + ", Forgotten Continent";
                default: return label;
            }
        }
    }
}
