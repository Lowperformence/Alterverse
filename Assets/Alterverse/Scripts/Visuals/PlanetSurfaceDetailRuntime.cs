using System.Collections.Generic;
using Alterverse.Match;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Builds a clearly readable crater field on top of the procedural arena surface.
    /// The geometry is presentation-only: no Collider, Rigidbody or NavMeshObstacle is ever added.
    /// Large bowls and raised irregular rims make the landscape read from the normal MOBA camera
    /// while keeping the gameplay collision plane completely unchanged.
    /// </summary>
    [DefaultExecutionOrder(260)]
    public sealed class PlanetSurfaceDetailRuntime : MonoBehaviour
    {
        private readonly struct Crater
        {
            public readonly Vector2 Center;
            public readonly float Radius;
            public readonly float Ellipse;
            public readonly float Rotation;
            public readonly float Strength;

            public Crater(float x,float z,float radius,float ellipse,float rotation,float strength)
            {
                Center=new Vector2(x,z);
                Radius=radius;
                Ellipse=ellipse;
                Rotation=rotation;
                Strength=strength;
            }
        }

        // Large landmark impacts. They deliberately avoid the cores, healing stations and exact center.
        // Their scale is intentionally much larger than the previous decorative dents.
        private static readonly Crater[] Major=
        {
            new(-22.5f, 24.0f, 7.8f, .82f,  18f, 1.00f),
            new( 23.5f,-24.5f, 7.3f, .88f, -24f,  .96f),
            new(-31.0f,-17.0f, 6.1f, .76f,  31f,  .92f),
            new( 30.0f, 18.5f, 6.4f, .84f, -14f,  .94f),
            new(-10.0f,-28.0f, 5.5f, .91f,   9f,  .82f),
            new( 11.0f, 28.0f, 5.2f, .79f, -33f,  .82f),
            new(-17.5f, 11.5f, 4.7f, .86f,  42f,  .76f),
            new( 18.0f,-10.5f, 4.5f, .80f, -39f,  .74f),
            new( -2.5f, 21.5f, 4.0f, .90f,  12f,  .68f),
            new(  3.5f,-21.0f, 4.2f, .83f, -18f,  .70f)
        };

        // Secondary impacts create the densely cratered reference-image feeling without becoming noise.
        private static readonly Crater[] Secondary=
        {
            new(-36, 28,2.2f,.84f, 12f,.62f),new(-33, 23,1.4f,.77f,-19f,.54f),
            new(-29, 30,1.1f,.92f, 31f,.48f),new(-27, 18,1.7f,.81f,-37f,.58f),
            new(-35,-26,2.0f,.78f, 27f,.59f),new(-29,-22,1.2f,.89f,-11f,.50f),
            new(-23,-30,1.6f,.75f, 41f,.55f),new(-17,-24,1.0f,.88f,-24f,.46f),
            new(-15, 20,1.4f,.80f, 14f,.52f),new(-11, 25, .9f,.91f,-32f,.44f),
            new( -8, 15,1.2f,.74f, 25f,.48f),new( -5,-16,1.4f,.86f,-18f,.50f),
            new(  7, 17,1.1f,.82f, 39f,.47f),new(  8,-16,1.5f,.76f,-29f,.52f),
            new( 14, 23,1.8f,.88f, 17f,.56f),new( 16,-27,1.1f,.79f,-41f,.48f),
            new( 21, 29,1.4f,.84f, 34f,.53f),new( 24, 18, .9f,.73f,-16f,.45f),
            new( 28,-30,1.8f,.90f, 23f,.57f),new( 31,-21,1.2f,.78f,-36f,.50f),
            new( 35, 27,2.0f,.82f, 11f,.60f),new( 36, 13,1.3f,.75f,-28f,.51f),
            new( 37,-27,1.6f,.88f, 37f,.55f),new( 34,-13,1.0f,.80f,-14f,.46f),
            new(-20,  3,1.1f,.89f, 21f,.45f),new(-14, -5, .9f,.78f,-31f,.42f),
            new( 13,  6,1.0f,.83f, 28f,.43f),new( 21, -3,1.2f,.76f,-22f,.46f)
        };

        private ProductionLunarGround currentGround;
        private GameObject detailRoot;
        private Mesh bowlMesh;
        private Mesh rimMesh;
        private Material bowlMaterial;
        private Material rimMaterial;
        private int probeFrame;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if(FindAnyObjectByType<PlanetSurfaceDetailRuntime>()!=null)return;
            GameObject host=new("DF_PlanetSurfaceDetailRuntime");
            DontDestroyOnLoad(host);
            host.hideFlags=HideFlags.DontSave;
            host.AddComponent<PlanetSurfaceDetailRuntime>();
        }

        private void Update()
        {
            if((probeFrame++%24)!=0)return;
            ProductionLunarGround ground=FindAnyObjectByType<ProductionLunarGround>();
            if(ground==currentGround&&detailRoot!=null)return;

            Clear();
            currentGround=ground;
            if(currentGround!=null)Build(currentGround.transform);
        }

        private void Build(Transform parent)
        {
            bool moon=IsMoon();
            float scale=moon?1f:.86f;
            float heightScale=moon?1f:.70f;

            var bowlVertices=new List<Vector3>(7000);
            var bowlTriangles=new List<int>(12000);
            var rimVertices=new List<Vector3>(7000);
            var rimTriangles=new List<int>(12000);

            for(int i=0;i<Major.Length;i++)
            {
                Crater c=Major[i];
                AddCrater(c.Center,c.Radius*scale,c.Ellipse,c.Rotation,c.Strength*heightScale,48,
                    bowlVertices,bowlTriangles,rimVertices,rimTriangles,i*17+3);
            }
            for(int i=0;i<Secondary.Length;i++)
            {
                Crater c=Secondary[i];
                AddCrater(c.Center,c.Radius*scale,c.Ellipse,c.Rotation,c.Strength*heightScale,22,
                    bowlVertices,bowlTriangles,rimVertices,rimTriangles,i*29+101);
            }

            bowlMesh=CreateMesh("DF_ReferenceCraterBowls",bowlVertices,bowlTriangles);
            rimMesh=CreateMesh("DF_ReferenceCraterRims",rimVertices,rimTriangles);

            Color bowlColor,rimColor;
            if(ArenaVariantRuntime.IsMars)
            {
                bowlColor=new Color(.115f,.043f,.027f,1f);
                rimColor=new Color(.43f,.19f,.10f,1f);
            }
            else if(ArenaVariantRuntime.IsGreenPlanet)
            {
                bowlColor=new Color(.045f,.073f,.044f,1f);
                rimColor=new Color(.24f,.31f,.17f,1f);
            }
            else if(ArenaVariantRuntime.IsNoxara)
            {
                bowlColor=new Color(.012f,.020f,.055f,1f);
                rimColor=new Color(.075f,.13f,.30f,1f);
            }
            else if(ArenaVariantRuntime.IsKryon)
            {
                bowlColor=new Color(.13f,.19f,.23f,1f);
                rimColor=new Color(.50f,.70f,.77f,1f);
            }
            else
            {
                bowlColor=new Color(.042f,.047f,.055f,1f);
                rimColor=new Color(.235f,.245f,.255f,1f);
            }

            bowlMaterial=RuntimeShaderLibrary.Create(bowlColor,false,.01f,.06f);
            rimMaterial=RuntimeShaderLibrary.Create(rimColor,false,.02f,.11f);
            if(bowlMaterial==null||rimMaterial==null)
            {
                Debug.LogError("DIMENSIONS FRONT: crater landscape materials could not be created.");
                Clear();
                return;
            }
            bowlMaterial.name="DF_CraterBowlMaterial";
            rimMaterial.name="DF_CraterRimMaterial";
            currentGround.ApplySurfaceMaterial(bowlMaterial,true);
            currentGround.ApplySurfaceMaterial(rimMaterial,true);

            detailRoot=new GameObject("DF_ReferenceCraterLandscape");
            detailRoot.transform.SetParent(parent,false);
            detailRoot.isStatic=true;

            CreateRenderer("DF_LargeCraterBowls",bowlMesh,bowlMaterial,detailRoot.transform);
            CreateRenderer("DF_RaisedCraterRims",rimMesh,rimMaterial,detailRoot.transform);
        }

        private static bool IsMoon()=>!ArenaVariantRuntime.IsMars&&!ArenaVariantRuntime.IsGreenPlanet&&
                                     !ArenaVariantRuntime.IsNoxara&&!ArenaVariantRuntime.IsKryon;

        private static Mesh CreateMesh(string name,List<Vector3> vertices,List<int> triangles)
        {
            Mesh mesh=new(){name=name};
            if(vertices.Count>65535)mesh.indexFormat=IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals();
            // No UV/tangent dependency: surface shader uses world-space detail.
            var colors=new Color[vertices.Count];
            bool rim=name=="DF_ReferenceCraterRims";
            int cursor=0;
            for(int crater=0;crater<Major.Length+Secondary.Length;crater++)
            {
                int segments=crater<Major.Length?48:22;
                if(!rim)colors[cursor++]=new Color(1,1,1,.18f);
                for(int ring=0;ring<3;ring++)for(int j=0;j<segments;j++)
                    colors[cursor++]=new Color(1,1,1,rim?(ring==2?1f:ring==0?.45f:.10f):.18f);
            }
            mesh.colors=colors;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void CreateRenderer(string name,Mesh mesh,Material material,Transform parent)
        {
            GameObject go=new(name);
            go.transform.SetParent(parent,false);
            go.isStatic=true;
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            MeshRenderer renderer=go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;
            renderer.receiveShadows=true;
            // No Collider / Rigidbody / NavMeshObstacle: presentation only.
        }

        private static void AddCrater(
            Vector2 center,float radius,float ellipse,float rotationDegrees,float strength,int segments,
            List<Vector3> bowlVertices,List<int> bowlTriangles,
            List<Vector3> rimVertices,List<int> rimTriangles,int seed)
        {
            float rotation=rotationDegrees*Mathf.Deg2Rad;
            float cosR=Mathf.Cos(rotation),sinR=Mathf.Sin(rotation);

            // Inner crater surface: a broad dark floor rising toward the wall. It always remains just above
            // the existing visual ground, so it cannot disappear behind the original terrain mesh.
            int centerIndex=bowlVertices.Count;
            Vector3 centerPoint=SurfacePoint(center,0f,0f,ellipse,cosR,sinR,seed)+Vector3.up*.024f;
            bowlVertices.Add(centerPoint);

            float[] bowlFractions={.43f,.72f,.90f};
            float[] bowlLift={.030f,.048f,.105f};
            int previousStart=-1;
            for(int ring=0;ring<bowlFractions.Length;ring++)
            {
                int start=bowlVertices.Count;
                for(int i=0;i<segments;i++)
                {
                    float a=i*Mathf.PI*2f/segments;
                    float wobble=1f+.055f*Mathf.Sin(a*(3+seed%3)+seed*.37f)+.025f*Mathf.Sin(a*7f+seed*.19f);
                    Vector3 p=SurfacePoint(center,radius*bowlFractions[ring]*wobble,a,ellipse,cosR,sinR,seed);
                    p.y+=bowlLift[ring]*strength;
                    bowlVertices.Add(p);
                }

                if(ring==0)
                {
                    for(int i=0;i<segments;i++)
                    {
                        int next=(i+1)%segments;
                        bowlTriangles.Add(centerIndex);
                        bowlTriangles.Add(start+next);
                        bowlTriangles.Add(start+i);
                    }
                }
                else
                {
                    ConnectRings(previousStart,start,segments,bowlTriangles);
                }
                previousStart=start;
            }

            // Raised rim: three irregular rings form a steep lip that is obvious from the gameplay camera.
            float[] rimFractions={.82f,1.00f,1.18f};
            float[] rimLift={.085f,.32f,.028f};
            int previousRim=-1;
            for(int ring=0;ring<rimFractions.Length;ring++)
            {
                int start=rimVertices.Count;
                for(int i=0;i<segments;i++)
                {
                    float a=i*Mathf.PI*2f/segments;
                    float broken=.90f+.11f*(.5f+.5f*Mathf.Sin(a*(4+seed%4)+seed*.51f));
                    float wobble=1f+.07f*Mathf.Sin(a*(3+seed%5)+seed*.21f)+.035f*Mathf.Sin(a*9f+seed*.33f);
                    Vector3 p=SurfacePoint(center,radius*rimFractions[ring]*wobble,a,ellipse,cosR,sinR,seed);
                    p.y+=rimLift[ring]*strength*broken;
                    rimVertices.Add(p);
                }
                if(ring>0)ConnectRings(previousRim,start,segments,rimTriangles);
                previousRim=start;
            }
        }

        private static Vector3 SurfacePoint(Vector2 center,float radial,float angle,float ellipse,float cosR,float sinR,int seed)
        {
            float localX=Mathf.Cos(angle)*radial;
            float localZ=Mathf.Sin(angle)*radial*ellipse;
            float x=center.x+localX*cosR-localZ*sinR;
            float z=center.y+localX*sinR+localZ*cosR;
            Vector3 p=LunarSurfaceField.Surface(x,z);
            // Tiny deterministic breakup prevents perfectly smooth manufactured rings.
            p.y+=(Mathf.PerlinNoise(x*.41f+seed,z*.41f+seed*.73f)-.5f)*.018f;
            return p;
        }

        private static void ConnectRings(int innerStart,int outerStart,int segments,List<int> triangles)
        {
            for(int i=0;i<segments;i++)
            {
                int next=(i+1)%segments;
                triangles.Add(innerStart+i);
                triangles.Add(outerStart+next);
                triangles.Add(outerStart+i);
                triangles.Add(innerStart+i);
                triangles.Add(innerStart+next);
                triangles.Add(outerStart+next);
            }
        }

        private void Clear()
        {
            if(detailRoot!=null)Destroy(detailRoot);
            if(bowlMesh!=null)Destroy(bowlMesh);
            if(rimMesh!=null)Destroy(rimMesh);
            if(bowlMaterial!=null)Destroy(bowlMaterial);
            if(rimMaterial!=null)Destroy(rimMaterial);
            detailRoot=null;
            bowlMesh=null;
            rimMesh=null;
            bowlMaterial=null;
            rimMaterial=null;
        }

        private void OnDestroy()=>Clear();
    }
}
