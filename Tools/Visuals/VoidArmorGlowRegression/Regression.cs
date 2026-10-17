using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Alterverse.Visuals;
class Regression
{
 static int checks;
 static void Require(bool value,string label){if(!value)throw new Exception(label);checks++;Console.WriteLine("PASS: "+label);}
 static Mesh Source()
 {
  var mesh=new Mesh{vertices=new Vector3[8],normals=new Vector3[8],boneWeights=new BoneWeight[8],bindposes=new[]{new Matrix4x4{fixture=7},new Matrix4x4{fixture=9}}};
  for(int i=0;i<8;i++){mesh.vertices[i]=new Vector3(i,i+.1f,i+.2f);mesh.normals[i]=new Vector3(0,1,0);mesh.boneWeights[i]=new BoneWeight{boneIndex0=i%2,boneIndex1=(i+1)%2,weight0=.6f,weight1=.4f};}
  mesh.SetTriangles(new[]{0,1,2},0);mesh.SetTriangles(new[]{3,4,5},1);mesh.SetTriangles(new[]{4,6,7},2);return mesh;
 }
 static void Main()
 {
  Mesh source=Source();Mesh halo=VoidKnightArmorGlow.BuildHaloMesh(source,new List<int>{1,2});
  Require(halo.vertexCount==15,"compact shared energy vertex once per shell");
  Require(halo.subMeshCount==1&&halo.GetTriangles(0).Length==18,"three shells combined into one submesh");
  Require(halo.vertices.All(p=>p.x>=3),"exclude every non-energy vertex");
  Require(halo.bindposes.Length==2&&halo.bindposes[1].fixture==9,"preserve skin bind matrices");
  Require(halo.GetTriangles(0).All(i=>i>=0&&i<halo.vertexCount),"all remapped indices are valid");
  Require(source.vertexCount==8&&source.GetTriangles(1).SequenceEqual(new[]{3,4,5}),"original model mesh is unchanged");
  for(int i=0;i<halo.vertexCount;i++)
  {
   int original=(int)halo.vertices[i].x;
   Require(halo.boneWeights[i].boneIndex0==source.boneWeights[original].boneIndex0&&halo.boneWeights[i].weight0==.6f&&halo.boneWeights[i].weight1==.4f,"preserve vertex skin weights "+i);
  }
  Require(halo.colors[0].r<halo.colors[5].r&&halo.colors[5].r<halo.colors[10].r&&halo.colors[0].a>halo.colors[10].a,"outer shells are wider and softer");
  GameObject model=new("Knight");GameObject body=new("DF_Anatomy");body.layer=31;body.transform.SetParent(model.transform,false);
  var skin=body.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=source;skin.bones=new[]{model.transform,body.transform};skin.rootBone=model.transform;
  Material metal=new(new Shader()){name="Void black metal"},energy=new(new Shader()){name="Void energy"},core=new(new Shader()){name="Void core energy"};
  skin.sharedMaterials=new[]{metal,energy,core};
  var sword=new GameObject("DF_WeaponR");sword.transform.SetParent(model.transform,false);var weapon=sword.AddComponent<SkinnedMeshRenderer>();weapon.sharedMesh=source;weapon.sharedMaterials=new[]{energy};
  VoidKnightArmorGlow.Apply(model.transform);
  Require(skin.sharedMaterials[0]==metal&&skin.sharedMesh==source,"preserve armor metal and source geometry");
  Require(skin.sharedMaterials[1]!=energy&&skin.sharedMaterials[2]!=core,"replace both energy slots with explicit self illumination");
  Require(skin.sharedMaterials[1].colors["_EmissionColor"].b>1,"core carries HDR emission");
  Require(weapon.sharedMaterials[0]==energy,"leave weapon colorway materials unchanged");
  var shells=model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(s=>s.name=="DF_VoidArmorHalo").ToArray();
  Require(shells.Length==1,"exactly one extra halo renderer");
  Require(shells[0].bones==skin.bones&&shells[0].rootBone==skin.rootBone,"halo uses the original live bone transforms");
  Require(shells[0].gameObject.layer==31&&!shells[0].receiveShadows,"halo respects portrait layer and avoids shadows");
  VoidKnightArmorGlow.Apply(model.transform);
  Require(model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Count(s=>s.name=="DF_VoidArmorHalo")==1,"repeated initialization cannot duplicate glow");
  Resources.missing=true;GameObject missing=new("Missing");VoidKnightArmorGlow.Apply(missing.transform);
  Require(Debug.errors==1&&missing.GetComponent<VoidKnightArmorGlow>()==null,"missing shaders fail before creating partial presentation");
  Console.WriteLine("VOID ARMOR GLOW: "+checks+" checks passed. Unity fixtures; native shader rendering not covered.");
 }
}
