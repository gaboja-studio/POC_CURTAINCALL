using CurtainCall.LevelPreview;
using CurtainCall.Network.Session;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.WaitingRoom.EditorTools
{
    /// <summary>
    /// 5단계 테스트 씬(전문가 서비스)을 4인 접속으로 확인할 수 있게 구성한다. 다시 실행해도 같은 결과가 된다.
    /// - 대기실 맵(1단계 그레이박스, ⑧ 전문가 대기실 포함)과 3단계 상점·소지, 4단계 밤 정비·스케줄표는 4단계 테스트 씬에서 그대로 가져온다.
    ///   4단계 씬을 추가로 열어 해당 루트를 이 씬으로 옮긴 뒤 4단계 씬은 저장하지 않고 닫는다. 루트끼리의 참조(스케줄표 판 ↔ 클로즈업 카메라 등)가 그대로 남는다.
    ///   앞 단계 코드를 직접 고치지 않으려는 것이며, 3·4단계가 바뀌면 이 메뉴를 다시 실행한다.
    /// - 맵 확인용 PlaySetup을 끄고, 카메라는 CinemachineBrain + 공용 PlayerFollowCamera로 바꾼다(2~4단계 씬과 같다).
    /// - 5단계: 서비스 상태 `ExpertService`(<see cref="ExpertService"/>), ⑧ 문·입구·표지·소리(<see cref="ExpertRoomView"/>),
    ///   방 안 황금빛 조명, ③ 수화기 옆 서비스 칸(<see cref="ExpertServicePhonePanel"/>)을 둔다. ⑧ 문 자리에는 늘 켜진 보이지 않는 막을 세워 걸어서는 못 들어가게 한다.
    /// - 접속 패널(공용), 밤까지 건너뛰는 4단계 테스트 패널, 5단계 테스트 패널을 둔다.
    /// </summary>
    public static class ExpertServiceSceneBuilder
    {
        const string TargetScene = "Assets/Scenes/Tests/WaitingRoom/ExpertService/WaitingRoomExpertService.unity";
        const string NightScene = "Assets/Scenes/Tests/WaitingRoom/NightSchedule/WaitingRoomNightSchedule.unity";
        const string FollowCameraPrefab = "Assets/Resources/Prefabs/Controllers/Camera/PlayerFollowCamera.prefab";
        const string PanelsName = "ExpertServiceTestPanels";
        const string ServiceName = "ExpertService";
        const string ExpertRoomPath = "S08_ExpertRoom";
        const string DoorName = "ExpertRoomDoor_Closed";
        const string PhonePath = "S03_Shop/Phone";

        /// <summary>문 가운데에서 방 안(+X, 구매자가 들어가 서는 곳)·문 앞 로비(-X, 다시 나타나는 곳)까지.</summary>
        const float InsideOffset = 1.6f, OutsideOffset = 1.4f;

        /// <summary>4단계 씬에서 옮겨 오는 루트 오브젝트(맵·상점·밤 정비·스케줄표).</summary>
        static readonly string[] CopiedRoots = { "WaitingRoomGreybox", "ShopStorage", "ShopStorageViews", "NightSchedule", "ScheduleState" };

        [MenuItem("Tools/CurtainCall/대기실/5단계 전문가 서비스 테스트 씬 구성")]
        static void Build()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != TargetScene)
            {
                EditorUtility.DisplayDialog("5단계 씬 구성", $"먼저 {TargetScene}을 열어 주세요.", "확인");
                return;
            }

            var playSetup = FindRoot(scene, "PlaySetup");
            if (playSetup != null) Undo.RecordObject(playSetup, "Disable PlaySetup");
            if (playSetup != null) playSetup.SetActive(false);

            SetUpCamera(scene);
            if (!CopyFromNightScene(scene)) return;
            if (!SetUpExpertRoom(scene)) return;
            SetUpPanels(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[ExpertService] 5단계 테스트 씬을 구성했습니다. 저장(Cmd+S)하세요.");
        }

        static void SetUpCamera(Scene scene)
        {
            var main = FindRoot(scene, "Main Camera");
            if (main != null)
            {
                if (!main.TryGetComponent<CinemachineBrain>(out _)) Undo.AddComponent<CinemachineBrain>(main);
                if (main.TryGetComponent<LevelPreviewCamera>(out var preview) && preview.enabled)
                {
                    Undo.RecordObject(preview, "Disable LevelPreviewCamera");
                    preview.enabled = false; // 맵 확인용. 내 캐릭터 카메라와 겹치지 않게 끈다.
                }
            }

            if (FindRoot(scene, "PlayerFollowCamera") != null) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FollowCameraPrefab);
            if (prefab == null) { Debug.LogError($"[ExpertService] {FollowCameraPrefab} 없음"); return; }
            var follow = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Undo.RegisterCreatedObjectUndo(follow, "Add PlayerFollowCamera");
        }

        /// <summary>
        /// 4단계 씬을 추가로 열고 <see cref="CopiedRoots"/>를 이 씬으로 옮긴다. 이미 있던 같은 이름 루트는 지우고 새로 받는다.
        /// 옮기기는 되돌리기(Undo)에 남기지 않는다 — 다시 받으려면 메뉴를 다시 실행한다.
        /// </summary>
        static bool CopyFromNightScene(Scene scene)
        {
            var source = EditorSceneManager.OpenScene(NightScene, OpenSceneMode.Additive);
            try
            {
                var roots = new GameObject[CopiedRoots.Length];
                for (int i = 0; i < CopiedRoots.Length; i++)
                {
                    roots[i] = FindRoot(source, CopiedRoots[i]);
                    if (roots[i] == null) { Debug.LogError($"[ExpertService] 4단계 씬에 {CopiedRoots[i]} 없음"); return false; }
                }

                foreach (var name in CopiedRoots)
                {
                    var old = FindRoot(scene, name);
                    if (old != null) Object.DestroyImmediate(old);
                }
                foreach (var root in roots) SceneManager.MoveGameObjectToScene(root, scene);
                return true;
            }
            finally
            {
                EditorSceneManager.CloseScene(source, true); // 4단계 씬은 저장하지 않고 닫는다(파일은 그대로).
                SceneManager.SetActiveScene(scene);
            }
        }

        /// <summary>
        /// 서비스 상태(씬 최상위), ⑧ 방 표시(그레이박스 S08 묶음), ③ 수화기 옆 서비스 칸. 다시 실행하면 지우고 새로 만든다.
        /// </summary>
        static bool SetUpExpertRoom(Scene scene)
        {
            var greybox = FindRoot(scene, "WaitingRoomGreybox");
            var room = greybox != null ? greybox.transform.Find(ExpertRoomPath) : null;
            var door = room != null ? room.Find(DoorName) : null;
            var phone = greybox != null ? greybox.transform.Find(PhonePath) : null;
            if (room == null || door == null || phone == null) { Debug.LogError($"[ExpertService] 그레이박스에 {ExpertRoomPath}/{DoorName} 또는 {PhonePath} 없음"); return false; }

            var oldService = FindRoot(scene, ServiceName);
            if (oldService != null) Undo.DestroyObjectImmediate(oldService);
            var service = new GameObject(ServiceName);
            SceneManager.MoveGameObjectToScene(service, scene);
            Undo.RegisterCreatedObjectUndo(service, "Add " + ServiceName);
            service.AddComponent<ExpertService>();

            foreach (var name in new[] { "ExpertRoomBarrier", "ExpertRoomInside", "ExpertRoomOutside", "ExpertRoomSign", "ExpertRoomSound", "ExpertRoomGlow" })
            {
                var old = room.Find(name);
                if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            }
            foreach (var old in room.GetComponents<ExpertRoomView>()) Undo.DestroyObjectImmediate(old);

            // 문 자리 막: 문이 열려도 걸어서는 지나갈 수 없다(구매자는 입구 F로 순간이동해 들어간다).
            var barrier = new GameObject("ExpertRoomBarrier");
            barrier.transform.SetParent(room, false);
            barrier.transform.localPosition = door.localPosition;
            var block = barrier.AddComponent<BoxCollider>();
            block.size = door.localScale;
            Undo.RegisterCreatedObjectUndo(barrier, "Add ExpertRoomBarrier");

            var right = room.right; // 그레이박스 +X = 로비 → ⑧ 방 안
            var inside = Point(room, "ExpertRoomInside", door.position + right * InsideOffset, Quaternion.LookRotation(right));
            var outside = Point(room, "ExpertRoomOutside", door.position - right * OutsideOffset, Quaternion.LookRotation(-right));
            foreach (var point in new[] { inside, outside })
                point.position = new Vector3(point.position.x, room.position.y, point.position.z); // 바닥 높이

            // 문 위 상태 표지(로비 쪽 면, 기존 "⑧ 전문가 대기실" 간판 아래)
            var signObject = new GameObject("ExpertRoomSign");
            signObject.transform.SetParent(room, false);
            signObject.transform.position = door.position - right * .2f + Vector3.up * 1.75f;
            signObject.transform.rotation = Quaternion.LookRotation(right);
            var sign = signObject.AddComponent<TextMesh>();
            sign.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var textMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Tests/WaitingRoom/Base/Materials/GB_Text.mat");
            signObject.GetComponent<MeshRenderer>().sharedMaterial = textMaterial != null ? textMaterial : sign.font.material;
            sign.text = "밤 정비 때 영업";
            sign.characterSize = .1f;
            sign.fontSize = 48;
            sign.anchor = TextAnchor.MiddleCenter;
            sign.alignment = TextAlignment.Center;
            sign.color = new Color(1f, .85f, .2f);
            Undo.RegisterCreatedObjectUndo(signObject, "Add ExpertRoomSign");

            var soundObject = new GameObject("ExpertRoomSound");
            soundObject.transform.SetParent(room, false);
            soundObject.transform.position = door.position;
            var sound = soundObject.AddComponent<AudioSource>();
            sound.playOnAwake = false;
            sound.spatialBlend = 1f;
            sound.maxDistance = 25f;
            Undo.RegisterCreatedObjectUndo(soundObject, "Add ExpertRoomSound");

            // 방 안 황금빛(문이 열린 만큼 밝아진다). 편집 화면에서는 꺼 둔다.
            var glowObject = new GameObject("ExpertRoomGlow");
            glowObject.transform.SetParent(room, false);
            glowObject.transform.position = new Vector3(0f, room.position.y + 2.2f, 0f) + Flat(door.position + right * 1.8f);
            var glow = glowObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, .76f, .32f);
            glow.range = 9f;
            glow.intensity = 0f;
            glow.shadows = LightShadows.None;
            glow.enabled = false;
            Undo.RegisterCreatedObjectUndo(glowObject, "Add ExpertRoomGlow");

            var view = Undo.AddComponent<ExpertRoomView>(room.gameObject);
            var so = new SerializedObject(view);
            so.FindProperty("door").objectReferenceValue = door;
            so.FindProperty("inside").objectReferenceValue = inside;
            so.FindProperty("outside").objectReferenceValue = outside;
            so.FindProperty("sign").objectReferenceValue = sign;
            so.FindProperty("sound").objectReferenceValue = sound;
            so.FindProperty("glow").objectReferenceValue = glow;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (!phone.TryGetComponent<ExpertServicePhonePanel>(out _)) Undo.AddComponent<ExpertServicePhonePanel>(phone.gameObject);
            return true;
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

        static Transform Point(Transform parent, string name, Vector3 position, Quaternion rotation)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            Undo.RegisterCreatedObjectUndo(go, "Add " + name);
            return go.transform;
        }

        static void SetUpPanels(Scene scene)
        {
            var panels = FindRoot(scene, PanelsName);
            if (panels == null)
            {
                panels = new GameObject(PanelsName);
                SceneManager.MoveGameObjectToScene(panels, scene);
                Undo.RegisterCreatedObjectUndo(panels, "Add " + PanelsName);
            }
            if (!panels.TryGetComponent<NetworkSessionTestUI>(out _)) Undo.AddComponent<NetworkSessionTestUI>(panels);
            if (!panels.TryGetComponent<NightScheduleTestUI>(out _)) Undo.AddComponent<NightScheduleTestUI>(panels);
            if (!panels.TryGetComponent<ExpertServiceTestUI>(out _)) Undo.AddComponent<ExpertServiceTestUI>(panels);
        }

        static GameObject FindRoot(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == name) return root;
            return null;
        }
    }
}
