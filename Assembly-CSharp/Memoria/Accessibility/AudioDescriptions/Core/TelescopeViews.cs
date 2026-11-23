using System;

namespace Memoria.Accessibility.AudioDescriptions
{
    internal enum TelescopeDirection { None, Left, Right, Up, Down }

    public static class TelescopeViews
    {
        private sealed class View
        {
            public readonly int Mask, Left, Right, Top, Bottom;
            public readonly string Label;

            public View(int mask, string label, int left, int right, int top, int bottom)
            {
                Mask = mask;
                Label = label;
                Left = left;
                Right = right;
                Top = top;
                Bottom = bottom;
            }

            public bool Contains(int x, int y)
            {
                return x >= Left && x <= Right && y >= Top && y <= Bottom;
            }

            public long Distance(int x, int y)
            {
                long dx = Math.Max((long)Left - x, (long)x - Right);
                long dy = Math.Max((long)Top - y, (long)y - Bottom);
                return Math.Max(0, dx) + Math.Max(0, dy);
            }
        }

        // Original 606 director, tag 1. Integer bounds are inclusive here.
        private static readonly View[] Views =
        {
            new View(1, "Marshland", Int32.MinValue, 126, 213, Int32.MaxValue),
            new View(4, "West mountain view", 146, 189, Int32.MinValue, 115),
            new View(8, "East mountain view", 392, Int32.MaxValue, Int32.MinValue, 112),
            new View(2, "Forest", 376, Int32.MaxValue, 150, 217),
            new View(16, "River view", 261, 289, 171, 238),
            new View(32, "South Gate", 281, 319, Int32.MinValue, 114)
        };

        public static int Current(int x, int y, int viewed)
        {
            foreach (View view in Views)
                if ((viewed & view.Mask) == 0 && view.Contains(x, y))
                    return view.Mask;
            return 0;
        }

        public static string Label(int marker)
        {
            foreach (View view in Views)
                if (view.Mask == marker)
                    return view.Label;
            return null;
        }

        internal static int Remaining(int viewed)
        {
            int count = 0;
            foreach (View view in Views)
                if ((viewed & view.Mask) == 0)
                    count++;
            return count;
        }

        internal static int Closest(int x, int y, int viewed)
        {
            long best = Int64.MaxValue;
            int marker = 0;
            foreach (View view in Views)
            {
                if ((viewed & view.Mask) != 0) continue;
                long distance = view.Distance(x, y);
                if (distance >= best) continue;
                best = distance;
                marker = view.Mask;
            }
            return marker;
        }

        internal static TelescopeDirection DirectionTo(int x, int y, int marker)
        {
            foreach (View view in Views)
            {
                if (view.Mask != marker) continue;
                if (x < view.Left) return TelescopeDirection.Right;
                if (x > view.Right) return TelescopeDirection.Left;
                if (y < view.Top) return TelescopeDirection.Down;
                if (y > view.Bottom) return TelescopeDirection.Up;
                break;
            }
            return TelescopeDirection.None;
        }
    }

    internal sealed class TelescopeGuide
    {
        private int _goal;
        private uint _pressedDirections;
        private uint _heldDirections;

        public int Marker { get; private set; }
        public TelescopeDirection Direction { get; private set; }

        public void Observe(int x, int y, int viewed, bool nativePrompt)
        {
            int marker = nativePrompt ? TelescopeViews.Current(x, y, viewed) : 0;
            // Stop a continuous pan at the displayed marker. Releasing the
            // direction or pressing a different one restores ordinary panning.
            if (marker != 0 && marker != Marker)
                _heldDirections = _pressedDirections;
            Marker = marker;
            if (_goal == 0 || (viewed & _goal) != 0)
                _goal = TelescopeViews.Closest(x, y, viewed);
            Direction = marker == 0 ? TelescopeViews.DirectionTo(x, y, _goal) : TelescopeDirection.None;
        }

        public uint FilterInput(uint input, uint directionMask)
        {
            _pressedDirections = input & directionMask;
            if (_pressedDirections != _heldDirections)
                _heldDirections = 0;
            return _heldDirections == 0 ? input : input & ~directionMask;
        }

        public void Suspend()
        {
            Marker = 0;
            Direction = TelescopeDirection.None;
            _pressedDirections = 0;
            _heldDirections = 0;
        }
    }
}
