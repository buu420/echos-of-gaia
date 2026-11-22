using System;
using System.Linq;
using System.IO;
using System.Text.Json;
using Memoria.Accessibility.Navigation;
internal static partial class Program
{
    private static void IceLedgeCases()
    {
        // Read the resource actually embedded in the native loader. The hashes
        // are checked separately against the installed event bytes by the audit.
        var assembly=typeof(NativeStorySteps).Assembly;
        using(var stream=assembly.GetManifestResourceStream("Memoria.Accessibility.StoryActionChains.json"))
        using(var json=JsonDocument.Parse(stream)) {
            var phases=json.RootElement.GetProperty("phases").EnumerateArray().Where(p=>p.GetProperty("id").GetString().StartsWith("ice-ledges-reviewed-")).ToArray();
            string Hash(int field)=>phases.SelectMany(p=>p.GetProperty("rooms").EnumerateArray()).First(r=>r.GetProperty("field").GetInt32()==field).GetProperty("directorHash").GetString();
            Func<int,string> first=s=>s==0?Hash(301):null;
            var lower=NativeStorySteps.SelectPhase(2505,301,first,p=>p.ActiveTriangles.Contains(136));
            Check(lower!=null && lower.GoalFields.SequenceEqual(new[]{301}) && lower.ActionUids.SequenceEqual(new[]{7}),"Ice Cavern lower path offers the native climb before its disconnected upper exit");
            var upper=NativeStorySteps.SelectPhase(2505,301,first,p=>p.ActiveTriangles.Contains(13));
            Check(upper!=null && upper.GoalFields.SequenceEqual(new[]{302}) && upper.ActionUids.Length==0,"the first Ice Cavern climb hands Story to the upper exit");
            var shelf=NativeStorySteps.SelectPhase(2505,301,first,p=>p.ActiveTriangles.Contains(26));
            Check(shelf!=null && shelf.ActionUids.SequenceEqual(new[]{10,12}),"a side shelf offers a climb back to the main path");
            Func<int,string> last=s=>s==0?Hash(310):null;
            var finalLower=NativeStorySteps.SelectPhase(2525,310,last,p=>p.ActiveTriangles.Contains(23));
            Check(finalLower!=null && finalLower.ActionUids.SequenceEqual(new[]{2}),"the final Ice Path offers its climbing ledge");
            var finalUpper=NativeStorySteps.SelectPhase(2525,310,last,p=>p.ActiveTriangles.Contains(4));
            Check(finalUpper!=null && finalUpper.GoalFields.SequenceEqual(new[]{311}) && finalUpper.ActionUids.Length==0,"the final ledge hands Story to the upper exit");
            var unknown=NativeStorySteps.SelectPhase(2525,310,last);
            Check(unknown!=null && unknown.InformationOnly,"unknown Ice Path floor never invents a jump route");
        }
    }
}
