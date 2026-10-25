// Extra deterministic engine APIs. Native physics and rendering are not simulated.
 public class Transform:Component {
  public Vector3 localPosition,localScale=Vector3.one;public Quaternion localRotation=Quaternion.identity;public Transform parent;
  public List<Transform> Children=>GameObject.All.Where(g=>g.transform.parent==this).Select(g=>g.transform).ToList();
  public int childCount=>Children.Count;public Transform GetChild(int i)=>Children[i];
  public Vector3 lossyScale=>parent==null?localScale:Vector3.Scale(parent.lossyScale,localScale);
  public Vector3 position{get=>parent==null?localPosition:parent.TransformPoint(localPosition);set=>localPosition=parent==null?value:parent.InverseTransformPoint(value);}
  public Quaternion rotation{get=>parent==null?localRotation:parent.rotation*localRotation;set=>localRotation=parent==null?value:Quaternion.Inverse(parent.rotation)*value;}
  public Vector3 forward{get=>rotation*Vector3.forward;set=>rotation=Quaternion.LookRotation(value);}public Vector3 right=>rotation*Vector3.right;public Vector3 up=>rotation*Vector3.up;
  public Transform Find(string name){var slash=name.IndexOf('/');string first=slash<0?name:name.Substring(0,slash);var child=Children.FirstOrDefault(c=>c.name==first);return slash<0?child:child?.Find(name.Substring(slash+1));}
  public bool IsChildOf(Transform t){for(var p=this;p!=null;p=p.parent)if(p==t)return true;return false;}
  public void SetPositionAndRotation(Vector3 p,Quaternion q){position=p;rotation=q;}
  public void SetParent(Transform t,bool world){var p=position;var q=rotation;var s=lossyScale;parent=t;if(t!=null)UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(gameObject,t.gameObject.scene);if(world){position=p;rotation=q;var ps=parent==null?Vector3.one:parent.lossyScale;localScale=new(s.x/ps.x,s.y/ps.y,s.z/ps.z);}}
  public Vector3 InverseTransformPoint(Vector3 p){var v=Quaternion.Inverse(rotation)*(p-position);var s=lossyScale;return new(v.x/s.x,v.y/s.y,v.z/s.z);}
  public Vector3 TransformPoint(Vector3 p)=>position+rotation*Vector3.Scale(lossyScale,p);
  public Vector3 InverseTransformDirection(Vector3 p)=>Quaternion.Inverse(rotation)*p;
 }
 // END TRANSFORM
namespace UnityEngine {
 public class BoxCollider:Collider{public Vector3 size;}
 public enum CollisionDetectionMode{ContinuousSpeculative}public enum ForceMode{VelocityChange}
 public class Rigidbody:Component {
  public float mass,linearDamping,angularDamping,maxAngularVelocity,maxDepenetrationVelocity;public int solverIterations,solverVelocityIterations;
  public CollisionDetectionMode collisionDetectionMode;public Vector3 linearVelocity,Torque,Force;
  public Vector3 position{get=>transform.position;set=>transform.position=value;}public Quaternion rotation=>transform.rotation;
  public void AddForce(Vector3 v,ForceMode m){Force+=v;}public void AddTorque(Vector3 v,ForceMode m){Torque+=v;}
 }
 public struct SoftJointLimit{public float limit;}
 public class CharacterJoint:Component{public Rigidbody connectedBody;public Vector3 anchor,connectedAnchor,axis,swingAxis;public bool autoConfigureConnectedAnchor,enableProjection,enableCollision;public float projectionDistance,projectionAngle;public SoftJointLimit lowTwistLimit,highTwistLimit,swing1Limit,swing2Limit;}
 public struct PhysicsScene {
  public int Id;public PhysicsScene(int id){Id=id;}public bool IsValid()=>SceneManagement.SceneManager.Loaded.Contains(Id);
  public void Simulate(float dt){SceneManagement.SceneManager.Simulated.Add(Id);}
 }
}
namespace UnityEngine.SceneManagement {
 public enum LocalPhysicsMode{Physics3D}public struct CreateSceneParameters{public CreateSceneParameters(LocalPhysicsMode m){}}
 public struct Scene {
  public int Id;public bool IsValid()=>SceneManager.Loaded.Contains(Id);public bool isLoaded=>IsValid();public UnityEngine.PhysicsScene GetPhysicsScene()=>new(Id);
  public static bool operator==(Scene a,Scene b)=>a.Id==b.Id;public static bool operator!=(Scene a,Scene b)=>a.Id!=b.Id;public override bool Equals(object o)=>o is Scene s&&this==s;public override int GetHashCode()=>Id;
 }
 public static class SceneManager {
  public static Scene ActiveScene=new(){Id=1};private static int next=100;public static HashSet<int> Loaded=new(){1};public static List<int> Simulated=new();
  public static event Action<Scene> sceneUnloaded;
  public static Scene CreateScene(string n,CreateSceneParameters p){var scene=new Scene{Id=next++};Loaded.Add(scene.Id);return scene;}
  public static void MoveGameObjectToScene(UnityEngine.GameObject g,Scene s){g.scene=s;foreach(var child in g.transform.Children)MoveGameObjectToScene(child.gameObject,s);}
  public static void UnloadSceneAsync(Scene s){if(!Loaded.Remove(s.Id))return;sceneUnloaded?.Invoke(s);foreach(var g in UnityEngine.GameObject.All.Where(g=>g.scene==s).ToArray())g.DestroyForTest();}
 }
}
