using System.Collections.Generic;
using UnityEngine;

namespace Alterverse.Visuals
{
    // The lower crown band is the head-seat plane (y=0); +Z faces forward.
    public static class BetaCapMesh
    {
        public static Mesh Build(Matrix4x4 toHead)
        {
            var vertices=new List<Vector3>();
            var indices=new[]{new List<int>(),new List<int>(),new List<int>()};
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,int material)
            {
                int n=vertices.Count;
                vertices.Add(toHead.MultiplyPoint3x4(a));vertices.Add(toHead.MultiplyPoint3x4(b));
                vertices.Add(toHead.MultiplyPoint3x4(c));vertices.Add(toHead.MultiplyPoint3x4(d));
                indices[material].AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            }
            void Triangle(Vector3 a,Vector3 b,Vector3 c,int material)
            {
                int n=vertices.Count;vertices.Add(toHead.MultiplyPoint3x4(a));
                vertices.Add(toHead.MultiplyPoint3x4(b));vertices.Add(toHead.MultiplyPoint3x4(c));
                indices[material].AddRange(new[]{n,n+1,n+2});
            }
            float Radius(float t)=>1f-.08f*t-.92f*t*t*t*t;
            Vector3 Crown(float a,float t,float extra=0f)=>new Vector3(
                Mathf.Sin(a)*(Radius(t)+extra),t*1.05f,Mathf.Cos(a)*(Radius(t)+extra)*.94f);
            const int sides=36,rows=8;
            float turn=Mathf.PI*2;
            for(int side=0;side<sides;side++)
            {
                float a=side*turn/sides,b=(side+1)*turn/sides;
                bool front=side<6||side>=sides-6;
                for(int row=0;row<rows;row++)
                {
                    float low=row/(float)rows*.94f,high=(row+1)/(float)rows*.94f;
                    if(front)
                        Quad(Crown(a,low),Crown(b,low),Crown(b,high),Crown(a,high),2);
                    else
                    {
                        // Real openings in the trucker mesh, with an inner surface on each thread.
                        float rib=(b-a)*.20f,thread=(high-low)*.20f;
                        Quad(Crown(a,low),Crown(a+rib,low),Crown(a+rib,high),Crown(a,high),0);
                        Quad(Crown(a+rib,low),Crown(b,low),Crown(b,low+thread),Crown(a+rib,low+thread),0);
                        Quad(Crown(a,low,-.018f),Crown(a,high,-.018f),Crown(a+rib,high,-.018f),Crown(a+rib,low,-.018f),0);
                        Quad(Crown(a+rib,low,-.018f),Crown(a+rib,low+thread,-.018f),Crown(b,low+thread,-.018f),Crown(b,low,-.018f),0);
                    }
                }
                Triangle(Crown(a,.94f),Crown(b,.94f),new Vector3(0,1.05f,0),0);
                Quad(Crown(a,0,.01f),Crown(b,0,.01f),Crown(b,.06f,.01f),Crown(a,.06f,.01f),0);
                Quad(Crown(b,0,-.025f),Crown(a,0,-.025f),Crown(a,.06f,-.025f),Crown(b,.06f,-.025f),0);
                Quad(Crown(a,0,-.025f),Crown(a,0,.01f),Crown(b,0,.01f),Crown(b,0,-.025f),0);
            }
            foreach(float a in new[]{Mathf.PI/3,turn-Mathf.PI/3})
                for(int row=0;row<8;row++)
                    Quad(Crown(a-.012f,row*.115f,.012f),Crown(a+.012f,row*.115f,.012f),
                         Crown(a+.012f,(row+1)*.115f,.012f),Crown(a-.012f,(row+1)*.115f,.012f),1);
            Vector3 Bill(float angle,float t,float down=0)=>new Vector3(
                Mathf.Sin(angle)*(1f+t*.04f),-t*.10f-.055f*Mathf.Sin(angle)*Mathf.Sin(angle)*t-down,
                Mathf.Cos(angle)*(.94f+t*.84f));
            const int billSides=24;
            for(int side=0;side<billSides;side++)
            {
                float a=-Mathf.PI/3+side*(Mathf.PI*2/3)/billSides,b=a+(Mathf.PI*2/3)/billSides;
                Quad(Bill(a,0),Bill(a,1),Bill(b,1),Bill(b,0),0);
                Quad(Bill(b,0,.035f),Bill(b,1,.035f),Bill(a,1,.035f),Bill(a,0,.035f),0);
                Quad(Bill(a,1),Bill(a,1,.035f),Bill(b,1,.035f),Bill(b,1),1);
                Quad(Bill(a,.85f,-.002f),Bill(a,.87f,-.002f),Bill(b,.87f,-.002f),Bill(b,.85f,-.002f),1);
            }
            foreach(float angle in new[]{-Mathf.PI/3,Mathf.PI/3})
                Quad(Bill(angle,0),Bill(angle,0,.035f),Bill(angle,1,.035f),Bill(angle,1),0);
            Vector3 Front(float x,float y,float lift)=>new Vector3(x,y,
                Mathf.Sqrt(Mathf.Max(0,Radius(y/1.05f)*Radius(y/1.05f)-x*x))*.94f+lift);
            // Curved fabric patch follows the front panel rather than floating ahead of it.
            for(int row=0;row<4;row++)for(int col=0;col<10;col++)
            {
                float x=-.53f+col*.106f,y=.30f+row*.105f;
                Quad(Front(x,y,.009f),Front(x+.106f,y,.009f),
                     Front(x+.106f,y+.105f,.009f),Front(x,y+.105f,.009f),0);
            }
            string[] glyphs={"11110100011000111110100011000111110","11111100001000011110100001000011111",
                "11111001000010000100001000010000100","01110100011000111111100011000110001"};
            const float pixel=.039f;
            for(int letter=0;letter<4;letter++)for(int row=0;row<7;row++)for(int col=0;col<5;col++)
            {
                if(glyphs[letter][row*5+col]!='1')continue;
                float x=-23*pixel*.5f+(letter*6+col)*pixel,y=.66f-(row+1)*pixel;
                Quad(Front(x,y,.019f),Front(x+pixel*.82f,y,.019f),
                    Front(x+pixel*.82f,y+pixel*.82f,.019f),Front(x,y+pixel*.82f,.019f),2);
            }
            var mesh=new Mesh{name="DF_BetaTruckerCap",subMeshCount=3};
            mesh.SetVertices(vertices);
            for(int i=0;i<indices.Length;i++)mesh.SetTriangles(indices[i],i);
            mesh.RecalculateNormals();mesh.RecalculateBounds();
            return mesh;
        }
    }

    public static class ProceduralHeadwearMeshes
    {
        public static bool IsProcedural(Alterverse.Shop.UniversalHeroCosmetic id) =>
            (int)id >= (int)Alterverse.Shop.UniversalHeroCosmetic.TvHead &&
            (int)id <= (int)Alterverse.Shop.UniversalHeroCosmetic.PirateHat;

        public static bool IsFullHead(Alterverse.Shop.UniversalHeroCosmetic id) =>
            id == Alterverse.Shop.UniversalHeroCosmetic.TvHead ||
            id == Alterverse.Shop.UniversalHeroCosmetic.SharkHeadMask ||
            id == Alterverse.Shop.UniversalHeroCosmetic.CyberPumpkinHead ||
            id == Alterverse.Shop.UniversalHeroCosmetic.CardboardRobotHead;

        public static float WidthAdjustment(Alterverse.Shop.UniversalHeroCosmetic id)
        {
            if (id == Alterverse.Shop.UniversalHeroCosmetic.GoldenCrown) return .88f;
            if (id == Alterverse.Shop.UniversalHeroCosmetic.VikingHelm) return 1.38f;
            if (id == Alterverse.Shop.UniversalHeroCosmetic.ChefHat) return .98f;
            if (id == Alterverse.Shop.UniversalHeroCosmetic.PirateHat) return 1.36f;
            return 1f;
        }

        public static bool TryBuild(Alterverse.Shop.UniversalHeroCosmetic id, Shader shader, out Mesh mesh, out Material[] materials)
        {
            mesh = null;
            materials = null;
            if (!IsProcedural(id) || shader == null) return false;

            var builder = new Builder();
            Color[] palette;
            float[] metallic;
            float[] smoothness;
            float[] emission;

            switch (id)
            {
                case Alterverse.Shop.UniversalHeroCosmetic.TvHead:
                    BuildTvHead(builder);
                    palette = new[] { new Color(.14f, .20f, .32f), new Color(.78f, .84f, .92f), new Color(.12f, .96f, 1f) };
                    metallic = new[] { .35f, .85f, .10f };
                    smoothness = new[] { .55f, .82f, .92f };
                    emission = new[] { 0f, 0f, 2.2f };
                    break;
                case Alterverse.Shop.UniversalHeroCosmetic.SharkHeadMask:
                    BuildSharkHead(builder);
                    palette = new[] { new Color(.16f, .48f, .82f), new Color(.95f, .97f, 1f), new Color(1f, .78f, .16f) };
                    metallic = new[] { .18f, .10f, .20f };
                    smoothness = new[] { .68f, .55f, .90f };
                    emission = new[] { 0f, 0f, 1.8f };
                    break;
                case Alterverse.Shop.UniversalHeroCosmetic.CyberPumpkinHead:
                    BuildCyberPumpkin(builder);
                    palette = new[] { new Color(.98f, .44f, .08f), new Color(.22f, .32f, .14f), new Color(.48f, 1f, .18f) };
                    metallic = new[] { .12f, .08f, .10f };
                    smoothness = new[] { .52f, .35f, .92f };
                    emission = new[] { 0f, 0f, 2.6f };
                    break;
                case Alterverse.Shop.UniversalHeroCosmetic.CardboardRobotHead:
                    BuildCardboardRobot(builder);
                    palette = new[] { new Color(.76f, .58f, .36f), new Color(.84f, .88f, .94f), new Color(1f, .22f, .26f) };
                    metallic = new[] { .05f, .88f, .15f };
                    smoothness = new[] { .22f, .85f, .90f };
                    emission = new[] { 0f, 0f, 2.4f };
                    break;
                case Alterverse.Shop.UniversalHeroCosmetic.GoldenCrown:
                    BuildGoldenCrown(builder);
                    palette = new[] { new Color(1f, .78f, .18f), new Color(.78f, .12f, .22f), new Color(.14f, .94f, .88f) };
                    metallic = new[] { .92f, .08f, .25f };
                    smoothness = new[] { .88f, .35f, .95f };
                    emission = new[] { .25f, 0f, 1.8f };
                    break;
                case Alterverse.Shop.UniversalHeroCosmetic.VikingHelm:
                    BuildVikingHelm(builder);
                    palette = new[] { new Color(.34f, .38f, .45f), new Color(.94f, .90f, .78f), new Color(.95f, .74f, .24f) };
                    metallic = new[] { .86f, .12f, .88f };
                    smoothness = new[] { .68f, .48f, .82f };
                    emission = new[] { 0f, 0f, .20f };
                    break;
                case Alterverse.Shop.UniversalHeroCosmetic.ChefHat:
                    BuildChefHat(builder);
                    palette = new[] { new Color(.96f, .97f, .99f), new Color(.86f, .16f, .24f), new Color(1f, .80f, .22f) };
                    metallic = new[] { .04f, .12f, .85f };
                    smoothness = new[] { .32f, .48f, .82f };
                    emission = new[] { 0f, 0f, .35f };
                    break;
                default:
                    BuildPirateHat(builder);
                    palette = new[] { new Color(.12f, .13f, .18f), new Color(.88f, .16f, .24f), new Color(.98f, .84f, .28f) };
                    metallic = new[] { .22f, .15f, .82f };
                    smoothness = new[] { .45f, .52f, .78f };
                    emission = new[] { 0f, .25f, .45f };
                    break;
            }

            mesh = builder.ToMesh("DF_Procedural_" + id);
            materials = new Material[3];
            for (int i = 0; i < 3; i++)
            {
                var mat = new Material(shader) { name = "DF_HeadwearMat_" + id + "_" + i };
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", palette[i]);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", palette[i]);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic[i]);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness[i]);
                if (emission[i] > 0f && mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", new Color(
                        palette[i].r * emission[i],
                        palette[i].g * emission[i],
                        palette[i].b * emission[i]));
                }
                materials[i] = mat;
            }
            return true;
        }

        private sealed class Builder
        {
            public readonly System.Collections.Generic.List<Vector3> Verts = new(1024);
            public readonly System.Collections.Generic.List<int>[] Tris =
            {
                new(1024), new(512), new(512)
            };

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int sub)
            {
                int i = Verts.Count;
                Verts.Add(a); Verts.Add(b); Verts.Add(c); Verts.Add(d);
                var t = Tris[sub];
                t.Add(i); t.Add(i + 1); t.Add(i + 2);
                t.Add(i); t.Add(i + 2); t.Add(i + 3);
            }

            public void Box(Vector3 center, Vector3 half, int sub)
            {
                Vector3 p000 = center + new Vector3(-half.x, -half.y, -half.z);
                Vector3 p001 = center + new Vector3(-half.x, -half.y,  half.z);
                Vector3 p010 = center + new Vector3(-half.x,  half.y, -half.z);
                Vector3 p011 = center + new Vector3(-half.x,  half.y,  half.z);
                Vector3 p100 = center + new Vector3( half.x, -half.y, -half.z);
                Vector3 p101 = center + new Vector3( half.x, -half.y,  half.z);
                Vector3 p110 = center + new Vector3( half.x,  half.y, -half.z);
                Vector3 p111 = center + new Vector3( half.x,  half.y,  half.z);

                Quad(p001, p101, p111, p011, sub);
                Quad(p100, p000, p010, p110, sub);
                Quad(p000, p001, p011, p010, sub);
                Quad(p101, p100, p110, p111, sub);
                Quad(p011, p111, p110, p010, sub);
                Quad(p000, p100, p101, p001, sub);
            }

            public void Frustum(Vector3 bottomCenter, Vector3 topCenter, float r0, float r1, float zScale, int sides, int sub, bool capTop = true, bool capBottom = false)
            {
                float step = Mathf.PI * 2f / sides;
                for (int i = 0; i < sides; i++)
                {
                    float a0 = i * step, a1 = (i + 1) * step;
                    Vector3 b0 = bottomCenter + new Vector3(Mathf.Sin(a0) * r0, 0f, Mathf.Cos(a0) * r0 * zScale);
                    Vector3 b1 = bottomCenter + new Vector3(Mathf.Sin(a1) * r0, 0f, Mathf.Cos(a1) * r0 * zScale);
                    Vector3 t0 = topCenter + new Vector3(Mathf.Sin(a0) * r1, 0f, Mathf.Cos(a0) * r1 * zScale);
                    Vector3 t1 = topCenter + new Vector3(Mathf.Sin(a1) * r1, 0f, Mathf.Cos(a1) * r1 * zScale);
                    Quad(b0, b1, t1, t0, sub);
                    if (capTop && r1 > .001f) Quad(topCenter, t0, t1, topCenter, sub);
                    if (capBottom && r0 > .001f) Quad(bottomCenter, b1, b0, bottomCenter, sub);
                }
            }

            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name, subMeshCount = 3 };
                m.SetVertices(Verts);
                for (int i = 0; i < 3; i++) m.SetTriangles(Tris[i], i);
                m.RecalculateNormals();
                m.RecalculateBounds();
                return m;
            }
        }

        private static void BuildTvHead(Builder b)
        {
            b.Box(new Vector3(0f, .05f, 0f), new Vector3(1.18f, .96f, .98f), 0);
            b.Box(new Vector3(0f, .06f, -1.14f), new Vector3(.76f, .64f, .20f), 0);
            b.Box(new Vector3(0f, .92f, 1.00f), new Vector3(1.16f, .09f, .06f), 1);
            b.Box(new Vector3(0f, -.82f, 1.00f), new Vector3(1.16f, .09f, .06f), 1);
            b.Box(new Vector3(-1.08f, .05f, 1.00f), new Vector3(.09f, .88f, .06f), 1);
            b.Box(new Vector3(1.04f, .05f, 1.00f), new Vector3(.13f, .88f, .06f), 1);
            b.Box(new Vector3(1.04f, .42f, 1.08f), new Vector3(.07f, .07f, .05f), 1);
            b.Box(new Vector3(1.04f, .16f, 1.08f), new Vector3(.07f, .07f, .05f), 1);
            b.Frustum(new Vector3(0f, 1.01f, 0f), new Vector3(0f, 1.16f, 0f), .28f, .16f, 1f, 12, 1);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int seg = 0; seg < 5; seg++)
                {
                    float t0 = seg / 5f, t1 = (seg + 1) / 5f;
                    Vector3 p0 = new Vector3(side * (.08f + t0 * .68f), 1.14f + t0 * .82f, 0f);
                    Vector3 p1 = new Vector3(side * (.08f + t1 * .68f), 1.14f + t1 * .82f, 0f);
                    b.Frustum(p0, p1, .035f, .025f, 1f, 8, 1);
                }
                b.Box(new Vector3(side * .78f, 1.98f, 0f), new Vector3(.07f, .07f, .07f), 2);
            }
            b.Box(new Vector3(-.42f, .28f, 1.01f), new Vector3(.18f, .20f, .04f), 2);
            b.Box(new Vector3( .34f, .28f, 1.01f), new Vector3(.18f, .20f, .04f), 2);
            b.Box(new Vector3(-.04f, -.34f, 1.01f), new Vector3(.48f, .09f, .04f), 2);
            b.Box(new Vector3(-.56f, -.22f, 1.01f), new Vector3(.10f, .10f, .04f), 2);
            b.Box(new Vector3( .48f, -.22f, 1.01f), new Vector3(.10f, .10f, .04f), 2);
        }

        private static void BuildSharkHead(Builder b)
        {
            b.Frustum(new Vector3(0f, -.92f, -.10f), new Vector3(0f, -.15f, -.05f), 1.08f, 1.24f, 1.12f, 16, 0, false, true);
            b.Frustum(new Vector3(0f, -.15f, -.05f), new Vector3(0f,  .62f,  .15f), 1.24f, 1.06f, 1.18f, 16, 0, false, false);
            b.Frustum(new Vector3(0f,  .62f,  .15f), new Vector3(0f, 1.12f,  .32f), 1.06f,  .42f, 1.15f, 16, 0, true, false);
            for (int seg = 0; seg < 5; seg++)
            {
                float t0 = seg / 5f, t1 = (seg + 1) / 5f;
                b.Box(new Vector3(0f, 1.05f + t0 * .85f, -.12f - t0 * .32f),
                      new Vector3(.08f * (1f - t0 * .45f), .11f, .28f * (1f - t1 * .55f)), 0);
            }
            b.Box(new Vector3(0f, -.68f, .52f), new Vector3(.84f, .24f, .64f), 1);
            for (int t = -3; t <= 3; t++)
            {
                float tx = t * .21f;
                b.Frustum(new Vector3(tx, .38f, 1.12f), new Vector3(tx, .14f, 1.15f), .075f, .01f, 1f, 6, 1);
                b.Frustum(new Vector3(tx, -.44f, 1.08f), new Vector3(tx, -.22f, 1.12f), .07f, .01f, 1f, 6, 1);
            }
            b.Box(new Vector3(-.96f, .36f, .58f), new Vector3(.08f, .11f, .16f), 2);
            b.Box(new Vector3( .96f, .36f, .58f), new Vector3(.08f, .11f, .16f), 2);
        }

        private static void BuildCyberPumpkin(Builder b)
        {
            const int rings = 10, sides = 20;
            for (int r = 0; r < rings; r++)
            {
                float v0 = -Mathf.PI * .46f + (r / (float)rings) * Mathf.PI * .92f;
                float v1 = -Mathf.PI * .46f + ((r + 1) / (float)rings) * Mathf.PI * .92f;
                float y0 = Mathf.Sin(v0) * 1.04f, y1 = Mathf.Sin(v1) * 1.04f;
                float c0 = Mathf.Cos(v0) * 1.22f, c1 = Mathf.Cos(v1) * 1.22f;
                for (int s = 0; s < sides; s++)
                {
                    float a0 = s * Mathf.PI * 2f / sides, a1 = (s + 1) * Mathf.PI * 2f / sides;
                    float rib0 = 1f + .075f * Mathf.Cos(a0 * 10f);
                    float rib1 = 1f + .075f * Mathf.Cos(a1 * 10f);
                    Vector3 p00 = new Vector3(Mathf.Sin(a0) * c0 * rib0, y0, Mathf.Cos(a0) * c0 * rib0);
                    Vector3 p10 = new Vector3(Mathf.Sin(a1) * c0 * rib1, y0, Mathf.Cos(a1) * c0 * rib1);
                    Vector3 p11 = new Vector3(Mathf.Sin(a1) * c1 * rib1, y1, Mathf.Cos(a1) * c1 * rib1);
                    Vector3 p01 = new Vector3(Mathf.Sin(a0) * c1 * rib0, y1, Mathf.Cos(a0) * c1 * rib0);
                    b.Quad(p00, p10, p11, p01, 0);
                }
            }
            for (int i = 0; i < 4; i++)
            {
                float t0 = i / 4f, t1 = (i + 1) / 4f;
                Vector3 c0 = new Vector3(t0 * t0 * .22f, .96f + t0 * .68f, -t0 * .14f);
                Vector3 c1 = new Vector3(t1 * t1 * .22f, .96f + t1 * .68f, -t1 * .14f);
                b.Frustum(c0, c1, .22f * (1f - t0 * .55f), .22f * (1f - t1 * .55f), 1f, 8, 1);
            }
            b.Box(new Vector3(-.40f, .24f, 1.18f), new Vector3(.20f, .18f, .06f), 2);
            b.Box(new Vector3( .40f, .24f, 1.18f), new Vector3(.20f, .18f, .06f), 2);
            b.Box(new Vector3( 0f,   .02f, 1.22f), new Vector3(.11f, .11f, .06f), 2);
            for (int g = -3; g <= 3; g++)
            {
                float gy = (g & 1) == 0 ? -.34f : -.24f;
                b.Box(new Vector3(g * .16f, gy, 1.16f), new Vector3(.10f, .11f, .06f), 2);
            }
        }

        private static void BuildCardboardRobot(Builder b)
        {
            b.Box(new Vector3(0f, .04f, 0f), new Vector3(1.12f, .98f, 1.04f), 0);
            b.Box(new Vector3(-1.22f, .08f, 0f), new Vector3(.12f, .28f, .28f), 0);
            b.Box(new Vector3( 1.22f, .08f, 0f), new Vector3(.12f, .28f, .28f), 0);
            b.Box(new Vector3(-1.36f, .08f, 0f), new Vector3(.06f, .12f, .12f), 1);
            b.Box(new Vector3( 1.36f, .08f, 0f), new Vector3(.06f, .12f, .12f), 1);
            b.Frustum(new Vector3(0f, 1.02f, 0f), new Vector3(0f, 1.56f, 0f), .06f, .04f, 1f, 8, 1);
            b.Frustum(new Vector3(0f, 1.52f, 0f), new Vector3(0f, 1.72f, 0f), .12f, .44f, 1f, 12, 1);
            b.Box(new Vector3(0f, 1.82f, 0f), new Vector3(.10f, .10f, .10f), 2);
            b.Box(new Vector3(0f, 1.03f, 0f), new Vector3(.18f, .02f, 1.05f), 1);
            b.Box(new Vector3(-.42f, .28f, 1.05f), new Vector3(.24f, .12f, .03f), 2);
            b.Box(new Vector3( .42f, .28f, 1.05f), new Vector3(.24f, .12f, .03f), 2);
            for (int m = -2; m <= 2; m++)
                b.Box(new Vector3(m * .20f, -.36f, 1.05f), new Vector3(.06f, .16f, .03f), 2);
        }

        private static void BuildGoldenCrown(Builder b)
        {
            b.Frustum(new Vector3(0f, 0f, 0f), new Vector3(0f, .52f, 0f), 1.02f, 1.14f, .96f, 16, 0, false, false);
            b.Frustum(new Vector3(0f, .05f, 0f), new Vector3(0f, .58f, 0f), .96f, .78f, .92f, 16, 1, false, false);
            b.Frustum(new Vector3(0f, .58f, 0f), new Vector3(0f, .86f, 0f), .78f, .24f, .92f, 16, 1, true, false);
            const int spikes = 8;
            for (int i = 0; i < spikes; i++)
            {
                float a = i * Mathf.PI * 2f / spikes;
                Vector3 basePos = new Vector3(Mathf.Sin(a) * 1.08f, .48f, Mathf.Cos(a) * 1.08f * .96f);
                Vector3 tipPos  = new Vector3(Mathf.Sin(a) * 1.16f, 1.18f, Mathf.Cos(a) * 1.16f * .96f);
                b.Frustum(basePos, tipPos, .18f, .03f, 1f, 6, 0);
                b.Box(tipPos + new Vector3(0f, .06f, 0f), new Vector3(.07f, .07f, .07f), 2);
                Vector3 gemPos = new Vector3(Mathf.Sin(a) * 1.09f, .26f, Mathf.Cos(a) * 1.09f * .96f);
                b.Box(gemPos, new Vector3(.09f, .11f, .09f), 2);
            }
        }

        private static void BuildVikingHelm(Builder b)
        {
            b.Frustum(new Vector3(0f, 0f, 0f), new Vector3(0f, .46f, 0f), 1.05f, 1.02f, .98f, 18, 0, false, false);
            b.Frustum(new Vector3(0f, .46f, 0f), new Vector3(0f, .88f, 0f), 1.02f, .72f, .98f, 18, 0, false, false);
            b.Frustum(new Vector3(0f, .88f, 0f), new Vector3(0f, 1.14f, 0f), .72f, .18f, .98f, 18, 0, true, false);
            b.Frustum(new Vector3(0f, .02f, 0f), new Vector3(0f, .22f, 0f), 1.08f, 1.08f, 1.0f, 18, 2, false, false);
            b.Box(new Vector3(0f, .64f, 0f), new Vector3(.10f, .54f, 1.04f), 2);
            b.Box(new Vector3(0f, -.18f, 1.06f), new Vector3(.11f, .32f, .06f), 0);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 prev = new Vector3(side * 1.02f, .38f, 0f);
                float rPrev = .26f;
                for (int seg = 1; seg <= 6; seg++)
                {
                    float t = seg / 6f;
                    Vector3 next = new Vector3(
                        side * (1.02f + Mathf.Sin(t * Mathf.PI * .65f) * 1.08f),
                        .38f + t * t * 1.28f,
                        Mathf.Sin(t * Mathf.PI * .5f) * .32f);
                    float rNext = .26f * (1f - t * .88f);
                    b.Frustum(prev, next, rPrev, rNext, 1f, 10, seg == 1 ? 2 : 1);
                    prev = next;
                    rPrev = rNext;
                }
            }
        }

        private static void BuildChefHat(Builder b)
        {
            b.Frustum(new Vector3(0f, 0f, 0f), new Vector3(0f, .32f, 0f), 1.02f, 1.04f, .96f, 20, 0, false, false);
            b.Frustum(new Vector3(0f, .32f, 0f), new Vector3(0f, 1.08f, 0f), 1.04f, 1.24f, .96f, 20, 0, false, false);
            b.Frustum(new Vector3(0f, .04f, 0f), new Vector3(0f, .26f, 0f), 1.05f, 1.06f, .97f, 20, 1, false, false);
            b.Box(new Vector3(0f, .16f, 1.04f), new Vector3(.18f, .14f, .05f), 2);
            b.Frustum(new Vector3(0f, 1.04f, 0f), new Vector3(0f, 1.48f, 0f), 1.42f, 1.28f, .98f, 20, 0, false, true);
            b.Frustum(new Vector3(0f, 1.48f, 0f), new Vector3(0f, 1.78f, 0f), 1.28f, .45f, .98f, 20, 0, true, false);
        }

        private static void BuildPirateHat(Builder b)
        {
            b.Frustum(new Vector3(0f, 0f, 0f), new Vector3(0f, .64f, 0f), 1.02f, .92f, .96f, 18, 0, true, false);
            b.Frustum(new Vector3(0f, .04f, 0f), new Vector3(0f, .26f, 0f), 1.05f, 1.04f, .97f, 18, 1, false, false);
            const int sides = 24;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                float tri0 = 1.48f + .38f * Mathf.Cos(a0 * 3f);
                float tri1 = 1.48f + .38f * Mathf.Cos(a1 * 3f);
                float lift0 = .42f + .26f * Mathf.Sin(a0 * 3f + .8f);
                float lift1 = .42f + .26f * Mathf.Sin(a1 * 3f + .8f);
                Vector3 in0 = new Vector3(Mathf.Sin(a0) * .98f, .18f, Mathf.Cos(a0) * .96f);
                Vector3 in1 = new Vector3(Mathf.Sin(a1) * .98f, .18f, Mathf.Cos(a1) * .96f);
                Vector3 out0 = new Vector3(Mathf.Sin(a0) * tri0, lift0, Mathf.Cos(a0) * tri0);
                Vector3 out1 = new Vector3(Mathf.Sin(a1) * tri1, lift1, Mathf.Cos(a1) * tri1);
                Vector3 top0 = out0 + new Vector3(0f, .28f, 0f);
                Vector3 top1 = out1 + new Vector3(0f, .28f, 0f);
                b.Quad(in0, in1, out1, out0, 0);
                b.Quad(out0, out1, top1, top0, 0);
                b.Quad(top0, top1, top1 + new Vector3(0f, .06f, 0f), top0 + new Vector3(0f, .06f, 0f), 2);
            }
            b.Box(new Vector3(0f, .46f, 1.18f), new Vector3(.22f, .22f, .06f), 2);
            for (int f = 0; f < 5; f++)
            {
                float t = f / 5f;
                b.Box(new Vector3(-.92f - t * .28f, .52f + t * .86f, -.10f - t * .42f),
                      new Vector3(.08f, .18f, .18f), 1);
            }
        }
    }

}
