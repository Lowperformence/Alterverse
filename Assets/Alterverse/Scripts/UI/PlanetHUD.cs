using Alterverse.Match;using UnityEngine;
namespace Alterverse.UI
{
public sealed class PlanetHUD:MonoBehaviour
{
private PlanetBuffReceiver playerBuff;private GUIStyle notice,buffStyle;private void Update(){if(playerBuff==null){GameObject player=GameObject.Find("AV_Player");if(player!=null)playerBuff=player.GetComponent<PlanetBuffReceiver>();}}
private void OnGUI(){notice??=new GUIStyle(GUI.skin.label){fontSize=18,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};buffStyle??=new GUIStyle(GUI.skin.box){fontSize=15,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};if(Time.unscaledTime<DimensionPlanetDirector.AnnouncementUntil&&!string.IsNullOrEmpty(DimensionPlanetDirector.Announcement)){float w=Mathf.Min(520,Screen.width*.68f);GUI.Box(new Rect((Screen.width-w)*.5f,98,w,38),GUIContent.none);GUI.Label(new Rect((Screen.width-w)*.5f,98,w,38),DimensionPlanetDirector.Announcement,notice);}if(playerBuff!=null&&playerBuff.ActiveRemaining>0){string effect=playerBuff.FluxActive?"-18% COOLDOWNS  •  +8% MOVE SPEED":"-18% DAMAGE TAKEN";GUI.Box(new Rect(18,102,230,50),$"{playerBuff.ActiveLabel}  {Mathf.CeilToInt(playerBuff.ActiveRemaining)}s\n{effect}",buffStyle);}}
}
}
