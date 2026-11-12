#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    /// <summary>실제 대기실 대신 메모리 지형에서 각성→1단계 오르기 허용→AI 순찰 오르기/내려오기, 좁은 소품 위 직접 오르기를 검사한다.</summary>
    public sealed class PursuitClimbRuntimePlayTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
        static object Get(object o, string n) => o.GetType().GetProperty(n, Flags)?.GetValue(o) ?? o.GetType().GetField(n, Flags)?.GetValue(o);
        static void Set(object o, string n, object value) { var p = o.GetType().GetProperty(n, Flags); if(p != null) p.SetValue(o,value); else o.GetType().GetField(n,Flags).SetValue(o,value); }
        static object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n,Flags).Invoke(o,args);
        [UnityTest]
        public IEnumerator RuntimeClimb_StageGateTraversalDropAndReplicaContacts()
        {
            var directorType = T("CurtainCall.WaitingRoom.Pursuit.PursuitDirector");
            Assert.IsTrue(directorType.GetProperty("Current").GetValue(null) == null, "기존 추격자가 없는 격리 실행이어야 한다.");
            var original = SceneManager.GetActiveScene();
            var scene = SceneManager.CreateScene("Pursuit runtime climbing fixture"); SceneManager.SetActiveScene(scene);
            var origin = new Vector3(-20000,0,20000);
            var root = new GameObject("격리 추격 검사"); root.SetActive(false);
            NavMeshData data = null; NavMeshDataInstance instance = default;
            var links = T("CurtainCall.WaitingRoom.Pursuit.PursuitClimbLinks");
            try
            {
                var sources = new List<NavMeshBuildSource>();
                void Box(Vector3 center, Vector3 size) {
                    var box = GameObject.CreatePrimitive(PrimitiveType.Cube); box.transform.SetParent(root.transform);
                    box.transform.position=origin+center; box.transform.localScale=size;
                    sources.Add(new NavMeshBuildSource { shape=NavMeshBuildSourceShape.Box, transform=Matrix4x4.TRS(origin+center,Quaternion.identity,Vector3.one), size=size });
                }
                Box(new Vector3(0,-.25f,0),new Vector3(12,.5f,12));
                Box(new Vector3(0,.6f,2),new Vector3(3,1.2f,3));
                var settings=NavMesh.GetSettingsByIndex(0); settings.agentRadius=.24f; settings.agentHeight=1.7f; settings.agentClimb=.55f;
                data=NavMeshBuilder.BuildNavMeshData(settings,sources,new Bounds(origin+Vector3.up*2,new Vector3(14,8,14)),Vector3.zero,Quaternion.identity);
                Assert.IsNotNull(data); instance=NavMesh.AddNavMeshData(data);
                var room=root.AddComponent(T("CurtainCall.WaitingRoom.Pursuit.PursuitRoom")); Set(room,"Count",0);
                var points=new[]{new GameObject("위쪽 순찰점").transform,new GameObject("아래쪽 순찰점").transform};
                points[0].position=origin+new Vector3(0,1.2f,2); points[1].position=origin+new Vector3(0,0,-2);
                foreach(var point in points) point.SetParent(root.transform);
                Set(room,"PatrolPoints",points);
                var director=(Behaviour)root.AddComponent(directorType); director.enabled=false;
                var rooms=Array.CreateInstance(room.GetType(),1); rooms.SetValue(room,0); Set(director,"Rooms",rooms);
                var graph=AssetDatabase.LoadAllAssetsAtPath("Assets/_ThirdParty/curtaincall/Pursuit/Behaviour/PursuitBehaviour.asset").First(a=>a.GetType().Name=="BehaviorGraph");
                Set(director,"BehaviourGraph",graph);
                // 2026-11-12 사용자 결정: 1~4단계 모두 오른다. 1단계(forcedStage 0)로 검사한다.
                root.SetActive(true); Set(director,"active",true); Set(director,"Elapsed",120f); Set(director,"forcedStage",0);
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_ThirdParty/curtaincall/Pursuit/Prefabs/Pursuit_Male_02.prefab");
                var hostGo=Object.Instantiate(prefab,root.transform); hostGo.transform.position=origin+new Vector3(0,0,-2);
                var actor=hostGo.GetComponent(T("CurtainCall.WaitingRoom.Pursuit.PursuitActor")); Call(actor,"Initialize",director,room,null);
                var replicaGo=Object.Instantiate(prefab,root.transform); replicaGo.transform.position=hostGo.transform.position;
                var replica=replicaGo.GetComponent(actor.GetType()); Call(replica,"Initialize",director,room,null);
                var gameSettings=T("CurtainCall.Settings.GameSettings"); var waiting=gameSettings.GetProperty("WaitingRoom").GetValue(null); var rules=Get(waiting,"Pursuit");
                Physics.SyncTransforms();
                Assert.Greater((int)links.GetMethod("Build").Invoke(null,new[]{(object)settings.agentTypeID,rules}),0);
                var nav=hostGo.GetComponent<NavMeshAgent>();
                float wakeEnd=Time.time+(float)Get(rules,"AwakeningSeconds")+.3f;
                while(Time.time<wakeEnd) { Call(actor,"ServerTick",120f,false); yield return null; }
                Assert.IsTrue(nav.enabled && nav.isOnNavMesh,"각성 후 실제 NavMesh 연결");
                Assert.AreNotEqual(0,nav.areaMask & (1<<2),"1단계부터 오르기 구역 사용");
                Call(actor,"ResetActor");
                bool climbed=false,dropped=false,finished=false; int hostContacts=0,remoteContacts=0;
                float end=Time.time+30;
                while(Time.time<end)
                {
                    Call(actor,"ServerTick",120f,false);
                    var motion=Get(actor,"State"); string name=motion.ToString();
                    Call(replica,"Receive",hostGo.transform.position,hostGo.transform.rotation,motion,null,0f,Vector3.zero,false,null);
                    if((bool)Get(actor,"IsClimbing")) {
                        climbed |= name=="Climb"; dropped |= name=="Drop";
                        Call(replica,"ReceiveTraversal",Get(actor,"TraversalFrom"),Get(actor,"TraversalTo"),Get(actor,"TraversalProgress"),Get(actor,"TraversalDuration"));
                    }
                    yield return null;
                    hostContacts=Math.Max(hostContacts,(int)Get(actor,"ClimbIkContacts")); remoteContacts=Math.Max(remoteContacts,(int)Get(replica,"ClimbIkContacts"));
                    Assert.Less((float)Get(actor,"ClimbIkError"),.015f);
                    if(dropped && !(bool)Get(actor,"IsClimbing") && hostGo.transform.position.y<.3f) { finished=true; break; }
                }
                Assert.IsTrue(climbed,"실제 오르기 링크 미통과"); Assert.IsTrue(dropped,"실제 내려오기 링크 미통과"); Assert.IsTrue(finished,"착지 후 복귀 실패");
                Assert.IsTrue(nav.isOnNavMesh && !nav.isOnOffMeshLink);
                Assert.Greater(hostContacts,0,"호스트 접점 없음"); Assert.Greater(remoteContacts,0,"복제본 접점 없음");
                Assert.AreEqual(0,(int)Get(actor,"ClimbIkContacts"),"통과 종료 후 IK 해제");
                Debug.Log("[PursuitClimbRuntime] 2단계 차단, 3단계 실제 Climb/Drop, 착지, 호스트/복제본 IK 확인");
            }
            finally
            {
                Object.DestroyImmediate(root); links.GetMethod("Clear").Invoke(null,null); instance.Remove();
                if(data!=null) Object.DestroyImmediate(data);
                if(original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
                SceneManager.UnloadSceneAsync(scene);
            }
        }

        /// <summary>
        /// 실제 대기실 베이크(반경 0.5m)처럼 윗면 NavMesh가 없는 1.94m 드럼통 위 플레이어를 1단계 추격자가 직접 올라가 따라가고,
        /// 플레이어가 내려가면 뛰어내려 NavMesh로 복귀한다.
        /// </summary>
        [UnityTest]
        public IEnumerator DirectClimb_NarrowTopWithoutNavMesh_ClimbsFollowsAndDrops()
        {
            var directorType = T("CurtainCall.WaitingRoom.Pursuit.PursuitDirector");
            Assert.IsTrue(directorType.GetProperty("Current").GetValue(null) == null, "기존 추격자가 없는 격리 실행이어야 한다.");
            var original = SceneManager.GetActiveScene();
            var scene = SceneManager.CreateScene("Pursuit direct climbing fixture"); SceneManager.SetActiveScene(scene);
            var origin = new Vector3(-23000,0,23000);
            var root = new GameObject("격리 직접 오르기 검사"); root.SetActive(false);
            NavMeshData data = null; NavMeshDataInstance instance = default;
            var links = T("CurtainCall.WaitingRoom.Pursuit.PursuitClimbLinks");
            try
            {
                var sources = new List<NavMeshBuildSource>();
                void Box(Vector3 center, Vector3 size) {
                    var box = GameObject.CreatePrimitive(PrimitiveType.Cube); box.transform.SetParent(root.transform);
                    box.transform.position=origin+center; box.transform.localScale=size;
                    sources.Add(new NavMeshBuildSource { shape=NavMeshBuildSourceShape.Box, transform=Matrix4x4.TRS(origin+center,Quaternion.identity,Vector3.one), size=size });
                }
                Box(new Vector3(0,-.25f,0),new Vector3(14,.5f,14));
                Box(new Vector3(0,.97f,2),new Vector3(1.2f,1.94f,1.2f));
                // 실제 대기실 추격 베이크 값: 좁은 윗면은 NavMesh가 생기지 않는다.
                var settings=NavMesh.GetSettingsByIndex(0); settings.agentRadius=.5f; settings.agentHeight=2f; settings.agentClimb=.75f;
                data=NavMeshBuilder.BuildNavMeshData(settings,sources,new Bounds(origin+Vector3.up*2,new Vector3(16,8,16)),Vector3.zero,Quaternion.identity);
                instance=NavMesh.AddNavMeshData(data);
                Assert.IsFalse(NavMesh.SamplePosition(origin+new Vector3(0,1.94f,2),out var onTop,.4f,NavMesh.AllAreas) && onTop.position.y>origin.y+1.5f, "드럼통 윗면에는 NavMesh가 없다");
                var room=root.AddComponent(T("CurtainCall.WaitingRoom.Pursuit.PursuitRoom")); Set(room,"Count",0);
                var points=new[]{new GameObject("순찰점 A").transform,new GameObject("순찰점 B").transform};
                // 1단계 시야(5m·100°) 안에서 드럼통 쪽을 보며 순찰한다.
                points[0].position=origin+new Vector3(0,0,-1.2f); points[1].position=origin+new Vector3(0,0,-2.5f);
                foreach(var point in points) point.SetParent(root.transform);
                Set(room,"PatrolPoints",points);
                var director=(Behaviour)root.AddComponent(directorType); director.enabled=false;
                var rooms=Array.CreateInstance(room.GetType(),1); rooms.SetValue(room,0); Set(director,"Rooms",rooms);
                var graph=AssetDatabase.LoadAllAssetsAtPath("Assets/_ThirdParty/curtaincall/Pursuit/Behaviour/PursuitBehaviour.asset").First(a=>a.GetType().Name=="BehaviorGraph");
                Set(director,"BehaviourGraph",graph);
                root.SetActive(true); Set(director,"active",true); Set(director,"Elapsed",0f); Set(director,"forcedStage",0);
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_ThirdParty/curtaincall/Pursuit/Prefabs/Pursuit_Male_02.prefab");
                var hostGo=Object.Instantiate(prefab,root.transform); hostGo.transform.SetPositionAndRotation(origin+new Vector3(0,0,-2.5f),Quaternion.identity);
                var actor=hostGo.GetComponent(T("CurtainCall.WaitingRoom.Pursuit.PursuitActor")); Call(actor,"Initialize",director,room,null);
                // 충돌체 없는 대리 플레이어(포획 확정은 런 권한이 없어 일어나지 않는다).
                var player=new GameObject("소품 위 플레이어"); player.transform.SetParent(root.transform);
                player.transform.position=origin+new Vector3(0,1.94f,2.2f);
                var candidates=(System.Collections.IList)directorType.GetField("candidates",Flags).GetValue(director); candidates.Clear(); candidates.Add(player);
                var rules=Get(T("CurtainCall.Settings.GameSettings").GetProperty("WaitingRoom").GetValue(null),"Pursuit");
                Assert.AreEqual(1,(int)Get(rules,"ClimbFromStage"),"공통 설정: 1단계부터 오르기");
                Physics.SyncTransforms();
                links.GetMethod("Build").Invoke(null,new[]{(object)settings.agentTypeID,rules});
                var nav=hostGo.GetComponent<NavMeshAgent>();
                bool climbed=false, perched=false, followed=false;
                float end=Time.time+20, traceAt=0; var trace=new List<string>();
                while(Time.time<end)
                {
                    Call(actor,"ServerTick",1f,false);
                    if(Time.time>=traceAt) { traceAt=Time.time+.5f;
                        trace.Add($"{Get(actor,"Behaviour")}/{Get(actor,"State")} p={(hostGo.transform.position-origin).ToString("F2")} goal={((Vector3)Get(actor,"NavigationGoal")-origin).ToString("F2")} nav={nav.enabled} approach={Get(actor,"hasDirectApproach")}"); }
                    climbed |= Get(actor,"State").ToString()=="Climb" && !nav.enabled;
                    perched |= (bool)Get(actor,"IsPerched");
                    var p=hostGo.transform.position; var q=player.transform.position;
                    if(perched && Mathf.Abs(p.y-q.y)<.15f && new Vector2(p.x-q.x,p.z-q.z).magnitude<.8f) { followed=true; break; }
                    Assert.Less((float)Get(actor,"ClimbIkError"),.015f);
                    yield return null;
                }
                Assert.IsTrue(climbed,"직접 오르기(Climb) 미실행\n"+string.Join("\n",trace)); Assert.IsTrue(perched,"윗면 착지 실패"); Assert.IsTrue(followed,"윗면에서 플레이어 옆까지 이동 실패");
                Assert.AreEqual("Chase",Get(actor,"Behaviour").ToString(),"윗면에서도 추격 상태(포획 가능)");
                // 플레이어가 반대쪽 바닥으로 내려가면 뛰어내려 NavMesh로 복귀한다.
                player.transform.position=origin+new Vector3(-3,0,-2);
                bool dropped=false, back=false; end=Time.time+10;
                while(Time.time<end)
                {
                    Call(actor,"ServerTick",1f,false);
                    dropped |= Get(actor,"State").ToString()=="Drop";
                    if(dropped && nav.enabled && nav.isOnNavMesh && !(bool)Get(actor,"IsPerched") && !(bool)Get(actor,"IsClimbing")) { back=true; break; }
                    yield return null;
                }
                Assert.IsTrue(dropped,"뛰어내리기 미실행"); Assert.IsTrue(back,"바닥 NavMesh 복귀 실패");
                Assert.Less(hostGo.transform.position.y-origin.y,.3f);
                Debug.Log("[PursuitClimbRuntime] NavMesh 없는 1.94m 윗면 직접 오르기·윗면 추격·뛰어내려 복귀 확인");
            }
            finally
            {
                Object.DestroyImmediate(root); links.GetMethod("Clear").Invoke(null,null); instance.Remove();
                if(data!=null) Object.DestroyImmediate(data);
                if(original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
                SceneManager.UnloadSceneAsync(scene);
            }
        }
    }
}
#endif
