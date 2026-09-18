using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Shop;
using UnityEngine;
namespace Alterverse.Visuals
{
    public sealed class FounderHeroVisual : MonoBehaviour
    {
        private Health health;
        private bool wasDead;
        private Mesh crestMesh;
        private Material crestMaterial;
        private void Start()
        {
            if (!FounderEntitlement.Owned || GetComponent<PlayerHeroAbilities>() == null) { enabled = false; return; }
            health = GetComponent<Health>(); if (health == null) { enabled = false; return; }
            Transform root = transform.Find("DF_VisualRoot"); if (root == null) return;
            // Attached cosmetic insignia, never a floating primitive above the head.
            GameObject crest = new("DF_FounderCrest"); crest.transform.SetParent(root,false);
            crest.transform.localPosition = new Vector3(-.22f,.3f,.39f);
            crestMesh = new Mesh { name = "DF_FounderInsignia" };
            crestMesh.vertices = new[] { new Vector3(0,.13f,0), new Vector3(-.075f,0,0), new Vector3(0,-.10f,0), new Vector3(.075f,0,0) };
            crestMesh.triangles = new[] { 0,1,2,0,2,3 }; crestMesh.RecalculateNormals(); crestMesh.RecalculateBounds();
            crest.AddComponent<MeshFilter>().sharedMesh = crestMesh;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit"); if (shader == null) shader = Shader.Find("Standard");
            if (shader != null) { crestMaterial = new Material(shader) { color = new Color(.9f,.7f,.3f) }; crest.AddComponent<MeshRenderer>().sharedMaterial = crestMaterial; }
            wasDead = health.IsDead;
        }
        private void Update()
        {
            if (health == null) return;
            if (wasDead && !health.IsDead) HeroSkillVfx.Area(gameObject,transform.position,1f,.4f,false);
            wasDead = health.IsDead;
        }
        private void OnDestroy() { if (crestMesh != null) Destroy(crestMesh); if (crestMaterial != null) Destroy(crestMaterial); }
    }
}
