import copy
import unittest
from unittest.mock import patch
import balance
from balance import load_catalog, simulate, Unit
class BalanceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):cls.c=load_catalog()
    def test_mirror_symmetry(self):
        for a,b in [('Kaelon','Nyra'),('Veyr','Mira'),('Rook','VoidKnight')]:
            self.assertAlmostEqual(simulate(self.c,[a],[b])['value']+simulate(self.c,[b],[a])['value'],1)
    def test_attack_slowdown_changes_chase(self):
        baseline=simulate(self.c,['Kaelon'],['Nyra'],skills=False,duration=20)
        original=balance.read
        with patch('balance.read',side_effect=lambda name:original(name).replace('attackMovementMultiplier = .88f','attackMovementMultiplier = 1f') if name=='HeroMotor.cs' else original(name)):
            unslowed=simulate(self.c,['Kaelon'],['Nyra'],skills=False,duration=20)
        self.assertNotEqual(baseline['units'][0]['first_hit'],unslowed['units'][0]['first_hit'])
    def test_timestep_convergence_kiting_regression(self):
        coarse=simulate(self.c,['Kaelon'],['Nyra'],distance=8,duration=30,kiting=True,skills=False,passives=False,dt=.00625)
        fine=simulate(self.c,['Kaelon'],['Nyra'],distance=8,duration=30,kiting=True,skills=False,passives=False,dt=.003125)
        self.assertAlmostEqual(coarse['value'],fine['value'],places=5)
        self.assertLess(abs(coarse['units'][0]['first_hit']-fine['units'][0]['first_hit']),.1)
    def test_ally_healing_excludes_self(self):
        r=simulate(self.c,['Mira'],['Nyra'],skills=True)
        self.assertEqual(r['units'][0]['ally_healing_generated'],0)
    def test_catalog(self):
        self.assertEqual(len(self.c),10);self.assertAlmostEqual(self.c['Nyra']['hp'],185)
        self.assertAlmostEqual(self.c['Timmy']['speed'],7.1*1.05*1.05*.855)
    def test_shields_expire_cap_do_not_add(self):
        u=Unit(self.c['Nyra'],0,0);u.shield(90,2,0);self.assertEqual(u.temp,74)
        u.shield(10,1,0);self.assertEqual(u.temp,74)
        u.hit(20,1);self.assertEqual(u.base,30);self.assertEqual(u.hp,185)
        u.hit(40,3);self.assertEqual(u.hp,175);self.assertEqual(u.base,0)
        self.assertEqual(u.heal(999),10);self.assertEqual(u.heal(999),0)
        u.hit(10,18);self.assertEqual(u.base,20);self.assertEqual(u.hp,185)
    def test_deterministic(self):
        self.assertEqual(simulate(self.c,['Veyr'],['Nyra']),simulate(self.c,['Veyr'],['Nyra']))
    def test_kiting_unreachable_and_gapclose(self):
        c=copy.deepcopy(self.c);c['Veyr']['speed']=4;c['Nyra']['speed']=6
        no=simulate(c,['Veyr'],['Nyra'],distance=5,skills=False,duration=4)
        yes=simulate(c,['Veyr'],['Nyra'],distance=5,skills=True,duration=4)
        self.assertIsNone(no['units'][0]['first_hit']);self.assertIsNotNone(yes['units'][0]['first_hit'])
    def test_chase_speed_attack_slowdown(self):
        c=copy.deepcopy(self.c);c['Kaelon']['speed']=5.13;c['Nyra']['speed']=4.7709
        r=simulate(c,['Kaelon'],['Nyra'],distance=6.8,skills=False,duration=20)
        self.assertIsNotNone(r['units'][0]['first_hit']);self.assertGreater(r['units'][0]['hp_and_shield_lost_before_first_hit'],0)
    def test_team_swap_distinct(self):
        r=simulate(self.c,['Kaelon','Mira','Nyra','Timmy'],['Rook','Elyra','Traveler','VoidKnight'])
        self.assertEqual(len(r['units']),8);self.assertTrue(0<=r['value']<=1)
    def test_ione_tahla_timestep_regression(self):
        for dt in (.00625,.003125):
            self.assertEqual(simulate(self.c,['Elyra'],['Mira'],distance=8,kiting=True,skills=False,passives=False,dt=dt)['value'],1)
    def test_invalid_timestep(self):
        with self.assertRaises(ValueError):simulate(self.c,['Kaelon'],['Nyra'],dt=0)
    def test_no_passive_regen_overheal(self):
        r=simulate(self.c,['Kaelon'],['Nyra'],distance=100,duration=1,skills=False)
        self.assertEqual(r['units'][0]['effective_healing_received'],0)
if __name__=='__main__':unittest.main()
