namespace Memoria.Accessibility.AudioDescriptions
{
    public static class NativeTimedInputContext
    {
        public static bool Active {get{
            try {
            int map=FF9StateSystem.Common.FF9.fldMapNo;if(map!=64 && map!=2204 && map!=2554)return false;
            var state=UIManager.Instance.State;if(state!=UIManager.UIState.FieldHUD && state!=UIManager.UIState.Pause)return false;
            var engine=PersistenSingleton<EventEngine>.Instance;
            if(map==2554)return NativeEarthShrineCue.IsTrapScene(engine);
            if(map==2204)return NativeCidEscapeCue.IsKeySequence(engine);
            return engine!=null && engine.eBin!=null && TimedPromptPhase.IsChanbara(map,engine.gMode,engine.eBin.getVarManually(728),engine.eBin.getVarManually(6357));
            }catch {return false;} // Field buffers may be replaced at a scene boundary.
        }}
    }
}
