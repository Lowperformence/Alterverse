using Alterverse.Characters;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Compact runtime copies of the approved FBX template bone proportions.
    /// They let the motion sampler build the correct Mixamo hierarchy even when the
    /// original large FBX files are not shipped with the repository.
    /// </summary>
    internal static class TemplateRigSkeletonData
    {
        private readonly struct BoneDef
        {
            public readonly string Name;
            public readonly int Parent;
            public readonly Vector3 Position;
            public readonly Vector3 Euler;

            public BoneDef(string name, int parent, Vector3 position, Vector3 euler)
            {
                Name = name; Parent = parent; Position = position; Euler = euler;
            }
        }

        private static readonly BoneDef[] Assassin =
        {
            new("mixamorig:Hips", -1, new Vector3(0.0010259f, 0.3712833f, -0.000147f), new Vector3(0f, 0f, 0.0001f)),
            new("mixamorig:Spine", 0, new Vector3(-0.0012011f, 0.030106f, -0.0023417f), new Vector3(-4.7550149f, -0.0500945f, -0.3885372f)),
            new("mixamorig:Spine1", 1, new Vector3(0f, 0.0329436f, 0f), new Vector3(1.3383491f, 0.0404826f, 0.893481f)),
            new("mixamorig:Spine2", 2, new Vector3(0f, 0.0523591f, 0f), new Vector3(3.4173022f, -0.0344767f, -0.5015531f)),
            new("mixamorig:LeftShoulder", 3, new Vector3(0.0243966f, 0.0406607f, -0.0020628f), new Vector3(90f, 84.6234292f, -28.5539369f)),
            new("mixamorig:LeftArm", 4, new Vector3(0f, 0.0309018f, 0f), new Vector3(-28.5541057f, 0.0000267f, -5.3765218f)),
            new("mixamorig:LeftForeArm", 5, new Vector3(0f, 0.07492f, 0f), new Vector3(-0.0000217f, 0f, 0.9999644f)),
            new("mixamorig:RightShoulder", 3, new Vector3(-0.0276011f, 0.0406609f, -0.004959f), new Vector3(-90f, -84.0258466f, -147.8856617f)),
            new("mixamorig:RightArm", 7, new Vector3(0f, 0.0278232f, 0f), new Vector3(-32.1143051f, -0.0000033f, -5.9741587f)),
            new("mixamorig:RightForeArm", 8, new Vector3(0f, 0.075026f, 0f), new Vector3(-0.0000485f, 0f, -1.000006f)),
            new("mixamorig:LeftUpLeg", 0, new Vector3(0.0334858f, -0.009149f, -0.0004158f), new Vector3(2.1257413f, 0.0000057f, 179.9999013f)),
            new("mixamorig:LeftLeg", 10, new Vector3(0f, 0.14234f, 0f), new Vector3(-7.2934469f, 0.0003309f, 0.0000205f)),
            new("mixamorig:LeftFoot", 11, new Vector3(0f, 0.1614f, 0f), new Vector3(8.9904054f, -0.0003669f, 0.0001851f)),
            new("mixamorig:RightUpLeg", 0, new Vector3(-0.0332738f, -0.008499f, -0.0001711f), new Vector3(2.1260586f, 0.0000015f, -179.9999146f)),
            new("mixamorig:RightLeg", 13, new Vector3(0f, 0.1423609f, 0f), new Vector3(-7.2933832f, -0.0003549f, -0.0000032f)),
            new("mixamorig:RightFoot", 14, new Vector3(0f, 0.1614081f, 0f), new Vector3(8.9904819f, 0.0003291f, -0.000188f)),
        };

        private static readonly BoneDef[] Tank =
        {
            new("mixamorig:Hips", -1, new Vector3(0f, 1.0953562f, 0.0008502f), new Vector3(0f, 0f, 0.0001f)),
            new("mixamorig:Spine", 0, new Vector3(0f, 0.096236f, 0.0038293f), new Vector3(-2.2792307f, 0.0000041f, -0.0000938f)),
            new("mixamorig:Spine1", 1, new Vector3(0f, 0.0607874f, 0f), new Vector3(0.8853473f, -0.0000075f, 0.0000428f)),
            new("mixamorig:Spine2", 2, new Vector3(0f, 0.0720596f, 0f), new Vector3(3.7819622f, -0.0000108f, 0.0000618f)),
            new("mixamorig:LeftShoulder", 3, new Vector3(0.0587699f, 0.0950934f, -0.0366174f), new Vector3(93.3708601f, 89.8495069f, -0.0000045f)),
            new("mixamorig:LeftArm", 4, new Vector3(0f, 0.0730927f, 0f), new Vector3(-4.9766482f, -0.0102139f, -0.1498982f)),
            new("mixamorig:LeftForeArm", 5, new Vector3(0f, 0.2595093f, 0f), new Vector3(-0.0000084f, 0f, 0.9999707f)),
            new("mixamorig:RightShoulder", 3, new Vector3(-0.0587699f, 0.0950934f, -0.0366174f), new Vector3(-93.3708621f, -89.8495069f, -179.9999044f)),
            new("mixamorig:RightArm", 7, new Vector3(0f, 0.0730927f, 0f), new Vector3(-4.9766441f, 0.0102077f, 0.149899f)),
            new("mixamorig:RightForeArm", 8, new Vector3(0f, 0.2595093f, 0f), new Vector3(-0.0000119f, 0f, -0.9999707f)),
            new("mixamorig:LeftUpLeg", 0, new Vector3(0.1064459f, -0.0636251f, -0.0018913f), new Vector3(8.4927588f, -0.000003f, 179.9999064f)),
            new("mixamorig:LeftLeg", 10, new Vector3(0f, 0.4432255f, 0f), new Vector3(-17.2740643f, 0.0004074f, 0.0000611f)),
            new("mixamorig:LeftFoot", 11, new Vector3(0f, 0.4786126f, 0f), new Vector3(56.736361f, -5.4334894f, 2.8642228f)),
            new("mixamorig:RightUpLeg", 0, new Vector3(-0.1064459f, -0.0636251f, -0.0018913f), new Vector3(8.4927645f, 0.000003f, -179.9999064f)),
            new("mixamorig:RightLeg", 13, new Vector3(0f, 0.4432255f, 0f), new Vector3(-17.2740653f, -0.0004074f, -0.0000611f)),
            new("mixamorig:RightFoot", 14, new Vector3(0f, 0.4786126f, 0f), new Vector3(56.7363589f, 5.4334888f, -2.864224f)),
        };

        private static readonly BoneDef[] Mage =
        {
            new("mixamorig:Hips", -1, new Vector3(0f, 1.2414586f, -0.0017099f), new Vector3(0f, 0f, 0.0001f)),
            new("mixamorig:Spine", 0, new Vector3(-0.0000086f, 0.0993861f, 0.0079974f), new Vector3(-1.4121762f, 0.0000071f, -0.000094f)),
            new("mixamorig:Spine1", 1, new Vector3(0f, 0.1159791f, 0f), new Vector3(1.1200697f, -0.0000076f, 0.0000612f)),
            new("mixamorig:Spine2", 2, new Vector3(0f, 0.1329013f, 0f), new Vector3(3.9301702f, -0.0000084f, 0.0000677f)),
            new("mixamorig:LeftShoulder", 3, new Vector3(0.0829819f, 0.1205951f, -0.0589165f), new Vector3(93.895702f, 89.8499378f, -0.0000052f)),
            new("mixamorig:LeftArm", 4, new Vector3(0f, 0.0864156f, 0f), new Vector3(-4.0957693f, -0.0101919f, -0.1496647f)),
            new("mixamorig:LeftForeArm", 5, new Vector3(0f, 0.2844664f, 0f), new Vector3(-0.0000298f, 0f, 0.9999764f)),
            new("mixamorig:RightShoulder", 3, new Vector3(-0.0829819f, 0.1205951f, -0.0589165f), new Vector3(-93.8957012f, -89.8499378f, -179.9998948f)),
            new("mixamorig:RightArm", 7, new Vector3(0f, 0.0864156f, 0f), new Vector3(-4.095773f, 0.0101922f, 0.1496649f)),
            new("mixamorig:RightForeArm", 8, new Vector3(0f, 0.2844664f, 0f), new Vector3(-0.0000262f, 0f, -0.9999764f)),
            new("mixamorig:LeftUpLeg", 0, new Vector3(0.1220176f, -0.0199502f, 0.0001324f), new Vector3(6.1357913f, 0.0000225f, 179.9998769f)),
            new("mixamorig:LeftLeg", 10, new Vector3(0f, 0.5547173f, 0f), new Vector3(-15.6380688f, 0.000404f, 0.0000639f)),
            new("mixamorig:LeftFoot", 11, new Vector3(0f, 0.5649003f, 0f), new Vector3(54.2965522f, -7.6672583f, 2.8850877f)),
            new("mixamorig:RightUpLeg", 0, new Vector3(-0.1220176f, -0.0199502f, 0.0001324f), new Vector3(6.1357969f, -0.0000225f, -179.9998769f)),
            new("mixamorig:RightLeg", 13, new Vector3(0f, 0.5547173f, 0f), new Vector3(-15.6380698f, -0.000404f, -0.0000639f)),
            new("mixamorig:RightFoot", 14, new Vector3(0f, 0.5649003f, 0f), new Vector3(54.2965502f, 7.6672577f, -2.885089f)),
        };

        private static readonly BoneDef[] Brakk =
        {
            new("mixamorig:Hips", -1, new Vector3(0f, 1.0129298f, -0.0166432f), new Vector3(0f, 0f, 0.0001f)),
            new("mixamorig:Spine", 0, new Vector3(0f, 0.1294736f, 0.0130686f), new Vector3(-3.1554732f, 0.0000043f, -0.0000938f)),
            new("mixamorig:Spine1", 1, new Vector3(0f, 0.1165054f, 0f), new Vector3(0.9718899f, -0.0000075f, 0.0000464f)),
            new("mixamorig:Spine2", 2, new Vector3(0f, 0.1189961f, 0f), new Vector3(4.8600054f, -0.0000117f, 0.0000724f)),
            new("mixamorig:LeftShoulder", 3, new Vector3(0.0805835f, 0.1037567f, -0.0516743f), new Vector3(94.0434132f, 89.8491679f, -0.0000062f)),
            new("mixamorig:LeftArm", 4, new Vector3(0f, 0.1210969f, 0f), new Vector3(-4.5074295f, -0.0101955f, -0.1497228f)),
            new("mixamorig:LeftForeArm", 5, new Vector3(0f, 0.2321796f, 0f), new Vector3(-0.0000365f, 0f, 0.9999657f)),
            new("mixamorig:RightShoulder", 3, new Vector3(-0.0805835f, 0.1037567f, -0.0516743f), new Vector3(-94.0434126f, -89.8491679f, -179.9998938f)),
            new("mixamorig:RightArm", 7, new Vector3(0f, 0.1210969f, 0f), new Vector3(-4.5074325f, 0.0101958f, 0.149723f)),
            new("mixamorig:RightForeArm", 8, new Vector3(0f, 0.2321796f, 0f), new Vector3(-0.000033f, 0f, -0.9999657f)),
            new("mixamorig:LeftUpLeg", 0, new Vector3(0.1565853f, -0.084068f, 0.0146695f), new Vector3(13.0994058f, -0.0000062f, 179.999907f)),
            new("mixamorig:LeftLeg", 10, new Vector3(0f, 0.3707475f, 0f), new Vector3(-17.8943034f, 0.0004044f, 0.0000647f)),
            new("mixamorig:LeftFoot", 11, new Vector3(0f, 0.3810747f, 0f), new Vector3(59.1545338f, -5.7969117f, 2.8489769f)),
            new("mixamorig:RightUpLeg", 0, new Vector3(-0.1565853f, -0.084068f, 0.0146695f), new Vector3(13.0994115f, 0.0000062f, -179.999907f)),
            new("mixamorig:RightLeg", 13, new Vector3(0f, 0.3707475f, 0f), new Vector3(-17.8943045f, -0.0004044f, -0.0000647f)),
            new("mixamorig:RightFoot", 14, new Vector3(0f, 0.3810747f, 0f), new Vector3(59.1545318f, 5.7969111f, -2.8489782f)),
        };

        private static readonly BoneDef[] HotZone =
        {
            new("mixamorig:Hips", -1, new Vector3(0f, 0.9740293f, 0.0057678f), new Vector3(0f, 0f, 0.0001f)),
            new("mixamorig:Spine", 0, new Vector3(0f, 0.1366387f, -0.0118399f), new Vector3(-3.1495357f, 0.0000043f, -0.0000938f)),
            new("mixamorig:Spine1", 1, new Vector3(0f, 0.1608626f, 0f), new Vector3(0.8008343f, -0.0000068f, 0.0000364f)),
            new("mixamorig:Spine2", 2, new Vector3(0f, 0.1181138f, 0f), new Vector3(7.9702862f, -0.0000173f, 0.0000865f)),
            new("mixamorig:LeftShoulder", 3, new Vector3(0.0833517f, 0.1105872f, -0.0550232f), new Vector3(93.9939876f, 89.8489448f, -0.0000064f)),
            new("mixamorig:LeftArm", 4, new Vector3(0f, 0.1124538f, 0f), new Vector3(-4.6885035f, -0.0101984f, -0.1497727f)),
            new("mixamorig:LeftForeArm", 5, new Vector3(0f, 0.247276f, 0f), new Vector3(-0.0000397f, 0f, 0.9999619f)),
            new("mixamorig:RightShoulder", 3, new Vector3(-0.0833517f, 0.1105872f, -0.0550232f), new Vector3(-93.993987f, -89.8489448f, -179.9998936f)),
            new("mixamorig:RightArm", 7, new Vector3(0f, 0.1124538f, 0f), new Vector3(-4.6885064f, 0.0101987f, 0.1497729f)),
            new("mixamorig:RightForeArm", 8, new Vector3(0f, 0.247276f, 0f), new Vector3(-0.0000362f, 0f, -0.9999619f)),
            new("mixamorig:LeftUpLeg", 0, new Vector3(0.1162191f, -0.0759107f, -0.007136f), new Vector3(11.5619404f, -0.0000048f, 179.9999083f)),
            new("mixamorig:LeftLeg", 10, new Vector3(0f, 0.37306f, 0f), new Vector3(-22.3837272f, 0.0004102f, 0.0000533f)),
            new("mixamorig:LeftFoot", 11, new Vector3(0f, 0.4037432f, 0f), new Vector3(60.8373559f, -5.9354807f, 2.8428923f)),
            new("mixamorig:RightUpLeg", 0, new Vector3(-0.1162191f, -0.0759107f, -0.007136f), new Vector3(11.5619461f, 0.0000048f, -179.9999083f)),
            new("mixamorig:RightLeg", 13, new Vector3(0f, 0.37306f, 0f), new Vector3(-22.3837283f, -0.0004102f, -0.0000533f)),
            new("mixamorig:RightFoot", 14, new Vector3(0f, 0.4037432f, 0f), new Vector3(60.8373539f, 5.9354801f, -2.8428936f)),
        };

        private static readonly BoneDef[] Soldier =
        {
            new("mixamorig:Hips", -1, new Vector3(0f, 1.0093268f, 0.0320688f), new Vector3(0f, 0f, 0.0001f)),
            new("mixamorig:Spine", 0, new Vector3(0f, 0.0946559f, -0.0117907f), new Vector3(-3.4221217f, 0.0000047f, -0.0000937f)),
            new("mixamorig:Spine1", 1, new Vector3(0f, 0.1112853f, 0f), new Vector3(1.6793441f, -0.0000066f, 0.0000507f)),
            new("mixamorig:Spine2", 2, new Vector3(0f, 0.1271832f, 0f), new Vector3(3.4473275f, -0.0000103f, 0.0000608f)),
            new("mixamorig:LeftShoulder", 3, new Vector3(0.068328f, 0.1017933f, -0.0468029f), new Vector3(94.0687624f, 89.8438969f, -0.0000068f)),
            new("mixamorig:LeftArm", 4, new Vector3(0f, 0.0763037f, 0f), new Vector3(-4.2112689f, -0.0105447f, -0.1578117f)),
            new("mixamorig:LeftForeArm", 5, new Vector3(0f, 0.2425903f, 0f), new Vector3(-0.0000357f, 0f, 0.9999659f)),
            new("mixamorig:RightShoulder", 3, new Vector3(-0.068328f, 0.1017933f, -0.0468029f), new Vector3(-94.0687618f, -89.8438969f, -179.9998932f)),
            new("mixamorig:RightArm", 7, new Vector3(0f, 0.0763037f, 0f), new Vector3(-4.2112719f, 0.010545f, 0.1578119f)),
            new("mixamorig:RightForeArm", 8, new Vector3(0f, 0.2425903f, 0f), new Vector3(-0.0000321f, 0f, -0.9999659f)),
            new("mixamorig:LeftUpLeg", 0, new Vector3(0.0983167f, -0.0526747f, -0.006028f), new Vector3(10.772149f, -0.0000039f, 179.7837197f)),
            new("mixamorig:LeftLeg", 10, new Vector3(0f, 0.4032689f, 0f), new Vector3(-20.1010094f, 0.0003974f, 0.0000561f)),
            new("mixamorig:LeftFoot", 11, new Vector3(0f, 0.4367158f, 0f), new Vector3(57.8900834f, -6.4778044f, 3.3768562f)),
            new("mixamorig:RightUpLeg", 0, new Vector3(-0.0983167f, -0.0526747f, -0.006028f), new Vector3(10.7721547f, 0.0000039f, -179.7837197f)),
            new("mixamorig:RightLeg", 13, new Vector3(0f, 0.4032689f, 0f), new Vector3(-20.1010105f, -0.0003974f, -0.0000561f)),
            new("mixamorig:RightFoot", 14, new Vector3(0f, 0.4367158f, 0f), new Vector3(57.8900814f, 6.4778038f, -3.3768575f)),
        };

        private static readonly BoneDef[] Traveler =
        {
            new("mixamorig:Hips", -1, new Vector3(0f, 0.9637561f, -0.0003547f), new Vector3(0f, 0f, 0.0001f)),
            new("mixamorig:Spine", 0, new Vector3(0f, 0.092263f, 0.015771f), new Vector3(-4.1057133f, 0.0000056f, -0.0000932f)),
            new("mixamorig:Spine1", 1, new Vector3(0f, 0.1626259f, 0f), new Vector3(1.8514685f, -0.000005f, 0.0000531f)),
            new("mixamorig:Spine2", 2, new Vector3(0f, 0.1375622f, 0f), new Vector3(4.6319182f, -0.0000104f, 0.0000713f)),
            new("mixamorig:LeftShoulder", 3, new Vector3(0.0654545f, 0.1090837f, -0.0605181f), new Vector3(93.2505088f, 89.849599f, -0.0000044f)),
            new("mixamorig:LeftArm", 4, new Vector3(0f, 0.083831f, 0f), new Vector3(-5.1005538f, -0.0102102f, -0.1501624f)),
            new("mixamorig:LeftForeArm", 5, new Vector3(0f, 0.2302142f, 0f), new Vector3(-0.0000104f, 0f, 0.9999692f)),
            new("mixamorig:RightShoulder", 3, new Vector3(-0.0654545f, 0.1090837f, -0.0605181f), new Vector3(-93.2505107f, -89.849599f, -179.9998956f)),
            new("mixamorig:RightArm", 7, new Vector3(0f, 0.083831f, 0f), new Vector3(-5.1005497f, 0.0102041f, 0.1501632f)),
            new("mixamorig:RightForeArm", 8, new Vector3(0f, 0.2302142f, 0f), new Vector3(-0.0000139f, 0f, -0.9999692f)),
            new("mixamorig:LeftUpLeg", 0, new Vector3(0.096338f, -0.0327331f, 0f), new Vector3(11.0392678f, -0.0000041f, 179.9707581f)),
            new("mixamorig:LeftLeg", 10, new Vector3(0f, 0.4167985f, 0f), new Vector3(-21.2037255f, 0.0003917f, 0.0000543f)),
            new("mixamorig:LeftFoot", 11, new Vector3(0f, 0.3928232f, 0f), new Vector3(57.2473192f, -5.6200343f, 2.9817656f)),
            new("mixamorig:RightUpLeg", 0, new Vector3(-0.096338f, -0.0327331f, 0f), new Vector3(11.0392735f, 0.0000041f, -179.9707581f)),
            new("mixamorig:RightLeg", 13, new Vector3(0f, 0.4167985f, 0f), new Vector3(-21.2037266f, -0.0003917f, -0.0000543f)),
            new("mixamorig:RightFoot", 14, new Vector3(0f, 0.3928232f, 0f), new Vector3(57.2473172f, 5.6200337f, -2.9817669f)),
        };

        public static GameObject BuildForHero(HeroId hero, Transform parent, out string templateName)
        {
            BoneDef[] bones;
            switch (hero)
            {
                case HeroId.Veyr: bones = Assassin; templateName = "AV_Rig_Assassin"; break;
                case HeroId.Kaelon: bones = Tank; templateName = "AV_Rig_Tank"; break;
                case HeroId.Nyra: bones = Soldier; templateName = "AV_Rig_Soldier"; break;
                case HeroId.Elyra:
                case HeroId.Mira: bones = Mage; templateName = "AV_Rig_Mage"; break;
                case HeroId.Rook: bones = Brakk; templateName = "AV_Rig_Brakk"; break;
                case HeroId.Traveler: bones = Traveler; templateName = "AV_Rig_Traveler"; break;
                default: bones = Soldier; templateName = "AV_Rig_Soldier"; break;
            }
            return Build(bones, templateName, parent);
        }

        public static GameObject BuildByResourceName(string resourceName, Transform parent)
        {
            BoneDef[] bones = resourceName switch
            {
                "AV_Rig_Assassin" => Assassin,
                "AV_Rig_Tank" => Tank,
                "AV_Rig_Mage" => Mage,
                "AV_Rig_Brakk" => Brakk,
                "AV_Rig_HotZone" => HotZone,
                "AV_Rig_Soldier" => Soldier,
                "AV_Rig_Traveler" => Traveler,
                _ => Soldier
            };
            return Build(bones, resourceName, parent);
        }

        private static GameObject Build(BoneDef[] bones, string name, Transform parent)
        {
            GameObject root = new GameObject(name + "_RuntimeSkeleton");
            root.transform.SetParent(parent, false);
            Transform[] created = new Transform[bones.Length];

            for (int i = 0; i < bones.Length; i++)
            {
                BoneDef bone = bones[i];
                GameObject node = new GameObject(bone.Name);
                Transform nodeTransform = node.transform;
                nodeTransform.SetParent(bone.Parent >= 0 ? created[bone.Parent] : root.transform, false);
                nodeTransform.localPosition = bone.Position;
                nodeTransform.localRotation = Quaternion.Euler(bone.Euler);
                nodeTransform.localScale = Vector3.one;
                created[i] = nodeTransform;
            }

            Animator animator = root.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            return root;
        }
    }
}