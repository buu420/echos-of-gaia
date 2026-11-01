namespace Memoria.Accessibility.Navigation
{
    // Read the original event entry, never Obj.currentByte: an additional command has a
    // different address space and cannot be indexed with the original talk-tag offset.
    public static class FieldTalkScript
    {
        public static bool IsTalkable(byte[] script,int returnOpcode)
        {
            if(script==null || script.Length<2)return false;
            int count=script[1],headerEnd=2+count*4;
            if(headerEnd>script.Length)return false;
            for(int i=0;i<count;i++) {
                int entry=2+i*4,tag=script[entry]|(script[entry+1]<<8);
                if(tag!=3)continue;
                int start=2+(script[entry+2]|(script[entry+3]<<8));
                if(start<headerEnd || start>script.Length-8)return false;
                return script[start+7]!=returnOpcode || start<=script.Length-9 && script[start+8]!=returnOpcode;
            }
            return false;
        }
    }
}
