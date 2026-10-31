using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.WaitingRoom.Editor
{
    /// <summary>로비 디버그 돈 기물 옆에 디버그 탈출로 열쇠 1·2(<see cref="DebugEscapeKeyProp"/>)를 둔다. 다시 실행해도 하나씩만 남는다.</summary>
    public static class DebugLobbyEscapeKeyInstaller
    {
        const string ScenePath = "Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity";
        const string MaterialPath = "Assets/Resources/Models/Materials/PerformanceArtKit/GripYellow.mat";

        [MenuItem("CurtainCall/Waiting Room/Install Debug Lobby Escape Keys")]
        public static void Install()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != ScenePath) throw new InvalidOperationException("Open WaitingRoomBase in EditMode.");
            var lobby = scene.GetRootGameObjects().Single(g => g.name == "WaitingRoom_LayoutA").transform.Find("S01_Lobby");
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            // 디버그 돈 기물(-4.2, 0, 74.4) 뒤로 한 줄. 디버그 보급품(x -8~-5.3, z 74.4~76.2) 오른쪽.
            Place(lobby, 1, new Vector3(-4.2f, 0f, 75.3f), material);
            Place(lobby, 2, new Vector3(-4.2f, 0f, 76.2f), material);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Debug lobby escape keys saved at (-4.2, 0, 75.3/76.2): F → 탈출로 열쇠 1/2 열림. Editor/Development Build only.");
        }

        static void Place(Transform lobby, int number, Vector3 position, Material material)
        {
            string name = "DebugLobbyEscapeKey" + number;
            var root = lobby.Find(name);
            if (root == null)
            {
                var go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, "Add debug escape key");
                go.transform.SetParent(lobby, false); root = go.transform;
            }
            Undo.RecordObject(root, "Position debug escape key");
            root.SetPositionAndRotation(position, Quaternion.identity);
            var prop = root.GetComponent<DebugEscapeKeyProp>() ?? Undo.AddComponent<DebugEscapeKeyProp>(root.gameObject);
            var so = new SerializedObject(prop);
            so.FindProperty("keyNumber").intValue = number;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 열쇠 모양 표시(손잡이 고리 + 몸통, 충돌 없음).
            Part(root, "Bow", new Vector3(-.09f, .04f, 0f), new Vector3(.1f, .04f, .1f), material);
            Part(root, "Shaft", new Vector3(.05f, .04f, 0f), new Vector3(.2f, .03f, .035f), material);
            Part(root, "Bit", new Vector3(.13f, .04f, .035f), new Vector3(.03f, .03f, .05f), material);
        }

        static void Part(Transform root, string name, Vector3 position, Vector3 scale, Material material)
        {
            var part = root.Find(name);
            if (part == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name; Undo.RegisterCreatedObjectUndo(go, "Add debug escape key part");
                go.transform.SetParent(root, false); part = go.transform;
                UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            }
            part.localPosition = position; part.localRotation = Quaternion.identity; part.localScale = scale;
            if (material != null) part.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
