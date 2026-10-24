using System.Collections.Generic;
using Alterverse.Characters;
using UnityEngine;

namespace Alterverse.Visuals
{
    public sealed partial class HeroVfxPool
    {
        private EpicVfxLibrary epicLibrary;
        private static int nerokOriginalDepth;
        internal readonly struct NerokOriginalScope : System.IDisposable
        {
            public void Dispose(){nerokOriginalDepth=System.Math.Max(0,nerokOriginalDepth-1);}
        }
        internal static NerokOriginalScope PreserveNerokOriginal(){nerokOriginalDepth++;return new NerokOriginalScope();}
        private readonly Dictionary<string,Stack<EpicVisual>> epicFree = new();
        private int epicInstances;
        private readonly HashSet<string> epicEmptyWarnings=new();
        private const int EpicInstanceLimit = Capacity - WarningReserve;
        private sealed class EpicVisual
        {
            public string Key;
            public GameObject Root;
            public ParticleSystem[] Systems;
            public Renderer[] Renderers;
            public bool[] RendererEnabled;
            public bool CanRender;
            public MaterialPropertyBlock Tint = new();
            public float NativeSeconds, Scale, LastAge;
            public bool Started, Sustained;
        }
        private bool TryRecoverEpicSurface()
        {
            epicLibrary=Resources.Load<EpicVfxLibrary>(EpicVfxLibrary.ResourcePath);
            Shader shader=epicLibrary!=null&&epicLibrary.beamMaterial!=null?epicLibrary.beamMaterial.shader:null;
            if(shader==null||!shader.isSupported||shader.name!="DimensionsFront/EpicParticles")return false;
            material=new Material(shader){name="DF_EpicWarningSurface"};ownsMaterial=true;
            material.SetTexture("_MainTex",Texture2D.whiteTexture);
            material.SetColor(Tint,Color.white);material.SetFloat(Shader.PropertyToID("_DstBlend"),1f);
            Debug.LogWarning("DF: legacy HeroEnergy unavailable; Epic VFX remain active and warning geometry uses a compatible surface.");
            return true;
        }
        private void LoadEpicLibrary()
        {
            epicLibrary = Resources.Load<EpicVfxLibrary>(EpicVfxLibrary.ResourcePath);
            if(epicLibrary == null)
                Debug.LogError("DF: Epic Toon FX runtime library missing. Run DIMENSIONS FRONT > VFX > Build Epic Toon replacement before Play/build.");
        }
        private static bool PhysicalShape(HeroFxShape shape) => shape == HeroFxShape.AnchorProjectile ||
            shape == HeroFxShape.AnchorChain || shape == HeroFxShape.TimmyGrenade ||
            shape == HeroFxShape.MonsterRock || shape == HeroFxShape.MonsterDiamond || shape == HeroFxShape.MonsterSlime;
        private static string EpicKey(HeroFxShape shape,HeroFxSprite sprite,GameObject owner,HeroFxMotion motion)
        {
            HeroId hero=owner!=null&&owner.TryGetComponent(out HeroIdentity identity)?identity.Hero:HeroId.Veyr;
            if(sprite!=HeroFxSprite.None) return sprite switch
            {
                HeroFxSprite.SlashHeavy => "HeavySlash", HeroFxSprite.SlashClaw or HeroFxSprite.ClawMark => "Claw",
                HeroFxSprite.Slash or HeroFxSprite.SlashLight or HeroFxSprite.SlashLunge or HeroFxSprite.SlashCross => "Slash",
                HeroFxSprite.Smoke or HeroFxSprite.Dust => "Smoke", HeroFxSprite.Flame => "Jet",
                HeroFxSprite.Rune or HeroFxSprite.ShieldGlyph => "Rune", HeroFxSprite.Portal or HeroFxSprite.Vortex => "Portal",
                HeroFxSprite.ShockRing => "Nova", HeroFxSprite.Fireball => "Fire", HeroFxSprite.Muzzle => "Muzzle",
                HeroFxSprite.Star or HeroFxSprite.SoftStar => "Heal", HeroFxSprite.Burst => "Impact", _ => "Orb"
            };
            return shape switch
            {
                HeroFxShape.Slash or HeroFxShape.ArcRibbon or HeroFxShape.EchoSickle => hero==HeroId.VoidKnight?"HeavySlash":"Slash",
                HeroFxShape.Claw => "Claw", HeroFxShape.Rift => "Portal", HeroFxShape.RiftCross => "ShadowMissile",
                HeroFxShape.Shield => "Shield", HeroFxShape.Plus => "Heal",
                HeroFxShape.Seed or HeroFxShape.Leaf => "NatureMissile",
                HeroFxShape.Petals => motion==HeroFxMotion.Hold?"NatureImpact":"NatureNova",
                HeroFxShape.Mist => "Smoke", HeroFxShape.WaterSpiral => "Vortex", HeroFxShape.WaveCrest => "Water",
                HeroFxShape.PressureWave or HeroFxShape.ShockDome or HeroFxShape.Ring => hero==HeroId.Rook?"FireNova":"Nova",
                HeroFxShape.Runes or HeroFxShape.Hex or HeroFxShape.GravityEight => "Rune",
                HeroFxShape.AnchorStrikeZero => "Punch",
                HeroFxShape.Cracks => "Impact", HeroFxShape.Burst => hero==HeroId.Rook?"FireImpact":"Impact",
                HeroFxShape.ContactStar => hero==HeroId.Veyr||hero==HeroId.VoidKnight||hero==HeroId.Timmy?"SwordImpact":
                    hero==HeroId.Kaelon?"HeavyImpact":hero==HeroId.Elyra?"MagicImpact":"Impact",
                HeroFxShape.Spark or HeroFxShape.Shard => "Spark",
                HeroFxShape.Streak => motion==HeroFxMotion.AttachedJet?"Jet":"Missile",
                HeroFxShape.Bolt => "Missile", HeroFxShape.Orb => hero==HeroId.Rook?"Fire":hero==HeroId.Nyra?"Missile":hero==HeroId.Elyra?"ArcaneMissile":"Orb", _ => "Orb"
            };
        }
        private bool AttachEpic(Slot slot,HeroFxShape shape)
        {
            slot.Epic=null;slot.EpicBeam=false;
            // Solid weapon/monster models and exact telegraphs retain their geometry.
            if(nerokOriginalDepth>0||slot.Warning||PhysicalShape(shape)||shape==HeroFxShape.Shield)return false;
            // Nerok is explicitly excluded from this migration: his complete old presentation stays.
            if(slot.Owner!=null&&slot.Owner.TryGetComponent(out HeroIdentity identity)&&identity.Hero==HeroId.Nerok)return false;
            if(epicLibrary==null)return false;
            if(shape==HeroFxShape.Laser||shape==HeroFxShape.MonsterBeam||
                (shape==HeroFxShape.Bolt&&slot.Motion==HeroFxMotion.Hold))
            {
                if(epicLibrary.beamMaterial==null||epicLibrary.beamMaterial.shader==null||!epicLibrary.beamMaterial.shader.isSupported)return false;
                slot.EpicBeam=true;
                slot.Filter.sharedMesh=EpicBeamMesh(shape==HeroFxShape.Bolt);
                slot.Renderer.sharedMaterial=epicLibrary.beamMaterial;
                return true;
            }
            string key=EpicKey(shape,slot.Sprite,slot.Owner,slot.Motion);
            var entry=epicLibrary.Find(key);
            if(entry==null||entry.prefab==null)return false;
            if(!epicFree.TryGetValue(key,out var free))epicFree.Add(key,free=new Stack<EpicVisual>());
            EpicVisual visual=null;
            if(free.Count>0)visual=free.Pop();
            else
            {
                // Recycle an idle instance of a different family before allocating beyond capacity.
                if(epicInstances>=EpicInstanceLimit)
                {
                    foreach(var cache in epicFree.Values)
                        if(cache.Count>0){Destroy(cache.Pop().Root);epicInstances--;break;}
                }
                if(epicInstances>=EpicInstanceLimit)return false;
                var root=Instantiate(entry.prefab,slot.Transform,false);root.SetActive(false);
                visual=new EpicVisual{Key=key,Root=root,Systems=root.GetComponentsInChildren<ParticleSystem>(true),
                    Renderers=root.GetComponentsInChildren<Renderer>(true),NativeSeconds=entry.nativeSeconds,Scale=entry.scale};
                visual.RendererEnabled=new bool[visual.Renderers.Length];
                for(int i=0;i<visual.Renderers.Length;i++)
                {
                    var renderer=visual.Renderers[i];visual.RendererEnabled[i]=renderer.enabled;
                    var materials=renderer.sharedMaterials;
                    if(renderer.enabled&&materials.Length>0&&materials[0]!=null&&materials[0].shader!=null&&materials[0].shader.isSupported)
                        visual.CanRender=true;
                }
                epicInstances++;
            }
            visual.Root.transform.SetParent(slot.Transform,false);
            visual.Root.transform.localPosition=Vector3.zero;
            visual.Root.transform.localRotation=Quaternion.identity;
            visual.Root.transform.localScale=Vector3.one*visual.Scale;
            visual.Started=false;visual.LastAge=0;
            visual.Sustained=IsProjectile(slot)||slot.Motion==HeroFxMotion.Sustained||slot.Motion==HeroFxMotion.AttachedJet||slot.ShieldBound;
            foreach(var system in visual.Systems)
            {
                system.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=system.main;main.loop=visual.Sustained;main.playOnAwake=false;
                main.simulationSpeed=1f;
            }
            slot.Epic=visual;
            return true;
        }
        private bool DrawEpic(Slot slot,float age,float fade)
        {
            var visual=slot.Epic;if(visual==null)return false;
            if(Time.time<slot.Start){visual.Root.SetActive(false);return false;}
            visual.Root.SetActive(true);
            // Native clocks are driven by the SAME normalized timeline as the old visual scheduler.
            // Pauses, delayed packets and short-range shots cannot create a late second impact.
            float sample=visual.Sustained?age:age/slot.Duration*visual.NativeSeconds;
            float delta=Mathf.Max(0,sample-visual.LastAge);
            bool particles=false;
            foreach(var system in visual.Systems)
            {
                // Stop/clear and playOnAwake=false leave native emission stopped. Resume it
                // explicitly before advancing the paused system; the slot remains the only clock.
                system.Play(false);
                if(!visual.Started)system.Simulate(Mathf.Max(.001f,sample),false,true,false);
                else if(delta>0)system.Simulate(delta,false,false,false);
                system.Pause(false);
                particles|=system.particleCount>0;
            }
            visual.Started=true;visual.LastAge=sample;
            Color tint=slot.Color;tint.a*=fade;
            visual.Tint.SetColor(Tint,tint);
            bool visible=particles&&visual.CanRender;
            for(int i=0;i<visual.Renderers.Length;i++)
            {
                var renderer=visual.Renderers[i];renderer.SetPropertyBlock(visual.Tint);
                renderer.enabled=visible&&visual.RendererEnabled[i];
            }
            // Keep simulating a delayed emitter while only the fallback draws. Never render
            // the fallback and native particles together, and never drop an attack silently.
            if(!visible&&age>=.08f&&epicEmptyWarnings.Add(visual.Key))
                Debug.LogWarning("DF: Epic "+visual.Key+" has no renderable particles; single fallback active. Run DIMENSIONS FRONT > VFX > Test Epic Toon emission.");
            return visible;
        }
        private void ReleaseEpic(Slot slot)
        {
            var visual=slot.Epic;if(visual==null)return;
            foreach(var system in visual.Systems)system.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
            visual.Root.SetActive(false);visual.Root.transform.SetParent(transform,false);
            epicFree[visual.Key].Push(visual);slot.Epic=null;
        }
        private Mesh epicBeam,epicTrace;
        private Mesh EpicBeamMesh(bool trace)
        {
            if(trace&&epicTrace!=null)return epicTrace;if(!trace&&epicBeam!=null)return epicBeam;
            float start=trace?-1.4f:0,end=trace?.55f:1;
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();var colors=new List<Color>();
            for(int plane=0;plane<2;plane++)
            {
                Vector3 side=plane==0?Vector3.right:Vector3.up;int n=vertices.Count;
                vertices.Add(side*-.24f+Vector3.forward*start);vertices.Add(side*.24f+Vector3.forward*start);
                vertices.Add(side*.24f+Vector3.forward*end);vertices.Add(side*-.24f+Vector3.forward*end);
                uv.Add(new Vector2(0,0));uv.Add(new Vector2(1,0));uv.Add(new Vector2(1,1));uv.Add(new Vector2(0,1));
                for(int i=0;i<4;i++)colors.Add(Color.white);
                indices.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            }
            var mesh=new Mesh{name="DF_EpicExactBeam"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetColors(colors);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();
            if(trace)epicTrace=mesh;else epicBeam=mesh;return mesh;
        }
    }
}
