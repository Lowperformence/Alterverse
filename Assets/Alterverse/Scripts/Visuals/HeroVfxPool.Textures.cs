using System.Collections.Generic;
using Alterverse.Combat;
using UnityEngine;

namespace Alterverse.Visuals
{
    // Frames are packed once by ArtSource/CombatVfx/build_atlas.py, never during play.
    internal enum HeroFxSprite
    {
        None,Glow,Burst,Muzzle,Smoke,Rune,Vortex,ClawMark,Slash,Flame,ShockRing,
        Flare,Trace,ShieldGlyph,Dust,Portal,FireSpark,Star,SoftStar,
        SlashLight,SlashHeavy,SlashClaw,SlashCross,SlashLunge,Fireball
    }

    public sealed partial class HeroVfxPool
    {
        private Material attackAtlas;
        private Quaternion billboardRotation=Quaternion.Euler(55f,0f,0f);
        private static readonly int AtlasCell=Shader.PropertyToID("_AtlasCell");

        private void LoadAttackAtlas()
        {
            attackAtlas=Resources.Load<Material>("CombatVfx/AttackAtlas");
            if(attackAtlas==null||attackAtlas.shader==null||!attackAtlas.shader.isSupported||
                attackAtlas.shader.name!="DimensionsFront/AttackAtlas")attackAtlas=null;
            if(attackAtlas==null)return;
            Texture2D texture=attackAtlas.GetTexture("_BaseMap") as Texture2D;
            if(texture==null||texture.width!=1024||texture.height!=1024)attackAtlas=null;
        }

        internal static int AtlasFrame(HeroFxSprite sprite,float age)
        {
            if(sprite==HeroFxSprite.None)return 0;
            if(sprite<=HeroFxSprite.SoftStar)return (int)sprite-1;
            int count=sprite==HeroFxSprite.Fireball?16:6;
            int first=sprite==HeroFxSprite.Fireball?48:18+((int)sprite-(int)HeroFxSprite.SlashLight)*6;
            return first+Mathf.Min(count-1,Mathf.FloorToInt(Mathf.Clamp01(age)*count));
        }

        internal static void Sprite(HeroFxSprite sprite,Vector3 from,Vector3 to,Vector2 size,Color color,
            float duration,GameObject owner,HeroFxMotion motion=HeroFxMotion.Hold,
            Quaternion rotation=default,bool billboard=true,Transform follow=null,Transform endFollow=null,
            float delay=0,float elapsed=0,float roll=0,Health shield=null,bool shieldUsesTotal=false)
        {
            if(sprite==HeroFxSprite.None)return;
            Emit(HeroFxShape.TexturedQuad,from,to,new Vector3(size.x,size.y,1f),color,duration,motion,
                rotation,owner,follow:follow,shield:shield,delay:delay,endFollow:endFollow,decorative:true,
                elapsed:elapsed,cancelOnOwnerDeath:owner!=null,sprite:sprite,billboard:billboard,roll:roll,shieldUsesTotal:shieldUsesTotal);
        }

        private static Mesh BuildSpriteQuad()
        {
            var mesh=new Mesh{name="DF_AttackAtlasQuad"};
            mesh.SetVertices(new List<Vector3>{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),
                new Vector3(.5f,.5f,0),new Vector3(-.5f,.5f,0)});
            mesh.SetUVs(0,new List<Vector2>{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)});
            mesh.SetTriangles(new List<int>{0,1,2,0,2,3},0);
            mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }

        internal static void CancelSprite(GameObject actor,HeroFxSprite sprite,HeroFxMotion motion)
        {
            if(instance==null)return;
            foreach(Slot slot in instance.slots)
                if(slot!=null&&slot.Active&&slot.Owner==actor&&slot.Sprite==sprite&&slot.Motion==motion)
                    instance.Hide(slot);
        }
    }
}
