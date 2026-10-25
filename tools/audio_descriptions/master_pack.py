"""Create a separately validated pack with louder title/new-game narration."""
import argparse
import copy
import json
from pathlib import Path

from build_pack import digest, validate_pack
from mastering import master_pcm, pcm_metrics, reconstructed_peak_dbfs


def master_pack(source, output):
    source, output = Path(source).resolve(), Path(output).resolve()
    if output == source or source in output.parents or output in source.parents:
        raise ValueError('Master into an independent directory')
    if output.exists() and any(output.iterdir()):
        raise ValueError('Master into an empty directory')
    original = validate_pack(source)
    if original.get('openingMastering'):
        raise ValueError('Opening narration was already mastered; start from original recordings')
    revised = copy.deepcopy(original)
    opening = {cue['id'] for film in original['films'] if film['movie'] in ('FMV000', 'FMV001')
               for cue in film['cues']}
    other = {cue['id'] for film in original['films'] if film['movie'] not in ('FMV000', 'FMV001')
             for cue in film['cues']}
    if not opening or opening & other:
        raise ValueError('Opening recordings must have their own clip identities')
    output.mkdir(parents=True, exist_ok=True)
    report = []
    for clip in revised['clips']:
        data = (source / clip['file']).read_bytes()
        if clip['id'] in opening:
            pcm = master_pcm(data[44:], opening=True)
            changed = data[:44] + pcm
            report.append({'id': clip['id'], 'original_sha256': digest(data),
                           'mastered_sha256': digest(changed),
                           'before': pcm_metrics(data[44:]), 'after': pcm_metrics(pcm),
                           'reconstructed_peak_dbfs': reconstructed_peak_dbfs(pcm)})
            data = changed
            clip['sha256'] = digest(data)
        (output / clip['file']).write_bytes(data)
    revised['openingMastering']={'version': 1, 'gainDb': 10, 'limiterCeilingDbfs': -1.8,
                                 'oversamplingHz': 192000, 'movies': ['FMV000', 'FMV001']}
    (output / 'manifest.json').write_text(json.dumps(revised, ensure_ascii=False, indent=1) + '\n', encoding='utf-8')
    validate_pack(output)
    return revised, report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', required=True, type=Path)
    parser.add_argument('--out', required=True, type=Path)
    parser.add_argument('--qa', required=True, type=Path)
    args = parser.parse_args()
    revised, report = master_pack(args.source, args.out)
    args.qa.parent.mkdir(parents=True, exist_ok=True)
    args.qa.write_text(json.dumps(report, indent=1) + '\n', encoding='utf-8')
    print(f"PASS mastered {len(report)} opening recordings; {len(revised['clips'])-len(report)} recordings preserved")


if __name__ == '__main__':
    main()
