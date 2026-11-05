namespace Memoria.Accessibility.AudioDescriptions
{
    public static class TelescopeViews
    {
        // Native 606 director, tag 1. Use the same open bounds as the game.
        // Return the visual view, not the dialogue or an undiscovered reward.
        public static int Current(int x,int y,int viewed)
        {
            if(x<127 && y>212 && (viewed&1)==0)return 1;
            if(y<116 && x>145 && x<190 && (viewed&4)==0)return 4;
            if(x>391 && y<113 && (viewed&8)==0)return 8;
            if(x>375 && y>149 && y<218 && (viewed&2)==0)return 2;
            if(x>260 && x<290 && y>170 && y<239 && (viewed&16)==0)return 16;
            if(y<115 && x>280 && x<320 && (viewed&32)==0)return 32;
            return 0;
        }
        public static string Label(int view)
        {
            switch(view) {
                case 1:return "Marshland";
                case 4:return "West mountain view";
                case 8:return "East mountain view";
                case 2:return "Forest";
                case 16:return "Harbor";
                case 32:return "South Gate";
                default:return null;
            }
        }
    }
}
