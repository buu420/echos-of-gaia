namespace Memoria.Accessibility.Navigation
{
    // Reviewed native KEYON listener. Recognition never polls keys or queues a request.
    public sealed class FieldTouchConfirmation
    {
        public const string SourceHash="c1eb918671bea8c3485749a909c6fb5a7aad9ab4959ad267a4cbbb8faf3a9c9f";
        public const string CallbackSourceHash="92bf8a8e6a55f301690f98dda9bb9e94b94ab45b89580d737a9e3166f4bd5ce3";
        public int Field,Uid,Sid,ShownUid,Level,Ip,Depth,Wait,SavedIp,SavedStatus;
        public int CallbackSid,CallbackModel,CallbackLevel;
        public string Hash,CallbackHash;
        public bool CanControl,SelectedTouch,Contains,NativeFacingAllowed,OriginalBytes,Running,CallbackRunning,CallbackHasTag,CallbackOriginalBytes;
        public bool Ready {get {
            if(Field!=2108 || Uid!=6 || Sid!=6 || Hash!=SourceHash || !CanControl || ShownUid!=6 || !SelectedTouch || !Contains || !NativeFacingAllowed || !OriginalBytes || !Running || Wait!=0 ||
                CallbackSid!=4 || CallbackModel!=5478 || CallbackHash!=CallbackSourceHash || !CallbackRunning || !CallbackOriginalBytes || CallbackLevel<=4 || !CallbackHasTag)return false;
            return Level==7 && Ip==-1 && Depth==0 || IsQueuedPush(30);
        }}

        internal bool IsQueuedPush(int entry)
        {
            // The collision pass schedules Push after event code runs. An idle
            // level-7 object then has one async frame on its event stack.
            return entry>=0 && OriginalBytes && Running && Level==1 && Ip==entry &&
                Depth==2 && Wait==0 && SavedIp==-1 && (SavedStatus & 0xffffff)==0xff0700;
        }

        internal bool CanConfirmQueuedPush(int entry, bool rendered, FieldInteractionState guard)
        {
            return CanControl && rendered && ShownUid==Uid && IsQueuedPush(entry) && guard==FieldInteractionState.Available;
        }
    }
}
