using Alterverse.Characters;
using UnityEngine;

namespace Alterverse.Visuals
{
    public sealed partial class StylizedHeroVisual
    {
        // Build once, before batching. Keep articulated mounts attached to their existing joint.
        private void BuildModernHeroFinish()
        {
            HeroId hero=identity.Hero;
            string[] chest=hero switch
            {
                HeroId.Veyr=>new[]{"VeyrChestEdge"},
                HeroId.Kaelon=>new[]{"KaelonChestInset"},
                HeroId.Nyra=>new[]{"NyraChestL","NyraChestR"},
                HeroId.Elyra=>new[]{"MageChestL","MageChestR"},
                HeroId.Mira=>new[]{"TahlaChestInset"},
                HeroId.Rook=>new[]{"BrakkChestL","BrakkChestR"},
                _=>new[]{"TravelerChestL","TravelerChestR"}
            };
            Color metal=hero==HeroId.Mira?new Color(.78f,.82f,.79f):
                hero==HeroId.Kaelon?new Color(.68f,.63f,.49f):new Color(.42f,.49f,.56f);
            Color inset=new Color(.045f,.055f,.074f);
            Color signal=hero==HeroId.Mira?new Color(.22f,.95f,.48f):
                hero==HeroId.Rook?new Color(1f,.40f,.06f):accent;
            foreach(string name in chest)
            {
                Transform mount=FindVisual(name);
                MeshFilter filter=mount!=null?mount.GetComponent<MeshFilter>():null;
                if(filter==null||filter.sharedMesh==null)continue;
                Bounds b=filter.sharedMesh.bounds;
                float z=b.max.z+.008f;
                for(int side=-1;side<=1;side+=2)
                {
                    float x=b.center.x+side*b.size.x*.29f;
                    MountedModernDetail(name+"Frame"+side,mount,
                        new Vector3(x,b.center.y,z),new Vector3(.021f,b.size.y*.48f,.014f),metal);
                    MountedModernDetail(name+"Socket"+side,mount,
                        new Vector3(x,b.center.y+b.size.y*.12f,z+.01f),new Vector3(.068f,.076f,.022f),inset);
                    MountedModernDetail(name+"Cell"+side,mount,
                        new Vector3(x,b.center.y+b.size.y*.12f,z+.025f),new Vector3(.035f,.042f,.009f),signal,true);
                    for(int vent=0;vent<3;vent++)
                        MountedModernDetail(name+"Vent"+side+"_"+vent,mount,
                            new Vector3(x,b.center.y-b.size.y*(.10f+vent*.065f),z+.01f),
                            new Vector3(.059f,.012f,.012f),inset);
                }
            }
            // Compact identifying chevrons on armor, never on faces, cloth or weapon grips.
            string shoulder=hero switch
            {
                HeroId.Veyr=>"VeyrShoulderL",HeroId.Kaelon=>"KaelonPauldronOuterL",
                HeroId.Nyra=>"NyraShoulderL",HeroId.Elyra=>"MageShoulderOuterL",
                HeroId.Mira=>"TahlaShoulderL",HeroId.Rook=>"BrakkShoulderL",
                _=>"TravelerShoulderL"
            };
            Transform shoulderMount=FindVisual(shoulder);
            MeshFilter shoulderFilter=shoulderMount!=null?shoulderMount.GetComponent<MeshFilter>():null;
            if(shoulderFilter!=null&&shoulderFilter.sharedMesh!=null)
            {
                Bounds b=shoulderFilter.sharedMesh.bounds;
                for(int side=-1;side<=1;side+=2)
                    MountedModernDetail("RoleChevron"+side,shoulderMount,
                        new Vector3(b.center.x+side*.025f,b.center.y,b.max.z+.013f),
                        new Vector3(.016f,.083f,.012f),metal,false,side*38f);
            }
        }

        private void MountedModernDetail(string name,Transform mount,Vector3 local,Vector3 size,
            Color color,bool energy=false,float angle=0)
        {
            GameObject part=Create("Modern"+name,PrimitiveType.Cube,Vector3.zero,size,color);
            Transform detail=part.transform;
            Vector3 ratio=new Vector3(mount.lossyScale.x/root.lossyScale.x,
                mount.lossyScale.y/root.lossyScale.y,mount.lossyScale.z/root.lossyScale.z);
            detail.localPosition=root.InverseTransformPoint(mount.TransformPoint(local));
            detail.localScale=Vector3.Scale(size,ratio);
            detail.rotation=mount.rotation*Quaternion.Euler(0,0,angle);
            if(energy)
            {
                HeroMeshLibrary.Paint(part.GetComponent<MeshFilter>().sharedMesh,color,true);
                part.GetComponent<MeshRenderer>().sharedMaterial=MaterialFor(color,true);
            }
            // Static pieces remain root children for CombineStaticPanels. Animated pieces
            // attach to the top-level joint, so destroying a batched plate cannot erase them.
            Transform anchor=mount;
            while(anchor.parent!=null&&anchor.parent!=root)anchor=anchor.parent;
            if(anchor!=mount)detail.SetParent(anchor,true);
        }
    }
}
