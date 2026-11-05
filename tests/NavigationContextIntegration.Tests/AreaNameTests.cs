using Assets.Sources.Scripts.UI.Common;
using Memoria.Accessibility.Navigation;
using Memoria.Assets;
internal static class AreaNameTests
{
    private static void Equal(string expected,string actual){if(expected!=actual)throw new System.Exception("Expected "+expected+", got "+actual);}
    public static void NativeNameCacheSeparatesAreasAndRefreshesAfterLanguageChanges()
    {
        EventEngineUtils.eventIDToFBGID[250]="first";EventEngineUtils.eventIDToFBGID[251]="second";
        FF9TextTool.Names[250]="Evil Forest/Trail";FF9TextTool.Names[251]="Evil Forest/Trail";
        Equal("Evil Forest, Trail, Area 1",NativeFieldAreaNames.Read(250,"first"));
        Equal("Evil Forest, Trail, Area 2",NativeFieldAreaNames.Read(251,"second"));
        Localization.CurrentDisplaySymbol="FR";FF9TextTool.Names[250]="Foret/Piste";FF9TextTool.Names[251]="Foret/Piste";
        Equal("Foret, Piste, Area 2",NativeFieldAreaNames.Read(251,"second"));
        Localization.CurrentDisplaySymbol="US";FF9TextTool.Names.Clear();EventEngineUtils.eventIDToFBGID.Clear();
    }
}
