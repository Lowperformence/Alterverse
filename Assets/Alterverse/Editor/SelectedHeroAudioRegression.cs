#if UNITY_EDITOR
using System;
using Alterverse.Audio;
using UnityEditor;
using UnityEngine;
namespace Alterverse.EditorChecks
{
    public static class SelectedHeroAudioRegression
    {
        [MenuItem("DIMENSIONS FRONT/Checks/Selected hero sound recordings")]
        public static void Run()
        {
            AudioClip[][] bank=HeroCombatAudio.Load();
            int count=0;
            for(int hero=0;hero<8;hero++)for(int action=0;action<5;action++)
            {
                if(!HeroCombatAudio.UsesSelectedRecording(hero,action))continue;
                count++;
                int cast=HeroCombatAudio.Cue(hero,action,0);
                Require(bank[cast]!=null&&bank[cast].Length==1&&bank[cast][0]!=null,"Missing selected cast recording");
                Require(!HeroCombatAudio.HasImpact(hero,action)&&!HeroCombatAudio.HasWindup(hero,action),"Legacy layer still enabled");
                Require(bank[HeroCombatAudio.Cue(hero,action,1)]==null&&bank[HeroCombatAudio.Cue(hero,action,2)]==null,
                    "Selected recording will be doubled by legacy audio");
            }
            Require(count==18,"Expected 18 selected ability mappings including Nerok's Maelstrom");
            Require(bank[HeroCombatAudio.Cue(7,0,1)]!=null,"Nerok's quiet basic hit was lost");
            Debug.Log("DF: 18 selected ability recordings loaded with no duplicate preparation/impact layers; basic attack banks preserved.");
        }
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    }
}
#endif
