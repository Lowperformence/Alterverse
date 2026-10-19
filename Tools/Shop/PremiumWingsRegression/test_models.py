import importlib.util
import json
import math
from pathlib import Path
import unittest

ROOT=Path(__file__).resolve().parents[3]

class ModelTests(unittest.TestCase):
 def test_baked_geometry_is_finite_non_degenerate_and_mobile_sized(self):
  library=json.loads((ROOT/'Assets/Alterverse/Resources/Wings/PremiumWingMeshes.json').read_text())
  for name,design in library.items():
   triangles=0
   for part,mesh in design.items():
    v=[mesh['positions'][i:i+3] for i in range(0,len(mesh['positions']),3)]
    self.assertLess(len(v),65536)
    for slot in ['body','facet','energy']:
     ids=mesh[slot];self.assertEqual(len(ids)%3,0);triangles+=len(ids)//3*(2 if part=='wing' else 1)
     for i in range(0,len(ids),3):
      a,b,c=[v[j] for j in ids[i:i+3]]
      u=[b[j]-a[j] for j in range(3)];w=[c[j]-a[j] for j in range(3)]
      n=[u[1]*w[2]-u[2]*w[1],u[2]*w[0]-u[0]*w[2],u[0]*w[1]-u[1]*w[0]]
      self.assertGreater(sum(x*x for x in n),1e-18,(name,part,slot,i))
    self.assertTrue(all(math.isfinite(x) for x in mesh['positions']))
   self.assertLess(triangles,14000)
   print(name,'rendered triangles with both sides:',triangles)
 def test_angel_keeps_approved_geometry_and_rgb_has_only_strip_emission(self):
  source=json.loads((ROOT/'ArtSource/Wings/PremiumWingParts.json').read_text())
  self.assertEqual(len(source['models']['Angel']),69)
  rgb=source['models']['RGB'];self.assertEqual(len([p for p in rgb if p['name'].startswith('RGBStrip_')]),16)
  for p in rgb:
   if source['materials'][p['material']][3]:self.assertEqual(p['material'],'RGB_Channel')
  self.assertFalse(any('Mech'==k for k in source['models']))

if __name__=='__main__':unittest.main()
