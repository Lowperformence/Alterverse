namespace Alterverse.Content
{
public readonly struct ObjectiveLore{public readonly string Name,Subtitle,Lore,RewardFantasy;public ObjectiveLore(string name,string subtitle,string lore,string rewardFantasy){Name=name;Subtitle=subtitle;Lore=lore;RewardFantasy=rewardFantasy;}}
public static class ObjectiveLoreContent
{
public static readonly ObjectiveLore Flux=new ObjectiveLore("FLUX","UNSTABLE DIMENSION PLANET","A compressed reality fragment that cannot hold one physical law for long.","Capture it to accelerate movement, ability tempo and a fast Front push.");public static readonly ObjectiveLore Guard=new ObjectiveLore("GUARD","FORTIFIED DIMENSION PLANET","A dense world-fragment whose surviving shell resists the collision between realities.","Capture it to endure retaliation, recover and force a safer Front push.");
}
}
