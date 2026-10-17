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
    /// 4단계 테스트 씬(밤 정비·스케줄표)을 4인 접속으로 확인할 수 있게 구성한다. 다시 실행해도 같은 결과가 된다.
    /// - 1단계 그레이박스가 없으면 1단계 메뉴로 만든다.
    /// - 맵 확인용 PlaySetup을 끄고, 카메라는 CinemachineBrain + 공용 PlayerFollowCamera로 바꾼다(2·3단계 씬과 같다).
    /// - 3단계 상점·소지(상태·표시 오브젝트, ③ 수화기·배송구·④ 선반 컴포넌트)는 3단계 테스트 씬에서 그대로 복사한다.
    ///   3단계 코드를 직접 참조하지 않으려는 것이며, 3단계가 바뀌면 이 메뉴를 다시 실행한다.
    /// - 대기실 문(공장식 롤링 셔터)과 밤 정비 때 셔터를 내리는 `NightSchedule`(<see cref="NightDoorLock"/>)을 둔다.
    /// - 스케줄표 상태 `ScheduleState`(<see cref="ScheduleBoard"/>), 로비 스케줄표 판의 <see cref="ScheduleBoardView"/>와 클로즈업 카메라를 둔다.
    /// - 접속 패널(공용)과 4단계 테스트 패널을 둔다.
    /// </summary>
    public static class NightScheduleSceneBuilder
    {
        const string TargetScene = "Assets/Scenes/Tests/WaitingRoom/NightSchedule/WaitingRoomNightSchedule.unity";
        const string ShopScene = "Assets/Scenes/Tests/WaitingRoom/ShopStorage/WaitingRoomShopStorage.unity";
        const string GreyboxMenu = "Tools/CurtainCall/대기실/그레이박스 다시 만들기";
        const string FollowCameraPrefab = "Assets/Resources/Prefabs/Controllers/Camera/PlayerFollowCamera.prefab";
        const string GreyboxName = "WaitingRoomGreybox";
        const string PanelsName = "NightScheduleTestPanels";
        const string NightRootName = "NightSchedule";
        const string MaterialFolder = "Assets/Scenes/Tests/WaitingRoom/Base/Materials";
        const string ScheduleStateName = "ScheduleState";
        const string BoardPath = "S01_Lobby/ScheduleBoard";

        /// <summary>스케줄표 판 가운데에서 클로즈업 카메라까지(그레이박스 기준, 판은 북쪽 벽이라 로비 쪽 -Z).</summary>
        static readonly Vector3 CloseUpOffset = new(0f, 0f, -1.7f);

        /// <summary>그레이박스 기준 대기실 문(로비 ↔ ⑥) 바닥 가운데. 1단계 벽 W_x+10의 출입구(폭 3·높이 3)와 같다.</summary>
        static readonly Vector3 DoorBase = new(10f, 0f, 0f);

        /// <summary>3단계 씬에서 통째로 복사하는 루트 오브젝트.</summary>
        static readonly string[] ShopRoots = { "ShopStorage", "ShopStorageViews" };

        /// <summary>그레이박스 안 같은 경로에 3단계 컴포넌트만 복사해 붙이는 오브젝트.</summary>
        static readonly string[] ShopFixtures = { "S03_Shop/Phone", "S03_Shop/DeliverySlot", "S04_PropRoom/HandoverShelf_4Tier" };

        [MenuItem("Tools/CurtainCall/대기실/4단계 밤 정비 테스트 씬 구성")]
        static void Build()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != TargetScene)
            {
                EditorUtility.DisplayDialog("4단계 씬 구성", $"먼저 {TargetScene}을 열어 주세요.", "확인");
                return;
            }

            var greybox = FindRoot(scene, GreyboxName);
            if (greybox == null)
            {
                EditorApplication.ExecuteMenuItem(GreyboxMenu);
                greybox = FindRoot(scene, GreyboxName);
                if (greybox == null) { Debug.LogError("[NightSchedule] 그레이박스를 만들지 못했습니다."); return; }
            }

            var playSetup = FindRoot(scene, "PlaySetup");
            if (playSetup != null) Undo.RecordObject(playSetup, "Disable PlaySetup");
            if (playSetup != null) playSetup.SetActive(false);

            SetUpCamera(scene);
            if (!CopyShop(scene, greybox)) return;
            SetUpNightDoor(scene, greybox);
            SetUpSchedule(scene, greybox);
            SetUpPanels(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[NightSchedule] 4단계 테스트 씬을 구성했습니다. 저장(Cmd+S)하세요.");
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
            if (prefab == null) { Debug.LogError($"[NightSchedule] {FollowCameraPrefab} 없음"); return; }
            var follow = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Undo.RegisterCreatedObjectUndo(follow, "Add PlayerFollowCamera");
        }

        static bool CopyShop(Scene scene, GameObject greybox)
        {
            var source = EditorSceneManager.OpenScene(ShopScene, OpenSceneMode.Additive);
            try
            {
                var sourceGreybox = FindRoot(source, GreyboxName);
                if (sourceGreybox == null) { Debug.LogError("[NightSchedule] 3단계 씬에 그레이박스가 없습니다."); return false; }

                foreach (var name in ShopRoots)
                {
                    var original = FindRoot(source, name);
                    if (original == null) { Debug.LogError($"[NightSchedule] 3단계 씬에 {name} 없음"); return false; }
                    var old = FindRoot(scene, name);
                    if (old != null) Undo.DestroyObjectImmediate(old);
                    var copy = Object.Instantiate(original);
                    copy.name = name;
                    SceneManager.MoveGameObjectToScene(copy, scene);
                    Undo.RegisterCreatedObjectUndo(copy, "Copy " + name);
                }

                foreach (var path in ShopFixtures)
                {
                    var from = sourceGreybox.transform.Find(path);
                    var to = greybox.transform.Find(path);
                    if (from == null || to == null) { Debug.LogError($"[NightSchedule] {path} 없음(3단계 씬 또는 그레이박스)"); return false; }
                    CopyBehaviours(from.gameObject, to.gameObject);
                }
                return true;
            }
            finally
            {
                EditorSceneManager.CloseScene(source, true); // 3단계 씬은 열어 보기만 하고 저장하지 않는다.
                SceneManager.SetActiveScene(scene);
            }
        }

        /// <summary>도형(Transform·Mesh·Collider) 밖의 컴포넌트를 같은 종류끼리 덮어쓰거나 새로 붙인다.</summary>
        static void CopyBehaviours(GameObject from, GameObject to)
        {
            foreach (var component in from.GetComponents<MonoBehaviour>())
            {
                if (component == null) continue;
                var existing = to.GetComponent(component.GetType());
                if (!UnityEditorInternal.ComponentUtility.CopyComponent(component)) continue;
                Undo.RecordObject(to, "Copy " + component.GetType().Name);
                if (existing != null) UnityEditorInternal.ComponentUtility.PasteComponentValues(existing);
                else UnityEditorInternal.ComponentUtility.PasteComponentAsNew(to);
            }
        }

        /// <summary>
        /// 대기실 문: 공장식 롤링 셔터(수직 개폐). 1단계 그레이박스의 로비 동쪽 벽(x=10) 가운데 출입구(폭 3·높이 3) 로비 쪽에 단다.
        /// 위 말림 통·노랑/검정 경고 줄무늬 가이드 기둥·골진 금속 셔터 판·하단 바·경고등. 셔터 판 묶음(`Shutter`)은 위쪽 끝이 기준점이라
        /// 세로 크기를 줄이면 말려 올라간 모습이 된다(<see cref="NightDoorLock"/>가 밤에 내리고 아침에 올린다).
        /// 좌표는 문 기준: x = 벽 두께 방향(-가 로비), y = 위, z = 출입구 폭 방향.
        /// </summary>
        static void SetUpNightDoor(Scene scene, GameObject greybox)
        {
            var old = FindRoot(scene, NightRootName);
            if (old != null) Undo.DestroyObjectImmediate(old);

            var root = new GameObject(NightRootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            Undo.RegisterCreatedObjectUndo(root, "Add " + NightRootName);

            var door = new GameObject("WaitingRoomShutter");
            door.transform.SetParent(root.transform, false);
            door.transform.SetPositionAndRotation(greybox.transform.TransformPoint(DoorBase), greybox.transform.rotation);
            var t = door.transform;

            const float face = -0.27f;              // 셔터 판이 지나는 면(벽 로비 쪽 면 앞)
            const float width = 3.1f;               // 출입구(3m)보다 조금 넓게
            const float height = 3f;                // 출입구 높이
            const float halfPost = width / 2f + 0.12f;

            // 위 말림 통(셔터가 감겨 들어가는 상자). 위쪽 "대기실 문 → ⑥" 표지(높이 3.6)를 가리지 않게 3.4까지만.
            Part(t, "Housing", new Vector3(face - 0.05f, height + 0.2f, 0f), new Vector3(0.5f, 0.4f, width + 0.5f), "GB_Metal");
            Part(t, "Housing_Band", new Vector3(face - 0.31f, height + 0.2f, 0f), new Vector3(0.02f, 0.1f, width + 0.5f), "GB_Gold");

            // 가이드 기둥: 노랑·검정 경고 줄무늬(0.5m 마디)
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 6; i++)
                    Part(t, $"Post_{(side < 0 ? "L" : "R")}_{i}", new Vector3(face, 0.25f + i * 0.5f, side * halfPost),
                        new Vector3(0.24f, 0.5f, 0.24f), i % 2 == 0 ? "GB_Gold" : "GB_Dark");

            // 경고등(셔터가 움직일 때 깜박임)
            var lamp = Part(t, "WarningLamp", new Vector3(face - 0.2f, height + 0.52f, halfPost - 0.1f), new Vector3(0.22f, 0.22f, 0.22f), "GB_Marker", PrimitiveType.Sphere);

            // 셔터 판 묶음: 기준점 = 위쪽 끝. 골진 판 12장 + 판 사이 홈 + 하단 바. 충돌체는 묶음 하나로 통로 전체를 막는다.
            var shutter = new GameObject("Shutter").transform;
            shutter.SetParent(t, false);
            shutter.localPosition = new Vector3(face, height, 0f);
            const int slats = 12;
            const float pitch = height / slats;
            for (int i = 0; i < slats; i++)
            {
                float y = -(i + 0.5f) * pitch;
                Part(shutter, $"Slat_{i:00}", new Vector3(i % 2 == 0 ? 0f : -0.02f, y, 0f), new Vector3(0.05f, pitch - 0.02f, width), "GB_Wall");
                Part(shutter, $"Groove_{i:00}", new Vector3(-0.03f, y - pitch / 2f, 0f), new Vector3(0.04f, 0.03f, width), "GB_Metal");
            }
            Part(shutter, "BottomBar", new Vector3(-0.01f, -height + 0.05f, 0f), new Vector3(0.12f, 0.1f, width), "GB_Dark");
            var block = shutter.gameObject.AddComponent<BoxCollider>();
            block.center = new Vector3(0f, -height / 2f, 0f);
            block.size = new Vector3(0.3f, height, width);

            // 잠김 표지(셔터 판 로비 쪽 면, 밤에만)
            var sign = new GameObject("Sign_잠김");
            sign.transform.SetParent(t, false);
            sign.transform.localPosition = new Vector3(face - 0.06f, 1.7f, 0f);
            sign.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            var text = sign.AddComponent<TextMesh>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var textMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/GB_Text.mat");
            sign.GetComponent<MeshRenderer>().sharedMaterial = textMaterial != null ? textMaterial : text.font.material;
            text.text = "잠김\n밤 정비 중";
            text.characterSize = 0.12f;
            text.fontSize = 48;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = new Color(1f, 0.85f, 0.2f);
            sign.SetActive(false);

            var doorLock = root.AddComponent<NightDoorLock>();
            var so = new SerializedObject(doorLock);
            so.FindProperty("shutter").objectReferenceValue = shutter;
            so.FindProperty("lockedDoor").objectReferenceValue = sign;
            so.FindProperty("warningLamp").objectReferenceValue = lamp.GetComponent<Renderer>();
            so.ApplyModifiedPropertiesWithoutUndo();
            // 편집 화면에서는 열린(낮) 모습으로 둔다.
            shutter.localScale = new Vector3(1f, so.FindProperty("openScale").floatValue, 1f);
        }

        /// <summary>그레이박스 재질을 입힌 도형 하나(충돌체 없음).</summary>
        static GameObject Part(Transform parent, string name, Vector3 position, Vector3 size, string material, PrimitiveType shape = PrimitiveType.Cube)
        {
            var go = GameObject.CreatePrimitive(shape);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/{material}.mat");
            if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        /// <summary>
        /// 스케줄표: 런 동안 남는 상태(`ScheduleState` 최상위, <see cref="ScheduleBoard"/>)와
        /// 로비 스케줄표 판의 상호작용·창(<see cref="ScheduleBoardView"/>), 판 앞 클로즈업 카메라.
        /// </summary>
        static void SetUpSchedule(Scene scene, GameObject greybox)
        {
            var oldState = FindRoot(scene, ScheduleStateName);
            if (oldState != null) Undo.DestroyObjectImmediate(oldState);
            var state = new GameObject(ScheduleStateName);
            SceneManager.MoveGameObjectToScene(state, scene);
            Undo.RegisterCreatedObjectUndo(state, "Add " + ScheduleStateName);
            state.AddComponent<ScheduleBoard>();

            var boardGroup = greybox.transform.Find(BoardPath);
            var boardShape = boardGroup != null ? boardGroup.Find("Board") : null;
            if (boardGroup == null || boardShape == null) { Debug.LogError($"[NightSchedule] 그레이박스에 {BoardPath}/Board 없음"); return; }

            // 판 앞(로비 쪽, -Z)에서 판을 바라보는 고정 카메라. 창을 열 때만 켠다.
            var nightRoot = FindRoot(scene, NightRootName);
            var cameraObject = new GameObject("ScheduleCloseUp");
            cameraObject.transform.SetParent(nightRoot != null ? nightRoot.transform : null, false);
            cameraObject.transform.SetPositionAndRotation(boardShape.position + greybox.transform.rotation * CloseUpOffset, greybox.transform.rotation);
            if (nightRoot == null) SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var closeUp = cameraObject.AddComponent<CinemachineCamera>();
            cameraObject.SetActive(false);

            foreach (var old in boardGroup.GetComponents<ScheduleBoardView>()) Undo.DestroyObjectImmediate(old);
            var view = Undo.AddComponent<ScheduleBoardView>(boardGroup.gameObject);
            var so = new SerializedObject(view);
            so.FindProperty("board").objectReferenceValue = boardShape;
            so.FindProperty("closeUp").objectReferenceValue = closeUp;
            so.ApplyModifiedPropertiesWithoutUndo();
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
        }

        static GameObject FindRoot(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == name) return root;
            return null;
        }
    }
}
