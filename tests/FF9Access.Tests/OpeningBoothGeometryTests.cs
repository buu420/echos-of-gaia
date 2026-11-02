using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class OpeningBoothGeometryTests
    {
        private static NavigationPoint Point(JsonElement v){var a=v.EnumerateArray().Select(n=>n.GetSingle()).ToArray();return new NavigationPoint(a[0],a[1],a[2]);}
        private static NavigationPoint[] Points(JsonElement v){return v.EnumerateArray().Select(Point).ToArray();}
        public static void NativeSquareEntranceHasAClippedWalkingApproachToTheBooth()
        {
            using(var json=JsonDocument.Parse(Assembly.GetExecutingAssembly().GetManifestResourceStream("Opening.BoothGeometry.json"))) {
                var root=json.RootElement;TestAssert.Equal(OpeningTicketBoothEvidence.Walkmesh,root.GetProperty("WalkmeshSha256").GetString());
                var triangles=root.GetProperty("Triangles").EnumerateArray().Select(t=>{
                    int flags=t.GetProperty("Flags").GetInt32(),normalIndex=t.GetProperty("NormalIndex").GetInt32();var normal=Point(t.GetProperty("Normal"));var vertices=Points(t.GetProperty("Vertices"));
                    return FieldNativeGeometryContract.CreateTriangle(t.GetProperty("Id").GetInt32(),t.GetProperty("Floor").GetInt32(),103,new FieldNativeTriangleData{OriginalVertices=vertices,CrossingVertices=vertices,
                        RuntimeNeighbors=t.GetProperty("Neighbors").EnumerateArray().Select(n=>n.GetInt32()).ToArray(),NativeNeighbors=t.GetProperty("Neighbors").EnumerateArray().Select(n=>n.GetInt32()).ToArray(),EdgeClones=t.GetProperty("Clones").EnumerateArray().Select(n=>n.GetInt32()).ToArray(),
                        RuntimeFlags=flags,NativeFlags=flags,NormalIndex=normalIndex,Normal=normal,NormalInverseY=t.GetProperty("NormalInverseY").GetInt32(),Projection=FieldNativeGeometryContract.Projection(normalIndex,normal),Plane=t.GetProperty("Plane").GetInt32(),ThetaX=t.GetProperty("ThetaX").GetInt32(),ThetaZ=t.GetProperty("ThetaZ").GetInt32()});
                }).ToArray();
                TestAssert.True(triangles.All(t=>t!=null));
                foreach(float radius in new[]{20f,40f,80f}) {
                    var geometry=new FieldGeometrySnapshot{Generation=1,Triangles=triangles,Radius=radius,AttributeMask=255,BlockedAttributes=128,Fingerprint="OriginalSquare"};
                    var entrance=FieldRegionApproaches.Interaction(Points(root.GetProperty("Entrance")),geometry);var booth=FieldRegionApproaches.Interaction(OpeningTicketBoothEvidence.Region,geometry);
                    if(entrance.Length==0 || booth.Length==0)throw new Exception("No native region approach at radius"+radius);
                    var context=TestFixtures.Context(OpeningTicketBoothEvidence.Map);context.Position=entrance[0];context.PlayerIdentity=30;context.StoryProgress=1000;
                    var target=new NavigationTarget{Id=new TargetIdentity(TargetKind.FieldActor,context.LogicalMapKey,28),Category=NavigationCategory.Objects,Generation=1,Label="Ticket booth",IsInteractive=true,Position=TestFixtures.P(25,-555.5f),InteractionUid=28,InteractionRegion=OpeningTicketBoothEvidence.Region,ApproachCandidates=booth};
                    var wrapped=StoryTargetCatalog.Wrap(context,1150,target,"Approach the ticket booth");var route=new FieldRoutePlanner().Plan(context,wrapped,geometry);
                    if(!route.Success)throw new Exception("Native entrance-to-booth route failed at radius"+radius+": "+route.Failure);
                    TestAssert.Equal(28,route.PhysicalIdentity.NativeId);
                }
            }
        }
    }
}
