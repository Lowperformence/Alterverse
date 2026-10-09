using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Visuals;

class RuntimeRegression
{
    static int assertions, evaluations;
    static string rigPath;
    static Vector3[][] solePoints;
    static void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }
    static float F(string value) { return float.Parse(value, CultureInfo.InvariantCulture); }
    static bool Finite(Vector3 v) { return !float.IsNaN(v.x + v.y + v.z) && !float.IsInfinity(v.x + v.y + v.z); }
    class Rig
    {
        public GameObject actor;
        public Transform model;
        public HeroVisualMotion motion;
        public HeroLocomotionAnimation animation;
        public Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        public Quaternion[] rest = new Quaternion[6];
        public Vector3[] neutralFeet = new Vector3[2];
        public float legLength;
    }
    static Rig Make(HeroId hero)
    {
        var rig = new Rig();
        rig.actor = new GameObject("Actor"); rig.actor.transform.localPosition = new Vector3(3, 0, -2);
        rig.actor.transform.localRotation = Quaternion.Euler(0, 47, 0);
        rig.actor.AddComponent<HeroIdentity>().Hero = hero;
        rig.actor.AddComponent<Health>(); rig.motion = rig.actor.AddComponent<HeroVisualMotion>();
        var visual = new GameObject("DF_VisualRoot").transform; visual.parent = rig.actor.transform;
        visual.localScale = new Vector3(1.22f, 1.32f, 1.22f);
        rig.model = new GameObject("DF_AuthoredModel").transform; rig.model.parent = visual;
        rig.model.localScale = new Vector3(1.10f, 1, 1.10f);
        foreach (string row in File.ReadAllLines(rigPath))
        {
            if (row.StartsWith("bone,") || row.Trim().Length == 0) continue;
            var columns = row.Split(','); var bone = new GameObject(columns[0]).transform;
            bone.parent = columns[1].Length == 0 ? rig.model : rig.bones[columns[1]];
            bone.localPosition = new Vector3(F(columns[2]), F(columns[3]), F(columns[4]));
            bone.localRotation = new Quaternion(F(columns[5]), F(columns[6]), F(columns[7]), F(columns[8]));
            rig.bones[columns[0]] = bone;
        }
        for (int i = 0; i < 6; i++) rig.rest[i] = rig.bones[LocomotionAnimationLibrary.Bones[i]].localRotation;
        for (int side = 0; side < 2; side++)
        {
            int start = side * 3;
            rig.neutralFeet[side] = rig.bones[LocomotionAnimationLibrary.Bones[start + 2]].position;
            rig.legLength += Vector3.Distance(rig.bones[LocomotionAnimationLibrary.Bones[start]].position, rig.bones[LocomotionAnimationLibrary.Bones[start + 1]].position) +
                Vector3.Distance(rig.bones[LocomotionAnimationLibrary.Bones[start + 1]].position, rig.bones[LocomotionAnimationLibrary.Bones[start + 2]].position);
        }
        rig.legLength *= .5f;
        rig.motion.PresentedRotation = rig.actor.transform.rotation;
        rig.animation = HeroLocomotionAnimation.For(rig.actor, rig.model);
        Check(rig.animation.Ready, "Native rest fixture did not bind");
        return rig;
    }
    static void Rebase(Rig rig)
    {
        for (int i = 0; i < 6; i++) rig.bones[LocomotionAnimationLibrary.Bones[i]].localRotation = rig.rest[i];
        rig.actor.transform.ClearWrites();
    }
    static void SetDirection(Rig rig, Vector3 direction)
    {
        rig.motion.PresentedVelocity = rig.motion.PresentedRotation * direction * 6f;
    }
    static void Sample(Rig rig)
    {
        Time.frameCount++; Rebase(rig); rig.animation.ApplyLegs(); evaluations++;
        Check(rig.actor.transform.rotationWrites + rig.actor.transform.positionWrites + rig.actor.transform.scaleWrites == 0,
            "Authored source altered gameplay root");
        Check(rig.model.rotationWrites + rig.model.positionWrites + rig.model.scaleWrites == 0,
            "Authored source altered model root");
        for (int i = 0; i < 6; i++)
            Check(rig.bones[LocomotionAnimationLibrary.Bones[i]].positionWrites == 0, "Authored source altered local joint positions");
    }
    static void Matrix()
    {
        foreach (HeroId hero in Enum.GetValues(typeof(HeroId)))
        foreach (Vector3 direction in new[] { Vector3.forward, -Vector3.forward, Vector3.right, -Vector3.right,
            (Vector3.forward + Vector3.right).normalized, (Vector3.forward - Vector3.right).normalized })
        foreach (float run in new[] { 0f, .5f, 1f })
        {
            Rig rig = Make(hero); rig.motion.RunBlend = run; SetDirection(rig, direction);
            int[] contacts = new int[2], swings = new int[2]; float[] maxLift = new float[2];
            for (int phase = 0; phase < 64; phase++)
            {
                rig.motion.GaitPhase = phase * Mathf.PI * 2 / 64; Sample(rig);
                for (int side = 0; side < 2; side++)
                {
                    Vector3 offset, knee; bool contact; Quaternion rotation;
                    Check(rig.animation.TryFootSample(side == 1, out offset, out knee, out contact, out rotation), "Fresh sample unavailable");
                    Check(Finite(offset) && Finite(knee), "Non-finite source retargeting output");
                    Check(offset.y >= -.00001f, "Source foot lift negative");
                    Vector3 horizontalTarget = rig.neutralFeet[side] + offset - rig.bones[LocomotionAnimationLibrary.Bones[side * 3]].position;
                    horizontalTarget.y = 0;
                    Check(horizontalTarget.magnitude <= rig.legLength, "Source foot target exceeds complete planar leg reach: " + horizontalTarget.magnitude / rig.legLength + " for " + hero + " direction " + direction.x + "," + direction.z);
                    Check(Quaternion.Angle(rotation, rig.bones[LocomotionAnimationLibrary.Bones[side * 3 + 2]].rotation) < .001f,
                        "Final source foot rotation differs from sampled rotation");
                    if (contact) contacts[side]++; else swings[side]++;
                    maxLift[side] = Math.Max(maxLift[side], offset.y);
                }
            }
            for (int side = 0; side < 2; side++)
            {
                Check(contacts[side] > 0 && swings[side] > 0, "Direction blend lost stance/swing on one leg");
                Check(maxLift[side] > .025f, "Direction blend has no visible foot lift");
            }
            Check(rig.animation.CycleStride(run) > .1f, "Invalid distance cycle stride");
        }
    }
    static void Continuity()
    {
        Rig rig = Make(HeroId.Nerok); rig.motion.RunBlend = .6f; rig.motion.GaitPhase = 1.234f;
        Quaternion[] previous = new Quaternion[6]; float worst = 0;
        for (int angle = 0; angle <= 360; angle++)
        {
            float radians = angle * Mathf.Deg2Rad; SetDirection(rig, new Vector3(Mathf.Sin(radians), 0, Mathf.Cos(radians)));
            Sample(rig);
            for (int bone = 0; bone < 6; bone++)
            {
                Quaternion current = rig.bones[LocomotionAnimationLibrary.Bones[bone]].rotation;
                if (angle > 0) worst = Math.Max(worst, Quaternion.Angle(previous[bone], current));
                previous[bone] = current;
            }
        }
        Check(worst < 5f, "Abrupt directional boundary rotation: " + worst);
        Console.WriteLine("Continuous 360-degree direction blend worst change: " + worst.ToString("F3") + " degrees per one-degree turn.");
        foreach (string name in new[] { "walk", "run", "left", "right", "back" })
        {
            var clip = LocomotionAnimationLibrary.Find(name);
            for (int bone = 0; bone < 6; bone++)
            {
                Check(Quaternion.Angle(clip.Rotation(bone, 0), clip.Rotation(bone, 1)) < .001f, "Rotation loop seam");
                Check(Vector3.Distance(clip.Position(bone, 0), clip.Position(bone, 1)) < .000001f, "Position loop seam");
            }
            Check(clip.Contact(0, 0) >= .5f, "Cycle phase zero is not left contact");
            Check(Math.Abs(clip.Height(0, 0) - clip.Height(0, 1)) < .000001f, "Lift loop seam");
        }
    }
    static void IntegratedContacts()
    {
        float worstError = 0, worstContact = 0, worstLift = 0;
        int contactFrames = 0, testedFrames = 0, misses = 0;
        foreach (Vector3 direction in new[] { Vector3.forward, -Vector3.forward, Vector3.right, -Vector3.right,
            (Vector3.forward + Vector3.right).normalized, (Vector3.forward - Vector3.right).normalized })
        foreach (float run in new[] { 0f, 1f })
        {
            Rig rig = Make(HeroId.Nerok); SetDirection(rig, direction); rig.motion.RunBlend = run;
            var ground = rig.actor.AddComponent<GroundedContactHarness>();
            var bones = new Transform[6]; for (int i = 0; i < 6; i++) bones[i] = rig.bones[LocomotionAnimationLibrary.Bones[i]];
            ground.Setup(bones, rig.bones["Hips"], rig.model, solePoints);
            float[] maxPose = new float[2]; float caseError = 0, caseContact = 0;
            for (int frame = 0; frame < 400; frame++)
            {
                Time.deltaTime = 1f / 60f; ground.RebasePelvis();
                rig.actor.transform.position += rig.motion.PresentedVelocity * Time.deltaTime;
                rig.motion.PresentedPosition = rig.actor.transform.position;
                rig.motion.GaitPhase = Mathf.Repeat(rig.motion.GaitPhase + 6f * Time.deltaTime / rig.animation.CycleStride(run) * Mathf.PI * 2, Mathf.PI * 2);
                Sample(rig); ground.FinalizeFeet(); testedFrames++;
                for (int side = 0; side < 2; side++)
                {
                    float error = ground.Error(side == 1); worstError = Math.Max(worstError, error); caseError = Math.Max(caseError,error);
                    if (error > .035f) misses++;
                    if (ground.Contact(side == 1)) { contactFrames++; worstContact = Math.Max(worstContact, error); caseContact = Math.Max(caseContact,error); }
                    worstLift = Math.Max(worstLift, bones[side * 3 + 2].position.y - rig.actor.transform.position.y);
                    maxPose[side] = Math.Max(maxPose[side], Quaternion.Angle(rig.rest[side * 3], bones[side * 3].localRotation));
                }
            }
            Console.WriteLine("Integrated direction="+direction.x+","+direction.z+" run="+run+" worst all/contact="+caseError.ToString("F4")+"/"+caseContact.ToString("F4")+"m");
            for (int side = 0; side < 2; side++) Check(maxPose[side] > 5f, "Integrated visible leg remained static");
            rig.motion.PlanarSpeed = 0;
            for (int frame = 0; frame < 180; frame++)
            {
                Time.frameCount++; ground.RebasePelvis(); Rebase(rig); rig.animation.ApplyLegs(); ground.FinalizeFeet();
            }
            Check(ground.Contact(false) && ground.Contact(true), "Stopped integrated feet did not regain both contacts");
        }
        Console.WriteLine("Integrated exact production FinalizeFeet/RequiredDrop/PrepareFoot/Solve + real source bank: " + testedFrames + " frames, " + contactFrames + " contact samples; worst final error=" + worstError.ToString("F4") + "m, worst contact error=" + worstContact.ToString("F4") + "m, misses >35mm=" + misses + ", max visible ankle lift=" + worstLift.ToString("F3") + "m.");
        Check(worstContact < .035f, "Integrated planted foot failed final contact reach");
        Check(worstError < .035f, "Integrated swing target failed full 3D reach");
    }
    static void DistanceCadence()
    {
        Rig rig = Make(HeroId.Nerok); rig.motion.RunBlend = 1; SetDirection(rig, Vector3.forward);
        float stride = rig.animation.CycleStride(1f), reference = -1;
        foreach (int fps in new[] { 30, 60, 120 })
        {
            var phase = new DistancePhase();
            for (int frame = 0; frame < fps * 3; frame++) phase.Advance(6f / fps, stride, false);
            if (reference < 0) reference = phase.phase;
            else Check(Math.Abs(phase.phase - reference) < .001f, "Distance cadence changed with rendering FPS");
            float held = phase.phase;
            phase.Advance(0, stride, false);
            Check(Math.Abs(phase.phase - held) < .000001f, "Stopped motion advanced source cycle");
            phase.Advance(10, stride, true);
            Check(Math.Abs(phase.phase - held) < .000001f, "Suspended grounding advanced source cycle");
        }
        Console.WriteLine("Exact production distance-phase expression: cadence stable at 30/60/120 FPS; stop and suspension hold phase.");
    }
    static void Lifecycle()
    {
        Rig rig = Make(HeroId.Nerok); SetDirection(rig, Vector3.forward); rig.motion.GaitPhase = 1.234f; Sample(rig);
        Time.frameCount++;
        Vector3 offset, knee; bool contact; Quaternion rotation;
        Check(!rig.animation.TryFootSample(false, out offset, out knee, out contact, out rotation), "Stale frame foot sample accepted");
        foreach (string reason in new[] { "zeroWeight", "stopped", "paused", "dead", "suspended", "disabled" })
        {
            rig.motion.LocomotionWeight = reason == "zeroWeight" ? 0 : 1;
            rig.motion.PlanarSpeed = reason == "stopped" ? 0 : 6;
            Time.timeScale = reason == "paused" ? 0 : 1;
            rig.actor.GetComponent<Health>().IsDead = reason == "dead";
            rig.motion.GroundingSuspended = reason == "suspended";
            rig.animation.isActiveAndEnabled = reason != "disabled";
            Sample(rig);
            for (int i = 0; i < 6; i++) Check(rig.bones[LocomotionAnimationLibrary.Bones[i]].rotationWrites == 0, reason + " changed leg pose");
            Check(!rig.animation.TryFootSample(false, out offset, out knee, out contact, out rotation), reason + " returned active source feet");
        }
        Time.timeScale = 1;
    }
    static void Main(string[] args)
    {
        rigPath = args[0]; Resources.BankPath = args[1];
        var points = new[] { new List<Vector3>(), new List<Vector3>() };
        foreach (string row in File.ReadAllLines(args[2]))
        {
            if (row.StartsWith("side,")) continue;
            var columns = row.Split(','); points[int.Parse(columns[0])].Add(new Vector3(F(columns[1]),F(columns[2]),F(columns[3])));
        }
        solePoints = new[] {points[0].ToArray(),points[1].ToArray()};
        Check(LocomotionAnimationLibrary.Ready, "Real source bank not loaded");
        Matrix(); Continuity(); DistanceCadence(); Lifecycle(); IntegratedContacts();
        Console.WriteLine("PASS unchanged LocomotionAnimationLibrary + HeroLocomotionAnimation: " + evaluations + " native Nerok fixture evaluations across eight identity profiles; " + assertions + " assertions.");
        Console.WriteLine("Substitute Unity APIs/math and fixture-based JSON field assignment; no Unity mesh, physics or Android execution.");
    }
}
