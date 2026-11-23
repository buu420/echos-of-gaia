using Memoria.Accessibility.Navigation;

namespace FF9Access.Tests
{
    internal static class FieldLearnedLabelTests
    {
        private const string MainStreet = "field:552:FBG_N11_LDBM_MAP155_LB_MST_0";

        public static void LearnedNamesDisambiguateSoldiersPresentedTogether()
        {
            var first = Soldier(12);
            var second = Soldier(13);
            var names = new ObservedIdentityStore();
            names.Record(first.Id, "Soldier", first.LabelIdentity);
            names.Record(second.Id, "Soldier", second.LabelIdentity);

            var result = new FieldTargetCatalog().Build(Context(), new[] { second, first }, names);

            TestAssert.Equal("Soldier 1", result.Find(first.Id).Label);
            TestAssert.Equal("Soldier 2", result.Find(second.Id).Label);
        }

        public static void LateTwinKeepsFirstSoldiersPlainNameAndNumbersSecond()
        {
            var catalog = new FieldTargetCatalog();
            var context = Context();
            var first = Soldier(12);
            var second = Soldier(13);
            second.Visible = false;
            second.OutOfView = true;
            var names = new ObservedIdentityStore();
            names.Record(first.Id, "Soldier", first.LabelIdentity);
            names.Record(second.Id, "Soldier", second.LabelIdentity);
            var observations = new[] { first, second };

            var initial = catalog.Build(context, observations, names);
            TestAssert.Equal("Soldier", initial.Find(first.Id).Label);
            TestAssert.False(initial.Contains(second.Id));

            first.Visible = false;
            first.OutOfView = true;
            second.Visible = true;
            second.OutOfView = false;
            var next = catalog.Build(context, observations, names);

            TestAssert.Equal("Soldier", next.Find(first.Id).Label);
            TestAssert.Equal(VisibilityEvidence.ObservedFieldLandmark, next.Find(first.Id).Evidence);
            TestAssert.Equal("Soldier 2", next.Find(second.Id).Label);
        }

        private static FieldTargetObservation Soldier(int uid)
        {
            return new FieldTargetObservation
            {
                Id = new TargetIdentity(TargetKind.FieldActor, MainStreet, uid),
                Position = new NavigationPoint(uid, 0, 0),
                NativePresentationIdentity = uid,
                LabelIdentity = "model217:soldier:" + uid,
                VisibleLabel = "Soldier in a tall cone helmet",
                Visible = true,
                IsPerson = true
            };
        }

        private static NavigationContext Context()
        {
            return new NavigationContext { LogicalMapKey = MainStreet, Generation = 1 };
        }
    }
}
