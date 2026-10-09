using System;
using System.Collections.Generic;
using UnityEngine;
using Alterverse.Characters;
using Alterverse.Visuals;

partial class RuntimeRegression
{
    // This exercises final native FK/contact IK, rather than accepting a small target
    // error as proof that the source pose remains natural. Scene physics is substituted.
    static void NativeContinuity()
    {
        var failures = new List<string>();
        int totalFrames = 0;
        foreach (HeroId hero in Enum.GetValues(typeof(HeroId)))
        foreach (string scenario in new[] { "forwardDiagonalJitter", "backDiagonalJitter", "continuousTurn", "sharpTurn", "startStopFade" })
        {
            Rig rig = Make(hero);
            var bones = new Transform[6];
            for (int i = 0; i < 6; i++) bones[i] = rig.bones[LocomotionAnimationLibrary.Bones[i]];
            var ground = rig.actor.AddComponent<GroundedContactHarness>();
            ground.Setup(bones, rig.bones["Hips"], rig.model, solePoints);
            // Use the boot's actual weighted rest vertices, transformed into the
            // corresponding native foot space. No assumed ankle height/flat shoe.
            var footLocalSoles = new Vector3[2][];
            for (int side = 0; side < 2; side++)
            {
                footLocalSoles[side] = new Vector3[solePoints[side].Length];
                for (int vertex = 0; vertex < solePoints[side].Length; vertex++)
                    footLocalSoles[side][vertex] = bones[side * 3 + 2].InverseTransformPoint(rig.model.TransformPoint(solePoints[side][vertex]));
            }
            Vector3 steering = DiagonalDirection(45f);
            Vector3[] previousFeet = new Vector3[2];
            Vector3[] previousKnees = new Vector3[2];
            Quaternion[] previousLegRotations = new Quaternion[6];
            Vector3 previousBody = Vector3.zero;
            bool[] previousContact = new bool[2];
            float speed = scenario == "startStopFade" ? 0f : scenario == "forwardDiagonalJitter" ? 1.8f : 6f;
            float weight = scenario == "startStopFade" ? 0f : 1f;
            float maxHeightAboveHip = float.NegativeInfinity, maxFootJump = 0f, maxBodyJump = 0f;
            float minimumSole = float.PositiveInfinity, maxError = 0f, minimumStride = float.PositiveInfinity, maximumStride = 0f;
            int contactChanges = 0, maximumJumpFrame = -1, maximumJumpSide = -1;
            bool maximumJumpContactBefore = false, maximumJumpContactAfter = false;
            float maximumKneeJump = 0f, maximumLegRotationJump = 0f, maximumFootRotationJump = 0f;
            int maximumKneeJumpFrame = -1, maximumKneeJumpSide = -1, maximumRotationFrame = -1, maximumRotationBone = -1;
            bool badHeight = false, badFootJump = false, badBodyJump = false, badGround = false, badKneeJump = false, badFootRotation = false;
            const int frameCount = 600, startupFrames = 60;
            const float dt = 1f / 60f, physicalGround = -.02f;
            for (int frame = 0; frame < frameCount; frame++)
            {
                Time.deltaTime = dt; Time.timeScale = 1f; Time.time += dt;
                ground.RebasePelvis();
                float angle = scenario == "backDiagonalJitter" ? 135f : 45f;
                if (scenario == "forwardDiagonalJitter" || scenario == "backDiagonalJitter")
                    angle += (frame % 2 == 0 ? .02f : -.02f);
                else if (scenario == "continuousTurn") angle = frame * 360f / frameCount;
                else if (scenario == "sharpTurn") angle = frame < 240 ? 0f : frame < 400 ? 180f : 90f;
                Vector3 requested = DiagonalDirection(angle);
                if (scenario == "sharpTurn")
                    steering = Vector3.Lerp(steering, requested, 1f - Mathf.Exp(-18f * dt));
                else steering = requested;
                // Match the gameplay/presentation acceleration and stopping rates.
                // Retain steering at zero speed: the real GaitDirection is retained,
                // while the substitute property derives it from PresentedVelocity.
                if (scenario == "startStopFade")
                {
                    float desiredSpeed = frame >= 90 && frame < 300 ? 6f : 0f;
                    speed = Mathf.MoveTowards(speed, desiredSpeed, (desiredSpeed > speed ? 28f : 36f) * dt);
                    float desiredWeight = speed > .10f ? Mathf.Clamp01((speed - .10f) / 1f) : 0f;
                    weight = Mathf.Lerp(weight, desiredWeight, 1f - Mathf.Exp(-(speed < .10f ? 20f : 11f) * dt));
                }
                SetDirection(rig, steering.sqrMagnitude > .00001f ? steering.normalized : requested);
                rig.motion.PlanarSpeed = speed;
                rig.motion.LocomotionWeight = weight;
                rig.motion.RunBlend = Mathf.Clamp01((speed - 1.8f) / 2.7f) * weight;
                Vector3 worldTravel = rig.motion.PresentedVelocity.normalized * speed * dt;
                rig.actor.transform.position += worldTravel;
                rig.motion.PresentedPosition = rig.actor.transform.position;
                float stride = rig.animation.CycleStride(rig.motion.RunBlend);
                minimumStride = Math.Min(minimumStride, stride); maximumStride = Math.Max(maximumStride, stride);
                Check(stride > .1f && !float.IsNaN(stride) && !float.IsInfinity(stride), hero + "/" + scenario + " invalid native stride");
                rig.motion.GaitPhase = Mathf.Repeat(rig.motion.GaitPhase + worldTravel.magnitude / stride * Mathf.PI * 2f, Mathf.PI * 2f);
                // Sample increments frameCount and restores rest leg rotations. The
                // harness's final call performs the per-frame source pelvis preparation.
                Sample(rig); ground.FinalizeFeet(); totalFrames++;
                Vector3 bodyRelative = rig.bones["Hips"].position - rig.actor.transform.position;
                if (frame > startupFrames) maxBodyJump = Math.Max(maxBodyJump, Vector3.Distance(bodyRelative, previousBody));
                previousBody = bodyRelative;
                for (int side = 0; side < 2; side++)
                {
                    Transform hip = bones[side * 3], knee = bones[side * 3 + 1], foot = bones[side * 3 + 2];
                    float kneeJump = frame > startupFrames ? Vector3.Distance(knee.position, previousKnees[side]) : 0f;
                    if (kneeJump > maximumKneeJump)
                    { maximumKneeJump = kneeJump; maximumKneeJumpFrame = frame; maximumKneeJumpSide = side; }
                    previousKnees[side] = knee.position;
                    if (kneeJump > rig.legLength * .30f) badKneeJump = true;
                    for (int joint = side * 3; joint < side * 3 + 3; joint++)
                    {
                        float angleJump = frame > startupFrames ? Quaternion.Angle(previousLegRotations[joint], bones[joint].rotation) : 0f;
                        if (angleJump > maximumLegRotationJump)
                        { maximumLegRotationJump = angleJump; maximumRotationFrame = frame; maximumRotationBone = joint; }
                        previousLegRotations[joint] = bones[joint].rotation;
                        if (joint % 3 == 2)
                        {
                            maximumFootRotationJump = Math.Max(maximumFootRotationJump, angleJump);
                            if (angleJump > 18f) badFootRotation = true;
                        }
                    }
                    Vector3 current = foot.position;
                    Check(Finite(current) && Finite(ground.Target(side == 1)), hero + "/" + scenario + " non-finite native pose");
                    float heightAboveHip = current.y - hip.position.y;
                    maxHeightAboveHip = Math.Max(maxHeightAboveHip, heightAboveHip);
                    float footJump = frame > startupFrames ? Vector3.Distance(current, previousFeet[side]) : 0f;
                    if (footJump > maxFootJump)
                    {
                        maxFootJump = footJump; maximumJumpFrame = frame; maximumJumpSide = side;
                        maximumJumpContactBefore = previousContact[side]; maximumJumpContactAfter = ground.Contact(side == 1);
                    }
                    maxError = Math.Max(maxError, ground.Error(side == 1));
                    bool contact = ground.Contact(side == 1);
                    if (frame > startupFrames && contact != previousContact[side]) contactChanges++;
                    previousContact[side] = contact; previousFeet[side] = current;
                    float sole = float.PositiveInfinity;
                    foreach (Vector3 point in footLocalSoles[side]) sole = Math.Min(sole, foot.TransformPoint(point).y - physicalGround);
                    if (frame > startupFrames) minimumSole = Math.Min(minimumSole, sole);
                    if (frame > startupFrames && heightAboveHip > rig.legLength * .10f) badHeight = true;
                    if (footJump > rig.legLength * .25f) badFootJump = true;
                    if (frame > startupFrames && sole < -.035f) badGround = true;
                }
                if (maxBodyJump > rig.legLength * .25f) badBodyJump = true;
            }
            string label = hero + "/" + scenario;
            string metrics = " footAboveHip=" + maxHeightAboveHip.ToString("F4") + "m footFrameJump=" + maxFootJump.ToString("F4") +
                "m kneeFrameJump=" + maximumKneeJump.ToString("F4") + "m legRotationFrameJump=" + maximumLegRotationJump.ToString("F2") +
                "deg footRotationFrameJump=" + maximumFootRotationJump.ToString("F2") + "deg bodyFrameJump=" + maxBodyJump.ToString("F4") + "m minimumSoleClearance=" + minimumSole.ToString("F4") +
                "m maxTargetError=" + maxError.ToString("F4") + "m stride=" + minimumStride.ToString("F3") + ".." + maximumStride.ToString("F3") + "m contactChanges=" + contactChanges + " peakJumpFrame=" + maximumJumpFrame + " side=" + maximumJumpSide +
                " contact=" + maximumJumpContactBefore + "->" + maximumJumpContactAfter +
                " peakKneeJumpFrame=" + maximumKneeJumpFrame + " side=" + maximumKneeJumpSide +
                " peakRotationFrame=" + maximumRotationFrame + " bone=" + (maximumRotationBone >= 0 ? LocomotionAnimationLibrary.Bones[maximumRotationBone] : "none");
            Console.WriteLine("Native continuity " + label + metrics);
            if (badHeight) failures.Add(label + " raised foot above hip + .10 leg" + metrics);
            if (badKneeJump) failures.Add(label + " knee moved over .30 leg in a 60 FPS frame after startup" + metrics);
            if (badFootRotation) failures.Add(label + " boot rotated over 18 degrees in a 60 FPS frame after startup" + metrics);
            if (badFootJump) failures.Add(label + " foot moved over .25 leg in a 60 FPS frame after startup" + metrics);
            if (badBodyJump) failures.Add(label + " body moved over .25 leg in a frame" + metrics);
            if (badGround) failures.Add(label + " actual boot sole penetrated ground more than 35mm" + metrics);
            if (scenario == "startStopFade")
                Check(ground.Contact(false) && ground.Contact(true), label + " did not settle both feet after weight faded");
        }
        Console.WriteLine("Native continuity evaluated " + totalFrames + " frames over eight identities/five scenarios; violations=" + failures.Count + ".");
        Check(failures.Count == 0, "Native continuity failures:\n" + string.Join("\n", failures.ToArray()));
    }
    static Vector3 DiagonalDirection(float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
    }
}
