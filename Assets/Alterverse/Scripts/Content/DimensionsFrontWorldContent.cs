namespace Alterverse.Content
{
public readonly struct WorldEntry{public readonly string Name,Hook;public WorldEntry(string name,string hook){Name=name;Hook=hook;}}
public static class DimensionsFrontWorldContent
{
public static readonly WorldEntry[] Dimensions={new WorldEntry("NULVARA","Reality decays into angular absence; survivors weaponize fractures and missing space."),new WorldEntry("LYRION","A precision-built reality where light is engineered into matter and law."),new WorldEntry("PYRAVEN","A star-forged civilization living inside controlled solar storms."),new WorldEntry("ORVESSA","Gravity is negotiable; cities orbit impossible geometric anchors."),new WorldEntry("THYRVANE","A living planetary intelligence grows architecture, armor and memory."),new WorldEntry("KHELT-9","A dead industrial reality rebuilt from machines that outlived their makers.")};
public static readonly WorldEntry[] FrontEvents={new WorldEntry("FRACTURE SURGE","The Front briefly becomes unstable and visually tears across contested ground."),new WorldEntry("LYRION OVERLOAD","Light structures flare as Lyrion pressure rises."),new WorldEntry("NULVARA BLEED","Missing-space shards intrude farther into occupied reality."),new WorldEntry("COLLISION ECHO","A short audiovisual shockwave marks a major swing in reality control.")};
}
}
