using System;
using System.Globalization;

namespace Memoria.Accessibility.AudioDescriptions
{
    internal static class ChocographPictureDescriptions
    {
        // Descriptions of the 24 original chocograph_map sprites in the Steam
        // Chocograph Atlas. Include only what appears inside the selected picture.
        private static readonly string[] Pictures =
        {
            "A river joins the sea through a narrow cut in a green bank. A pale beach curves left beneath a hillside.",
            "Steep green mountains frame a grassy valley. On the right, blue water curves around a pale rocky point.",
            "Seen from above, a narrow river curves along the right side of green lowlands. Dark ridges cross the grass diagonally.",
            "A long pale beach crosses the foreground, with surf below and a flat grassy strip between mountains behind it.",
            "An inlet opens toward the sea on the right. A long brown headland points into it from the left, above a sandy inner shore.",
            "A bare gray headland faces the open sea. A darker brown patch covers the near side and a thin pale shore runs along the right.",
            "Burnt-orange land makes a sharp fork into the blue water. A thin rocky finger reaches toward the upper right.",
            "Several flat brown land fingers enclose a blue channel. A narrow strip across the back nearly closes the view of the sea.",
            "A broad blue cove fills most of the picture. Brown rock curves in from the right, with a long thin shelf in the distance.",
            "Seen from above, a green wedge of land with gray cliff edges projects into dark blue water on two sides.",
            "A green headland with a pale cliff fills the right foreground. Two smaller pieces of land sit across the open water.",
            "Blue water curves around a green peninsula on the right, with a second small grass point at the lower edge.",
            "Pale gray mountain walls nearly enclose a dark green hollow. The view looks down into the opening.",
            "A blue bay edged with white surf lies beyond a grassy valley. A dark winding channel crosses the scene below it.",
            "Seen from above, an orange-brown mass of rock forms several rounded lobes. A dark green surface surrounds the far edges.",
            "A high brown ridge with a jagged crest stands above a strip of green ground and blue sea.",
            "Narrow orange rock fingers extend over dark green-blue water from a long ledge along the upper edge.",
            "Almost the entire view is textured blue water. A thin pale horizon runs across the top.",
            "Pale gray shelves and broad rounded points reach into a winding blue channel, with a short blunt spur in the middle.",
            "The foreground is open blue water, with a grassy shore at the far left and a small low island near the far right.",
            "A small isolated brown island with several lobes stands in open water. Its far edge rises in a darker rounded ridge.",
            "Dry brown bluffs fill the foreground. Flat rock bars sit across a narrow stretch of sea under a blue sky.",
            "A low narrow brown island rises to a dark pointed peak. The sea surrounds it, with a rock neck entering from the lower left.",
            "A sweeping brown headland curves toward a series of low cliffs. Blue water surrounds the point, and the near side has a rounded inlet."
        };

        internal static string Read(int row, string spriteName, bool displayed)
        {
            if (!displayed || row < 0 || row >= Pictures.Length ||
                !String.Equals(spriteName, "chocograph_map_" + row.ToString("D2", CultureInfo.InvariantCulture), StringComparison.Ordinal))
                return null;
            return Pictures[row];
        }
    }
}
