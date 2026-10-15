using System.Collections.Generic;
using Alterverse.Characters;
using Alterverse.Combat;
using UnityEngine;
namespace Alterverse.Visuals
{
    /// <summary>Sample original two-hand upper-body clips last, preserving shared native grounded legs.</summary>
    [DisallowMultipleComponent]
    public sealed class VoidKnightPresentation:MonoBehaviour
    {
        private Transform model;private Health health;private string action="Idle";private float started=-100,duration,contact,authoredContact;private int serial;
        private readonly Dictionary<string,AnimationClip> clips=new();private readonly List<Transform> preserved=new();private Vector3[] positions,scales;private Quaternion[] rotations;
        private static readonly HashSet<string> Upper=new(){"Spine","Chest","Head","UpperArmL","ForearmL","HandL","UpperArmR","ForearmR","HandR","WeaponGripL","WeaponGripR"};
        public void Initialize(Transform imported)
        {
            if(model!=null)return;model=imported;health=GetComponent<Health>();
            foreach(AnimationClip clip in Resources.LoadAll<AnimationClip>("AuthoredHeroes/VoidKnight"))
            {string name=clip.name;int split=name.LastIndexOf('|');if(split>=0)name=name.Substring(split+1);if(!name.StartsWith("__preview__"))clips[name]=clip;}
            foreach(Transform part in model.GetComponentsInChildren<Transform>(true))if(!Upper.Contains(part.name))preserved.Add(part);
            positions=new Vector3[preserved.Count];scales=new Vector3[preserved.Count];rotations=new Quaternion[preserved.Count];
            if(clips.Count==0)Debug.LogError("DF: Void Knight clips missing from imported FBX.",this);ApplyUpperBody();
        }
        public void BeginBasic(float windup){if(Mathf.Abs(windup-VoidKnightBalance.CounterWindup)<.001f){BeginCounter();return;}serial=serial%3+1;Begin("Attack"+serial,1f,windup,.28f);}
        public void BeginAbility(int slot,float windup)=>Begin(slot==3?"Ultimate":"Skill"+slot,slot==3?1.3f:slot==1?.8f:1f,windup,slot==3?.65f:slot==2?.18f:.28f);
        public void BeginCounter()=>Begin("Counter",.8f,VoidKnightBalance.CounterWindup,.2f);
        private void Begin(string name,float seconds,float windup,float sourceContact){action=name;started=Time.time;duration=seconds;contact=windup;authoredContact=sourceContact;}
        public void ApplyUpperBody()
        {
            if(model==null||clips.Count==0)return;
            bool stunned=GetComponent<HeroCombatStatus>()?.Stunned==true;if(stunned)started=-100;
            string name=stunned?"Hit":Time.time<started+duration?action:"Idle";if(health!=null&&health.IsDead)name="Death";
            if(!clips.TryGetValue(name,out AnimationClip clip))return;
            for(int i=0;i<preserved.Count;i++){positions[i]=preserved[i].localPosition;rotations[i]=preserved[i].localRotation;scales[i]=preserved[i].localScale;}
            float age=Mathf.Max(0,Time.time-started);
            float sample=stunned?clip.length*.45f:name=="Idle"?Time.time%Mathf.Max(.01f,clip.length):name=="Death"?clip.length:age<contact&&contact>.001f?age/contact*authoredContact:authoredContact+(age-contact)/Mathf.Max(.01f,duration-contact)*Mathf.Max(0,clip.length-authoredContact);
            clip.SampleAnimation(model.gameObject,Mathf.Clamp(sample,0,clip.length));
            for(int i=0;i<preserved.Count;i++){preserved[i].localPosition=positions[i];preserved[i].localRotation=rotations[i];preserved[i].localScale=scales[i];}
        }
    }
}
