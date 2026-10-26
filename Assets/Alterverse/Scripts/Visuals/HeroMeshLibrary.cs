using System;
using System.Collections.Generic;
using UnityEngine;

namespace Alterverse.Visuals
{
    // Authored cross-sections, generated once, shared by every instance. No primitive GameObjects/colliders.
    internal static class HeroMeshLibrary
    {
        private static readonly Dictionary<string,Mesh> meshes=new();
        private static readonly Dictionary<(Color,int),Material> materials=new();
        private static readonly Dictionary<(Mesh,Color,bool,int),Mesh> painted=new();
        private static Material heroSurface;
        internal static Mesh Painted(Mesh mesh,Color color,bool energy,int finish=1)
        {
            var key=(mesh,color,energy,finish);if(painted.TryGetValue(key,out Mesh result)&&result!=null)return result;
            result=UnityEngine.Object.Instantiate(mesh);result.name=mesh.name+"_Palette";
            Paint(result,color,energy,finish);painted[key]=result;return result;
        }
        internal static void Paint(Mesh mesh,Color color,bool energy,int finish=1)
        {
            var colors=new Color[mesh.vertexCount];color.a=energy?1:0;
            var surface=new Vector2[mesh.vertexCount];
            // Coated armor gets a tighter, brighter highlight; undersuit fabric stays softer.
            Vector2 response=finish==0?new Vector2(.015f,.24f):finish==3?new Vector2(0f,.38f):new Vector2(.58f,.62f);
            Vector3[] vertices=mesh.vertices,normals=mesh.normals;
            Bounds bounds=mesh.bounds;
            float heightRange=Mathf.Max(.001f,bounds.max.y-bounds.min.y);
            for(int i=0;i<colors.Length;i++)
            {
                if(energy)colors[i]=color;
                else
                {
                    float height=Mathf.Clamp01((vertices[i].y-bounds.min.y)/heightRange);
                    float contour=normals.Length==colors.Length
                        ? .98f+.025f*Mathf.Max(0,normals[i].z)+.015f*Mathf.Abs(normals[i].x)
                        :1f;
                    float sculptShade=Mathf.Lerp(.96f,1.04f,height)*contour;
                    Color shaded=color*sculptShade;shaded.a=0;colors[i]=shaded;
                }
                // Baked low-frequency coating variation avoids a uniform plastic highlight.
                float grain=Mathf.PerlinNoise(vertices[i].x*23f+17f,vertices[i].y*19f+vertices[i].z*11f);
                surface[i]=new Vector2(response.x,Mathf.Clamp01(response.y+(grain-.5f)*.12f));
            }
            mesh.colors=colors;mesh.uv2=surface;
        }
        internal static Mesh Loft(string key,float[] y,float[] width,float[] depth,float[] forward=null,int sides=16)
        {
            if(meshes.TryGetValue(key,out Mesh cached)&&cached!=null)return cached;
            // Spend extra geometry on the hero's silhouette. Small bolts and shell inlays
            // keep their original budget, while heads, bodies and limbs get softer contours.
            if(IsAnatomicalSurface(key))
            {
                y=RefineSections(y,false);width=RefineSections(width,true);depth=RefineSections(depth,true);
                if(forward!=null)forward=RefineSections(forward,false);
                sides=Mathf.Max(32,sides);
            }
            var v=new List<Vector3>();var normals=new List<Vector3>();var t=new List<int>();
            Vector3 Point(int ring,int side)
            {
                float angle=(side+.5f)*Mathf.PI*2/sides;
                return new Vector3(Mathf.Cos(angle)*width[ring],y[ring],Mathf.Sin(angle)*depth[ring]+(forward==null?0:forward[ring]));
            }
            // Shared rings and analytic elliptical normals remove the old per-quad facets.
            // Caps get their own vertices so their edges stay crisp. No geometry is rebuilt in Update.
            for(int ring=0;ring<y.Length;ring++)for(int side=0;side<sides;side++)
            {
                int lo=Mathf.Max(0,ring-1),hi=Mathf.Min(y.Length-1,ring+1);
                float angle=(side+.5f)*Mathf.PI*2/sides,c=Mathf.Cos(angle),sn=Mathf.Sin(angle);
                Vector3 rise=new Vector3(c*(width[hi]-width[lo]),y[hi]-y[lo],
                    sn*(depth[hi]-depth[lo])+(forward==null?0:forward[hi]-forward[lo]));
                Vector3 around=new Vector3(-sn*width[ring],0,c*depth[ring]);
                v.Add(Point(ring,side));normals.Add(Vector3.Cross(rise,around).normalized);
            }
            for(int ring=0;ring<y.Length-1;ring++)for(int side=0;side<sides;side++)
            {
                int next=(side+1)%sides,a=ring*sides+side,b=(ring+1)*sides+side;
                int c=(ring+1)*sides+next,d=ring*sides+next;
                t.Add(a);t.Add(b);t.Add(c);t.Add(a);t.Add(c);t.Add(d);
            }
            for(int cap=0;cap<2;cap++)
            {
                int ring=cap==0?0:y.Length-1,start=v.Count;
                for(int side=0;side<sides;side++){v.Add(Point(ring,side));normals.Add(cap==0?Vector3.down:Vector3.up);}
                for(int side=1;side<sides-1;side++)
                {t.Add(start);t.Add(start+(cap==0?side:side+1));t.Add(start+(cap==0?side+1:side));}
            }
            Mesh mesh=new Mesh{name=key};mesh.SetVertices(v);mesh.SetNormals(normals);mesh.SetTriangles(t,0);mesh.RecalculateBounds();
            meshes[key]=mesh;return mesh;
        }
        private static bool IsAnatomicalSurface(string key)
        {
            return key.IndexOf("Torso",StringComparison.OrdinalIgnoreCase)>=0
                ||key.IndexOf("Pelvis",StringComparison.OrdinalIgnoreCase)>=0
                ||key.IndexOf("Leg",StringComparison.OrdinalIgnoreCase)>=0
                ||key.IndexOf("Boot",StringComparison.OrdinalIgnoreCase)>=0
                ||key.IndexOf("UpperArm",StringComparison.OrdinalIgnoreCase)>=0
                ||key.IndexOf("Forearm",StringComparison.OrdinalIgnoreCase)>=0
                ||key.IndexOf("Glove",StringComparison.OrdinalIgnoreCase)>=0
                ||key.IndexOf("Head",StringComparison.OrdinalIgnoreCase)>=0
                ||key.IndexOf("Helmet",StringComparison.OrdinalIgnoreCase)>=0
                ||key.IndexOf("Face",StringComparison.OrdinalIgnoreCase)>=0
                ||key.IndexOf("Neck",StringComparison.OrdinalIgnoreCase)>=0
                ||key.IndexOf("Greave",StringComparison.OrdinalIgnoreCase)>=0
                ||key.IndexOf("Hood",StringComparison.OrdinalIgnoreCase)>=0
                ||key.IndexOf("Cuirass",StringComparison.OrdinalIgnoreCase)>=0
                ||key.IndexOf("Chest",StringComparison.OrdinalIgnoreCase)>=0
                ||key.IndexOf("Shoulder",StringComparison.OrdinalIgnoreCase)>=0
                ||key.IndexOf("Gauntlet",StringComparison.OrdinalIgnoreCase)>=0
                ||key.IndexOf("Shell",StringComparison.OrdinalIgnoreCase)>=0
                ||key.IndexOf("Jaw",StringComparison.OrdinalIgnoreCase)>=0
                ||key.IndexOf("Hair",StringComparison.OrdinalIgnoreCase)>=0;
        }
        internal static int FinishFor(string name,Color color)
        {
            string n=name.ToLowerInvariant();
            bool skinPart=n.Contains("face")||n.Contains("head")||n.Contains("nose")||n.Contains("lip")||n.Contains("ear")||n.Contains("finger");
            bool armor=n.Contains("guard")||n.Contains("helmet")||n.Contains("plate")||n.Contains("optic")||n.Contains("mask");
            bool skinColor=color.r>.35f&&color.r>color.g*1.12f&&color.r>color.b*1.2f;
            if(skinPart&&!armor&&skinColor)return 3;
            if(n.Contains("robe")||n.Contains("cape")||n.Contains("coat")||n.Contains("hair")||
               n.Contains("veil")||n.Contains("scarf")||n.Contains("hood")||n.Contains("leather")||
               n.Contains("glove")||n.Contains("undersuit")||color.maxColorComponent<.17f)return 0;
            return 1;
        }
        private static float[] RefineSections(float[] source,bool positive)
        {
            if(source==null||source.Length<2)return source;
            const int subdivisions=4;
            var refined=new List<float>((source.Length-1)*subdivisions+1);
            for(int i=0;i<source.Length-1;i++)
            {
                float p0=source[Mathf.Max(0,i-1)],p1=source[i];
                float p2=source[i+1],p3=source[Mathf.Min(source.Length-1,i+2)];
                for(int j=0;j<subdivisions;j++)
                {
                    float t=j/(float)subdivisions;
                    float value=.5f*(2f*p1+(-p0+p2)*t+(2f*p0-5f*p1+4f*p2-p3)*t*t+(-p0+3f*p1-3f*p2+p3)*t*t*t);
                    // Avoid anatomical overshoot and retain every original authored anchor.
                    value=Mathf.Clamp(value,Mathf.Min(p1,p2),Mathf.Max(p1,p2));
                    if(positive)value=Mathf.Max(.002f,value);
                    refined.Add(value);
                }
            }
            refined.Add(source[source.Length-1]);return refined.ToArray();
        }
        internal static Mesh Ribbon(string key,Vector3[] centers,float[] widths,float thickness)
        {
            if(meshes.TryGetValue(key,out Mesh cached)&&cached!=null)return cached;
            string lower=key.ToLowerInvariant();
            if(lower.Contains("cape")||lower.Contains("coat")||lower.Contains("robe")||lower.Contains("veil")||
               lower.Contains("scarf")||lower.Contains("hair")||lower.Contains("hood")||lower.Contains("tail"))
            {
                Mesh cloth=HeroSculptGeometry.DrapedRibbon(key,centers,widths,thickness);
                meshes[key]=cloth;return cloth;
            }
            var v=new List<Vector3>();var t=new List<int>();
            for(int i=0;i<centers.Length-1;i++)
            {
                Vector3 a=centers[i],b=centers[i+1],left=Vector3.left,right=Vector3.right;
                Vector3 al=a+left*widths[i],ar=a+right*widths[i],bl=b+left*widths[i+1],br=b+right*widths[i+1];
                Vector3 z=Vector3.forward*thickness;
                Face(v,t,al,ar,br,bl);Face(v,t,bl-z,br-z,ar-z,al-z);
                Face(v,t,al-z,al,bl,bl-z);Face(v,t,ar,ar-z,br-z,br);
            }
            Mesh mesh=new Mesh{name=key};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            meshes[key]=mesh;return mesh;
        }
        internal static Mesh Ring(string key,float radius,float tube)
        {
            if(meshes.TryGetValue(key,out Mesh m)&&m!=null)return m;
            m=ProductionMeshFactory.Ring(radius,tube,40,8);m.name=key;meshes[key]=m;return m;
        }
        private static void Face(List<Vector3> v,List<int> t,params Vector3[] p)
        {int start=v.Count;v.AddRange(p);for(int i=1;i<p.Length-1;i++){t.Add(start);t.Add(start+i);t.Add(start+i+1);}}
        internal static Material Surface(Color color,int finish)
        {
            if(heroSurface==null)heroSurface=Resources.Load<Material>("HeroArt/HeroSurface");
            if(heroSurface!=null)return heroSurface;
            var key=(color,finish);if(materials.TryGetValue(key,out Material cached)&&cached!=null)return cached;
            Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");if(shader==null)return null;
            Material m=new Material(shader){name="DF_SharedHeroSurface",color=color,enableInstancing=true};
            if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",color);
            m.SetFloat("_Metallic",finish==1?.58f:0);m.SetFloat("_Smoothness",finish==1?.62f:finish==2?.58f:finish==3?.38f:.24f);
            if(finish==2){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*.85f);}
            materials[key]=m;return m;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            foreach(var mesh in meshes.Values)if(mesh!=null)UnityEngine.Object.Destroy(mesh);
            foreach(var mat in materials.Values)if(mat!=null)UnityEngine.Object.Destroy(mat);
            foreach(var mesh in painted.Values)if(mesh!=null)UnityEngine.Object.Destroy(mesh);
            meshes.Clear();materials.Clear();painted.Clear();heroSurface=null;
        }
    }
}

