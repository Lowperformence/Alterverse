using System.Collections.Generic;
using UnityEngine;

namespace Alterverse.Visuals
{
    // Visual-only pressure suit; LaneUnitVisualMotion owns every animated joint.
    public sealed class NeptuneDiverMechVisual : MonoBehaviour
    {
        readonly List<Material> materials = new List<Material>();
        readonly List<Mesh> meshes = new List<Mesh>();
        public static void Build(Transform actor, int team)
        {
            if (actor.GetComponent<NeptuneDiverMechVisual>() != null) return;
            var visual=actor.gameObject.AddComponent<NeptuneDiverMechVisual>();
            try{visual.BuildSuit(team);}
            catch(System.Exception error)
            {
                // Optional planet art must not discard the entire playable match.
                Transform incompleteRoot=actor.Find("DF_NeptuneDiverMechVisual");
                if(incompleteRoot!=null){incompleteRoot.gameObject.SetActive(false);Destroy(incompleteRoot.gameObject);}
                Destroy(visual);
                Debug.LogWarning("DIMENSIONS FRONT: Neptune diving suit failed; using the standard base guardian.");
                Debug.LogException(error);
                Alterverse.Combat.HealingGuardMech.BuildRobotVisual(actor,team);
            }
        }
        Material Surface(Color color, float metal = 0f, Color glow = default)
        {
            var material = RuntimeShaderLibrary.Create(color, false, metal, .65f, glow);
            materials.Add(material);
            return material;
        }
        Transform Pivot(string name, Transform parent, Vector3 position)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false); t.localPosition = position;
            return t;
        }
        Transform Part(string name, Transform parent, Vector3 position, Vector3 scale, Material material, PrimitiveType type = PrimitiveType.Sphere)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = scale;
            var collider = go.GetComponent<Collider>();
            collider.enabled = false; Destroy(collider);
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (type == PrimitiveType.Cube) go.GetComponent<MeshFilter>().sharedMesh = ProductionMeshFactory.BeveledBox();
            return go.transform;
        }
        void Ring(Transform parent, Vector3 position, float radius, float tube, Material material, bool horizontal = false)
        {
            const int rows = 32, sides = 8;
            var vertices = new Vector3[(rows + 1) * (sides + 1)];
            var triangles = new int[rows * sides * 6];
            int k = 0;
            for (int i = 0; i <= rows; i++)
                for (int j = 0; j <= sides; j++)
                {
                    float a = i * Mathf.PI * 2 / rows, b = j * Mathf.PI * 2 / sides;
                    vertices[i * (sides + 1) + j] = new Vector3(Mathf.Cos(a) * (radius + tube * Mathf.Cos(b)), Mathf.Sin(a) * (radius + tube * Mathf.Cos(b)), tube * Mathf.Sin(b));
                    if (i == rows || j == sides) continue;
                    int n = i * (sides + 1) + j;
                    triangles[k++] = n; triangles[k++] = n + sides + 1; triangles[k++] = n + 1;
                    triangles[k++] = n + 1; triangles[k++] = n + sides + 1; triangles[k++] = n + sides + 2;
                }
            var mesh = new Mesh { name = "DF_DiverPressureRing" };
            mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            meshes.Add(mesh);
            var t = Pivot("PressureSeal", parent, position);
            if (horizontal) t.localRotation = Quaternion.Euler(90, 0, 0);
            t.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            t.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
        void BuildSuit(int team)
        {
            var root = Pivot("DF_NeptuneDiverMechVisual", transform, Vector3.zero);
            var suit = Surface(new Color(.025f, .12f, .19f));
            var armor = Surface(new Color(.08f, .48f, .52f), .5f);
            var brass = Surface(new Color(.7f, .48f, .2f), .75f);
            var rubber = Surface(new Color(.025f, .04f, .06f));
            var ivory = Surface(new Color(.74f, .84f, .83f), .4f);
            var glass = Surface(new Color(.025f, .25f, .36f), .65f, new Color(0, .15f, .22f));
            Color signal = team == 0 ? new Color(.15f, .8f, 1f) : new Color(1f, .58f, .17f);
            var light = Surface(signal, .2f, signal * 1.3f);
            Part("PressureSuit", root, new Vector3(0,.48f,0), new Vector3(1.05f,1.2f,.72f), suit);
            Part("ChestArmor", root, new Vector3(0,.62f,.25f), new Vector3(.85f,.75f,.35f), armor);
            Part("HarnessBelt", root, new Vector3(0,.08f,0), new Vector3(1.02f,.2f,.76f), rubber, PrimitiveType.Cube);
            Ring(root, new Vector3(0,1.04f,0), .37f,.07f, brass,true);
            var head = Pivot("DF_NeptuneDiverHead",root,new Vector3(0,1.3f,0));
            Part("PressureHelmet",head,Vector3.zero,new Vector3(.94f,.91f,.87f),brass);
            Part("RoundGlassViewport",head,new Vector3(0,0,.39f),new Vector3(.64f,.64f,.16f),glass);
            Ring(head,new Vector3(0,0,.44f),.33f,.045f,ivory);
            Part("GlassReflection",head,new Vector3(-.14f,.16f,.48f),new Vector3(.12f,.06f,.015f),ivory);
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI/4;
                Part("ViewportBolt",head,new Vector3(Mathf.Cos(a)*.37f,Mathf.Sin(a)*.37f,.43f),Vector3.one*.05f,rubber);
            }
            Part("PressureGauge",root,new Vector3(-.2f,.67f,.44f),new Vector3(.22f,.22f,.08f),ivory);
            Part("GaugeNeedle",root,new Vector3(-.2f,.7f,.49f),new Vector3(.02f,.09f,.015f),rubber,PrimitiveType.Cube);
            Part("TeamBeacon",root,new Vector3(.22f,.69f,.44f),new Vector3(.17f,.12f,.06f),light);
            for(int side=-1;side<=1;side+=2)
            {
                string suffix=side<0?"L":"R";
                Part("AirTank",root,new Vector3(side*.27f,.6f,-.48f),new Vector3(.34f,.65f,.34f),ivory,PrimitiveType.Capsule);
                Part("TankValve",root,new Vector3(side*.27f,1.23f,-.48f),new Vector3(.12f,.12f,.12f),brass);
                // Closely spaced rounded segments make a flexible ribbed respirator hose.
                for(int i=0;i<20;i++)
                {
                    float t=i/19f;
                    Part("RespiratorHose",root,new Vector3(side*(.34f+.25f*Mathf.Sin(t*Mathf.PI)),1.02f-.48f*Mathf.Sin(t*Mathf.PI),-.43f+.84f*t),Vector3.one*.095f,rubber);
                }
                var arm=Pivot("DF_NeptuneDiverArm"+suffix,root,new Vector3(side*.64f,.83f,0));
                Part("ShoulderPlate",arm,Vector3.zero,new Vector3(.42f,.38f,.47f),ivory);
                Part("Sleeve",arm,new Vector3(0,-.2f,0),new Vector3(.29f,.27f,.29f),suit,PrimitiveType.Capsule);
                var forearm=Pivot("DF_NeptuneDiverForearm"+suffix,arm,new Vector3(0,-.38f,0));
                Part("ForearmArmor",forearm,new Vector3(0,-.13f,0),new Vector3(.32f,.26f,.34f),armor,PrimitiveType.Capsule);
                Part("DivingGlove",forearm,new Vector3(0,-.4f,.02f),new Vector3(.31f,.32f,.34f),rubber);
                Part("ShoulderLamp",arm,new Vector3(0,.07f,.24f),new Vector3(.13f,.13f,.07f),light);
                var leg=Pivot("DF_NeptuneDiverLeg"+suffix,root,new Vector3(side*.29f,-.02f,0));
                Part("SuitThigh",leg,new Vector3(0,-.17f,0),new Vector3(.37f,.24f,.39f),suit,PrimitiveType.Capsule);
                var knee=Pivot("DF_NeptuneDiverKnee"+suffix,leg,new Vector3(0,-.35f,0));
                Part("KneePlate",knee,new Vector3(0,0,.15f),new Vector3(.32f,.28f,.17f),ivory);
                Part("ShinArmor",knee,new Vector3(0,-.15f,0),new Vector3(.33f,.24f,.36f),armor,PrimitiveType.Capsule);
                var foot=Pivot("DF_NeptuneDiverFoot"+suffix,knee,new Vector3(0,-.32f,0));
                Part("WeightedDivingBoot",foot,new Vector3(0,-.05f,.12f),new Vector3(.4f,.27f,.62f),rubber,PrimitiveType.Cube);
                Part("BootToe",foot,new Vector3(0,-.02f,.33f),new Vector3(.39f,.21f,.23f),brass);
            }
        }
        void OnDestroy()
        {
            foreach(var material in materials) if(material != null) Destroy(material);
            foreach(var mesh in meshes) if(mesh != null) Destroy(mesh);
        }
    }
}

