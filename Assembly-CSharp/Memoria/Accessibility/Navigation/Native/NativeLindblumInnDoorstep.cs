namespace Memoria.Accessibility.Navigation
{
    internal static class NativeLindblumInnDoorstep
    {
        public const float StreetHeight = 383;
        // With the player restrictions active, the stock inn trigger sits against a
        // blocked floor. The player's 80-unit body touches the entrance before its
        // center can reach it. Give this door up to 40 units of street-side contact.
        private const float MaxContact = 40;
        private static readonly NavigationPoint[] NativeRegion =
        {
            new NavigationPoint(-3547, StreetHeight, 3281),
            new NavigationPoint(-3606, StreetHeight, 3106),
            new NavigationPoint(-2999, StreetHeight, 2958),
            new NavigationPoint(-2973, StreetHeight, 3163)
        };
        internal static readonly NavigationPoint[] ApproachRegion =
        {
            NativeRegion[0], NativeRegion[1],
            new NavigationPoint(NativeRegion[2].X + MaxContact, StreetHeight, NativeRegion[2].Z),
            new NavigationPoint(NativeRegion[3].X + MaxContact, StreetHeight, NativeRegion[3].Z)
        };

        internal static bool Contains(float x, float z)
        {
            var near = NativeRegion[2];
            var far = NativeRegion[3];
            if (z <= near.Z || z >= far.Z) return false;
            float front = near.X + (far.X - near.X) * (z - near.Z) / (far.Z - near.Z);
            return x >= front && x < front + MaxContact;
        }

        internal static bool Available(int map, FieldMap field, Quad quad)
        {
            int expectedSid;
            string mesh;
            switch (map)
            {
                case 552:
                    expectedSid = 21;
                    mesh = "13bb4136104810147f70227efe5fc701fc83865b457e5bee784f4223f8468561";
                    break;
                case 1302:
                    expectedSid = 12;
                    mesh = "16d3af152c597ae934491884c31c567a64aa20dcdbef7a18cc8448b1b68a261d";
                    break;
                case 2102:
                    expectedSid = 9;
                    mesh = "73fa9bf84f78ea5697b1563d5ae899f0f9bdd3dbad086e8443cf18e9e247d9d6";
                    break;
                default:
                    return false;
            }
            if (quad.sid != expectedSid || quad.uid != expectedSid || quad.n != NativeRegion.Length ||
                quad.q == null || quad.q.Length < NativeRegion.Length || quad.ebData == null ||
                field == null || field.bgi == null || field.bgi.attributeMask != 255)
                return false;
            for (int i = 0; i < NativeRegion.Length; i++)
                if (quad.q[i] == null || quad.q[i].X != NativeRegion[i].X || quad.q[i].Z != NativeRegion[i].Z)
                    return false;
            return FieldExitDestinations.RequiresInwardApproach(map, quad.sid, quad.uid, NativeFieldTargets.Fingerprint(quad.ebData)) &&
                NativeFieldTargets.CurrentBgiHash(field) == mesh;
        }
    }
}
