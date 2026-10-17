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
    /// - 밤 정비 때 대기실 문을 막는 `NightSchedule`(<see cref="NightDoorLock"/>)을 둔다.
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

        /// <summary>그레이박스 기준 대기실 문(로비 ↔ ⑥) 가운데와 크기. 1단계 벽 W_x+10의 출입구와 같다.</summary>
        static readonly Vector3 DoorCenter = new(10f, 1.5f, 0f);
        static readonly Vector3 DoorSize = new(0.3f, 3f, 3f);

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
        /// 밤 정비 때만 켜지는 잠긴 대기실 문. 1단계 그레이박스의 로비 동쪽 벽(x=10) 가운데 출입구(폭 3·높이 3)를 막는다.
        /// </summary>
        static void SetUpNightDoor(Scene scene, GameObject greybox)
        {
            var old = FindRoot(scene, NightRootName);
            if (old != null) Undo.DestroyObjectImmediate(old);

            var root = new GameObject(NightRootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            Undo.RegisterCreatedObjectUndo(root, "Add " + NightRootName);

            var door = new GameObject("NightDoor_Locked");
            door.transform.SetParent(root.transform, false);
            door.transform.SetPositionAndRotation(greybox.transform.TransformPoint(DoorCenter), greybox.transform.rotation);

            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.name = "Blocker";
            blocker.transform.SetParent(door.transform, false);
            blocker.transform.localScale = DoorSize;
            var doorMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/GB_Door.mat");
            if (doorMaterial != null) blocker.GetComponent<MeshRenderer>().sharedMaterial = doorMaterial;

            var sign = new GameObject("Sign_잠김");
            sign.transform.SetParent(door.transform, false);
            sign.transform.localPosition = new Vector3(-0.2f, 0.4f, 0f); // 로비 쪽 면
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
            text.color = Color.white;

            door.SetActive(false);
            var doorLock = root.AddComponent<NightDoorLock>();
            var so = new SerializedObject(doorLock);
            so.FindProperty("lockedDoor").objectReferenceValue = door;
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
