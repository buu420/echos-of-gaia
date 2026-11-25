using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.Navigation
{
    public struct FieldActorInteractionRange
    {
        public readonly float BodyClearance;
        public readonly float ContactLimit;

        public FieldActorInteractionRange(float bodyClearance, float contactLimit)
        {
            BodyClearance = bodyClearance;
            ContactLimit = contactLimit;
        }
    }

    public static class FieldActorApproaches
    {
        internal const string WreckedCrewCabin = "field:207:FBG_N03_BSHP_MAP070_BT_GTR_0";

        internal static bool IsCabinVivi(NavigationContext context, int uid)
        {
            return uid == 16 && context.LogicalMapKey == WreckedCrewCabin &&
                context.StoryProgress >= 2055 && context.StoryProgress < 2060 && context.PlayerIdentity == 15;
        }

        // Both native rings: the body clearance ring and, inside the strict talk/push
        // range, a farther ring. WalkMesh.Collision selects by XZ distance and the
        // 400 unit height interval only, so a counter, ledge or walkmesh gap between
        // the player and the actor does not stop the ordinary Confirm. OnSurfaces
        // keeps the nearest ring that has floor connected with the player.
        public static NavigationPoint[] Create(NavigationPoint player, NavigationPoint center, float collisionRadius, float talkRadius)
        {
            return Create(player, center, collisionRadius, talkRadius, true);
        }

        public static NavigationPoint[] Create(NavigationPoint player, NavigationPoint center, float collisionRadius, float talkRadius, bool includeOuterTalkRange)
        {
            if (!player.IsFinite || !center.IsFinite || float.IsNaN(collisionRadius) || float.IsNaN(talkRadius)) return new NavigationPoint[0];
            float clearance = Math.Max(0, collisionRadius);
            float radius = clearance + 4;
            if (talkRadius > clearance && radius >= talkRadius) radius = clearance + (talkRadius - clearance) * 0.5f;

            // Leave room inside the strict native talk radius for the final step.
            const int samples = 17;
            float outerRadius = talkRadius - 40;
            bool sampleOuter = includeOuterTalkRange && outerRadius > radius && !float.IsInfinity(outerRadius);
            var points = new NavigationPoint[sampleOuter ? 2 * samples : samples];
            double initial = Math.Atan2(player.Z - center.Z, player.X - center.X);
            for (int i = 0; i < samples; i++)
            {
                double angle = i == 0 ? initial : (i - 1) * Math.PI / 8;
                float x = (float)Math.Cos(angle), z = (float)Math.Sin(angle);
                points[i] = new NavigationPoint(center.X + x * radius, center.Y, center.Z + z * radius);
                if (sampleOuter)
                    points[samples + i] = new NavigationPoint(center.X + x * outerRadius, center.Y, center.Z + z * outerRadius);
            }
            return points;
        }
        public static NavigationPoint[] OnSurfaces(NavigationPoint[] candidates,NavigationPoint actor,FieldGeometrySnapshot geometry)
        {
            return OnSurfaces(candidates,actor,geometry,false);
        }

        internal static NavigationPoint[] OnSurfaces(NavigationPoint[] candidates, NavigationPoint actor,
            FieldGeometrySnapshot geometry, bool preferClearance, bool allClearRings = false)
        {
            var result=new List<NavigationPoint>();var rings=new List<float>();var connected=new List<bool>();
            if(candidates==null || geometry==null)return result.ToArray();
            var reachable=Connected(geometry);
            foreach(var candidate in candidates)foreach(var triangle in geometry.Triangles) {
                if(!candidate.IsFinite || !geometry.Permitted(triangle) || !triangle.Contains(candidate))continue;
                float height=triangle.Height(candidate);
                // Match WalkMesh.Collision's strict vertical talk/body interval.
                if(Math.Abs(height-actor.Y)>=400)continue;
                var point=new NavigationPoint(candidate.X,height,candidate.Z);bool duplicate=false;
                foreach(var old in result)if(NavigationPoint.Distance(old,point)<0.01f){duplicate=true;break;}
                if(duplicate)continue;
                result.Add(point);rings.Add(FieldGeometrySnapshot.Horizontal(candidate,actor));connected.Add(reachable==null || reachable.Contains(triangle.Id));
            }
            // A person behind a counter or on a ledge stands on native floor that shares
            // no edge with the player's floor. The native talk range still crosses that
            // gap, so keep the nearest ring with connected floor. When no ring has any,
            // return every supported point and let the planner report the disconnection.
            if (allClearRings)
            {
                var clear = new List<NavigationPoint>();
                for (int i = 0; i < result.Count; i++)
                    if (connected[i] && geometry.IsSegmentClear(result[i], result[i])) clear.Add(result[i]);
                return clear.ToArray();
            }
            float nearest=float.MaxValue;
            for(int i=0;i<result.Count;i++)if(connected[i] && rings[i]<nearest)nearest=rings[i];
            if(nearest==float.MaxValue)return result.ToArray();
            if(preferClearance) {
                // A connected inner ring can sit entirely against a wall. Use the
                // nearest ring with room to stop at the player's full body radius.
                float nearestClear=float.MaxValue;
                for(int i=0;i<result.Count;i++) {
                    if(connected[i] && rings[i]<nearestClear && geometry.IsSegmentClear(result[i],result[i])) {
                        nearestClear=rings[i];
                        if(nearestClear<=nearest+0.5f)break;
                    }
                }
                if(nearestClear<float.MaxValue)nearest=nearestClear;
            }
            var chosen=new List<NavigationPoint>();
            for(int i=0;i<result.Count;i++)if(connected[i] && rings[i]<=nearest+0.5f)chosen.Add(result[i]);
            return chosen.ToArray();
        }
        internal static NavigationPoint[] Recover(NavigationPoint origin, NavigationTarget target, FieldGeometrySnapshot geometry)
        {
            var range = target.ActorInteractionRange;
            const float margin = 4;
            float near = range.BodyClearance + margin;
            float far = range.ContactLimit - margin;
            if (range.BodyClearance <= 0 || !(far >= near && far < float.PositiveInfinity)) return new NavigationPoint[0];

            // The normal two rings may miss the walkable patch between a wall and
            // the native contact limit. Search the actual interval only after the
            // usual approach fails, and keep the original wall and floor checks.
            const int rings = 5, directions = 72;
            var actor = target.Position;
            var points = new NavigationPoint[rings * (directions + 1)];
            double heading = Math.Atan2(origin.Z - actor.Z, origin.X - actor.X);
            for (int direction = 0; direction <= directions; direction++)
            {
                double angle = direction == 0 ? heading : (direction - 1) * 2 * Math.PI / directions;
                float x = (float)Math.Cos(angle), z = (float)Math.Sin(angle);
                for (int ring = 0; ring < rings; ring++)
                {
                    float radius = near + (far - near) * ring / (rings - 1);
                    points[direction * rings + ring] = new NavigationPoint(actor.X + x * radius, actor.Y, actor.Z + z * radius);
                }
            }
            return OnSurfaces(points, actor, geometry, preferClearance: true, allClearRings: true);
        }

        // Permitted triangles connected with the player's current support through
        // shared edges. Null when that support is unknown, so every ring counts.
        internal static HashSet<int> Connected(FieldGeometrySnapshot geometry)
        {
            var start=geometry.Find(geometry.ActiveTriangle);
            if(!geometry.Permitted(start))return null;
            var reachable=new HashSet<int>{start.Id};var pending=new Queue<FieldTriangle>();pending.Enqueue(start);
            while(pending.Count>0) {
                var triangle=pending.Dequeue();
                foreach(int id in triangle.Neighbors) {
                    var next=geometry.Find(id);
                    if(!geometry.Permitted(next) || !reachable.Add(next.Id))continue;
                    pending.Enqueue(next);
                }
            }
            return reachable;
        }
    }
}
