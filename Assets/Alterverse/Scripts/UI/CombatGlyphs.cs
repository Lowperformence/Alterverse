using UnityEngine;
namespace Alterverse.UI
{
    internal enum CombatGlyph { Blade, Heavy, Rifle, Orb, SupportShot, Gauntlet, Shot, Area, Dash, Heal, Shield, Stun, Volley, Rift, Slam, Burst }
    // Mobile-first vector glyphs: no textures, materials, GameObjects or runtime allocations.
    internal static class CombatGlyphs
    {
        public static void Line(Vector2 a,Vector2 b,float width,Color color){Vector2 d=b-a;Matrix4x4 m=GUI.matrix;try{GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg,a);FrontEndTheme.Fill(new Rect(a.x,a.y-width*.5f,d.magnitude,width),color);}finally{GUI.matrix=m;}}
        public static void Ring(Vector2 c,float r,float w,Color color,int count=24){for(int i=0;i<count;i++){float a=i*Mathf.PI*2/count,b=(i+1)*Mathf.PI*2/count;Line(c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*r,c+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*r,w,color);}}
        private static void Arrow(Vector2 c,float s,Color color){Line(c+Vector2.left*s,c+Vector2.right*s*.75f,4,color);Line(c+new Vector2(s*.25f,-s*.5f),c+Vector2.right*s*.8f,4,color);Line(c+new Vector2(s*.25f,s*.5f),c+Vector2.right*s*.8f,4,color);}
        public static void Draw(CombatGlyph g,Vector2 c,float size,Color color)
        {
            float s=size*.5f;
            switch(g)
            {
                case CombatGlyph.Blade: Line(c+new Vector2(-s*.75f,s),c+new Vector2(s*.65f,-s*.75f),6,color);Line(c+new Vector2(-s*.55f,s*.15f),c+new Vector2(s*.25f,s*.75f),3,color);break;
                case CombatGlyph.Heavy: Line(c+new Vector2(-s*.65f,s*.8f),c+new Vector2(s*.25f,-s*.35f),7,color);FrontEndTheme.Fill(new Rect(c.x+s*.05f,c.y-s*.75f,s*.75f,s*.5f),color);break;
                case CombatGlyph.Rifle: Line(c+new Vector2(-s,-s*.15f),c+new Vector2(s*.55f,-s*.15f),7,color);Line(c+new Vector2(-s*.15f,0),c+new Vector2(-s*.45f,s*.65f),5,color);Line(c+new Vector2(s*.65f,-s*.15f),c+new Vector2(s,s*.0f),3,color);break;
                case CombatGlyph.Orb: Ring(c,s*.55f,4,color,18);Ring(c,s*.2f,3,color,12);Line(c+new Vector2(-s,-s*.55f),c+new Vector2(-s*.55f,-s*.2f),3,color);Line(c+new Vector2(s,s*.55f),c+new Vector2(s*.55f,s*.2f),3,color);break;
                case CombatGlyph.SupportShot: Ring(c+new Vector2(-s*.35f,0),s*.25f,3,color,12);Line(c,c+Vector2.right*s,4,color);break;
                case CombatGlyph.Gauntlet: FrontEndTheme.Fill(new Rect(c.x-s*.55f,c.y-s*.15f,s*.9f,s*.75f),color);for(int i=0;i<3;i++)FrontEndTheme.Fill(new Rect(c.x-s*.45f+i*s*.32f,c.y-s*.65f,s*.25f,s*.55f),color);break;
                case CombatGlyph.Heal: Line(c+Vector2.left*s*.8f,c+Vector2.right*s*.8f,7,color);Line(c+Vector2.up*s*.8f,c+Vector2.down*s*.8f,7,color);Ring(c,s*.8f,2,color,20);break;
                case CombatGlyph.Shield: Line(c+new Vector2(-s*.75f,-s*.65f),c+new Vector2(s*.75f,-s*.65f),4,color);Line(c+new Vector2(-s*.75f,-s*.65f),c+new Vector2(-s*.6f,s*.25f),4,color);Line(c+new Vector2(s*.75f,-s*.65f),c+new Vector2(s*.6f,s*.25f),4,color);Line(c+new Vector2(-s*.6f,s*.25f),c+new Vector2(0,s),4,color);Line(c+new Vector2(s*.6f,s*.25f),c+new Vector2(0,s),4,color);break;
                case CombatGlyph.Dash: Arrow(c,s,color);Line(c+new Vector2(-s*.85f,-s*.5f),c+new Vector2(-s*.25f,-s*.5f),2,color);Line(c+new Vector2(-s*.85f,s*.5f),c+new Vector2(-s*.25f,s*.5f),2,color);break;
                case CombatGlyph.Area: Ring(c,s*.85f,4,color,20);Ring(c,s*.35f,3,color,14);for(int i=0;i<4;i++){float a=i*Mathf.PI*.5f;Line(c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*s*.45f,c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*s,3,color);}break;
                case CombatGlyph.Stun: Line(c+new Vector2(-s*.2f,-s),c+new Vector2(-s*.65f,0),5,color);Line(c+new Vector2(-s*.65f,0),c+new Vector2(s*.35f,-s*.15f),5,color);Line(c+new Vector2(s*.35f,-s*.15f),c+new Vector2(0,s),5,color);break;
                case CombatGlyph.Volley: for(int i=-1;i<=1;i++){Vector2 o=Vector2.up*i*s*.5f;Line(c+o+Vector2.left*s,c+o+Vector2.right*s*.7f,4,color);}break;
                case CombatGlyph.Rift: Line(c+new Vector2(-s,-s*.5f),c+new Vector2(-s*.2f,-s*.15f),4,color);Line(c+new Vector2(-s*.2f,-s*.15f),c+new Vector2(s*.15f,s*.65f),4,color);Line(c+new Vector2(s*.15f,s*.65f),c+new Vector2(s,-s*.35f),4,color);break;
                case CombatGlyph.Slam: Line(c+new Vector2(0,-s),c+new Vector2(0,s*.15f),7,color);for(int i=-1;i<=1;i++)Line(c+new Vector2(0,s*.2f),c+new Vector2(i*s*.75f,s),4,color);break;
                case CombatGlyph.Burst: Ring(c,s*.25f,4,color,10);for(int i=0;i<8;i++){float a=i*Mathf.PI*.25f;Line(c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*s*.4f,c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*s,4,color);}break;
                default: Ring(c,s*.55f,3,color,16);Arrow(c,s*.8f,color);break;
            }
        }
    }
}