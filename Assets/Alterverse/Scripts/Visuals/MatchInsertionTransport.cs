using System.Collections.Generic;
using Alterverse.Audio;
using Alterverse.CameraSystem;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Match;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>One presentation per match world. Follows the existing server-owned opening phases.</summary>
    [DefaultExecutionOrder(1200)]
    public sealed class MatchInsertionTransport : MonoBehaviour
    {
        private sealed class Cargo
        {
            public InsertionTransportModel model;
            public Vector3 spawn;
            public Quaternion heading;
            public bool present;
        }
        private struct HiddenRenderer
        {
            public Renderer renderer;
            public bool wasHidden;
        }
        private readonly Cargo[] craft = new Cargo[2];
        private readonly List<HiddenRenderer> passengers = new();
        private MatchOpening opening;
        private InsertionTransportAudio sound;
        private MobaCamera framing;
        private bool ready, released, finished;
        private float progress;

        public void Prepare(MatchOpening controller)
        {
            opening=controller;
            framing=UnityEngine.Camera.main?.GetComponent<MobaCamera>();
            framing?.SetInsertionFraming(1f);
            sound=gameObject.AddComponent<InsertionTransportAudio>();sound.Prepare();
            for(int team=0;team<2;team++)
            {
                var ship=new GameObject(team==0?"ATLAS / Zero insertion":"ATLAS / Lumen insertion");
                ship.transform.SetParent(transform,false);
                var model=ship.AddComponent<InsertionTransportModel>();model.Build(team);
                ship.SetActive(false);
                craft[team]=new Cargo{model=model,heading=Quaternion.Euler(0,team==0?90f:-90f,0)};
            }
        }

        public static void PrepareForReveal(GameObject world)
        {
            var insertion=world.GetComponentInChildren<MatchInsertionTransport>();
            if(insertion!=null)insertion.CaptureSquads();
        }

        private void CaptureSquads()
        {
            if(ready||finished)return;
            // Offline bots build in Start. Capture only after all startup callbacks have run.
            var heroes=FindObjectsByType<HeroIdentity>(FindObjectsSortMode.None);
            var counts=new int[2];
            foreach(var hero in heroes)
            {
                if(!hero.transform.IsChildOf(transform.parent))continue;
                var team=hero.GetComponent<TeamMember>();
                if(team==null)continue;
                int index=Mathf.Clamp(team.Team,0,1);
                craft[index].spawn+=hero.transform.position;counts[index]++;
                foreach(var renderer in hero.GetComponentsInChildren<Renderer>(true))
                {
                    passengers.Add(new HiddenRenderer{renderer=renderer,wasHidden=renderer.forceRenderingOff});
                    renderer.forceRenderingOff=true;
                }
            }
            for(int team=0;team<2;team++)
            {
                if(counts[team]==0)continue;
                Cargo c=craft[team];c.spawn/=counts[team];
                c.present=true;c.model.gameObject.SetActive(true);
            }
            ready=true;
            // A client joining after GO must not replay an insertion or hide live players.
            if(opening==null||!opening.OpeningActive){Finish();return;}
            UpdatePose();
        }

        /// <summary>Stable across variable frame rates and replicated countdown phase corrections.</summary>
        public static float PhaseProgress(int phase,float elapsed,float duration)
        {
            float t=Mathf.Clamp01(elapsed/Mathf.Max(.01f,duration));
            return phase switch {4=>t,3=>1f+t,2=>2f+t,1=>3f+t,0=>4f+t,_=>0f};
        }

        private void LateUpdate()
        {
            if(!ready||finished)return;
            if(opening==null||!opening.OpeningActive){Finish();return;}
            if(MatchClock.InBackground){sound.Advance(progress);return;}
            UpdatePose();
        }

        private void UpdatePose()
        {
            // Never rewind an already opened bay when a network correction arrives.
            progress=Mathf.Max(progress,PhaseProgress(opening.PhaseCode,opening.PhaseElapsed,opening.PhaseDuration));
            if(opening.PhaseCode>=0)sound.Advance(progress);
            if(progress>=2f)ReleasePassengers();
            framing?.SetInsertionFraming(1f-Mathf.SmoothStep(0f,1f,Mathf.Clamp01(progress-4f)));
            if(progress>=4f)
            {
                foreach(var c in craft)if(c?.model!=null)c.model.gameObject.SetActive(false);
                if(progress>=5f)Finish();
                return;
            }
            for(int team=0;team<2;team++)
            {
                Cargo c=craft[team];if(!c.present)continue;
                float arrive=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(progress));
                float uncover=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(progress-2f));
                float depart=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(progress-3f));
                Vector3 hover=new Vector3(0,1.8f,0);
                Vector3 approach=c.heading*new Vector3(0,9f,-16f);
                Vector3 release=c.heading*new Vector3(0,2.3f,2.8f);
                Vector3 departure=c.heading*new Vector3(0,13f,22f);
                // Keep the spawn hidden under the solid hull, then reveal it naturally as the ship clears it.
                Vector3 offset=Vector3.Lerp(approach,hover,arrive)+release*uncover+departure*depart;
                c.model.transform.position=c.spawn+offset;
                c.model.transform.rotation=c.heading*Quaternion.Euler(Mathf.Lerp(-9f,0,arrive)-12f*depart,0,0);
                float doors=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(progress-1f));
                c.model.Pose(doors,progress<1f?.95f:progress<3f?.68f:1f);
            }
        }

        private void ReleasePassengers()
        {
            if(released)return;released=true;
            foreach(var entry in passengers)
                if(entry.renderer!=null)entry.renderer.forceRenderingOff=entry.wasHidden;
            passengers.Clear();
        }

        private void Finish()
        {
            if(finished)return;finished=true;
            sound?.Stop();
            framing?.SetInsertionFraming(0f);
            ReleasePassengers();
            foreach(var c in craft)if(c?.model!=null)c.model.gameObject.SetActive(false);
            // Retain the small prepared model until world teardown: no resource destruction at GO.
            enabled=false;
        }
        private void OnDisable(){sound?.Stop();framing?.SetInsertionFraming(0f);ReleasePassengers();}
        private void OnDestroy(){ReleasePassengers();}
    }
}
