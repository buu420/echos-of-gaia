using System.Collections.Generic;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class DisplayedChoiceTests
    {
        public static void ChoicesStartingOnTheFirstLineKeepTheirFirstCharacter()
        {
            var phrases=DisplayedChoice.Split("Light the candle\nLook around",0);
            TestAssert.Equal("Light the candle",DisplayedChoice.Read(phrases,0,2,null));
            TestAssert.Equal("Look around",DisplayedChoice.Read(phrases,1,2,null));
            phrases=DisplayedChoice.Split("Ticket holder\nChoose\nPeek into the ticket booth\nLeave",2);
            TestAssert.Equal("Ticket holder\nChoose",phrases[0]);
            TestAssert.Equal("Peek into the ticket booth",DisplayedChoice.Read(phrases,0,2,null));
        }
        private static string[] Phrases(){return new[]{"Do you understand now?","How do you play the card game?","How does the card game proceed?","I think I understand"};}
        public static void MaskedNativeSlotsMapToTheDisplayedRows()
        {
            var disabled=new List<int>{1,2,3,4,5};
            TestAssert.Equal(Phrases()[1],DisplayedChoice.Read(Phrases(),0,8,disabled));
            TestAssert.Equal(Phrases()[2],DisplayedChoice.Read(Phrases(),6,8,disabled));
            TestAssert.Equal(Phrases()[3],DisplayedChoice.Read(Phrases(),7,8,disabled));
        }
        public static void UnmaskedNativeSlotsRetainTheirOrder()
        {
            TestAssert.Equal(Phrases()[1],DisplayedChoice.Read(Phrases(),0,3,new List<int>()));
            TestAssert.Equal(Phrases()[3],DisplayedChoice.Read(Phrases(),2,3,null));
        }
        public static void DisabledOrInvalidSelectionsHaveNoGuessedText()
        {
            var disabled=new List<int>{0,2};
            TestAssert.Equal<string>(null,DisplayedChoice.Read(new[]{"Prompt","Shown"},0,3,disabled));
            TestAssert.Equal("Shown",DisplayedChoice.Read(new[]{"Prompt","Shown"},1,3,disabled));
            TestAssert.Equal<string>(null,DisplayedChoice.Read(Phrases(),-1,3,null));
            TestAssert.Equal<string>(null,DisplayedChoice.Read(Phrases(),3,3,null));
            TestAssert.Equal<string>(null,DisplayedChoice.Read(null,0,1,null));
            TestAssert.Equal<string>(null,DisplayedChoice.Read(new[]{"Prompt"},0,1,null));
        }
        public static void MalformedOrEmptyDisplayedRowsDoNotReadAnotherOption()
        {
            TestAssert.Equal<string>(null,DisplayedChoice.Read(new[]{"Prompt",""},0,1,null));
            TestAssert.Equal<string>(null,DisplayedChoice.Read(Phrases(),7,8,new List<int>{1,1,2,3,4,5}));
        }
    }
}
