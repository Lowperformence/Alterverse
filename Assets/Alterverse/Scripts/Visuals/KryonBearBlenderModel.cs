using UnityEngine;
namespace Alterverse.Visuals
{
    /// <summary>Blender frost-bear geometry bound to the existing HotZoneTank animation sockets.</summary>
    [DefaultExecutionOrder(1150)]
    public sealed class KryonBearBlenderModel:MonoBehaviour
    {
        readonly Transform[] knees=new Transform[4],paws=new Transform[4],legs=new Transform[4];
        Transform body,head,tail; Vector3 previous;float phase,blend;
        public static bool TryBuild(Transform host,out Transform body,out Transform head,out Transform muzzle,out Transform[] legs)
        {
            body=head=muzzle=null;legs=null;
            var data=ResourceCreatureMesh.Load("KryonBear");if(data==null||data.joints==null)return false;
            var root=new GameObject("DF_KryonBearBody");root.transform.SetParent(host,false);root.transform.localPosition=new Vector3(0,.18f,0);
            foreach(var joint in data.joints)
            {
                Transform parent=string.IsNullOrEmpty(joint.parent)?root.transform:root.transform.Find(joint.parent);
                if(parent==null){root.SetActive(false);Destroy(root);return false;}
                var bone=new GameObject(joint.path.Substring(joint.path.LastIndexOf('/')+1)).transform;bone.SetParent(parent,false);
                bone.localPosition=new Vector3(joint.position[0],joint.position[1],joint.position[2]);
            }
            Color[] palette={new(.065f,.105f,.13f),new(.53f,.72f,.81f),new(.83f,.93f,.96f),new(.015f,.75f,1f),new(.24f,.48f,.62f)};
            if(ResourceCreatureMesh.Apply(root.transform,"KryonBear",palette,shareGeometry:true)==null){root.SetActive(false);Destroy(root);return false;}
            body=root.transform;head=body.Find("DF_KryonBearHeadPivot");legs=new Transform[4];
            var motion=root.AddComponent<KryonBearBlenderModel>();motion.body=body;motion.head=head;motion.tail=body.Find("Tail");motion.previous=host.position;
            for(int i=0;i<4;i++)
            {
                legs[i]=body.Find("DF_KryonBearLeg_"+i);motion.legs[i]=legs[i];
                motion.knees[i]=legs[i].Find("Knee");motion.paws[i]=motion.knees[i].Find("Paw");
            }
            muzzle=new GameObject("DF_KryonBearMuzzle").transform;muzzle.SetParent(head,false);muzzle.localPosition=new Vector3(0,-.05f,1.05f);
            var skin=root.GetComponent<SkinnedMeshRenderer>();
            if(skin!=null&&skin.sharedMaterials.Length>3)skin.sharedMaterials[3].SetColor("_EmissionColor",palette[3]*2.2f);
            return true;
        }
        void OnEnable(){if(transform.parent!=null)previous=transform.parent.position;phase=blend=0f;}
        void LateUpdate()
        {
            if(body==null||Time.deltaTime<=0)return;
            Vector3 current=body.parent.position,delta=current-previous;previous=current;delta.y=0;
            if(delta.sqrMagnitude>9){blend=phase=0;return;}
            blend=Mathf.MoveTowards(blend,Mathf.Clamp01(delta.magnitude/Time.deltaTime),Time.deltaTime*6f);phase+=delta.magnitude*3.2f;
            for(int i=0;i<4;i++)
            {
                float wave=Mathf.Sin(phase+(i==0||i==3?0:Mathf.PI));
                knees[i].localRotation=Quaternion.AngleAxis(Mathf.Max(0,wave)*24f*blend,Vector3.right);
                paws[i].localRotation=Quaternion.Inverse(knees[i].localRotation)*Quaternion.AngleAxis(-wave*10f*blend,Vector3.right);
            }
            if(tail!=null)tail.localRotation=Quaternion.AngleAxis(Mathf.Sin(Time.time*1.4f)*7f,Vector3.up);
        }
    }
}

