namespace Memoria.Accessibility.Navigation
{
    public static class FieldExitLabel
    {
        public static string Read(NavigationPoint viewport)
        {
            if(!viewport.IsFinite || viewport.Z<=0 || viewport.X<0 || viewport.X>1 || viewport.Y<0 || viewport.Y>1)return "Exit";
            string vertical=viewport.Y>2f/3?"Upper":viewport.Y<1f/3?"Lower":null;
            string horizontal=viewport.X<1f/3?"left":viewport.X>2f/3?"right":null;
            return vertical!=null?(vertical+(horizontal==null?"":"-"+horizontal)+" exit"):horizontal==null?"Exit":horizontal=="left"?"Left exit":"Right exit";
        }
        // The screen side of a Read label as a phrase ("Upper-left exit" -> "upper left"), or null
        // for the centre "Exit" and any other label.
        public static string Phrase(string label)
        {
            if(label==null || !label.EndsWith(" exit") || label.Length<=5)return null;
            string side=label.Substring(0,label.Length-5).Replace('-',' ').ToLower();
            switch(side){case "upper":case "lower":case "left":case "right":case "upper left":case "upper right":case "lower left":case "lower right":return side;default:return null;}
        }
    }
}
