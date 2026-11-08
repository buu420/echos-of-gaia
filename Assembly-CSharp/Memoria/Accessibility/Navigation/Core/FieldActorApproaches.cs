using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.Navigation
{
    public static class FieldActorApproaches
    {
        internal const string WreckedCrewCabin = "field:207:FBG_N03_BSHP_MAP070_BT_GTR_0";

        internal static bool IsCabinVivi(NavigationContext context, int uid)
        {
            return uid == 16 && context.LogicalMapKey == WreckedCrewCabin &&
                context.StoryProgress >= 2055 && context.StoryProgress < 2060 && context.PlayerIdentity == 15;
        }

        public static NavigationPoint[] Create(NavigationPoint player, NavigationPoint center, float collisionRadius, float talkRadius)
        {
            return Create(player, center, collisionRadius, talkRadius, false);
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
            var result=new List<NavigationPoint>();
            if(candidates==null || geometry==null)return result.ToArray();
            foreach(var candidate in candidates)foreach(var triangle in geometry.Triangles) {
                if(!candidate.IsFinite || !geometry.Permitted(triangle) || !triangle.Contains(candidate))continue;
                float height=triangle.Height(candidate);
                // Match WalkMesh.Collision's strict vertical talk/body interval.
                if(Math.Abs(height-actor.Y)>=400)continue;
                var point=new NavigationPoint(candidate.X,height,candidate.Z);bool duplicate=false;
                foreach(var old in result)if(NavigationPoint.Distance(old,point)<0.01f){duplicate=true;break;}
                if(!duplicate)result.Add(point);
            }
            return result.ToArray();
        }
    }
}
