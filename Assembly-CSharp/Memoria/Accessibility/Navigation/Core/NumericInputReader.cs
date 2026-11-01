using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
namespace Memoria.Accessibility.Navigation
{
    public sealed class NumericInputObservation
    {
        public int Field,TextId,WindowIdentity,SelectedVariable=-1;
        public long Generation,OutputGeneration;
        public bool Visible,RenderReady,Foreground,IsOverlay;
        public string Template,RenderedText;public IDictionary<int,int> Values;
    }
    public sealed class NumericInputAnnouncement
    {public string Text;public bool Initial;internal string Key,Window,Item;internal int Amount,Remaining;}
    public sealed class NumericInputReader
    {
        private string _accepted,_window,_pending,_item;private int _amount,_remaining;
        public static bool Supports(int field,int textId,string template)
        {
            string plain=Regex.Replace(template??"",@"\[(?!NUMB=)[^\]]*\]","");
            if((field==909 || field==1909) && textId==201)return plain.Contains("Bid Status") && plain.Contains("Funds: [NUMB=4] Gil") && plain.Contains("Price: [NUMB=1] Gil");
            if((field==909 || field==1909) && textId==203)return plain.Contains("Bid:") && plain.Contains("[NUMB=5][NUMB=6][NUMB=7]00 Gil");
            return field==2800 && textId==105 && plain.Contains("[NUMB=6][NUMB=7]") && plain.Contains("[NUMB=5]");
        }
        public NumericInputAnnouncement Observe(NumericInputObservation o)
        {
            if(o==null || !o.Visible || !o.RenderReady || !o.Foreground || o.IsOverlay || String.IsNullOrEmpty(o.RenderedText) || !Supports(o.Field,o.TextId,o.Template) || o.Values==null){_pending=null;return null;}
            bool status=o.TextId==201,bid=o.Field!=2800;int a=0,b=0,c=0,amount,remaining;string item=null;
            if(status) {
                if(!o.Values.TryGetValue(1,out amount) || !o.Values.TryGetValue(4,out remaining) || amount<0 || remaining<0){_pending=null;return null;}
                var match=Regex.Match(Regex.Replace(o.RenderedText,@"\[[^\]]*\]",""),@"(?:^|\n)\s*Item:\s*([^\r\n]+)");
                if(!match.Success){_pending=null;return null;}item=match.Groups[1].Value.Trim();
            }else {
                if(!o.Values.TryGetValue(5,out a) || !o.Values.TryGetValue(6,out b) || !o.Values.TryGetValue(7,out c) || b<0 || b>9 || c<0 || c>9 || a<0 || bid && a>9){_pending=null;return null;}
                amount=bid?(a*100+b*10+c)*100:b*10+c;remaining=bid?-1:a;
            }
            string window=o.Generation+":"+o.OutputGeneration+":"+o.WindowIdentity+":"+o.Field+":"+o.TextId;
            string key=window+":"+amount+":"+remaining+":"+(status?-1:o.SelectedVariable)+":"+item;_pending=key;if(key==_accepted)return null;
            string digit=null;
            if(status)digit=null;
            else if(bid)digit=o.SelectedVariable==5?"Ten-thousands digit "+a:o.SelectedVariable==6?"Thousands digit "+b:o.SelectedVariable==7?"Hundreds digit "+c:null;
            else digit=o.SelectedVariable==6?"Tens digit "+b:o.SelectedVariable==7?"Units digit "+c:null;
            string text;
            if(status)text=(window!=_window || item!=_item?item+". ":"")+"Current price "+amount+" gil. Funds "+remaining+" gil.";
            else if(window==_window && amount==_amount && remaining==_remaining && digit!=null)text=digit+".";
            else text=(bid?"Bid "+amount+" gil.":"Amount "+amount+". "+remaining+" remaining.")+(digit==null?"":" "+digit+".");
            return new NumericInputAnnouncement{Text=text,Initial=window!=_window,Key=key,Window=window,Item=item,Amount=amount,Remaining=remaining};
        }
        public void Acknowledge(NumericInputAnnouncement announcement,bool accepted)
        {if(!accepted || announcement==null || announcement.Key!=_pending)return;_accepted=announcement.Key;_window=announcement.Window;_item=announcement.Item;_amount=announcement.Amount;_remaining=announcement.Remaining;}
    }
}
