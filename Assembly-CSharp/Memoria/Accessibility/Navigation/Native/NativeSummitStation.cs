using Memoria.Data;

namespace Memoria.Accessibility.Navigation
{
    internal static class NativeSummitStation
    {
        private const string CableCar = "field:815:FBG_N16_STGT_MAP341_SG_TRN_2";
        private const string DaggerScript = "86f7d7d562714fe5b24dcd4b19d02b690de74fd75a121888c7aa8bcabe74f5ee";
        private const string MarcusScript = "6c9e19ab3f3ada619df2444cfb5eae5dac2738a1093e5e008737e42807f5970d";
        private const string SteinerScript = "545f40beb83ac74605e337eb8932dfd081f7c3b33fd76d1f45a1aa8dc975c719";

        internal static void AnnotateActor(NavigationContext context, Actor player, Actor actor,
            FieldTargetObservation observation, float collision, float interaction)
        {
            if (context.LogicalMapKey != CableCar || context.StoryProgress != 4230 || !context.CanControl ||
                (!observation.Visible && !observation.OutOfView)) return;

            string label;
            if (Matches(actor, 13, 109, MarcusScript)) label = "Marcus";
            else if (Matches(actor, 10, 5489, SteinerScript))
                label = FF9StateSystem.Common.FF9.GetPlayer(CharacterId.Steiner)?.Name ?? "Steiner";
            else return;

            if (!Matches(player, 9, 185, DaggerScript)) return;
            observation.VisibleLabel = label;
            if (!observation.HasInteraction) return;

            // The seats are separated from the aisle. The native Talk range spans
            // the gap, so try the existing finer approach search on the aisle floor.
            observation.ActorInteractionRange = new FieldActorInteractionRange(collision, interaction);
        }

        private static bool Matches(Actor actor, int id, int model, string fingerprint)
        {
            return actor != null && actor.uid == id && actor.sid == id && actor.model == model && actor.ebData != null &&
                NativeFieldTargets.Fingerprint(actor.ebData) == fingerprint;
        }
    }
}
