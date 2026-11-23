using Memoria.Accessibility.AudioDescriptions;

namespace FF9Access.Tests
{
    internal static class TelescopeGuideTests
    {
        private const uint Up = 16, Right = 32, Down = 64, Left = 128, Confirm = 0x20000;
        private const uint Directions = Up | Right | Down | Left;

        public static void TheNativeExclamationControlsTheConfirmCue()
        {
            var guide = new TelescopeGuide();
            guide.Observe(112, 225, 0, false);
            TestAssert.Equal(0, guide.Marker);
            guide.Observe(112, 225, 0, true);
            TestAssert.Equal(1, guide.Marker);
            guide.Observe(112, 225, 1, true);
            TestAssert.Equal(0, guide.Marker);
            guide.Observe(127, 225, 0, true);
            TestAssert.Equal(0, guide.Marker);
        }

        public static void PanningHoldsAtAnUnvisitedMarkerWithoutConsumingConfirm()
        {
            var guide = new TelescopeGuide();
            guide.Observe(132, 225, 0, false);
            TestAssert.Equal(Left, guide.FilterInput(Left, Directions));
            guide.Observe(126, 225, 0, true);
            TestAssert.Equal(Confirm, guide.FilterInput(Left | Confirm, Directions));
            TestAssert.Equal(Right, guide.FilterInput(Right, Directions));
            TestAssert.Equal(Left, guide.FilterInput(Left, Directions));
            guide.Observe(132, 225, 0, false);
            guide.Observe(126, 225, 0, true);
            TestAssert.Equal(0u, guide.FilterInput(Left, Directions));
            TestAssert.Equal(0u, guide.FilterInput(0, Directions));
            TestAssert.Equal(Left, guide.FilterInput(Left, Directions));
            guide.Suspend();
            TestAssert.Equal(Left, guide.FilterInput(Left, Directions));
        }

        public static void PanDirectionsLeadToTheFourMarkersRemainingAfterTheMountainAndMarsh()
        {
            var guide = new TelescopeGuide();
            int x=112, y=225, visited=5;
            for (int step=0; step<640 && visited!=63; step++)
            {
                int visible=TelescopeViews.Current(x,y,visited);
                guide.Observe(x,y,visited,visible!=0);
                if (guide.Marker!=0)
                {
                    visited |= guide.Marker;
                    guide.Suspend();
                    continue;
                }
                switch (guide.Direction)
                {
                    case TelescopeDirection.Left: x-=3; break;
                    case TelescopeDirection.Right: x+=3; break;
                    case TelescopeDirection.Up: y-=3; break;
                    case TelescopeDirection.Down: y+=3; break;
                    default: throw new System.InvalidOperationException("A remaining landmark has no panning direction.");
                }
            }
            TestAssert.Equal(63, visited);
            TestAssert.Equal(0, TelescopeViews.Remaining(visited));
        }
    }
}
