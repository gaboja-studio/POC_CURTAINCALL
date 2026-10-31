using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace CurtainCall.WaitingRoom.Editor {
 public static partial class WaitingRoomLayoutA {
 public static string ConnectNightMaintenance() => RunPass("Night maintenance circuits and gates", target => {
  Remove(target,"NightMaintenance"); var root=Group(target,"NightMaintenance",Vector3.zero);root.rotation=Quaternion.identity;
  var mat=Mat("LA_SteelPlate");
  void Box(Transform parent,string name,Vector3 pos,Vector3 scale) { var g=GameObject.CreatePrimitive(PrimitiveType.Cube);Undo.RegisterCreatedObjectUndo(g,"Night gate");g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=pos;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=mat; }
  void Gate(string name,Vector3 pos,float yaw,float width,float height) {
   var gate=Group(root,name,pos);gate.rotation=Quaternion.Euler(0,yaw,0);
   Box(gate,"Header",new Vector3(0,height+.12f,0),new Vector3(width+.3f,.24f,.3f));
   var bars=Group(gate,"Bars",Vector3.zero);bars.localPosition=new Vector3(0,height,0);
   for(float x=-width/2+.09f;x<width/2;x+=.22f)Box(bars,"VerticalBar",new Vector3(x,-height/2,0),new Vector3(.065f,height,.09f));
   foreach(float y in new[]{-.1f,-height*.5f,-height+.1f})Box(bars,"CrossBar",new Vector3(0,y,0),new Vector3(width,.08f,.12f));
   var blocker=new GameObject("GateCollision");blocker.transform.SetParent(bars,false);var c=blocker.AddComponent<BoxCollider>();c.center=new Vector3(0,-height/2,0);c.size=new Vector3(width,height,.16f);
   // Bar renderers need no individual colliders.
   foreach(var b in bars.GetComponentsInChildren<BoxCollider>().Where(c=>c.gameObject!=blocker).ToArray())Object.DestroyImmediate(b);
   var door=Undo.AddComponent<NightDoorLock>(gate.gameObject);var so=new SerializedObject(door);so.FindProperty("shutter").objectReferenceValue=bars;so.FindProperty("showLegacyNotice").boolValue=false;so.ApplyModifiedPropertiesWithoutUndo();
  }
  var u=(new Vector2(-48.5f,112)-DomeC).normalized;var p=DomeC+u*14.4f;
  Gate("PracticeGate",new Vector3(p.x,0,p.y),Mathf.Atan2(u.x,u.y)*Mathf.Rad2Deg,5.8f,5.3f);
  // 2026-10-31 사용자: 밤 정비에도 소품실은 들어간다. 소품실·작업실 공용 복도(x -8.2) 대신 작업실 갈림길 입구만 막는다.
  Gate("WorkshopGate",new Vector3(-10.6f,0,93.4f),90,3.25f,4.1f);
  // 통로 북쪽 벽 끝(x -18.5)과 맞춰 둔다. 앞(x -17.4)에 두면 벽 끝과 철창 사이 1m 틈으로 빠져나갔다(2026-10-30).
  Gate("CargoGate",new Vector3(-18.75f,0,68.7f),90,3.8f,4.1f);
  Gate("BasementGate",new Vector3(5.25f,0,101.5f),0,3.5f,4.1f);
  var circuit=target.GetComponentsInChildren<Light>(true).Where(l=>l.type!=LightType.Directional && !l.transform.IsChildOf(root) && !new[]{"S02_Dormitory","S03_Shop","S08_ElevatorHall","LG_Lounge"}.Any(n=>{var t=target.Find(n);return t!=null&&l.transform.IsChildOf(t);})).ToArray();
  var power=Undo.AddComponent<NightPower>(root.gameObject);var ps=new SerializedObject(power);var list=ps.FindProperty("circuit");list.arraySize=circuit.Length;for(int i=0;i<circuit.Length;i++)list.GetArrayElementAtIndex(i).objectReferenceValue=circuit[i];ps.ApplyModifiedPropertiesWithoutUndo();
  // Small independent emergency pool near the route to the dormitory, always powered.
  var lamp=new GameObject("EmergencyDormLight");lamp.transform.SetParent(root,false);lamp.transform.position=new Vector3(5,3.2f,75);var light=lamp.AddComponent<Light>();light.type=LightType.Point;light.range=12;light.intensity=3;light.color=new Color(.65f,.75f,1);light.shadows=LightShadows.None;
  return $"4 gates, {circuit.Length} circuit lights; dorm/shop/expert/lounge remain powered";
 });
 }
}
