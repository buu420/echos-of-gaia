"""Coverage claims must stay complete, provenance-bound and separate from live proof."""
import copy
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location('guide_audit', ROOT / 'tools/guide_audit.py')
AUDIT = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(AUDIT)


class GuideAuditTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.report = json.loads((ROOT / 'docs/accessibility/guide-coverage.json').read_text(encoding='utf-8'))

    def changed(self):
        return copy.deepcopy(self.report)

    def test_retained_whole_game_inventory_and_current_source_match(self):
        AUDIT.validate(self.report, ROOT)

    def test_cannot_drop_an_unresolved_native_field(self):
        report = self.changed()
        report['fields'].pop()
        with self.assertRaises(ValueError):
            AUDIT.validate(report, ROOT)

    def test_cannot_duplicate_a_guide_chapter(self):
        report = self.changed()
        report['storyChapters'][-1] = copy.deepcopy(report['storyChapters'][0])
        with self.assertRaises(ValueError):
            AUDIT.validate(report, ROOT)

    def test_cannot_replace_optional_content_with_a_foreign_topic(self):
        report = self.changed()
        report['optionalTopics'][-1]['id'] = 'unreviewed-topic'
        with self.assertRaises(ValueError):
            AUDIT.validate(report, ROOT)

    def test_scripts_do_not_establish_full_accessibility(self):
        report = self.changed()
        report['optionalTopics'][0]['fullyAccessibleProven'] = True
        with self.assertRaises(ValueError):
            AUDIT.validate(report, ROOT)

    def test_no_manufactured_live_gameplay_proof(self):
        report = self.changed()
        report['fields'][0]['liveValidated'] = True
        with self.assertRaises(ValueError):
            AUDIT.validate(report, ROOT)

    def test_no_optional_entry_binding_to_a_different_field(self):
        report = self.changed()
        report['optionalTopics'][0]['nativeBindings'][0]['fieldSha256'] = '0' * 64
        with self.assertRaises(ValueError):
            AUDIT.validate(report, ROOT)

    def test_story_checkpoints_cannot_disappear(self):
        report = self.changed()
        report['storyChapters'][0]['checkpoints'].pop()
        with self.assertRaises(ValueError):
            AUDIT.validate(report, ROOT)

    def test_source_drift_requires_refreshing_the_audit(self):
        report = self.changed()
        report['sourceArtifacts'][0]['sha256'] = '0' * 64
        with self.assertRaises(ValueError):
            AUDIT.validate(report, ROOT)

    def test_unresolved_topics_must_keep_their_limitations(self):
        report = self.changed()
        report['optionalTopics'][1]['remainingGaps'] = []
        with self.assertRaises(ValueError):
            AUDIT.validate(report, ROOT)

    def test_prerequisite_compiler_accepts_windows_checkout_line_endings(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for name in ['tools/compile_story_prerequisites.py', 'docs/accessibility/story-prerequisites.json',
                         'Assembly-CSharp/Memoria/Accessibility/Navigation/Core/StoryPrerequisiteData.cs']:
                target = root / name
                target.parent.mkdir(parents=True, exist_ok=True)
                data = (ROOT / name).read_bytes().replace(b'\r\n', b'\n')
                target.write_bytes(data.replace(b'\n', b'\r\n'))
            result = subprocess.run([sys.executable, str(root / 'tools/compile_story_prerequisites.py'), '--check'],
                                    capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)


if __name__ == '__main__':
    unittest.main()
