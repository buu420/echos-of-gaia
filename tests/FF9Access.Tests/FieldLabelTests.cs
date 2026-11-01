using System;
using System.IO;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class FieldLabelTests
    {
        private static FieldTargetObservation Actor(string scope="model118:sid3:script1")
        {
            var actor=new FieldTargetObservation{Id=new TargetIdentity(TargetKind.FieldActor,"field:100",3),Visible=true,IsPerson=true,HasInteraction=true,Position=new NavigationPoint(1,0,1),NativePresentationIdentity=15};
            Set(actor,"VisibleLabel","Soldier with round shield");Set(actor,"LabelIdentity",scope);return actor;
        }
        private static void Set(object obj,string field,object value) { var member=obj.GetType().GetField(field);TestAssert.True(member!=null);member.SetValue(obj,value); }
        private static void Record(ObservedIdentityStore store,TargetIdentity id,string name,string scope)
        { var method=typeof(ObservedIdentityStore).GetMethod("Record",new[]{typeof(TargetIdentity),typeof(string),typeof(string)});TestAssert.True(method!=null);method.Invoke(store,new object[]{id,name,scope}); }
        private static NavigationContext Context(){return new NavigationContext{LogicalMapKey="field:100",Generation=1,SaveIdentity="save",PlayerIdentity=17};}
        public static void VisibleAppearanceReplacesGenericPersonLetters()
        { var actor=Actor();var target=new FieldTargetCatalog().Build(Context(),new[]{actor},null).Targets[0];TestAssert.True(target.Label.StartsWith("Soldier with round shield"));TestAssert.False(target.Label.StartsWith("Person")); }
        public static void ActualDisplayedNameWinsOverAppearance()
        { var actor=Actor();var names=new ObservedIdentityStore();Record(names,actor.Id,"Doug","model118:sid3:script1");TestAssert.Equal("Doug",new FieldTargetCatalog().Build(Context(),new[]{actor},names).Targets[0].Label); }
        public static void ReusedUidCannotInheritAnotherActorsName()
        { var actor=Actor("model52:sid3:script2");var names=new ObservedIdentityStore();Record(names,actor.Id,"Doug","model118:sid3:script1");TestAssert.False(new FieldTargetCatalog().Build(Context(),new[]{actor},names).Targets[0].Label=="Doug"); }
        public static void LegacyUnscopedNamesCannotRenameScopedNativeActor()
        { var actor=Actor();var names=new ObservedIdentityStore();names.Record(actor.Id,"Someone from a previous layout");TestAssert.False(new FieldTargetCatalog().Build(Context(),new[]{actor},names).Targets[0].Label.Contains("previous layout")); }
        public static void ScopedNamesPersistAndKeepTheirIdentity()
        {
            string root=Path.Combine(Path.GetTempPath(),"ff9-names-"+Guid.NewGuid().ToString("N"));
            try { var actor=Actor();var names=new ObservedIdentityStore(root);Record(names,actor.Id,"Doug","model118:sid3:script1");names.CommitSave("save");
                var loaded=new ObservedIdentityStore(root);loaded.SwitchSave("save");TestAssert.Equal("Doug",new FieldTargetCatalog().Build(Context(),new[]{actor},loaded).Targets[0].Label);
                Set(actor,"LabelIdentity","different scene entry");TestAssert.False(new FieldTargetCatalog().Build(Context(),new[]{actor},loaded).Targets[0].Label=="Doug");
            }finally{if(Directory.Exists(root) && Path.GetFullPath(root).StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase))Directory.Delete(root,true);}
        }
        public static void NativeRightCurlyQuoteCanIntroduceDisplayedName()
        { string name;TestAssert.True(DisplayedSpeakerLabel.TryRead("Mene\n”Let's play!",out name));TestAssert.Equal("Mene",name); }
        public static void UnrevealedQuestionMarksDoNotReplaceAnAppearanceLabel()
        { string name;TestAssert.False(DisplayedSpeakerLabel.TryRead("???\n“Hello.”",out name)); }
    }
}
