using System.Collections.Generic;
using Alterverse.Match;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Replaces the older per-cell Diamond Core floor overlay with a seamless shared-grid prism surface.
    /// This removes the long white cracks while keeping the faceted prism read from the gameplay camera.
    /// </summary>
    [DefaultExecutionOrder(1100)]
    public sealed class DiamondCoreFloorSeamFix : MonoBehaviour
    {
        GameObject root;
        Mesh mesh;
        Material material;
        float nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(GameObject.Find("DF_PSRJ1719_FloorSeamFix")!=null)return;
            GameObject go=new("DF_PSRJ1719_FloorSeamFix");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<DiamondCoreFloorSeamFix>();
        }

        void Update()
        {
            if(!ArenaVariantRuntime.IsDiamondCore)
            {
                Clear();
                return;
            }

            if(Time.unscaledTime<nextScan)return;
            nextScan=Time.unscaledTime+.20f;

            DisableOldOverlay();
            if(root==null)BuildSeamlessFloor();
        }

        static void DisableOldOverlay()
        {
            GameObject old=GameObject.Find("FacetedCrystalGroundOverlay");
            if(old==null)return;
            MeshRenderer renderer=old.GetComponent<MeshRenderer>();
            if(renderer!=null)renderer.enabled=false;
        }

        void BuildSeamlessFloor()
        {
            GameObject world=GameObject.Find("DF_MatchWorld");
            if(world==null){Clear();return;}

            // The previous match may have destroyed the root while native resources remain.
            Clear();
            root=new GameObject("DF_PSRJ1719_SeamlessPrismFloor");
            root.transform.SetParent(world.transform,false);

            mesh=BuildMesh();
            MeshFilter filter=root.AddComponent<MeshFilter>();
            filter.sharedMesh=mesh;

            Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
            if(shader==null){Clear();return;}

            material=new Material(shader){name="DF_PSRJ1719_SeamlessPrismFloorMat"};
            // Neutral silver-blue tint preserves the original floor colour. The geometry/normals,
            // not bright lines or multiple materials, provide the visible prism structure.
            DiamondCoreCrystalVisuals.ConfigureTransparentMaterial(
                material,new Color(.60f,.65f,.68f,.060f),.84f,0f,.012f);

            MeshRenderer renderer=root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;
            renderer.receiveShadows=false;
        }

        static Mesh BuildMesh()
        {
            const float minX=-42f,maxX=42f,minZ=-36f,maxZ=36f,step=7.5f;
            int xCells=Mathf.CeilToInt((maxX-minX)/step);
            int zCells=Mathf.CeilToInt((maxZ-minZ)/step);

            // Every cell reads from the same precomputed corner positions. Adjacent triangles therefore
            // meet on exactly identical coordinates and cannot open the long white cracks seen before.
            Vector3[,] points=new Vector3[xCells+1,zCells+1];
            for(int z=0;z<=zCells;z++)
            for(int x=0;x<=xCells;x++)
            {
                float px=Mathf.Min(minX+x*step,maxX);
                float pz=Mathf.Min(minZ+z*step,maxZ);

                // Small deterministic vertex variation keeps the surface irregular while remaining shared
                // by every neighbouring cell, so it never produces gaps.
                if(x>0&&x<xCells&&z>0&&z<zCells)
                {
                    float jx=HashSigned(x,z,17)*.30f;
                    float jz=HashSigned(x,z,43)*.30f;
                    px+=jx;
                    pz+=jz;
                }

                Vector3 p=LunarSurfaceField.Surface(px,pz);
                p.y+=.032f;
                points[x,z]=p;
            }

            List<Vector3> vertices=new();
            List<Vector3> normals=new();
            List<int> indices=new();

            int cell=0;
            for(int z=0;z<zCells;z++)
            for(int x=0;x<xCells;x++)
            {
                Vector3 a=points[x,z];
                Vector3 b=points[x+1,z];
                Vector3 c=points[x+1,z+1];
                Vector3 d=points[x,z+1];

                // Duplicate only the triangle vertices for flat per-face normals. Coordinates still match
                // perfectly along borders, so the prism facets remain visible without seam gaps.
                if((cell&1)==0)
                {
                    AddTriangle(vertices,normals,indices,a,b,c);
                    AddTriangle(vertices,normals,indices,a,c,d);
                }
                else
                {
                    AddTriangle(vertices,normals,indices,a,b,d);
                    AddTriangle(vertices,normals,indices,b,c,d);
                }
                cell++;
            }

            Mesh result=new(){name="DF_PSRJ1719_SeamlessPrismFloorMesh"};
            result.SetVertices(vertices);
            result.SetNormals(normals);
            result.SetTriangles(indices,0);
            result.RecalculateBounds();
            return result;
        }

        static float HashSigned(int x,int z,int salt)
        {
            int n=x*73856093^z*19349663^salt*83492791;
            n=(n<<13)^n;
            int positive=n&0x7fffffff;
            return (positive%10001)/5000f-1f;
        }

        static void AddTriangle(List<Vector3> vertices,List<Vector3> normals,List<int> indices,Vector3 a,Vector3 b,Vector3 c)
        {
            if(Vector3.Cross(b-a,c-a).y<0f)(b,c)=(c,b);
            Vector3 normal=Vector3.Cross(b-a,c-a).normalized;
            int start=vertices.Count;
            vertices.Add(a);vertices.Add(b);vertices.Add(c);
            normals.Add(normal);normals.Add(normal);normals.Add(normal);
            indices.Add(start);indices.Add(start+1);indices.Add(start+2);
        }

        void Clear()
        {
            if(root!=null)Destroy(root);
            root=null;
            if(mesh!=null)Destroy(mesh);
            mesh=null;
            if(material!=null)Destroy(material);
            material=null;
        }

        void OnDestroy(){Clear();}
    }
}
