using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Combat
{
    /// <summary>
    /// Leaves two flat tread strips behind a moving scheduled tank. Marks are pooled and
    /// disappear after three seconds so the effect stays cheap enough for mobile matches.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TankTrackEmitter : MonoBehaviour
    {
        private const float Lifetime=3f;
        private const float EmitDistance=.62f;
        private const float TrackOffset=.70f;
        private const float RearOffset=.62f;
        private const float TrackWidth=.28f;
        private const float TrackLength=.90f;
        private const float TrackHeight=.025f;
        private const int InitialPoolSize=24;

        private sealed class TrackMark
        {
            public GameObject GameObject;
            public float ExpiresAt;
            public bool Active;
        }

        private readonly List<TrackMark> marks=new(InitialPoolSize+8);
        private Vector3 lastEmitPosition;
        private bool initialized;
        private Transform worldRoot;
        private static Material sharedTrackMaterial;

        private void Awake()
        {
            GameObject world=GameObject.Find("DF_MatchWorld");
            worldRoot=world!=null?world.transform:null;
            for(int i=0;i<InitialPoolSize;i++)marks.Add(CreateMark(false));
        }

        private void OnEnable()
        {
            lastEmitPosition=transform.position;
            initialized=true;
        }

        private void Update()
        {
            RecycleExpired();
            if(Time.timeScale<=0f)return;

            if(!initialized)
            {
                lastEmitPosition=transform.position;
                initialized=true;
                return;
            }

            Vector3 delta=transform.position-lastEmitPosition;
            delta.y=0f;
            if(delta.sqrMagnitude<EmitDistance*EmitDistance)return;

            Vector3 forward=delta.normalized;
            Vector3 right=Vector3.Cross(Vector3.up,forward).normalized;
            Vector3 center=transform.position-forward*RearOffset;
            center.y=Mathf.Max(.02f,transform.position.y-.69f);

            Emit(center-right*TrackOffset,forward);
            Emit(center+right*TrackOffset,forward);
            lastEmitPosition=transform.position;
        }

        private void OnDisable()
        {
            for(int i=0;i<marks.Count;i++)
            {
                TrackMark mark=marks[i];
                if(mark?.GameObject==null)continue;
                mark.Active=false;
                mark.GameObject.SetActive(false);
            }
        }

        private void Emit(Vector3 position,Vector3 forward)
        {
            TrackMark mark=GetAvailableMark();
            if(mark==null||mark.GameObject==null)return;

            Transform t=mark.GameObject.transform;
            t.position=position;
            t.rotation=Quaternion.LookRotation(forward,Vector3.up);
            t.localScale=new Vector3(TrackWidth,TrackHeight,TrackLength);
            mark.ExpiresAt=Time.time+Lifetime;
            mark.Active=true;
            mark.GameObject.SetActive(true);
        }

        private void RecycleExpired()
        {
            float now=Time.time;
            for(int i=0;i<marks.Count;i++)
            {
                TrackMark mark=marks[i];
                if(mark==null||!mark.Active||mark.GameObject==null||now<mark.ExpiresAt)continue;
                mark.Active=false;
                mark.GameObject.SetActive(false);
            }
        }

        private TrackMark GetAvailableMark()
        {
            for(int i=0;i<marks.Count;i++)
            {
                TrackMark mark=marks[i];
                if(mark!=null&&!mark.Active)return mark;
            }

            TrackMark extra=CreateMark(false);
            marks.Add(extra);
            return extra;
        }

        private TrackMark CreateMark(bool active)
        {
            GameObject strip=GameObject.CreatePrimitive(PrimitiveType.Cube);
            strip.name="DF_TankTrackStrip";
            if(worldRoot!=null)strip.transform.SetParent(worldRoot,true);

            Collider collider=strip.GetComponent<Collider>();
            if(collider!=null)
            {
                collider.enabled=false;
                Destroy(collider);
            }

            Renderer renderer=strip.GetComponent<Renderer>();
            if(renderer!=null)
            {
                renderer.shadowCastingMode=ShadowCastingMode.Off;
                renderer.receiveShadows=false;
                Material material=TrackMaterial();
                if(material!=null)renderer.sharedMaterial=material;
            }

            strip.SetActive(active);
            return new TrackMark{GameObject=strip,Active=active,ExpiresAt=0f};
        }

        private static Material TrackMaterial()
        {
            if(sharedTrackMaterial!=null)return sharedTrackMaterial;
            Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
            if(shader==null)return null;
            sharedTrackMaterial=new Material(shader){name="DF_TankTrackMaterial"};
            sharedTrackMaterial.color=new Color(.055f,.05f,.045f,1f);
            return sharedTrackMaterial;
        }
    }
}
