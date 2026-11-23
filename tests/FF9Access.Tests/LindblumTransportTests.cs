using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Memoria.Accessibility.Navigation;

namespace FF9Access.Tests
{
    // Installed event roots and everything they request, encoded by Export-FF9ExitNativeCases.py.
    internal static class LindblumTransportTests
    {
        public static void AfterTheAttackTheFountainContactIsAStoryActionBeforeTheChoice()
        {
            var native = Load(1307, 4, 3);
            var snapshot = Snapshot(5670, 3);
            var start = native.Function(native.Sid, native.Tag, snapshot);
            var beforeChoice = ScriptStoryAnalyzer.Read(start, null, native.Op("MAPJUMP"), native.Op("WMAPJUMP"), native.Requests);
            TestAssert.False(beforeChoice.Milestones.Contains(5680));

            using var review = JsonDocument.Parse(ReadRelative("docs/accessibility/story-action-chains.json"));
            var row = review.RootElement.GetProperty("steps").EnumerateArray().SingleOrDefault(step =>
                step.GetProperty("id").GetString() == "lindblum-siege-reviewed-fountain-contact");
            if (row.ValueKind == JsonValueKind.Undefined)
                throw new InvalidOperationException("The story has no reviewed action before the native choice.");

            var effect = row.GetProperty("effects").EnumerateArray().Single();
            var evidence = ScriptStoryAnalyzer.Read(native.Function(native.Sid, native.Tag, Snapshot(5670, 3)), null,
                native.Op("MAPJUMP"), native.Op("WMAPJUMP"), native.Requests, null, native.Op("UCOFF"),
                native.Op("UCON"), null, native.Op("REQ"), new[] { 36 });
            var step = new StoryStep
            {
                Field = row.GetProperty("field").GetInt32(), Sid = native.Sid, Uid = native.Sid,
                Floor = row.GetProperty("floor").GetInt32(), Ceiling = row.GetProperty("ceiling").GetInt32(),
                Milestone = 5680, Kind = StoryStepKind.Talk, Hash = row.GetProperty("hash").GetString(), Source = "reviewed-prefix",
                Effects = new[] { new StoryStepEffect
                {
                    Sid = native.Sid, Uid = native.Sid, Tag = native.Tag, Offset = effect.GetProperty("offset").GetInt32(),
                    Proof = effect.GetProperty("proof").GetString(), Hash = effect.GetProperty("hash").GetString()
                } }
            };
            string EntryHash(int sid) => sid == native.Sid ? native.Hash : null;
            var catalog = new StoryStepCatalog(new[] { step });
            var candidate = catalog.Candidates(1307, 5670, 4, native.Hash, EntryHash, _ => null, null, 4).Single();
            TestAssert.True(StoryStepCatalog.HasEffect(candidate, evidence, EntryHash));
            TestAssert.Equal(0, catalog.Candidates(1307, 5680, 4, native.Hash, EntryHash, _ => null, null, 4).Count);
        }

        public static void BaseLevelLiftsPreserveBothOfferedFloorsAndCancel()
        {
            CheckMenu(603, 3, 3, 3700, 37, 128, false, 601);
            CheckMenu(2153, 3, 3, 9370, 38, 128, false, 2151);
        }

        public static void CastleLiftRetainsSameFieldTravelAndTheBaseLevel()
        {
            CheckMenu(601, 5, 2, 3700, 35, 200, true, 603);
            CheckMenu(601, 5, 2, 3700, 39, 200, true, 603);
            CheckMenu(2151, 3, 3, 9370, 36, 200, true, 2153);
            CheckMenu(2151, 3, 3, 9370, 40, 200, true, 2153);
            var wrongSide = Read(Load(2151, 3, 3), 9370, 36, 128);
            TestAssert.Equal(FieldExitPathKind.NoJump, wrongSide.Kind);
            var moving = Read(Load(601, 5, 2), 3700, 35, 200, mapByte34: 1);
            TestAssert.Equal(FieldExitPathKind.NoJump, moving.Kind);
        }

        public static void HarborBoardingPreservesTheCurrentBranchAndCancel()
        {
            var harbor = Load(2173, 8, 3);
            foreach (bool pending in new[] { false, true })
            {
                var proof = Read(harbor, 9370, 42, 128, atePending: pending);
                TestAssert.Equal(FieldExitPathKind.ChoiceSingle, proof.Kind);
                TestAssert.True(proof.UsesChoice && proof.CanDecline);
                TestAssert.True(proof.Destinations.SetEquals(new[] { pending ? 2105 : 2855 }));
                TestAssert.Equal(FieldExitAdmission.Service, FieldExitAdmissions.Admit("Harbor boarding", proof, true));
            }
            var busy = Read(harbor, 9370, 42, 128, busy: true);
            TestAssert.Equal(FieldExitPathKind.Unknown, busy.Kind);
            TestAssert.Equal(FieldExitAdmission.None, FieldExitAdmissions.Admit("Harbor boarding", busy, true));
        }

        public static void ReturnTrolleysPreserveNativeFloorGatesAndCancel()
        {
            CheckMenu(602, 14, 2, 3700, 42, 192, false, 603, 605);
            CheckMenu(605, 6, 2, 3700, 45, null, false, 602, 603);
            CheckMenu(2152, 12, 2, 9370, 42, 192, false, 2153, 2155);
            CheckMenu(2155, 4, 2, 9370, 45, null, false, 2152, 2153);
            var unproven = Read(Load(602, 14, 2), 3700, 42, 192, floor: null);
            TestAssert.Equal(FieldExitPathKind.Unknown, unproven.Kind);
            TestAssert.Equal(FieldExitAdmission.None, FieldExitAdmissions.Admit("Castle trolley", unproven));
        }

        private static void CheckMenu(int field, int sid, int tag, int counter, int entry, int? direction, bool local, params int[] destinations)
        {
            var native = Load(field, sid, tag);
            var proof = Read(native, counter, entry, direction);
            if (proof.Kind != FieldExitPathKind.ChoiceMenu)
                throw new InvalidOperationException($"Field {field} produced {proof.Kind} at {proof.Stop ?? "end of proof"}.");
            TestAssert.True(proof.UsesChoice && proof.CanDecline);
            TestAssert.Equal(local, proof.InFieldTransfer);
            TestAssert.True(proof.Destinations.SetEquals(destinations));
            string service = FieldExitFunctions.Function(field, sid, native.Hash, tag == 3);
            TestAssert.Equal(FieldExitAdmission.Service, FieldExitAdmissions.Admit(service, proof, tag == 3));
        }

        private static FieldExitPathEvidence Read(NativeCase native, int counter, int entry, int? direction, int? floor = 2,
            bool atePending = false, bool busy = false, int mapByte34 = 0)
        {
            var point = TestFixtures.P(10, 10);
            return FieldExitPaths.Read(
                () => native.Function(native.Sid, native.Tag, Snapshot(counter, entry, atePending, mapByte34)),
                (uid, tag, caller, snapshot) => native.Function(uid == 255 ? caller / 256 : uid, tag, snapshot.WithInstance(new byte[256])),
                native.Op("MAPJUMP"), native.Op("WMAPJUMP"), native.Requests,
                (uid, prop) => FieldExitDestinations.PlayerProperty(native.Player, point, uid, prop, direction),
                _ => false, native.Op("UCOFF"), native.Op("UCON"), new[] { native.Op("MOVE"), native.Op("MOVQ") },
                new FieldExitPathOptions
                {
                    AsyncRequest = native.Op("REQ"), PlayerUid = native.Player, Requestable = (uid, level, caller) => !busy,
                    DialogueOpcodes = new[] { native.Op("MES"), native.Op("MESN"), native.Op("MESA"), native.Op("MESAN"), native.Op("MESB") },
                    CalculateExitPosition = native.Op("MJPOS"), ExitPosition = point, Angle2 = (x, z) => 128, ObjectFloor = _ => floor,
                    Sine = angle => (int)(MathF.Sin(angle / 4096f * 360f * 0.0174532924f) * 4096f),
                    Cosine = angle => (int)(MathF.Cos(angle / 4096f * 360f * 0.0174532924f) * 4096f),
                    TrackInFieldTransfer = native.Field == 601 || native.Field == 2151,
                    VisualOnlyRequest = call => native.Field == 2173 && call.Identity == 9 * 256 + 15 && call.ScriptId == 9
                });
        }

        private static ScriptSnapshot Snapshot(int counter, int entry, bool atePending = false, int mapByte34 = 0)
        {
            var globals = new byte[2048];
            globals[0] = (byte)counter;
            globals[1] = (byte)(counter >> 8);
            globals[2] = (byte)entry;
            globals[3] = (byte)(entry >> 8);
            if (atePending) globals[3616 / 8] |= 1;
            var map = new byte[1024];
            map[17] = 255;
            map[34] = (byte)mapByte34;
            return new ScriptSnapshot(globals, map, new byte[256]) { UserControl = true };
        }

        private sealed class NativeCase
        {
            internal int Field, Sid, Player, Tag;
            internal string Hash;
            internal Dictionary<string, int> Opcodes;
            internal Dictionary<string, List<ScriptInstruction>> Functions;
            internal int Op(string name) => Opcodes[name];
            internal int[] Requests => new[] { Op("REQ"), Op("REQSW"), Op("REQEW") };

            internal ScriptStoryFunction Function(int uid, int tag, ScriptSnapshot snapshot)
            {
                if (!Functions.TryGetValue(uid + ":" + tag, out var code)) return null;
                int actor = uid == 250 ? Player : uid;
                return new ScriptStoryFunction { Identity = actor * 256 + tag, ScriptId = actor, Tag = tag, Code = code, Snapshot = snapshot };
            }
        }

        private static NativeCase Load(int field, int sid, int tag)
        {
            using var document = JsonDocument.Parse(ReadRelative("tests/fixtures/lindblum-services.json"));
            var row = document.RootElement.GetProperty("cases").EnumerateArray().Single(item =>
                item.GetProperty("field").GetInt32() == field && item.GetProperty("sid").GetInt32() == sid && item.GetProperty("tag").GetInt32() == tag);
            return new NativeCase
            {
                Field = field, Sid = sid, Tag = tag, Player = row.GetProperty("player").GetInt32(), Hash = row.GetProperty("hash").GetString(),
                Opcodes = document.RootElement.GetProperty("opcodes").EnumerateObject().ToDictionary(item => item.Name, item => item.Value.GetInt32()),
                Functions = row.GetProperty("functions").EnumerateObject().ToDictionary(item => item.Name,
                    item => item.Value.EnumerateArray().Select(Instruction).ToList())
            };
        }

        private static ScriptInstruction Instruction(JsonElement row)
        {
            var instruction = new ScriptInstruction(row.GetProperty("offset").GetInt32(), row.GetProperty("next").GetInt32(), row.GetProperty("opcode").GetInt32())
            {
                Arguments = row.GetProperty("args").EnumerateArray().Select(value => value.ValueKind == JsonValueKind.Number ? (int?)value.GetInt32() : null).ToArray(),
                ArgumentExpressions = row.GetProperty("exprs").EnumerateArray().Select(value => value.ValueKind == JsonValueKind.String ? Convert.FromHexString(value.GetString()) : null).ToArray()
            };
            instruction.Argument = instruction.Constant(0);
            instruction.Expression = instruction.ArgumentExpression(0);
            return instruction;
        }

        private static string ReadRelative(string name)
        {
            foreach (string origin in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
                for (var directory = new DirectoryInfo(origin); directory != null; directory = directory.Parent)
                {
                    string path = Path.Combine(directory.FullName, name);
                    if (File.Exists(path)) return File.ReadAllText(path);
                }
            throw new FileNotFoundException(name);
        }
    }
}
