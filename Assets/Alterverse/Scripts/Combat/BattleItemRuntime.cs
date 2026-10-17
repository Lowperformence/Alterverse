using Alterverse.Characters;
using Alterverse.Profile;
using Alterverse.Visuals;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Combat
{
    public static class BattleItemRuntime
    {
        public static bool TryDropEquipped(BattleItemType item,int team,Vector3 position,out BattleItemPickup pickup)
        {
            pickup=null;
            if(item==BattleItemType.None||BattleItemInventory.Equipped!=item)return false;
            pickup=BattleItemPickup.Create(item,team,position,true);
            if(pickup==null)return false;
            if(BattleItemInventory.TryConsumeEquipped(item))return true;
            Object.Destroy(pickup.gameObject);pickup=null;return false;
        }

    }

    public sealed class BattleItemPickup:MonoBehaviour
    {
        private const float PickupDelay=.25f,Radius=1.35f,DescentSeconds=4f,DropHeight=8f;
        private BattleItemType type;
        private int team;
        private float spawned,nextScan;
        private Transform visual,orbit,parachute,landingMarker;
        private Vector3 landing;
        private bool airborne;
        private Mesh canopyMesh;
        private Material chuteMat;
        private Alterverse.Match.DimensionsFront match;
        private Material energyMat,darkMat,teamMat;
        private bool claimed;
        private bool authoritative=true;
        public bool IsAirborne=>airborne;

        public static bool Spawn(BattleItemType item,int ownerTeam,Vector3 position)
            =>Create(item,ownerTeam,position,false)!=null;

        public static BattleItemPickup Create(BattleItemType item,int ownerTeam,Vector3 position,bool airdrop,bool authoritative=true)
        {
            if((int)item<(int)BattleItemType.Heal||(int)item>(int)BattleItemType.TeamHeal)return null;
            GameObject go=new("DF_BattleItemDrop_"+item);
            Vector3 ground=position;ground.y=LunarSurfaceField.WorldSurface(position.x,position.z).y+.10f;
            Transform world=GameObject.Find("DF_MatchWorld")?.transform;
            if(world!=null)go.transform.SetParent(world,true);
            BattleItemPickup pickup=go.AddComponent<BattleItemPickup>();
            pickup.type=item;pickup.team=Mathf.Clamp(ownerTeam,0,1);pickup.spawned=Time.time;pickup.authoritative=authoritative;
            pickup.landing=ground;pickup.airborne=airdrop;
            pickup.match=Object.FindAnyObjectByType<Alterverse.Match.DimensionsFront>();
            go.transform.position=ground+(airdrop?Vector3.up*DropHeight:Vector3.zero);
            if(!pickup.Build()){Object.Destroy(go);return null;}
            if(airdrop)pickup.BuildParachute();
            return pickup;
        }

        private bool Build()
        {
            Color color=type==BattleItemType.Heal?new Color(.12f,1f,.34f):
                type==BattleItemType.Shield?new Color(.18f,.82f,1f):
                type==BattleItemType.Speed?new Color(1f,.72f,.18f):new Color(.28f,1f,.72f);
            Shader shader=RuntimeShaderLibrary.Lit;
            if(shader==null)return false;
            energyMat=new Material(shader){color=color};darkMat=new Material(shader){color=new Color(.10f,.12f,.16f)};
            teamMat=new Material(shader){color=team==0?StylizedArtDirection.ZeroEnergy:StylizedArtDirection.LumenEnergy};
            if(energyMat.HasProperty("_EmissionColor")){energyMat.EnableKeyword("_EMISSION");energyMat.SetColor("_EmissionColor",color*3f);}
            if(energyMat.HasProperty("_Smoothness"))energyMat.SetFloat("_Smoothness",.62f);
            if(darkMat.HasProperty("_Metallic"))darkMat.SetFloat("_Metallic",.45f);

            visual=new GameObject("DF_BattleItemVisual").transform;visual.SetParent(transform,false);visual.localPosition=Vector3.up*.72f;
            Part("Crate",PrimitiveType.Cube,visual,new Vector3(0,-.08f,0),new Vector3(.66f,.46f,.54f),darkMat);
            for(int side=-1;side<=1;side+=2)
            {
                Part("CrateBrace"+side,PrimitiveType.Cube,visual,new Vector3(side*.25f,-.08f,.286f),
                    new Vector3(.065f,.43f,.035f),energyMat);
                Part("TeamStripe"+side,PrimitiveType.Cube,visual,new Vector3(side*.34f,-.08f,0),
                    new Vector3(.025f,.12f,.42f),teamMat);
            }
            Part("Core",PrimitiveType.Sphere,visual,Vector3.zero,new Vector3(.34f,.34f,.34f),energyMat);
            Part("ShellL",PrimitiveType.Cube,visual,new Vector3(-.27f,0,0),new Vector3(.12f,.42f,.24f),darkMat).localRotation=Quaternion.Euler(18,20,12);
            Part("ShellR",PrimitiveType.Cube,visual,new Vector3(.27f,0,0),new Vector3(.12f,.42f,.24f),darkMat).localRotation=Quaternion.Euler(-18,-20,-12);

            orbit=new GameObject("DF_BattleItemOrbit").transform;orbit.SetParent(visual,false);
            for(int i=0;i<4;i++)
            {
                float a=i*Mathf.PI*.5f;
                Part("Shard",PrimitiveType.Cube,orbit,new Vector3(Mathf.Cos(a)*.52f,0,Mathf.Sin(a)*.52f),new Vector3(.08f,.22f,.08f),energyMat)
                    .localRotation=Quaternion.Euler(25,i*90,35);
            }

            if(type==BattleItemType.Heal){Part("PlusV",PrimitiveType.Cube,visual,new Vector3(0,0,.28f),new Vector3(.10f,.34f,.06f),energyMat);Part("PlusH",PrimitiveType.Cube,visual,new Vector3(0,0,.28f),new Vector3(.34f,.10f,.06f),energyMat);}
            else if(type==BattleItemType.Shield)Part("ShieldMark",PrimitiveType.Cube,visual,new Vector3(0,0,.28f),new Vector3(.32f,.30f,.06f),energyMat).localRotation=Quaternion.Euler(0,0,45);
            else if(type==BattleItemType.Speed) {Part("SpeedMark",PrimitiveType.Cube,visual,new Vector3(0,0,.28f),new Vector3(.38f,.08f,.06f),energyMat).localRotation=Quaternion.Euler(0,0,-18);Part("SpeedMark2",PrimitiveType.Cube,visual,new Vector3(-.08f,.13f,.28f),new Vector3(.28f,.07f,.06f),energyMat).localRotation=Quaternion.Euler(0,0,-18);}
            else if(type==BattleItemType.TeamHeal)
            {
                Part("TeamHealPlusV",PrimitiveType.Cube,visual,new Vector3(0,0,.30f),new Vector3(.09f,.34f,.06f),energyMat);
                Part("TeamHealPlusH",PrimitiveType.Cube,visual,new Vector3(0,0,.30f),new Vector3(.34f,.09f,.06f),energyMat);
                Part("TeamHealAllyL",PrimitiveType.Sphere,visual,new Vector3(-.28f,.18f,.29f),new Vector3(.10f,.10f,.055f),energyMat);
                Part("TeamHealAllyR",PrimitiveType.Sphere,visual,new Vector3(.28f,.18f,.29f),new Vector3(.10f,.10f,.055f),energyMat);
            }
            return true;
        }

        private void BuildParachute()
        {
            Shader shader=RuntimeShaderLibrary.Lit;
            chuteMat=new Material(shader){name="DF_TeamSupplyCanopy",color=team==0?new Color(.48f,.19f,.78f):new Color(.12f,.65f,.78f)};
            if(chuteMat.HasProperty("_BaseColor"))chuteMat.SetColor("_BaseColor",chuteMat.color);
            if(chuteMat.HasProperty("_Smoothness"))chuteMat.SetFloat("_Smoothness",.25f);
            if(chuteMat.HasProperty("_Cull"))chuteMat.SetFloat("_Cull",0f);
            parachute=new GameObject("DF_SupplyParachute").transform;parachute.SetParent(transform,false);
            const int sides=16,rings=5;
            var vertices=new System.Collections.Generic.List<Vector3>();
            var triangles=new System.Collections.Generic.List<int>();
            for(int ring=0;ring<rings;ring++)
            {
                float angle=ring/(float)(rings-1)*Mathf.PI*.5f;
                float radius=Mathf.Sin(angle)*1.05f,y=1.85f+Mathf.Cos(angle)*.46f;
                for(int side=0;side<sides;side++)
                {
                    float azimuth=side*Mathf.PI*2/sides;
                    vertices.Add(new Vector3(Mathf.Cos(azimuth)*radius,y,Mathf.Sin(azimuth)*radius));
                }
            }
            for(int ring=0;ring<rings-1;ring++)
                for(int side=0;side<sides;side++)
                {
                    int a=ring*sides+side,b=ring*sides+(side+1)%sides,c=a+sides,d=b+sides;
                    // Two-sided fabric is visible from above and below.
                    if(ring>0){triangles.Add(a);triangles.Add(b);triangles.Add(c);}
                    triangles.Add(b);triangles.Add(d);triangles.Add(c);
                }
            canopyMesh=new Mesh{name="DF_SmallSupplyParachute"};
            canopyMesh.SetVertices(vertices);canopyMesh.SetTriangles(triangles,0);canopyMesh.RecalculateNormals();canopyMesh.RecalculateBounds();
            GameObject canopy=new("Canopy");canopy.transform.SetParent(parachute,false);
            canopy.AddComponent<MeshFilter>().sharedMesh=canopyMesh;
            canopy.AddComponent<MeshRenderer>().sharedMaterial=chuteMat;
            for(int side=0;side<4;side++)
            {
                float angle=side*Mathf.PI*.5f+Mathf.PI*.25f;
                Vector3 top=new Vector3(Mathf.Cos(angle)*.88f,1.9f,Mathf.Sin(angle)*.88f);
                Vector3 bottom=new Vector3(Mathf.Cos(angle)*.27f,.86f,Mathf.Sin(angle)*.27f);
                Transform cord=Part("SupplyCord",PrimitiveType.Cylinder,parachute,(top+bottom)*.5f,
                    new Vector3(.012f,(top-bottom).magnitude*.5f,.012f),darkMat);
                cord.localRotation=Quaternion.FromToRotation(Vector3.up,top-bottom);
            }
            // Persistent landing marker makes the supply location visible while descending.
            landingMarker=Part("LandingMarker",PrimitiveType.Cylinder,transform,Vector3.zero,
                new Vector3(1.65f,.016f,1.65f),chuteMat);
            landingMarker.SetParent(transform.parent,true);
            landingMarker.position=landing+Vector3.up*.025f;
        }

        private Transform Part(string name,PrimitiveType primitive,Transform parent,Vector3 pos,Vector3 scale,Material mat)
        {
            GameObject go=GameObject.CreatePrimitive(primitive);go.name="DF_BattleItem"+name;go.transform.SetParent(parent,false);
            go.transform.localPosition=pos;go.transform.localScale=scale;
            if(primitive==PrimitiveType.Cube)go.GetComponent<MeshFilter>().sharedMesh=ProductionMeshFactory.BeveledBox();
            Collider c=go.GetComponent<Collider>();if(c!=null){c.enabled=false;Destroy(c);}
            Renderer r=go.GetComponent<Renderer>();if(r!=null){r.sharedMaterial=mat;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;}
            return go.transform;
        }

        private void Update()
        {
            if(Time.timeScale<=0f||claimed)return;
            if(match==null||match.HasEnded){Destroy(gameObject);return;}
            float age=Time.time-spawned;
            if(airborne)
            {
                float t=Mathf.Clamp01(age/DescentSeconds);
                transform.position=landing+Vector3.up*Mathf.Lerp(DropHeight,0,t);
                if(parachute!=null)parachute.localRotation=Quaternion.Euler(0,Mathf.Sin(age*2)*4f,Mathf.Sin(age*3)*5f);
                if(t<1)return;
                airborne=false;
                if(parachute!=null){Destroy(parachute.gameObject);parachute=null;}
                Alterverse.Audio.UiSoundRouter.Confirm();
            }
            if(visual!=null)
            {
                visual.localPosition=Vector3.up*(.72f+Mathf.Sin(Time.time*3.2f)*.06f);
                visual.localScale=Vector3.one*(1f+Mathf.Sin(Time.time*4.4f)*.05f);
                orbit?.Rotate(Vector3.up,70f*Time.deltaTime,Space.Self);
            }
            if(age<PickupDelay||Time.time<nextScan)return;
            nextScan=Time.time+.08f;
            float radiusSqr=Radius*Radius;
            for(int i=0;i<Health.Active.Count;i++)
            {
                Health h=Health.Active[i];if(h==null||h.IsDead||h.GetComponent<HeroIdentity>()==null)continue;
                TeamMember member=h.GetComponent<TeamMember>();if(member==null||member.Team!=team)continue;
                Vector3 d=h.transform.position-transform.position;d.y=0;if(d.sqrMagnitude>radiusSqr)continue;
                Apply(h);return;
            }
        }

        private void Apply(Health hero)
        {
            if(claimed)return;claimed=true;
            if(!authoritative)
            {
                PrototypeVfx.Burst(hero.transform.position,energyMat!=null?energyMat.color:Color.white,.6f,5);
                Destroy(gameObject);
                return;
            }
            switch(type)
            {
                case BattleItemType.Heal:
                    hero.Heal(hero.Max*.20f,false);
                    HeroSkillVfx.Support(hero.gameObject,hero,false,.45f);
                    break;
                case BattleItemType.Shield:
                    hero.AddShield(hero.Max*.20f,5f,false);
                    HeroSkillVfx.Support(hero.gameObject,hero,true,5f);
                    break;
                case BattleItemType.TeamHeal:
                    for(int i=0;i<Health.Active.Count;i++)
                    {
                        Health ally=Health.Active[i];
                        if(ally==null||ally.IsDead||ally.GetComponent<HeroIdentity>()==null)continue;
                        TeamMember allyMember=ally.GetComponent<TeamMember>();
                        if(allyMember==null||allyMember.Team!=team)continue;
                        ally.Heal(20f,false);
                        HeroSkillVfx.Support(ally.gameObject,ally,false,.45f);
                    }
                    break;
                case BattleItemType.Speed:
                    (hero.GetComponent<HeroCombatStatus>()??hero.gameObject.AddComponent<HeroCombatStatus>()).BoostSpeed(1.10f,5f);
                    PrototypeVfx.Ring(hero.transform.position,new Color(1f,.72f,.18f),1.1f,.3f);
                    break;
            }
            PrototypeVfx.Burst(hero.transform.position,energyMat!=null?energyMat.color:Color.white,.6f,5);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if(landingMarker!=null)Destroy(landingMarker.gameObject);
            if(canopyMesh!=null)Destroy(canopyMesh);
            if(chuteMat!=null)Destroy(chuteMat);
            if(teamMat!=null)Destroy(teamMat);
            if(energyMat!=null)Destroy(energyMat);if(darkMat!=null)Destroy(darkMat);
        }
    }

    /// <summary>
    /// One supply per equipped human-player item. Offline keeps the local single-drop path.
    /// Network matches use the verified eight-slot match plan and reserve four distinct landing
    /// positions per team, so a full team can receive four simultaneous supplies without overlap.
    /// </summary>
    public sealed class BattleItemAirdropSchedule:MonoBehaviour
    {
        public const float DropAtSeconds=120f;
        private Alterverse.Match.DimensionsFront match;

        // Offline/local-bot path.
        private BattleItemType item;
        private int team;
        private bool captured,issued;
        private BattleItemPickup drop;

        // Network path. One deterministic entry per roster slot.
        private readonly bool[] networkIssued=new bool[Alterverse.Multiplayer.MatchSession.MaxPlayers];
        private readonly BattleItemPickup[] networkDrops=new BattleItemPickup[Alterverse.Multiplayer.MatchSession.MaxPlayers];
        private float nextResolve;
        public string Status { get; private set; }

        private void Awake(){match=GetComponent<Alterverse.Match.DimensionsFront>();}

        public void ResetSchedule()
        {
            if(drop!=null)Destroy(drop.gameObject);
            drop=null;captured=issued=false;item=BattleItemType.None;Status=null;nextResolve=0;
            for(int i=0;i<networkDrops.Length;i++)
            {
                if(networkDrops[i]!=null)Destroy(networkDrops[i].gameObject);
                networkDrops[i]=null;networkIssued[i]=false;
            }
        }

        private void Update()
        {
            if(match==null||match.HasEnded||Time.timeScale<=0||MainMenuVisible())return;
            if(Alterverse.Multiplayer.CoopAuthority.Active)
            {
                UpdateNetworkSupplies();
                return;
            }
            UpdateOfflineSupply();
        }

        private void UpdateOfflineSupply()
        {
            if(!captured)
            {
                if(Time.time<nextResolve)return;nextResolve=Time.time+.25f;
                GameObject player=GameObject.Find("AV_Player");
                TeamMember member=player!=null?player.GetComponent<TeamMember>():null;
                if(member==null)return;
                item=BattleItemInventory.Equipped;team=member.Team;captured=true;
            }
            if(issued)
            {
                Status=drop==null?null:drop.IsAirborne?"TEAM SUPPLY INCOMING":"TEAM SUPPLY READY";
                return;
            }
            if(item==BattleItemType.None){Status=null;return;}
            if(match.ElapsedSeconds<DropAtSeconds)
            {
                Status="SUPPLY DROP  "+Mathf.CeilToInt(DropAtSeconds-match.ElapsedSeconds)+"s";
                return;
            }
            if(Time.time<nextResolve)return;nextResolve=Time.time+1f;
            if(!TryLandingPosition(team,out Vector3 position)){Status="SUPPLY WAITING FOR CLEAR LANDING";return;}
            if(BattleItemInventory.Equipped!=item)
            {
                issued=true;Status="SUPPLY CANCELLED: ITEM NOT EQUIPPED";return;
            }
            if(!BattleItemRuntime.TryDropEquipped(item,team,position,out drop))
            {
                Status="SUPPLY RETRYING";return;
            }
            issued=true;Status="TEAM SUPPLY DELIVERED";
        }

        private void UpdateNetworkSupplies()
        {
            Alterverse.Multiplayer.CoopMatchPlan plan=Alterverse.Multiplayer.CoopAuthority.ActivePlan;
            if(plan?.Session?.Slots==null){Status="TEAM SUPPLIES WAITING FOR ROSTER";return;}

            int configured=0,issuedCount=0,airborneCount=0;
            for(int slot=0;slot<Alterverse.Multiplayer.MatchSession.MaxPlayers;slot++)
            {
                Alterverse.Multiplayer.MatchPlayerSlot player=plan.Session.Slots[slot];
                if(player==null||player.IsBot||player.BattleItem==BattleItemType.None)continue;
                configured++;
                if(networkIssued[slot])
                {
                    issuedCount++;
                    if(networkDrops[slot]!=null&&networkDrops[slot].IsAirborne)airborneCount++;
                }
            }

            if(configured==0){Status=null;return;}
            if(match.ElapsedSeconds<DropAtSeconds)
            {
                Status=$"TEAM SUPPLIES  {Mathf.CeilToInt(DropAtSeconds-match.ElapsedSeconds)}s  •  {configured} READY";
                return;
            }
            if(Time.time<nextResolve)return;
            nextResolve=Time.time+.5f;

            bool authority=Alterverse.Multiplayer.CoopAuthority.LocalHasServerAuthority;
            for(int slot=0;slot<Alterverse.Multiplayer.MatchSession.MaxPlayers;slot++)
            {
                if(networkIssued[slot])continue;
                Alterverse.Multiplayer.MatchPlayerSlot player=plan.Session.Slots[slot];
                if(player==null||player.IsBot||player.BattleItem==BattleItemType.None)continue;

                int teamSlot=slot%Alterverse.Multiplayer.MatchSession.TeamSize;
                if(!TryLandingPosition(player.Team,teamSlot,out Vector3 position))continue;

                BattleItemPickup created=BattleItemPickup.Create(player.BattleItem,player.Team,position,true,authority);
                if(created==null)continue;

                networkDrops[slot]=created;
                networkIssued[slot]=true;
                issuedCount++;

                // Every real player pays for only their own planned supply on their own device.
                // The authoritative host/dedicated server never consumes another player's wallet.
                if(player.IsLocal)
                    BattleItemInventory.TryConsumeEquipped(player.BattleItem);
            }

            airborneCount=0;
            for(int i=0;i<networkDrops.Length;i++)
                if(networkDrops[i]!=null&&networkDrops[i].IsAirborne)airborneCount++;

            Status=issuedCount<=0?"TEAM SUPPLIES WAITING FOR CLEAR LANDING":
                airborneCount>0?$"TEAM SUPPLIES INCOMING  •  {issuedCount}/{configured}":
                $"TEAM SUPPLIES READY  •  {issuedCount}/{configured}";
        }

        private static bool MainMenuVisible()=>Alterverse.UI.MainMenuController.IsVisible;

        internal static bool TryLandingPosition(int team,out Vector3 position)
        {
            float sign=team==0?-1f:1f;
            foreach(float x in new[]{10f,8f,12f})
                foreach(float z in new[]{7f,-7f,9f,-9f})
                {
                    Vector3 p=new(sign*x,0,z);p.y=LunarSurfaceField.WorldSurface(p.x,p.z).y;
                    if(Physics.CheckSphere(p+Vector3.up*.9f,.75f,
                        GameplayCollision.SightMask,QueryTriggerInteraction.Ignore))continue;
                    position=p;return true;
                }
            position=default;return false;
        }

        internal static bool TryLandingPosition(int team,int teamSlot,out Vector3 position)
        {
            teamSlot=Mathf.Clamp(teamSlot,0,Alterverse.Multiplayer.MatchSession.TeamSize-1);
            float sign=team==0?-1f:1f;
            float baseZ=(teamSlot-(Alterverse.Multiplayer.MatchSession.TeamSize-1)*.5f)*7f;

            // Each roster slot owns its own Z lane. X and small Z fallbacks stay inside that lane,
            // preventing another team-mate's crate from selecting the same landing point.
            foreach(float x in new[]{10f,8f,12f})
                foreach(float zOffset in new[]{0f,-1.5f,1.5f})
                {
                    Vector3 p=new(sign*x,0,baseZ+zOffset);
                    p.y=LunarSurfaceField.WorldSurface(p.x,p.z).y;
                    if(Physics.CheckSphere(p+Vector3.up*.9f,.75f,
                        GameplayCollision.SightMask,QueryTriggerInteraction.Ignore))continue;
                    position=p;return true;
                }
            position=default;return false;
        }

        private void OnDisable()
        {
            if(drop!=null)Destroy(drop.gameObject);
            for(int i=0;i<networkDrops.Length;i++)if(networkDrops[i]!=null)Destroy(networkDrops[i].gameObject);
        }
    }

}

