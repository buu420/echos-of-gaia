using System;
using System.Collections.Generic;
using Memoria.Accessibility.Navigation;

namespace Memoria.Accessibility.AudioDescriptions
{
    internal sealed class PandemoniumLightCue
    {
        private static readonly string[] Directions =
        {
            "right", "up right", "up", "up left", "left", "down left", "down", "down right", "nearby"
        };

        private int _lastSpokenLights;

        internal void Reset()
        {
            _lastSpokenLights = 0;
        }

        internal void Observe(int visibleLights, int directionMask, Func<string, bool> speak)
        {
            if (visibleLights == _lastSpokenLights)
                return;

            if (visibleLights == 0)
            {
                if (speak("No lights in view"))
                    Reset();
                return;
            }

            var directions = new List<string>();
            for (int i = 0; i < Directions.Length; i++)
            {
                if ((directionMask & (1 << i)) != 0)
                    directions.Add(Directions[i]);
            }

            if (directions.Count > 0 && speak("Lights " + String.Join(", ", directions.ToArray())))
                _lastSpokenLights = visibleLights;
        }

        internal static int Direction(NavigationPoint light, NavigationPoint player, float viewportAspect)
        {
            if (!InView(light) || !InView(player) || viewportAspect <= 0 || Single.IsNaN(viewportAspect) || Single.IsInfinity(viewportAspect))
                return -1;

            float x = (light.X - player.X) * viewportAspect;
            float y = light.Y - player.Y;
            if (x * x + y * y < .0016f)
                return 8;

            int octant = (int)Math.Floor(Math.Atan2(y, x) / (Math.PI / 4d) + .5d);
            return (octant + 8) % 8;
        }

        internal static bool InView(NavigationPoint point)
        {
            return point.IsFinite && point.Z > 0 && point.X >= 0 && point.X <= 1 && point.Y >= 0 && point.Y <= 1;
        }
    }
}
