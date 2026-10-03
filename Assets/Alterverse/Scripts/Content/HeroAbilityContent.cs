namespace Alterverse.Content
{
public readonly struct AbilitySet{public readonly string Hero,Passive,Skill1,Skill2,Ultimate;public AbilitySet(string hero,string passive,string skill1,string skill2,string ultimate){Hero=hero;Passive=passive;Skill1=skill1;Skill2=skill2;Ultimate=ultimate;}}
public static class HeroAbilityContent
{
public static readonly AbilitySet[] Sets={new AbilitySet("ZHAEV NULL","ABSENT STEP","NULL CUT","GAPFOLD","VACANT ECHO"),new AbilitySet("AUREN KEST","HARDLIGHT MASS","ANCHOR STRIKE","LUMEN WALL","KEST CITADEL"),new AbilitySet("SIVRA QUELL","CALIBRATED EDGE","PRISM SHOT","VECTOR SHIFT","SIGHTLINE BREAK"),new AbilitySet("IONE VASKA","FALSE ANGLE","ORBIT FRACTURE","INVERT FIELD","IMPOSSIBLE CENTER"),new AbilitySet("TAHLA VENN","LIVING MEMORY","RESONANT SEED","GROWTH VEIL","VENN WILDSURGE"),new AbilitySet("BRAKK ORUN","SCRAP MOMENTUM","WRECK SWING","MAGNET GRAB","ORUN OVERDRIVE"),new AbilitySet("THE TRAVELER","BETWEEN SECONDS","TEMPORAL BEAM","RIFT STEP","PARADOX COLLAPSE"),new AbilitySet("NEROK ABYSS","SALVAGE TIDE","ANCHOR RECALL","PRESSURE HULL","ABYSSAL MAELSTROM")};
}
}
