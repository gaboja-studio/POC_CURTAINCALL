using System;
using System.Collections;
using System.Linq;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace CurtainCall.UI.SceneIntegration.Tests
{
 public sealed partial class DevStartLauncherPlayTests
 {
  [UnityTest]
  public IEnumerator PursuitSurfaceV7_SharedMeshVariantsCaptureStainsAndBloodToggle()
  {
   yield return PursuitHost();
   var director=Find("CurtainCall.WaitingRoom.Pursuit.PursuitDirector");
   var actors=((IEnumerable)Get(director,"Actors")).Cast<Component>().ToArray();
   Assert.Greater(actors.Length,0);
   foreach(var actor in actors){
    var skin=actor.GetComponentInChildren<SkinnedMeshRenderer>();
    Assert.IsNotNull(skin);
    Assert.AreEqual("CurtainCall/Pursuit/TroupeV7",skin.sharedMaterial.shader.name);
    Assert.AreEqual(1,skin.sharedMaterials.Length);
#if UNITY_EDITOR
    var original=UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_ThirdParty/curtaincall/Pursuit/Reclaimed/"+skin.sharedMesh.name.Split(' ')[0]+" repaired shell.asset");
    Assert.AreEqual(Enumerable.Range(0,original.subMeshCount).Sum(i=>(long)original.GetIndexCount(i)),Enumerable.Range(0,skin.sharedMesh.subMeshCount).Sum(i=>(long)skin.sharedMesh.GetIndexCount(i)),"Material art must preserve the original triangle count.");
#endif
    Assert.IsTrue(actor.GetComponentsInChildren<Transform>(true).All(t=>!t.name.StartsWith("Reclaimed_")));
   }
   var a=actors[0];var appearance=a.GetComponent(T("CurtainCall.WaitingRoom.Pursuit.PursuitAppearance"));
   var surface=a.GetComponentInChildren<SkinnedMeshRenderer>();var mesh=surface.sharedMesh;
   var originalState=Field(appearance,"SurfaceState");var captures=(int)Get(appearance,"CaptureCount");
   string bloodKey="CurtainCall.Settings.ShowBloodEffect";
   bool hadSetting=PlayerPrefs.HasKey(bloodKey);int previousSetting=PlayerPrefs.GetInt(bloodKey,1);
   var setting=T("CurtainCall.StartLobby.ClientSettings").GetProperty("ShowBloodEffect");
   int startFrame=Time.frameCount;int assertions=0;
   try{
    setting.SetValue(null,true);
    foreach(string state in new[]{"Clean","Worn","LightBlood","Bloodied","HeavyBlood"}){
     Set(appearance,"SurfaceState",Enum.Parse(T("CurtainCall.WaitingRoom.Pursuit.PursuitSurfaceState"),state));
     Call(appearance,"SetCaptureCount",0);
     yield return null;yield return null;
     var props=new MaterialPropertyBlock();surface.GetPropertyBlock(props);
     Assert.AreSame(mesh,surface.sharedMesh,"A surface variant must not clone or replace geometry.");
     Assert.AreEqual(state=="LightBlood"?.25f:state=="Bloodied"?.6f:state=="HeavyBlood"?1:0,props.GetFloat("_BaseBlood"));
     Assert.AreEqual(state=="Clean"?.04f:state=="Worn"?.85f:state=="LightBlood"?.28f:state=="HeavyBlood"?.65f:.4f,props.GetFloat("_WearAmount"),.001f);
     Call(appearance,"SetCaptureCount",3);yield return null;yield return null;
     surface.GetPropertyBlock(props);
     bool visible=(bool)Static("CurtainCall.Damage.InjuryBloodEffects","IsVisible");
     var rules=Get(Static("CurtainCall.Settings.GameSettings","WaitingRoom"),"Pursuit");
     Assert.AreEqual(visible?Mathf.Clamp01(3*(float)Field(rules,"BloodPerCapture")):0,props.GetFloat("_BloodAmount"),.001f);
     setting.SetValue(null,false);yield return null;yield return null;
     surface.GetPropertyBlock(props);Assert.AreEqual(0,props.GetFloat("_BloodVisible"));Assert.AreEqual(0,props.GetFloat("_BloodAmount"));
     setting.SetValue(null,true);assertions++;
    }
    Assert.Greater(Time.frameCount,startFrame+8,"Actual Play Mode frames must advance.");
    File.WriteAllText(Application.dataPath+"/_ThirdParty/curtaincall/Pursuit/Concepts/JointSeams/Source~/V7/live-blood-variants-verification.json",
     "{\"spawnedActors\":"+actors.Length+",\"surfaceStates\":"+assertions+",\"advancedFrames\":"+(Time.frameCount-startFrame)+",\"result\":\"PASS\"}");
   }
   finally{
    Set(appearance,"SurfaceState",originalState);Call(appearance,"SetCaptureCount",captures);
    if(hadSetting)PlayerPrefs.SetInt(bloodKey,previousSetting);else PlayerPrefs.DeleteKey(bloodKey);
   }
  }

  [UnityTest]
  public IEnumerator PursuitSurface_PlacementSelectionMatchesReplicaAndSurvivesCaptureReset()
  {
   yield return PursuitHost();
   var director=Find("CurtainCall.WaitingRoom.Pursuit.PursuitDirector");
   var actor=((IEnumerable)Get(director,"Actors")).Cast<Component>().First(a=>Get(a,"Placement")!=null);
   var placement=Get(actor,"Placement");var appearance=actor.GetComponent(T("CurtainCall.WaitingRoom.Pursuit.PursuitAppearance"));
   var choice=Field(placement,"Appearance");var pattern=Field(placement,"AppearancePattern");
   var replicaObject=UnityEngine.Object.Instantiate((GameObject)Field(placement,"Prefab"));
   var replica=replicaObject.GetComponent(T("CurtainCall.WaitingRoom.Pursuit.PursuitActor"));
   var replicaLook=replicaObject.GetComponent(T("CurtainCall.WaitingRoom.Pursuit.PursuitAppearance"));
   var surface=actor.GetComponentInChildren<SkinnedMeshRenderer>();
   var replicaSurface=replica.GetComponentInChildren<SkinnedMeshRenderer>();
   string bloodKey="CurtainCall.Settings.ShowBloodEffect";
   bool hadSetting=PlayerPrefs.HasKey(bloodKey);int previousSetting=PlayerPrefs.GetInt(bloodKey,1);
   var setting=T("CurtainCall.StartLobby.ClientSettings").GetProperty("ShowBloodEffect");
   int frames=Time.frameCount;
   try {
    setting.SetValue(null,true);
    foreach(string state in new[]{"Clean","Worn","LightBlood","Bloodied","HeavyBlood","Varied","Prefab"}){
     Set(placement,"Appearance",Enum.Parse(T("CurtainCall.WaitingRoom.Pursuit.PursuitAppearanceChoice"),state));
     Set(placement,"AppearancePattern",7);
     Call(placement,"ApplyAppearance",appearance);
     Call(replica,"Initialize",director,Get(actor,"Room"),placement);
     Call(replica,"Receive",actor.transform.position,actor.transform.rotation,Get(actor,"State"),null,0f,Vector3.zero,false,null);
     yield return null;yield return null;
     Assert.AreEqual(Get(placement,"ResolvedSurface"),Field(appearance,"SurfaceState"));
     Assert.AreEqual(Field(appearance,"SurfaceState"),Field(replicaLook,"SurfaceState"));
     Assert.AreEqual(Field(appearance,"SurfaceSeed"),Field(replicaLook,"SurfaceSeed"));
     Assert.AreSame(surface.sharedMesh,replicaSurface.sharedMesh);
     Assert.AreSame(surface.sharedMaterial,replicaSurface.sharedMaterial);
     var before=Field(appearance,"SurfaceState");
     Call(appearance,"SetCaptureCount",3);Call(replicaLook,"SetCaptureCount",3);
     yield return null;yield return null;
     var localProps=new MaterialPropertyBlock();var remoteProps=new MaterialPropertyBlock();
     surface.GetPropertyBlock(localProps);replicaSurface.GetPropertyBlock(remoteProps);
     foreach(string property in new[]{"_BaseBlood","_WearAmount","_BloodAmount","_VariantSeed"})
      Assert.AreEqual(localProps.GetFloat(property),remoteProps.GetFloat(property),property);
     Call(actor,"ResetActor");Call(replica,"ResetActor");
     yield return null;
     Assert.AreEqual(before,Field(appearance,"SurfaceState"),"Capture reset must keep the authored appearance.");
     Assert.AreEqual(0,Get(appearance,"CaptureCount"));
    }
    Assert.Greater(Time.frameCount,frames+20);
    File.WriteAllText(Application.dataPath+"/_ThirdParty/curtaincall/Pursuit/Concepts/JointSeams/Source~/V7/live-placement-appearance-verification.json",
     "{\"choices\":7,\"advancedFrames\":"+(Time.frameCount-frames)+",\"result\":\"PASS\"}");
   } finally {
    Set(placement,"Appearance",choice);Set(placement,"AppearancePattern",pattern);Call(placement,"ApplyAppearance",appearance);
    UnityEngine.Object.DestroyImmediate(replicaObject);
    if(hadSetting)PlayerPrefs.SetInt(bloodKey,previousSetting);else PlayerPrefs.DeleteKey(bloodKey);
   }
  }

  [UnityTest]
  public IEnumerator PursuitSurface_IndependentLayersRetainWearWhenBloodHiddenOnReplica()
  {
   yield return PursuitHost();
   var director=Find("CurtainCall.WaitingRoom.Pursuit.PursuitDirector");
   var actor=((IEnumerable)Get(director,"Actors")).Cast<Component>().First(a=>Get(a,"Placement")!=null);
   var placement=Get(actor,"Placement");string saved=JsonUtility.ToJson(placement);
   var look=actor.GetComponent(T("CurtainCall.WaitingRoom.Pursuit.PursuitAppearance"));
   var clone=UnityEngine.Object.Instantiate((GameObject)Field(placement,"Prefab"));
   var replica=clone.GetComponent(T("CurtainCall.WaitingRoom.Pursuit.PursuitActor"));
   var remoteLook=clone.GetComponent(T("CurtainCall.WaitingRoom.Pursuit.PursuitAppearance"));
   var skin=actor.GetComponentInChildren<SkinnedMeshRenderer>();var remoteSkin=clone.GetComponentInChildren<SkinnedMeshRenderer>();
   string bloodKey="CurtainCall.Settings.ShowBloodEffect";
   bool had=PlayerPrefs.HasKey(bloodKey);int previous=PlayerPrefs.GetInt(bloodKey,1);
   var setting=T("CurtainCall.StartLobby.ClientSettings").GetProperty("ShowBloodEffect");
   int frame=Time.frameCount;
   try {
    setting.SetValue(null,true);
    Set(placement,"CustomSurfaceLayers",true);Set(placement,"DirtAmount",.9f);Set(placement,"WearAmount",.8f);Set(placement,"BloodAmount",.25f);
    Set(placement,"WearPattern",3);Set(placement,"AppearancePattern",11);
    Call(placement,"ApplyAppearance",look);Call(replica,"Initialize",director,Get(actor,"Room"),placement);
    Call(replica,"Receive",actor.transform.position,actor.transform.rotation,Get(actor,"State"),null,0f,Vector3.zero,false,null);
    yield return null;yield return null;
    var props=new MaterialPropertyBlock();var copy=new MaterialPropertyBlock();
    skin.GetPropertyBlock(props);remoteSkin.GetPropertyBlock(copy);
    Assert.AreEqual(.9f,props.GetFloat("_DirtAmount"));Assert.AreEqual(.8f,props.GetFloat("_WearAmount"));Assert.AreEqual(.25f,props.GetFloat("_BaseBlood"));
    foreach(string name in new[]{"_DirtAmount","_WearAmount","_BaseBlood","_WearSeed","_VariantSeed"})Assert.AreEqual(props.GetFloat(name),copy.GetFloat(name));
    float wearSeed=props.GetFloat("_WearSeed"),bloodSeed=props.GetFloat("_VariantSeed");
    Set(placement,"BloodAmount",.95f);Set(placement,"AppearancePattern",2);Call(placement,"ApplyAppearance",look);Call(placement,"ApplyAppearance",remoteLook);
    Call(look,"SetCaptureCount",3);Call(remoteLook,"SetCaptureCount",3);
    yield return null;yield return null;
    skin.GetPropertyBlock(props);Assert.AreEqual(.95f,props.GetFloat("_BaseBlood"));Assert.AreEqual(wearSeed,props.GetFloat("_WearSeed"));
    Assert.AreNotEqual(bloodSeed,props.GetFloat("_VariantSeed"));Assert.Greater(props.GetFloat("_BloodAmount"),0);
    setting.SetValue(null,false);yield return null;yield return null;
    skin.GetPropertyBlock(props);remoteSkin.GetPropertyBlock(copy);
    foreach(var values in new[]{props,copy}){
     Assert.AreEqual(0,values.GetFloat("_BloodVisible"));Assert.AreEqual(0,values.GetFloat("_BloodAmount"));
     Assert.AreEqual(.9f,values.GetFloat("_DirtAmount"));Assert.AreEqual(.8f,values.GetFloat("_WearAmount"));
     Assert.AreEqual(wearSeed,values.GetFloat("_WearSeed"));
    }
    Call(actor,"ResetActor");Call(replica,"ResetActor");yield return null;
    Assert.AreEqual(.95f,Get(look,"AuthoredBloodAmount"));Assert.AreEqual(.9f,Get(look,"DirtAmount"));
    Assert.AreSame(skin.sharedMesh,remoteSkin.sharedMesh);Assert.AreSame(skin.sharedMaterial,remoteSkin.sharedMaterial);
    Assert.Greater(Time.frameCount,frame+5);
    File.WriteAllText(Application.dataPath+"/_ThirdParty/curtaincall/Pursuit/Concepts/JointSeams/Source~/V8/live-layer-verification.json",
     "{\"result\":\"PASS\",\"advancedFrames\":"+(Time.frameCount-frame)+",\"independentDirtWearBlood\":true,\"replicaMatched\":true}");
   } finally {
    JsonUtility.FromJsonOverwrite(saved,placement);Call(placement,"ApplyAppearance",look);
    UnityEngine.Object.DestroyImmediate(clone);
    if(had)PlayerPrefs.SetInt(bloodKey,previous);else PlayerPrefs.DeleteKey(bloodKey);
   }
  }
 }
}

