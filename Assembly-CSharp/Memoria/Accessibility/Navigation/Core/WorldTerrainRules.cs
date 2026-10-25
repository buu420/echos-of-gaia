namespace Memoria.Accessibility.Navigation
{
    public static class WorldTerrainRules
    {
        public static int Topography(int id) { return (id & 0xfc) >> 2; }
        public static bool InMask(uint high, uint low, int id) { int bit=Topography(id); return bit>=32 ? ((high>>(bit-32))&1)!=0 : ((low>>bit)&1)!=0; }
        public static int Remap(TransportRules rules,int id) { return (rules.ControlMode==2 || rules.ControlMode==3) && (id==56 || id==57) ? 54 : id; }
        public static bool CanTraverse(TransportRules rules,WorldSurface from,WorldSurface to)
        {
            if(rules==null || from==null || to==null)return false;
            int id=Remap(rules,to.TerrainId),previous=Remap(rules,from.TerrainId);
            if(!InMask(rules.LimitHigh,rules.LimitLow,id))return false;
            if(rules.PreventGroundWaterCrossing) {
                bool nextWater=InMask(0x23ED0000,0,id),nextGround=InMask(0x5C126670,0x18FF3CFF,id);
                bool oldWater=InMask(0x23ED0000,0,previous),oldGround=InMask(0x5C126670,0x18FF3CFF,previous);
                if(nextGround && oldWater || oldGround && nextWater)return false;
            }
            return true;
        }
    }
}
