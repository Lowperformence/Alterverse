from pathlib import Path
import random
from PIL import Image
# Periodic digital patches, four neutral urban tones; no frame-dependent noise.
rng=random.Random(417)
n=32;grid=[[1]*n for _ in range(n)]
palette=[(35,40,47),(91,97,103),(168,171,165),(204,195,174)]
for i in range(110):
 x=rng.randrange(n);y=rng.randrange(n);w=rng.randrange(2,8);h=rng.randrange(2,6);color=rng.randrange(4)
 for dy in range(h):
  for dx in range(w):
   if dx==w-1 and dy==h-1 and i%2:continue
   grid[(y+dy)%n][(x+dx)%n]=color
for i in range(65):
 x=rng.randrange(n);y=rng.randrange(n);color=rng.randrange(4)
 for dx,dy in [(0,0),(1,0),(0,1)]:grid[(y+dy)%n][(x+dx)%n]=color
image=Image.new('RGB',(n,n));image.putdata([palette[grid[y][x]] for y in range(n) for x in range(n)])
image=image.resize((512,512),Image.Resampling.NEAREST)
target=Path(__file__).resolve().parents[2]/'Assets/Alterverse/Resources/HeroArt/UrbanDigitalCamo.png'
target.parent.mkdir(parents=True,exist_ok=True);image.save(target)
print(target)
