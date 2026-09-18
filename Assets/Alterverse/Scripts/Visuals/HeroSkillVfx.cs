using Alterverse.Characters;
using Alterverse.Combat;
using UnityEngine;
namespace Alterverse.Visuals
{
    // Thin opaque line geometry; one shared material, no transparent screens or burst particle clouds.
    public sealed class HeroSkillVfx : MonoBehaviour
    {
        private static Material ink;
        private float until;
        private GameObject owner;
        private bool cancelWithOwner;
        private void Update()
        {
            if (Time.time >= until || (cancelWithOwner && (owner == null || owner.GetComponent<Health>()?.IsDead == true))) Destroy(gameObject);
        }
        private static void Stroke(Vector3[] points, Color color, float width, float duration, bool loop, GameObject owner = null, bool cancel = false)
        {
            if (ink == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader == null) return;
                ink = new Material(shader) { name = "DF_SharedSkillInk" };
            }
            GameObject obj = new("DF_HeroSkillStroke");
            Transform world = owner != null ? owner.transform.parent : null;
            if (world != null) obj.transform.SetParent(world, true);
            LineRenderer line = obj.AddComponent<LineRenderer>();
            line.sharedMaterial = ink; line.useWorldSpace = true; line.loop = loop;
            line.startColor = line.endColor = color; line.widthMultiplier = width;
            line.positionCount = points.Length; line.SetPositions(points);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; line.receiveShadows = false;
            HeroSkillVfx lifetime = obj.AddComponent<HeroSkillVfx>();
            lifetime.until = Time.time + duration; lifetime.owner = owner; lifetime.cancelWithOwner = cancel;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetMaterial() { if (ink != null) Destroy(ink); ink = null; }
        public static void Area(GameObject actor, Vector3 center, float radius, float duration, bool warning)
        {
            HeroIdentity hero = actor.GetComponent<HeroIdentity>();
            Color color = warning ? new Color(1f,.62f,.2f) : hero != null ? StylizedArtDirection.HeroAccent(hero.Hero) : Color.white;
            center.y = .22f;
            Vector3[] ring = new Vector3[40];
            for (int i = 0; i < ring.Length; i++)
            { float a = i * Mathf.PI * 2 / ring.Length; ring[i] = center + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius; }
            Stroke(ring, color, warning ? .07f : .13f, duration, true, actor, warning);
            int corners = hero == null ? 4 : hero.Hero == HeroId.Kaelon ? 6 : hero.Hero == HeroId.Mira ? 12 : hero.Hero == HeroId.Rook ? 8 : 4;
            Vector3[] glyph = new Vector3[corners];
            for (int i = 0; i < corners; i++)
            { float a = i * Mathf.PI * 2 / corners; float r = radius * ((hero != null && hero.Hero == HeroId.Mira && i % 2 == 1) ? .38f : .7f); glyph[i] = center + new Vector3(Mathf.Cos(a), .01f, Mathf.Sin(a)) * r; }
            Stroke(glyph, hero != null ? StylizedArtDirection.HeroAccent(hero.Hero) : color, .055f, duration, true, actor, warning);
        }
        public static void Basic(GameObject actor, Vector3 impact)
        {
            HeroIdentity hero = actor.GetComponent<HeroIdentity>();
            Color color = hero != null ? StylizedArtDirection.HeroAccent(hero.Hero) : Color.white;
            actor.GetComponent<HeroVisualMotion>()?.PulseAttack(false);
            Vector3 start = actor.transform.position + Vector3.up * .5f, end = impact + Vector3.up * .5f;
            bool ranged = actor.GetComponent<HeroCombatProfile>()?.AttackRange > 4f;
            if (ranged) Stroke(new[] { start, end }, color, hero != null && hero.Hero == HeroId.Nyra ? .09f : .055f, .14f, false, actor);
            else
            {
                Vector3 side = actor.transform.right * .7f;
                Stroke(new[] { end - side, end + actor.transform.forward * .35f, end + side }, color, .12f, .16f, false, actor);
            }
        }
        public static void Link(GameObject actor, Vector3 impact, bool ultimate = false)
        {
            Basic(actor, impact); actor.GetComponent<HeroVisualMotion>()?.PulseAttack(ultimate);
            if (ultimate) Area(actor, impact, .65f, .28f, false);
        }
        public static void Dash(GameObject actor, Vector3 from, Vector3 to)
        {
            Color color = StylizedArtDirection.HeroAccent(actor.GetComponent<HeroIdentity>().Hero);
            Stroke(new[] { from + Vector3.up*.25f, to + Vector3.up*.25f }, color, .18f, .25f, false, actor);
        }
    }
}
