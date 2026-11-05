using System;
using System.Linq;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    // Reviewed role headings from the installed own/requested speech of field people (counter staff,
    // workers, guards). A heading is used only when it is a descriptive role; personal names, titles,
    // relations and story identities wait for the game to introduce them on that same actor.
    internal static class ObjectRoleTests
    {
        private static string Label(string heading,string language="US"){return FieldSpeakerRoles.Label(new[]{heading+"\n“Welcome.”"},name=>false,language);}
        public static void CounterStaffAndWorkerRolesLabelUnknownPeople()
        {
            foreach(string role in new[]{"Innkeeper","Shopkeeper","Card Seller","Lazy Carpenter","Scientist","Weaponsmith","Synthesist","Librarian","Sales Clerk","Ticketmaster","Boatman","Engineer","Chief Engineer","Burmecian Soldier","Gatekeeper","Carpenter’s Apprentice"})
                TestAssert.Equal(role,Label(role));
        }
        public static void NamesTitlesAndRelationsStayUnintroduced()
        {
            foreach(string heading in new[]{"Grandma Pickle","Wendy Grocer","Queen Stella","Dragoos","Hippolady","Wayne","Alice","Ashley","Mayor’s Daughter","Synthesist’s Wife","Tom’s Mother","Qu Clan Member","Mysterious Girl","Innkeeper Hal","Carpenter Hans","Shopkeeper Eve"})
                TestAssert.True(Label(heading)==null);
        }
        public static void GenericPersonHeadingsKeepTheRicherReviewedAppearance()
        {
            TestAssert.Equal("Man in a flat cap",FieldSpeakerRoles.Prefer("Man","Man in a flat cap"));
            TestAssert.Equal("Elderly man with a cane",FieldSpeakerRoles.Prefer("Old Man","Elderly man with a cane"));
            TestAssert.Equal("Child with pigtails",FieldSpeakerRoles.Prefer("Girl","Child with pigtails"));
            TestAssert.Equal("Man",FieldSpeakerRoles.Prefer("Man",null));
            // Specific roles and introduced names say more than an outfit.
            TestAssert.Equal("Innkeeper",FieldSpeakerRoles.Prefer("Innkeeper","Man in a cap"));
            TestAssert.Equal("Alexandrian Soldier",FieldSpeakerRoles.Prefer("Alexandrian Soldier","Soldier with pointed helmet"));
            TestAssert.Equal("Blank",FieldSpeakerRoles.Prefer("Blank","Youth with spiky hair"));
            TestAssert.Equal("Bird",FieldSpeakerRoles.Prefer(null,"Bird"));
        }
        public static void NewRolesStayEnglishOnly()
        {
            TestAssert.True(Label("Innkeeper","FR")==null);
            TestAssert.Equal("Innkeeper",Label("Innkeeper","UK"));
        }
    }
}
