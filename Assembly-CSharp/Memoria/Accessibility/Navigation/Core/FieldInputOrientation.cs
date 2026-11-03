namespace Memoria.Accessibility.Navigation
{
    public enum FieldInputBasis {Unknown,Digital,Stick}
    public static class FieldInputOrientation
    {
        public static FieldInputBasis Observe(FieldInputBasis previous,bool nativeStickMovement,long pathOwner)
        {return pathOwner==0?(nativeStickMovement?FieldInputBasis.Stick:FieldInputBasis.Digital):previous;}
        public static float Resolve(FieldInputBasis basis,bool fallbackStick,bool analog,bool absoluteStick,bool absoluteKeys,float absoluteAngle,float relativeAngle)
        {
            bool stick=basis==FieldInputBasis.Stick || basis==FieldInputBasis.Unknown && fallbackStick;
            return analog && (stick?absoluteStick:absoluteKeys)?absoluteAngle:relativeAngle;
        }
    }
}
