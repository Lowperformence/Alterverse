using System;
using System.Collections.Generic;
using Alterverse.Audio;
using Alterverse.Characters;
using Alterverse.Multiplayer;
using Alterverse.Visuals;
using UnityEngine;
static class TravelerAudioRegression
{
    static int checks;static void Check(bool value,string reason){checks++;if(!value)throw new Exception(reason);}
    static void Main()
    {
        Resources.AudioClipsAvailable=true;TravelerLightningVfx.NativeClips.Clear();SelectedAbilityVfx.StarsSound=null;
        var baseline=HeroCombatAudio.Load();var changed=new HashSet<int>{HeroCombatAudio.ScratchHoldCue};
        foreach(int skill in new[]{0,2})foreach(int phase in new[]{0,1,2})
        {
            TravelerLightningVfx.NativeClips[skill*3+phase]=new AudioClip{name="CompleteNativeLightning"+skill+"Phase"+phase};
            changed.Add(HeroCombatAudio.Cue((int)HeroId.Traveler,skill+1,phase));
        }
        SelectedAbilityVfx.StarsSound=new AudioClip{name="CompleteNativeStunningStars"};var native=HeroCombatAudio.Load();
        foreach(int skill in new[]{0,2})foreach(int phase in new[]{0,1,2})
        {
            int cue=HeroCombatAudio.Cue((int)HeroId.Traveler,skill+1,phase);
            Check(native[cue]?.Length==1&&ReferenceEquals(native[cue][0],TravelerLightningVfx.NativeClips[skill*3+phase]),"selected Traveler phase keeps complete native clip reference");
        }
        Check(ReferenceEquals(native[HeroCombatAudio.ScratchHoldCue][0],SelectedAbilityVfx.StarsSound),"one appended stars cue retains full source recording");
        for(int cue=0;cue<baseline.Length;cue++)
        {
            if(changed.Contains(cue))continue;
            Check((baseline[cue]?.Length??0)==(native[cue]?.Length??0),"unrelated audio variant count unchanged "+cue);
            if(baseline[cue]==null)continue;
            for(int v=0;v<baseline[cue].Length;v++)Check(baseline[cue][v]?.name==native[cue][v]?.name,"unrelated full recording unchanged "+cue);
        }
        var traveler=new GameObject("Traveler");traveler.AddComponent<HeroIdentity>().Configure(HeroId.Traveler);traveler.AddComponent<CoopHeroSlot>().IsLocalHuman=true;
        Camera.main=new GameObject("Camera").AddComponent<Camera>();int before=DimensionAudio.Plays;
        HeroCombatAudio.Windup(traveler,3);Check(DimensionAudio.Plays==before+1&&DimensionAudio.LastCue==HeroCombatAudio.Cue((int)HeroId.Traveler,3,0),"Chrono native shot sound starts with its existing visual launch cue");
        HeroCombatAudio.Cast(traveler,2);Check(DimensionAudio.Plays==before+1,"Chrono area resolution cannot replay a late second shot sound");
        HeroCombatAudio.Impact(traveler,2,new Vector3(0,0,4));Check(DimensionAudio.Plays==before+1,"confirmed victims cannot each create another full ground-impact sound");
        HeroCombatAudio.PlayLightningGroundImpact(traveler,new Vector3(0,0,4));Check(DimensionAudio.Plays==before+2&&DimensionAudio.LastCue==HeroCombatAudio.Cue((int)HeroId.Traveler,3,1),"central ground contact owns the native Chrono impact sound");
        TravelerLightningVfx.Defer=true;before=DimensionAudio.Plays;HeroCombatAudio.Impact(traveler,0,new Vector3(0,0,4));
        Check(DimensionAudio.Plays==before&&TravelerLightningVfx.Pending!=null,"Temporal Beam contact audio follows confirmed visual arrival");TravelerLightningVfx.Pending();TravelerLightningVfx.Pending=null;TravelerLightningVfx.Defer=false;
        Check(DimensionAudio.Plays==before+1&&DimensionAudio.LastCue==HeroCombatAudio.Cue((int)HeroId.Traveler,1,1),"Temporal native impact cue plays once on arrival");
        var timmy=new GameObject("Timmy");timmy.AddComponent<HeroIdentity>().Configure(HeroId.Timmy);timmy.AddComponent<CoopHeroSlot>().IsLocalHuman=true;
        before=DimensionAudio.Plays;HeroCombatAudio.PlayScratchHold(timmy,new(),0);Check(DimensionAudio.Plays==before+1&&DimensionAudio.LastCue==HeroCombatAudio.ScratchHoldCue,"confirmed Scratch starts package stars sound in its own cue");
        HeroCombatAudio.PlayScratchHold(timmy,new(),1);Check(DimensionAudio.Plays==before+1,"late hold packet cannot start a fresh full sound late");
        TravelerLightningVfx.NativeClips.Clear();Check(!HeroCombatAudio.HasImpact((int)HeroId.Traveler,3),"unprepared native chain preserves original Chrono audio routing");
        Console.WriteLine($"PASS {checks} selected Traveler and stars audio-bank/full-clip/synchronization/unchanged-other-recording checks. Controlled voice/library fixtures; native audio/device acceptance remains separate.");
    }
}
