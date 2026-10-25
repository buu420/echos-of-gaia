using System;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class ChocoboHotColdTests
    {
        private static HotColdFrame Frame(double now=0, double seconds=59.99)
        {
            return new HotColdFrame{Generation=1,MapKey="field:2950",Active=true,RoundActive=true,CanSpeak=true,TimerVisible=true,TimerRunning=true,TimerSeconds=seconds,NowMilliseconds=now,Found=0};
        }
        private static NavigationAnnouncement Take(ChocoboHotColdReader reader,double now=0,bool busy=false,string last=null)
        {
            var announcement=reader.TakeSpeech(new SpeechState{NowMilliseconds=now,Busy=busy,LastAnnouncement=last},false,false);
            if(announcement!=null)reader.Acknowledge(announcement,true);return announcement;
        }
        public static void RejectedResultsRemainPendingUntilSpeechAcceptsThem()
        {
            var reader=new ChocoboHotColdReader();reader.Update(Frame());reader.ObserveResult("Found Potion!");
            var rejected=reader.TakeSpeech(new SpeechState(),false,false);reader.Acknowledge(rejected,false);
            TestAssert.True(reader.HasPendingResults);
            reader.Update(Frame(500));TestAssert.Equal("Found Potion!",Take(reader,500).Text);TestAssert.False(reader.HasPendingResults);
        }
        public static void RejectedCryIsNotMarkedSpokenAndCanRetryTheSameShownCry()
        {
            var reader=new ChocoboHotColdReader();reader.Update(Frame());reader.ObserveCue(HotColdCue.Far);
            var rejected=reader.TakeSpeech(new SpeechState{LastAnnouncement="Earlier dialog"},false,false);reader.Acknowledge(rejected,false);
            reader.Update(Frame(500));reader.ObserveCue(HotColdCue.Far);TestAssert.Equal("Far.",Take(reader,500).Text);
            reader.Update(Frame(1000));reader.ObserveCue(HotColdCue.Far);TestAssert.Equal(null,Take(reader,1000));
        }
        public static void AlreadyShownResultsSurviveRoundEndAndWindowGapsUntilSpeechIsIdle()
        {
            var reader=new ChocoboHotColdReader();reader.Update(Frame());reader.ObserveResult("Found Potion!");var first=Take(reader);
            reader.ObserveResult("Total found: 2. Beak LV +1!");TestAssert.True(reader.HasPendingResults);
            TestAssert.Equal(null,Take(reader,100,true,first.Text));
            var ended=Frame(200,0);ended.RoundActive=false;ended.Depth=ended.Found=null;
            // The native adapter keeps only pending result speech alive across a
            // closed window, while ordinary navigation/control becomes available.
            reader.Update(ended);TestAssert.True(reader.HasPendingResults);
            TestAssert.Equal("Total found: 2. Beak LV +1!",Take(reader,1500).Text);TestAssert.False(reader.HasPendingResults);
        }
        public static void StartingANewRoundDropsThePreviousRoundsUnspokenResults()
        {
            var reader=new ChocoboHotColdReader();reader.Update(Frame());
            var ended=Frame(100,0);ended.RoundActive=false;reader.Update(ended);reader.ObserveResult("Total points: 12.");
            TestAssert.True(reader.HasPendingResults);reader.Update(Frame(200));
            TestAssert.False(reader.HasPendingResults);TestAssert.Equal(null,Take(reader,200));
        }
        public static void AlreadyShownResultsWaitForOrdinaryChoiceSpeechThenFinish()
        {
            var reader=new ChocoboHotColdReader();reader.Update(Frame());reader.ObserveResult("Beak LV +1!");reader.ObserveOtherDialog();
            TestAssert.Equal(null,reader.TakeSpeech(new SpeechState{Busy=true,DialogueActive=true,LastAnnouncement="Mene: Play again?"},false,false));
            TestAssert.Equal("Beak LV +1!",reader.TakeSpeech(new SpeechState{NowMilliseconds=1000,DialogueActive=true},false,false).Text);
        }
        public static void QueuedResultsWaitWhileANewDialogIsOpeningOrTyping()
        {
            var reader=new ChocoboHotColdReader();reader.Update(Frame());reader.ObserveResult("Total points: 12.");
            TestAssert.Equal(null,reader.TakeSpeech(new SpeechState{DialogueStarting=true},false,false));TestAssert.True(reader.HasPendingResults);
            TestAssert.Equal(null,reader.TakeSpeech(new SpeechState{NowMilliseconds=500,DialogueActive=true,Busy=true,LastAnnouncement="Mene: Play again?"},false,false));
            var output=reader.TakeSpeech(new SpeechState{NowMilliseconds=1500,DialogueActive=true},false,false);
            TestAssert.Equal("Total points: 12.",output.Text);reader.Acknowledge(output,true);TestAssert.False(reader.HasPendingResults);
        }
        public static void ADelayedFoundCounterDoesNotEraseTheNextDisplayedCry()
        {
            var reader=new ChocoboHotColdReader();reader.Update(Frame());reader.ObserveResult("Found Potion!");Take(reader);
            reader.Update(Frame(350));reader.ObserveCue(HotColdCue.Far);
            var counted=Frame(400);counted.Found=1;reader.Update(counted);
            TestAssert.Equal("Far. Found 1.",Take(reader,400).Text);
        }
        public static void AllEnglishCryVariantsUseTheShownCryMeaning()
        {
            int[] ids={278,279,280,281,282,283,284,285,286,287,288,289,290,291,292,293};
            string[] cries={"Kweh.","Kweh!?","Kwehhh!?","K-KWEHHH!!!"};
            foreach(string locale in new[]{"US","UK"})foreach(int id in ids) {
                int meaning=id<=281?id-278:(id-282)%3;HotColdCue cue;
                TestAssert.Equal(HotColdDialogRole.Cry,HotColdPresentationBindings.Classify(locale,id,"[IMME]"+cries[meaning],cries[meaning],out cue));
                TestAssert.Equal((HotColdCue)(meaning+1),cue);
            }
        }
        public static void ChangedOrUnsupportedTextKeepsTheOrdinaryDialogReader()
        {
            HotColdCue cue;
            TestAssert.Equal(HotColdDialogRole.Other,HotColdPresentationBindings.Classify("US",279,"[IMME]New NPC dialogue","New NPC dialogue",out cue));
            TestAssert.Equal(HotColdDialogRole.Other,HotColdPresentationBindings.Classify("XX",274,"[MPOS=5,30][NUMB=7]","0",out cue));
            TestAssert.Equal(HotColdDialogRole.Other,HotColdPresentationBindings.Classify("US",274,"[NUMB=7]Total found","Total found 8",out cue));
        }
        public static void LocalizedBindingsDistinguishPurchaseNumbersAndCryVariants()
        {
            string[] locales={"JP","FR","GR","IT","ES"};
            string[] depthPositions={"[MPOS=30,57]","[MPOS=78,54]","[MPOS=38,54]","[MPOS=70,54]","[MPOS=79,54]"};
            string[][] cries={new[]{"クェ…","クェ","クェッ!?","クェェッ!!?","ク、クェーッ!!"},new[]{"Kwoa...","Kwoa","Kwoaa ?!","Kwoaaa ?!","Kwo... Kwoaa !!"},new[]{"Krä!","Kräh!","Kräähh!","Käräh!!","Kärääh!!"},new[]{"Kué...","Kué!","Kuéh!","Kuééh??!","K-Kuééh!!"},new[]{"Kue...","Kue.","¡¿Kue?!","¡¡¿Kuee?!!","¡¡Ku... kueee!!"}};
            for(int l=0;l<locales.Length;l++) {
                int found=l==0?274:275;HotColdCue cue;
                TestAssert.Equal(HotColdDialogRole.FoundCount,HotColdPresentationBindings.Classify(locales[l],found,"[MPOS=5,30][NFOC][NUMB=7]","0",out cue));
                TestAssert.Equal(HotColdDialogRole.DepthNumber,HotColdPresentationBindings.Classify(locales[l],found+2,depthPositions[l]+"[NFOC][NUMB=6]","80",out cue));
                for(int offset=0;offset<21;offset++) {
                    int meaning=offset<5?offset:(offset-5)%4;
                    string cry=cries[l][meaning];
                    TestAssert.Equal(HotColdDialogRole.Cry,HotColdPresentationBindings.Classify(locales[l],found+4+offset,"[IMME]"+cry,cry,out cue));
                    TestAssert.Equal(meaning<=1?HotColdCue.NothingNearby:(HotColdCue)meaning,cue);
                }
                TestAssert.Equal(HotColdDialogRole.Other,HotColdPresentationBindings.Classify(locales[l],251,"[NUMB=7]","3",out cue));
            }
        }
        public static void LocatedAndDepthCombineWithoutReadingAnUnlabeledNumber()
        {
            var reader=new ChocoboHotColdReader();var f=Frame();f.Depth=80;reader.Update(f);reader.ObserveCue(HotColdCue.Located);
            TestAssert.Equal("Treasure located; keep digging. Depth 80.",Take(reader).Text);
            TestAssert.Equal(null,Take(reader,50));
        }
        public static void RepeatedPecksStayQuietUntilTheCueChanges()
        {
            var reader=new ChocoboHotColdReader();reader.Update(Frame());reader.ObserveCue(HotColdCue.NothingNearby);
            TestAssert.Equal("Nothing nearby.",Take(reader).Text);
            reader.Update(Frame(100));reader.ObserveCue(HotColdCue.NothingNearby);TestAssert.Equal(null,Take(reader,100));
            reader.Update(Frame(300));reader.ObserveCue(HotColdCue.Close);TestAssert.Equal("Close.",Take(reader,300).Text);
        }
        public static void RapidDepthChangesSpeakOnlyTheLatestDisplayedNumber()
        {
            var reader=new ChocoboHotColdReader();var f=Frame();f.Depth=80;reader.Update(f);var first=Take(reader);
            f=Frame(100);f.Depth=70;reader.Update(f);TestAssert.Equal(null,Take(reader,100,true,first.Text));
            f=Frame(250);f.Depth=50;reader.Update(f);TestAssert.Equal(null,Take(reader,250,true,first.Text));
            TestAssert.Equal("Depth 50.",Take(reader,350).Text);TestAssert.Equal(null,Take(reader,500));
        }
        public static void ResultsKeepItemsFullInventoryPointsAndBeakTextAndClearPendingFeedback()
        {
            var reader=new ChocoboHotColdReader();var f=Frame();f.Depth=10;reader.Update(f);reader.ObserveCue(HotColdCue.Located);
            reader.ObserveResult("Found Potion but you can’t carry any more.");reader.ObserveResult("You get 3 points, kupo!");
            TestAssert.Equal("Found Potion but you can’t carry any more. You get 3 points, kupo!",Take(reader).Text);
            TestAssert.Equal(null,Take(reader,400));
            f=Frame(600,0);f.RoundActive=false;reader.Update(f);reader.ObserveResult("Total found: 2. Total points: 6. Beak LV +1!");
            TestAssert.Equal("Total found: 2. Total points: 6. Beak LV +1!",Take(reader,600).Text);
        }
        public static void CueDoesNotInterruptUnrelatedSpeechAndExpiresWhenStale()
        {
            var reader=new ChocoboHotColdReader();reader.Update(Frame());reader.ObserveCue(HotColdCue.Far);
            TestAssert.Equal(null,Take(reader,0,true,"Mene: Good luck, kupo!"));
            reader.Update(Frame(2500));TestAssert.Equal(null,Take(reader,2500));
        }
        public static void ANewResultAndCryWaitForAnEarlierResultToFinish()
        {
            var reader=new ChocoboHotColdReader();reader.Update(Frame());reader.ObserveResult("Found Potion!");var result=Take(reader);
            reader.Update(Frame(350));reader.ObserveResult("You get 3 points, kupo!");
            TestAssert.Equal(null,Take(reader,350,true,result.Text));
            var points=Take(reader,600);TestAssert.Equal("You get 3 points, kupo!",points.Text);
            reader.Update(Frame(1000));reader.ObserveCue(HotColdCue.Close);TestAssert.Equal(null,Take(reader,1000,true,points.Text));
            TestAssert.Equal("Close.",Take(reader,1300).Text);
        }
        public static void AnUnheardCryCanBeSpokenOnTheNextSameCryAfterAnOverlap()
        {
            var reader=new ChocoboHotColdReader();reader.Update(Frame());reader.ObserveCue(HotColdCue.Far);
            TestAssert.Equal(null,Take(reader,0,true,"Mene: Good luck!"));
            reader.Update(Frame(2500));TestAssert.Equal(null,Take(reader,2500));
            reader.Update(Frame(2600));reader.ObserveCue(HotColdCue.Far);TestAssert.Equal("Far.",Take(reader,2600).Text);
        }
        public static void PassiveSpeechDoesNotInterruptARequestedStatus()
        {
            var reader=new ChocoboHotColdReader();reader.Update(Frame());var status=reader.TakeSpeech(new SpeechState(),true,false);reader.Acknowledge(status,true);
            reader.Update(Frame(500));reader.ObserveCue(HotColdCue.Close);TestAssert.Equal(null,Take(reader,500,true,status.Text));
            TestAssert.Equal("Close.",Take(reader,1000).Text);
        }
        public static void NewestCryReplacesPendingFeedback()
        {
            var reader=new ChocoboHotColdReader();reader.Update(Frame());reader.ObserveCue(HotColdCue.NothingNearby);
            reader.ObserveCue(HotColdCue.Far);reader.ObserveCue(HotColdCue.Close);
            TestAssert.Equal("Close.",Take(reader).Text);
        }
        public static void StatusUsesTheDisplayedTimerRoundingAndCurrentShownNumbers()
        {
            var reader=new ChocoboHotColdReader();var f=Frame(0,59.99);f.Depth=80;f.Found=2;reader.Update(f);reader.ObserveCue(HotColdCue.Located);
            var speech=reader.TakeSpeech(new SpeechState(),true,false);
            TestAssert.Equal("Chocobo Hot and Cold. 59 seconds remaining. Treasure located; keep digging. Depth 80. Found 2.",speech.Text);
            TestAssert.True(speech.Interrupt);
        }
        public static void StatusDoesNotReuseHiddenNumbersOrATimerThatIsNotShown()
        {
            var reader=new ChocoboHotColdReader();var f=Frame();f.Depth=80;f.Found=2;reader.Update(f);Take(reader);
            f=Frame(400);f.Depth=null;f.Found=null;f.TimerVisible=false;reader.Update(f);
            var speech=reader.TakeSpeech(new SpeechState{NowMilliseconds=400},true,false);
            TestAssert.Equal("Chocobo Hot and Cold. Time unavailable. Found count unavailable.",speech.Text);
        }
        public static void TimerWarningsAreSparseAndBonusTimeRearmsThem()
        {
            var reader=new ChocoboHotColdReader();reader.Update(Frame(0,11));reader.Update(Frame(1000,10));
            TestAssert.Equal("10 seconds remaining.",Take(reader,1000).Text);
            reader.Update(Frame(1100,10));TestAssert.Equal(null,Take(reader,1100));
            reader.Update(Frame(6000,5));TestAssert.Equal("5 seconds remaining.",Take(reader,6000).Text);
            reader.Update(Frame(6500,20));TestAssert.Equal(null,Take(reader,6500));
            reader.Update(Frame(16500,10));TestAssert.Equal("10 seconds remaining.",Take(reader,16500).Text);
        }
        public static void TimerJumpSelectsTheUrgentWarningAndIgnoresHiddenStoppedOrInvalidTime()
        {
            var reader=new ChocoboHotColdReader();reader.Update(Frame(0,12));reader.Update(Frame(1000,4));
            TestAssert.Equal("4 seconds remaining.",Take(reader,1000).Text);
            foreach(double time in new[]{double.NaN,double.PositiveInfinity,-1d}) {
                var f=Frame(2000,time);reader.Update(f);TestAssert.Equal(null,Take(reader,2000));
            }
            var hidden=Frame(3000,2);hidden.TimerVisible=false;reader.Update(hidden);TestAssert.Equal(null,Take(reader,3000));
            var stopped=Frame(4000,1);stopped.TimerRunning=false;reader.Update(stopped);TestAssert.Equal(null,Take(reader,4000));
        }
        public static void PauseBackgroundAndSceneChangesDropPendingSpeech()
        {
            var reader=new ChocoboHotColdReader();reader.Update(Frame());reader.ObserveCue(HotColdCue.Close);
            var paused=Frame(100);paused.CanSpeak=false;reader.Update(paused);reader.ObserveResult("Found Potion!");
            TestAssert.Equal(null,reader.TakeSpeech(new SpeechState(),true,true));
            reader.Update(Frame(200));TestAssert.Equal(null,Take(reader,200));
            reader.ObserveCue(HotColdCue.Far);var changed=Frame(300);changed.Generation=2;changed.MapKey="field:2951";reader.Update(changed);
            TestAssert.Equal(null,Take(reader,300));
        }
        public static void NewRoundDoesNotRetainLastTreasureOrFoundCount()
        {
            var reader=new ChocoboHotColdReader();var f=Frame();f.Depth=80;f.Found=3;reader.Update(f);reader.ObserveCue(HotColdCue.Located);Take(reader);
            f=Frame(100);f.RoundActive=false;f.Depth=null;f.Found=null;reader.Update(f);
            reader.Update(Frame(200));var status=reader.TakeSpeech(new SpeechState{NowMilliseconds=200},true,false);
            TestAssert.Equal("Chocobo Hot and Cold. 59 seconds remaining. Found 0.",status.Text);
        }
        public static void RepeatUsesTheLastAcceptedAnnouncementAndOtherDialogsClearPendingCues()
        {
            var reader=new ChocoboHotColdReader();reader.Update(Frame());reader.ObserveCue(HotColdCue.Close);reader.ObserveOtherDialog();
            TestAssert.Equal(null,Take(reader));
            TestAssert.Equal("Mene: Choose an option.",reader.TakeSpeech(new SpeechState{LastAnnouncement="Mene: Choose an option."},false,true).Text);
        }
    }
}
