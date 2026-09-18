using UnityEngine;
namespace Alterverse.Visuals
{
public sealed class ArenaAtmosphere:MonoBehaviour
{
private Transform[] shards;private Vector3[] origins;private readonly System.Collections.Generic.Dictionary<Color,Material> materials=new();private void Start(){const int count=16;shards=new Transform[count];origins=new Vector3[count];for(int i=0;i<count;i++){int side=i%2==0?-1:1;float z=-31+(i/2)*8.8f;Vector3 p=new Vector3(side*(44+(i%3)*1.2f),1.3f+(i%4)*.45f,z);GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=side<0?"DF_ZeroAmbientShard":"DF_LumenAmbientShard";g.transform.SetParent(transform,false);g.transform.position=p;g.transform.localScale=new Vector3(.12f,.55f+.1f*(i%3),.12f);g.transform.rotation=Quaternion.Euler(18,i*37,24);Collider c=g.GetComponent<Collider>();if(c!=null)c.enabled=false;Renderer r=g.GetComponent<Renderer>();if(r!=null)r.material.color=Color.Lerp(side<0?StylizedArtDirection.ZeroEnergy:StylizedArtDirection.LumenEnergy,StylizedArtDirection.Ink,.38f);shards[i]=g.transform;origins[i]=p;}BuildTerrainDetails();}

private void BuildTerrainDetails()
{
Color rock=Color.Lerp(StylizedArtDirection.ArenaStone,StylizedArtDirection.Ink,.18f);
Color zeroRock=Color.Lerp(StylizedArtDirection.ZeroGround,rock,.55f),lumenRock=Color.Lerp(StylizedArtDirection.LumenGround,rock,.55f);
Vector3[] rocks={new(-31,0,-8),new(-27,0,8),new(-12,0,-28),new(-7,0,28),new(29,0,-7),new(26,0,8),new(13,0,-28),new(8,0,28)};
for(int i=0;i<rocks.Length;i++){bool left=rocks[i].x<0;RockCluster(rocks[i],left?zeroRock:lumenRock,i);}
Vector3[] craters={new(-18,.03f,5),new(-10,.03f,-7),new(16,.03f,-6),new(9,.03f,7),new(0,.03f,24),new(0,.03f,-24)};
for(int i=0;i<craters.Length;i++)Crater(craters[i],i);ObjectiveScatter(new Vector3(0,0,0));ObjectiveScatter(new Vector3(0,0,10));ObjectiveScatter(new Vector3(0,0,-3.2f));
Vector3[] growth={new(-32,0,9),new(-30,0,-10),new(-15,0,27),new(-14,0,-27),new(32,0,10),new(30,0,-9),new(16,0,27),new(15,0,-27)};
for(int i=0;i<growth.Length;i++)Bush(growth[i],growth[i].x<0,i);BuildDebrisFields();BuildMicroScatter();
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
Color c=Color.Lerp(StylizedArtDirection.ObjectiveGold,StylizedArtDirection.ArenaStone,.55f);
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
Color c=zero?new Color(.34f,.16f,.48f):new Color(.08f,.48f,.42f);
for(int i=0;i<5;i++){float a=(i*72+seed*19)*Mathf.Deg2Rad;GameObject g=Part("DF_RealityGrowth",PrimitiveType.Cube,p+new Vector3(Mathf.Cos(a)*.42f,.35f,Mathf.Sin(a)*.42f),new Vector3(.18f,.72f,.18f),Color.Lerp(c,i%2==0?Color.white:StylizedArtDirection.Ink,.18f));g.transform.rotation=Quaternion.Euler(18,i*72,zero?-28:28);}
}
private GameObject Part(string name,PrimitiveType type,Vector3 p,Vector3 scale,Color color)
{
GameObject g=GameObject.CreatePrimitive(type);g.name=name;g.transform.SetParent(transform,false);g.transform.position=p;g.transform.localScale=scale;Collider c=g.GetComponent<Collider>();if(c!=null)c.enabled=false;
if(!materials.TryGetValue(color,out Material m)){Shader shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)shader=Shader.Find("Standard");if(shader!=null){m=new Material(shader){color=color};if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",.08f);materials[color]=m;}}
if(m!=null)g.GetComponent<Renderer>().sharedMaterial=m;return g;
}

private void Update(){if(shards==null)return;float t=Time.time;for(int i=0;i<shards.Length;i++){Transform s=shards[i];if(s==null)continue;Vector3 p=origins[i];p.y+=Mathf.Sin(t*(.7f+(i%3)*.12f)+i)*.35f;s.position=p;s.Rotate(8*Time.deltaTime,18*Time.deltaTime,5*Time.deltaTime,Space.Self);}}
private void OnDestroy(){foreach(Material m in materials.Values)if(m!=null)Destroy(m);}
}
}
