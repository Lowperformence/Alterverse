"""Matchmaker deployment rules agree with the authoritative ranked backend."""
import json
from pathlib import Path
import unittest
from ranked import TEAM_SIZE, MATCH_PLAYERS


class MatchFormatTests(unittest.TestCase):
    def test_both_profiles_cap_teams_at_four(self):
        config = json.loads((Path(__file__).resolve().parents[1] /
                             'matchmaking/edgegap-matchmaker-v1.json').read_text())
        self.assertEqual((TEAM_SIZE, MATCH_PLAYERS), (4, 8))
        for name in ('dimensions-front-online', 'dimensions-front-ranked'):
            with self.subTest(profile=name):
                rules = config['profiles'][name]['rules']
                initial = rules['initial']['match_size']['attributes']
                self.assertEqual(initial, dict(team_count=2, min_team_size=4, max_team_size=4))
                for seconds, expansion in rules.get('expansions', {}).items():
                    if 'match_size' not in expansion:
                        continue
                    size = expansion['match_size']
                    self.assertEqual(size['team_count'],
                                     1 if name.endswith('online') and int(seconds) >= 20 else 2)
                    self.assertEqual(size['max_team_size'], TEAM_SIZE)
                    self.assertGreaterEqual(size['min_team_size'], 1)
                    self.assertLessEqual(size['min_team_size'], TEAM_SIZE)
                    if name.endswith('ranked'):
                        self.assertEqual(size['min_team_size'], TEAM_SIZE)
        casual = config['profiles']['dimensions-front-online']['rules']
        self.assertEqual(casual['expansions']['10']['match_size']['min_team_size'], 1)

    def test_solo_partial_match_has_a_short_flush_deadline(self):
        config = json.loads((Path(__file__).resolve().parents[1] /
                             'matchmaking/edgegap-matchmaker-v1.json').read_text())
        casual = config['profiles']['dimensions-front-online']
        # Edgegap flushes partial matches before expansion/expiration, not as
        # soon as a smaller minimum is introduced. Do not leave solo at 2m.
        expiration = casual['ticket_expiration_period']
        seconds = int(expiration[:-1]) * (60 if expiration.endswith('m') else 1)
        self.assertGreater(seconds, 20)
        self.assertLessEqual(seconds, 30)
        self.assertEqual(casual['rules']['expansions']['20']['match_size'],
                         dict(team_count=1, min_team_size=1, max_team_size=4))
        self.assertEqual(config['profiles']['dimensions-front-ranked']['ticket_expiration_period'], '5m')

    def test_casual_only_rollout_matches_canonical_profile(self):
        folder = Path(__file__).resolve().parents[1] / 'matchmaking'
        canonical = json.loads((folder / 'edgegap-matchmaker-v1.json').read_text())
        rollout = json.loads((folder / 'edgegap-casual-live-v1.2.json').read_text())
        self.assertEqual(set(rollout['profiles']), {'dimensions-front-online'})
        self.assertEqual(rollout['profiles']['dimensions-front-online'],
                         canonical['profiles']['dimensions-front-online'])
        self.assertEqual(rollout['version'], canonical['version'])


if __name__ == '__main__':
    unittest.main()
