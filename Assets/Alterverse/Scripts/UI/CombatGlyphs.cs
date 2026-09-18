using UnityEngine;
namespace Alterverse.UI
{
    internal enum CombatGlyph { Strike, Shot, Area, Dash, Heal, Shield, Stun, Volley }
    // Original functional symbols, drawn without textures/material instances or extra GameObjects.
    internal static class CombatGlyphs
    {
        public static void Line(Vector2 a,Vector2 b,float width,Color color)
        {
            Vector2 d=b-a;Matrix4x4 matrix=GUI.matrix;
            try{GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg,a);FrontEndTheme.Fill(new Rect(a.x,a.y-width*.5f,d.magnitude,width),color);}
            finally{GUI.matrix=matrix;}
        }
        public static void Ring(Vector2 center,float radius,float width,Color color,int count=32)
        {
            for(int i=0;i<count;i++){float a=i*Mathf.PI*2/count,b=(i+1)*Mathf.PI*2/count;
                Line(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,width,color);}
        }
        public static void Draw(CombatGlyph glyph,Vector2 center,float size,Color color)
        {
            float s=size*.5f;Vector2 c=center;
            switch(glyph)
            {
                case CombatGlyph.Heal:
                    Line(c+Vector2.left*s,c+Vector2.right*s,5,color);Line(c+Vector2.up*s,c+Vector2.down*s,5,color);break;
                case CombatGlyph.Shield:
                    Line(c+new Vector2(-s,-s*.8f),c+new Vector2(s,-s*.8f),3,color);
                    Line(c+new Vector2(-s,-s*.8f),c+new Vector2(-s*.8f,s*.35f),3,color);
                    Line(c+new Vector2(s,-s*.8f),c+new Vector2(s*.8f,s*.35f),3,color);
                    Line(c+new Vector2(-s*.8f,s*.35f),c+new Vector2(0,s),3,color);
                    Line(c+new Vector2(s*.8f,s*.35f),c+new Vector2(0,s),3,color);break;
                case CombatGlyph.Dash:
                    for(int i=0;i<2;i++){float x=(i-.5f)*s;Line(c+new Vector2(x-s*.4f,-s),c+new Vector2(x+s*.5f,0),3,color);Line(c+new Vector2(x+s*.5f,0),c+new Vector2(x-s*.4f,s),3,color);}break;
                case CombatGlyph.Area:
                    Ring(c,s,3,color,12);Ring(c,s*.42f,2,color,12);break;
                case CombatGlyph.Stun:
                    Line(c+new Vector2(0,-s),c+new Vector2(-s*.65f,s*.15f),4,color);
                    Line(c+new Vector2(-s*.65f,s*.15f),c+new Vector2(s*.5f,-s*.15f),4,color);
                    Line(c+new Vector2(s*.5f,-s*.15f),c+new Vector2(0,s),4,color);break;
                case CombatGlyph.Volley:
                    for(int i=-1;i<=1;i++)Line(c+new Vector2(-s,i*s*.7f),c+new Vector2(s*.8f,i*s*.7f-s*.3f),3,color);break;
                case CombatGlyph.Shot:
                    Ring(c,s*.65f,3,color,16);Line(c+Vector2.left*s*1.2f,c+Vector2.right*s*1.2f,2,color);Line(c+Vector2.up*s*1.2f,c+Vector2.down*s*1.2f,2,color);break;
                default:
                    Line(c+new Vector2(-s,s),c+new Vector2(s,-s),5,color);Line(c+new Vector2(-s*.7f,0),c+new Vector2(0,s*.7f),3,color);break;
            }
        }
    }
}
