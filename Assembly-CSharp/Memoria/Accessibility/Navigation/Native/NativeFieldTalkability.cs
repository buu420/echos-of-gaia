namespace Memoria.Accessibility.Navigation
{
    public static class NativeFieldTalkability
    {
        public static bool IsTalkable(Obj obj)
        { return FieldTalkScript.IsTalkable(obj==null?null:obj.ebData,(int)EBin.event_code_binary.rsv04); }
    }
}
