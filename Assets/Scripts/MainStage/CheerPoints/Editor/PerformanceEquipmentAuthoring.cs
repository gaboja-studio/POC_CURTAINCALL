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
            var catalog=AssetDatabase.LoadAssetAtPath<ItemCatalog>("Assets/Resources/Settings/GameSettings/Items/ItemCatalog.asset");
            foreach(var e in catalog.Items)
            {
                string description=e.id switch {
                    ItemUses.SpringShoes=>"공중에서 사용하면 한 번 더 점프합니다. 착지 전까지 1회만 사용할 수 있으며, 실제 추가 점프 시 내구도 1을 소모합니다.",
                    ItemUses.RescueGloves=>"안전한 바닥에서 바라보는 가까운 동료를 내 주변의 안전한 위치로 끌어옵니다. 공중·매달림 상태의 동료와 빈 착지 공간이 있을 때만 발동하며 내구도 1을 소모합니다.",
                    ItemUses.StickyGloves=>"매달린 상태에서 사용하면 3초 동안 매달림 제한시간이 줄어들지 않습니다. 한 번의 매달림에 1회만 사용할 수 있으며 발동 시 내구도 1을 소모합니다. 주변 위험물은 멈추지 않습니다.",
                    ItemUses.BalloonPants=>"공중에서 사용하면 최대 3초 동안 하강이 느려지고 수평 조작이 강화됩니다. 같은 슬롯 키를 다시 누르면 접습니다. 한 체공에 1회만 사용할 수 있으며 발동 시 내구도 1을 소모합니다.", _=>null };
                if(description==null) continue;
                e.summary=e.shopDescription=e.shopTooltip=description;
                e.useCue=e.id switch { ItemUses.SpringShoes=>"한 번 더!", ItemUses.RescueGloves=>"잡았다!", ItemUses.StickyGloves=>"아직 붙어 있다!", _=>"사뿐하게!" };
            }
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
            return "Configured 3 visuals, 4 descriptions, Q/E and GameSettings link.";
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

    public sealed class PerformanceCheerWindow : EditorWindow
    {
        bool equipment;
        [MenuItem("CurtainCall/Performance/Cheer Point Placement")]
        static void Open() => GetWindow<PerformanceCheerWindow>("환호 포인트");
        void OnGUI()
        {
            var settings=AssetDatabase.LoadAssetAtPath<PerformanceEquipmentSettings>(PerformanceEquipmentAuthoring.SettingsPath);
            if(!settings) { EditorGUILayout.HelpBox("공연 기물 설정 에셋이 없습니다.",MessageType.Warning); return; }
            EditorGUI.BeginChangeCheck();
            var style=(CheerPointStyle)EditorGUILayout.EnumPopup("외형",settings.cheerStyle);
            if(EditorGUI.EndChangeCheck()) { Undo.RecordObject(settings,"Change cheer style"); settings.cheerStyle=style; EditorUtility.SetDirty(settings); }
            if(GUILayout.Button("GameSettings 수치 열기")) Selection.activeObject=settings;
            equipment=EditorGUILayout.Toggle("기물 활용 경로",equipment);
            EditorGUILayout.HelpBox("고리의 파란 Z축이 통과 방향입니다. Scene 뷰에서 위치·회전을 조정하세요. 불꽃은 피해를 주지 않습니다.",MessageType.Info);
            using(new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                if(GUILayout.Button("Scene 뷰 중심에 환호 고리 추가"))
                {
                    var scene=SceneManager.GetActiveScene(); string trick=scene.name;
                    if(trick!="Tightrope" && trick!="HumanCannon") { ShowNotification(new GUIContent("공연 씬을 활성화하세요.")); return; }
                    var parent=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="PerformanceCheerPoints");
                    if(!parent) { parent=new GameObject("PerformanceCheerPoints"); Undo.RegisterCreatedObjectUndo(parent,"Create cheer root"); }
                    var point=PerformanceEquipmentAuthoring.AddPoint(parent.transform,trick,parent.transform.childCount,SceneView.lastActiveSceneView ? SceneView.lastActiveSceneView.pivot : Vector3.zero,Quaternion.identity,equipment);
                    Undo.RegisterCreatedObjectUndo(point.gameObject,"Add cheer point"); Selection.activeGameObject=point.gameObject; EditorSceneManager.MarkSceneDirty(scene);
                }
            foreach(var point in UnityEngine.Object.FindObjectsByType<PerformanceCheerPoint>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(point && GUILayout.Button($"{point.gameObject.scene.name} / {point.name}")) { Selection.activeGameObject=point.gameObject; SceneView.lastActiveSceneView?.FrameSelected(); }
        }
    }
}
#endif
