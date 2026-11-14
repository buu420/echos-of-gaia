using System.Collections.Generic;

namespace Memoria.Accessibility.Navigation
{
    public static class WorldEntranceScripts
    {
        internal const int NoPlace = 0;
        public static int EventTag(int cell, int terrain)
        {
            return 0x8000 | ((cell / 48) << 8) | ((cell % 48) << 2) | ((terrain & 0xc000) >> 14);
        }

        // The director passes M5[39] to entry 1, tag 11 to present the native
        // place prompt. Inspect that request on the active branch without running it.
        public static int? PresentedPlace(IList<ScriptInstruction> instructions, ScriptSnapshot snapshot, int requestOpcode)
        {
            if (instructions.Count == 0)
                return null;

            int offset = instructions[0].Offset;
            int? value = null;
            for (int step = 0; step < 48; step++)
            {
                ScriptInstruction current = null;
                foreach (var instruction in instructions)
                    if (instruction.Offset == offset) { current = instruction; break; }
                if (current == null)
                    return null;
                if (current.Opcode == 4)
                    return NoPlace;

                offset = current.NextOffset;
                switch (current.Opcode)
                {
                    case 5:
                        if (!ExitScriptAnalyzer.TryExpression(current.Expression, snapshot, out value) || !value.HasValue)
                            return null;
                        break;
                    case 1:
                    case 2:
                    case 3:
                        if (!current.Argument.HasValue || (current.Opcode != 1 && !value.HasValue))
                            return null;
                        if (current.Opcode == 1 || (current.Opcode == 2 ? value == 0 : value != 0))
                            offset += current.Opcode == 2 ? current.Argument.Value : (short)current.Argument.Value;
                        break;
                    case 6:
                    case 11:
                    case 13:
                        int? branch = ScriptBranches.SelectSwitch(current, value);
                        if (!branch.HasValue)
                            return null;
                        offset = branch.Value;
                        break;
                    default:
                        return current.Opcode == requestOpcode && current.Constant(0) == 6 &&
                            current.Constant(1) == 1 && current.Constant(2) == 11 ? snapshot.Read(1, 5, 39) : null;
                }
            }
            return null;
        }
    }
}
