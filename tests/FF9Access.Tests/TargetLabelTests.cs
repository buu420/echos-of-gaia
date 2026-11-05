using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    // FieldTargetCatalog.Label: a unique meaningful description is spoken as it is. Identical presented
    // descriptions receive stable occurrence numbers within the map. Unknown descriptions keep an honest
    // generic word ("Person", "Object", "Item"), numbered only when several are presented.
    internal static class TargetLabelTests
    {
        private static FieldTargetObservation Seen(int id,string label,bool person=false)
        {return new FieldTargetObservation{Id=new TargetIdentity(TargetKind.FieldActor,"field:100",id),Position=TestFixtures.P(id,0),Visible=true,IsPerson=person,HasInteraction=true,VisibleLabel=label};}
        private static CatalogResult Build(FieldTargetCatalog catalog,params FieldTargetObservation[] values){return catalog.Build(TestFixtures.Context(),values,new ObservedIdentityStore(null));}
        public static void UniqueMeaningfulLabelsSpeakWithoutLetters()
        {
            var catalog=new FieldTargetCatalog();var innkeeper=Seen(3,"Innkeeper",true);var lever=Seen(9,"Lever");
            var result=Build(catalog,innkeeper,lever);
            TestAssert.Equal("Innkeeper",result.Find(innkeeper.Id).Label);TestAssert.Equal("Lever",result.Find(lever.Id).Label);
        }
        public static void IdenticalDescriptionsGetStableOccurrenceNumbers()
        {
            var catalog=new FieldTargetCatalog();var first=Seen(4,"Chest");var second=Seen(12,"Chest");
            var result=Build(catalog,second,first);
            TestAssert.Equal("Chest 1",result.Find(first.Id).Label);TestAssert.Equal("Chest 2",result.Find(second.Id).Label);
            // Moving, or only one remaining on screen, never renumbers an occurrence within the map.
            second.Position=TestFixtures.P(-500,0);
            TestAssert.Equal("Chest 2",Build(catalog,second).Find(second.Id).Label);
            TestAssert.Equal("Chest 1",Build(catalog,first,second).Find(first.Id).Label);
            // A third later arrival takes the next number.
            var third=Seen(2,"Chest");TestAssert.Equal("Chest 3",Build(catalog,first,second,third).Find(third.Id).Label);
        }
        public static void ALoneTargetKeepsItsPlainNameWhenATwinArrives()
        {
            var catalog=new FieldTargetCatalog();var first=Seen(4,"Chest");
            TestAssert.Equal("Chest",Build(catalog,first).Find(first.Id).Label);
            var second=Seen(2,"Chest");var both=Build(catalog,first,second);
            TestAssert.Equal("Chest",both.Find(first.Id).Label);TestAssert.Equal("Chest 2",both.Find(second.Id).Label);
        }
        public static void UnknownDescriptionsStayHonestlyGeneric()
        {
            var catalog=new FieldTargetCatalog();var person=Seen(5,null,true);var thing=Seen(6,null);
            var result=Build(catalog,person,thing);
            TestAssert.Equal("Person",result.Find(person.Id).Label);TestAssert.Equal("Object",result.Find(thing.Id).Label);
            var other=Seen(7,null);var more=Build(new FieldTargetCatalog(),thing,other);
            TestAssert.Equal("Object 1",more.Find(thing.Id).Label);TestAssert.Equal("Object 2",more.Find(other.Id).Label);
        }
        public static void SameWordsInAnotherCategoryAreCountedSeparately()
        {
            var catalog=new FieldTargetCatalog();var guard=Seen(1,"Guard",true);var statue=Seen(2,"Guard");
            var result=Build(catalog,guard,statue);
            TestAssert.Equal("Guard",result.Find(guard.Id).Label);TestAssert.Equal("Guard",result.Find(statue.Id).Label);
        }
        public static void ANewMapStartsNumberingAgain()
        {
            var catalog=new FieldTargetCatalog();Build(catalog,Seen(1,"Chest"),Seen(2,"Chest"));
            var context=TestFixtures.Context();context.LogicalMapKey="field:101";
            var only=new FieldTargetObservation{Id=new TargetIdentity(TargetKind.FieldActor,"field:101",8),Position=TestFixtures.P(8,0),Visible=true,HasInteraction=true,VisibleLabel="Chest"};
            TestAssert.Equal("Chest",catalog.Build(context,new[]{only},new ObservedIdentityStore(null)).Find(only.Id).Label);
        }
    }
}
