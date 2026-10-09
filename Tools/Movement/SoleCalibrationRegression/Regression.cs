static int checks;
static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
static void Near(Vector3 a,Vector3 b,string message){Check(Vector3.Distance(a,b)<.0002f,message+" error="+Vector3.Distance(a,b));}
static Transform T(string name,Transform parent,Vector3 position,Vector3 scale,Quaternion rotation){var t=new GameObject(name).transform;t.parent=parent;t.localPosition=position;t.localScale=scale;t.localRotation=rotation;return t;}
static void FullCapture(float factor,bool permute){
 var imported=T("fullImport",null,new Vector3(9,2,-6),new Vector3(1.22f,1.32f,1.1f)*factor,Quaternion.Euler(2,37,-3));
 var renderer=T("fullRenderer",imported,new Vector3(.3f,.2f,-.1f)/factor,new Vector3(1.1f,1,.9f),Quaternion.Euler(0,17,4));
 var l=T("leftFoot",imported,new Vector3(.18f,.12f,0)/factor,Vector3.one/factor,Quaternion.identity);
 var r=T("rightFoot",imported,new Vector3(-.18f,.12f,0)/factor,Vector3.one/factor,Quaternion.identity);
 Transform[] bones=permute?new[]{r,l}:new[]{l,r};int li=permute?1:0,ri=1-li;
 var skin=renderer.gameObject.AddComponent<SkinnedMeshRenderer>();skin.bones=bones;
 var vertices=new List<Vector3>();var weights=new List<BoneWeight>();
 foreach(int side in new[]{0,1})foreach(Vector3 point in new[]{new Vector3(-.06f,-.08f,-.05f),new Vector3(.06f,-.08f,.18f),new Vector3(0,-.02f,.12f)}){
  vertices.Add(renderer.InverseTransformPoint((side==0?l:r).TransformPoint(point)));
  weights.Add(new BoneWeight{boneIndex0=side==0?li:ri,weight0=1});
 }
 vertices.Add(renderer.InverseTransformPoint(l.position+Vector3.up*2.4f));weights.Add(new BoneWeight{boneIndex0=li,weight0=1});
 vertices.Add(new Vector3(float.NaN,0,0));weights.Add(new BoneWeight{boneIndex0=li,weight0=1});
 skin.sharedMesh=new Mesh{vertices=vertices.ToArray(),boneWeights=weights.ToArray(),bindposes=new[]{bones[0].worldToLocalMatrix*renderer.localToWorldMatrix,bones[1].worldToLocalMatrix*renderer.localToWorldMatrix}};
 var empty=T("nullMesh",imported,Vector3.zero,Vector3.one,Quaternion.identity);empty.gameObject.AddComponent<SkinnedMeshRenderer>();
 var unreadable=T("unreadableSkin",imported,Vector3.zero,Vector3.one,Quaternion.identity).gameObject.AddComponent<SkinnedMeshRenderer>();
 unreadable.sharedMesh=new Mesh{isReadable=false};unreadable.bones=null;
 var h=new CurrentSoleHarness{left=new Leg{foot=l,length=1.2f,sole=.08f},right=new Leg{foot=r,length=1.2f,sole=.08f}};
 h.CaptureSoles(imported);
 Check(h.left.solePoints.Length==3&&h.right.solePoints.Length==3,"full CaptureSoles exact valid points/outlier rejection "+factor+" "+permute);
 float expected=-l.TransformVector(new Vector3(-.06f,-.08f,-.05f)).y;
 expected=Mathf.Max(expected,-l.TransformVector(new Vector3(.06f,-.08f,.18f)).y);
 Check(Math.Abs(h.left.sole-expected)<.0002f&&Math.Abs(h.right.sole-expected)<.0002f,"full CaptureSoles actual boot floor "+factor);
 foreach(var point in h.left.solePoints)Check(l.TransformVector(point).magnitude<.3f,"full CaptureSoles footlocal scale cancellation");
}
static void SupportAndSwing(){
 var h=new CurrentSoleHarness{motion=new HeroVisualMotion{PlanarSpeed=2,RunBlend=1}};
 var foot=new GameObject("plantFoot").transform;
 var leg=new Leg{foot=foot,length=1.2f,sole=.08f,authoredSample=true,contact=true,initialized=true,soleAdjustment=.03f,plant=new Vector3(4,.06f,0),target=new Vector3(4,.09f,0)};
 Vector3 previous=new Vector3(0,2,0);float dt=1f/60,maxStep=1.2f*6*dt;
 h.StabilizeAuthoredTarget(leg,previous,dt);
 Check(Math.Abs(leg.plant.y-.06f)<.00001f&&leg.target.y<=.09f+.276f+.00001f,"support anchor at floor on first frame; displayed ankle has bounded temporary lift");
 Check(leg.target.x<=maxStep+.00001f&&leg.target.x>0,"support XZ speed budget honored during immediate height repair");
 for(int frame=0;frame<10;frame++){
  Vector3 last=leg.target;
  h.PlanAuthored(leg,Vector3.up*5,Vector3.zero,true,Quaternion.identity,.2f);
  h.StabilizeAuthoredTarget(leg,last,dt);
  Check(Math.Abs(leg.plant.y-.06f)<.00001f,"stable support anchor remains terrain during descent");
  Check(leg.target.y<=last.y+.00001f&&last.y-leg.target.y<=maxStep+.00001f,"touchdown target descends continuously inside travel budget");
 }
 Check(Math.Abs(leg.target.y-.06f)<.00001f,"support settles to floor within ten frames");
 var fitted=new Leg{hip=T("fitHip",null,new Vector3(0,1.2f,0),Vector3.one,Quaternion.identity),knee=T("fitKnee",null,new Vector3(0,.6f,0),Vector3.one,Quaternion.identity),foot=new GameObject("fitFoot").transform,length=1.2f,sole=.08f,authoredSample=true,contact=true,initialized=true,authoredLift=.276f,target=new Vector3(10,.336f,0),normal=Vector3.up,authoredRotation=Quaternion.identity};
 h.FitAuthoredTarget(fitted,.144f,.6f,false);
 Check(Math.Abs(fitted.plant.y-.06f)<.00001f,"reach fitting preserves terrain plant, excluding transient lift");
 float oldPlant=OldSoleHarness.FloatingPlant();Check(oldPlant>1.8f,"old high previous ankle poisoned persistent support anchor");
 foreach(float run in new[]{0f,.5f,1f}){
  h.motion.RunBlend=run;leg.contact=false;leg.target=new Vector3(4,.06f,0);leg.soleAdjustment=0;
  h.StabilizeAuthoredTarget(leg,previous,dt);
  float limit=.06f+leg.length*Mathf.Lerp(.14f,.23f,run);
  Check(Math.Abs(leg.target.y-limit)<.00001f&&leg.authoredLift<=limit-.06f+.00001f,"corrupt swing bounded at run="+run);
  Check(leg.target.x<=maxStep+.00001f&&leg.target.x>0,"swing XZ speed budget honored");
 }
 leg.contact=true;leg.target=Vector3.up*.06f;h.StabilizeAuthoredTarget(leg,new Vector3(0,-2,0),dt);
 Check(Math.Abs(leg.target.y-.06f)<.00001f,"below-ground support raised to floor");
 Console.WriteLine("Support regression: old persistent plant="+oldPlant.ToString("F3")+"m; new plant=.060m and target settles within ten frames; swing bounded .168..276m above ankle-floor.");
}
public static void Main(){
 SupportAndSwing();
 foreach(float factor in new[]{.01f,1f,100f})foreach(bool permutation in new[]{false,true})FullCapture(factor,permutation);
 foreach(float factor in new[]{.01f,1f,100f}){
  var root=T("root",null,new Vector3(7,3,-5),new Vector3(1.22f,1.32f,1.10f),Quaternion.Euler(4,37,-2));
  var imported=T("import",root,new Vector3(.2f,-.96f,.1f),Vector3.one*factor,Quaternion.Euler(0,12,0));
  var renderer=T("renderer",imported,new Vector3(.3f,.15f,-.1f)/factor,new Vector3(1.10f,1f,.9f),Quaternion.Euler(0,19,5));
  var foot=T("foot",imported,new Vector3(.18f,.12f,.02f)/factor,Vector3.one/factor,Quaternion.identity);
  var shin=T("shin",imported,new Vector3(.18f,.62f,.02f)/factor,Vector3.one/factor,Quaternion.identity);
  foreach(bool permute in new[]{false,true}){
   Transform[] bones=permute?new[]{shin,foot}:new[]{foot,shin};int fi=permute?1:0,si=1-fi;
   Matrix4x4[] bind={bones[0].worldToLocalMatrix*renderer.localToWorldMatrix,bones[1].worldToLocalMatrix*renderer.localToWorldMatrix};
   var leg=new Leg{foot=foot,length=1.2f,sole=.08f};var points=new HashSet<Vector3>();
   foreach(Vector3 local in new[]{new Vector3(-.06f,-.08f,-.05f),new Vector3(.06f,-.08f,.18f),new Vector3(0,-.02f,.12f)}){
    Vector3 world=foot.TransformPoint(local),mesh=renderer.InverseTransformPoint(world);
    var weight=new BoneWeight{boneIndex0=fi,weight0=1};
    Check(FootWeighted(weight,fi)&&!FootWeighted(weight,si),"foot bone index permutation");
    Vector3 actual=SkinVertex(bones,bind,weight,mesh);Near(actual,world,"import factor "+factor+" rigid skin");
    Check(CaptureSolePoint(leg,actual,points),"real boot accepted");
   }
   leg.solePoints=new List<Vector3>(points).ToArray();float expected=float.PositiveInfinity;
   foreach(var local in leg.solePoints)expected=Mathf.Min(expected,foot.TransformVector(local).y);
   Check(Math.Abs(SoleExtent(leg)-(-expected))<.0002f,"geometry-derived rotated sole");
   Vector3 meshPoint=renderer.InverseTransformPoint(foot.TransformPoint(new Vector3(0,-.05f,.1f)));
   Vector3 original=SkinVertex(bones,bind,new BoneWeight{boneIndex0=fi,weight0=.6f,boneIndex1=si,weight1=.4f},meshPoint);
   shin.localPosition+=new Vector3(.03f,.08f,-.01f)/factor;
   Vector3 moved=SkinVertex(bones,bind,new BoneWeight{boneIndex0=fi,weight0=.3f,boneIndex1=si,weight1=.2f},meshPoint);
   Near(moved,original+shin.parent.TransformVector(new Vector3(.03f,.08f,-.01f)/factor)*.4f,"normalized mixed weights animated shin");
   shin.localPosition-=new Vector3(.03f,.08f,-.01f)/factor;
   Near(SkinVertex(bones,bind,new BoneWeight{boneIndex0=fi,weight0=.4f,boneIndex1=99,weight1=.6f},meshPoint),foot.TransformPoint(new Vector3(0,-.05f,.1f)),"invalid influence excluded and normalized");
   Near(SkinVertex(bones,bind,new BoneWeight{boneIndex3=fi,weight3=1},meshPoint),foot.TransformPoint(new Vector3(0,-.05f,.1f)),"fourth influence slot");
   Check(FootWeighted(new BoneWeight{boneIndex3=fi,weight3=.36f},fi),"foot selection fourth slot");
   Check(!FootWeighted(new BoneWeight{boneIndex0=fi,weight0=.35f},fi),"foot selection threshold");
   Check(float.IsNaN(SkinVertex(new Transform[]{null},new Matrix4x4[]{bind[fi]},new BoneWeight{boneIndex0=0,weight0=1},meshPoint).x),"null bone rejected");
   Check(float.IsNaN(SkinVertex(bones,new Matrix4x4[0],new BoneWeight{boneIndex0=fi,weight0=1},meshPoint).x),"missing bind rejected");
   Check(float.IsNaN(SkinVertex(bones,bind,new BoneWeight{boneIndex0=fi,weight0=float.PositiveInfinity},meshPoint).x),"infinite weight rejected");
   Check(float.IsNaN(SkinVertex(bones,bind,new BoneWeight{boneIndex0=99,weight0=1},meshPoint).x),"no valid influence NaN");
   Check(float.IsNaN(SkinVertex(bones,bind,new BoneWeight{boneIndex0=fi,weight0=float.NaN},meshPoint).x),"NaN weight rejected");
   Check(!CaptureSolePoint(leg,new Vector3(float.NaN,0,0),points),"NaN point rejected");
   Check(!CaptureSolePoint(leg,new Vector3(0,float.PositiveInfinity,0),points),"infinite point rejected");
   Check(!CaptureSolePoint(leg,foot.position+Vector3.up*2,points),"shoulder outlier rejected");
   foot.localScale=foot.localScale*100;
   Check(SoleExtent(leg)<=leg.length*.35f+.00001f,"post-calibration scale corruption bounded");
   foot.localScale=foot.localScale/100;
  }
 }
 var direct=T("foot",null,Vector3.zero,Vector3.one,Quaternion.identity);
 var corrupt=new Leg{foot=direct,length=1.2f,sole=.08f,solePoints=new[]{new Vector3(0,-2.4f,0),new Vector3(float.NaN,0,0)}};
 Check(Math.Abs(SoleExtent(corrupt)-.42f)<.00001f,"live corrupt point clamped");
 var current=new CurrentSoleHarness();current.motion=new Alterverse.Visuals.HeroVisualMotion{PresentedRotation=Quaternion.identity,LocomotionWeight=1};
 corrupt.normal=Vector3.up;corrupt.authoredSample=true;corrupt.authoredRotation=Quaternion.identity;corrupt.target=Vector3.up*.06f;
 current.PrepareFoot(corrupt,.6f,false);Check(corrupt.target.y<.5f,"new path cannot target shoulders");
 float oldHeight=OldSoleHarness.Reproduce();Check(oldHeight>2.2f,"unchanged prepatch shoulder target reproduced");
 Console.WriteLine("PASS sole calibration: "+checks+" focused checks; old target="+oldHeight.ToString("F3")+"m, corrected target="+corrupt.target.y.ToString("F3")+"m");
 Console.WriteLine("Limits: exact extracted product methods, synthetic affine transforms and skin weights; no Unity importer, BakeMesh, actual FBX/scene physics or rendered mesh.");
}
