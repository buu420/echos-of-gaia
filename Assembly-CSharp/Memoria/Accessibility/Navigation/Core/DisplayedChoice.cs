using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.Navigation
{
    // Native widgets retain logical slots, including disabled null entries.
    // DialogBoxSymbols removes those entries from the displayed text.
    public static class DisplayedChoice
    {
        public static string[] Split(string text,int startRow)
        {
            if(text==null)return new string[0];
            if(startRow<0)return new[]{text};
            int newline=-1;
            for(int row=0;row<startRow;row++){newline=text.IndexOf('\n',newline+1);if(newline<0)return new[]{text};}
            var phrases=new List<string>();phrases.Add(newline<0?"":text.Substring(0,newline));
            int next=text.IndexOf('\n',newline+1);
            while(next>=0){phrases.Add(text.Substring(newline+1,next-newline-1));newline=next;next=text.IndexOf('\n',newline+1);}
            phrases.Add(text.Substring(newline+1));return phrases.ToArray();
        }
        public static string Read(string[] phrases,int selected,int slotCount,IList<int> disabled)
        {
            if(phrases==null || selected<0 || selected>=slotCount)return null;
            var hidden=new HashSet<int>();
            if(disabled!=null)foreach(int slot in disabled)
                if(slot<0 || slot>=slotCount || !hidden.Add(slot))return null;
            if(hidden.Contains(selected))return null;
            int index=1;for(int slot=0;slot<selected;slot++)if(!hidden.Contains(slot))index++;
            return index<phrases.Length && !String.IsNullOrEmpty(phrases[index])?phrases[index]:null;
        }
    }
}
