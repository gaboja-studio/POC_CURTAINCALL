using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CurtainCall.WaitingRoom.PassageGimmicks
{
    // 10단계: 단서 글귀(2026-11-12 사용자 문구). 다시 실행할 수 있다.
    // 글씨 크기는 3m쯤 떨어져서도 읽히게 잡았다(처음 0.085m는 너무 작았음).
    // - 벽의 낙서 1~6: 붉은 페인트 글씨. 기존 낙서 자리(1~4)는 임시 판을 숨기고 글씨를 붙인다. 5·6은 새로 만든다.
    // - 숨겨진 낙서 1~5: UV 등을 비출 때만 드러나는 형광 글씨. 1~3은 기존 UV 자리, 4·5는 생활 장소에 새로 만든다.
    // 조사했을 때 나오는 글귀는 문구 파일(target.<ID>.desc)에 있다. 여기 적힌 글은 벽에 보이는 글씨다(줄만 나눔).
    public static partial class FinalLayoutBuilder
    {
        static readonly Color Fluorescent = new(.72f, 1f, .3f, 1f);

        static readonly (string path, string text, float size)[] PaintedOnExisting =
        {
            ("S02_Dormitory/Restroom/Exploration_A/graffiti1", "다음은 누구?", .22f),
            ("S02_Dormitory/Restroom/Exploration_A/graffiti2", "공연.. 잠자기…\n공연.. 잠자기…\n공연?", .16f),
            ("S10_Workshop/Exploration_A/graffiti-3", "2일째. 친구가 없어졌어.\n다음날 또 한명 없어졌어.\n항상 누군가 지켜보고 있는 것 같아.\n조심해.", .14f),
            ("P05_PipeZone/Exploration_A/graffiti4", "4일째.\n여전히 아무도 오지 않아.\n여기서 기다리라며…?\n다들 어디로 간 거야?", .14f),
        };
        static readonly (string uv, string clue, string text, float size)[] HiddenOnExisting =
        {
            ("S02_Dormitory/Restroom/TrialUvMark", "S02_Dormitory/Restroom/Exploration_A/uv-1-detail", "관리실 컴퓨터로 가.\n필요한 건 다 준비해놨어.\n여기서 나갈 방법을 찾아.", .12f),
            ("S12_CargoBay/Exploration_A/cargo-uv", "S12_CargoBay/Exploration_A/hiddenGraffiti3", "RUN", .34f),
            ("S05_JunkStorage/Exploration_A/passwordPrefixUv", "S05_JunkStorage/Exploration_A/passwordPrefixClue", "누군가에게는 새 역사가 시작된 날,\n누군가에게는 기일.\n그 날을 기억해. 항상.", .12f),
        };

        [MenuItem("CurtainCall/Waiting Room/Final Layout/10. Write Clue Graffiti")]
        static void CluesMenu() => Debug.Log(WriteClues());

        public static string WriteClues()
        {
            var layout = GameObject.Find(LayoutPath)?.transform;
            if (layout == null) return "WaitingRoom 씬을 여세요.";
            var paintSource = layout.Find("S10_Workshop/Exploration_A/graffiti-3");
            var uvSource = layout.Find("S12_CargoBay/Exploration_A/cargo-uv");
            var hiddenSource = layout.Find("S12_CargoBay/Exploration_A/hiddenGraffiti3");
            if (paintSource == null || uvSource == null || hiddenSource == null) return "기존 낙서·UV 낙서를 찾지 못했습니다.";
            WallGraffitiTool.CreateAssets();
            int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Final layout: clue graffiti");
            var old = layout.Find(RootName + "/Clues"); if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var root = Child(Child(layout, RootName), "Clues");
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Clues");
            Physics.SyncTransforms();
            int written = 0; var problems = new System.Collections.Generic.List<string>();

            foreach (var (path, text, size) in PaintedOnExisting)
            {
                var clue = layout.Find(path);
                if (clue == null || !Write(clue, clue.position, text, size, false)) problems.Add(path); else written++;
            }
            // 새 벽의 낙서 5(무대 입구 통로 동벽)·6(작업실 남벽, 톱날 발사기 옆).
            written += NewPainted(root, paintSource, "final:graffiti-5", "벽의 낙서 5", new Vector3(-9.7f, 1.5f, 99f), Vector3.right, "가고 싶지 않아.\n가고 싶지 않아.\n가고 싶지 않아.", .18f, problems);
            written += NewPainted(root, paintSource, "final:graffiti-6", "벽의 낙서 6", new Vector3(-17.15f, 1.6f, 53.2f), Vector3.back, "전력 공급원을 끊고\n더 깊은 곳으로 가면\n나갈 수 있을까?", .15f, problems);

            foreach (var (uvPath, cluePath, text, size) in HiddenOnExisting)
            {
                var uv = layout.Find(uvPath); var clue = layout.Find(cluePath);
                if (uv == null || clue == null || !Write(clue, uv.position, text, size, true)) problems.Add(cluePath); else written++;
            }
            // 새 숨겨진 낙서 4(생활 장소 동벽, 바닥 잠자리 위)·5(생활 장소 남벽, 옷장 옆).
            written += NewHidden(root, uvSource, hiddenSource, 4, new Vector3(1.2f, 1.6f, 44.3f), Vector3.right, "두 개의 암호를 만들었어.\n뭘 해야 하는지 알지?\n도망쳐.", .14f, problems);
            written += NewHidden(root, uvSource, hiddenSource, 5, new Vector3(-4.9f, 1.6f, 37.8f), Vector3.back, "기계실 깊은 곳으로 와.\n거기서 기다리고 있을게.", .12f, problems);

            // 옛 4자리 비밀번호 단서 정리: 압수한 노트의 새김 숫자 "34"(비밀번호 패드가 없어져 쓸모없음).
            var note = layout.Find("B1_Office/Exploration_A/confiscatedNotebook")?.GetComponent<ClueInspectable>();
            if (note != null)
            {
                var so = new SerializedObject(note); so.FindProperty("secretText").stringValue = ""; so.FindProperty("secretMark").objectReferenceValue = null;
                so.ApplyModifiedProperties(); PrefabUtility.RecordPrefabInstancePropertyModifications(note);
                var engrave = note.transform.Find("Engrave")?.GetComponent<Renderer>();
                if (engrave != null && engrave.enabled) { Undo.RecordObject(engrave, "Hide old engraving"); engrave.enabled = false; PrefabUtility.RecordPrefabInstancePropertyModifications(engrave); }
            }
            // 관리실 컴퓨터 날짜 정답.
            var computer = layout.Find("B1_Office/SecurityRoom/Console/Keyboard")?.GetComponent<ExplorationGate>();
            if (computer != null) { var so = new SerializedObject(computer); so.FindProperty("password").stringValue = ComputerDate; so.ApplyModifiedProperties(); PrefabUtility.RecordPrefabInstancePropertyModifications(computer); }

            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(undo); EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
            return $"글씨 {written}개 / 컴퓨터 날짜 {ComputerDate}" + (problems.Count > 0 ? " / 벽을 찾지 못한 곳: " + string.Join(", ", problems) : "");
        }

        // 단서 물체 아래에 벽 글씨를 붙인다. near 근처의 가장 가까운 벽면을 찾아 그 면에 1.2cm 띄워 놓는다.
        static bool Write(Transform clue, Vector3 near, string text, float size, bool hidden)
        {
            foreach (var stale in clue.Cast<Transform>().Where(t => t.name == "WallText").ToArray()) Undo.DestroyObjectImmediate(stale.gameObject);
            if (!WallAt(near, clue, out var point, out var normal)) return false;
            // 임시 판(Document 등)은 그리지 않는다. 글씨만 보이게.
            foreach (var r in clue.GetComponentsInChildren<Renderer>(true))
            { if (!r.enabled) continue; Undo.RecordObject(r, "Hide placeholder"); r.enabled = false; PrefabUtility.RecordPrefabInstancePropertyModifications(r); }
            float yaw = Mathf.Atan2(-normal.x, -normal.z) * Mathf.Rad2Deg;
            // 닳은 페인트 글꼴은 획이 끊겨 작은 글씨에서 읽기 어렵다 → 형광 낙서도 붓글씨 글꼴을 쓴다.
            var tmp = WallGraffitiTool.Stamp(clue, text, point + normal * .012f, yaw, size, false, hidden ? 0 : Random.Range(-2.5f, 2.5f));
            tmp.gameObject.name = "WallText";
            if (hidden) tmp.color = Fluorescent;
            tmp.ForceMeshUpdate();
            Undo.RegisterCreatedObjectUndo(tmp.gameObject, "Wall text");
            return true;
        }

        static int NewPainted(Transform root, Transform source, string id, string title, Vector3 from, Vector3 toward, string text, float size, System.Collections.Generic.List<string> problems)
        {
            if (!Physics.Raycast(from, toward, out var hit, 12f, ~0, QueryTriggerInteraction.Ignore)) { problems.Add(id); return 0; }
            var clue = Object.Instantiate(source.gameObject, root).transform; clue.name = id.Substring(id.IndexOf(':') + 1);
            clue.SetPositionAndRotation(hit.point + hit.normal * .02f, Quaternion.LookRotation(-hit.normal)); clue.gameObject.SetActive(true);
            var so = new SerializedObject(clue.GetComponent<ClueInspectable>());
            so.FindProperty("targetId").stringValue = id; so.FindProperty("title").stringValue = title; so.FindProperty("description").stringValue = text.Replace("\n", " ");
            so.FindProperty("availableAfter").stringValue = ""; so.FindProperty("revealPending").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (!Write(clue, clue.position, text, size, false)) { problems.Add(id); return 0; }
            return 1;
        }

        static int NewHidden(Transform root, Transform uvSource, Transform clueSource, int number, Vector3 from, Vector3 toward, string text, float size, System.Collections.Generic.List<string> problems)
        {
            string uvId = "final:uv-" + number, clueId = "final:hidden-graffiti-" + number;
            if (!Physics.Raycast(from, toward, out var hit, 12f, ~0, QueryTriggerInteraction.Ignore)) { problems.Add(clueId); return 0; }
            var rotation = Quaternion.LookRotation(-hit.normal);
            var uv = Object.Instantiate(uvSource.gameObject, root).transform; uv.name = "uv-" + number;
            uv.SetPositionAndRotation(hit.point + hit.normal * .02f, rotation);
            var clue = Object.Instantiate(clueSource.gameObject, root).transform; clue.name = "hidden-graffiti-" + number;
            clue.SetPositionAndRotation(hit.point + hit.normal * .025f, rotation);
            var cso = new SerializedObject(clue.GetComponent<ClueInspectable>());
            cso.FindProperty("targetId").stringValue = clueId; cso.FindProperty("title").stringValue = "숨겨진 낙서 " + number; cso.FindProperty("description").stringValue = text.Replace("\n", " ");
            cso.FindProperty("availableAfter").stringValue = uvId; cso.FindProperty("revealPending").boolValue = false;
            cso.ApplyModifiedPropertiesWithoutUndo();
            var uso = new SerializedObject(uv.GetComponent<UvMark>());
            uso.FindProperty("targetId").stringValue = uvId;
            Refs(uso.FindProperty("marks"), clue.gameObject);
            uso.ApplyModifiedPropertiesWithoutUndo();
            // 복제한 UV 자리의 얼룩 판은 실행 중에는 숨겨지지만 편집 화면에서 글씨를 가리므로 그리지 않게 한다(충돌체는 둔다).
            foreach (var r in uv.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            clue.gameObject.SetActive(true);
            bool ok = Write(clue, uv.position, text, size, true);
            clue.gameObject.SetActive(false); // UV 등을 비추기 전에는 꺼 둔다(UvMark가 켠다).
            if (!ok) { problems.Add(clueId); return 0; }
            return 1;
        }

        // near에서 네 방향으로 쏴 가장 가까운 벽면(단서 자신의 충돌체는 무시).
        static bool WallAt(Vector3 near, Transform ignore, out Vector3 point, out Vector3 normal)
        {
            point = normal = default; float best = float.MaxValue;
            foreach (var d in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
            {
                // 벽 속에서 쏘면 벽 뒷면이나 옆방 벽이 잡힌다 → 출발점이 빈 공간일 때만 쏜다.
                if (Physics.CheckSphere(near - d * .35f, .04f, ~0, QueryTriggerInteraction.Ignore)) continue;
                foreach (var hit in Physics.RaycastAll(near - d * .35f, d, 1.2f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.transform.IsChildOf(ignore) || hit.collider.GetComponentInParent<UvMark>() != null || hit.collider.GetComponentInParent<ClueInspectable>() != null) continue;
                    // 얇은 벽의 반대편 면이 더 가깝게 잡힐 수 있다 → 단서가 그 면의 앞쪽에 있을 때만 받는다.
                    if (Vector3.Dot(near - hit.point, hit.normal) < -.03f) continue;
                    if (hit.distance < best) { best = hit.distance; point = hit.point; normal = hit.normal; }
                }
            }
            return best < float.MaxValue;
        }
    }
}
