"""Reviewed Story bindings must still identify the actual native interaction."""
import copy
import json
from pathlib import Path
import sys
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'tools'))
from story_action_validation import validate


class ReviewedActionValidationTests(unittest.TestCase):
    def setUp(self):
        self.native = json.loads((ROOT / 'tests/Fixtures/story-reviewed-action.json').read_text())
        self.chains = json.loads((ROOT / 'docs/accessibility/story-action-chains.json').read_text())
        # This fixture retains just the required native sites, not the complete game inventory.
        self.chains['steps'] = [next(r for r in self.chains['steps'] if r['id'] == 'evil-forest-baku-challenge')]
        self.chains['phases'] = []
        self.chains['loopActions'] = []

    def errors(self):
        return validate(self.chains, self.native['fields'], self.native['inventorySHA256'])

    def test_baku_challenge_binds_the_prompt_before_the_player_makes_a_choice(self):
        self.assertEqual([], self.errors())

    def test_different_native_install_cannot_supply_reviewed_bindings(self):
        self.chains['inventorySHA256'] = '0' * 64
        self.assertTrue(any('inventory' in e for e in self.errors()))

    def test_shared_script_uid_must_be_an_actual_created_instance(self):
        self.chains['steps'][0]['uid'] = 129
        self.assertTrue(any('instance' in e for e in self.errors()))

    def test_an_effect_at_another_script_site_cannot_validate_the_action(self):
        self.chains['steps'][0]['effects'][0]['offset'] = 356
        self.assertTrue(any('effect' in e for e in self.errors()))

    def test_prefix_evidence_cannot_pretend_to_have_written_a_flag(self):
        self.chains['steps'][0]['effects'][0]['proof'] = 'assigned'
        self.assertTrue(any('assignment' in e for e in self.errors()))

    def test_prefix_does_not_cross_a_wait_to_claim_a_future_site(self):
        code=self.native['fields']['202']['entries']['9']['functions']['3']
        code[0].update(op='WAIT',args=[1],next=542)
        self.chains['steps'][0]['effects'][0]['offset']=542
        self.assertTrue(any('native yield' in e for e in self.errors()))

    def test_wait_ip_must_be_a_real_yield_resume_address(self):
        self.chains['steps'][0]['waiting']['ips'] = [1518]
        self.assertTrue(any('wait' in e for e in self.errors()))

    def test_wait_owner_hash_is_not_substituted_by_a_similar_actor(self):
        self.chains['steps'][0]['waiting']['hash'] = '0' * 64
        self.assertTrue(any('hash' in e for e in self.errors()))

    def test_a_guard_cannot_belong_to_an_uncreated_owner(self):
        self.chains['steps'][0]['guards'][0]['uid'] = 20
        self.assertTrue(any('instance' in e for e in self.errors()))

    def test_reviewed_action_retires_in_a_bounded_counter_window(self):
        self.chains['steps'][0]['ceiling'] = 2100
        self.assertTrue(any('counter' in e for e in self.errors()))

    def test_duplicate_actions_are_not_silently_loaded_twice(self):
        self.chains['steps'].append(copy.deepcopy(self.chains['steps'][0]))
        self.assertTrue(any('duplicate' in e for e in self.errors()))


if __name__ == '__main__':
    unittest.main()
