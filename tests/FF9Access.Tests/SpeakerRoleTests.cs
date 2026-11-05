using System;
using System.Linq;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    // People are labelled from the speaker heading of their OWN native Talk dialogue (castle zone 3.mes
    // examples). Role headings describe what a sighted player sees; a proper name is used only once the
    // game has displayed it (or it is a known party character). Another actor's heading is never used.
    internal static class SpeakerRoleTests
    {
        private static Func<string,bool> Known(params string[] names){return name=>names.Contains(name);}
        public static void RoleHeadingsLabelUnknownPeople()
        {
            TestAssert.Equal("Alexandrian Soldier",FieldSpeakerRoles.Label(new[]{"Alexandrian Soldier\n“No sign of the princess on\nthis side, sir!”"},Known()));
            TestAssert.Equal("Head Chef",FieldSpeakerRoles.Label(new[]{"Head Chef\n“Now is time you show skill, boys!”","Head Chef\n“Must hurry!!!”"},Known()));
            TestAssert.Equal("Captain of the Knights of Pluto",FieldSpeakerRoles.Label(new[]{"Captain of the Knights of Pluto\n“Grrr!”"},Known()));
        }
        public static void ProperNamesWaitUntilTheGameHasShownThem()
        {
            TestAssert.True(FieldSpeakerRoles.Label(new[]{"Mosh\n“Kupo!”"},Known())==null);
            TestAssert.Equal("Mosh",FieldSpeakerRoles.Label(new[]{"Mosh\n“Kupo!”"},Known("Mosh")));
            TestAssert.True(FieldSpeakerRoles.Label(new[]{"Blutzen, Pluto Knight II\n“Captain, sir!”"},Known())==null);
            TestAssert.Equal("Blutzen, Pluto Knight II",FieldSpeakerRoles.Label(new[]{"Blutzen, Pluto Knight II\n“Captain, sir!”"},Known("Blutzen, Pluto Knight II")));
        }
        public static void PlayerLinesAndAmbiguousSpeakersGiveNoLabel()
        {
            // [ZDNE] headings are removed by the native opcode strip: the player is speaking.
            TestAssert.True(FieldSpeakerRoles.Label(new[]{"\n“Got it!”"},Known())==null);
            TestAssert.Equal("Scholar",FieldSpeakerRoles.Label(new[]{"\n“Hmm...”","Scholar\n“We haven’t seen the princess.”"},Known()));
            TestAssert.True(FieldSpeakerRoles.Label(new[]{"Scholar Ally\n“Hello.”"},Known())==null);
            // Two different non-player speakers in one handler: whose label it is is not proven.
            TestAssert.True(FieldSpeakerRoles.Label(new[]{"Alexandrian Soldier\n“Anything to report?”","Female Soldier\n“No, ma’am!”"},Known())==null);
            // Narration without a speaker heading is not a name.
            TestAssert.True(FieldSpeakerRoles.Label(new[]{"There are gourmet dishes on the table."},Known())==null);
        }
        public static void DisplayedNamesAreKnownAcrossMaps()
        {
            var store=new ObservedIdentityStore();
            TestAssert.False(store.HasName("Blank"));
            store.Record(new TargetIdentity(TargetKind.FieldActor,"field:153:A",7),"Blank","sig");
            TestAssert.True(store.HasName("Blank"));TestAssert.False(store.HasName("blank "));
        }
        public static void PartyCharactersAreKnownByName()
        {
            TestAssert.Equal("Blank",FieldSpeakerRoles.Label(new[]{"Blank\n“Hurry!”"},Known("Blank","Zidane")));
        }
    }
}
