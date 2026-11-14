using System;
using System.Collections.Generic;
using System.Globalization;
using Memoria.Accessibility.AudioDescriptions;

namespace FF9Access.Tests
{
    internal static class ChocographPictureDescriptionTests
    {
        public static void AllOriginalPicturesHaveDistinctVisibleDescriptions()
        {
            var descriptions = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < 24; i++)
            {
                string picture = "chocograph_map_" + i.ToString("D2", CultureInfo.InvariantCulture);
                string description = ChocographPictureDescriptions.Read(i, picture, true);
                TestAssert.True(!String.IsNullOrEmpty(description) && descriptions.Add(description));
            }
        }

        public static void OffscreenOrRetainedPicturesDoNotSpeakAnUnselectedClue()
        {
            TestAssert.True(ChocographPictureDescriptions.Read(2, "chocograph_map_02", false) == null);
            TestAssert.True(ChocographPictureDescriptions.Read(2, "chocograph_map_01", true) == null);
            TestAssert.True(ChocographPictureDescriptions.Read(24, "chocograph_map_24", true) == null);
        }
    }
}
