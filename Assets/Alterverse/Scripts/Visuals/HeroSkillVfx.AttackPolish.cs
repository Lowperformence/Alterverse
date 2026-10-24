using Alterverse.Characters;
using Alterverse.Combat;
using UnityEngine;

namespace Alterverse.Visuals
{
    public sealed partial class HeroSkillVfx
    {
        // Ability palette shared by the replacement effects. No secondary overlay emitters.
        private static Color AttackColour(GameObject actor,int slot)
        {
            HeroId hero=Hero(actor);
            if(hero==HeroId.Nyra)return slot<0||slot==1?StylizedArtDirection.HeroAccent(hero):SivraSkillColor(slot);
            if(hero==HeroId.Traveler&&slot>=0)return TravelerColor(slot);
            if(hero==HeroId.Rook)return new Color(1f,.38f,.09f,.85f);
            if(hero==HeroId.Nerok)return new Color(.10f,.76f,1f,.78f);
            if(hero==HeroId.Kaelon&&slot==0)return new Color(.68f,.20f,1f,.90f);
            if(hero==HeroId.Kaelon&&slot==2)return new Color(.08f,.46f,1f,.90f);
            if(hero==HeroId.Kaelon&&slot==3)return new Color(1f,.84f,.06f,.80f);
            return Accent(actor);
        }

    }
}
