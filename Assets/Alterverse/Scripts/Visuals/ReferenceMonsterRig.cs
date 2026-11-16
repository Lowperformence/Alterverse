using System.Collections.Generic;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>One visible rig for reference-built biped monsters; combat remains on HotZoneTank.</summary>
    [DefaultExecutionOrder(950)]
    public sealed class ReferenceMonsterRig : MonoBehaviour
    {
        private Transform host, hips, chest, head;
        private readonly Dictionary<string,Transform> joints = new Dictionary<string,Transform>();
        private readonly Dictionary<Transform,Quaternion> rest = new Dictionary<Transform,Quaternion>();
        private Vector3 previous, hipsRestPos;
        private float phase, moving, attackUntil, previousHeading, turnLean;
        private readonly Vector3[] plants = new Vector3[2], swingStart = new Vector3[2], swingEnd = new Vector3[2];
        private readonly bool[] swinging = new bool[2];
        private bool planted;
        public Transform Muzzle { get; private set; }
        private Transform leftMuzzle;
        public Transform AttackSocket => (attackSerial & 1) == 0 && leftMuzzle != null ? leftMuzzle : Muzzle;
        public Color EnergyColor { get; private set; }

        public static ReferenceMonsterRig Build(Transform host, string resource, Transform visualParent = null)
        {
            var data = ResourceCreatureMesh.Load(resource);
            if (data == null || data.joints == null) return null;
            var root = new GameObject("DF_ReferenceMonster_" + resource).transform;
            root.SetParent(visualParent != null ? visualParent : host, false);
            var rig = root.gameObject.AddComponent<ReferenceMonsterRig>();
            rig.host = host;
            rig.species = resource;
            rig.joints.Add("", root);
            foreach (var joint in data.joints)
            {
                if (!rig.joints.TryGetValue(joint.parent, out Transform parent) || joint.position == null || joint.position.Length != 3)
                { Destroy(root.gameObject); return null; }
                var bone = new GameObject(joint.path.Substring(joint.path.LastIndexOf('/') + 1)).transform;
                bone.SetParent(parent, false);
                bone.localPosition = new Vector3(joint.position[0], joint.position[1], joint.position[2]);
                rig.joints.Add(joint.path, bone);
                rig.rest.Add(bone, bone.localRotation);
            }
            Color[] colors = resource == "GJMagentaGolem" ? new[] { new Color(.085f,.015f,.061f),new Color(.44f,.045f,.29f),new Color(.72f,.13f,.43f),new Color(.87f,.28f,.86f),new Color(.28f,.045f,.18f) } :
                resource == "MarsSkeleton" ? new[] { new Color(.075f,.065f,.06f),new Color(.59f,.54f,.42f),new Color(.76f,.70f,.54f),new Color(.62f,.18f,.85f),new Color(.26f,.22f,.16f) } :
                resource == "GreenStalker" ? new[] { new Color(.041f,.063f,.056f),new Color(.23f,.32f,.26f),new Color(.46f,.51f,.39f),new Color(.43f,.62f,.24f),new Color(.095f,.13f,.10f) } :
                new[] { new Color(.045f,.085f,.12f),new Color(.44f,.59f,.65f),new Color(.74f,.84f,.88f),new Color(.06f,.77f,1f),new Color(.23f,.38f,.46f) };
            rig.EnergyColor = colors[3];
            if (ResourceCreatureMesh.Apply(root, resource, colors, shareGeometry: true) == null) { Destroy(root.gameObject); return null; }
            rig.joints.TryGetValue("Hips", out rig.hips);
            if (rig.hips != null) rig.hipsRestPos = rig.hips.localPosition;
            rig.chest = rig.joints["Hips/Chest"];
            rig.head = rig.joints["Hips/Chest/Head"];
            rig.Muzzle = new GameObject("DF_ReferenceMonsterMuzzle").transform;
            rig.Muzzle.SetParent(rig.joints["Hips/Chest/ArmR/ElbowR/HandR"], false);
            rig.Muzzle.localPosition = new Vector3(0, -.10f, .28f);
            rig.leftMuzzle = new GameObject("DF_ReferenceMonsterMuzzleL").transform;
            rig.leftMuzzle.SetParent(rig.joints["Hips/Chest/ArmL/ElbowL/HandL"], false);
            rig.leftMuzzle.localPosition = rig.Muzzle.localPosition;
            rig.previous = host.position;
            rig.previousHeading = host.eulerAngles.y;
            rig.PlaceOnSurface();
            return rig;
        }

        float attackStarted = -100f, contact = .22f;
        int attackSerial;
        string species;

        public void PulseAttack(float windup = .22f)
        {
            contact = Mathf.Max(.01f, windup);
            attackStarted = Time.time;
            attackUntil = Time.time + contact + .42f;
            attackSerial++;
        }

        public void Seek(float elapsed)
        {
            attackStarted -= elapsed;
            attackUntil -= elapsed;
        }

        private void PlaceOnSurface()
        {
            Vector3 p = host.position;
            p.y = LunarSurfaceField.Surface(p.x, p.z).y + .035f;
            transform.position = p;
            transform.rotation = host.rotation;
        }

        private void OnEnable()
        {
            if (host == null) return;
            previous = host.position;
            previousHeading = host.eulerAngles.y;
            phase = moving = turnLean = 0f;
            planted = false;
            attackStarted = -100f;
            attackUntil = 0f;
            attackSerial = 0;
            PlaceOnSurface();
        }

        private Vector3 Ground(Vector3 p)
        {
            p.y = LunarSurfaceField.Surface(p.x, p.z).y + .16f;
            return p;
        }

        private void LateUpdate()
        {
            if (host == null) return;
            PlaceOnSurface();
            if (Time.timeScale <= 0f) { previous = host.position; return; }
            float dt = Mathf.Max(Time.deltaTime, .001f);
            Vector3 delta = host.position - previous;
            delta.y = 0;
            previous = host.position;
            if (delta.sqrMagnitude > 16f) { planted = false; delta = Vector3.zero; }
            float speed = delta.magnitude / dt;
            moving = Mathf.MoveTowards(moving, Mathf.Clamp01(speed / .7f), dt * 5.5f);
            phase += delta.magnitude * 2.8f;

            float rawTurn = Mathf.Clamp(Mathf.DeltaAngle(previousHeading, host.eulerAngles.y) / dt, -360f, 360f);
            previousHeading = host.eulerAngles.y;
            turnLean = Mathf.Lerp(turnLean, Mathf.Clamp(-rawTurn * .018f * moving, -5.5f, 5.5f), 1f - Mathf.Exp(-10f * dt));

            foreach (var pair in rest) pair.Key.localRotation = pair.Value;

            float t = Time.time;
            float stride = Mathf.Sin(phase) * moving;
            float bob = (1f - Mathf.Cos(phase * 2f)) * .034f * moving + Mathf.Sin(t * 1.55f) * .009f * (1f - moving);
            bool stalker = species == "GreenStalker";
            bool skeleton = species == "MarsSkeleton";
            bool slasher = stalker || skeleton;

            if (hips != null)
            {
                hips.localPosition = hipsRestPos + new Vector3(stride * .025f, bob, 0f);
                hips.localRotation = rest[hips] * Quaternion.Euler(
                    (stalker ? 5f : skeleton ? 3.5f : 2f) * moving,
                    stride * 4.2f,
                    -stride * 1.8f + turnLean * .45f);
            }

            float age = t - attackStarted;
            int attackVariant = attackSerial % 3;
            bool bothArms = attackVariant == 2;
            float attackYaw = bothArms
                ? CombatMotion.Curve(age, contact, contact + .42f, -6f, 4f, 6f)
                : CombatMotion.Curve(age, contact, contact + .42f, -18f, 14f, 24f) * ((attackSerial & 1) == 0 ? -1f : 1f);
            float attackPitch = bothArms
                ? CombatMotion.Curve(age, contact, contact + .42f, -14f, 16f, 20f)
                : attackVariant == 1
                    ? CombatMotion.Curve(age, contact, contact + .42f, 8f, -10f, -14f)
                    : CombatMotion.Curve(age, contact, contact + .42f, -6f, 9f, 12f);

            float stancePitch = (stalker ? 11f : skeleton ? 7.5f : 4.5f) + Mathf.Cos(phase * 2f) * 1.8f * moving + Mathf.Sin(t * 1.5f) * 1.1f;
            chest.rotation =
                Quaternion.AngleAxis(attackYaw - stride * 5.2f, host.up) *
                Quaternion.AngleAxis(stancePitch + attackPitch, host.right) *
                Quaternion.AngleAxis(stride * 2.2f + turnLean * .65f, host.forward) *
                chest.rotation;

            head.localRotation = Quaternion.Euler(
                -(stancePitch * .35f) + 2.2f * Mathf.Sin(t * 1.35f) - attackPitch * .35f,
                stride * 2.8f + Mathf.Sin(t * .9f) * 2.5f * (1f - moving),
                -turnLean * .35f);

            for (int i = 0; i < 2; i++)
            {
                string side = i == 0 ? "L" : "R";
                float sign = i == 0 ? -1f : 1f;
                Transform arm = joints["Hips/Chest/Arm" + side];
                Transform elbow = joints["Hips/Chest/Arm" + side + "/Elbow" + side];
                Transform hand = joints["Hips/Chest/Arm" + side + "/Elbow" + side + "/Hand" + side];
                float reach = Vector3.Distance(arm.position, elbow.position) + Vector3.Distance(elbow.position, hand.position);

                // Species-authentic combat guard with natural arm swing during locomotion.
                float armSwing = (i == 0 ? stride : -stride) * reach * (slasher ? .16f : .12f);
                float guardDown = stalker ? .44f : skeleton ? .48f : .52f;
                float guardFwd = (stalker ? .38f : skeleton ? .34f : .31f) + (i == 0 ? -.03f : .04f);
                float guardOut = stalker ? .18f : skeleton ? .15f : .14f;
                Vector3 guard = arm.position
                    - host.up * (reach * guardDown - Mathf.Abs(armSwing) * .18f)
                    + host.forward * (reach * guardFwd + armSwing)
                    + host.right * (sign * reach * guardOut);

                bool striking = bothArms || i == (attackSerial % 2);
                Vector3 target = guard;
                if (striking && t < attackUntil)
                {
                    Vector3 draw, hit, through;
                    if (attackVariant == 1)
                    {
                        // Variant 1: Low-to-high diagonal uppercut / claw rip.
                        draw = arm.position + host.right * (sign * reach * .42f) - host.forward * (reach * .18f) - host.up * (reach * .46f);
                        hit = arm.position + host.forward * (reach * .88f) - host.up * (reach * .06f);
                        through = hit - host.right * (sign * reach * .32f) + host.up * (reach * .22f);
                    }
                    else if (attackVariant == 2)
                    {
                        // Variant 2: Double-arm overhead slam / twin-claw scissor crush.
                        draw = arm.position + host.right * (sign * reach * .36f) - host.forward * (reach * .12f) + host.up * (reach * .22f);
                        hit = arm.position + host.forward * (reach * .90f) - host.right * (sign * reach * .08f) - host.up * (reach * .18f);
                        through = hit - host.right * (sign * reach * .24f) - host.up * (reach * .16f);
                    }
                    else
                    {
                        // Variant 0: Wide horizontal hook / cross slash.
                        draw = arm.position + host.right * (sign * reach * (slasher ? .50f : .26f)) - host.forward * (reach * .16f) - host.up * (reach * .18f);
                        hit = arm.position + host.forward * (reach * .90f) - host.up * (reach * .18f);
                        through = hit + (slasher ? -host.right * (sign * reach * .42f) : host.forward * (reach * .05f));
                    }
                    target = CombatMotion.Path(age, contact, contact + .42f, guard, draw, hit, through);
                }
                Vector3 elbowHint = arm.position + host.right * (sign * reach * .68f) - host.up * (reach * .32f) - host.forward * (reach * .14f);
                CombatMotion.Solve(arm, elbow, hand, target, elbowHint);

                Transform hip = joints["Hips/Thigh" + side];
                Transform knee = joints["Hips/Thigh" + side + "/Shin" + side];
                Transform foot = joints["Hips/Thigh" + side + "/Shin" + side + "/Foot" + side];
                Vector3 home = Ground(foot.position);
                float cycle = Mathf.Repeat(phase / (Mathf.PI * 2f) + i * .5f, 1f);
                if (!planted) { plants[i] = home; swingStart[i] = home; swingEnd[i] = home; swinging[i] = false; }
                bool swing = moving > .05f && cycle > .58f;
                if (swing && !swinging[i]) { swingStart[i] = plants[i]; swingEnd[i] = Ground(home + host.forward * .30f); }
                Vector3 legTarget = plants[i];
                if (swing)
                {
                    float u = (cycle - .58f) / .42f;
                    legTarget = Vector3.Lerp(swingStart[i], swingEnd[i], Mathf.SmoothStep(0, 1, u))
                        + Vector3.up * (Mathf.Sin(u * Mathf.PI) * .18f);
                }
                else if (swinging[i]) { plants[i] = Ground(swingEnd[i]); legTarget = plants[i]; }
                if (moving < .03f) { plants[i] = Vector3.Lerp(plants[i], home, dt * 7f); legTarget = plants[i]; }
                swinging[i] = swing;
                SolveLeg(hip, knee, foot, legTarget, host.forward);
                foot.rotation = host.rotation;
            }
            planted = true;
        }

        private static void SolveLeg(Transform hip, Transform knee, Transform foot, Vector3 target, Vector3 forward)
        {
            Vector3 origin = hip.position;
            float upper = Vector3.Distance(origin, knee.position), lower = Vector3.Distance(knee.position, foot.position);
            Vector3 line = target - origin;
            if (line.sqrMagnitude < .00001f || upper < .001f || lower < .001f) return;
            float d = Mathf.Clamp(line.magnitude, Mathf.Abs(upper - lower) + .002f, upper + lower - .002f);
            Vector3 dir = line.normalized;
            Vector3 bend = Vector3.ProjectOnPlane(forward, dir).normalized;
            float along = (upper * upper + d * d - lower * lower) / (2 * d);
            Vector3 k = origin + dir * along + bend * Mathf.Sqrt(Mathf.Max(0, upper * upper - along * along));
            hip.rotation = Quaternion.FromToRotation(knee.position - origin, k - origin) * hip.rotation;
            knee.rotation = Quaternion.FromToRotation(foot.position - knee.position, origin + dir * d - knee.position) * knee.rotation;
        }
    }
}
