using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    // Appearance labels come only from reviewed renders of the exact installed meshes
    // (docs/accessibility/reviewed-field-labels.json). They describe what is drawn, never a
    // personal name, story identity, reward, asset symbol or numeric ID.
    internal static class ObjectLabelTests
    {
        private static readonly string[] StoryNames={"Zidane","Vivi","Garnet","Dagger","Steiner","Adelbert","Freya","Quina","Eiko","Amarant","Beatrix","Kuja","Garland","Brahne","Cid","Hilda","Ruby","Blank","Marcus","Cinna","Baku","Tot","Zorn","Thorn","Mog","Stiltzkin","Fratley","Ramuh","Lani","Puck","Necron","Trance","Genome","Burmecian","Cleyran","Black Waltz","Tantalus","Alexandria","Lindblum","Treno"};
        private static JsonElement[] Published()
        {
            using(var json=JsonDocument.Parse(File.ReadAllText(Repo("docs/accessibility/reviewed-field-labels.json"))))
                return json.RootElement.GetProperty("entries").EnumerateArray().Select(e=>e.Clone()).ToArray();
        }
        public static void CompiledAppearancesMatchThePublishedReviewExactly()
        {
            var published=Published();
            TestAssert.Equal(published.Length,published.Select(e=>e.GetProperty("model").GetInt32()).Distinct().Count());
            foreach(var entry in published) {
                int model=entry.GetProperty("model").GetInt32();
                string label=entry.GetProperty("label").GetString();
                bool conditional=entry.TryGetProperty("visibilityRequirement",out _);
                TestAssert.Equal(label,conditional?FieldVisibleLabels.Read(model,true):FieldVisibleLabels.Read(model));
            }
            // Every model the table answers is published with its proof.
            var models=new HashSet<int>(published.Select(e=>e.GetProperty("model").GetInt32()));
            for(int model=0;model<6000;model++)if(FieldVisibleLabels.Read(model)!=null || FieldVisibleLabels.Read(model,true)!=null)TestAssert.True(models.Contains(model));
        }
        public static void EveryPublishedAppearanceCitesItsInstalledMeshProof()
        {
            foreach(var entry in Published()) {
                TestAssert.True(entry.GetProperty("sourceAsset").GetString().StartsWith("assets/resources/models/"));
                TestAssert.True(entry.GetProperty("loadedGeo").GetString().StartsWith("GEO_"));
                TestAssert.Equal(64,entry.GetProperty("proofSha256").GetString().Length);
                TestAssert.True(entry.GetProperty("proofRow").GetInt32()>=1);
                TestAssert.False(String.IsNullOrWhiteSpace(entry.GetProperty("visibleFeatures").GetString()));
            }
        }
        public static void NewlyReviewedPeopleAndPropsReplaceGenericLetters()
        {
            TestAssert.Equal("Soldier in a tall cone helmet",FieldVisibleLabels.Read(217));
            TestAssert.Equal("Cook with a frying pan",FieldVisibleLabels.Read(5474));
            TestAssert.Equal("Bird",FieldVisibleLabels.Read(63));
            TestAssert.Equal("Chocobo",FieldVisibleLabels.Read(259));
            TestAssert.Equal("Chest",FieldVisibleLabels.Read(701));
            TestAssert.Equal("Chest",FieldVisibleLabels.Read(702));
            TestAssert.Equal("Ladder",FieldVisibleLabels.Read(22));
            TestAssert.Equal("Barrel",FieldVisibleLabels.Read(241));
        }
        public static void ModelsLoadingAReviewedMeshShareItsAppearance()
        {
            // ModelFactory upscale/revert tables load model 185's mesh for 76 and model 98's for 668-670.
            TestAssert.Equal(FieldVisibleLabels.Read(185),FieldVisibleLabels.Read(76));
            foreach(int model in new[]{668,669,670})TestAssert.Equal(FieldVisibleLabels.Read(98),FieldVisibleLabels.Read(model));
        }
        public static void AppearanceLabelsNeverNameStoryIdentitiesOrIds()
        {
            foreach(var entry in Published()) {
                string label=entry.GetProperty("label").GetString();
                TestAssert.False(String.IsNullOrWhiteSpace(label));
                TestAssert.True(label.Length<=60);
                TestAssert.False(label.Any(Char.IsDigit));
                TestAssert.False(label.Contains("GEO") || label.Contains("_"));
                foreach(string name in StoryNames)TestAssert.False(label.IndexOf(name,StringComparison.OrdinalIgnoreCase)>=0);
            }
        }
        public static void UnreadableRendersStayGeneric()
        {
            // Deferred in the review: collapsed or ambiguous bind poses receive no guessed label.
            foreach(int model in new[]{395,6,16,262,414,622,530})TestAssert.Equal<string>(null,FieldVisibleLabels.Read(model));
        }
        private static string Repo(string relative)
        {
            var dir=new DirectoryInfo(AppContext.BaseDirectory);
            while(dir!=null && !Directory.Exists(Path.Combine(dir.FullName,"Assembly-CSharp")))dir=dir.Parent;
            if(dir==null){dir=new DirectoryInfo(Environment.CurrentDirectory);while(dir!=null && !Directory.Exists(Path.Combine(dir.FullName,"Assembly-CSharp")))dir=dir.Parent;}
            if(dir==null)throw new Exception("Repository root not found");
            return Path.Combine(dir.FullName,relative);
        }
    }
}
