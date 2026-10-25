"""Read-only census of every installed English FFIX field. Writes only to --output-root.

The comparison classes describe the baseline exit-reader policy, not live
visibility or a promise of complete game-state coverage. Raw research outputs
contain disassembly and potential destinations; they are never runtime targets.
Requires UnityPy (or --unitypy-root pointing at a separate installed copy).
"""
import argparse
import os
import runpy
import sys
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--game-root', required=True, type=Path)
    parser.add_argument('--output-root', required=True, type=Path)
    parser.add_argument('--unitypy-root', type=Path)
    args = parser.parse_args()
    game = args.game_root.resolve()
    output = args.output_root.resolve()
    if game == output or game in output.parents or output in game.parents:
        parser.error('Use an output directory separate from the game and its parent directories')
    if not (game / 'StreamingAssets/p0data7.bin').is_file():
        parser.error('The installed event bundle was not found')
    output.mkdir(parents=True, exist_ok=True)
    os.environ['FF9_AUDIT_GAME_ROOT'] = str(game)
    os.environ['FF9_AUDIT_OUTPUT_ROOT'] = str(output)
    if args.unitypy_root:
        os.environ['FF9_AUDIT_UNITYPY_ROOT'] = str(args.unitypy_root.resolve())
    else:
        os.environ.pop('FF9_AUDIT_UNITYPY_ROOT', None)
    helpers = Path(__file__).resolve().parent / 'field_audit'
    sys.path.insert(0, str(helpers))
    runpy.run_path(str(helpers / 'census.py'), run_name='__main__')
    runpy.run_path(str(helpers / 'classify.py'), run_name='__main__')
    runpy.run_path(str(helpers / 'coverage.py'), run_name='__main__')


if __name__ == '__main__':
    main()
