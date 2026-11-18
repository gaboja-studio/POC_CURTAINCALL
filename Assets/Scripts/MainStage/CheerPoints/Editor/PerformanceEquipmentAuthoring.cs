#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using CurtainCall.Settings;
using CurtainCall.Tightrope;
using CurtainCall.WaitingRoom;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;

namespace CurtainCall.MainStage.CheerPoints.Editor
{
    public static class PerformanceEquipmentAuthoring
    {
        public const string SettingsPath="Assets/Resources/Settings/GameSettings/Items/PerformanceEquipment.asset";
        public const string RopeScene="Assets/Scenes/Tests/MainStage/Tightrope/Tightrope.unity";
        public const string CannonScene="Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity";
        public static string Configure()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Stop play mode.");
            CheerPointConceptBuilder.Build();
            var settings=AssetDatabase.LoadAssetAtPath<PerformanceEquipmentSettings>(SettingsPath);
            if(!settings) { settings=ScriptableObject.CreateInstance<PerformanceEquipmentSettings>(); AssetDatabase.CreateAsset(settings,SettingsPath); }
            settings.cheerVisuals=new[]{"A_FireHoop","B_EncoreHalo","C_MaskApparition"}.Select(n=>AssetDatabase.LoadAssetAtPath<GameObject>(CheerPointConceptBuilder.Folder+"/"+n+".prefab")).ToArray();
            EditorUtility.SetDirty(settings);
            var game=new SerializedObject(GameSettings.Current);
            game.FindProperty("performanceEquipment").objectReferenceValue=settings; game.ApplyModifiedPropertiesWithoutUndo();
            const string inputPath="Assets/Resources/Input/PlayerControls.inputactions";
            var inputs=InputActionAsset.FromJson(File.ReadAllText(inputPath));
            var map=inputs.FindActionMap("Common",true);
            if(map.FindAction("UseSlotOne")==null) map.AddAction("UseSlotOne",InputActionType.Button,"<Keyboard>/q",expectedControlLayout:"Button");
            if(map.FindAction("UseSlotTwo")==null) map.AddAction("UseSlotTwo",InputActionType.Button,"<Keyboard>/e",expectedControlLayout:"Button");
            File.WriteAllText(inputPath,inputs.ToJson()); UnityEngine.Object.DestroyImmediate(inputs); AssetDatabase.ImportAsset(inputPath);
            ApplyDescriptions();
            return "Configured 3 visuals, 4 descriptions, Q/E and GameSettings link.";
        }

        const string Use="슬롯 사용키(1슬롯 Q · 2슬롯 E)", Where="공연 · 연습실(연습은 내구도 소모 없음)";

        /// <summary>
        /// 공연 기물 4종의 상점 설명·툴팁(기획 "공연 기물 고도화" Confluence 44793873, 2026-11-18).
        /// 형식은 상점 공통(2026-11-02 사용자): 설명 = 문장 · 빈 줄 · 적용 · 사용 · 내구도 · 소모 조건, 툴팁 = 문장 · "사용: … · 내구도: …".
        /// </summary>
        public static string ApplyDescriptions()
        {
            var catalog=AssetDatabase.LoadAssetAtPath<ItemCatalog>("Assets/Resources/Settings/GameSettings/Items/ItemCatalog.asset");
            int changed=0;
            foreach(var e in catalog.Items)
            {
                (string sentence,string tip,string spend)? text=e.id switch {
                    ItemUses.SpringShoes=>("공중에서 한 번 더 점프합니다. 착지 전에는 다시 사용할 수 없습니다.",
                        "공중에서 한 번 더 점프합니다.","실제로 추가 점프할 때 내구도 소모"),
                    ItemUses.RescueGloves=>("안전한 바닥에서 앞쪽의 가까운 동료를 내 곁으로 부드럽게 끌어옵니다. 당길 수 있는 동료에게는 나에게만 장갑 아이콘과 사용키가 보입니다.",
                        "앞쪽의 가까운 동료를 내 곁으로 끌어옵니다.","길과 도착할 자리가 안전해 실제로 당길 때 내구도 소모"),
                    ItemUses.StickyGloves=>("매달린 상태에서 사용하면 남은 매달림 시간이 3초 동안 줄어들지 않습니다. 주변 위험물은 멈추지 않습니다.",
                        "매달려 있을 때 3초 동안 매달림 시간이 멈춥니다.","한 번 매달리는 동안 한 번, 사용할 때 내구도 소모"),
                    ItemUses.BalloonPants=>("공중에서 바지가 부풀어 최대 3초 동안 천천히 내려오고 좌우로 더 쉽게 움직입니다. 같은 키를 다시 누르거나 착지하면 접힙니다.",
                        "공중에서 천천히 내려오며 좌우로 더 쉽게 움직입니다.","착지 전 한 번, 펼칠 때 내구도 소모(접을 때는 소모 없음)"),
                    _=>null };
                if(text==null) continue;
                var (sentence,tip,spend)=text.Value;
                e.summary=e.shopDescription=$"{sentence}\n\n• 적용: {Where}\n• 사용: {Use}\n• 내구도: {e.newUses}\n• {spend}";
                e.shopTooltip=$"{tip}\n사용: {Use} · 내구도: {e.newUses}";
                e.useCue=e.id switch { ItemUses.SpringShoes=>"한 번 더!", ItemUses.RescueGloves=>"잡았다!", ItemUses.StickyGloves=>"아직 붙어 있다!", _=>"사뿐하게!" };
                changed++;
            }
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
            return $"Performance item descriptions: {changed}";
        }

        public static string InspectCourses()
        {
            string result="";
            foreach(var path in new[]{RopeScene,CannonScene})
            {
                var existing=SceneManager.GetSceneByPath(path); bool opened=!existing.isLoaded;
                var scene=opened ? EditorSceneManager.OpenScene(path,OpenSceneMode.Additive) : existing;
                try {
                    var components=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Component>(true)).ToArray();
                    result+="\n"+path+"\n";
                    var course=components.OfType<TightropeCourse>().FirstOrDefault();
                    if(course) { result+=$"lanes={course.LaneCount} finish={course.FinishDistance}\n"; for(int lane=0;lane<course.LaneCount;lane++) result+=$"lane {lane} start={course.GetLanePosition(lane,0)} end={course.Layout.GetLaneEnd(lane)} at12={course.GetLanePosition(lane,12)}\n"; }
                    result+=string.Join("\n",components.OfType<CurtainCall.HumanCannon.HumanCannonStation>().Select(s=>$"CANNON {s.name} muzzle={s.Muzzle.position} velocity={s.LaunchVelocity}"))+"\n";
                    result+=string.Join("\n",components.OfType<Transform>().Where(t=>t.name.Contains("Platform") || t.name.Contains("Station") || t.name.Contains("Trampoline") || t.name.Contains("Boarding") || t.name.Contains("Pole")).Take(75).Select(t=>$"{t.name} {t.position}"));
                } finally { if(opened) EditorSceneManager.CloseScene(scene,true); }
            }
            return result;
        }

        public static string PlacePoints()
        {
            if(EditorApplication.isPlaying || Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty)) throw new InvalidOperationException("Save scenes and stop play mode first.");
            var original=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach(var path in new[]{RopeScene,CannonScene})
                {
                    var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
                    if(scene.GetRootGameObjects().Any(g=>g.name=="PerformanceCheerPoints")) continue; // Never overwrite hand placement.
                    var root=new GameObject("PerformanceCheerPoints");
                    if(path==RopeScene)
                    {
                        var course=UnityEngine.Object.FindFirstObjectByType<TightropeCourse>();
                        var distances=new[]{12f,58f,148f,22f,154f};
                        for(int i=0;i<5;i++)
                        {
                            int lane=new[]{2,3,2,3,3}[i]; float d=distances[i];
                            if(!course.IsLaneUsable(lane,d)) throw new InvalidOperationException($"No rope at lane {lane}, {d}");
                            AddPoint(root.transform,"Tightrope",i,course.GetLanePosition(lane,d)+Vector3.up*(i>=3 ? 4.1f : 1f),Quaternion.LookRotation(course.Forward),i>=3);
                        }
                    }
                    else
                    {
                        var stations=UnityEngine.Object.FindObjectsByType<CurtainCall.HumanCannon.HumanCannonStation>(FindObjectsSortMode.None)
                            .Where(s=>s.isActiveAndEnabled).OrderBy(s=>s.Muzzle.position.z).ThenBy(s=>Mathf.Abs(s.Muzzle.position.x)).ToArray();
                        var groups=stations.GroupBy(s=>Mathf.RoundToInt(s.BoardingPoint.position.z/10f)).Select(g=>g.First()).ToArray();
                        if(groups.Length<3) throw new InvalidOperationException("Expected 3 cannon sections.");
                        for(int i=0;i<5;i++)
                        {
                            var station=groups[i<3 ? i : i-3]; var velocity=station.LaunchVelocity;
                            float t=i<3 ? .6f : 1.05f;
                            Vector3 pos=station.Muzzle.position+velocity*t+Vector3.down*(.5f*GameSettings.Base.Character.Gravity*t*t)+Vector3.up*(i>=3 ? 3.9f : .9f);
                            AddPoint(root.transform,"HumanCannon",i,pos,Quaternion.LookRotation(Vector3.ProjectOnPlane(velocity,Vector3.up)),i>=3);
                        }
                    }
                    EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                }
            } finally { EditorSceneManager.RestoreSceneManagerSetup(original); }
            return "Placed 5 points per performance (3 regular / 2 equipment routes).";
        }

        public static PerformanceCheerPoint AddPoint(Transform parent,string trick,int index,Vector3 position,Quaternion rotation,bool equipment)
        {
            var go=new GameObject($"Cheer_{index+1:00}_{(equipment ? "Equipment" : "Regular")}");
            go.transform.SetParent(parent,false); go.transform.SetPositionAndRotation(position,rotation);
            var point=go.AddComponent<PerformanceCheerPoint>(); point.pointId=Guid.NewGuid().ToString("N"); point.trickId=trick; point.equipmentRoute=equipment;
            return point;
        }

        public static string CaptureLive(string trick)
        {
            if(!EditorApplication.isPlaying) throw new InvalidOperationException("Capture the live performance, not an uninitialized scene.");
            var point=PerformanceCheerPoint.All.Where(p=>p && p.trickId==trick && !p.equipmentRoute).OrderBy(p=>p.transform.position.z).First();
            var camera=new GameObject("CheerCaptureCamera").AddComponent<Camera>();
            try
            {
                if(Camera.main) camera.CopyFrom(Camera.main);
                camera.enabled=false; camera.fieldOfView=52;
                camera.transform.position=point.transform.position+point.transform.rotation*new Vector3(-4,2.4f,-8);
                camera.transform.LookAt(point.transform.position+Vector3.up*.4f);
                camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
                string directory=Path.GetFullPath("Temp/PerformanceEquipment"); Directory.CreateDirectory(directory);
                string path=Path.Combine(directory,trick+"-fire-hoop.png");
                CheerPointConceptBuilder.Render(camera,path); return path;
            } finally { UnityEngine.Object.DestroyImmediate(camera.gameObject); }
        }
    }

}
#endif
