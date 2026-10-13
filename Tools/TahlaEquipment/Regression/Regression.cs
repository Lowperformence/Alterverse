using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using UnityEngine;
using Alterverse.Characters;
using Alterverse.Visuals;

static class Regression
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static float F(string value) => float.Parse(value, Inv);
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static Matrix4x4 Relative(Transform bone, Transform model)
    {
        Matrix4x4 result = Matrix4x4.identity;
        for (var current = bone; current != model; current = current.parent)
            result = Matrix4x4.TRS(current.localPosition, current.localRotation, current.localScale) * result;
        return result;
    }
    sealed class Fixture
    {
        public GameObject model;
        public SkinnedMeshRenderer body, oldStaff;
        public Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        public float scale;
    }
    static Fixture Build(string csv, float scale, bool worldTransform)
    {
        var f = new Fixture { model = new GameObject("Model"), scale = scale };
        if (worldTransform)
        {
            f.model.transform.localPosition = new Vector3(13f, 7f, -9f);
            f.model.transform.localRotation = Quaternion.Euler(17f, 63f, -8f);
            f.model.transform.localScale = new Vector3(2f, 1.7f, 2.2f);
        }
        var order = new List<Transform>();
        foreach (string line in File.ReadAllLines(csv))
        {
            if (line.StartsWith("bone,")) continue;
            string[] row = line.Split(',');
            var go = new GameObject(row[0]);
            go.transform.SetParent(row[1] == "" ? f.model.transform : f.bones[row[1]], false);
            go.transform.localPosition = new Vector3(F(row[2]), F(row[3]), F(row[4]));
            go.transform.localRotation = new Quaternion(F(row[5]), F(row[6]), F(row[7]), F(row[8]));
            f.bones.Add(row[0], go.transform);
            // A left focus previously had no skin cluster: exercise the ancestor-bind fallback.
            if (row[0] != "WeaponGripL" && row[0] != "Root") order.Add(go.transform);
        }
        var body = new GameObject("DF_Anatomy"); body.transform.SetParent(f.model.transform, false);
        body.transform.localScale = Vector3.one * scale;
        f.body = body.AddComponent<SkinnedMeshRenderer>(); f.body.bones = order.ToArray();
        f.body.rootBone = f.bones["Root"];
        var bind = new Matrix4x4[order.Count];
        for (int i = 0; i < bind.Length; i++) bind[i] = Relative(order[i], f.model.transform).inverse * Relative(body.transform, f.model.transform);
        var weights = new BoneWeight[9];
        for (int i = 0; i < weights.Length; i++) weights[i] = new BoneWeight { boneIndex0 = order.IndexOf(f.bones[i < 3 ? "Head" : "Chest"]), weight0 = 1f };
        var points = new[] { new Vector3(-.02f,1.7f,.04f),new Vector3(.02f,1.7f,.04f),new Vector3(0,1.75f,.04f),
            new Vector3(-.03f,1.2f,.08f),new Vector3(.03f,1.2f,.08f),new Vector3(0,1.3f,.08f),
            new Vector3(-.03f,1.2f,.10f),new Vector3(.03f,1.2f,.10f),new Vector3(0,1.3f,.10f) };
        for (int i = 0; i < points.Length; i++) points[i] /= scale;
        var mesh = new Mesh { name = "Original anatomy", vertices = points, boneWeights = weights, bindposes = bind, subMeshCount = 2 };
        mesh.SetTriangles(new[] { 0,1,2,3,4,5 },0); mesh.SetTriangles(new[] { 6,7,8 },1);
        mesh.RecalculateBounds(); f.body.sharedMesh = mesh; f.body.localBounds = mesh.bounds;
        f.body.sharedMaterials = new[] { new Material(new Shader()) { name = "Skin" },new Material(new Shader()) { name = "Champagne metal" } };
        var old = new GameObject("DF_WeaponR"); old.transform.SetParent(f.model.transform,false); old.transform.localScale = Vector3.one * scale;
        f.oldStaff = old.AddComponent<SkinnedMeshRenderer>(); f.oldStaff.bones = new[] { f.bones["WeaponGripR"] };
        f.oldStaff.sharedMesh = new Mesh { bindposes = new[] { Relative(f.bones["WeaponGripR"], f.model.transform).inverse * Relative(old.transform,f.model.transform) } };
        return f;
    }
    static void Main(string[] args)
    {
        byte[] bytes = File.ReadAllBytes(args[0]);
        string json;
        using (var stream = new GZipStream(new MemoryStream(bytes), CompressionMode.Decompress))
        using (var reader = new StreamReader(stream)) json = reader.ReadToEnd();
        var map = (Dictionary<string,object>)JsonUtility.Parse(json);
        Resources.Assets["TahlaEquipment/Model"] = new TextAsset { bytes = bytes };
        int cases = 0;
        foreach (float scale in new[] { 1f, .01f })
        foreach (bool world in new[] { false, true })
        {
            var f = Build(args[1], scale, world);
            TahlaEquipmentAppearance.Apply(f.model.transform, HeroId.Rook);
            Check(f.model.GetComponent<TahlaEquipmentAppearance>() == null, "other hero was changed");
            TahlaEquipmentAppearance.Apply(f.model.transform, HeroId.Mira);
            Check(f.model.GetComponent<TahlaEquipmentAppearance>() != null, "component missing");
            Check(f.model.GetComponent<TahlaEquipmentAppearance>().IsReady, "successful attachment is not ready");
            Check(!f.oldStaff.enabled && f.oldStaff.name == "DF_WeaponR_Legacy", "old staff is still active");
            var renderers = f.model.GetComponentsInChildren<SkinnedMeshRenderer>();
            Check(renderers.Length == 5, "incorrect assembly count");
            foreach (var raw in (object[])map["assemblies"])
            {
                var assembly = (Dictionary<string,object>)raw;
                var target = Array.Find(renderers, r => r.name == (string)assembly["name"]);
                Check(target != null && target.enabled, "assembly not enabled");
                var values = (object[])assembly["vertices"];
                Check(target.sharedMesh.vertexCount == values.Length / 3, "vertex count changed");
                for (int i = 0; i < values.Length / 3; i++)
                {
                    var expected = new Vector3(Convert.ToSingle(values[i*3],Inv),Convert.ToSingle(values[i*3+1],Inv),Convert.ToSingle(values[i*3+2],Inv)) / scale;
                    Check((target.sharedMesh.vertices[i] - expected).magnitude < .002f, "import-unit or world-transform drift");
                    Check(target.sharedMesh.boneWeights[i].weight0 == 1f, "non-rigid equipment weight");
                }
            }
            var muzzle = f.model.transform.Find("Root");
            foreach (var t in f.model.GetComponentsInChildren<Transform>()) if (t.name == "DF_Muzzle") muzzle = t;
            var mm = (object[])map["muzzle"];
            var expectedMuzzle = f.model.transform.TransformPoint(new Vector3(Convert.ToSingle(mm[0],Inv),Convert.ToSingle(mm[1],Inv),Convert.ToSingle(mm[2],Inv)));
            Check((muzzle.position - expectedMuzzle).magnitude < .00003f,"muzzle does not follow staff binding");
            Check(f.body.sharedMesh.GetTriangles(0).Length == 3, "head skin was removed");
            Check(f.body.sharedMesh.GetTriangles(1).Length == 0, "old chest plate was retained");
            Check(f.body.sharedMesh.GetTriangles(2).Length == 3, "torso undersuit was lost");
            TahlaEquipmentAppearance.Apply(f.model.transform,HeroId.Mira);
            Check(f.model.GetComponentsInChildren<SkinnedMeshRenderer>().Length == 5,"duplicate equipment");
            cases++;
        }
        var failed = Build(args[1], .01f, true);
        Mesh original = failed.body.sharedMesh;
        failed.bones["FootR"].name = "MissingFoot";
        bool rejected = false;
        try { TahlaEquipmentAppearance.Apply(failed.model.transform,HeroId.Mira); } catch { rejected = true; }
        Check(rejected && failed.body.sharedMesh == original && failed.oldStaff.enabled && failed.oldStaff.name == "DF_WeaponR", "failed attachment did not preserve original model");
        Check(!failed.model.GetComponent<TahlaEquipmentAppearance>().IsReady, "failed attachment suppresses the legacy fallback");
        Console.WriteLine("TAHLA_COMPONENT_REGRESSION_PASSED: "+cases+" scale/world cases, left-grip fallback, hero gate, cleanup, muzzle, idempotence and failure restoration");
    }
}
