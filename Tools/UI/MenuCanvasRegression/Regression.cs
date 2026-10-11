using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Alterverse.UI;
using UnityEngine;
using UnityEngine.UI;

static class Regression
{
    private static int checks;
    private static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    private static void Near(float actual,float expected,string message)=>Check(Math.Abs(actual-expected)<.001f,message+" actual="+actual+" expected="+expected);
    private static object Member(object target,string field)=>target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).GetValue(target);
    private static IList Nodes(FrontEndCanvas renderer)=>(IList)Member(renderer,"nodes");
    private static T Node<T>(FrontEndCanvas renderer,int index,string member)=>(T)Member(Nodes(renderer)[index],member);
    private static readonly Rect Card=new(100,50,200,100);

    private static void Main()
    {
        var renderer=new GameObject("menu").AddComponent<FrontEndCanvas>();
        var canvas=(Canvas)Member(renderer,"canvas");
        var group=(CanvasGroup)Member(renderer,"group");
        Check(canvas.pixelPerfect,"menu glyphs use pixel-aligned native rendering");
        Check(!canvas.enabled&&!group.blocksRaycasts&&!group.interactable,"rendering surface cannot capture gameplay/menu input");
        Event.current.type=EventType.Layout;
        renderer.Begin(0); renderer.Rounded(Card,Color.white,false,false);renderer.PushClip(Card,true);renderer.Label(Card,"Layout",new GUIStyle());renderer.PopClip();renderer.End();
        Check(Nodes(renderer).Count==0&&FrontEndCanvas.Current==null,"layout does not allocate graphics or retain rendering scope");

        foreach(float scale in new[]{.5f,1f,1.8f})
        {
            GUIUtility.Origin=new Vector2(54,25);GUIUtility.Scale=scale;Event.current.type=EventType.Repaint;
            renderer.Begin(1);
            renderer.Rounded(Card,Color.white,false,false);
            renderer.PushClip(Card,true);
            renderer.Label(new Rect(112,62,160,40),"Hero",new GUIStyle{fontSize=24});
            renderer.PopClip();
            renderer.End();
            var panel=Node<RectTransform>(renderer,0,"Rect");
            Near(panel.anchoredPosition.x,54+100*scale,"safe-area x");Near(panel.anchoredPosition.y,-25-50*scale,"safe-area y");Near(panel.sizeDelta.x,200*scale,"scaled panel width");
            Near(Node<Image>(renderer,0,"Image").pixelsPerUnitMultiplier,1/scale,"corner radius tracks layout scale");
            var text=Node<Text>(renderer,2,"Label");var textRect=text.rectTransform;
            Near(textRect.anchoredPosition.x,Mathf.RoundToInt(54+112*scale)-(54+100*scale),"nested clip x snaps to an absolute screen pixel");
            Near(textRect.anchoredPosition.y,-(Mathf.RoundToInt(25+62*scale)-(25+50*scale)),"nested clip y snaps to an absolute screen pixel");
            Check(text.fontSize==Mathf.RoundToInt(24*scale),"native text follows authored font scale");
            Check(!text.resizeTextForBestFit,"Canvas does not shrink text a second time after theme fitting");
            Check(!text.raycastTarget&&!Node<Image>(renderer,0,"Image").raycastTarget,"visual primitives do not swallow touch");
            Check(Node<Coffee.UISoftMask.SoftMask>(renderer,1,"SoftMask").enabled&&!Node<RectMask2D>(renderer,1,"HardMask").enabled,"rounded cards use addon masking");
            var first=Nodes(renderer)[0];
            for(int frame=0;frame<30;frame++)
            {
                renderer.Begin(1);renderer.Rounded(Card,Color.white,false,false);renderer.PushClip(Card,true);renderer.Label(new Rect(112,62,160,40),"Hero",new GUIStyle());renderer.PopClip();renderer.End();
                Check(ReferenceEquals(first,Nodes(renderer)[0])&&Nodes(renderer).Count==3,"stable page reuses graphics");
            }
        }

        GUIUtility.Origin=new Vector2(54.35f,25.45f);GUIUtility.Scale=.73f;
        renderer.Begin(2);renderer.Label(new Rect(112.6f,62.3f,160.7f,20.5f),"Readable detail",new GUIStyle{fontSize=12});renderer.End();
        var small=Node<Text>(renderer,0,"Label");
        Check(small.fontSize==11,"small menu detail retains an 11-screen-pixel readability floor");
        Near(small.rectTransform.anchoredPosition.x,Mathf.RoundToInt(54.35f+112.6f*.73f),"fractional layout x aligns to a screen pixel");
        Near(-small.rectTransform.anchoredPosition.y,Mathf.RoundToInt(25.45f+62.3f*.73f),"fractional layout y aligns to a screen pixel");

        GUIUtility.Origin=new Vector2(0,0);GUIUtility.Scale=1;
        var art=new Texture2D(400,200,TextureFormat.RGBA32,false);
        renderer.Begin(2);renderer.Texture(new Rect(0,0,100,100),art,ScaleMode.ScaleToFit,new Rect(0,0,1,1));renderer.Texture(new Rect(0,0,100,100),art,ScaleMode.ScaleAndCrop,new Rect(0,0,1,1));renderer.End();
        Near(Node<RectTransform>(renderer,0,"Rect").sizeDelta.y,50,"hero fit retains aspect ratio");Near(Node<RectTransform>(renderer,0,"Rect").anchoredPosition.y,-25,"hero fit is centred");
        var crop=Node<RawImage>(renderer,1,"Texture").uvRect;Near(crop.x,.25f,"artwork crop centred");Near(crop.width,.5f,"artwork crop is undistorted");
        Check(!Node<RectTransform>(renderer,2,"Rect").gameObject.activeSelf,"shorter page retires unused visuals");

        // Replacing an old clip must not destroy the pooled children that a new page will reuse.
        for(int page=0;page<30;page++)
        {
            renderer.Begin(3);renderer.PushClip(Card,false);renderer.Label(new Rect(100,-80,100,30),"Scrolled",new GUIStyle());renderer.PopClip();renderer.End();
            Check(Node<RectMask2D>(renderer,0,"HardMask").enabled,"scroll viewport uses rectangular clipping");
            renderer.Begin(4);renderer.Rounded(Card,Color.white,true,true);renderer.Label(Card,"Returned",new GUIStyle());renderer.End();
            Check(!Node<RectTransform>(renderer,1,"Rect").Destroyed,"retired mask cannot destroy reused text");
            Check(Nodes(renderer).Count==3,"page changes keep pool bounded");
        }

        renderer.Begin(0);renderer.Particles(Card,Color.white);renderer.End();
        var particles=Node<ParticleSystem>(renderer,0,"Particles");
        Check(particles.isPlaying&&particles.main.useUnscaledTime&&particles.main.maxParticles==40,"bounded particles animate in paused menu");
        Check(!particles.useAutoRandomSeed&&particles.randomSeed==47021,"menu seed is independent of gameplay randomness");
        Check(particles.velocityOverLifetime.x.mode==particles.velocityOverLifetime.y.mode&&particles.velocityOverLifetime.y.mode==particles.velocityOverLifetime.z.mode,"all velocity axes share Unity's required curve mode");
        Near(particles.transform.localPosition.x,100,"emitter centred x");Near(particles.transform.localPosition.y,-50,"emitter centred y");
        Event.current.type=EventType.MouseUp;renderer.Begin(0);renderer.Pulse(Card);renderer.End();
        Check(((Image)Member(renderer,"feedback")).enabled,"action feedback appears on input pass");
        DG.Tweening.DOTween.Tick(.30f);
        Check(!((Image)Member(renderer,"feedback")).enabled&&group.alpha==1,"unscaled tweens complete while match clock is paused");
        renderer.SetVisible(false);Check(!canvas.enabled&&!particles.isPlaying,"hiding menu stops particles and canvas");
        Check(FrontEndCanvas.Current==null,"no render scope leaks into another UI");
        Console.WriteLine("PASS "+checks+" production Canvas geometry, input isolation, clipping, pooling, paused animation and lifecycle checks. No native rendering.");
    }
}
