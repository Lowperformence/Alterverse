// Exact pre-hotfix extracts from /tmp/shoulder-before-HeroGroundedLocomotion.cs
// Full source SHA256 55b37ec2d810a0941625803fc979df5426dfc3db18dd7ac9c4d91d280e373751
private sealed class Leg
        {
            public Transform hip, knee, foot;
            public Vector3 rest, plant, swingStart, target, normal = Vector3.up;
            public Quaternion restRotation;
            public Quaternion rotation, rotationBefore;
            public Vector3[] solePoints;
            public float length, sole, cycle, soleAdjustment;
            public bool contact, initialized, authoredSample, hasBendDirection, hasFootRotation;
            public int footRotationFrame = -1;
            public Vector3 bendDirection;
            public Vector3 authoredKneeHint, authoredTarget;
            public Quaternion authoredRotation;
            public float authoredLift;
        }

private static float SoleExtent(Leg leg)
        {
            if (leg.solePoints == null || leg.solePoints.Length == 0) return leg.sole;
            Matrix4x4 matrix = leg.foot.localToWorldMatrix; float low = float.PositiveInfinity;
            foreach (Vector3 point in leg.solePoints) low = Mathf.Min(low, matrix.MultiplyVector(point).y);
            return -low;
        }

private void PrepareFoot(Leg leg, float stance, bool moving)
        {
            float pitch = 0f;
            if (!leg.authoredSample && moving && leg.contact)
            {
                float u = leg.cycle / stance;
                if (u < .12f) pitch = Mathf.Lerp(-7f, 0f, CombatMotion.Ease(u / .12f));
                else if (u > .80f) pitch = Mathf.Lerp(0f, 17f, CombatMotion.Ease((u - .80f) / .20f));
            }
            else if (!leg.authoredSample && moving)
            {
                float u = Mathf.Clamp01((leg.cycle - stance) / (1f - stance));
                pitch = u < .30f ? Mathf.Lerp(17f, -10f, CombatMotion.Ease(u / .30f)) :
                    u > .80f ? Mathf.Lerp(-10f, -7f, CombatMotion.Ease((u - .80f) / .20f)) : -10f;
            }
            pitch *= motion.LocomotionWeight;
            Quaternion flat = motion.PresentedRotation * leg.restRotation;
            Quaternion desiredRotation = leg.authoredSample ?
                Quaternion.FromToRotation(Vector3.up, leg.normal) * leg.authoredRotation :
                Quaternion.AngleAxis(pitch, motion.PresentedRotation * Vector3.right) *
                    Quaternion.FromToRotation(Vector3.up, leg.normal) * flat;
            // A direction reversal can switch blend quadrants in one frame. Keep
            // the final boot orientation continuous, also while clips fade out.
            // Reach fitting calls this repeatedly; use one baseline per frame.
            if (leg.footRotationFrame != Time.frameCount)
            {
                leg.rotationBefore = leg.hasFootRotation ? leg.rotation : leg.foot.rotation;
                leg.hasFootRotation = true; leg.footRotationFrame = Time.frameCount;
            }
            leg.rotation = Quaternion.RotateTowards(leg.rotationBefore, desiredRotation,
                720f * Mathf.Min(Time.deltaTime, .1f));
            leg.foot.rotation = leg.rotation;
            // Toe-off rotates around the ankle. Raise the ankle by the actual rotated
            // sole extent so the front of the boot cannot rotate through the floor.
            if (leg.solePoints != null && leg.solePoints.Length > 0)
            {
                leg.soleAdjustment = SoleExtent(leg) - leg.sole; leg.target.y += leg.soleAdjustment;
            }
        }

private void PlanAuthored(Leg leg, Vector3 offset, Vector3 kneeHint, bool contact, Quaternion rotation, float cycle)
        {
            leg.target.y -= leg.soleAdjustment; leg.soleAdjustment = 0f;
            Vector3 planarOffset = new Vector3(offset.x, 0f, offset.z);
            Vector3 sourceGround = Ground(Nominal(leg) + planarOffset, leg, out Vector3 normal);
            // Preserve the animated native FK sole clearance above the terrain.
            // Source shoes have different ankle/sole proportions; applying
            // their clearance again would lift or squash the native leg pose.
            float extent = SoleExtent(leg);
            float lift = Mathf.Max(0f, Nominal(leg).y + offset.y - sourceGround.y - (extent - leg.sole));
            // Human target rigs have longer shins than the cartoon source rig.
            // Retargeting must limit clearance rather than carry its high kicks
            // through a full-strength foot solve on every hero.
            lift = Mathf.Min(lift, leg.length * Mathf.Lerp(.14f, .23f, motion.RunBlend));
            if (contact)
            {
                if (!leg.initialized || !leg.contact || !leg.authoredSample)
                {
                    leg.plant = sourceGround; leg.normal = normal;
                }
                leg.target = leg.plant;
            }
            else
            {
                leg.target = sourceGround + Vector3.up * lift;
                leg.normal = normal;
            }
            leg.authoredSample = true; leg.authoredKneeHint = kneeHint;
            leg.authoredRotation = rotation;
            leg.authoredLift = contact ? 0f : lift;
            leg.authoredTarget = sourceGround + Vector3.up * leg.authoredLift;
            leg.cycle = cycle; leg.contact = contact; leg.initialized = true;
            // An unreachable plant is projected toward reach below. Never replace
            // it with the opposite end of the source stride in a single frame.
        }
private void StabilizeAuthoredTarget(Leg leg, Vector3 previous, float dt)
        {
            if (!leg.authoredSample) return;
            float maxStep = Mathf.Max(leg.length * 6f, motion.PlanarSpeed * 1.7f) * dt;
            leg.target = Vector3.MoveTowards(previous, leg.target, maxStep);
            Vector3 floor = Ground(leg.target, leg, out Vector3 normal);
            float minimum = floor.y + leg.soleAdjustment;
            leg.target.y = Mathf.Max(minimum, leg.target.y);
            leg.normal = normal;
            leg.authoredLift = Mathf.Max(0f, leg.target.y - minimum);
            if (leg.contact) leg.plant = leg.target - Vector3.up * leg.soleAdjustment;
        }
