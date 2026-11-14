using System;

namespace Memoria.Accessibility.AudioDescriptions
{
    internal sealed class GulugWellRopeCue
    {
        internal const string Controls = "Hold Down or Left to descend. Hold Up or Right to climb.";
        private bool _spoken;

        internal static bool AcceptsDirection(int offset, int animation)
        {
            // Player entry 10, tag 12. The input loop precedes both scripted dismounts.
            return offset >= 571 && offset <= 1083 && (animation == 13053 || animation == 13055 || animation == 13073);
        }

        internal void Observe(bool climbing, Func<string, bool> speak)
        {
            if (!climbing)
                _spoken = false;
            else if (!_spoken)
                _spoken = speak(Controls);
        }
    }
}
