using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    // One mesh per surface palette entry; all decoration is collider-free.
    public sealed class StylizedArenaOverlay : MonoBehaviour
    {
        private sealed class Batch
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<int> Triangles = new List<int>();
            public Material Material;
        }
        private readonly List<Batch> batches = new List<Batch>();
        private readonly List<Mesh> meshes = new List<Mesh>();

        private void Start()
        {
            int soil = Palette(StylizedArtDirection.ArenaVoid);
            int stone = Palette(StylizedArtDirection.ArenaStone);
            int lightStone = Palette(StylizedArtDirection.ArenaStoneLight);
            int inset = Palette(StylizedArtDirection.ArenaCenter);
            int zero = Palette(StylizedArtDirection.ZeroGround);
            int lumen = Palette(StylizedArtDirection.LumenGround);
            int objective = Palette(StylizedArtDirection.ObjectiveGold);
            if (soil < 0) return;

            // Large quiet panels break up the flat floor without competing with heroes.
            for (int x=-38;x<40;x+=6)
            for (int z=-32;z<34;z+=6)
            {
                if (Mathf.Abs(z-16)<5 || Mathf.Abs(z+16)<5 || (Mathf.Abs(x)<13 && Mathf.Abs(z)<13)) continue;
                Tile(x,z,5.7f,5.7f,.025f,((x+z)%3==0)?soil:inset,.65f);
            }
            foreach(float z in new[]{-16f,16f})
            {
                Path(new Vector2(-35,z),new Vector2(35,z),4.3f,.082f,stone,lightStone);Ribbon(new Vector2(-34,z),new Vector2(-3,z),.24f,.096f,zero);Ribbon(new Vector2(3,z),new Vector2(34,z),.24f,.096f,lumen);
                Ribbon(new Vector2(-35,z-2.35f),new Vector2(-1,z-2.35f),.13f,.09f,zero);
                Ribbon(new Vector2(-35,z+2.35f),new Vector2(-1,z+2.35f),.13f,.09f,zero);
                Ribbon(new Vector2(1,z-2.35f),new Vector2(35,z-2.35f),.13f,.09f,lumen);
                Ribbon(new Vector2(1,z+2.35f),new Vector2(35,z+2.35f),.13f,.09f,lumen);
                // Four diagonal approaches lead toward the central objective court.
                Path(new Vector2(-19,z),new Vector2(-7,z*.38f),3.2f,.074f,inset,stone);
                Path(new Vector2(19,z),new Vector2(7,z*.38f),3.2f,.074f,inset,stone);
            }
            // Fracture seams point toward the contested center and break up broad empty slabs.
            for(int side=-1;side<=1;side+=2)
            {
                int fracture=side<0?zero:lumen;
                for(int i=0;i<5;i++)
                {
                    float z=-24f+i*12f;
                    Vector2 a=new Vector2(side*38f,z);
                    Vector2 b=new Vector2(side*(18f+i%2*2f),z+(i%2==0?2.5f:-2.5f));
                    Ribbon(a,b,.09f,.116f,fracture);
                    Vector2 mid=Vector2.Lerp(a,b,.58f);
                    Ribbon(mid,mid+new Vector2(-side*2.4f,i%2==0?2f:-2f),.07f,.117f,fracture);
                }
            }
            // Small warm objective chevrons guide the eye inward without painting the combat lanes.
            foreach(float z in new[]{-16f,16f})
            for(int side=-1;side<=1;side+=2)
            {
                for(int i=0;i<3;i++)
                {
                    float x=side*(12f-i*2.4f);
                    Ribbon(new Vector2(x,z+side*.55f),new Vector2(x-side*1.15f,z),.11f,.119f,objective);
                    Ribbon(new Vector2(x-side*1.15f,z),new Vector2(x,z-side*.55f),.11f,.119f,objective);
                }
            }
            Disc(Vector2.zero,11.6f,.10f,32,inset);
            Ring(Vector2.zero,10.9f,11.5f,.105f,32,stone);
            Ring(Vector2.zero,7.8f,8f,.108f,32,lightStone);Ring(Vector2.zero,5.55f,5.72f,.113f,32,objective);
            // Split inlay reinforces the two realities, while leaving the animated front legible.
            for(int i=0;i<16;i++)
            {
                float a=i*Mathf.PI*2/16;
                Vector2 p=new Vector2(Mathf.Cos(a),Mathf.Sin(a))*9.8f;
                Tile(p.x,p.y,.65f,.65f,.112f,p.x<0?zero:lumen,.15f);
            }
            // Broad faction territories make the two realities readable at a glance without recoloring combat space.\n            for(int side=-1;side<=1;side+=2)\n            {\n                int territory=side<0?zero:lumen;\n                float cx=side*27f;\n                Tile(cx,0,15.5f,10.5f,.041f,territory,.8f);\n                Tile(cx,27f,18f,5.2f,.039f,territory,.65f);\n                Tile(cx,-27f,18f,5.2f,.039f,territory,.65f);\n            }\n            foreach(int side in new[]{-1,1})
            {
                int trim=side<0?zero:lumen;
                Vector2 baseCenter=new Vector2(side*35,0);
                Disc(baseCenter,6.2f,.10f,24,inset);
                Ring(baseCenter,5.65f,6.2f,.115f,24,stone);
                Ring(baseCenter,4.95f,5.10f,.119f,24,trim);
                foreach(float z in new[]{-16f,16f})
                {
                    Vector2 tower=new Vector2(side*21,z);
                    Disc(tower,2.65f,.11f,12,inset);
                    Ring(tower,2.35f,2.65f,.12f,12,trim);
                    Path(new Vector2(side*34,side<0?-4:4),new Vector2(side*25,z),3f,.07f,stone,inset);
                }
            }
            Flush();
        }

        private int Palette(Color color)
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit");
            if(shader==null)shader=Shader.Find("Standard");
            if(shader==null)return -1;
            Material material=new Material(shader){color=color};
            if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",.12f);
            if(material.HasProperty("_Metallic"))material.SetFloat("_Metallic",.05f);
            batches.Add(new Batch{Material=material});return batches.Count-1;
        }

        private void Tile(float x,float z,float w,float h,float y,int palette,float bevel)
        {
            float a=w*.5f,b=h*.5f,c=Mathf.Min(bevel,Mathf.Min(a,b)*.45f);
            Polygon(palette,new[]{
                new Vector3(x-a+c,y,z-b),new Vector3(x+a-c,y,z-b),
                new Vector3(x+a,y,z-b+c),new Vector3(x+a,y,z+b-c),
                new Vector3(x+a-c,y,z+b),new Vector3(x-a+c,y,z+b),
                new Vector3(x-a,y,z+b-c),new Vector3(x-a,y,z-b+c)});
        }

        private void Path(Vector2 from,Vector2 to,float width,float y,int a,int b)
        {
            float length=Vector2.Distance(from,to);int count=Mathf.Max(1,Mathf.CeilToInt(length/3.4f));
            for(int i=0;i<count;i++)
            {
                float margin=.025f/count;
                Vector2 start=Vector2.Lerp(from,to,(float)i/count+margin);
                Vector2 end=Vector2.Lerp(from,to,(float)(i+1)/count-margin);
                Ribbon(start,end,width,y,i%4==0?b:a);
            }
        }

        private void Ribbon(Vector2 from,Vector2 to,float width,float y,int palette)
        {
            Vector2 direction=(to-from).normalized,normal=new Vector2(-direction.y,direction.x)*width*.5f;
            Polygon(palette,new[]{Point(from-normal,y),Point(from+normal,y),Point(to+normal,y),Point(to-normal,y)});
        }

        private static Vector3 Point(Vector2 p,float y)=>new Vector3(p.x,y,p.y);
        private void Disc(Vector2 center,float radius,float y,int count,int palette)
        {
            var points=new Vector3[count];
            for(int i=0;i<count;i++){float a=i*Mathf.PI*2/count;points[i]=Point(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,y);}
            Polygon(palette,points);
        }

        private void Ring(Vector2 center,float inner,float outer,float y,int count,int palette)
        {
            for(int i=0;i<count;i++)
            {
                float a=i*Mathf.PI*2/count+.008f,b=(i+1)*Mathf.PI*2/count-.008f;
                Vector2 da=new Vector2(Mathf.Cos(a),Mathf.Sin(a)),db=new Vector2(Mathf.Cos(b),Mathf.Sin(b));
                Polygon(palette,new[]{Point(center+da*inner,y),Point(center+db*inner,y),Point(center+db*outer,y),Point(center+da*outer,y)});
            }
        }

        private void Polygon(int palette,Vector3[] points)
        {
            if(palette<0)return;
            Batch batch=batches[palette];int first=batch.Vertices.Count;
            bool reverse=Vector3.Cross(points[1]-points[0],points[2]-points[0]).y<0;
            batch.Vertices.AddRange(points);
            for(int i=1;i<points.Length-1;i++)
            {
                batch.Triangles.Add(first);
                batch.Triangles.Add(first+(reverse?i+1:i));
                batch.Triangles.Add(first+(reverse?i:i+1));
            }
        }

        private void Flush()
        {
            for(int i=0;i<batches.Count;i++)
            {
                Batch batch=batches[i];if(batch.Vertices.Count==0)continue;
                Mesh mesh=new Mesh{name="DF_ArenaSurface_"+i};
                mesh.SetVertices(batch.Vertices);mesh.SetTriangles(batch.Triangles,0);
                mesh.RecalculateNormals();mesh.RecalculateBounds();meshes.Add(mesh);
                GameObject g=new GameObject(mesh.name);g.transform.SetParent(transform,false);
                g.AddComponent<MeshFilter>().sharedMesh=mesh;
                MeshRenderer renderer=g.AddComponent<MeshRenderer>();renderer.sharedMaterial=batch.Material;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;
            }
        }

        private void OnDestroy()
        {
            foreach(Batch batch in batches)if(batch.Material!=null)Destroy(batch.Material);
            foreach(Mesh mesh in meshes)if(mesh!=null)Destroy(mesh);
        }
    }
}
