using UnityEngine;
namespace Alterverse.UI
{
    // One coordinate system for drawing and touch hit-testing; safe area uses bottom-left coordinates.
    public readonly struct CombatHudLayout
    {
        public const float ControlScale=1.25f, ControlsLift=32f, AttackIconOpacity=.70f;
        public readonly Rect Safe;
        public readonly float Scale, Width, Height;
        public CombatHudLayout(Rect safe)
        {
            Safe=safe; Scale=Mathf.Max(.01f,Mathf.Min(safe.width/960f,safe.height/540f));
            Width=safe.width/Scale; Height=safe.height/Scale;
        }
        public Vector2 Point(Vector2 screen)=>new Vector2((screen.x-Safe.x)/Scale,(Safe.yMax-screen.y)/Scale);
        public Rect Action(int slot)
        {
            // Scale the complete cluster about its bottom/right inset, including
            // spacing. Drawing and hit-testing continue to use this same rectangle.
            Rect original=slot switch
            {
                0=>new Rect(-234,-104,80,80),
                1=>new Rect(-226,-204,80,80),
                2=>new Rect(-126,-236,80,80),
                3=>new Rect(-330,-180,84,84),
                _=>new Rect(-126,-128,102,102)
            };
            return new Rect(Width-24f+(original.x+24f)*ControlScale,
                Height-24f+(original.y+24f)*ControlScale-ControlsLift,
                original.width*ControlScale,original.height*ControlScale);
        }
        public int Hit(Vector2 point){for(int i=0;i<5;i++)if(Action(i).Contains(point))return i;return -1;}
        public bool ContainsWithMargin(int slot,Vector2 point)
        {Rect r=Action(slot);r.xMin-=20;r.yMin-=20;r.xMax+=20;r.yMax+=20;return r.Contains(point);}
        public const float JoystickRadius=74f*ControlScale, ThumbRadius=26f*ControlScale;
        public Vector2 JoystickIdle=>new Vector2(164f,Height-124f-ControlsLift);
        public Vector2 JoystickCenter(Vector2 point)
        {
            float inset=JoystickRadius+ThumbRadius+12f;
            return new Vector2(Mathf.Clamp(point.x,inset,Width-inset),
                Mathf.Clamp(point.y,inset,Height-inset-ControlsLift));
        }
        // Anchor cancellation to the top of the safe area on every display aspect ratio.
        public Rect CancelZone=>new Rect(Width-218f,16f,110f,72f);
        // Floating movement can start anywhere on the left screen half, including safe-area margins.
        // Drawing still respects the safe area; PrototypeHUD gives combat buttons priority.
        public Rect MoveCapture=>Rect.MinMaxRect(-Safe.x/Scale,(Safe.yMax-Screen.height)/Scale,
            (Screen.width*.5f-Safe.x)/Scale,(Safe.yMax+1f)/Scale);
        public Matrix4x4 Matrix=>Matrix4x4.TRS(new Vector3(Safe.x,Screen.height-Safe.yMax,0),Quaternion.identity,Vector3.one*Scale);
    }
}
