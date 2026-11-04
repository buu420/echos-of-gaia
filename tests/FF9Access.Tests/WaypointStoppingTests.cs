using System;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class WaypointStoppingTests
    {
        // Original Mono rejects the exact portal landing in four captured
        // street cases. Modern CLR accepts it, so require an interior endpoint.
        public static void ShortOwnedStepsFinishInsideTheExistingStoppingCircle()
        {
            foreach(float remaining in new[]{7.7847233f,8.780993f,4.728697f,24.427288f}) {
                float step=FieldAnalogMovement.StepMagnitude(30,remaining,1,true);
                float gap=remaining-step;
                TestAssert.True(step>0 && gap>0 && gap<FieldAnalogMovement.ArrivalRadius(80,1)*.95f);
            }
        }
        public static void InteriorStoppingUsesTheCurrentNativeRadius()
        {
            foreach(float bodyRadius in new[]{.25f,.5f,1f,2f,80f}) {
                float radius=FieldAnalogMovement.ArrivalRadius(bodyRadius,1),remaining=radius*1.1f;
                float gap=remaining-FieldAnalogMovement.StepMagnitude(30,remaining,1,true,radius);
                TestAssert.True(gap>0 && gap<radius*.95f);
            }
        }
        public static void FarOwnedWalkingAndRunningKeepTheirNativeStep()
        {
            foreach(float proposed in new[]{10f,30f,60f})TestAssert.Near(proposed,FieldAnalogMovement.StepMagnitude(proposed,600,1,true,4),0);
        }
        public static void PhysicalAndUnownedPathsKeepTheirNativeStep()
        {
            TestAssert.Near(30,FieldAnalogMovement.StepMagnitude(30,5,0,true,4),0);
            TestAssert.Near(30,FieldAnalogMovement.StepMagnitude(30,5,1,false,4),0);
        }
        public static void ZeroOrInvalidToleranceCannotCreateAnInvalidStep()
        {
            foreach(float radius in new[]{0f,-1f,float.NaN,float.PositiveInfinity})TestAssert.Near(5,FieldAnalogMovement.StepMagnitude(30,5,1,true,radius),0);
        }
        public static void NearStepNeverPassesTheWaypoint()
        {
            foreach(float remaining in new[]{0f,1f,3.9f,4f,5f,30f}) {
                float step=FieldAnalogMovement.StepMagnitude(30,remaining,1,true,4);
                TestAssert.True(step>=0 && step<=remaining);
            }
        }
    }
}
