using System;
using System.Collections.Generic;
using Assets.Sources.Scripts.UI.Common;
using Memoria.Assets;
namespace Memoria.Accessibility.Navigation
{
    public static class NativeFieldAreaNames
    {
        private static object _nativeNames;
        private static int _count=-1,_dual=-1;
        private static string _language;
        private static Dictionary<int,string> _labels=new Dictionary<int,string>(),_sources=new Dictionary<int,string>();
        public static string Read(int map,string scene)
        {
            string name=FF9TextTool.RemoveOpCode(FF9TextTool.LocationName(map));
            if(String.IsNullOrEmpty(name))return name;
            string expected;
            if(!EventEngineUtils.eventIDToFBGID.TryGetValue(map,out expected) || expected!=scene)return name;
            var names=FF9TextTool.DisplayBatch.locationName;
            string previous;
            if(!ReferenceEquals(_nativeNames,names) || _count!=names.Count || _language!=Localization.CurrentDisplaySymbol ||
                _dual!=Configuration.Lang.DualLanguageMode || !_sources.TryGetValue(map,out previous) || previous!=name) {
                var entries=new List<FieldAreaName>();_sources.Clear();
                foreach(var native in EventEngineUtils.eventIDToFBGID) {
                    string label=FF9TextTool.RemoveOpCode(FF9TextTool.LocationName(native.Key));
                    if(!String.IsNullOrEmpty(label)){entries.Add(new FieldAreaName{Field=native.Key,Scene=native.Value,Name=label});_sources[native.Key]=label;}
                }
                _labels=FieldAreaNames.Build(entries);_nativeNames=names;_count=names.Count;_language=Localization.CurrentDisplaySymbol;_dual=Configuration.Lang.DualLanguageMode;
            }
            string result;return _labels.TryGetValue(map,out result)?result:name;
        }
        // The numbered native location name of a destination field (current text language), or null.
        public static string Destination(int field)
        {
            string scene;if(!EventEngineUtils.eventIDToFBGID.TryGetValue(field,out scene))return null;
            string name=Read(field,scene);return String.IsNullOrEmpty(name)?null:name;
        }
        internal static string Screen(int field,int entrance)
        {
            if(FieldAreaNames.SpiralArea(field,entrance)==0)return null;
            return FieldAreaNames.SpiralName(field,entrance,FF9TextTool.RemoveOpCode(FF9TextTool.LocationName(field)));
        }
    }
}
