#if UNITY_EDITOR
using System;
using System.Reflection;
using Alterverse.Characters;
using Alterverse.Combat;
using UnityEditor;
using UnityEngine;

namespace Alterverse.EditorChecks
{
    // Run in an otherwise empty disposable Play Mode scene at y=80.
    // Tests authoritative hit geometry, not only the cone's render shader.
    public static class LiveConeHitboxRegression
    {
        private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Live cone hitbox: " + message);
        }
        private static GameObject Create(HeroId? hero, int team, Vector3 position, bool monster = false)
        {
            GameObject go = new GameObject("DF_LiveConeCheck");
            go.transform.position = position;
            go.AddComponent<Health>().Configure(1000);
            if (hero.HasValue)
            {
                go.AddComponent<HeroIdentity>().Configure(hero.Value);
                go.AddComponent<HeroCombatProfile>().Configure(hero.Value);
                go.AddComponent<HeroAbilityKit>().Configure(hero.Value);
            }
            go.AddComponent<TeamMember>().Initialize(team);
            if (monster) go.AddComponent<HotZoneTank>();
            return go;
        }
        private static float Total(GameObject go)
        {
            Health hp = go.GetComponent<Health>();
            return hp.Current + hp.Shield;
        }
        private static void Tick(HeroAttackDelivery delivery)
            => typeof(HeroAttackDelivery).GetMethod("Update", Hidden).Invoke(delivery, null);

        private static void SetPendingTime(HeroAttackDelivery delivery, string field, float time)
        {
            Array slots = (Array)typeof(HeroAttackDelivery).GetField("pending", Hidden).GetValue(delivery);
            for (int i = 0; i < slots.Length; i++)
            {
                object p = slots.GetValue(i);
                Type type = p.GetType();
                if (!(bool)type.GetField("Active").GetValue(p)) continue;
                type.GetField(field).SetValue(p, time);
                slots.SetValue(p, i);
            }
        }
        private static HeroAttackDelivery Fire(GameObject shooter)
        {
            HeroAttackDelivery delivery = HeroAttackDelivery.For(shooter);
            var execution = shooter.GetComponent<HeroAbilityExecution>() ??
                shooter.AddComponent<HeroAbilityExecution>();
            Check(delivery.AbilityAimed(execution, 2,
                new AimPose(Vector3.forward, shooter.transform.position + Vector3.forward * 5)),
                "unable to queue live cone projectile");
            SetPendingTime(delivery, "ReleaseAt", Time.time - 1f);
            Tick(delivery);
            return delivery;
        }

        [MenuItem("DIMENSIONS FRONT/Checks/Live cone hitbox (Play mode)")]
        public static void Run()
        {
            if (!Application.isPlaying || Time.timeScale <= 0f ||
                !Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay)
            {
                Debug.LogWarning("Run live cone checks in an empty disposable offline/host Play Mode scene.");
                return;
            }
            Vector3 origin = new Vector3(0, 80, 0);
            Check(AimGeometry.ConeCircle(origin, Vector3.forward,
                origin + Quaternion.Euler(0, 23f, 0) * Vector3.forward * 4f, 6f, 35f, .6f),
                "visible cone edge must overlap an enemy body");
            Check(!AimGeometry.ConeCircle(origin, Vector3.forward,
                origin + Quaternion.Euler(0, 23f, 0) * Vector3.forward * 4f, 6f, 35f, 0f),
                "centre outside the cone is not sufficient without body contact");
            Check(AimGeometry.SweptConeCircle(origin, Vector3.forward,
                origin + new Vector3(-5, 0, 4), origin + new Vector3(5, 0, 4),
                6f, 35f, .35f),
                "fast target crossing between frames is missed");

            GameObject source = null, enemy = null, fallback = null;
            try
            {
                // A Hot Zone unit stands closer than a hero, but must not steal the hit.
                source = Create(HeroId.Nyra, 0, origin);
                enemy = Create(HeroId.Kaelon, 1, origin + Vector3.forward * 4f);
                fallback = Create(null, 1, origin + Vector3.forward * 2f, monster: true);
                Check(AimGeometry.Select(source, Vector3.forward, 6f, 17.5f) ==
                    enemy.GetComponent<Health>(), "hero-first acquisition");
                Fire(source);
                Check(Total(enemy) < 1000f, "enemy hero must take priority over closer monster");
                Check(Mathf.Approximately(Total(fallback), 1000f), "monster took hero's hit");

                source.SetActive(false); enemy.SetActive(false); fallback.SetActive(false);
                UnityEngine.Object.Destroy(source);UnityEngine.Object.Destroy(enemy);UnityEngine.Object.Destroy(fallback);
                source = Create(HeroId.Nyra, 0, origin);
                enemy = Create(HeroId.Kaelon, 1, origin + new Vector3(4, 0, 4));
                HeroAttackDelivery live = Fire(source);
                Check(Mathf.Approximately(Total(enemy), 1000f), "target outside wedge hit at launch");
                Check(live.TryActiveCone(out _, out Vector3 activeDirection, out AimSpec spec) &&
                    Vector3.Angle(activeDirection, Vector3.forward) < .01f &&
                    Mathf.Approximately(spec.coneAngle, AimingSettings.Current.For(HeroId.Nyra, 2).coneAngle),
                    "live hit geometry differs from the preview");
                enemy.transform.position = origin + Vector3.forward * 4f;
                Tick(live);
                float total = Total(enemy);
                Check(total < 1000f, "moving enemy entering active wedge was not hit");
                Tick(live);
                Check(Mathf.Approximately(Total(enemy), total), "same cone attack damaged twice");

                source.SetActive(false);enemy.SetActive(false);
                UnityEngine.Object.Destroy(source);UnityEngine.Object.Destroy(enemy);
                source = Create(HeroId.Nyra, 0, origin);
                enemy = Create(HeroId.Kaelon, 1, origin + new Vector3(4, 0, 4));
                live = Fire(source);
                SetPendingTime(live, "ConeUntil", Time.time - .01f);
                enemy.transform.position = origin + Vector3.forward * 3f;
                Tick(live);
                Check(Mathf.Approximately(Total(enemy), 1000f), "expired cone dealt late damage");

                source.SetActive(false); enemy.SetActive(false);
                UnityEngine.Object.Destroy(source);UnityEngine.Object.Destroy(enemy);
                source = Create(HeroId.Nyra, 0, origin);
                fallback = Create(null, 1, origin + Vector3.forward * 2f, monster: true);
                Fire(source);
                Check(Total(fallback) < 1000f, "monster not selected when no hero is in the cone");
                Debug.Log("DIMENSIONS FRONT: live cone sweep, preview equality, hero priority, Hot Zone fallback, entry, one hit and expiry passed.");
            }
            finally
            {
                if (source != null) { source.SetActive(false); UnityEngine.Object.Destroy(source); }
                if (enemy != null) { enemy.SetActive(false); UnityEngine.Object.Destroy(enemy); }
                if (fallback != null) { fallback.SetActive(false); UnityEngine.Object.Destroy(fallback); }
            }
        }
    }
}
#endif
