using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.WaitingRoom.Editor
{
    /// <summary>로비 디버그 보급품 옆에 디버그 돈 기물(<see cref="DebugMoneyProp"/>, F마다 공동 자금 +1000)을 둔다. 다시 실행해도 하나만 남는다.</summary>
    public static class DebugLobbyMoneyInstaller
    {
        const string ScenePath = "Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity";
        const string Name = "DebugLobbyMoney";
        const string MaterialPath = "Assets/Resources/Models/Materials/PerformanceArtKit/GripYellow.mat";

        [MenuItem("CurtainCall/Waiting Room/Install Debug Lobby Money")]
        public static void Install()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != ScenePath) throw new InvalidOperationException("Open WaitingRoomBase in EditMode.");
            var lobby = scene.GetRootGameObjects().Single(g => g.name == "WaitingRoom_LayoutA").transform.Find("S01_Lobby");
            var root = lobby.Find(Name);
            if (root == null)
            {
                var go = new GameObject(Name);
                Undo.RegisterCreatedObjectUndo(go, "Add debug money");
                go.transform.SetParent(lobby, false); root = go.transform;
            }
            Undo.RecordObject(root, "Position debug money");
            // 디버그 보급품(x -8~-5.3, z 74.4~76.2) 바로 오른쪽.
            root.SetPositionAndRotation(new Vector3(-4.2f, 0f, 74.4f), Quaternion.identity);
            if (root.GetComponent<DebugMoneyProp>() == null) Undo.AddComponent<DebugMoneyProp>(root.gameObject);

            // 지폐 묶음 모양 표시(충돌 없음).
            var body = root.Find("Body");
            if (body == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Body"; Undo.RegisterCreatedObjectUndo(go, "Add debug money body");
                go.transform.SetParent(root, false); body = go.transform;
                UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            }
            body.localPosition = new Vector3(0f, .05f, 0f); body.localRotation = Quaternion.identity; body.localScale = new Vector3(.32f, .1f, .16f);
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null) body.GetComponent<Renderer>().sharedMaterial = material;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Debug lobby money saved at (-4.2, 0, 74.4): F → +1000 team funds. Editor/Development Build only.");
        }
    }
}
