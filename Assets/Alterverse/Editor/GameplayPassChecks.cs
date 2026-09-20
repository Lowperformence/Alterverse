#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.UI;
using UnityEditor;
using UnityEngine;
namespace Alterverse.EditorChecks
{
    public static class GameplayPassChecks
    {
        private static void Check(bool value,string reason)
        {if(!value)throw new InvalidOperationException("Gameplay pass: "+reason);}

        [MenuItem("DIMENSIONS FRONT/Checks/Collision inventory (Play mode)")]
        public static void Inventory()
        {
            if(!Application.isPlaying){Debug.LogWarning("Start a match for the collider inventory.");return;}
            GameObject world=GameObject.Find("DF_MatchWorld");
            if(world==null)return;
            Collider[] all=world.GetComponentsInChildren<Collider>(true);
            var active=new List<Collider>();int disabled=0,mesh=0,child=0,units=0;
            foreach(Collider c in all)
            {
                if(!c.enabled||!c.gameObject.activeInHierarchy){disabled++;continue;}
                active.Add(c);if(c is MeshCollider)mesh++;
                if(c.gameObject.layer==GameplayCollision.Units)units++;
                Health owner=c.GetComponentInParent<Health>();
                if(owner!=null&&owner.transform!=c.transform){child++;Debug.LogWarning("Child gameplay collider: "+c.name,c);}
                if(c.gameObject.layer==GameplayCollision.Decorative)Debug.LogWarning("Unexpected decorative collider: "+c.name,c);
            }
            active.Sort((a,b)=>b.bounds.size.sqrMagnitude.CompareTo(a.bounds.size.sqrMagnitude));
            Debug.Log($"Collider inventory: active={active.Count}, disabled={disabled}, mesh={mesh}, unit roots={units}, health-child colliders={child}");
            for(int i=0;i<Mathf.Min(12,active.Count);i++)
            {Collider c=active[i];Debug.Log($"Collider {c.name}: {c.GetType().Name}, size={c.bounds.size}, layer={LayerMask.LayerToName(c.gameObject.layer)}, trigger={c.isTrigger}",c);}
        }

        [MenuItem("DIMENSIONS FRONT/Checks/Joystick geometry and limited prediction")]
        public static void Geometry()
        {
            foreach(float ratio in new[]{16f/9,18f/9,19.5f/9,20f/9})
            foreach(float height in new[]{360f,540f,1080f})
            {
                var layout=new CombatHudLayout(new Rect(24,12,height*ratio-48,height-24));
                Vector2 idle=layout.JoystickIdle;
                Check(Mathf.Abs(idle.x-CombatHudLayout.JoystickRadius-90)<.01f,"idle safe margin changed");
                Check(layout.MoveCapture.Contains(idle),"idle outside capture");
                Rect ring=new Rect(idle.x-74,idle.y-74,148,148);
                Check(ring.xMin>=0&&ring.yMin>100&&ring.yMax<=layout.Height,"ring overlaps edge or hero HUD");
                for(int slot=0;slot<5;slot++)Check(!layout.MoveCapture.Overlaps(layout.Action(slot)),"move capture overlaps ability");
                Vector2 screen=new Vector2(layout.Safe.x+idle.x*layout.Scale,layout.Safe.yMax-idle.y*layout.Scale);
                Check((layout.Point(screen)-idle).sqrMagnitude<.001f,"safe-area mapping mismatch");
            }
            Vector3 origin=Vector3.zero,target=Vector3.forward*4;
            foreach(float delay in new[]{.30f,.32f,.38f,.40f,.45f,.46f,.50f,.55f,.68f})
            foreach(float speed in new[]{0f,2f,6f,7.1f,25f})
            foreach(float quality in new[]{.05f,.35f,.55f,.65f,.8f})
            {
                Vector3 point=HeroAimPrediction.Point(origin,target,Vector3.right*speed,delay,quality,7);
                Check((point-target).magnitude<=1.201f,"prediction displacement exceeds cap");
                Check(point.magnitude<=7.001f,"prediction exceeds cast range");
                if(speed==0)Check(point==target,"stationary aim drifted");
                Vector3 opposite=target-Vector3.right*speed*delay;
                if(speed>2)Check(Vector3.Distance(opposite,point)>Vector3.Distance(target,point),"direction reversal tracks future input");
            }
            Debug.Log("Joystick aspect/safe-area and limited prediction checks passed. Rendering, touch and hit-rate tests remain separate.");
        }

        [MenuItem("DIMENSIONS FRONT/Checks/Unit pass-through and solid wall (Play mode)")]
        public static void Collision()
        {
            if(!Application.isPlaying||Time.timeScale<=0){Debug.LogWarning("Start an unpaused match.");return;}
            GameObject root=new GameObject("DF_CollisionPolicyChecks");
            try
            {
                Check(Physics.GetIgnoreLayerCollision(GameplayCollision.Units,GameplayCollision.Units),"unit collision matrix still blocks");
                var actor=new GameObject("Player controller fixture");actor.transform.SetParent(root.transform);
                actor.transform.position=new Vector3(0,80,-3);actor.layer=GameplayCollision.Units;
                CharacterController cc=actor.AddComponent<CharacterController>();cc.height=2;cc.radius=.5f;
                actor.AddComponent<Health>();HeroLocomotion body=actor.AddComponent<HeroLocomotion>();
                GameObject unit=GameObject.CreatePrimitive(PrimitiveType.Capsule);unit.transform.SetParent(root.transform);
                unit.transform.position=new Vector3(0,80,0);unit.layer=GameplayCollision.Units;
                var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.SetParent(root.transform);
                wall.transform.position=new Vector3(0,80,3);wall.transform.localScale=new Vector3(8,4,.2f);wall.layer=GameplayCollision.Structures;
                Physics.SyncTransforms();body.Move(Vector3.forward*8,40,33,false);
                Check(actor.transform.position.z>1&&actor.transform.position.z<2.6f,"controller either blocked by unit or crossed wall");
                Collider[] hits=new Collider[4];
                Check(Physics.OverlapSphereNonAlloc(unit.transform.position,1,hits,GameplayCollision.TargetMask,QueryTriggerInteraction.Collide)>0,"unit disappeared from hit queries");
                Debug.Log("Unit pass-through, solid-wall and query hitbox checks passed.");
            }
            finally{root.SetActive(false);UnityEngine.Object.Destroy(root);}
        }
    }
}
#endif
