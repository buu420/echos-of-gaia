"""Opcode table extracted read-only from the source tree (EBin.cs enum + EventEngineUtils arg tables)."""
import re
from pathlib import Path

from audit_paths import SOURCE_ROOT as SRC


def load():
    decoder = (SRC / 'Assembly-CSharp/Global/Event/Engine/EventEngineUtils.cs').read_text(encoding='utf-8-sig')
    counts = [int(x) for x in re.findall(r'-?\d+', re.search(r'static SByte\[\] opArgCount.*?\{(.*?)\};', decoder, re.S).group(1))]
    sizes = []
    size_text = re.search(r'static Byte\[\]\[\] opArgSize.*?\{(.*?)\n\s*\};', decoder, re.S).group(1)
    for m in re.finditer(r'null|new Byte\[\]\{([^}]*)\}', size_text):
        sizes.append(None if m.group(0) == 'null' else [int(v) for v in re.findall(r'\d+', m.group(1))])
    enum_text = re.search(r'public enum event_code_binary\s*\{(.*?)\n\s*\}',
                          (SRC / 'Assembly-CSharp/Global/EBin.cs').read_text(encoding='utf-8-sig'), re.S).group(1)
    names = {}
    n = 0
    for tok in enum_text.split(','):
        tok = re.sub(r'//[^\n]*', '', tok).strip()
        if not tok:
            continue
        p = tok.split('=')
        name = p[0].strip()
        if len(p) > 1:
            n = int(p[1].strip(), 0)
        names[name] = n
        n += 1
    return counts, sizes, names


COUNTS, SIZES, NAMES = load()
BYNUM = {v: k for k, v in NAMES.items()}

if __name__ == '__main__':
    for k, v in sorted(NAMES.items(), key=lambda kv: kv[1]):
        print(hex(v), k, COUNTS[v] if v < len(COUNTS) else '?', SIZES[v] if v < len(SIZES) else '?')
