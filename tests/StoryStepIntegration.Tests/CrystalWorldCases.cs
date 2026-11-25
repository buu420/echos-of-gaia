using System;
using System.Linq;
using Memoria.Accessibility.Navigation;

internal static partial class Program
{
    private static void CrystalWorldCases()
    {
        const int field = 2926;
        const int approachUid = 6;
        const int playerUid = 8;
        const int start = 11765;
        const int complete = 11770;
        const string directorHash = "bce4b859ebaf8f33447aa980f0120e229a383c6b94cd2803cab5310da68f7d7b";
        const string approachHash = "b3b2e54e6deaa4e1dfc61754d7bc05be0407e1fe794a1816035167aaf064d4b3";
        const string playerHash = "c6578108f66bd87234d500407e2548ae321d24ea001497ae8392219c36b69e7c";
        var engine = new EventEngine();
        foreach (int uid in new[] { 0, approachUid, playerUid })
            engine.Objects[uid] = new Obj { sid = uid, uid = uid };
        Func<int, string> hashes = sid => sid == 0 ? directorHash : sid == approachUid ? approachHash : sid == playerUid ? playerHash : null;

        SetCounter(start);
        NativeStorySteps.RefreshTransitState();
        var step = NativeStorySteps.Candidates(engine, field, start, approachUid, approachHash, hashes, playerUid, uid: approachUid)
            .SingleOrDefault(candidate => candidate.Source == "reviewed-prefix");
        var currentPrefix = CrystalWorldApproachPrefix(true);
        Check(StoryStepCatalog.HasEffect(step, currentPrefix, hashes) && currentPrefix.Milestones.Count == 0,
            "Crystal World offers the active walk-in before any native choice or battle result");
        Check(!StoryStepCatalog.HasEffect(step, CrystalWorldApproachPrefix(false), hashes),
            "the Crystal World walk-in prefix cannot fire while native control is unavailable");
        Check(!NativeStorySteps.Candidates(engine, field, start, approachUid, approachHash, hashes, 4, uid: approachUid)
            .Any(candidate => candidate.Source == "reviewed-prefix"),
            "the Crystal World walk-in belongs to the controlled player");

        var phase = NativeStorySteps.SelectPhase(start, field, hashes);
        Check(phase?.GoalFields.SequenceEqual(new[] { field }) == true && phase.ActionUids.SequenceEqual(new[] { approachUid }),
            "Crystal World Story selects the current walk-in instead of the separate final gate");
        Check(NativeStorySteps.SelectPhase(start, field, sid => sid == 0 ? "different script" : hashes(sid))?.InformationOnly == true,
            "unknown Crystal World director bytes provide information without a route");

        SetCounter(complete);
        NativeStorySteps.RefreshTransitState();
        Check(!NativeStorySteps.Candidates(engine, field, complete, approachUid, approachHash, hashes, playerUid, uid: approachUid)
            .Any(candidate => candidate.Source == "reviewed-prefix"),
            "the Crystal World walk-in retires after the native counter changes");
        var after = NativeStorySteps.SelectPhase(complete, field, hashes);
        Check(after?.GoalFields.SequenceEqual(new[] { 2927 }) == true && after.ReviewedLinks.Any(link =>
            link.From == field && link.To == 2927 && link.ActionUid == 5),
            "Crystal World Story keeps the separately presented final gate after the walk-in scene");
    }

    private static ScriptStoryEvidence CrystalWorldApproachPrefix(bool control)
    {
        // Installed field 2926, SID 6, tag 2. The user-control branch
        // reaches the opening assignment at 38 and then the wait at 73.
        var function = new ScriptStoryFunction {
            Identity = 6 * 256 + 2,
            ScriptId = 6,
            Tag = 2,
            Snapshot = new ScriptSnapshot(null, new byte[1024], null) { UserControl = control },
            Code = new[] {
                new ScriptInstruction(30, 34, 5) { Expression = new byte[] { 0x7a, 2, 0x7f } },
                new ScriptInstruction(34, 37, 3) { Argument = 1 },
                new ScriptInstruction(37, 38, 4),
                new ScriptInstruction(38, 46, 5) { Expression = new byte[] { 0xc5, 158, 0x7d, 0, 0, 44, 0x7f } },
                new ScriptInstruction(46, 54, 5) { Expression = new byte[] { 0xc5, 159, 0x7d, 1, 0, 32, 0x7f } },
                new ScriptInstruction(54, 57, 2) { Argument = 19 },
                new ScriptInstruction(57, 58, 45),
                new ScriptInstruction(58, 66, 5) { Expression = new byte[] { 0xc5, 144, 0x7d, 0, 0, 32, 0x7f } },
                new ScriptInstruction(66, 69, 2) { Argument = 4 },
                new ScriptInstruction(69, 70, 171),
                new ScriptInstruction(70, 73, 1) { Argument = 3 },
                new ScriptInstruction(73, 76, 34)
            }
        };
        function.Snapshot.Write(1, 1, 159, 1);
        return ScriptStoryAnalyzer.Read(function, null, 43, 182, new[] { 16, 18, 20 }, null, 45, 46, null, 16, new[] { 18, 20, 34 });
    }
}
