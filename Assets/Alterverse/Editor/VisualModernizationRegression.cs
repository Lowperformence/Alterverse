#if UNITY_EDITOR
using System;
using Alterverse.Audio;
using Alterverse.Performance;
using UnityEditor;
using UnityEngine;

namespace Alterverse.EditorChecks
{
    public static class VisualModernizationRegression
    {
        [MenuItem("DIMENSIONS FRONT/Checks/Visual modernization materials and budgets")]
        public static void Run()
        {
            Shader energy=Resources.Load<Shader>("CombatVfx/HeroEnergy");
            Shader scenery=Resources.Load<Shader>("DF_ScenerySurface");
            Require(energy!=null&&energy.isSupported&&!ShaderUtil.ShaderHasError(energy),"HeroEnergy unavailable or shader errors");
            Require(scenery!=null&&scenery.isSupported&&!ShaderUtil.ShaderHasError(scenery),"ScenerySurface unavailable or shader errors");
            Material material=new Material(energy);
            Material leaf=new Material(scenery);
            try
            {
                Require(material.HasProperty("_Life")&&material.HasProperty("_DetailQuality")&&material.HasProperty("_SurfaceStyle"),"energy parameters missing");
                Require(leaf.HasProperty("_Transmission")&&leaf.GetFloat("_Transmission")==0,"non-leaf backlight default changed");
            }
            finally { UnityEngine.Object.DestroyImmediate(material);UnityEngine.Object.DestroyImmediate(leaf); }
            foreach(int target in new[]{30,60,120})
            {
                var budget=new VfxQualityHysteresis();
                for(int i=0;i<100;i++){budget.Observe(target*.80f,1,target);budget.Observe(target*.84f,1,target);Require(!budget.UnderBudget,"quality flapping");}
                budget.Observe(target*.7f,1,target);budget.Observe(target*.7f,1,target);Require(budget.UnderBudget,"overload hysteresis");
                for(int i=0;i<4;i++)budget.Observe(target,1,target);
                Require(!budget.UnderBudget,"recovery hysteresis");
            }
            float one=AudioDuckEnvelope.Advance(1,true,.2f),many=1;
            for(int i=0;i<120;i++)many=AudioDuckEnvelope.Advance(many,true,.2f/120);
            Require(Mathf.Abs(one-many)<.00001f,"audio ducking depends on frame rate");
            Debug.Log("DF: modernization shaders, material properties, budgets and audio envelope passed. Run real combat/device visual and GPU checks separately.");
        }
        private static void Require(bool valid,string message)
        {if(!valid)throw new InvalidOperationException("DF modernization check: "+message);}
    }
}
#endif
