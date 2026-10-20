using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.WaitingRoom.EditorTools
{
    /// <summary>
    /// 방 안 그레이박스 가구를 벽 쪽으로 정리해 출입구·동선을 비우고, 설명 표지를 그 가구 앞에 다시 붙인다.
    /// 바닥 방 이름표는 그 방에 들어서는 방향에서 바로 읽히게 돌린다.
    /// 절대 위치로 놓으므로 여러 번 실행해도 같다. 기능 오브젝트(물려줄 선반·스케줄표·상점·공연 기록 노트 등)는 옮기지 않는다.
    /// 분위기 입히기(<see cref="WaitingRoomMoodDresser"/>)는 이 배치를 기준으로 프로토타입 실물을 세운다.
    /// </summary>
    public static class WaitingRoomRoomArranger
    {
        const string GreyboxName = "WaitingRoomGreybox";

        [MenuItem("Tools/CurtainCall/대기실/방 배치 정리(동선·표지)")]
        static void ApplyMenu()
        {
            var scene = SceneManager.GetActiveScene();
            var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == GreyboxName);
            if (root == null) { Debug.LogError($"[WaitingRoomArrange] '{GreyboxName}' 없음"); return; }
            int moved = Arrange(root.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[WaitingRoomArrange] 가구·표지 {moved}개 정리");
        }

        // 표지 방향(yaw): 표지의 forward가 벽(가구) 쪽을 향해야 앞에서 읽힌다. 0 = 남쪽에서 읽음, 180 = 북쪽, 90 = 서쪽, 270 = 동쪽.
        const float ReadFromSouth = 0f, ReadFromNorth = 180f, ReadFromWest = 90f, ReadFromEast = 270f;

        public static int Arrange(Transform greybox)
        {
            int n = 0;
            Transform T(string path) => greybox.Find(path);

            // ④ 소품실(x -10~10, z -20.5~-9, 로비 문 x -2): 문에서 물려줄 선반까지 가운데 길(x -4~1)을 비운다.
            //    서쪽 = 소품 창고(선반·의상), 동쪽 = 분장 구역(동쪽 벽 화장대), 남쪽 벽 = 물려줄 선반·인형.
            n += Move(T("S04_PropRoom/PropShelf_A"), -9.55f, -12.2f, 90f);                 // 서쪽 벽(문 앞을 막던 자리에서)
            n += Move(T("S04_PropRoom/PropShelf_B"), -6.3f, -9.55f, 0f);                   // 북쪽 벽 문 서쪽
            n += Sign(T("S04_PropRoom/Sign_소품 선반"), -7.9f, 2.45f, -10.05f, ReadFromSouth); // 두 선반이 만나는 모서리 위
            n += Move(T("S04_PropRoom/CostumeStorage"), -9.05f, -17.6f, 0f);               // 서쪽 벽에 붙임
            n += Sign(T("S04_PropRoom/Sign_의상 보관"), -8.2f, 2.35f, -17.6f, ReadFromEast);
            n += Move(T("S04_PropRoom/DressingTable"), 9.5f, -14.8f, 0f);                  // 동쪽 벽 화장대 줄 가운데
            n += Move(T("S04_PropRoom/DressingMirror"), 9.8f, -14.8f, 0f);
            n += Sign(T("S04_PropRoom/Sign_분장대·거울"), 9.8f, 2.75f, -14.8f, ReadFromWest);
            n += Sign(T("S04_PropRoom/Sign_낡은 인형·인형탈"), 4f, 1.75f, -20.3f, ReadFromNorth);

            // ⑤ 폐기물 보관소(x -22~-10, z -9~9, 로비 문 z 0): 문 정면 4m에 있던 기물 더미를 남쪽 벽으로.
            n += Move(T("S05_JunkStorage/BrokenProps"), -15.6f, -7.75f, 0f);
            n += Sign(T("S05_JunkStorage/Sign_사용하지 않는 기물"), -15.6f, 1.5f, -6.55f, ReadFromNorth);

            // 바닥 방 이름표: 들어서는 방향에서 읽히게(글자 위쪽이 방 안쪽)
            n += Floor(T("S04_PropRoom/Label"), 180f);                // 로비 → 남쪽으로 들어감
            n += Floor(T("S05_JunkStorage/Label"), 270f);             // 로비 → 서쪽
            n += Floor(T("S08_ExpertRoom/Label"), 90f);               // 로비 → 동쪽
            n += Floor(FindLabel(T("S02_Dormitory"), "② 화장실"), 90f); // 숙소 → 동쪽(화장실 문)
            n += Floor(T("S06_StageCorridor_Temp/Label"), 90f);       // 대기실 문 → 동쪽(터널)
            n += Floor(T("S07_StageEntrance_Temp/Label"), 90f);
            n += Floor(T("S06_StageCorridor_Temp/GatherZone_Border/Label"), 90f);

            HideDescriptionTexts(greybox);
            return n;
        }

        /// <summary>
        /// 공간·물건을 글자로 설명하던 그레이박스 표지(바닥 방 이름 Label*, 벽·가구 표지 Sign_*)를 모두 끈다(2026-10-20 사용자 지시).
        /// 기능 글자(⑧ 영업 안내 ExpertRoomSign, 스폰 번호)와 프로토타입 실물 간판(Mood 아래)은 그대로 둔다. 다시 보려면 오브젝트를 켜면 된다.
        /// </summary>
        static void HideDescriptionTexts(Transform greybox)
        {
            foreach (var text in greybox.GetComponentsInChildren<TextMesh>(true))
            {
                var t = text.transform;
                if (IsUnder(t, greybox, "Mood") || IsUnder(t, greybox, "SpawnPoints")) continue;
                if (!t.name.StartsWith("Label") && !t.name.StartsWith("Sign_")) continue;
                if (!t.gameObject.activeSelf) continue;
                Undo.RecordObject(t.gameObject, "Arrange Waiting Room");
                t.gameObject.SetActive(false);
            }
        }

        static bool IsUnder(Transform t, Transform root, string name)
        {
            for (var p = t.parent; p != null && p != root; p = p.parent)
                if (p.name == name) return true;
            return false;
        }

        static int Move(Transform t, float x, float z, float yaw)
        {
            if (t == null) return 0;
            Undo.RecordObject(t, "Arrange Waiting Room");
            t.localPosition = new Vector3(x, t.localPosition.y, z);
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return 1;
        }

        static int Sign(Transform t, float x, float y, float z, float yaw)
        {
            if (t == null) return 0;
            Undo.RecordObject(t, "Arrange Waiting Room");
            t.localPosition = new Vector3(x, y, z);
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return 1;
        }

        static int Floor(Transform t, float yaw)
        {
            if (t == null) return 0;
            Undo.RecordObject(t, "Arrange Waiting Room");
            t.localRotation = Quaternion.Euler(90f, yaw, 0f);
            return 1;
        }

        static Transform FindLabel(Transform room, string text) =>
            room == null ? null : room.Cast<Transform>().FirstOrDefault(c => c.name == "Label" && c.GetComponent<TextMesh>()?.text == text);
    }
}
