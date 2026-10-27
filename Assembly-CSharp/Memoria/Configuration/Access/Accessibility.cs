using System;

namespace Memoria
{
    public sealed partial class Configuration
    {
        public static class Accessibility
        {
            public static String NavigationBindings { get => Instance._accessibility.NavigationBindings; set => Instance._accessibility.NavigationBindings.Value = value; }
            public static Int32 NavigationIntervalMilliseconds { get => Memoria.Accessibility.Navigation.NavigationControls.IntervalMilliseconds(Instance._accessibility.NavigationIntervalMilliseconds, Instance._accessibility.NavigationUpdateInterval); set => Instance._accessibility.NavigationIntervalMilliseconds.Value = Memoria.Accessibility.Navigation.NavigationControls.IntervalMilliseconds(value, 1); }
            public static Boolean AnnounceRegionChanges { get => Memoria.Accessibility.Navigation.NavigationControls.RegionAnnouncements(Instance._accessibility.AnnounceRegionChanges); set => Instance._accessibility.AnnounceRegionChanges.Value = value ? "1" : "0"; }
            public static String ControllerNaming { get => Instance._accessibility.ControllerNaming; set => Instance._accessibility.ControllerNaming.Value = value; }
            // Retain the existing INI key for the Hot and Cold status shortcut only.
            public static String ControllerMenuButton { get => Instance._accessibility.ControllerMenuButton; set => Instance._accessibility.ControllerMenuButton.Value = value; }
            public static Boolean WorldMapFootsteps { get => Instance._accessibility.WorldMapFootsteps; set => Instance._accessibility.WorldMapFootsteps.Value = value; }
            public static Int32 WorldMapFootstepVolume { get => Math.Max(0, Math.Min(400, Instance._accessibility.WorldMapFootstepVolume.Value)); set => Instance._accessibility.WorldMapFootstepVolume.Value = Math.Max(0, Math.Min(400, value)); }
            public static Boolean AudioDescriptions { get => Instance._accessibility.AudioDescriptions; set => Instance._accessibility.AudioDescriptions.Value = value; }
            public static Boolean FieldExitSounds { get => Instance._accessibility.FieldExitSounds; set => Instance._accessibility.FieldExitSounds.Value = value; }
            public static Int32 FieldExitSoundVolume { get => Math.Max(0, Math.Min(200, Instance._accessibility.FieldExitSoundVolume.Value)); set => Instance._accessibility.FieldExitSoundVolume.Value = Math.Max(0, Math.Min(200, value)); }
            public static Boolean FieldNpcSounds { get => Instance._accessibility.FieldNpcSounds; set => Instance._accessibility.FieldNpcSounds.Value = value; }
            public static Int32 FieldNpcSoundVolume { get => Math.Max(0, Math.Min(200, Instance._accessibility.FieldNpcSoundVolume.Value)); set => Instance._accessibility.FieldNpcSoundVolume.Value = Math.Max(0, Math.Min(200, value)); }
            public static Boolean FieldInteractableSounds { get => Instance._accessibility.FieldInteractableSounds; set => Instance._accessibility.FieldInteractableSounds.Value = value; }
            public static Int32 FieldInteractableSoundVolume { get => Math.Max(0, Math.Min(200, Instance._accessibility.FieldInteractableSoundVolume.Value)); set => Instance._accessibility.FieldInteractableSoundVolume.Value = Math.Max(0, Math.Min(200, value)); }
            public static Int32 AudioDescriptionVolume { get => Math.Max(0, Math.Min(200, Instance._accessibility.AudioDescriptionVolume.Value)); set => Instance._accessibility.AudioDescriptionVolume.Value = Math.Max(0, Math.Min(200, value)); }
            public static Boolean AnnounceIcons
            {
                get => Instance._accessibility.AnnounceIcons;
                set => Instance._accessibility.AnnounceIcons.Value = value;
            }

            public static Int32 NavigationUpdateInterval
            {
                get => Instance._accessibility.NavigationUpdateInterval;
                set => Instance._accessibility.NavigationUpdateInterval.Value = value;
            }

            public static Boolean CageMinigameAudioFeedback
            {
                get => Instance._accessibility.CageMinigameAudioFeedback;
                set => Instance._accessibility.CageMinigameAudioFeedback.Value = value;
            }

            public static Boolean CageMinigameSkipEnabled
            {
                get => Instance._accessibility.CageMinigameSkipEnabled;
                set => Instance._accessibility.CageMinigameSkipEnabled.Value = value;
            }

            public static Int32 CageMinigameFeedbackInterval
            {
                get => Instance._accessibility.CageMinigameFeedbackInterval;
                set => Instance._accessibility.CageMinigameFeedbackInterval.Value = value;
            }

            public static Boolean CageMinigameUseTones
            {
                get => Instance._accessibility.CageMinigameUseTones;
                set => Instance._accessibility.CageMinigameUseTones.Value = value;
            }

            public static void SaveValues()
            {
                SaveValue(Instance._accessibility.Name, Instance._accessibility.NavigationBindings);
                SaveValue(Instance._accessibility.Name, Instance._accessibility.NavigationIntervalMilliseconds);
                SaveValue(Instance._accessibility.Name, Instance._accessibility.AnnounceRegionChanges);
                SaveValue(Instance._accessibility.Name, Instance._accessibility.ControllerNaming);
                SaveValue(Instance._accessibility.Name, Instance._accessibility.ControllerMenuButton);
                SaveValue(Instance._accessibility.Name, Instance._accessibility.WorldMapFootsteps);
                SaveValue(Instance._accessibility.Name, Instance._accessibility.WorldMapFootstepVolume);
                SaveValue(Instance._accessibility.Name, Instance._accessibility.AudioDescriptions);
                SaveValue(Instance._accessibility.Name, Instance._accessibility.AudioDescriptionVolume);
                SaveValue(Instance._accessibility.Name, Instance._accessibility.FieldExitSounds);
                SaveValue(Instance._accessibility.Name, Instance._accessibility.FieldExitSoundVolume);
                SaveValue(Instance._accessibility.Name, Instance._accessibility.FieldNpcSounds);
                SaveValue(Instance._accessibility.Name, Instance._accessibility.FieldNpcSoundVolume);
                SaveValue(Instance._accessibility.Name, Instance._accessibility.FieldInteractableSounds);
                SaveValue(Instance._accessibility.Name, Instance._accessibility.FieldInteractableSoundVolume);
                SaveValue(Instance._accessibility.Name, Instance._accessibility.AnnounceIcons);
                SaveValue(Instance._accessibility.Name, Instance._accessibility.NavigationUpdateInterval);
                SaveValue(Instance._accessibility.Name, Instance._accessibility.CageMinigameAudioFeedback);
                SaveValue(Instance._accessibility.Name, Instance._accessibility.CageMinigameSkipEnabled);
                SaveValue(Instance._accessibility.Name, Instance._accessibility.CageMinigameFeedbackInterval);
                SaveValue(Instance._accessibility.Name, Instance._accessibility.CageMinigameUseTones);
            }
        }
    }
}
