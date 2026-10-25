using System;

namespace Memoria.Launcher
{
    public static class AccessibilityBuildPolicy
    {
        public const string ForkId = "ff9-access-local";
        public const int AccessibilityApi = 1;
        public static bool IsCustomBuild { get { return true; } }
        public const string UpdateStatus = "Updates unavailable: no update source is configured for this accessibility build.";

        public static bool CanApplyUpdate(string installedForkId, string candidateForkId, int candidateAccessibilityApi)
        {
            return String.Equals(installedForkId, ForkId, StringComparison.Ordinal) &&
                String.Equals(candidateForkId, ForkId, StringComparison.Ordinal) &&
                candidateAccessibilityApi == AccessibilityApi;
        }
    }
}
