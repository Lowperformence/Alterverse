using System;
using System.Collections.Generic;
using Alterverse.Audio;
using Alterverse.Characters;
using Alterverse.Multiplayer;
using Alterverse.Visuals;
using UnityEngine;

static class NovaAudioRegression
{
    static int checks;
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static void Main()
    {
        Resources.AudioClipsAvailable=true;
        var original=HeroCombatAudio.Load();
        Check(!HeroCombatAudio.HasImpact((int)HeroId.Nyra,3),"original selected Snare recording has no extra impact layer");
        foreach(int slot in new[]{2,3})foreach(int phase in new[]{0,1,2})
            SivraNovaVfx.NativeClips[slot*3+phase]=new AudioClip{name="Epic Toon FX/Nova"+(slot==2?"Small":"")+"/Phase"+phase};
        var native=HeroCombatAudio.Load();var changed=new HashSet<int>();
        foreach(int slot in new[]{2,3})foreach(int phase in new[]{0,1,2})
        {
            int cue=HeroCombatAudio.Cue((int)HeroId.Nyra,slot+1,phase);changed.Add(cue);
            Check(native[cue]!=null&&native[cue].Length==1,"one native clip bank per selected action/phase");
            Check(ReferenceEquals(native[cue][0],SivraNovaVfx.NativeClips[slot*3+phase]),"full imported AudioClip reference is used directly");
        }
        Check(HeroCombatAudio.HasImpact((int)HeroId.Nyra,3),"native Snare contact sound is enabled");
        Check(HeroCombatAudio.HasWindup((int)HeroId.Nyra,3),"native Snare charge sound is enabled when present");
        for(int cue=0;cue<original.Length;cue++)
        {
            if(changed.Contains(cue))continue;
            Check((original[cue]?.Length??0)==(native[cue]?.Length??0),"other cue variant count unchanged: "+cue);
            if(original[cue]==null)continue;
            for(int v=0;v<original[cue].Length;v++)Check(original[cue][v]?.name==native[cue][v]?.name,"other recording unchanged: "+cue+"/"+v);
        }
        var actor=new GameObject("Sivra");actor.AddComponent<HeroIdentity>().Configure(HeroId.Nyra);actor.AddComponent<CoopHeroSlot>();
        Camera.main=new GameObject("Camera").AddComponent<Camera>();
        int before=DimensionAudio.Plays;
        for(int shot=0;shot<3;shot++)HeroCombatAudio.Cast(actor,3);
        Check(DimensionAudio.Plays==before+3,"three native ultimate release calls reach the audio system");
        Check(DimensionAudio.LastCooldown<.18f,"native ultimate shot sound does not retain the one-second selected-voice throttle");
        SivraNovaVfx.Defer=true;before=DimensionAudio.Plays;HeroCombatAudio.Impact(actor,2,new(3,0,0));
        Check(DimensionAudio.Plays==before&&SivraNovaVfx.Pending!=null,"impact audio waits for the confirmed visual flight");
        SivraNovaVfx.Pending();SivraNovaVfx.Pending=null;
        Check(DimensionAudio.Plays==before+1&&DimensionAudio.LastCue==HeroCombatAudio.Cue((int)HeroId.Nyra,3,1),"arrival plays the native Snare impact cue");
        SivraNovaVfx.NativeClips.Remove(2*3+2);Check(!HeroCombatAudio.HasWindup((int)HeroId.Nyra,3),"absent native windup adds no old preparation layer");
        SivraNovaVfx.NativeClips.Clear();
        Check(!HeroCombatAudio.HasImpact((int)HeroId.Nyra,3),"missing native import retains the original routing");
        Console.WriteLine($"PASS {checks} native Nova audio-bank/routing/timing and unchanged-other-recording contracts. Controlled audio/library fixtures; actual sound playback remains a Unity/device review.");
    }
}
