using System.Collections.Generic;
using Alterverse.Characters;
using UnityEngine;

namespace Alterverse.Visuals
{
    public sealed partial class StylizedHeroVisual
    {
        // Broad, readable contours; all detail is opaque and batched at construction.
        private void BuildCharacterFinish(float legWidth)
        {
            bool heavy=identity.Hero==HeroId.Kaelon||identity.Hero==HeroId.Rook;
            float beltWidth=heavy?.82f:.56f;
            Detail("Belt",new Vector3(0,-.29f,.04f),new Vector3(beltWidth,.12f,.46f),cloth);
            Detail("Buckle",new Vector3(0,-.29f,.29f),new Vector3(.17f,.14f,.075f),armorLight);
            // A shared armor language makes every playable hero read as part of the same roster.
            Plate("CollarTrim",new Vector3(0,.46f,.18f),heavy?.62f:.46f,.12f,.12f,armorLight);
            Detail("ChestEnergySeal",new Vector3(0,.23f,.30f),new Vector3(.10f,.16f,.045f),teamEdge,default,null,true);
            for(int side=-1;side<=1;side+=2)
            {
                string suffix=side<0?"L":"R";
                Plate("Shin"+suffix,new Vector3(side*legWidth,-.70f,.11f),heavy?.23f:.17f,.33f,.10f,dark);
                Bind("Shin"+suffix,"Leg"+suffix);
                Plate("VambraceTrim"+suffix,new Vector3(side*(heavy?.52f:.39f),-.12f,.15f),heavy?.19f:.14f,.25f,.07f,armorLight,side*8,"Arm"+suffix);
                Detail("Toe"+suffix,new Vector3(side*legWidth,-.865f,.19f),new Vector3(.21f,.055f,.20f),armorLight);
                Bind("Toe"+suffix,"Boot"+suffix);
            }
            switch(identity.Hero)
            {
                case HeroId.Kaelon: FinishKest();break;
                case HeroId.Nyra: FinishQuell();break;
                case HeroId.Elyra: FinishVaska();break;
                case HeroId.Mira: FinishVenn();break;
                case HeroId.Rook: FinishOrun();break;
                default: FinishNull();break;
            }
        }
        private void FinishNull()
        {
            // Split hood, one high shoulder and a long cutting edge instead of a round head/body.
            Panel("Hood",new[]{new Vector2(-.28f,-.18f),new Vector2(-.31f,.18f),new Vector2(-.10f,.38f),new Vector2(.22f,.23f),new Vector2(.27f,-.12f)},.35f,new Vector3(0,.81f,-.035f),cloth);
            Plate("CheekL",new Vector3(-.17f,.76f,.25f),.14f,.30f,.075f,dark,-18);
            Plate("CheekR",new Vector3(.18f,.79f,.25f),.12f,.24f,.075f,dark,16);
            Plate("Clavicle",new Vector3(.14f,.40f,.24f),.34f,.25f,.11f,armorLight,-22);
            Plate("Pauldron",new Vector3(-.48f,.53f,.04f),.36f,.48f,.34f,dark,-30);
            Plate("ShoulderShard",new Vector3(-.59f,.66f,.02f),.17f,.44f,.14f,accent,-28);
            Plate("Abdomen",new Vector3(.01f,-.02f,.22f),.35f,.38f,.12f,dark,12);
            Detail("BladeRSpine",new Vector3(.57f,.13f,.37f),new Vector3(.035f,.63f,.03f),accent,new Vector3(0,0,-18),"BladeR",true);
            Detail("BladeGuard",new Vector3(.48f,-.16f,.34f),new Vector3(.27f,.07f,.13f),dark,default,"BladeR");
            Plate("VambraceL",new Vector3(-.43f,-.08f,.13f),.24f,.38f,.15f,armorLight,-17,"ArmL");
        }
        private void FinishKest()
        {
            Plate("HelmCrest",new Vector3(0,.97f,-.02f),.20f,.48f,.42f,armorLight);
            Plate("Gorget",new Vector3(0,.54f,.22f),.69f,.27f,.25f,dark);
            for(int side=-1;side<=1;side+=2)
            {
                string suffix=side<0?"L":"R";
                Plate("ShoulderCap"+suffix,new Vector3(side*.66f,.63f,.05f),.60f,.26f,.61f,dark,side*-9);
                Detail("ShoulderSeal"+suffix,new Vector3(side*.65f,.79f,.05f),new Vector3(.28f,.035f,.31f),accent,default,null,true);
                Plate("Tasset"+suffix,new Vector3(side*.31f,-.27f,.27f),.31f,.39f,.18f,armorLight,side*8);
            }
            Plate("ShieldRim",new Vector3(-.79f,.06f,.485f),.77f,1.19f,.045f,armorLight);
            Plate("ShieldFace",new Vector3(-.79f,.09f,.522f),.62f,1.00f,.04f,dark);
            Detail("ShieldSeal",new Vector3(-.79f,.14f,.553f),new Vector3(.12f,.53f,.027f),accent,default,null,true);
            Detail("HammerGrip",new Vector3(.54f,-.15f,.25f),new Vector3(.11f,.79f,.13f),cloth,default,"ArmR");
            Plate("AnchorHead",new Vector3(.54f,.18f,.30f),.46f,.28f,.45f,armorLight,0,"ArmR");
        }
        private void FinishQuell()
        {
            Plate("HairSweep",new Vector3(-.08f,.81f,-.19f),.39f,.62f,.16f,cloth,-16);
            Plate("CheekGuard",new Vector3(.19f,.75f,.19f),.13f,.31f,.12f,armorLight,-9);
            Plate("SightShoulder",new Vector3(.35f,.46f,.03f),.30f,.25f,.34f,armorLight);
            Plate("CapeClasp",new Vector3(-.29f,.41f,.12f),.22f,.27f,.18f,dark);
            Detail("RifleReceiver",new Vector3(.44f,.15f,.34f),new Vector3(.31f,.34f,.55f),armorLight);
            Detail("RifleMagazine",new Vector3(.43f,-.06f,.42f),new Vector3(.18f,.26f,.27f),cloth,new Vector3(-14,0,0));
            Detail("MuzzleBrake",new Vector3(.44f,.13f,1.60f),new Vector3(.16f,.15f,.24f),dark);
            Detail("ScopeLens",new Vector3(.44f,.38f,.674f),new Vector3(.075f,.075f,.025f),accent,default,null,true);
            for(int side=-1;side<=1;side+=2)
                Detail("BarrelRail"+side,new Vector3(.44f+side*.075f,.16f,1.12f),new Vector3(.035f,.045f,.68f),armorLight);
            Plate("HipHolster",new Vector3(-.29f,-.35f,-.05f),.22f,.40f,.22f,dark,-12);
        }
        private void FinishVaska()
        {
            Plate("CrownCenter",new Vector3(0,1.08f,.025f),.15f,.47f,.20f,dark);
            for(int side=-1;side<=1;side+=2)
            {
                string suffix=side<0?"L":"R";
                Plate("Crown"+suffix,new Vector3(side*.24f,1.03f,.01f),.16f,.49f,.16f,armorLight,side*-22);
                Plate("Stole"+suffix,new Vector3(side*.17f,-.10f,.17f),.17f,1.17f,.075f,cloth,side*10);
                Plate("Collar"+suffix,new Vector3(side*.23f,.60f,-.12f),.28f,.43f,.28f,dark,side*-24);
                Detail("OrbitLens"+suffix,new Vector3(side*.72f,side<0?.46f:.28f,.115f),new Vector3(.105f,.18f,.035f),accent,new Vector3(0,0,45),"Orbit"+suffix,true);
            }
            Plate("Bodice",new Vector3(0,.22f,.20f),.39f,.39f,.11f,dark);
            Detail("FocusGem",new Vector3(0,.36f,.29f),new Vector3(.13f,.18f,.035f),accent,new Vector3(0,0,45),null,true);
        }
        private void FinishVenn()
        {
            Plate("Headdress",new Vector3(0,.99f,-.02f),.48f,.26f,.42f,armorLight);
            Plate("Breastplate",new Vector3(0,.20f,.20f),.49f,.41f,.15f,dark);
            for(int side=-1;side<=1;side+=2)
            {
                string suffix=side<0?"L":"R";
                Plate("ShoulderPetal"+suffix,new Vector3(side*.37f,.48f,-.03f),.26f,.30f,.36f,armorLight,side*-20);
                Plate("CoatInset"+suffix,new Vector3(side*.24f,-.34f,-.108f),.21f,.66f,.026f,cloth,side*-5);
                Detail("ProjectorCap"+suffix,new Vector3(side*.55f,.88f,-.21f),new Vector3(.22f,.15f,.14f),armorLight,default,"Wing"+suffix);
                // The projector light must follow its moving housing too.
                Bind("Projector"+suffix,"Wing"+suffix);
            }
            Plate("EmitterRim",new Vector3(.82f,.88f,.21f),.44f,.40f,.13f,armorLight);
            Plate("EmitterInset",new Vector3(.82f,.88f,.29f),.29f,.27f,.045f,dark);
            Detail("EmitterLens",new Vector3(.82f,.89f,.324f),new Vector3(.12f,.13f,.023f),accent,default,null,true);
            Detail("StaffGrip",new Vector3(.71f,.02f,.19f),new Vector3(.13f,.28f,.13f),cloth,new Vector3(0,0,-8));
        }
        private void FinishOrun()
        {
            Plate("HelmRoof",new Vector3(-.04f,.95f,.0f),.51f,.25f,.46f,dark,-9);
            Detail("EyeSlit",new Vector3(-.04f,.79f,.319f),new Vector3(.30f,.038f,.024f),accent,new Vector3(0,0,-9),null,true);
            Plate("LeftPauldron",new Vector3(-.59f,.47f,-.02f),.48f,.51f,.58f,armorLight,22);
            Plate("RightPauldron",new Vector3(.52f,.42f,-.04f),.39f,.36f,.46f,dark,-20);
            Plate("BreakerCarapace",new Vector3(.72f,.13f,.33f),.80f,.38f,.75f,dark,-12,"GauntletR");
            for(int i=0;i<3;i++)
            {
                Detail("RamPiston"+i,new Vector3(.48f+i*.22f,.20f,.40f),new Vector3(.12f,.13f,.51f),armorLight,default,"GauntletR");
                Detail("ArmorScoring"+i,new Vector3(-.21f+i*.16f,.18f,.395f),new Vector3(.048f,.25f,.025f),cloth,new Vector3(0,0,-27));
            }
            Plate("HipGuard",new Vector3(-.30f,-.32f,.20f),.36f,.43f,.21f,dark,15);
            Detail("ExhaustGuard",new Vector3(-.42f,.71f,-.25f),new Vector3(.24f,.22f,.25f),dark,new Vector3(0,0,18));
        }
        private void Plate(string name,Vector3 position,float width,float height,float depth,Color color,float angle=0,string pivot=null)
        {
            Panel(name,new[]{new Vector2(-width*.5f,height*.28f),new Vector2(-width*.27f,height*.5f),new Vector2(width*.34f,height*.42f),new Vector2(width*.5f,-height*.20f),new Vector2(0,-height*.5f),new Vector2(-width*.44f,-height*.27f)},depth,position,color);
            root.Find("DF_"+name).localRotation=Quaternion.Euler(0,0,angle);
            if(pivot!=null)Bind(name,pivot);
        }
        private void Detail(string name,Vector3 position,Vector3 scale,Color color,Vector3 rotation=default,string pivot=null,bool energy=false)
        {
            var part=Create(name,PrimitiveType.Cube,position,scale,color,rotation);
            if(energy){HeroMeshLibrary.Paint(part.GetComponent<MeshFilter>().sharedMesh,FinishedColor(color,true),true);part.GetComponent<MeshRenderer>().sharedMaterial=MaterialFor(color,true);}
            if(pivot!=null)Bind(name,pivot);
        }
        private static Mesh BuildCuirass()
        {
            // Elliptical, chamfered rib cage: broad clavicle, narrow waist, shallow abdomen.
            float[] y={-.90f,-.39f,.38f,.77f,1f};
            float[] width={.30f,.38f,.51f,.47f,.24f};
            float[] depth={.32f,.44f,.47f,.39f,.26f};
            var vertices=new List<Vector3>();var triangles=new List<int>();
            Vector2[] contour={new(-.70f,-1),new(.70f,-1),new(1,-.45f),new(1,.45f),new(.70f,1),new(-.70f,1),new(-1,.45f),new(-1,-.45f)};
            Vector3 Point(int ring,int side)=>new Vector3(contour[side].x*width[ring],y[ring],contour[side].y*depth[ring]);
            for(int ring=0;ring<y.Length-1;ring++)for(int side=0;side<8;side++)
                OutwardFace(vertices,triangles,new[]{Point(ring,side),Point(ring,(side+1)%8),Point(ring+1,(side+1)%8),Point(ring+1,side)});
            foreach(int ring in new[]{0,y.Length-1})
            {var cap=new Vector3[8];for(int i=0;i<8;i++)cap[i]=Point(ring,i);OutwardFace(vertices,triangles,cap);}
            var mesh=new Mesh{name="DF_SculptedCuirass"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        private void AlignVeyrSwordAssemblies()
        {
            if(identity==null||identity.Hero!=HeroId.Veyr)return;
            for(int side=-1;side<=1;side+=2)
            {
                string suffix=side<0?"L":"R";
                Transform weapon=FindVisual("Weapon"+suffix);
                if(weapon==null)continue;

                // These pieces were authored in hero-root space and then reparented with worldPositionStays.
                // Reassert their intended weapon-local transforms so grip, guard and blade form one rigid sword.
                Transform hilt=weapon.Find("DF_Hilt"+suffix);
                if(hilt!=null)
                {
                    hilt.localPosition=new Vector3(0,-.015f,.015f);
                    hilt.localRotation=Quaternion.identity;
                }
                Transform guard=weapon.Find("DF_Guard"+suffix);
                if(guard!=null)
                {
                    guard.localPosition=new Vector3(0,-.115f,.060f);
                    guard.localRotation=Quaternion.Euler(0,0,side*8f);
                }
                Transform pommel=weapon.Find("DF_Pommel"+suffix);
                if(pommel!=null)
                {
                    pommel.localPosition=new Vector3(0,.13f,0);
                    pommel.localRotation=Quaternion.Euler(0,0,45f);
                }

                // Weapon skins use different blade roots; keep their handles physically touching those roots too.
                Transform eclipseGrip=weapon.Find("DF_SignatureEclipseGrip"+suffix);
                if(eclipseGrip!=null)eclipseGrip.localPosition=new Vector3(0,0,.015f);

                Transform duskglassGrip=weapon.Find("DF_SignatureDuskglassGrip"+suffix);
                if(duskglassGrip!=null)
                {
                    duskglassGrip.localPosition=new Vector3(0,-.02f,.015f);
                    duskglassGrip.localScale=new Vector3(.085f,.32f,.09f);
                }
            }
        }
        private void CombineMovingPanels()
        {
            AlignVeyrSwordAssemblies();
            // Batch within each joint, never across a separately animated weapon pivot.
            Transform[] all=root.GetComponentsInChildren<Transform>(true);
            foreach(Transform pivot in all)
            {
                if(pivot==root||!MovingPivot(pivot.name))continue;
                // Rig.Build runs after this art pass. Preserve the original limb filters,
                // renderers and part boundaries for knee splitting and elbow/wrist parenting.
                // Only rigid weapons and orbit props are safe to batch before rig construction.
                if(pivot.name!="DF_WeaponL"&&pivot.name!="DF_WeaponR"&&
                   pivot.name!="DF_OrbitL"&&pivot.name!="DF_OrbitR")continue;
                var groups=new Dictionary<Material,List<CombineInstance>>();
                foreach(var renderer in pivot.GetComponentsInChildren<MeshRenderer>())
                {
                    if(!renderer.enabled)continue;
                    Transform ancestor=renderer.transform;
                    while(ancestor!=pivot&&!MovingPivot(ancestor.name))ancestor=ancestor.parent;
                    if(ancestor!=pivot)continue;
                    var filter=renderer.GetComponent<MeshFilter>();if(filter==null||renderer.sharedMaterial==null)continue;
                    if(!groups.TryGetValue(renderer.sharedMaterial,out var list))groups.Add(renderer.sharedMaterial,list=new List<CombineInstance>());
                    list.Add(new CombineInstance{mesh=filter.sharedMesh,transform=pivot.worldToLocalMatrix*filter.transform.localToWorldMatrix});renderer.enabled=false;Destroy(renderer);Destroy(filter);
                }
                foreach(var group in groups)
                {
                    var mesh=new Mesh{name="DF_MovingArmorBatch"};mesh.CombineMeshes(group.Value.ToArray(),true,true);mesh.RecalculateBounds();detailMeshes.Add(mesh);
                    var go=new GameObject("DF_MovingSurface");go.transform.SetParent(pivot,false);
                    go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=group.Key;
                }
            }
        }
        private static bool MovingPivot(string n)=>n=="DF_ArmL"||n=="DF_ArmR"||n=="DF_LegL"||n=="DF_LegR"||n=="DF_BootL"||n=="DF_BootR"||n=="DF_WeaponL"||n=="DF_WeaponR"||n=="DF_OrbitL"||n=="DF_OrbitR";
    }
}
