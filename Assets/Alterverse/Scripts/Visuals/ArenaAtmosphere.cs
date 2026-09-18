using UnityEngine;
namespace Alterverse.Visuals
{
public sealed class ArenaAtmosphere:MonoBehaviour
{
private Transform[] shards;private Vector3[] origins;private Transform[] wisps;private Vector3[] wispOrigins;private readonly System.Collections.Generic.Dictionary<Color,Material> materials=new();private void Start(){const int count=16;shards=new Transform[count];origins=new Vector3[count];for(int i=0;i<count;i++){int side=i%2==0?-1:1;float z=-31+(i/2)*8.8f;Vector3 p=new Vector3(side*(44+(i%3)*1.2f),1.3f+(i%4)*.45f,z);GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=side<0?"DF_ZeroAmbientShard":"DF_LumenAmbientShard";g.transform.SetParent(transform,false);g.transform.position=p;g.transform.localScale=new Vector3(.12f,.55f+.1f*(i%3),.12f);g.transform.rotation=Quaternion.Euler(18,i*37,24);Collider c=g.GetComponent<Collider>();if(c!=null)c.enabled=false;Renderer r=g.GetComponent<Renderer>();if(r!=null)r.material.color=Color.Lerp(side<0?StylizedArtDirection.ZeroEnergy:StylizedArtDirection.LumenEnergy,StylizedArtDirection.Ink,.38f);shards[i]=g.transform;origins[i]=p;}BuildTerrainDetails();BuildEnergyWisps();}


private void BuildEnergyWisps()
{
wisps=new Transform[8];wispOrigins=new Vector3[8];
for(int i=0;i<wisps.Length;i++)
{
int side=i<4?-1:1;float z=-24f+(i%4)*16f;Vector3 p=new Vector3(side*39f,.65f+(i%2)*.25f,z);
GameObject g=Part(side<0?"DF_ZeroWisp":"DF_LumenWisp",PrimitiveType.Sphere,p,Vector3.one*.16f,side<0?StylizedArtDirection.ZeroEnergy:StylizedArtDirection.LumenEnergy);
wisps[i]=g.transform;wispOrigins[i]=p;
}
}

private void BuildTerrainDetails()
{
Color rock=Color.Lerp(StylizedArtDirection.ArenaStone,StylizedArtDirection.Ink,.18f);
Color zeroRock=Color.Lerp(StylizedArtDirection.ZeroGround,rock,.55f),lumenRock=Color.Lerp(StylizedArtDirection.LumenGround,rock,.55f);
Vector3[] rocks={new(-31,0,-8),new(-27,0,8),new(-12,0,-28),new(-7,0,28),new(29,0,-7),new(26,0,8),new(13,0,-28),new(8,0,28)};
for(int i=0;i<rocks.Length;i++){bool left=rocks[i].x<0;RockCluster(rocks[i],left?zeroRock:lumenRock,i);}
Vector3[] craters={new(-18,.03f,5),new(-10,.03f,-7),new(16,.03f,-6),new(9,.03f,7),new(0,.03f,24),new(0,.03f,-24)};
for(int i=0;i<craters.Length;i++)Crater(craters[i],i);ObjectiveScatter(new Vector3(0,0,0));ObjectiveScatter(new Vector3(0,0,10));ObjectiveScatter(new Vector3(0,0,-3.2f));CentralImpactScars();
Vector3[] growth={new(-32,0,9),new(-30,0,-10),new(-15,0,27),new(-14,0,-27),new(32,0,10),new(30,0,-9),new(16,0,27),new(15,0,-27)};
for(int i=0;i<growth.Length;i++)Bush(growth[i],growth[i].x<0,i);BuildDebrisFields();BuildMicroScatter();BuildRealityGardens();BuildLaneEdgeClusters();BuildLargeBoulders();BuildDeadTrees();BuildErosionBands();BuildCraterFields();
}










private void BuildCraterFields()
{
Vector3[] centers={new(-30,.02f,25),new(-24,.02f,-25),new(29,.02f,25),new(23,.02f,-25)};
for(int n=0;n<centers.Length;n++)
{
for(int i=0;i<3;i++)
{
Vector3 p=centers[n]+new Vector3((i-1)*1.65f,0,(i%2==0?.75f:-.65f));
Crater(p,n*3+i);
}
}
}

private void BuildErosionBands()
{
Color dark=Color.Lerp(StylizedArtDirection.ArenaVoid,StylizedArtDirection.ArenaStone,.32f);
Vector3[] starts={new(-30,0,6),new(-26,0,-6),new(29,0,6),new(25,0,-6)};
for(int n=0;n<starts.Length;n++)
for(int i=0;i<4;i++)
{
float side=starts[n].x<0?-1f:1f;Vector3 p=starts[n]+new Vector3(side*i*1.15f,.025f,(i%2==0?.32f:-.28f));
GameObject g=Part("DF_ErosionStone",PrimitiveType.Cube,p,new Vector3(.72f-i*.09f,.055f,.3f+i*.06f),dark);g.transform.rotation=Quaternion.Euler(0,side*(18+i*7),0);
}
}

private void BuildDeadTrees()
{
Vector3[] sites={new(-34,0,29),new(-25,0,-29),new(34,0,29),new(25,0,-29)};
for(int n=0;n<sites.Length;n++)
{
bool zero=sites[n].x<0;Color c=Color.Lerp(zero?StylizedArtDirection.ZeroGround:StylizedArtDirection.LumenGround,StylizedArtDirection.ArenaStone,.38f);
GameObject trunk=Part(zero?"DF_ZeroDeadGrowth":"DF_LumenCrystalTree",PrimitiveType.Cube,sites[n]+Vector3.up*.75f,new Vector3(.22f,1.5f,.22f),c);trunk.transform.rotation=Quaternion.Euler(zero?12:0,n*33,zero?-9:0);
for(int i=0;i<3;i++){Vector3 p=sites[n]+new Vector3((i-1)*.38f,1.15f+i*.24f,(i%2==0?.16f:-.16f));GameObject branch=Part("DF_RealityBranch",PrimitiveType.Cube,p,new Vector3(.12f,.75f,.12f),Color.Lerp(c,StylizedArtDirection.ArenaStoneLight,.12f));branch.transform.rotation=Quaternion.Euler(zero?35:22,i*67,zero?(i-1)*28:(i-1)*18);}
}
}

private void BuildLargeBoulders()
{
Vector3[] sites={new(-39,0,31),new(-38,0,-30),new(-5,0,33),new(6,0,-33),new(39,0,30),new(38,0,-31)};
for(int n=0;n<sites.Length;n++)
{
bool zero=sites[n].x<0;Color c=Color.Lerp(StylizedArtDirection.ArenaStone,zero?StylizedArtDirection.ZeroGround:StylizedArtDirection.LumenGround,.22f);
for(int i=0;i<2;i++){Vector3 p=sites[n]+new Vector3(i*.8f,.45f+i*.18f,(i%2==0?-.35f:.35f));GameObject g=Part("DF_Boulder",PrimitiveType.Cube,p,new Vector3(1.25f-i*.18f,.9f+i*.22f,1.05f+i*.15f),c);g.transform.rotation=Quaternion.Euler(18+i*11,n*37+i*53,12-i*19);}
}
}

private void BuildLaneEdgeClusters()
{
foreach(float laneZ in new[]{-16f,16f})
for(int side=-1;side<=1;side+=2)
{
bool zero=side<0;Color rock=Color.Lerp(zero?StylizedArtDirection.ZeroGround:StylizedArtDirection.LumenGround,StylizedArtDirection.ArenaStone,.58f);
for(int group=0;group<2;group++)
{
Vector3 center=new Vector3(side*(10f+group*11f),0,laneZ+(group==0?5.2f:-5.2f));
RockCluster(center,rock,group+(zero?3:8));Bush(center+new Vector3(side*.9f,0,1.1f),zero,group+12);
}
}
}

private void BuildRealityGardens()
{
Vector3[] centers={new(-36,0,23),new(-35,0,-23),new(36,0,23),new(35,0,-23)};
for(int c=0;c<centers.Length;c++)
{
bool zero=centers[c].x<0;Color baseColor=zero?StylizedArtDirection.ZeroGround:StylizedArtDirection.LumenGround;
for(int i=0;i<6;i++)
{
float a=(i*60+c*17)*Mathf.Deg2Rad;float r=.7f+(i%3)*.45f;Vector3 p=centers[c]+new Vector3(Mathf.Cos(a)*r,.2f,Mathf.Sin(a)*r);
GameObject g=Part(zero?"DF_ZeroThorn":"DF_LumenReed",PrimitiveType.Cube,p,new Vector3(zero?.13f:.11f,.45f+(i%3)*.2f,zero?.18f:.11f),Color.Lerp(baseColor,StylizedArtDirection.ArenaStone,.18f));
g.transform.rotation=Quaternion.Euler(zero?22:0,i*60,zero?-24:0);
}
}
}

private void CentralImpactScars()
{
Color hot=Color.Lerp(StylizedArtDirection.ArenaStoneLight,StylizedArtDirection.Ink,.48f);
for(int i=0;i<7;i++)
{
float a=(i*51+11)*Mathf.Deg2Rad;float r=6.2f+(i%3)*1.15f;Vector3 from=new Vector3(Mathf.Cos(a)*r,.055f,Mathf.Sin(a)*r);Vector3 to=from+new Vector3(Mathf.Cos(a)*2.1f,0,Mathf.Sin(a)*2.1f);
for(int j=0;j<3;j++){Vector3 p=Vector3.Lerp(from,to,j/2f);GameObject g=Part("DF_ImpactScar",PrimitiveType.Cube,p,new Vector3(.09f,.035f,.75f),hot);g.transform.rotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg,0);}
}
}

private void BuildMicroScatter()
{
Color pebble=Color.Lerp(StylizedArtDirection.ArenaStone,StylizedArtDirection.ArenaVoid,.35f);
for(int i=0;i<28;i++)
{
float x=-37f+(i*17%73);float z=-31f+(i*23%61);
if(Mathf.Abs(z-16)<3.4f||Mathf.Abs(z+16)<3.4f||new Vector2(x,z).sqrMagnitude<150f)continue;
float size=.12f+(i%4)*.055f;GameObject g=Part("DF_Pebble",PrimitiveType.Cube,new Vector3(x,.045f,z),new Vector3(size,.07f,size*(1.2f+(i%3)*.25f)),pebble);g.transform.rotation=Quaternion.Euler(i%2*8,i*47,0);
}
}

private void ObjectiveScatter(Vector3 center)
{
Color c=Color.Lerp(StylizedArtDirection.ArenaStoneLight,StylizedArtDirection.ArenaStone,.58f);
for(int i=0;i<5;i++){float a=(i*72+18)*Mathf.Deg2Rad;float r=3.7f+(i%2)*.45f;Vector3 p=center+new Vector3(Mathf.Cos(a)*r,.12f,Mathf.Sin(a)*r);GameObject g=Part("DF_ObjectiveFragment",PrimitiveType.Cube,p,new Vector3(.28f,.28f+(i%2)*.18f,.42f),c);g.transform.rotation=Quaternion.Euler(15,i*57,22);}
}

private void BuildDebrisFields()
{
Vector3[] fields={new(-22,0,28),new(-21,0,-28),new(22,0,28),new(21,0,-28)};
for(int f=0;f<fields.Length;f++)
{
bool zero=fields[f].x<0;Color c=Color.Lerp(zero?StylizedArtDirection.ZeroGround:StylizedArtDirection.LumenGround,StylizedArtDirection.ArenaStone,.62f);
for(int i=0;i<7;i++){float a=(i*137+f*29)*Mathf.Deg2Rad;float r=.6f+(i%4)*.42f;Vector3 p=fields[f]+new Vector3(Mathf.Cos(a)*r,.08f+(i%2)*.06f,Mathf.Sin(a)*r);GameObject g=Part("DF_Debris",PrimitiveType.Cube,p,new Vector3(.22f+(i%3)*.12f,.12f+(i%2)*.08f,.28f+(i%4)*.08f),c);g.transform.rotation=Quaternion.Euler(i*11,i*43+f*17,i*7);}
}
}

private void RockCluster(Vector3 p,Color color,int seed)
{
for(int i=0;i<3;i++){GameObject g=Part("DF_ArenaRock",PrimitiveType.Cube,p+new Vector3((i-1)*.55f,.22f+(i%2)*.13f,((seed+i)%3-1)*.38f),new Vector3(.75f+i*.12f,.48f+(i%2)*.22f,.65f),color);g.transform.rotation=Quaternion.Euler(12+i*9,seed*31+i*47,18-i*7);}
}
private void Crater(Vector3 p,int seed)
{
Color rim=Color.Lerp(StylizedArtDirection.ArenaStone,StylizedArtDirection.Ink,.28f);
for(int i=0;i<8;i++){float a=i*Mathf.PI*.25f;Vector3 q=p+new Vector3(Mathf.Cos(a)*1.15f,.04f,Mathf.Sin(a)*1.15f);GameObject g=Part("DF_CraterRim",PrimitiveType.Cube,q,new Vector3(.62f,.12f,.34f),rim);g.transform.rotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg+seed*5,8);}
Part("DF_CraterCenter",PrimitiveType.Cylinder,p,new Vector3(.92f,.025f,.92f),StylizedArtDirection.Ink);
}
private void Bush(Vector3 p,bool zero,int seed)
{
Color c=Color.Lerp(zero?StylizedArtDirection.ZeroGround:StylizedArtDirection.LumenGround,StylizedArtDirection.ArenaStone,.32f);
for(int i=0;i<5;i++){float a=(i*72+seed*19)*Mathf.Deg2Rad;GameObject g=Part("DF_RealityGrowth",PrimitiveType.Cube,p+new Vector3(Mathf.Cos(a)*.42f,.35f,Mathf.Sin(a)*.42f),new Vector3(.18f,.72f,.18f),Color.Lerp(c,i%2==0?StylizedArtDirection.ArenaStoneLight:StylizedArtDirection.Ink,.12f));g.transform.rotation=Quaternion.Euler(18,i*72,zero?-28:28);}
}
private GameObject Part(string name,PrimitiveType type,Vector3 p,Vector3 scale,Color color)
{
GameObject g=GameObject.CreatePrimitive(type);g.name=name;g.transform.SetParent(transform,false);g.transform.position=p;g.transform.localScale=scale;Collider c=g.GetComponent<Collider>();if(c!=null)c.enabled=false;
if(!materials.TryGetValue(color,out Material m)){Shader shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)shader=Shader.Find("Standard");if(shader!=null){m=new Material(shader){color=color};if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",.08f);materials[color]=m;}}
if(m!=null)g.GetComponent<Renderer>().sharedMaterial=m;return g;
}

private void Update(){if(shards==null)return;float t=Time.time;for(int i=0;i<shards.Length;i++){Transform s=shards[i];if(s==null)continue;Vector3 p=origins[i];p.y+=Mathf.Sin(t*(.7f+(i%3)*.12f)+i)*.35f;s.position=p;s.Rotate(8*Time.deltaTime,18*Time.deltaTime,5*Time.deltaTime,Space.Self);}if(wisps!=null)for(int i=0;i<wisps.Length;i++){Transform w=wisps[i];if(w==null)continue;Vector3 p=wispOrigins[i];p.y+=Mathf.Sin(t*.9f+i)*.22f;p.z+=Mathf.Sin(t*.42f+i)*.55f;w.position=p;}}
private void OnDestroy(){foreach(Material m in materials.Values)if(m!=null)Destroy(m);}
}
}
