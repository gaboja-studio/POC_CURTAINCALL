using System;using System.Linq;using System.Reflection;using UnityEngine;using UnityEngine.SceneManagement;using UnityEditor.SceneManagement;using CurtainCall.EscapeRoute;using CurtainCall.Settings;
public static class ReceiverReachDiagnostic {
 public static object Run(){
  var scene=SceneManager.GetActiveScene();if(Application.isPlaying||scene.isDirty)throw new Exception("Saved edit scene required");
  var original=UnityEngine.Object.FindFirstObjectByType<EscapeUnderstageChase>(FindObjectsInactive.Include);if(!original)throw new Exception("Missing chase");
  var preview=EditorSceneManager.NewPreviewScene();var copy=UnityEngine.Object.Instantiate(original.gameObject);
  SceneManager.MoveGameObjectToScene(copy,preview);foreach(var c in copy.GetComponentsInChildren<Collider>(true))c.enabled=false;
  var doll=copy.GetComponentInChildren<EscapePursuerDoll>(true);var socket=typeof(EscapePursuerDoll).GetMethod("SocketAt",BindingFlags.NonPublic|BindingFlags.Instance);
  string P(Transform t)=>t.parent?P(t.parent)+"/"+t.name:t.name;
  try {var feet=new Vector3(-234,5.8f,183.3f);var target=new Vector3(-238,5.88f,183.59f);copy.transform.SetPositionAndRotation(feet,Quaternion.LookRotation(Vector3.ProjectOnPlane(target-feet,Vector3.up)));
   var hand=(Vector3)socket.Invoke(doll,new object[]{EscapePursuerDoll.Motion.GrabStretch,doll.graspTime*.6f,false});Physics.SyncTransforms();
   float[] V(Vector3 p)=>new[]{p.x,p.y,p.z};
   object Hits(Vector3 from,Vector3 to)=>Physics.SphereCastAll(from,GameSettings.EscapeRoute.Pursuer.armRadius,(to-from).normalized,(to-from).magnitude,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore).Where(h=>!h.transform.IsChildOf(original.transform)&&!h.transform.IsChildOf(copy.transform)).Select(h=>new{path=P(h.transform),distance=h.distance,point=V(h.point)}).ToArray();
   var aim=target+Vector3.up*1.224f;
   var area=(BoxCollider)new UnityEditor.SerializedObject(original).FindProperty("encounters").GetArrayElementAtIndex(2).FindPropertyRelative("platform").objectReferenceValue;
   var clear=typeof(EscapeUnderstageChase).GetMethod("ClearReach",BindingFlags.NonPublic|BindingFlags.Instance);var duplicate=copy.GetComponent<EscapeUnderstageChase>();
   var offset=Quaternion.Inverse(copy.transform.rotation)*(hand-feet);
   bool Filter(Vector3 at){var forward=Vector3.ProjectOnPlane(target-at,Vector3.up);if(forward.sqrMagnitude<.09f)return false;var start=at+Quaternion.LookRotation(forward)*offset;return (bool)clear.Invoke(duplicate,new object[]{at+Vector3.up*3,aim,null})&&(bool)clear.Invoke(duplicate,new object[]{start,aim,null});}
   bool found=EscapePursuerNavigation.TryAttackPosition(copy.transform,area,aim,20,out var goal,Filter);
   var nextHand=found?goal+Quaternion.LookRotation(Vector3.ProjectOnPlane(target-goal,Vector3.up))*offset:Vector3.zero;
   var result=new{feet=V(feet),target=V(target),hand=V(hand),bodyHits=Hits(feet+Vector3.up*3,aim),handHits=Hits(hand,aim),found,goal=V(goal),nextHand=V(nextHand),selectedBodyHits=found?Hits(goal+Vector3.up*3,aim):null,selectedHandHits=found?Hits(nextHand,aim):null};
   System.IO.File.WriteAllText(".harness/local/pursuer-space-20261116/receiver-reach.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
  }finally{UnityEngine.Object.DestroyImmediate(copy);EditorSceneManager.ClosePreviewScene(preview);}
 }
}
