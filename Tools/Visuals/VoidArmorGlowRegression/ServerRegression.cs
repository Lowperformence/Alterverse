using System;
using UnityEngine;
using Alterverse.Visuals;
class ServerRegression
{
 static void Main()
 {
  Resources.missing=true;
  GameObject model=new("Dedicated server hero");
  VoidKnightArmorGlow.Apply(model.transform);
  if(model.GetComponent<VoidKnightArmorGlow>()!=null||model.transform.children.Count!=0||Debug.errors!=0)
   throw new Exception("Dedicated server must skip all armor glow resources and rendering.");
  Console.WriteLine("PASS: UNITY_SERVER skips local armor glow.");
 }
}
