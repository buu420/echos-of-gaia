namespace Memoria.Accessibility.Navigation
{
    public enum FieldExitAdmission { None, Walk, Service }
    // Whether a region whose map jump runs in a requested entry is presented as an exit now, from the
    // proof of the path that runs now. A reviewed service menu (air cab, trolley, lift) is a Service;
    // a plain walk-out (no dialogue on the current path) or a declinable prompt is a Walk exit.
    // Anything unproven, leading nowhere now, or writing the scenario counter (a Story scene) is not.
    public static class FieldExitAdmissions
    {
        public static FieldExitAdmission Admit(string function,FieldExitPathEvidence evidence,bool reviewedConfirm=false)
        {
            if(evidence==null || evidence.StoryCounter || evidence.Destinations.Count==0)return FieldExitAdmission.None;
            if(evidence.Kind==FieldExitPathKind.Unknown || evidence.Kind==FieldExitPathKind.NoJump)return FieldExitAdmission.None;
            if(function!=null)return FieldExitAdmission.Service;
            if(evidence.Kind==FieldExitPathKind.ChoiceSingle)return FieldExitAdmission.Walk;
            if(reviewedConfirm && (evidence.Kind==FieldExitPathKind.Determined || evidence.Kind==FieldExitPathKind.ChoiceMenu && evidence.Destinations.Count==1))
                return FieldExitAdmission.Walk;
            return evidence.Kind==FieldExitPathKind.Determined && !evidence.Dialogue?FieldExitAdmission.Walk:FieldExitAdmission.None;
        }
    }
}
