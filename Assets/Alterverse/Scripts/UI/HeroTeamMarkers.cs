using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Profile;
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
        private static readonly Color ClanWarCrimson = new(1f, .12f, .24f, .98f);
        private static readonly Color ClanWarBone = new(.98f, .95f, .88f, .99f);

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
            public float TrailNormalized;
            public float TrailHoldUntil;
            public string HpLabel;
            public string ShieldLabel;
        }

        private Texture2D pixel;
        private Camera cam;
        private GUIStyle style;
        private GUIStyle warTagStyle;
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
            nextRefresh = Time.unscaledTime + .25f;
            if (player == null)
            {
                GameObject p = LocalHeroContext.Find();
                if (p != null) player = p.GetComponent<TeamMember>();
            }
            var activeHealth = Health.Active;
            if (activeHealth.Count > 0)
            {
                int liveHeroes = 0;
                bool matchesExisting = true;
                for (int i = 0; i < activeHealth.Count; i++)
                {
                    Health hp = activeHealth[i];
                    if (hp == null) continue;
                    HeroIdentity hero = hp.GetComponent<HeroIdentity>();
                    TeamMember tm = hp.GetComponent<TeamMember>();
                    if (hero == null || tm == null) continue;
                    if (liveHeroes >= markerCount || markers[liveHeroes].Health != hp)
                        matchesExisting = false;
                    liveHeroes++;
                }
                if (matchesExisting && liveHeroes == markerCount && markerCount > 0)
                    return;

                if (markers.Length < liveHeroes)
                    markers = new CachedHeroMarker[Mathf.Max(12, liveHeroes)];
                markerCount = 0;
                for (int i = 0; i < activeHealth.Count; i++)
                {
                    Health hp = activeHealth[i];
                    if (hp == null) continue;
                    HeroIdentity hero = hp.GetComponent<HeroIdentity>();
                    TeamMember tm = hp.GetComponent<TeamMember>();
                    if (hero == null || tm == null) continue;
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
                        TrailNormalized = hp.Normalized,
                        TrailHoldUntil = 0f,
                        HpLabel = string.Empty,
                        ShieldLabel = string.Empty
                    };
                }
                return;
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
                    TrailNormalized = hp.Normalized,
                    TrailHoldUntil = 0f,
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
                string enemyClanTag = string.Empty;
                bool clanWarEnemy = enemy && ClanWarAndXpService.IsEnemyClanHero(m.Transform.gameObject, player.Team, out enemyClanTag);
                float w = (enemy ? 60f : 46f) * scale, h = 8f * scale;
                Color team = enemy ? EnemyColor : AllyColor;
                Rect frame = new Rect(p.x - w * .5f, p.y, w, h);

                GUI.color = FrameColor;
                GUI.DrawTexture(frame, pixel);

                GUI.color = clanWarEnemy
                    ? ClanWarCrimson
                    : new Color(FrontEndTheme.Accent.r, FrontEndTheme.Accent.g, FrontEndTheme.Accent.b, .7f);
                GUI.DrawTexture(new Rect(frame.x, frame.y, frame.width, 1f * scale), pixel);
                GUI.DrawTexture(new Rect(frame.x, frame.yMax - 1f * scale, frame.width, 1f * scale), pixel);

                float inset = 2f * scale;
                float innerWidth = Mathf.Max(0f, frame.width - inset * 2f);
                float innerHeight = Mathf.Max(1f, frame.height - inset * 2f);
                float norm = Mathf.Clamp01(m.Health.Normalized);

                if (norm < m.TrailNormalized - 0.005f)
                {
                    if (Time.unscaledTime >= m.TrailHoldUntil)
                        m.TrailNormalized = Mathf.MoveTowards(m.TrailNormalized, norm, Time.unscaledDeltaTime * 0.95f);
                }
                else
                {
                    m.TrailNormalized = norm;
                    m.TrailHoldUntil = Time.unscaledTime + 0.16f;
                }

                if (m.TrailNormalized > norm + 0.004f)
                {
                    GUI.color = new Color(1f, 0.86f, 0.38f, 0.92f);
                    GUI.DrawTexture(new Rect(frame.x + inset, frame.y + inset, innerWidth * Mathf.Clamp01(m.TrailNormalized), innerHeight), pixel);
                }

                bool lowHpExecute = enemy && norm <= 0.18f;
                if (lowHpExecute)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 12f);
                    GUI.color = Color.Lerp(team, new Color(1f, 0.78f, 0.22f, 1f), pulse * 0.65f);
                }
                else
                {
                    GUI.color = team;
                }
                GUI.DrawTexture(new Rect(frame.x + inset, frame.y + inset, innerWidth * norm, innerHeight), pixel);

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

                if (enemy)
                {
                    if (clanWarEnemy)
                        DrawEnemyClanSkull(p.x, p.y - 28f * scale, scale, enemyClanTag);
                    else
                        DrawEnemyCrystal(p.x, p.y - 24f * scale, scale);
                }
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

        /// <summary>
        /// Draws a crisp procedural Totenkopf (skull) emblem and clan tag above an enemy clan player.
        /// </summary>
        private void DrawEnemyClanSkull(float cx, float cy, float scale, string enemyClanTag)
        {
            Color oldColor = GUI.color;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);

            // Dark backing shield + crimson war border
            float badgeW = 20f * scale, badgeH = 20f * scale;
            GUI.color = new Color(.03f, .01f, .02f, .94f);
            GUI.DrawTexture(new Rect(cx - badgeW * .5f, cy - badgeH * .5f, badgeW, badgeH), pixel);
            GUI.color = Color.Lerp(ClanWarCrimson, new Color(1f, .55f, .20f, 1f), pulse * .35f);
            GUI.DrawTexture(new Rect(cx - badgeW * .5f, cy - badgeH * .5f, badgeW, 1.5f * scale), pixel);
            GUI.DrawTexture(new Rect(cx - badgeW * .5f, cy + badgeH * .5f - 1.5f * scale, badgeW, 1.5f * scale), pixel);

            // Skull cranium (upper block + rounded crown)
            float craniumW = 12f * scale, craniumH = 9f * scale;
            GUI.color = ClanWarBone;
            GUI.DrawTexture(new Rect(cx - craniumW * .5f, cy - 6.5f * scale, craniumW, craniumH), pixel);
            GUI.DrawTexture(new Rect(cx - craniumW * .38f, cy - 8f * scale, craniumW * .76f, 2f * scale), pixel);

            // Skull jaw
            float jawW = 8f * scale, jawH = 4.5f * scale;
            GUI.DrawTexture(new Rect(cx - jawW * .5f, cy + 2f * scale, jawW, jawH), pixel);

            // Eye sockets (glowing dark crimson-black)
            GUI.color = new Color(.08f, .01f, .02f, 1f);
            float eyeS = 3.2f * scale;
            GUI.DrawTexture(new Rect(cx - 4.2f * scale, cy - 3.8f * scale, eyeS, eyeS), pixel);
            GUI.DrawTexture(new Rect(cx + 1.0f * scale, cy - 3.8f * scale, eyeS, eyeS), pixel);

            // Red eye pupils
            GUI.color = ClanWarCrimson;
            float pupil = 1.4f * scale;
            GUI.DrawTexture(new Rect(cx - 3.3f * scale, cy - 2.9f * scale, pupil, pupil), pixel);
            GUI.DrawTexture(new Rect(cx + 1.9f * scale, cy - 2.9f * scale, pupil, pupil), pixel);

            // Nasal cavity & teeth separators
            GUI.color = new Color(.08f, .01f, .02f, 1f);
            GUI.DrawTexture(new Rect(cx - .9f * scale, cy - .2f * scale, 1.8f * scale, 2f * scale), pixel);
            GUI.DrawTexture(new Rect(cx - 2.2f * scale, cy + 3.2f * scale, 1f * scale, 2.8f * scale), pixel);
            GUI.DrawTexture(new Rect(cx - .5f * scale, cy + 3.2f * scale, 1f * scale, 2.8f * scale), pixel);
            GUI.DrawTexture(new Rect(cx + 1.2f * scale, cy + 3.2f * scale, 1f * scale, 2.8f * scale), pixel);

            if (!string.IsNullOrEmpty(enemyClanTag) && warTagStyle != null)
            {
                GUI.color = new Color(1f, .36f, .36f, .98f);
                GUI.Label(new Rect(cx - 44f * scale, cy - 22f * scale, 88f * scale, 12f * scale),
                    $"☠ [{enemyClanTag}]", warTagStyle);
            }

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
            warTagStyle = new GUIStyle(style)
            {
                fontSize = Mathf.Max(8, Mathf.RoundToInt(9 * scale)),
                normal = { textColor = new Color(1f, .38f, .38f, 1f) }
            };
        }

        private void OnDestroy()
        {
            if (pixel != null) Destroy(pixel);
        }
    }
}
