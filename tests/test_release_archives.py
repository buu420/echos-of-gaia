"""Offline release-pair checks using tiny, synthetic archives only."""
import copy
import hashlib
import json
from pathlib import Path
import struct
import subprocess
import sys
import tempfile
import unittest
import warnings
import zipfile


ROOT = Path(__file__).resolve().parents[1]
VERIFIER = ROOT / 'tools' / 'verify_echos_release.py'
VERSION = '0.1.0'
COMMIT = 'a' * 40


def encoded(value):
    return json.dumps(value, sort_keys=True).encode('utf-8')


class ReleaseArchiveTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.directory = Path(self.temporary.name)
        self.version = VERSION
        self.names = [f'EchosOfGaia-v{VERSION}-beta-{kind}.zip'
                      for kind in ('windows', 'amm')]
        self.payload = {
            'accessibility/LICENSE': b'Synthetic source license notice',
            'accessibility/licenses/UnityEngine.UI_LICENSE': b'Synthetic Unity notice',
            'x64/FF9_Data/Managed/Assembly-CSharp.dll': b'synthetic managed DLL',
            'accessibility/audio-descriptions/example.wav': b'synthetic narrator',
        }
        self.manifest = {'Schema': 1, 'SourceCommit': COMMIT, 'Files': [
            {'Path': name, 'SHA256': hashlib.sha256(data).hexdigest(), 'Bytes': len(data)}
            for name, data in self.payload.items()]}
        self.release = {'Schema': 1, 'Product': 'Echos of Gaia', 'Version': VERSION,
                        'Channel': 'beta', 'SourceCommit': COMMIT}
        self.amm_manifest = {
            'gameId': 'final-fantasy-ix', 'pluginId': 'buu420',
            'modVersion': VERSION, 'targetPlatform': 'windows',
            'installActions': [{'type': 'copyFile', 'source': 'Payload/' + name,
                                'target': name} for name in self.payload],
            'verify': [{'type': 'hashEquals', 'path': name,
                        'sha256': hashlib.sha256(data).hexdigest()}
                       for name, data in self.payload.items()],
            'preInstall': {'executable': 'files/Manager-PreInstall.ps1',
                           'failureFatal': True, 'runOnUpdate': True,
                           'installToGameFolder': False, 'runFromGameFolder': False,
                           'needsAdmin': False, 'what': 'Check synthetic inputs.',
                           'why': 'Reject a mismatched synthetic fixture.',
                           'modifies': 'No files; read-only validation.'},
            'postInstall': None, 'postUninstall': None,
        }
        common = {'package-manifest.json': encoded(self.manifest),
                  'release.json': encoded(self.release),
                  'compatibility.json': encoded({'Schema': 1, 'Policy': 'synthetic'}),
                  'tools/FF9Access.Deployment.ps1': b'# synthetic deployment helper'}
        common.update({'Payload/' + name: data for name, data in self.payload.items()})
        self.windows = dict(common, **{'README.txt': b'Synthetic test fixture',
            'Install-EchosOfGaia.ps1': b'# synthetic install wrapper',
            'Restore-EchosOfGaia.ps1': b'# synthetic restore wrapper'})
        self.amm = {'files/' + name: data for name, data in common.items()}
        self.amm['files/Manager-PreInstall.ps1'] = b'# Read-only synthetic preflight'
        self.amm['manifest.json'] = encoded(self.amm_manifest)

    def write_pair(self, extra_windows=()):
        for name, entries in zip(self.names, (self.windows, self.amm)):
            with zipfile.ZipFile(self.directory / name, 'w', zipfile.ZIP_STORED) as archive:
                for path, data in entries.items():
                    # ZipInfo normalizes backslashes on Windows at construction;
                    # preserve hostile names so this exercises the verifier.
                    info = zipfile.ZipInfo(path)
                    info.filename = path
                    info.orig_filename = path
                    archive.writestr(info, data)
                if name == self.names[0]:
                    with warnings.catch_warnings():
                        warnings.simplefilter('ignore', UserWarning)
                        for path, data in extra_windows:
                            archive.writestr(path, data)
        self.write_checksums()

    def write_checksums(self):
        lines = [hashlib.sha256((self.directory / name).read_bytes()).hexdigest()
                 + '  ' + name for name in self.names]
        (self.directory / 'SHA256SUMS.txt').write_text('\n'.join(lines) + '\n')

    def run_verifier(self):
        return subprocess.run([sys.executable, str(VERIFIER), '--directory',
            str(self.directory), '--version', self.version], capture_output=True, text=True)

    def use_double_click_release(self):
        self.version = '0.1.3'
        self.names = ['EchosOfGaia-v0.1.3-beta-windows.zip',
                      'EchosOfGaia-v0.1.3-beta-amm.zip']
        self.release['Version'] = self.version
        self.windows['release.json'] = encoded(self.release)
        self.amm['files/release.json'] = encoded(self.release)
        self.amm_manifest['modVersion'] = self.version
        self.amm['manifest.json'] = encoded(self.amm_manifest)

    def test_double_click_release_accepts_root_installer(self):
        self.use_double_click_release()
        self.windows['Install-EchosOfGaia.exe'] = b'MZ synthetic installer fixture'
        self.write_pair()
        result = self.run_verifier()
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    def test_double_click_release_requires_installer(self):
        self.use_double_click_release()
        self.assert_rejected('missing archive file: install-echosofgaia.exe')

    def test_double_click_release_rejects_empty_installer(self):
        self.use_double_click_release()
        self.windows['Install-EchosOfGaia.exe'] = b''
        self.assert_rejected('empty required archive file: install-echosofgaia.exe')

    def test_installer_is_never_a_runtime_payload(self):
        self.add_payload('Install-EchosOfGaia.exe')
        self.assert_rejected('forbidden payload path')

    def assert_rejected(self, reason):
        self.write_pair()
        result = self.run_verifier()
        self.assertNotEqual(result.returncode, 0, result.stdout)
        self.assertIn(reason, (result.stdout + result.stderr).lower())

    def add_payload(self, name, data=b'extra'):
        self.payload[name] = data
        self.manifest['Files'].append({'Path': name, 'Bytes': len(data),
                                      'SHA256': hashlib.sha256(data).hexdigest()})
        self.windows['Payload/' + name] = data
        self.amm['files/Payload/' + name] = data
        for entries, prefix in ((self.windows, ''), (self.amm, 'files/')):
            entries[prefix + 'package-manifest.json'] = encoded(self.manifest)
        self.amm_manifest['installActions'].append(
            {'type': 'copyFile', 'source': 'Payload/' + name, 'target': name})
        self.amm_manifest['verify'].append(
            {'type': 'hashEquals', 'path': name, 'sha256': hashlib.sha256(data).hexdigest()})
        self.amm['manifest.json'] = encoded(self.amm_manifest)

    def test_valid_pair_and_cli_report(self):
        self.write_pair()
        result = self.run_verifier()
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertIn('PASS', result.stdout)
        self.assertIn('4 payload', result.stdout)

    def test_payload_count_is_not_hardcoded(self):
        self.add_payload('accessibility/docs/example.md')
        self.write_pair()
        result = self.run_verifier()
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertIn('5 payload', result.stdout)

    def test_public_memoria_support_and_launcher_paths_are_allowed(self):
        for name in ('StreamingAssets/Data/Battle/Actions.csv',
                     'StreamingAssets/Data/Characters/example.png',
                     'StreamingAssets/Data/example.seq',
                     'StreamingAssets/Data/example.sfxmodel',
                     'StreamingAssets/Shaders/example shader.txt',
                     'StreamingAssets/Scripts/Memoria.Scripts.dll',
                     'FF9_Data/EmbeddedAsset/example.png',
                     'FF9_Data/EmbeddedAsset/example.mpc',
                     'x86/FF9_Data/Plugins/SaXAudio.dll', 'FF9_Launcher.exe'):
            self.add_payload(name)
        self.write_pair()
        result = self.run_verifier()
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    def test_manifest_cannot_duplicate_a_payload_record(self):
        self.manifest['Files'].append(copy.deepcopy(self.manifest['Files'][0]))
        for entries, prefix in ((self.windows, ''), (self.amm, 'files/')):
            entries[prefix + 'package-manifest.json'] = encoded(self.manifest)
        self.assert_rejected('duplicate')

    def test_zip_symlink_is_not_an_extractable_payload(self):
        self.write_pair()
        info = zipfile.ZipInfo('tools/link')
        info.create_system = 3
        info.external_attr = (0o120777 << 16)
        with zipfile.ZipFile(self.directory / self.names[0], 'a') as archive:
            archive.writestr(info, '../outside')
        self.write_checksums()
        result = self.run_verifier()
        self.assertNotEqual(result.returncode, 0)
        self.assertIn('path type', result.stderr.lower())

    def test_payload_hash_mismatch(self):
        self.windows['Payload/accessibility/LICENSE'] = b'X' * len(self.payload['accessibility/LICENSE'])
        self.assert_rejected('hash')

    def test_payload_size_mismatch(self):
        self.windows['Payload/accessibility/LICENSE'] = b'too short'
        self.assert_rejected('size')

    def test_missing_declared_payload(self):
        del self.windows['Payload/accessibility/LICENSE']
        self.assert_rejected('missing')

    def test_undeclared_payload(self):
        self.windows['Payload/accessibility/extra.wav'] = b'unlisted'
        self.assert_rejected('unlisted')

    def test_path_traversal_and_absolute_paths(self):
        for name in ('../escape.txt', '/absolute.txt', 'C:/absolute.txt',
                     'tools/../escape.txt', 'tools\\escape.txt', 'tools/file:stream',
                     'tools/name. ', 'tools/CON.txt'):
            with self.subTest(name=name):
                self.windows[name] = b'unsafe'
                self.assert_rejected('path')
                del self.windows[name]

    def test_exact_and_casefold_duplicate_archive_entries(self):
        for name in ('README.txt', 'readme.TXT'):
            with self.subTest(name=name):
                self.write_pair([(name, b'duplicate')])
                result = self.run_verifier()
                self.assertNotEqual(result.returncode, 0)
                self.assertIn('duplicate', result.stderr.lower())

    def test_amm_target_redirection(self):
        self.amm_manifest['installActions'][0]['target'] = 'somewhere/else'
        self.amm['manifest.json'] = encoded(self.amm_manifest)
        self.assert_rejected('action')

    def test_extra_or_missing_amm_actions(self):
        original = copy.deepcopy(self.amm_manifest['installActions'])
        for actions in (original[:-1], original + [original[0]],
                        original + [{'type': 'copyFolder', 'source': 'tools', 'target': 'tools'}]):
            with self.subTest(actions=actions):
                self.amm_manifest['installActions'] = actions
                self.amm['manifest.json'] = encoded(self.amm_manifest)
                self.assert_rejected('action')

    def test_owning_settings_or_saves_or_stock_executables_is_rejected(self):
        for name in ('Memoria.ini', 'Settings.ini', 'saves/slot1.dat',
                     'x64/FF9.exe', 'FF9_Launcher.exe.bak', 'UnityEngine.dll'):
            with self.subTest(name=name):
                self.setUp()
                self.add_payload(name)
                self.assert_rejected('payload path')

    def test_personal_or_native_asset_payload_is_rejected(self):
        for name in ('accessibility/raw_voice/reference.wav', 'accessibility/reference-profile.json',
                     'accessibility/secret.pfx', 'accessibility/Dependencies.7z',
                     'accessibility/original.mp4', 'accessibility/film.movie',
                     'accessibility/Manager-PreInstall.ps1'):
            with self.subTest(name=name):
                self.setUp()
                self.add_payload(name)
                self.assert_rejected('payload path')

    def test_source_commit_mismatch(self):
        self.release['SourceCommit'] = 'b' * 40
        self.windows['release.json'] = encoded(self.release)
        self.assert_rejected('sourcecommit')

    def test_release_product_version_and_channel(self):
        for key, value in (('Product', 'Echoes of Gaia'), ('Version', '2.0.0'),
                           ('Channel', 'stable'), ('Schema', True)):
            with self.subTest(key=key):
                release = dict(self.release, **{key: value})
                self.windows['release.json'] = encoded(release)
                self.assert_rejected('release')

    def test_manifest_bytes_must_be_identical_across_pair(self):
        self.amm['files/package-manifest.json'] += b'\n'
        self.assert_rejected('manifest')

    def test_compatibility_metadata_must_match(self):
        self.amm['files/compatibility.json'] = encoded({'Schema': 1, 'Policy': 'different'})
        self.assert_rejected('compatibility')

    def test_preinstall_contract_and_helper_exclusion(self):
        self.amm_manifest['preInstall']['installToGameFolder'] = True
        self.amm['manifest.json'] = encoded(self.amm_manifest)
        self.assert_rejected('preinstall')

    def test_actual_catalog_preinstall_shape_is_accepted(self):
        catalog_path = ROOT / 'packaging' / 'amm' / 'final-fantasy-ix-game.json'
        catalog = json.loads(catalog_path.read_text(encoding='utf-8-sig'))
        self.amm_manifest['preInstall'] = catalog['defaultPreInstall']
        self.amm['manifest.json'] = encoded(self.amm_manifest)
        self.write_pair()
        result = self.run_verifier()
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    def test_preinstall_requires_nonempty_explanatory_metadata(self):
        original = copy.deepcopy(self.amm_manifest['preInstall'])
        for key in ('what', 'why', 'modifies'):
            for value in (None, '', '   ', False):
                with self.subTest(key=key, value=value):
                    hook = copy.deepcopy(original)
                    if value is None:
                        del hook[key]
                    else:
                        hook[key] = value
                    self.amm_manifest['preInstall'] = hook
                    self.amm['manifest.json'] = encoded(self.amm_manifest)
                    self.assert_rejected('preinstall')

    def test_amm_hash_verification_rules_cover_exact_payload(self):
        original = copy.deepcopy(self.amm_manifest['verify'])
        wrong_hash = copy.deepcopy(original)
        wrong_hash[0]['sha256'] = '0' * 64
        redirected = copy.deepcopy(original)
        redirected[0]['path'] = 'Memoria.ini'
        for rules in (original[:-1], original + [original[0]], wrong_hash, redirected):
            with self.subTest(rules=rules):
                self.amm_manifest['verify'] = rules
                self.amm['manifest.json'] = encoded(self.amm_manifest)
                self.assert_rejected('verification')

    def test_missing_preinstall_helper(self):
        del self.amm['files/Manager-PreInstall.ps1']
        self.assert_rejected('missing')

    def test_postinstall_hook_is_forbidden(self):
        self.amm_manifest['postInstall'] = {'executable': 'files/other.ps1'}
        self.amm['manifest.json'] = encoded(self.amm_manifest)
        self.assert_rejected('postinstall')

    def test_missing_wrapper(self):
        del self.windows['Restore-EchosOfGaia.ps1']
        self.assert_rejected('missing')

    def test_missing_notice(self):
        self.manifest['Files'] = [f for f in self.manifest['Files']
                                  if f['Path'] != 'accessibility/LICENSE']
        for entries, prefix in ((self.windows, ''), (self.amm, 'files/')):
            del entries[prefix + 'Payload/accessibility/LICENSE']
            entries[prefix + 'package-manifest.json'] = encoded(self.manifest)
        self.assert_rejected('notice')

    def test_checksum_mismatch(self):
        self.write_pair()
        sums = self.directory / 'SHA256SUMS.txt'
        sums.write_text(sums.read_text().replace(hashlib.sha256(
            (self.directory / self.names[0]).read_bytes()).hexdigest(), '0' * 64))
        result = self.run_verifier()
        self.assertNotEqual(result.returncode, 0)
        self.assertIn('checksum', result.stderr.lower())

    def test_checksum_manifest_requires_exact_pair(self):
        self.write_pair()
        sums = self.directory / 'SHA256SUMS.txt'
        sums.write_text(sums.read_text() + '0' * 64 + '  extra.zip\n')
        result = self.run_verifier()
        self.assertNotEqual(result.returncode, 0)
        self.assertIn('checksum', result.stderr.lower())

    def test_crc_corruption_is_detected_after_updating_download_checksum(self):
        self.write_pair()
        path = self.directory / self.names[0]
        with zipfile.ZipFile(path) as archive:
            info = archive.getinfo('Payload/accessibility/LICENSE')
        data = bytearray(path.read_bytes())
        name_length, extra_length = struct.unpack_from('<HH', data, info.header_offset + 26)
        at = info.header_offset + 30 + name_length + extra_length
        data[at] ^= 1
        path.write_bytes(data)
        self.write_checksums()
        result = self.run_verifier()
        self.assertNotEqual(result.returncode, 0)
        self.assertIn('crc', result.stderr.lower())


if __name__ == '__main__':
    unittest.main()
