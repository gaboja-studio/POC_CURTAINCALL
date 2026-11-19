using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CurtainCall.WaitingRoom.PassageGimmicks
{
    // 4단계: 지하 기계실의 녹슨 벽 뒤 배선(삭제됨, 2026-11-19 사용자).
    // 예전에는 녹슨 벽(BreakableWall)을 부수면 숨은 배선 한 가닥이 드러났다. 지금은 기계실에 드러난 배선 4가닥(5단계)만 쓰므로
    // 이 단계는 남아 있는 녹슨 벽·숨은 배선(FinalLayout/MachineWiring)을 지우기만 한다.
    public static partial class FinalLayoutBuilder
    {
        const string CableMaterials = "Assets/Resources/Sounds/EscapeRouteMaterials/";

        [MenuItem("CurtainCall/Waiting Room/Final Layout/4. Remove Machine Room Hidden Wiring")]
        static void WiringMenu() => Debug.Log(BuildMachineWiring());

        public static string BuildMachineWiring()
        {
            var layout = GameObject.Find(LayoutPath)?.transform;
            if (layout == null) return "WaitingRoom 씬을 여세요.";
            var old = layout.Find(RootName + "/MachineWiring");
            if (old == null) return "녹슨 벽·숨은 배선 없음(이미 지워짐).";
            Undo.DestroyObjectImmediate(old.gameObject);
            EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
            return "녹슨 벽·숨은 배선 삭제. 5단계(동선 연결)를 다시 실행해 톱날 발사기·정전 배선 목록을 맞추세요.";
        }
    }
}
