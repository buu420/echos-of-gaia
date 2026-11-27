using System;

namespace Memoria.Accessibility.Navigation
{
    public static partial class NativeFieldTargets
    {
        private const string TotHatchScript = "736c7e646ce1f76cd23b0b4358a29f701503d2703f32175ffd912216f442ec96";

        private sealed class ReviewedStoryPrompt
        {
            public string Map, Mesh, Background, Script, PlayerScript;
            public int Uid, PlayerUid, FirstStory, LastStory;
            public int[] Support;
        }

        // These stock touch handlers present a Confirm prompt. Their scheduled
        // Push can already own priority 1 when the player reaches the prompt.
        private static readonly ReviewedStoryPrompt[] ReviewedStoryPrompts =
        {
            new ReviewedStoryPrompt
            {
                Map = TotTower, Uid = 8, Script = TotHatchScript, FirstStory = 4470, LastStory = 4500,
                PlayerUid = 13, PlayerScript = "0258bc05c50832796d07415384517dc8dcca12dc3d95f4a1e4671d9afb8c1b46",
                Mesh = "7727529a2181e88a03bf2d89ac561b3cd8687a8c0f108ef88fefb27e96bb1b55",
                Background = "c96b4aeb723a85db23e4a5121c34eda44c4f0a98341847fca4ce5918b1f55610",
                Support = new[] { 77, 83, 96 }
            },
            new ReviewedStoryPrompt
            {
                Map = "field:953:FBG_N21_GRGR_MAP424_GR_REN_0", Uid = 5, FirstStory = 4515, LastStory = 4520,
                Script = "712f75ae44ef876cc09a789d5f3be6dcbc50a615ca6f339cfbeb1ea321cb8b34",
                PlayerUid = 8, PlayerScript = "3fc4aab6f9ac6e3323c4c9caa02995de23266b3a5b3efc5a100c837ce0e03131",
                Mesh = "3a8c6e6c110307e1e07d7664d3578e5f492fcc7ccefdd0c99ef27b1e78ceb8ba",
                Background = "dfd60ec6ca814ab84dbcc753c98ac89291ee22421ad9e2e03414c823586d6832",
                Support = new[] { 1, 5, 8, 14, 15, 17, 25, 36, 37, 38, 39, 40 }
            },
            new ReviewedStoryPrompt
            {
                Map = "field:952:FBG_N21_GRGR_MAP423_GR_GBA_0", Uid = 9, FirstStory = 4520, LastStory = 4530,
                Script = "014a3e0a9b64564ccc039d3c0083ce0759c0072817ed29601d573c0ceb8d2fdc",
                PlayerUid = 12, PlayerScript = "b874d7390827967cec9bc8d8ce6f4ec4366f5dc1a55fcda1de53af6a258096eb",
                Mesh = "b03c42d8d4c37d70886fe67ce2f880cbccb4c4da1c593595db797f197ff8c2d2",
                Background = "b28aec85e4f475a76519384df9dca2298b42c4e22021c83b76311e7b055e3a4f",
                Support = new[] { 19, 20, 21, 23 }
            }
        };

        private static bool ReviewedStockEntry(Obj obj, int id, string script)
        {
            return obj != null && obj.sid == id && obj.uid == id && obj.ebData != null &&
                obj.state == EventEngine.stateRunning && !obj.isAdditionCommand && ReferenceEquals(obj.currentByte, obj.ebData) &&
                Fingerprint(obj.ebData) == script;
        }

        private static bool ReviewedPromptScene(NavigationContext context, EventEngine engine, FieldMap field, Actor player,
            int playerUid, string playerScript, string mesh, string background)
        {
            return context.CanControl && engine != null && engine.gMode == 1 && engine.GetUserControl() &&
                field?.GetCurrentBgCamera() != null && field.camIdx == 0 && field.curCamIdx == 0 && field.scene?.ebgBin != null &&
                player != null && player.model == 185 && player.uid == context.PlayerIdentity &&
                ReferenceEquals(player.fieldMapActorController?.fieldMap, field) && ReviewedStockEntry(player, playerUid, playerScript) &&
                CurrentBgiHash(field) == mesh && Fingerprint(field.scene.ebgBin) == background;
        }

        private static bool ReviewedPromptQuad(Quad quad, int uid, string script)
        {
            return ReviewedStockEntry(quad, uid, script) && quad.q != null && quad.n >= 3 && quad.n <= quad.q.Length &&
                quad.wait == 0 && NativeFieldTalkability.IsTalkable(quad);
        }

        private static ReviewedStoryPrompt FindReviewedStoryPrompt(NavigationContext context, EventEngine engine, FieldMap field, Actor player, Quad quad)
        {
            foreach (var prompt in ReviewedStoryPrompts)
            {
                if (context.LogicalMapKey == prompt.Map && context.StoryProgress >= prompt.FirstStory &&
                    context.StoryProgress < prompt.LastStory && quad.uid == prompt.Uid && ReviewedPromptQuad(quad, prompt.Uid, prompt.Script) &&
                    ReviewedPromptScene(context, engine, field, player, prompt.PlayerUid, prompt.PlayerScript, prompt.Mesh, prompt.Background))
                    return prompt;
            }
            return null;
        }

        private static bool PresentedStoryPrompt(Quad quad)
        {
            return EIcon.NavigationPresentedUid == quad.uid && EIcon.NavigationScriptIconUid == quad.uid &&
                EIcon.SFIconType == BubbleUI.IconType.Exclamation;
        }

        private static InteractionReadiness ReadPresentedStoryPrompt(EventEngine engine, Actor player, Quad quad, int[] support, int[] alternate = null)
        {
            var controller = player.fieldMapActorController;
            int triangle = controller.activeTri;
            if (Array.IndexOf(support, triangle) < 0 && (alternate == null || Array.IndexOf(alternate, triangle) < 0))
                return new InteractionReadiness();
            if (engine.TreadQuad(player, 2) != quad || engine.TreadQuad(player, 4) != quad ||
                !engine.NavigationContainsQuad(quad, controller.curPos.x, controller.curPos.z))
                return new InteractionReadiness();

            bool queued = EventCollision.NavigationHasTouchFacing(player, quad) && IsQueuedStockQuadPush(engine, quad);
            if (!PresentedStoryPrompt(quad)) return new InteractionReadiness { AwaitingPrompt = queued };
            return FieldInteractionRules.PushReadiness(true, true, queued || EventCollision.NavigationCanTalkQuad(player, quad), true);
        }

        internal static InteractionReadiness ReadReviewedStoryPromptReadiness(NavigationContext context, EventEngine engine, Actor player, Quad quad)
        {
            var prompt = FindReviewedStoryPrompt(context, engine, player.fieldMapActorController.fieldMap, player, quad);
            return prompt == null ? null : ReadPresentedStoryPrompt(engine, player, quad, prompt.Support);
        }
    }
}
