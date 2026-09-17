using UnityEngine;
namespace Alterverse.UI
{
public static class ShopPurchaseFeedback
{
private static string message="";private static float expires;public static void Show(string text,float seconds=2.4f){message=text??"";expires=Time.unscaledTime+Mathf.Max(.5f,seconds);}public static string Current=>Time.unscaledTime<expires?message:"";public static float Pulse=>Time.unscaledTime<expires?.5f+.5f*Mathf.Sin(Time.unscaledTime*10f):0f;public static void Clear(){message="";expires=0f;}
}
}
