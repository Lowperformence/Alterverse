using System.Collections.Generic;
using UnityEngine;

namespace Alterverse.Visuals
{
    // Lightweight world-space backdrop visible beyond the arena edge.
    // Stars are unlit and collider-free so the combat lighting remains untouched.
    public sealed class SpaceBackdrop : MonoBehaviour
    {
        private readonly List<Material> materials = new List<Material>();

        private void Start()
        {
            if(FindAnyObjectByType<CosmicArenaBackdrop>()!=null){enabled=false;return;}
            BuildStarField();
            BuildDistantPlanets();
        }

        private void BuildStarField()
        {
            Material dim = MakeUnlit(new Color(.42f,.47f,.56f));
            Material bright = MakeUnlit(new Color(.78f,.84f,.92f));
            if (dim == null || bright == null) return;

            const int count = 96;
            for (int i=0;i<count;i++)
            {
                float a = Mathf.Repeat(i * 137.50776f, 360f) * Mathf.Deg2Rad;
                float radius = 62f + (i * 29 % 38);
                float y = -8f + (i * 17 % 42) * .72f;
                float size = i % 19 == 0 ? .22f : (i % 7 == 0 ? .14f : .075f);
                GameObject star = GameObject.CreatePrimitive(PrimitiveType.Quad);
                star.name = "DF_BackdropStar";
                star.transform.SetParent(transform,false);
                star.transform.position = new Vector3(Mathf.Cos(a)*radius,y,Mathf.Sin(a)*radius);
                star.transform.localScale = Vector3.one * size;
                star.transform.LookAt(Vector3.zero);
                Collider c=star.GetComponent<Collider>();if(c!=null){c.enabled=false;UnityEngine.Object.Destroy(c);}
                Renderer r=star.GetComponent<Renderer>();r.sharedMaterial=i%7==0?bright:dim;
                r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;
            }
        }

        private void BuildDistantPlanets()
        {
            Planet(new Vector3(-82f,13f,66f),8.5f,new Color(.12f,.14f,.19f),new Color(.28f,.31f,.38f));
            Planet(new Vector3(88f,18f,-72f),6.2f,new Color(.08f,.13f,.16f),new Color(.18f,.34f,.38f));
            Planet(new Vector3(72f,7f,82f),2.6f,new Color(.16f,.13f,.18f),new Color(.34f,.28f,.38f));
        }

        private void Planet(Vector3 position,float size,Color dark,Color light)
        {
            Material surface=MakeLit(Color.Lerp(dark,light,.42f));if(surface==null)return;
            GameObject planet=GameObject.CreatePrimitive(PrimitiveType.Sphere);planet.name="DF_DistantPlanet";
            planet.transform.SetParent(transform,false);planet.transform.position=position;planet.transform.localScale=Vector3.one*size;
            Collider c=planet.GetComponent<Collider>();if(c!=null){c.enabled=false;UnityEngine.Object.Destroy(c);}
            Renderer r=planet.GetComponent<Renderer>();r.sharedMaterial=surface;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private Material MakeUnlit(Color color)
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Unlit");if(shader==null)shader=Shader.Find("Unlit/Color");if(shader==null)return null;
            Material m=new Material(shader){color=color};materials.Add(m);return m;
        }

        private Material MakeLit(Color color)
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)shader=Shader.Find("Standard");if(shader==null)return null;
            Material m=new Material(shader){color=color};if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",.12f);materials.Add(m);return m;
        }

        private void OnDestroy(){foreach(Material m in materials)if(m!=null)Destroy(m);}
    }
}
