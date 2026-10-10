using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>Articulates generated lane infantry and the modular siege/guard mechs.</summary>
    public sealed class LaneUnitVisualMotion : MonoBehaviour
    {
        private Transform visualRoot, body;
        private Transform legL, legR, shinL, shinR, footL, footR, armL, armR, shoulderL, shoulderR, head;
        private Quaternion legRestL, legRestR, shinRestL, shinRestR, footRestL, footRestR;
        private Quaternion armRestL, armRestR, shoulderRestL, shoulderRestR, headRest;
        private Vector3 footPositionL, footPositionR, legPositionL, legPositionR;
        private Vector3 lastPosition, previousVelocity;
        private float phase, gait, lean, attackUntil, attackDuration, attackWeight;
        private bool configured, robot;
        private Transform attackElbowL,attackElbowR,attackHandL,attackHandR;
        private float attackContact=.18f,attackStarted=-100;private int attackSerial;
        public Transform AttackSocket=>(attackSerial&1)==0?attackHandL:attackHandR;
        public Transform RightAttackSocket=>attackHandR;
        private MechMachineGunVisual machineGun;private bool weaponBound;

        public void ConfigureInfantry(Color armor, Color accent) => Configure(false, armor, accent);
        public void ConfigureRobot() => Configure(true, Color.gray, Color.white);

        private void Configure(bool isRobot, Color armor, Color accent)
        {
            if (configured) return;
            configured = true;
            robot = isRobot;
            visualRoot = new GameObject("DF_LocomotionPose").transform;
            visualRoot.SetParent(transform, false);

            Renderer sourceRenderer = GetComponent<Renderer>();
            if (!robot && sourceRenderer != null)
            {
                MeshFilter sourceFilter = GetComponent<MeshFilter>();
                body = new GameObject("DF_LocomotionBody").transform;
                body.SetParent(visualRoot, false);
                if (sourceFilter != null)
                    body.gameObject.AddComponent<MeshFilter>().sharedMesh = sourceFilter.sharedMesh;
                MeshRenderer bodyRenderer = body.gameObject.AddComponent<MeshRenderer>();
                bodyRenderer.sharedMaterials = sourceRenderer.sharedMaterials;
                bodyRenderer.shadowCastingMode = sourceRenderer.shadowCastingMode;
                sourceRenderer.enabled = false;
            }

            // Keep the gameplay collider at the actor root and move only its visible parts.
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child != visualRoot) child.SetParent(visualRoot, true);
            }

            if (robot)
                BindRobotParts();
            else
                BuildInfantryParts(armor, accent);
            BuildAttackChain(ref armL,-1f,out attackElbowL,out attackHandL);
            BuildAttackChain(ref armR,1f,out attackElbowR,out attackHandR);
            CaptureRestPose();
            lastPosition = transform.position;
        }

        private void BindRobotParts()
        {
            if (FindDeep("DF_NeptuneDiverLegL") != null)
            {
                legL = FindDeep("DF_NeptuneDiverLegL"); legR = FindDeep("DF_NeptuneDiverLegR");
                shinL = FindDeep("DF_NeptuneDiverKneeL"); shinR = FindDeep("DF_NeptuneDiverKneeR");
                footL = FindDeep("DF_NeptuneDiverFootL"); footR = FindDeep("DF_NeptuneDiverFootR");
                armL = FindDeep("DF_NeptuneDiverArmL"); armR = FindDeep("DF_NeptuneDiverArmR");
                shoulderL = FindDeep("DF_NeptuneDiverForearmL"); shoulderR = FindDeep("DF_NeptuneDiverForearmR");
                head = FindDeep("DF_NeptuneDiverHead");
                return;
            }
            if (FindDeep("DF_GreenMechLegL") != null)
            {
                legL = FindDeep("DF_GreenMechLegL"); legR = FindDeep("DF_GreenMechLegR");
                footL = FindDeep("DF_GreenMechFootL"); footR = FindDeep("DF_GreenMechFootR");
                armL = FindDeep("DF_GreenMechArmL"); armR = FindDeep("DF_GreenMechArmR");
                head = FindDeep("DF_GreenMechHeadPivot");
                return;
            }
            if (FindDeep("DF_MarsGolemLegL") != null)
            {
                legL = FindDeep("DF_MarsGolemLegL"); legR = FindDeep("DF_MarsGolemLegR");
                footL = FindDeep("DF_MarsGolemFootL"); footR = FindDeep("DF_MarsGolemFootR");
                armL = FindDeep("DF_MarsGolemArmL"); armR = FindDeep("DF_MarsGolemArmR");
                head = FindDeep("DF_MarsGolemHead");
                return;
            }
            BuildGuardPivots(-1f, out legL, out shinL, out footL, out armL, out shoulderL);
            BuildGuardPivots(1f, out legR, out shinR, out footR, out armR, out shoulderR);
            head = Group("DF_GuardHeadMotion", visualRoot, new Vector3(0f,1.49f,.05f),
                "DF_GuardSensorHeadCore", "DF_GuardSensorHeadArmor", "DF_GuardVisor", "DF_GuardBrow",
                "DF_GuardSensorEyeL", "DF_GuardSensorEyeR");
        }

        private Transform FindDeep(string name)
        {
            foreach (Transform part in visualRoot.GetComponentsInChildren<Transform>(true))
                if (part.name == name) return part;
            return null;
        }

        private Transform Group(string name, Transform parent, Vector3 position, params string[] partNames)
        {
            Transform pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.position = visualRoot.TransformPoint(position);
            for (int i = 0; i < partNames.Length; i++)
            {
                Transform part = FindDeep(partNames[i]);
                if (part != null && part != pivot) part.SetParent(pivot, true);
            }
            return pivot;
        }

        private void BuildGuardPivots(float side, out Transform leg, out Transform shin, out Transform foot,
            out Transform arm, out Transform forearm)
        {
            string suffix = side < 0f ? "L" : "R";
            leg = Group("DF_GuardHipMotion" + suffix, visualRoot, new Vector3(side*.44f,-.06f,0),
                "DF_GuardThighCore" + suffix, "DF_GuardThighArmor" + suffix);
            shin = Group("DF_GuardKneeMotion" + suffix, leg, new Vector3(side*.50f,-.69f,.10f),
                "DF_GuardKnee" + suffix, "DF_GuardKneePlate" + suffix,
                "DF_GuardShinCore" + suffix, "DF_GuardShinArmor" + suffix, "DF_GuardShinSide" + suffix);
            foot = Group("DF_GuardAnkleMotion" + suffix, shin, new Vector3(side*.53f,-1.34f,.17f),
                "DF_GuardAnkle" + suffix, "DF_GuardFootCore" + suffix,
                "DF_GuardFootArmor" + suffix, "DF_GuardHeel" + suffix);
            // The broad shoulder plates are fixed to the torso. Only the joint and upper
            // arm enter the IK chain; moving the plates with it makes them orbit like a rotor.
            arm = Group("DF_GuardArmMotion" + suffix, visualRoot, new Vector3(side*1.02f,.96f,0),
                "DF_GuardShoulderCore" + suffix, "DF_GuardUpperArm" + suffix);
            forearm = Group("DF_GuardElbowMotion" + suffix, arm, new Vector3(side*1.25f,.20f,.08f),
                "DF_GuardElbow" + suffix, "DF_GuardForearmCore" + suffix,
                "DF_GuardForearmArmor" + suffix, "DF_GuardForearmPanel" + suffix,
                "DF_GuardArmStatus" + suffix, "DF_GuardWrist" + suffix,
                "DF_GuardHand" + suffix, "DF_GuardFinger" + suffix + "_-1",
                "DF_GuardFinger" + suffix + "_0", "DF_GuardFinger" + suffix + "_1");
        }

        private void BuildInfantryParts(Color armor, Color accent)
        {
            Material armorMaterial = FindMaterial("DF_BackPlate", armor);
            Material accentMaterial = FindMaterial("DF_Visor", accent);
            legL = CreatePart("DF_LocomotionLegL", PrimitiveType.Cube, new Vector3(-.19f,-.67f,.02f), new Vector3(.20f,.62f,.22f), armorMaterial, armor);
            legR = CreatePart("DF_LocomotionLegR", PrimitiveType.Cube, new Vector3(.19f,-.67f,.02f), new Vector3(.20f,.62f,.22f), armorMaterial, armor);
            footL = CreatePart("DF_LocomotionBootL", PrimitiveType.Cube, new Vector3(-.19f,-.95f,.09f), new Vector3(.22f,.14f,.34f), accentMaterial, accent);
            footR = CreatePart("DF_LocomotionBootR", PrimitiveType.Cube, new Vector3(.19f,-.95f,.09f), new Vector3(.22f,.14f,.34f), accentMaterial, accent);
            armL = CreatePart("DF_LocomotionArmL", PrimitiveType.Cube, new Vector3(-.32f,.01f,.03f), new Vector3(.16f,.35f,.19f), armorMaterial, armor);
            armR = CreatePart("DF_LocomotionArmR", PrimitiveType.Cube, new Vector3(.32f,.01f,.03f), new Vector3(.16f,.35f,.19f), armorMaterial, armor);
            shoulderL = visualRoot.Find("DF_Shoulder");
            head = visualRoot.Find("DF_Visor");
        }

        private Material FindMaterial(string partName, Color fallback)
        {
            Transform part = visualRoot.Find(partName);
            Renderer renderer = part != null ? part.GetComponent<Renderer>() : null;
            if (renderer != null) return renderer.sharedMaterial;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            return RuntimeMaterialPalette.For(gameObject).Fallback(shader,fallback);
        }

        private Transform CreatePart(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material, Color color)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(visualRoot, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            Collider collider = part.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (material != null) renderer.sharedMaterial = material;
                else RuntimeMaterialPalette.Tint(gameObject,renderer,color);
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            return part.transform;
        }

        private void CaptureRestPose()
        {
            if (footL != null) footPositionL = footL.localPosition;
            if (footR != null) footPositionR = footR.localPosition;
            if (legL != null) legPositionL = legL.localPosition;
            if (legR != null) legPositionR = legR.localPosition;
            legRestL = Rest(legL); legRestR = Rest(legR);
            shinRestL = Rest(shinL); shinRestR = Rest(shinR);
            footRestL = Rest(footL); footRestR = Rest(footR);
            armRestL = Rest(armL); armRestR = Rest(armR);
            shoulderRestL = Rest(shoulderL); shoulderRestR = Rest(shoulderR); headRest = Rest(head);
        }

        private void BuildAttackChain(ref Transform arm,float side,out Transform elbow,out Transform hand)
        {
            elbow=hand=null;if(arm==null)return;
            if(robot)
            {
                string suffix=side<0?"L":"R";
                elbow=side<0?shoulderL:shoulderR;
                if(arm.name.StartsWith("DF_MarsGolem"))
                {elbow=FindDeep("DF_MarsGolemForearm"+suffix);hand=FindDeep("DF_MarsGolemFist"+suffix);return;}
                if(arm.name.StartsWith("DF_GreenMech"))
                {
                    elbow=new GameObject("DF_GreenMechElbow"+suffix).transform;elbow.SetParent(arm,false);elbow.localPosition=new Vector3(0,-.55f,.08f);
                    Transform armor=FindDeep("ForearmArmor"+suffix);hand=FindDeep("DF_GreenMechClaw"+suffix);
                    if(armor!=null)armor.SetParent(elbow,true);if(hand!=null)hand.SetParent(elbow,true);return;
                }
                if(elbow==null||!elbow.IsChildOf(arm))return;
                hand=new GameObject("DF_AttackPalm").transform;hand.SetParent(elbow,false);
                hand.localPosition=arm.name.StartsWith("DF_Neptune")?new Vector3(0,-.4f,.02f):new Vector3(0,-.52f,.09f);return;
            }
            Transform mesh=arm;
            Transform upper=new GameObject("DF_InfantryUpperArm").transform;
            upper.SetParent(visualRoot,false);upper.localPosition=mesh.localPosition+Vector3.up*.15f;
            mesh.SetParent(upper,true);mesh.localPosition=new Vector3(0,-.085f,0);mesh.localScale=new Vector3(.16f,.17f,.19f);
            elbow=new GameObject("DF_InfantryElbow").transform;elbow.SetParent(upper,false);elbow.localPosition=new Vector3(0,-.17f,0);
            GameObject lower=GameObject.CreatePrimitive(PrimitiveType.Cube);Destroy(lower.GetComponent<Collider>());
            lower.name="DF_InfantryForearm";lower.transform.SetParent(elbow,false);lower.transform.localPosition=new Vector3(0,-.085f,0);lower.transform.localScale=new Vector3(.14f,.17f,.17f);
            lower.GetComponent<Renderer>().sharedMaterial=mesh.GetComponent<Renderer>().sharedMaterial;
            hand=new GameObject("DF_InfantryPalm").transform;hand.SetParent(elbow,false);hand.localPosition=new Vector3(0,-.17f,0);arm=upper;
        }
        public void PulseAttack(float windup=.18f)
        {
            attackContact=Mathf.Max(.01f,windup);attackStarted=Time.time;attackSerial++;
            attackDuration=attackContact+.38f;attackUntil=Time.time+attackDuration;
        }
        public void Seek(float elapsed){attackStarted-=elapsed;attackUntil-=elapsed;}
        private void AttackArm(Transform arm,Transform elbow,Transform hand,float side,bool striking)
        {
            if(arm==null||elbow==null||hand==null)return;
            elbow.localRotation=Quaternion.identity;
            float reach=Vector3.Distance(arm.position,elbow.position)+Vector3.Distance(elbow.position,hand.position);
            Vector3 guard=arm.position-transform.up*reach*.55f+transform.forward*reach*.32f;
            if(machineGun!=null)
            {
                float recoil=side>0?CombatMotion.Curve(Time.time-attackStarted,attackContact,attackDuration,0,.045f,.015f):0;
                CombatMotion.Solve(arm,elbow,hand,guard-transform.forward*recoil,
                    arm.position+transform.right*side*reach*.55f-transform.up*reach*.35f);
                return;
            }
            Vector3 hit=arm.position+transform.forward*reach*.92f-transform.up*reach*.20f;
            Vector3 target=striking?CombatMotion.Path(Time.time-attackStarted,attackContact,attackDuration,guard,
                arm.position-transform.up*reach*.3f-transform.forward*reach*.12f+transform.right*side*reach*.2f,hit,hit+transform.forward*reach*.02f):guard;
            CombatMotion.Solve(arm,elbow,hand,target,arm.position+transform.right*side*reach*.55f-transform.up*reach*.35f);
        }

        private void LateUpdate()
        {
            if (!configured || visualRoot == null) return;
            if(robot&&!weaponBound){weaponBound=true;machineGun=GetComponentInChildren<MechMachineGunVisual>();}
            float dt = Mathf.Max(Time.deltaTime, .0001f);
            if (Time.timeScale <= 0f) { lastPosition = transform.position; return; }

            Vector3 position = transform.position;
            Vector3 delta = position - lastPosition;
            delta.y = 0f;
            if (delta.magnitude > Mathf.Max(2f, dt * 40f))
            {
                lastPosition = position;
                previousVelocity = Vector3.zero;
                phase = gait = lean = 0f;
                attackUntil = attackDuration = attackWeight = 0f;
                ResetPoseAfterRelocation();
                return;
            }
            lastPosition = position;
            Vector3 velocity = delta / dt;
            float maxSpeed = robot ? 5f : 4f;
            velocity = Vector3.ClampMagnitude(velocity, maxSpeed);
            float moveAmount = Mathf.InverseLerp(.08f, robot ? 1.1f : .75f, velocity.magnitude);
            gait = Mathf.Lerp(gait, moveAmount, 1f - Mathf.Exp(-16f * dt));
            float acceleration = Mathf.Clamp(Vector3.Dot(velocity - previousVelocity, transform.forward) / dt, -18f, 18f);
            previousVelocity = velocity;

            float stepLength = robot ? 1.55f : 1.05f;
            // Use 60% of the former visual step cadence at the same travel speed.
            phase = Mathf.Repeat(phase + delta.magnitude / stepLength * Mathf.PI * 2f * .60f, Mathf.PI * 2f);
            float stride = Mathf.Sin(phase) * (robot ? 34f : 42f) * gait;
            if (robot) { Pose(legL, legRestL, stride); Pose(legR, legRestR, -stride); }
            else
            {
                StepInfantryFoot(footL, footPositionL, footRestL, phase, gait);
                StepInfantryFoot(footR, footPositionR, footRestR, phase + Mathf.PI, gait);
                AimInfantryLeg(legL, legPositionL, footL, legRestL);
                AimInfantryLeg(legR, legPositionR, footR, legRestR);
            }
            Pose(shinL, shinRestL, -Mathf.Max(0f, stride) * .48f);
            Pose(shinR, shinRestR, -Mathf.Max(0f, -stride) * .48f);
            if (robot) { Pose(footL, footRestL, -stride * .45f); Pose(footR, footRestR, stride * .45f); }
            Pose(armL, armRestL, -stride * .8f); Pose(armR, armRestR, stride * .8f);

            lean = Mathf.Lerp(lean, Mathf.Clamp(acceleration * .075f + velocity.magnitude * .18f, -1.5f, 3.8f), 1f - Mathf.Exp(-11f * dt));
            float idleBreath = Mathf.Sin(Time.time * (robot ? 1.2f : 1.8f)) * (robot ? .006f : .010f) * (1f - gait);
            float bounce = (1f - Mathf.Cos(phase * 2f)) * (robot ? .036f : .045f) * gait;
            visualRoot.localPosition = Vector3.up * (idleBreath + bounce);
            float sway = Mathf.Sin(phase) * gait * (robot ? 1.5f : 2.4f);
            visualRoot.localRotation = Quaternion.Euler(lean, 0f, sway);
            Pose(shoulderL, shoulderRestL, -sway * .65f); Pose(shoulderR, shoulderRestR, -sway * .65f);
            Pose(head, headRest, -lean * .28f - sway * .2f);

            float twist=machineGun!=null?0:CombatMotion.Curve(Time.time-attackStarted,attackContact,attackDuration,-14,12,20)*((attackSerial&1)==0?-1:1);
            visualRoot.rotation=Quaternion.AngleAxis(twist,transform.up)*visualRoot.rotation;
            AttackArm(armL,attackElbowL,attackHandL,-1f,(attackSerial&1)==0);
            AttackArm(armR,attackElbowR,attackHandR,1f,(attackSerial&1)!=0);
        }

        private void ResetPoseAfterRelocation()
        {
            if (visualRoot == null) return;
            visualRoot.localPosition = Vector3.zero;
            visualRoot.localRotation = Quaternion.identity;
            if (legL != null) { legL.localPosition = legPositionL; legL.localRotation = legRestL; }
            if (legR != null) { legR.localPosition = legPositionR; legR.localRotation = legRestR; }
            if (shinL != null) shinL.localRotation = shinRestL;
            if (shinR != null) shinR.localRotation = shinRestR;
            if (footL != null) { footL.localPosition = footPositionL; footL.localRotation = footRestL; }
            if (footR != null) { footR.localPosition = footPositionR; footR.localRotation = footRestR; }
            if (armL != null) armL.localRotation = armRestL;
            if (armR != null) armR.localRotation = armRestR;
            if (shoulderL != null) shoulderL.localRotation = shoulderRestL;
            if (shoulderR != null) shoulderR.localRotation = shoulderRestR;
            if (head != null) head.localRotation = headRest;
        }

        private static Quaternion Rest(Transform value) => value != null ? value.localRotation : Quaternion.identity;
        private static void StepInfantryFoot(Transform foot, Vector3 rest, Quaternion rotation, float angle, float weight)
        {
            if (foot == null) return;
            float cycle = Mathf.Repeat(angle / (Mathf.PI * 2f), 1f);
            bool planted = cycle < .52f;
            float progress = planted ? cycle / .52f : (cycle - .52f) / .48f;
            float forward = planted ? Mathf.Lerp(.23f, -.23f, progress) : Mathf.SmoothStep(-.23f, .23f, progress);
            float arc = planted ? 0f : Mathf.Sin(progress * Mathf.PI);
            float lift = arc * arc * .29f;
            foot.localPosition = rest + new Vector3(0f, lift, forward) * weight;
            foot.localRotation = rotation * Quaternion.Euler(-19f * arc * arc * weight, 0f, 0f);
        }
        private static void AimInfantryLeg(Transform leg, Vector3 rest, Transform foot, Quaternion rotation)
        {
            if (leg == null || foot == null) return;
            Vector3 hip = new Vector3(rest.x, -.35f, rest.z);
            Vector3 ankle = foot.localPosition + Vector3.up * .07f;
            Vector3 direction = ankle - hip;
            leg.localPosition = (hip + ankle) * .5f;
            leg.localRotation = Quaternion.FromToRotation(Vector3.down, direction) * rotation;
            Vector3 scale = leg.localScale; scale.y = direction.magnitude; leg.localScale = scale;
        }
        private static void Pose(Transform part, Quaternion rest, float xAngle)
        {
            if (part != null) part.localRotation = rest * Quaternion.Euler(xAngle, 0f, 0f);
        }
    }
}

