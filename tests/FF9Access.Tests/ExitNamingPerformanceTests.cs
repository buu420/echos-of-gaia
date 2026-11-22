using System;
using System.Collections.Generic;
using System.Diagnostics;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class ExitNamingPerformanceTests
    {
        public static void CastleSizedExitCatalogKeepsEveryApproachWithinAnAllocationBudget()
        {
            var context=TestFixtures.Context();
            var catalog=new FieldTargetCatalog();
            var observations=new List<FieldTargetObservation>();
            var routes=new Dictionary<TargetIdentity,FieldExitRoute>();
            int[] sizes={490,445,308};
            for(int door=0;door<sizes.Length;door++) {
                var points=new NavigationPoint[sizes[door]];
                for(int i=0;i<points.Length;i++)points[i]=new NavigationPoint(door*1000+i,5,i*.5f);
                var id=new TargetIdentity(TargetKind.FieldExit,context.LogicalMapKey,door+5);
                observations.Add(new FieldTargetObservation{Id=id,NativePresentationIdentity=door+100,
                    Position=points[0],VisibleLabel="Upper exit",ContainsJump=true,
                    Exit=new ExitPresentation{Visible=true,ApproachEnabled=true,Approach=points[0],ApproachCandidates=points}});
                var route=new FieldExitRoute{Presentation=door+100};
                route.Approaches[100+door]=(NavigationPoint[])points.Clone();
                route.Names[100+door]="Castle room "+(door+1);
                routes.Add(id,route);
            }
            FieldExitRoutes.Publish(context.LogicalMapKey,context.Generation,routes);
            catalog.Build(context,observations,null); // warm the code and dictionaries
            long before=GC.GetAllocatedBytesForCurrentThread();var timer=Stopwatch.StartNew();
            CatalogResult result=null;
            for(int pass=0;pass<3;pass++)result=catalog.Build(context,observations,null);
            timer.Stop();long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
            Console.WriteLine("Castle exit naming: 3 builds; ms="+timer.ElapsedMilliseconds+"; allocated="+allocated);
            TestAssert.Equal(3,result.Targets.Count);
            for(int door=0;door<3;door++) {
                var target=result.Targets[door];
                TestAssert.Equal("Exit to Castle room "+(door+1),target.Label);
                TestAssert.Equal(sizes[door],target.ApproachCandidates.Length);
                TestAssert.Equal(100+door,target.NextFields[0]);
                for(int i=0;i<sizes[door];i++)TestAssert.Equal(observations[door].Exit.ApproachCandidates[i],target.ApproachCandidates[i]);
            }
            if(allocated>=2000000)throw new Exception("Stationary exit refreshes allocated "+allocated+" bytes for 3 builds; budget is 2 MB");
        }
        public static void PointEqualityPreservesInstalledMonoSemantics()
        {
            var a=new NavigationPoint(0f,5f,2f);var b=new NavigationPoint(-0f,5f,2f);
            TestAssert.True(a.Equals(b));TestAssert.Equal(a.GetHashCode(),b.GetHashCode());
            TestAssert.True(new HashSet<NavigationPoint>{a}.Contains(b));
            TestAssert.True(new List<NavigationPoint>{a}.Contains(b));
            TestAssert.False(a.Equals(new NavigationPoint(0,6,2)));
            TestAssert.False(new NavigationPoint(0,5,float.NaN).Equals(new NavigationPoint(0,5,float.NaN)));
            TestAssert.False(a.Equals((object)null));TestAssert.False(a.Equals((object)"point"));
        }
    }
}
