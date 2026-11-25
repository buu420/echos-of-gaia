using System.Linq;
using Memoria.Accessibility.Navigation;

namespace FF9Access.Tests
{
    internal static class PinnacleRocksWalkwayTests
    {
        public static void OnlyWallsOfTheOriginalPinnacleRampUseReviewedClearance()
        {
            var mesh = new FieldGeometrySnapshot {
                Radius = 80,
                Triangles = Enumerable.Range(0, 115).Select(id => new FieldTriangle { Id = id }).ToArray()
            };
            mesh.Find(7).Neighbors[0] = 6;
            PinnacleRocksWalkways.Review(1252, "altered walkmesh", mesh);
            TestAssert.Equal(0, mesh.Find(7).ReviewedBoundaryEdges);

            PinnacleRocksWalkways.Review(1252,
                "96d2e3006185ce7cdf6f3ed5b7ea8e2d9f6cb8e7af98cfa6cb4f393dbe37df1b", mesh);
            TestAssert.Equal(6, mesh.Find(7).ReviewedBoundaryEdges);
            TestAssert.Equal(64f, mesh.Find(7).ReviewedBoundaryClearance);
            TestAssert.Equal(0, mesh.Find(38).ReviewedBoundaryEdges);
            TestAssert.Equal(80f, mesh.Radius);
        }
    }
}
