#!/usr/bin/env python3
"""Verify an Echos of Gaia release pair offline without extracting or executing it.

Uses only Python's standard library. This checks release integrity and installation
ownership; the core package validator separately checks dependency completeness.
"""
import argparse
from contextlib import ExitStack
import hashlib
import json
from pathlib import Path
import re
import stat
import sys
import zipfile


class VerificationError(ValueError):
    """An archive or release contract is invalid."""


def require(condition, message):
    if not condition:
        raise VerificationError(message)


def safe_path(value, directory=False):
    """Require a canonical relative path safe for Windows extraction."""
    require(isinstance(value, str) and value, 'Invalid empty/non-string path')
    path = value[:-1] if directory and value.endswith('/') else value
    require(path and not path.startswith('/') and '\\' not in path,
            f'Unsafe archive/payload path: {value!r}')
    for part in path.split('/'):
        require(part not in ('', '.', '..') and not part.endswith(('.', ' '))
                and not any(ord(c) < 32 or ord(c) == 127 or c in ':<>"|?*' for c in part)
                and not re.fullmatch(r'(?i)(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\..*)?', part),
                f'Unsafe archive/payload path: {value!r}')
    return path


def digest_stream(stream):
    digest = hashlib.sha256()
    size = 0
    while True:
        block = stream.read(1024 * 1024)
        if not block:
            return digest.hexdigest(), size
        digest.update(block)
        size += len(block)


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result, f'Duplicate JSON key: {key}')
        result[key] = value
    return result


class Archive:
    def __init__(self, path, stack):
        self.name = path.name
        self.zip = stack.enter_context(zipfile.ZipFile(path))
        self.files = {}
        seen = {}
        for entry in self.zip.infolist():
            require(entry.orig_filename == entry.filename,
                    f'Unsafe archive path in {self.name}')
            name = safe_path(entry.filename, directory=entry.is_dir())
            folded = name.casefold()
            require(folded not in seen, f'Duplicate archive path in {self.name}: {name}')
            seen[folded] = (name, entry.is_dir())
            mode = stat.S_IFMT(entry.external_attr >> 16)
            require(mode in (0, stat.S_IFREG, stat.S_IFDIR),
                    f'Unsupported archive path type: {name}')
            require(not entry.flag_bits & 1, f'Encrypted archive entry: {name}')
            if not entry.is_dir():
                try:
                    with self.zip.open(entry) as stream:
                        self.files[name] = digest_stream(stream)
                except (zipfile.BadZipFile, RuntimeError, EOFError, OSError) as error:
                    raise VerificationError(f'Archive CRC/read failure in {self.name}: {name}: {error}') from error
        for name, _ in seen.values():
            parts = name.split('/')
            for end in range(1, len(parts)):
                parent = seen.get('/'.join(parts[:end]).casefold())
                require(parent is None or parent[1], f'Archive path has file as parent: {name}')

    def read(self, path):
        require(path in self.files, f'Missing archive file in {self.name}: {path}')
        return self.zip.read(path)

    def json(self, path):
        try:
            value = json.loads(self.read(path).decode('utf-8-sig'), object_pairs_hook=unique_object)
        except (UnicodeError, json.JSONDecodeError) as error:
            raise VerificationError(f'Invalid JSON metadata {path}: {error}') from error
        require(isinstance(value, dict), f'JSON metadata must be an object: {path}')
        return value


def allowed_payload(path):
    """Reject private inputs, game files, installer helpers and user-owned state."""
    safe_path(path)
    lower = path.casefold()
    parts = lower.split('/')
    suffix = Path(lower).suffix
    banned_suffixes = {'.pfx', '.p12', '.pem', '.key', '.7z', '.mkv', '.mp4', '.movie',
                       '.mov', '.webm', '.avi', '.sav', '.save', '.dat', '.ini', '.bak',
                       '.ps1', '.psm1', '.bat', '.cmd', '.sh', '.py'}
    require(suffix not in banned_suffixes
            and not any(part.startswith(('.env', 'private', 'raw_voice', 'raw-voice',
                                         'reference', 'voice_reference', 'voice-reference'))
                        or part in ('raw', 'research', 'saves', 'save', 'credentials',
                                    'rejected', 'failed-takes') for part in parts)
            and not (suffix == '.exe' and path != 'FF9_Launcher.exe')
            and parts[-1] not in ('unityengine.dll', 'assembly-csharp-firstpass.dll',
                                  'ff9.exe', 'memoria.ini', 'settings.ini'),
            f'Forbidden payload path: {path}')
    require(lower.startswith('accessibility/')
            or path == 'FF9_Launcher.exe'
            or (len(parts) == 1 and suffix in ('.dll', '.config'))
            or (parts[0] in ('x86', 'x64') and suffix == '.dll')
            or path == 'StreamingAssets/Scripts/Memoria.Scripts.dll'
            or (path.startswith('StreamingAssets/Data/')
                and suffix in ('.csv', '.png', '.seq', '.sfxmodel', '.txt'))
            or (path.startswith('StreamingAssets/Shaders/') and suffix == '.txt')
            or (path.startswith('FF9_Data/EmbeddedAsset/') and suffix in ('.png', '.mpc')),
            f'Forbidden payload path outside mod ownership: {path}')


def check_layout(archive, prefix, manager, version):
    fixed = {'package-manifest.json', 'release.json', 'compatibility.json'}
    fixed |= ({'Manager-PreInstall.ps1'} if manager else
              {'README.txt', 'Install-EchosOfGaia.ps1', 'Restore-EchosOfGaia.ps1'})
    if not manager and tuple(map(int, version.split('.'))) >= (0, 1, 3):
        fixed.add('Install-EchosOfGaia.exe')
    for name in fixed:
        require(prefix + name in archive.files, f'Missing archive file: {prefix + name}')
        require(archive.files[prefix + name][1] > 0, f'Empty required archive file: {prefix + name}')
    require(any(name.startswith(prefix + 'tools/') for name in archive.files),
            'Missing tools helpers')
    for name in archive.files:
        if manager and name == 'manifest.json':
            continue
        require(name.startswith(prefix), f'Unexpected archive path: {name}')
        local = name[len(prefix):]
        require(local in fixed or local.startswith(('Payload/', 'tools/')),
                f'Unexpected archive path: {name}')
        if local.startswith('tools/'):
            require(Path(local).suffix.lower() in ('.ps1', '.psm1', '.py', '.md', '.txt', '.json'),
                    f'Unexpected tools path: {name}')


def check_package(archive, prefix, version):
    manifest = archive.json(prefix + 'package-manifest.json')
    require(type(manifest.get('Schema')) is int and manifest['Schema'] == 1,
            'Invalid package manifest Schema')
    source = manifest.get('SourceCommit')
    require(isinstance(source, str) and re.fullmatch(r'[0-9a-fA-F]{40}|[0-9a-fA-F]{64}', source),
            'Invalid package SourceCommit')
    records = manifest.get('Files')
    require(isinstance(records, list) and records, 'Missing package manifest Files')
    declared = {}
    seen = set()
    for record in records:
        require(isinstance(record, dict), 'Invalid package manifest file record')
        path = record.get('Path')
        safe_path(path)
        allowed_payload(path)
        require(path.casefold() not in seen, f'Duplicate package manifest path: {path}')
        seen.add(path.casefold())
        size, digest = record.get('Bytes'), record.get('SHA256')
        require(type(size) is int and size >= 0, f'Invalid payload size: {path}')
        require(isinstance(digest, str) and re.fullmatch(r'[0-9a-fA-F]{64}', digest),
                f'Invalid payload hash: {path}')
        full = prefix + 'Payload/' + path
        require(full in archive.files, f'Missing declared payload file: {path}')
        actual_hash, actual_size = archive.files[full]
        require(actual_size == size, f'Payload size mismatch: {path}')
        require(actual_hash == digest.lower(), f'Payload hash mismatch: {path}')
        declared[path] = (actual_hash, actual_size)
    actual = {name[len(prefix + 'Payload/'):] for name in archive.files
              if name.startswith(prefix + 'Payload/')}
    require(actual == set(declared), 'Unlisted Payload files outside package manifest')
    require('accessibility/LICENSE' in declared and declared['accessibility/LICENSE'][1] > 0,
            'Missing source license notice in payload')
    release = archive.json(prefix + 'release.json')
    expected = {'Schema': 1, 'Product': 'Echos of Gaia', 'Version': version,
                'Channel': 'beta', 'SourceCommit': source}
    require(type(release.get('Schema')) is int
            and all(release.get(key) == value for key, value in expected.items()),
            'Invalid release identity/version/SourceCommit metadata')
    compatibility = archive.json(prefix + 'compatibility.json')
    require(bool(compatibility), 'Empty compatibility metadata')
    return declared, release, compatibility


def check_amm(archive, payload, version):
    manifest = archive.json('manifest.json')
    for key, expected in {'gameId': 'final-fantasy-ix', 'pluginId': 'buu420',
                          'modVersion': version, 'targetPlatform': 'windows'}.items():
        require(manifest.get(key) == expected, f'Invalid AMM {key}')
    expected_pre = {'executable': 'files/Manager-PreInstall.ps1', 'failureFatal': True,
                    'runOnUpdate': True, 'installToGameFolder': False,
                    'runFromGameFolder': False, 'needsAdmin': False}
    explanatory = {'what', 'why', 'modifies'}
    pre = manifest.get('preInstall')
    require(isinstance(pre, dict) and set(pre) == set(expected_pre) | explanatory
            and all(pre.get(key) == value for key, value in expected_pre.items())
            and all(type(pre[key]) is bool for key in expected_pre if key != 'executable')
            and all(isinstance(pre[key], str) and pre[key].strip() for key in explanatory),
            'Invalid AMM preInstall read-only helper contract')
    for hook in ('postInstall', 'postUninstall'):
        require(hook in manifest and manifest[hook] is None, f'AMM {hook} must be null')
    actions = manifest.get('installActions')
    require(isinstance(actions, list) and len(actions) == len(payload),
            'AMM install action count does not match declared payload')
    seen = set()
    for action in actions:
        require(isinstance(action, dict) and set(action) == {'type', 'source', 'target'},
                'AMM install action must be exactly one explicit copyFile')
        target = action.get('target')
        require(isinstance(target, str) and target in payload and target not in seen,
                'AMM install action redirects, duplicates or owns an undeclared target')
        require(action == {'type': 'copyFile', 'source': 'Payload/' + target, 'target': target},
                'AMM install action source/type mismatch')
        seen.add(target)
    require(seen == set(payload), 'AMM install action coverage is incomplete')
    rules = manifest.get('verify')
    require(isinstance(rules, list) and len(rules) == len(payload),
            'AMM verification rule count does not match declared payload')
    verified = set()
    for rule in rules:
        require(isinstance(rule, dict) and set(rule) == {'type', 'path', 'sha256'},
                'AMM verification rule must be exactly one explicit hashEquals')
        path, digest = rule.get('path'), rule.get('sha256')
        require(isinstance(path, str) and path in payload and path not in verified,
                'AMM verification rule redirects, duplicates or reads an undeclared path')
        require(rule.get('type') == 'hashEquals' and isinstance(digest, str)
                and digest.lower() == payload[path][0],
                f'AMM verification hash/type mismatch: {path}')
        verified.add(path)
    require(verified == set(payload), 'AMM verification rule coverage is incomplete')


def check_checksums(directory, names):
    entries = {}
    try:
        lines = (directory / 'SHA256SUMS.txt').read_text(encoding='utf-8-sig').splitlines()
    except (OSError, UnicodeError) as error:
        raise VerificationError(f'Cannot read checksum manifest: {error}') from error
    require(len(lines) == 2, 'Checksum manifest must contain exactly two entries')
    for line in lines:
        match = re.fullmatch(r'([0-9a-fA-F]{64})  (.+)', line)
        require(match is not None, 'Invalid checksum manifest line')
        digest, name = match.groups()
        require(name in names and name not in entries, 'Unexpected/duplicate checksum filename')
        entries[name] = digest.lower()
    require(set(entries) == set(names), 'Checksum manifest does not list the release pair')
    for name, expected in entries.items():
        with (directory / name).open('rb') as stream:
            digest, _ = digest_stream(stream)
        require(digest == expected, f'Download checksum mismatch: {name}')


def verify(directory, version):
    require(re.fullmatch(r'\d+\.\d+\.\d+', version) is not None, 'Invalid release version')
    names = [f'EchosOfGaia-v{version}-beta-{kind}.zip' for kind in ('windows', 'amm')]
    check_checksums(directory, names)
    with ExitStack() as stack:
        windows, amm = [Archive(directory / name, stack) for name in names]
        check_layout(windows, '', manager=False, version=version)
        check_layout(amm, 'files/', manager=True, version=version)
        left, release, compatibility = check_package(windows, '', version)
        right, manager_release, manager_compatibility = check_package(amm, 'files/', version)
        require(windows.read('package-manifest.json') == amm.read('files/package-manifest.json'),
                'Package manifest bytes differ between archives')
        require(left == right, 'Declared payload differs between archives')
        require(release == manager_release, 'Release metadata differs between archives')
        require(compatibility == manager_compatibility, 'Compatibility metadata differs between archives')
        check_amm(amm, left, version)
        return len(left)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--directory', required=True, type=Path)
    parser.add_argument('--version', required=True)
    args = parser.parse_args(argv)
    try:
        count = verify(args.directory, args.version)
    except (VerificationError, OSError, zipfile.BadZipFile, RuntimeError, EOFError,
            NotImplementedError) as error:
        print(f'FAIL Echos of Gaia release: {error}', file=sys.stderr)
        return 1
    print(f'PASS Echos of Gaia {args.version} beta: 2 archives, {count} payload files; '
          'checksums, CRCs, ownership and metadata verified')
    return 0


if __name__ == '__main__':
    sys.exit(main())
