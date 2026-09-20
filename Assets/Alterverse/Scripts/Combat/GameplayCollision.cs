using UnityEngine;
namespace Alterverse.Combat
{
    // Root hitboxes stay queryable. Mobile units never form movement walls.
    public static class GameplayCollision
    {
        public const int Units=8, Structures=9, Ground=10, Decorative=11;
        public const int MovementMask=(1<<0)|(1<<Structures)|(1<<Ground);
        public const int TargetMask=(1<<0)|(1<<Units)|(1<<Structures);
        public const int SightMask=(1<<0)|(1<<Structures);
    }
}
