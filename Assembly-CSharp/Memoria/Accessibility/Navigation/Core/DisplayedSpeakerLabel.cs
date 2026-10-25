using System;
namespace Memoria.Accessibility.Navigation
{
    public static class DisplayedSpeakerLabel
    {
        public static bool TryRead(string displayed,out string label)
        {
            label=null;if(String.IsNullOrEmpty(displayed))return false;
            int newline=displayed.IndexOf('\n');if(newline<=0)return false;
            string name=displayed.Substring(0,newline).Trim(),words=displayed.Substring(newline+1).TrimStart();
            if(name.Length==0 || name.Length>60 || words.Length==0 || name.IndexOf(':')>=0)return false;
            char opening=words[0];
            if(opening!='“' && opening!='"' && opening!='「' && opening!='『' && opening!='«' && opening!='„')return false;
            label=name;return true;
        }
    }
}
