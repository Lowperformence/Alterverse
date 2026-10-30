// Controlled presentation boundary for the real MainMenuAccountStartup partial.
using System.Collections.Generic;
using Alterverse.Profile;
using UnityEngine;
namespace Alterverse.Characters
{
 public enum HeroId { First, Second, Third }
 public static class HeroIdentity {public const int HeroCount=3;}
 public static class PlayerHeroSelection
 {
  public static int Reads;
  public static HeroId Selected {get{Reads++;return (HeroId)AccountProfilePrefs.GetInt("DF_TestHero");}}
 }
}
namespace Alterverse.Match
{
 public static class ArenaVariantRuntime {public static int Selected=>AccountProfilePrefs.GetInt("DF_TestArena");}
}
namespace Alterverse.Shop
{
 public static class UniversalCosmeticSelection {public static int Migrations;public static void EnsureMigrated(Alterverse.Characters.HeroId hero){Migrations++;}}
 public static class ArenaShopCatalog {public static int IndexOf(int arena)=>arena;}
}
namespace Alterverse.UI
{
 public class MenuHeroShowcase {public int Invalidations;public void InvalidateLoadout(){Invalidations++;}}
 public class CosmeticCollectionPanel {public static int Created;public CosmeticCollectionPanel(){Created++;}public void Dispose(){}}
 public class FrontEndTheme
 {
  public object Heading,Body,Small;public static readonly List<string> Labels=new();
  public void Panel(Rect rect){}
  public void FitLabel(Rect rect,string value,object style){Labels.Add(value);}
  public void Label(Rect rect,string value,object style,bool wrap=false){Labels.Add(value);}
  public bool Action(Rect rect,string text,bool primary=false,bool enabled=true)=>false;
 }
 public sealed partial class MainMenuController
 {
  int selectedHero=-1,playArenaBrowseIndex,mapShopPageStart;float master,music,sfx,menuWidth=1500;bool audioDirty;
  CosmeticCollectionPanel collection;MenuHeroShowcase showcase=new();FrontEndTheme theme=new();
  public bool PrepareForTest()=>PrepareAccountPresentation();public void DrawStartupForTest()=>DrawAccountStartup();
  public int SelectedForTest=>selectedHero;public int ArenaForTest=>playArenaBrowseIndex;public float MasterForTest=>master;
 }
}
