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
            ("S05_JunkStorage/Exploration_A/passwordPrefixUv", "S05_JunkStorage/Exploration_A/passwordPrefixClue", "누군가에게는\n새 역사가 시작된 날,\n누군가에게는 기일.\n그 날을 기억해. 항상.", .12f),
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
            Physics.SyncTransforms(); sceneRenderers = null;
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
                if (uv == null || clue == null || !Write(clue, uv.position, text, size, true, uv)) problems.Add(cluePath); else written++;
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

        // 단서 물체 아래에 벽 글씨를 붙인다. near 근처의 가장 가까운 벽면을 찾고, 그 벽을 따라 글씨가 다른 물체에 가려지지 않는
        // 빈 자리를 찾아 단서 물체째 옮긴다(2026-11-13 플레이: 기둥·칸막이·세면대·타일 판에 글씨가 잘렸음). also는 함께 옮길 UV 자리.
        static bool Write(Transform clue, Vector3 near, string text, float size, bool hidden, Transform also = null)
        {
            foreach (var stale in clue.Cast<Transform>().Where(t => t.name == "WallText").ToArray()) Undo.DestroyObjectImmediate(stale.gameObject);
            if (!WallAt(near, clue, out var point, out var normal)) return false;
            // 임시 판(Document 등)은 그리지 않는다. 글씨만 보이게.
            foreach (var r in clue.GetComponentsInChildren<Renderer>(true))
            { if (!r.enabled) continue; Undo.RecordObject(r, "Hide placeholder"); r.enabled = false; PrefabUtility.RecordPrefabInstancePropertyModifications(r); }
            float yaw = Mathf.Atan2(-normal.x, -normal.z) * Mathf.Rad2Deg;
            // 닳은 페인트 글꼴은 획이 끊겨 작은 글씨에서 읽기 어렵다 → 형광 낙서도 붓글씨 글꼴을 쓴다.
            var tmp = WallGraffitiTool.Stamp(clue, text, point + normal * .012f, yaw, size, false, 0);
            tmp.gameObject.name = "WallText";
            if (hidden) tmp.color = Fluorescent;
            tmp.ForceMeshUpdate();
            // 글씨가 실제로 차지하는 크기(여백 포함)로 빈 벽면을 찾는다.
            // 꺼져 있는 숨은 낙서는 textBounds가 0이라 글귀에서 직접 크기를 잰다.
            var tb = tmp.textBounds.size; var pref = tmp.GetPreferredValues(text, tmp.rectTransform.sizeDelta.x, 0);
            float width = Mathf.Max(tb.x, pref.x, size * 3f) + .1f, height = Mathf.Max(tb.y, pref.y, size) + .12f;
            // 먼저 앞 0.6m가 완전히 빈 자리, 없으면(화장실 칸처럼 좁은 곳) 앞 0.4m만 빈 자리.
            if (FreeSpot(point, normal, width, height, clue, also, .6f, out var spot, out float lift) || FreeSpot(point, normal, width, height, clue, also, .4f, out spot, out lift))
            {
                var delta = spot - point;
                Undo.RecordObject(clue, "Move clue"); clue.position += delta; PrefabUtility.RecordPrefabInstancePropertyModifications(clue);
                if (also != null) { Undo.RecordObject(also, "Move uv"); also.position += delta; PrefabUtility.RecordPrefabInstancePropertyModifications(also); }
                tmp.transform.position = spot + normal * (lift + .012f);
            }
            else Debug.LogWarning("[Clues] 가려지지 않는 자리를 찾지 못했습니다: " + clue.name);
            tmp.ForceMeshUpdate();
            Undo.RegisterCreatedObjectUndo(tmp.gameObject, "Wall text");
            return true;
        }

        static Renderer[] sceneRenderers;

        // 벽면(point, normal)을 따라 폭 x 높이 글씨가 가려지지 않는 자리. 원래 자리에서 가까운 순서로 찾는다.
        // lift: 그 자리 벽면 앞에 붙어 있는 얇은 판(타일·칠)의 두께. 글씨를 그 앞에 붙이기 위한 값.
        static bool FreeSpot(Vector3 point, Vector3 normal, float width, float height, Transform clue, Transform also, float clear, out Vector3 spot, out float lift)
        {
            spot = point; lift = 0;
            var tangent = Vector3.Cross(Vector3.up, normal).normalized;
            sceneRenderers ??= Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            float[] shifts = { 0, .1f, -.1f, .2f, -.2f, .3f, -.3f, .4f, -.4f, .6f, -.6f, .8f, -.8f, 1.2f, -1.2f, 1.6f, -1.6f, 2f, -2f, 2.5f, -2.5f, 3f, -3f, 3.6f, -3.6f, 4.2f, -4.2f };
            float[] heights = { 1.6f, 1.85f, 1.4f, 2.1f, 2.4f };
            // 바닥 높이: 벽 앞에 놓인 가구(변기·상자) 위가 아니라 이름에 Floor가 든 진짜 바닥.
            float floor = point.y - 1.25f; bool found = false;
            foreach (var down in Physics.RaycastAll(point + normal * .3f, Vector3.down, 3.4f, ~0, QueryTriggerInteraction.Ignore))
                if (down.normal.y > .7f && down.collider.name.Contains("Floor") && (!found || down.point.y > floor)) { floor = down.point.y; found = true; }
            foreach (float s in shifts)
                foreach (float h in heights)
                {
                    var c = point + tangent * s; c.y = floor + Mathf.Max(h, height * .5f + .25f);
                    // 1) 네 모서리와 가운데 뒤에 같은 벽면이 있어야 한다(문·창·모서리 밖이면 안 됨).
                    bool wall = true;
                    foreach (var (u, v) in new[] { (0f, 0f), (-.5f, -.5f), (.5f, -.5f), (-.5f, .5f), (.5f, .5f) })
                    {
                        var p = c + tangent * (u * width) + Vector3.up * (v * height);
                        if (!Physics.Raycast(p + normal * .5f, -normal, out var hit, .62f, ~0, QueryTriggerInteraction.Ignore) || Vector3.Dot(hit.normal, normal) < .9f
                            || Mathf.Abs(Vector3.Dot(hit.point - point, normal)) > .06f) { wall = false; break; }
                    }
                    if (!wall) continue;
                    // 2) 글씨 앞 0.6m(사람이 서서 읽는 공간)에 충돌체가 없어야 한다.
                    var front = c + normal * (.08f + (clear - .08f) * .5f); var half = new Vector3(width * .5f, height * .5f, (clear - .08f) * .5f);
                    var rotation = Quaternion.LookRotation(normal, Vector3.up);
                    if (Physics.OverlapBox(front, half, rotation, ~0, QueryTriggerInteraction.Ignore).Any(o => !o.transform.IsChildOf(clue) && (also == null || !o.transform.IsChildOf(also)))) continue;
                    // 3) 충돌체 없는 장식(기둥 장식·거울·칸막이 판 등)이 글씨 앞을 가리지 않아야 한다. 벽에 붙은 얇은 판은 두께만 잰다.
                    var box = new Bounds(c + normal * (.06f + (clear - .06f) * .5f), Abs(tangent) * width + Vector3.up * height + Abs(normal) * (clear - .06f));
                    bool hiddenBehind = false; float panel = 0;
                    foreach (var r in sceneRenderers)
                    {
                        if (r == null || !r.enabled || !r.gameObject.activeInHierarchy || r is ParticleSystemRenderer || r.transform.IsChildOf(clue) || (also != null && r.transform.IsChildOf(also))) continue;
                        var b = r.bounds; if (!b.Intersects(new Bounds(c + normal * (clear * .5f), Abs(tangent) * width + Vector3.up * height + Abs(normal) * (clear + .02f)))) continue;
                        // 방 전체를 감싸는 합쳐진 메시(벽면 묶음)는 따로 판단할 수 없어 건너뛴다.
                        if (Vector3.Dot(Abs(normal), b.size) > 3f && Vector3.Dot(Abs(tangent), b.size) > 3f) continue;
                        float nearFace = Vector3.Dot(b.center - point, normal) - Vector3.Dot(Abs(normal), b.extents), farFace = nearFace + 2 * Vector3.Dot(Abs(normal), b.extents);
                        if (farFace <= .05f) { panel = Mathf.Max(panel, Mathf.Max(0, farFace)); continue; } // 벽면에 붙은 얇은 판
                        if (b.Intersects(box)) { hiddenBehind = true; break; }
                    }
                    if (hiddenBehind) continue;
                    spot = c; lift = panel; return true;
                }
            return false;
        }

        static Vector3 Abs(Vector3 v) => new(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

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
            bool ok = Write(clue, uv.position, text, size, true, uv);
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
