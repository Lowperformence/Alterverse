using Alterverse.Characters;
using UnityEngine;

namespace Alterverse.Visuals
{
    public sealed partial class StylizedHeroVisual
    {
        // Authored chest attachments are built before armor binding and mesh batching.
        // Keep each character's established palette, silhouette and animation hierarchy.
        private void BuildInterstellarFinish()
        {
            string[] plates;
            switch(identity.Hero)
            {
                case HeroId.Veyr: plates=new[]{"VeyrChestEdge"};break;
                case HeroId.Kaelon: plates=new[]{"KaelonChestInset"};break;
                case HeroId.Nyra: plates=new[]{"NyraChestL","NyraChestR"};break;
                case HeroId.Elyra: plates=new[]{"MageChestL","MageChestR"};break;
                case HeroId.Mira: plates=new[]{"TahlaChestInset"};break;
                case HeroId.Rook: plates=new[]{"BrakkChestL","BrakkChestR"};break;
                default: plates=new[]{"TravelerChestL","TravelerChestR"};break;
            }
            Color trim=Color.Lerp(new Color(.50f,.57f,.65f),accent,.18f);
            foreach(string plateName in plates)
            {
                Transform mount=FindVisual(plateName);
                MeshFilter filter=mount!=null?mount.GetComponent<MeshFilter>():null;
                if(filter==null||filter.sharedMesh==null)continue;
                Bounds bounds=filter.sharedMesh.bounds;
                float z=bounds.max.z+.009f;
                // Fine inset rails sit on the actual plate surface, never in guessed world space.
                for(int side=-1;side<=1;side+=2)
                {
                    Vector3 local=new Vector3(bounds.center.x+side*bounds.size.x*.22f,bounds.center.y,z);
                    Vector3 position=root.InverseTransformPoint(mount.TransformPoint(local));
                    string name="InterstellarRail"+plateName+side;
                    Detail(name,position,new Vector3(.012f,bounds.size.y*.40f,.010f),trim);
                    Transform rail=FindVisual(name);
                    rail.rotation=mount.rotation;
                    rail.SetParent(mount,true);
                    for(int i=0;i<3;i++)
                    {
                        local=new Vector3(bounds.center.x+side*bounds.size.x*.22f,bounds.center.y+bounds.size.y*(.17f-i*.065f),z+.008f);
                        position=root.InverseTransformPoint(mount.TransformPoint(local));
                        name="InterstellarStatus"+plateName+side+"_"+i;
                        Detail(name,position,new Vector3(.025f,.009f,.008f),accent,default,null,true);
                        Transform light=FindVisual(name);light.rotation=mount.rotation;light.SetParent(mount,true);
                    }
                }
            }
        }
    }
}
