using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class FieldInputOrientationTests
    {
        public static void ReleasingTheStickRetainsItsNativeOrientation()
        {
            var basis=FieldInputOrientation.Observe(FieldInputBasis.Unknown,true,0);
            TestAssert.Equal(90f,FieldInputOrientation.Resolve(basis,false,true,true,false,90,0));
        }
        public static void ActualDigitalInputWinsOverASimultaneouslyActiveStick()
        {
            var basis=FieldInputOrientation.Observe(FieldInputBasis.Stick,false,0);
            TestAssert.Equal(0f,FieldInputOrientation.Resolve(basis,true,true,true,false,90,0));
        }
        public static void CameraControlAnglesRemainCurrentWhileTheInputKindIsRetained()
        {
            TestAssert.Equal(45f,FieldInputOrientation.Resolve(FieldInputBasis.Digital,false,true,true,false,90,45));
            TestAssert.Equal(90f,FieldInputOrientation.Resolve(FieldInputBasis.Stick,false,true,true,false,90,45));
            TestAssert.Equal(45f,FieldInputOrientation.Resolve(FieldInputBasis.Stick,false,false,true,false,90,45));
        }
        public static void OwnedAutomaticWalkingDoesNotReplaceTheHumanInputKind()
        {TestAssert.Equal(FieldInputBasis.Digital,FieldInputOrientation.Observe(FieldInputBasis.Digital,true,12));}
    }
}
