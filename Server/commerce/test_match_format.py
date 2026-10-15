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
                for expansion in rules.get('expansions', {}).values():
                    if 'match_size' not in expansion:
                        continue
                    size = expansion['match_size']
                    self.assertEqual(size['team_count'], 2)
                    self.assertEqual(size['max_team_size'], TEAM_SIZE)
                    self.assertGreaterEqual(size['min_team_size'], 1)
                    self.assertLessEqual(size['min_team_size'], TEAM_SIZE)
                    if name.endswith('ranked'):
                        self.assertEqual(size['min_team_size'], TEAM_SIZE)
        casual = config['profiles']['dimensions-front-online']['rules']
        self.assertEqual(casual['expansions']['10']['match_size']['min_team_size'], 1)


if __name__ == '__main__':
    unittest.main()
