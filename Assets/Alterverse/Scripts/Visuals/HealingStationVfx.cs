using UnityEngine;

namespace Alterverse.Visuals
{
    // Visuals only. Availability and radius are supplied by the existing station.
    public sealed class HealingStationVfx : MonoBehaviour
    {
        private string effect;
        private float radius,nextPulse;
        private bool available;
        public void Initialize(string key,float actualRadius)
        {effect=key;radius=Mathf.Max(.01f,actualRadius);nextPulse=0;}
        public void SetAvailable(bool value)
        {
            if(available==value)return;
            available=value;nextPulse=0;
            if(!value)HeroVfxPool.CancelShape(gameObject,HeroFxShape.Plus);
        }
        public void Pickup()
        {
            SetAvailable(false);
            Vector3 p=transform.position+Vector3.up*.18f;
            HeroVfxPool.Emit(HeroFxShape.ContactStar,p,p,Vector3.one,new Color(1,1,1,.8f),.55f,
                HeroFxMotion.Hold,owner:gameObject,epicKey:"HealPickup",effectRadius:radius);
        }
        private void Update()
        {
            if(!available||effect==null||Time.timeScale<=0||Time.time<nextPulse)return;
            nextPulse=Time.time+.9f;
            Vector3 p=transform.position+Vector3.up*.12f;
            HeroVfxPool.Emit(HeroFxShape.Plus,p,p,Vector3.one,new Color(1,1,1,effect=="HealField"?.28f:.48f),.9f,
                HeroFxMotion.Sustained,owner:gameObject,epicKey:effect,effectRadius:radius);
        }
        private void OnDisable(){HeroVfxPool.CancelShape(gameObject,HeroFxShape.Plus);HeroVfxPool.CancelShape(gameObject,HeroFxShape.ContactStar);}
    }
}
