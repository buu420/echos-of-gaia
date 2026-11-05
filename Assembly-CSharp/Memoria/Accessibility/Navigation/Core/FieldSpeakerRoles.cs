using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.Navigation
{
    // A person's label from the speaker headings of its own native Talk dialogue (opcode-stripped
    // field text). Only reviewed descriptive English role headings may precede dialogue.
    // Word count is not name provenance: "Innkeeper Hal" and "Dante the Signmaker" are names.
    // A proper name needs this actor's observed identity or a native party-instance binding.
    // More than one distinct speaker in the handler is not proof whose label it is.
    public static class FieldSpeakerRoles
    {
        private static readonly HashSet<string> Roles=new HashSet<string>(new[]{
            "Attendant","Soldier","Alexandrian Soldier","Scholar","Card Game Usher","Old Man","Old Woman",
            "Aviator","Adventurer","Girl","Conductor","Old Lady","Worker","Waitress","Nobleman","Boy","Youth",
            "Four-armed Man","Bishop","Thug","Guy Doing Research","Elite Guard","Officer","Guard","Cable Car Enthusiast",
            "Barkeep","Treasure Hunter","Stubborn Geezer","Supervisor","Fish Man","Male Red Mage","Pigeon Lover",
            "Merchant","Lindblum Officer","Tour Guide","Cymbalist","Violinist","Old Man with a Cane","Servant",
            "Crew Member","Drummer","Retired Boatman","Young Lady","Chef","Carpenter","Craftsman","Head Chef",
            "Captain of the Knights of Pluto","Female Soldier","Moogle","Chocobo",
            // Counter staff, workers and guards reviewed from installed own/requested speech (2026-11-04).
            // Personal names, titles, relations and story identities are deliberately absent.
            "Innkeeper","Shopkeeper","Card Seller","Lazy Carpenter","Scientist","Weaponsmith","Synthesist","Synthesis Expert",
            "Librarian","Sales Clerk","Ticketmaster","Boatman","Engineer","New Engineer","Experienced Engineer","Veteran Engineer",
            "Chief Engineer","Industrial District Engineer","Construction Worker","Carpenter’s Apprentice","Carpenter's Apprentice",
            "Burmecian Soldier","Knight of Pluto","Newbie Soldier","Tall Guard","Short Guard","Gatekeeper","Trumpeter",
            "Noble Lady","Noblewoman","Noble Lord","Young Nobleman","Honorable Lady","Honorable Lord","Red Mage","Female Red Mage",
            "Female Adventurer","Female Customer","Young Man","Earnest Young Man","Silly Old Man","Lady with an Apron",
            "Flower Girl","Florist","Village Sculptor","Dali Villager","Drunk","Bandit","Card Freak","Booklover","Bookworm","Judge","Man"
        },StringComparer.Ordinal);
        public static string Label(IList<string> ownTexts,Func<string,bool> knownName)
        {return Label(ownTexts,knownName,"US");}
        public static string Label(IList<string> ownTexts,Func<string,bool> knownName,string language)
        {
            if(ownTexts==null)return null;
            string speaker=null;
            foreach(string text in ownTexts) {
                string heading;if(!DisplayedSpeakerLabel.TryRead(text,out heading))continue;
                if(speaker==null)speaker=heading;else if(speaker!=heading)return null;
            }
            if(speaker==null)return null;
            if(knownName!=null && knownName(speaker))return speaker;
            bool english=language=="US" || language=="UK" || language=="EN";
            return english && IsRole(speaker)?speaker:null;
        }
        public static bool IsRole(string heading)
        {
            return heading!=null && Roles.Contains(heading);
        }
        // Headings that only restate age or sex. A reviewed appearance ("Man in a flat cap") says more.
        private static readonly HashSet<string> GenericPeople=new HashSet<string>(new[]{
            "Man","Woman","Girl","Boy","Youth","Child","Old Man","Old Woman","Old Lady","Young Man","Young Lady"
        },StringComparer.Ordinal);
        // The label to present when a person has both a usable heading and a reviewed appearance.
        public static string Prefer(string heading,string appearance)
        {
            if(heading==null)return appearance;
            return appearance!=null && GenericPeople.Contains(heading)?appearance:heading;
        }
    }
}
