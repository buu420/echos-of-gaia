"""Native Story step bindings stay generated, provenance-bound and separate from chapter rows."""
import json
from pathlib import Path
import unittest
import importlib.util

ROOT = Path(__file__).resolve().parents[1]


def load(path):
    return json.loads((ROOT / path).read_text(encoding='utf-8'))


class StoryStepCoverageTests(unittest.TestCase):
    def test_early_dialogue_skips_are_part_of_the_request_guard(self):
        spec=importlib.util.spec_from_file_location('story_step_analysis',ROOT/'tools/story_step_analysis.py')
        analysis=importlib.util.module_from_spec(spec);spec.loader.exec_module(analysis)
        fixture=load('tests/Fixtures/story-early-request.json')
        guards=analysis.Function(810,22,3,fixture['ops']).guards(fixture['requestOffset'])
        self.assertTrue(guards,'native counter<4180 and counter<4200 both skip the later request')
    def test_steps_come_from_the_story_catalog_inventory(self):
        self.assertEqual(load('docs/accessibility/story-steps.json')['inventorySHA256'], load('docs/accessibility/story-navigation.json')['inventorySHA256'])

    def test_every_step_is_guarded_or_a_counter_writer(self):
        for step in load('docs/accessibility/story-steps.json')['steps']:
            self.assertTrue(step['source'] == 'direct-counter' or step['guards'], step)
            if step['kind'] == 'position':
                self.assertTrue('minimum' in step or 'maximum' in step, step)

    def test_guide_report_separates_chapter_rows_from_action_bindings(self):
        report = load('docs/accessibility/guide-coverage.json')
        self.assertIn('not native action bindings', report['proofBoundary'])
        self.assertEqual(report['nativeStoryStepBindings']['summary'], load('docs/accessibility/story-step-coverage.json')['summary'])

    def test_ledger_never_claims_live_proof(self):
        ledger = load('docs/accessibility/story-step-coverage.json')
        self.assertIn('not a live playthrough', ledger['proofBoundary'])
        self.assertEqual(sum(c['stageMilestones'] for c in ledger['chapters']) > 0, True)


if __name__ == '__main__':
    unittest.main()
