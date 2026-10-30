using System;
using Memoria;
using Memoria.Prime.WinAPI;
using Memoria.ScreenReader;

internal static class QuietPresentationTests
{
    private static void Require(bool condition,string message) { if(!condition)throw new Exception(message); }
    public static void ScreenReaderKeepsTitleStableAcrossMapCameraAndMovie()
    {
        User32.Reset();ScreenReaderManager.Instance.Enabled=true;var window=new PlayerWindow();
        foreach(string debug in new[]{"Map: 100-0 | Index/Counter: 0/1 | Loc: 2","Map: 100-1 | Loc: 2","FMV: mbg101 | Map: 100","World Map: 0, King Ed Plains"})window.SetTitle(debug);
        Require(User32.Title=="FINAL FANTASY IX","debug map/camera/movie details escaped into the Windows title: "+User32.Title);
        Require(User32.Writes==0,"unchanged title emits redundant Windows text-change messages");
    }
    public static void DisabledSpeechPreservesDebugTitleWithoutDuplicateWrites()
    {
        User32.Reset();ScreenReaderManager.Instance.Enabled=false;var window=new PlayerWindow();
        window.SetTitle("Map: 100-0 | Loc: 2");window.SetTitle("Map: 100-0 | Loc: 2");
        Require(User32.Title=="FINAL FANTASY IX - Map: 100-0 | Loc: 2","debug title removed with screen reader disabled");
        Require(User32.Writes==1,"identical debug title written twice");
    }
    public static void EnablingSpeechRestoresStableTitleOnce()
    {
        User32.Reset();ScreenReaderManager.Instance.Enabled=false;var window=new PlayerWindow();window.SetTitle("Map: 100");
        ScreenReaderManager.Instance.Enabled=true;window.SetTitle("Map: 101");window.SetTitle("FMV: mbg101");
        Require(User32.Title=="FINAL FANTASY IX" && User32.Writes==2,"enabling screen-reader speech failed to restore and retain the ordinary title");
    }
    public static void FailedTitleWriteCanRetry()
    {
        User32.Reset();ScreenReaderManager.Instance.Enabled=false;var window=new PlayerWindow();User32.Accept=false;
        window.SetTitle("Map: 100");User32.Accept=true;window.SetTitle("Map: 100");window.SetTitle("Map: 100");
        Require(User32.Writes==2 && User32.Title.EndsWith("Map: 100"),"failed title write was treated as delivered or successful write was repeated");
        ScreenReaderManager.Instance.Enabled=true;
    }
}
