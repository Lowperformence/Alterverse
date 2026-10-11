using System;
using System.Reflection;
using Alterverse.EditorChecks;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
class Regression {
 const string Energy="Assets/Alterverse/Resources/CombatVfx/HeroEnergy.shader",Button="Assets/Alterverse/Shaders/AbilityButton.shader",Surface="Assets/Alterverse/Resources/HeroArt/HeroSurface.shader",Other="Assets/Resources/NewAttack.shader";
 static int checks;
 static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
 static void Reset(){
  AssetDatabase.Shaders.Clear();Resources.Assets.Clear();ShaderUtil.Compiled=0;ShaderUtil.FailName=ShaderUtil.ThrowName=null;ShaderUtil.AllSynchronous=true;Debug.Errors=Debug.Logs=0;UnityEngine.Object.Destroyed=0;SystemInfo.graphicsDeviceType=GraphicsDeviceType.OpenGLES3;EditorSettings.asyncShaderCompilation=true;EditorApplication.isPlaying=true;
  Add(Energy,"Energy",1,"CombatVfx/HeroEnergy");Add(Button,"Button",1,"CombatUI/AbilityButton");Add(Surface,"Surface",4,"HeroArt/HeroSurface");Add(Other,"NewAttack",2,null);
 }
 static void Add(string path,string name,int passes,string resource){var s=new Shader{name=name,Passes=passes};AssetDatabase.Shaders[path]=s;if(resource!=null)Resources.Assets[resource]=new Material(s);}
 static void Reject(Action action,string phrase){try{action();}catch(BuildFailedException error){Check(error.Message.Contains(phrase),"Error must identify cause: "+phrase);return;}throw new Exception("Expected rejection: "+phrase);}
 static void Register(){typeof(ProjectShaderValidation).GetMethod("RegisterPreflight",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);}
 static void Main(){
  Reset();ProjectShaderValidation.ValidateProjectShaders(true);
  Check(ShaderUtil.Compiled==8,"Every project's default pass is warmed, including new shaders");
  Check(ShaderUtil.AllSynchronous,"Default compilation must finish before rendering");
  Check(UnityEngine.Object.Destroyed==4,"Every temporary material is disposed");
  Check(Resources.Assets["CombatVfx/HeroEnergy"].shader==AssetDatabase.Shaders[Energy],"Runtime material templates remain intact");
  Reset();Register();Register();EditorApplication.Emit(PlayModeStateChange.EnteredPlayMode);Check(ShaderUtil.Compiled==0&&EditorSettings.asyncShaderCompilation,"Other editor transitions must be ignored");
  EditorApplication.Emit(PlayModeStateChange.ExitingEditMode);Check(!EditorSettings.asyncShaderCompilation,"Eliminate cyan async placeholders before Play Mode");
  Check(ShaderUtil.Compiled==8&&EditorApplication.isPlaying,"Preflight runs once despite repeated registration");
  Reset();new ProjectShaderValidation().OnPreprocessBuild(new UnityEditor.Build.Reporting.BuildReport());Check(ShaderUtil.Compiled==0,"Build checks avoid warming variants for the wrong editor device");
  Check(new ProjectShaderValidation().callbackOrder>-100,"Build validation follows the existing Android shader preserver");
  Reset();AssetDatabase.Shaders[Other].HasError=true;Reject(()=>new ProjectShaderValidation().OnPreprocessBuild(null),Other);
  Reset();ShaderUtil.FailName="NewAttack";Reject(()=>ProjectShaderValidation.ValidateProjectShaders(true),Other);
  Check(UnityEngine.Object.Destroyed==4,"Compiler error still disposes temporary materials");
  Reset();ShaderUtil.ThrowName="NewAttack";Reject(()=>ProjectShaderValidation.ValidateProjectShaders(true),"compiler failed");Check(UnityEngine.Object.Destroyed==4,"Compiler exception still disposes temporary materials");
  Reset();Resources.Assets.Remove("CombatVfx/HeroEnergy");Reject(()=>ProjectShaderValidation.ValidateProjectShaders(false),"CombatVfx/HeroEnergy");
  Reset();Resources.Assets["CombatUI/AbilityButton"].shader=AssetDatabase.Shaders[Other];Reject(()=>ProjectShaderValidation.ValidateProjectShaders(false),"CombatUI/AbilityButton");
  Reset();AssetDatabase.Shaders[Other]=null;Reject(()=>ProjectShaderValidation.ValidateProjectShaders(false),"Shader could not be loaded: "+Other);
  Reset();AssetDatabase.Shaders[Other].name=AssetDatabase.Shaders[Energy].name;Reject(()=>ProjectShaderValidation.ValidateProjectShaders(false),"Duplicate shader name");
  Reset();AssetDatabase.Shaders[Other].isSupported=false;Reject(()=>ProjectShaderValidation.ValidateProjectShaders(true),"unsupported");
  Reset();AssetDatabase.Shaders[Other].Passes=0;Reject(()=>ProjectShaderValidation.ValidateProjectShaders(true),"no usable render passes");
  Reset();SystemInfo.graphicsDeviceType=GraphicsDeviceType.Null;AssetDatabase.Shaders[Other].isSupported=false;ProjectShaderValidation.ValidateProjectShaders(true);Check(ShaderUtil.Compiled==0,"Headless build checks don't require a graphics device");
  Reset();AssetDatabase.Shaders[Other].HasError=true;EditorApplication.Emit(PlayModeStateChange.ExitingEditMode);Check(!EditorApplication.isPlaying&&Debug.Errors==1,"Broken shaders abort Play Mode with a diagnostic");
  Reset();AssetDatabase.Shaders.Clear();Reject(()=>ProjectShaderValidation.ValidateProjectShaders(false),"No project shaders");
  Console.WriteLine("PASS: "+checks+" shader preflight behavior checks");
 }
}
