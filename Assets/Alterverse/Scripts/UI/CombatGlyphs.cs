using UnityEngine;
namespace Alterverse.UI
{
    internal enum CombatGlyph
    {
        Blade,NullCut,Rift,Dash,Execute,Heavy,Impact,Shield,Gravity,Citadel,
        Rifle,PrismShot,Snare,Volley,Orb,OrbitFracture,Invert,Phase,Singularity,
        SupportShot,Seed,Heal,Shelter,Wildsurge,Gauntlet,Swing,Armor,Slam,Overdrive,ChronoBeam,TimeStep,ChronoField,Paradox
    }
    // Lightweight vector-style combat icons. No textures, materials or extra GameObjects.
    internal static class CombatGlyphs
    {
        public static void Line(Vector2 a,Vector2 b,float width,Color color)
        {
            Vector2 d=b-a;
            if(d.sqrMagnitude<.000001f||width<=0f)return;
            Matrix4x4 matrix=GUI.matrix;
            // Compose in HUD coordinates BEFORE safe-area translation/scaling. RotateAroundPivot
            // pre-multiplies a screen-space rotation around an unscaled pivot: it scatters the
            // segments whenever the mobile HUD scale is not 1 (and with display cutout offsets).
            try
            {
                GUI.matrix=LineMatrix(matrix,a,b);
                FrontEndTheme.Fill(new Rect(0,-width*.5f,d.magnitude,width),color);
            }
            finally{GUI.matrix=matrix;}
        }
        internal static Matrix4x4 LineMatrix(Matrix4x4 hud,Vector2 a,Vector2 b)
        {
            Vector2 d=b-a;
            return hud*Matrix4x4.TRS(new Vector3(a.x,a.y,0),
                Quaternion.Euler(0,0,Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg),Vector3.one);
        }
        public static void Ring(Vector2 center,float radius,float width,Color color,int count=28)
        {
            for(int i=0;i<count;i++){float a=i*Mathf.PI*2/count,b=(i+1)*Mathf.PI*2/count;
                Line(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,width,color);}
        }
        public static void Disc(Vector2 center,float radius,Color color,int count=32)
        {
            // Horizontal scan fill approximates a clean circular face and never leaks outside its radius.
            int steps=Mathf.Max(12,count);
            for(int i=-steps;i<=steps;i++)
            {
                float y=radius*i/steps;
                float half=Mathf.Sqrt(Mathf.Max(0,radius*radius-y*y));
                FrontEndTheme.Fill(new Rect(center.x-half,center.y+y-.75f,half*2,1.5f),color);
            }
        }
        private static void Cross(Vector2 c,float s,float w,Color col){Line(c+Vector2.left*s,c+Vector2.right*s,w,col);Line(c+Vector2.up*s,c+Vector2.down*s,w,col);}
        private static void Arrow(Vector2 c,float s,Color col){Line(c+new Vector2(-s,0),c+new Vector2(s*.65f,0),4,col);Line(c+new Vector2(s*.2f,-s*.45f),c+new Vector2(s*.7f,0),4,col);Line(c+new Vector2(s*.2f,s*.45f),c+new Vector2(s*.7f,0),4,col);}
        private static void ShieldShape(Vector2 c,float s,Color col)
        {
            Vector2 a=c+new Vector2(-s*.65f,-s*.65f),b=c+new Vector2(s*.65f,-s*.65f),d=c+new Vector2(0,s);
            Line(a,b,4,col);Line(a,c+new Vector2(-s*.55f,s*.25f),4,col);Line(b,c+new Vector2(s*.55f,s*.25f),4,col);
            Line(c+new Vector2(-s*.55f,s*.25f),d,4,col);Line(c+new Vector2(s*.55f,s*.25f),d,4,col);
        }
        private static void Fist(Vector2 c,float s,Color col)
        {
            FrontEndTheme.Fill(new Rect(c.x-s*.55f,c.y-s*.15f,s*1.1f,s*.85f),col);
            for(int i=0;i<3;i++)FrontEndTheme.Fill(new Rect(c.x-s*.52f+i*s*.36f,c.y-s*.62f,s*.28f,s*.48f),col);
            FrontEndTheme.Fill(new Rect(c.x-s*.75f,c.y+s*.2f,s*.3f,s*.45f),col);
        }
        private static void Blade(Vector2 c,float s,Color col)
        {
            Line(c+new Vector2(-s*.75f,s*.75f),c+new Vector2(s*.7f,-s*.7f),6,col);
            Line(c+new Vector2(-s*.45f,s*.15f),c+new Vector2(.0f,s*.58f),3,col);
            Line(c+new Vector2(s*.7f,-s*.7f),c+new Vector2(s*.35f,-s*.5f),3,col);
        }
        public static void Draw(CombatGlyph g,Vector2 c,float size,Color col)
        {
            float s=size*.5f;
            switch(g)
            {
                case CombatGlyph.Blade: Blade(c,s,col); break;
                case CombatGlyph.NullCut: Blade(c,s,col);Line(c+new Vector2(-s,-s*.35f),c+new Vector2(s,s*.35f),3,col);break;
                case CombatGlyph.Rift: Line(c+new Vector2(0,-s),c+new Vector2(-s*.25f,-s*.3f),5,col);Line(c+new Vector2(-s*.25f,-s*.3f),c+new Vector2(s*.18f,s*.05f),5,col);Line(c+new Vector2(s*.18f,s*.05f),c+new Vector2(-s*.18f,s*.45f),5,col);Line(c+new Vector2(-s*.18f,s*.45f),c+new Vector2(0,s),5,col);break;
                case CombatGlyph.Dash: Arrow(c,s,col);Line(c+new Vector2(-s,-s*.42f),c+new Vector2(-s*.25f,-s*.42f),2,col);Line(c+new Vector2(-s,s*.42f),c+new Vector2(-s*.25f,s*.42f),2,col);break;
                case CombatGlyph.Execute: Cross(c,s*.72f,4,col);Ring(c,s*.35f,3,col,16);Blade(c,s*.65f,col);break;
                case CombatGlyph.Heavy: FrontEndTheme.Fill(new Rect(c.x-s*.18f,c.y-s*.15f,s*.36f,s*1.1f),col);FrontEndTheme.Fill(new Rect(c.x-s*.7f,c.y-s*.62f,s*1.4f,s*.55f),col);break;
                case CombatGlyph.Impact: Line(c+new Vector2(0,-s),c+new Vector2(0,s*.15f),6,col);for(int i=-2;i<=2;i++)Line(c+new Vector2(i*s*.28f,s*.15f),c+new Vector2(i*s*.5f,s),3,col);break;
                case CombatGlyph.Shield: ShieldShape(c,s,col);break;
                case CombatGlyph.Gravity: Ring(c,s*.35f,4,col,18);for(int i=0;i<4;i++){float a=i*Mathf.PI*.5f;Vector2 d=new Vector2(Mathf.Cos(a),Mathf.Sin(a));Line(c+d*s,c+d*s*.48f,4,col);}break;
                case CombatGlyph.Citadel: ShieldShape(c,s,col);Ring(c,s*1.05f,2,col,22);FrontEndTheme.Fill(new Rect(c.x-s*.42f,c.y+s*.05f,s*.25f,s*.45f),col);FrontEndTheme.Fill(new Rect(c.x+s*.17f,c.y+s*.05f,s*.25f,s*.45f),col);break;
                case CombatGlyph.Rifle: Line(c+new Vector2(-s*.9f,s*.2f),c+new Vector2(s*.75f,-s*.2f),6,col);FrontEndTheme.Fill(new Rect(c.x-s*.35f,c.y+s*.15f,s*.32f,s*.45f),col);break;
                case CombatGlyph.PrismShot: Ring(c,s*.52f,2,col,18);Cross(c,s*.8f,2,col);Line(c+new Vector2(-s,0),c+new Vector2(s,0),5,col);break;
                case CombatGlyph.Snare: Ring(c,s*.55f,4,col,18);Line(c+new Vector2(-s,-s*.65f),c+new Vector2(s,s*.65f),3,col);Line(c+new Vector2(-s,s*.65f),c+new Vector2(s,-s*.65f),3,col);break;
                case CombatGlyph.Volley: for(int i=-2;i<=1;i++)Line(c+new Vector2(-s,i*s*.3f),c+new Vector2(s,i*s*.3f-s*.18f),3,col);break;
                case CombatGlyph.Orb: Ring(c,s*.45f,4,col,18);Ring(c,s*.82f,2,col,20);break;
                case CombatGlyph.OrbitFracture: Ring(c,s*.45f,4,col,16);Line(c+new Vector2(-s,-s*.55f),c+new Vector2(-s*.25f,-s*.8f),3,col);Line(c+new Vector2(s*.25f,s*.8f),c+new Vector2(s,s*.55f),3,col);break;
                case CombatGlyph.Invert: Ring(c,s*.82f,3,col,20);for(int i=0;i<4;i++){float a=i*Mathf.PI*.5f+.5f;Vector2 d=new Vector2(Mathf.Cos(a),Mathf.Sin(a));Line(c+d*s*.9f,c+d*s*.25f,3,col);}break;
                case CombatGlyph.Phase: Ring(c+Vector2.left*s*.35f,s*.58f,3,col,16);Ring(c+Vector2.right*s*.35f,s*.58f,3,col,16);Arrow(c,s*.65f,col);break;
                case CombatGlyph.Singularity: Ring(c,s*.9f,3,col,24);Ring(c,s*.55f,4,col,20);FrontEndTheme.Fill(new Rect(c.x-s*.18f,c.y-s*.18f,s*.36f,s*.36f),col);break;
                case CombatGlyph.SupportShot: Ring(c,s*.35f,4,col,16);Line(c+new Vector2(-s,-s*.7f),c+new Vector2(s*.65f,s*.55f),3,col);break;
                case CombatGlyph.Seed: Ring(c,s*.38f,4,col,16);for(int i=0;i<3;i++){float a=(-.7f+i*.7f);Line(c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*s*.45f,c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*s,3,col);}break;
                case CombatGlyph.Heal: Cross(c,s*.72f,7,col);Ring(c,s*.92f,2,col,20);break;
                case CombatGlyph.Shelter: ShieldShape(c,s,col);Cross(c,s*.28f,3,col);break;
                case CombatGlyph.Wildsurge: Cross(c,s*.48f,6,col);Ring(c,s*.68f,3,col,20);Ring(c,s,2,col,24);break;
                case CombatGlyph.Gauntlet: Fist(c,s,col);break;
                case CombatGlyph.Swing: Fist(c+new Vector2(s*.2f,0),s*.7f,col);Ring(c,s*.9f,3,col,12);break;
                case CombatGlyph.Armor: Fist(c,s*.62f,col);ShieldShape(c,s,col);break;
                case CombatGlyph.Slam: Fist(c+new Vector2(0,-s*.35f),s*.58f,col);for(int i=-2;i<=2;i++)Line(c+new Vector2(i*s*.2f,s*.2f),c+new Vector2(i*s*.45f,s),3,col);break;
                case CombatGlyph.Overdrive: Fist(c+new Vector2(-s*.38f,0),s*.52f,col);Fist(c+new Vector2(s*.38f,0),s*.52f,col);Ring(c,s,3,col,22);break;
                case CombatGlyph.ChronoBeam:
                    Ring(c-new Vector2(s*.38f,0),s*.34f,3,col,18);
                    Line(c-new Vector2(s*.12f,0),c+new Vector2(s,.0f),5,col);break;
                case CombatGlyph.TimeStep:
                    Ring(c-new Vector2(s*.48f,0),s*.42f,3,col,20);Ring(c+new Vector2(s*.48f,0),s*.42f,3,col,20);
                    Line(c-new Vector2(s*.15f,0),c+new Vector2(s*.15f,0),3,col);break;
                case CombatGlyph.ChronoField:
                    Ring(c,s,3,col,24);Ring(c,s*.58f,2,col,20);
                    for(int i=0;i<8;i++){float a=i*Mathf.PI*.25f;Vector2 d=new(Mathf.Cos(a),Mathf.Sin(a));Line(c+d*s*.62f,c+d*s*.92f,2,col);}break;
                case CombatGlyph.Paradox:
                    Ring(c,s,4,col,24);Ring(c,s*.45f,3,col,20);
                    Line(c-new Vector2(s,.15f*s),c+new Vector2(s,.30f*s),3,col);
                    Line(c-new Vector2(.20f*s,s),c+new Vector2(.12f*s,s),3,col);break;
            }
        }
    }
}
