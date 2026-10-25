using Memoria.Prime.Ini;
using System;

namespace Memoria
{
    public sealed partial class Configuration
    {
        private sealed class AccessibilitySection : IniSection
        {
            public readonly IniValue<Boolean> AnnounceIcons;
            public readonly IniValue<Int32> NavigationUpdateInterval;
            public readonly IniValue<String> NavigationBindings, ControllerNaming, ControllerMenuButton;
            public readonly IniValue<Int32> NavigationIntervalMilliseconds;
            public readonly IniValue<String> AnnounceRegionChanges;
            public readonly IniValue<Boolean> WorldMapFootsteps;
            public readonly IniValue<Int32> WorldMapFootstepVolume;
            public readonly IniValue<Boolean> AudioDescriptions;
            public readonly IniValue<Int32> AudioDescriptionVolume;
            public readonly IniValue<Boolean> FieldExitSounds;
            public readonly IniValue<Int32> FieldExitSoundVolume;
            public readonly IniValue<Boolean> FieldNpcSounds,FieldInteractableSounds;
            public readonly IniValue<Int32> FieldNpcSoundVolume,FieldInteractableSoundVolume;
            public readonly IniValue<Boolean> CageMinigameAudioFeedback;
            public readonly IniValue<Boolean> CageMinigameSkipEnabled;
            public readonly IniValue<Int32> CageMinigameFeedbackInterval;
            public readonly IniValue<Boolean> CageMinigameUseTones;

            public AccessibilitySection() : base(nameof(AccessibilitySection), true)
            {
                AnnounceIcons = BindBoolean(nameof(AnnounceIcons), true);
                NavigationUpdateInterval = BindInt32(nameof(NavigationUpdateInterval), 1);
                NavigationBindings = BindString(nameof(NavigationBindings), Memoria.Accessibility.Navigation.NavigationBindings.DefaultSerialization);
                NavigationIntervalMilliseconds = BindInt32(nameof(NavigationIntervalMilliseconds), 0);
                AnnounceRegionChanges = BindString(nameof(AnnounceRegionChanges), "1");
                WorldMapFootsteps = BindBoolean(nameof(WorldMapFootsteps), true);
                WorldMapFootstepVolume = BindInt32(nameof(WorldMapFootstepVolume), 200);
                AudioDescriptions = BindBoolean(nameof(AudioDescriptions), true);
                AudioDescriptionVolume = BindInt32(nameof(AudioDescriptionVolume), 100);
                FieldExitSounds = BindBoolean(nameof(FieldExitSounds), true);
                FieldExitSoundVolume = BindInt32(nameof(FieldExitSoundVolume), 100);
                FieldNpcSounds = BindBoolean(nameof(FieldNpcSounds), true);
                FieldNpcSoundVolume = BindInt32(nameof(FieldNpcSoundVolume), 100);
                FieldInteractableSounds = BindBoolean(nameof(FieldInteractableSounds), true);
                FieldInteractableSoundVolume = BindInt32(nameof(FieldInteractableSoundVolume), 100);
                ControllerNaming = BindString(nameof(ControllerNaming), "Xbox");
                ControllerMenuButton = BindString(nameof(ControllerMenuButton), "R3");
                CageMinigameAudioFeedback = BindBoolean(nameof(CageMinigameAudioFeedback), true);
                CageMinigameSkipEnabled = BindBoolean(nameof(CageMinigameSkipEnabled), true);
                CageMinigameFeedbackInterval = BindInt32(nameof(CageMinigameFeedbackInterval), 500);
                CageMinigameUseTones = BindBoolean(nameof(CageMinigameUseTones), true);
            }
        }
    }
}
