using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Visuals;
using UnityEngine;

namespace Alterverse.UI
{
    public sealed class HeroTeamMarkers : MonoBehaviour
    {
        private static readonly Color EnemyColor = new(1f, .16f, .13f, .98f);
        private static readonly Color AllyColor = new(.20f, .86f, 1f, .94f);
        private static readonly Color FrameColor = new(.018f, .022f, .03f, .94f);
        private static readonly Color ShieldColor = new(.30f, .68f, 1f, 1f);

        private struct CachedHeroMarker
        {
            public HeroIdentity Identity;
            public Transform Transform;
            public Health Health;
            public TeamMember Team;
            public HeroVisualMotion Motion;
            public int LastCurHp;
            public int LastMaxHp;
            public int LastShield;
            public string HpLabel;
            public string ShieldLabel;
        }

        private Texture2D pixel;
        private Camera cam;
        private GUIStyle style;
        private TeamMember player;
        private CachedHeroMarker[] markers = System.Array.Empty<CachedHeroMarker>();
        private int markerCount;
        private float nextRefresh;

        private void Awake()
        {
            pixel = new Texture2D(1, 1);
            pixel.SetPixel(0, 0, Color.white);
            pixel.Apply();
            cam = Camera.main;
            RefreshTargets();
        }

        private void Update()
        {
            if (cam == null) cam = Camera.main;
            if (Time.unscaledTime >= nextRefresh) RefreshTargets();
        }

        private void RefreshTargets()
        {
            nextRefresh = Time.unscaledTime + 1.5f;
            if (player == null || !player.gameObject.activeInHierarchy)
            {
                GameObject p = LocalHeroContext.Find();
                if (p != null) player = p.GetComponent<TeamMember>();
            }

            HeroIdentity[] found = FindObjectsByType<HeroIdentity>(FindObjectsInactive.Exclude);
            if (markers.Length < found.Length)
                markers = new CachedHeroMarker[Mathf.Max(12, found.Length)];

            markerCount = 0;
            for (int i = 0; i < found.Length; i++)
            {
                HeroIdentity hero = found[i];
                if (hero == null) continue;
                Health hp = hero.GetComponent<Health>();
                TeamMember tm = hero.GetComponent<TeamMember>();
                if (hp == null || tm == null) continue;
                markers[markerCount++] = new CachedHeroMarker
                {
                    Identity = hero,
                    Transform = hero.transform,
                    Health = hp,
                    Team = tm,
                    Motion = hero.GetComponent<HeroVisualMotion>(),
                    LastCurHp = -1,
                    LastMaxHp = -1,
                    LastShield = -1,
                    HpLabel = string.Empty,
                    ShieldLabel = string.Empty
                };
            }
        }

        private void OnGUI()
        {
            if (Event.current != null && Event.current.type != EventType.Repaint) return;
            if (cam == null || pixel == null || Time.timeScale <= 0f || player == null) return;

            float screenW = Screen.width, screenH = Screen.height;
            float scale = Mathf.Clamp(screenH / 900f, .72f, 1.2f);
            EnsureStyle(scale);
            Color old = GUI.color;

            for (int i = 0; i < markerCount; i++)
            {
                ref CachedHeroMarker m = ref markers[i];
                if (m.Identity == null || m.Health == null || m.Health.IsDead || m.Team == null) continue;
                if (CombatVisibility.HiddenFrom(player.gameObject, m.Health)) continue;

                Vector3 anchor = HeroPresentationAnchor.Position(m.Transform, m.Motion);
                Vector3 p = cam.WorldToScreenPoint(anchor + Vector3.up * 1.65f);
                if (p.z <= 0f) continue;
                p.y = screenH - p.y;
                if (p.x < -80f || p.x > screenW + 80f || p.y < -80f || p.y > screenH + 80f) continue;

                bool enemy = m.Team.Team != player.Team;
                float w = (enemy ? 60f : 46f) * scale, h = 8f * scale;
                Color team = enemy ? EnemyColor : AllyColor;
                Rect frame = new Rect(p.x - w * .5f, p.y, w, h);

                GUI.color = FrameColor;
                GUI.DrawTexture(frame, pixel);
                GUI.color = new Color(FrontEndTheme.Accent.r, FrontEndTheme.Accent.g, FrontEndTheme.Accent.b, .7f);
                GUI.DrawTexture(new Rect(frame.x, frame.y, frame.width, 1f * scale), pixel);
                GUI.DrawTexture(new Rect(frame.x, frame.yMax - 1f * scale, frame.width, 1f * scale), pixel);

                float inset = 2f * scale;
                float innerWidth = Mathf.Max(0f, frame.width - inset * 2f);
                GUI.color = team;
                GUI.DrawTexture(new Rect(frame.x + inset, frame.y + inset, innerWidth * Mathf.Clamp01(m.Health.Normalized), Mathf.Max(1f, frame.height - inset * 2f)), pixel);

                int curHp = Mathf.RoundToInt(m.Health.Current);
                int maxHp = Mathf.RoundToInt(m.Health.Max);
                if (curHp != m.LastCurHp || maxHp != m.LastMaxHp)
                {
                    m.LastCurHp = curHp;
                    m.LastMaxHp = maxHp;
                    m.HpLabel = $"{curHp} / {maxHp}";
                }

                GUI.color = Color.white;
                GUI.Label(new Rect(p.x - w * .8f, p.y - 16f * scale, w * 1.6f, 14f * scale), m.HpLabel, style);
                DrawShield(frame, ref m, scale);

                if (enemy) DrawEnemyCrystal(p.x, p.y - 24f * scale, scale);
                else
                {
                    GUI.color = AllyColor;
                    GUI.DrawTexture(new Rect(p.x - 4f * scale, p.y - 4f * scale, 8f * scale, 2f * scale), pixel);
                }
            }
            GUI.color = old;
        }

        private void DrawShield(Rect healthFrame, ref CachedHeroMarker m, float scale)
        {
            float shield = m.Health.Shield;
            if (!(shield > 0f)) return;
            float capacity = m.Health.HeroBaseShieldMax > 0f ? m.Health.HeroBaseShieldMax : Mathf.Max(1f, m.Health.Max);
            Rect frame = new Rect(healthFrame.x, healthFrame.yMax + 2f * scale, healthFrame.width, 6f * scale);
            float inset = 1f * scale, width = Mathf.Max(0f, frame.width - inset * 2f);
            GUI.color = FrameColor;
            GUI.DrawTexture(frame, pixel);
            GUI.color = new Color(.035f, .10f, .18f, .95f);
            GUI.DrawTexture(new Rect(frame.x + inset, frame.y + inset, width, frame.height - inset * 2f), pixel);
            GUI.color = ShieldColor;
            float fill = Mathf.Min(width, Mathf.Max(2f * scale, width * Mathf.Clamp01(shield / Mathf.Max(1f, capacity))));
            GUI.DrawTexture(new Rect(frame.x + inset, frame.y + inset, fill, frame.height - inset * 2f), pixel);

            int shieldInt = Mathf.CeilToInt(shield);
            if (shieldInt != m.LastShield)
            {
                m.LastShield = shieldInt;
                m.ShieldLabel = $"+{shieldInt}";
            }
            GUI.color = Color.white;
            GUI.Label(new Rect(frame.x, frame.yMax + 1f * scale, frame.width, 12f * scale), m.ShieldLabel, style);
        }

        private void DrawEnemyCrystal(float cx, float cy, float scale)
        {
            float size = 10f * scale;
            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            GUIUtility.RotateAroundPivot(45f, new Vector2(cx, cy));
            GUI.color = new Color(.025f, .015f, .02f, .96f);
            GUI.DrawTexture(new Rect(cx - size * .62f, cy - size * .62f, size * 1.24f, size * 1.24f), pixel);
            GUI.color = EnemyColor;
            GUI.DrawTexture(new Rect(cx - size * .5f, cy - size * .5f, size, size), pixel);
            float inner = size * .42f;
            GUI.color = new Color(1f, .66f, .58f, .98f);
            GUI.DrawTexture(new Rect(cx - inner * .5f, cy - inner * .5f, inner, inner), pixel);
            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
        }

        private void EnsureStyle(float scale)
        {
            int size = Mathf.RoundToInt(10 * scale);
            if (style != null && style.fontSize == size) return;
            style = new GUIStyle(GUI.skin.label)
            {
                font = FrontEndTypography.Display,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = size,
                normal = { textColor = Color.white }
            };
        }

        private void OnDestroy()
        {
            if (pixel != null) Destroy(pixel);
        }
    }
}
