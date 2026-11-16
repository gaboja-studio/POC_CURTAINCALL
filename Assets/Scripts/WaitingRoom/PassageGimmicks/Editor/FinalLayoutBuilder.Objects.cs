using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CurtainCall.WaitingRoom.PassageGimmicks
{
    // 11단계: 탐색 오브젝트(2026-11-13 사용자). 다시 실행할 수 있다. 결과물은 FinalLayout/Objects.
    // - 망가진 노트북: 이전 세대 생활 장소 화장실 구석. 뒤집어 보면 포스트잇(20240912 / RUN).
    // - 키 카드 복제본: 무대 장치 보관실 캐비넷 3에서 나온다. 관리실 문 옆 단말기에 쓰면 사라지고 문이 열린다(5단계).
    // - 낡은 인형 옷: 13단계에서 만든다(전시대 인형 옷 4벌).
    // - 물병: 생활 장소. 살펴보면 안에 든 공연 토큰(100원)이 나온다.
    // - 찢겨진 노트 낱장 4장: 캐비넷 1·폐기물 보관소 트렁크·잠긴 상자 2·관리실 서랍 안(2026-11-14). 그림은 14단계가 입힌다.
    // - 상자 보상 비율과 묻힌 발견품 자리 3곳도 여기서 맞춘다.
    public static partial class FinalLayoutBuilder
    {
        public const string KeycardId = "keycard-copy";
        const string LaptopPrefab = "Assets/_ThirdParty/Synty/PolygonApocalypse/Prefabs/Item/SM_Item_Laptop_01.prefab";
        const string BottlePrefab = "Assets/_ThirdParty/Synty/PolygonApocalypse/Prefabs/Item/SM_Item_Drink_Bottle_01.prefab";

        [MenuItem("CurtainCall/Waiting Room/Final Layout/11. Place Clue Objects")]
        static void ObjectsMenu() => Debug.Log(PlaceClueObjects());

        public static string PlaceClueObjects()
        {
            var layout = GameObject.Find(LayoutPath)?.transform;
            if (layout == null) return "WaitingRoom 씬을 여세요.";
            int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Final layout: clue objects");
            var old = layout.Find(RootName + "/Objects"); if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var root = Child(Child(layout, RootName), "Objects");
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Objects");
            Physics.SyncTransforms();
            var notes = new List<string>();
            notes.Add(EnsureKeycardItem());

            // ---- 망가진 노트북 ----
            {
                // 화장실(생활 장소 서남쪽 칸) 구석 후보 중 비어 있는 첫 자리.
                var spot = FirstFree(new[] { new Vector3(-19.55f, 0, 36.05f), new Vector3(-9.75f, 0, 36.05f), new Vector3(-9.75f, 0, 31.0f), new Vector3(-19.55f, 0, 31.0f), new Vector3(-19.55f, 0, 35.0f), new Vector3(-19.55f, 0, 34.0f), new Vector3(-19.55f, 0, 33.0f), new Vector3(-18.6f, 0, 36.05f), new Vector3(-17.6f, 0, 36.05f) }, .3f, 1.2f);
                var holder = Child(root, "BrokenLaptop"); holder.position = spot; holder.rotation = Quaternion.Euler(0, 205f, 0);
                var art = Spawn(holder, LaptopPrefab, .36f);
                var b = art != null ? Bounds(art) : new Bounds(spot + Vector3.up * .02f, new Vector3(.34f, .03f, .24f));
                if (art == null) Plain(Box(holder, "LaptopBody", Mat("LA_SteelPlate"), b.min, b.max, false));
                else { art.position += Vector3.up * (spot.y + .012f - b.min.y); b = Bounds(art); }
                // 포스트잇: 바닥면(뒤집어야 보이는 면)에 붙인다.
                var mark = Child(holder, "PostIt"); mark.position = new Vector3(b.center.x, b.min.y - .003f, b.center.z); mark.rotation = Quaternion.Euler(180f, holder.eulerAngles.y, 0);
                var paper = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.DestroyImmediate(paper.GetComponent<Collider>()); paper.name = "PostItPaper";
                paper.transform.SetParent(mark, false); paper.transform.SetPositionAndRotation(mark.position, Quaternion.Euler(-90f, holder.eulerAngles.y, 0)); paper.transform.localScale = new Vector3(.09f, .09f, 1f);
                paper.GetComponent<Renderer>().sharedMaterial = Mat("LA_MascotYellow") ?? Mat("LA_Paper"); paper.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var ink = WallGraffitiTool.Stamp(mark, "20240912\nRUN", mark.position + Vector3.down * .001f, 0, .016f);
                ink.gameObject.name = "PostItText"; ink.transform.rotation = Quaternion.Euler(-90f, holder.eulerAngles.y, 0); ink.color = new Color(.08f, .07f, .1f, 1f);
                ink.rectTransform.sizeDelta = new Vector2(.085f, .085f); ink.ForceMeshUpdate();
                Clue(holder, "final:broken-laptop", "망가진 노트북", "", null, mark, .42f, 1.2f);
                notes.Add($"노트북 {spot:F1}{(art == null ? "(에셋 없음, 임시 도형)" : "")}");
            }

            // ---- 물병 + 안에 든 공연 토큰 ----
            {
                var table = layout.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Table_02" && t.parent != null && t.parent.name == "CommonTable" && t.gameObject.activeInHierarchy);
                Vector3 spot; string where;
                if (table != null)
                {
                    // 탁자 윗면 중 다른 물건(프라이팬·그릇)이 없는 자리: 위에서 쏴서 탁자 자체에 닿고 그 위 0.3m가 비어 있는 곳.
                    var tb = Bounds(table); spot = new Vector3(tb.center.x, tb.max.y, tb.center.z); where = "공용 탁자 위(빈 자리 못 찾음)";
                    bool done = false;
                    for (float fx = -.4f; fx <= .41f && !done; fx += .1f)
                        for (float fz = -.35f; fz <= .36f && !done; fz += .1f)
                        {
                            var probe = new Vector3(tb.center.x + tb.extents.x * fx * 2f, tb.max.y + .6f, tb.center.z + tb.extents.z * fz * 2f);
                            if (Physics.Raycast(probe, Vector3.down, out var th, 1.2f, ~0, QueryTriggerInteraction.Ignore) && th.normal.y > .9f && th.collider.transform.IsChildOf(table)
                                && !Physics.CheckSphere(th.point + Vector3.up * .2f, .14f, ~0, QueryTriggerInteraction.Ignore)
                                && !Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Any(r => r.enabled && !r.transform.IsChildOf(table) && !r.transform.IsChildOf(root) && r.bounds.size.magnitude < 1.2f && r.bounds.Intersects(new Bounds(th.point + Vector3.up * .15f, new Vector3(.2f, .28f, .2f)))))
                            { spot = th.point; where = "공용 탁자 위"; done = true; }
                        }
                }
                else { spot = FirstFree(new[] { new Vector3(-8f, 0, 41f), new Vector3(-6f, 0, 40f) }, .15f); where = "바닥"; }
                var holder = Child(root, "WaterBottle"); holder.position = spot; holder.rotation = Quaternion.Euler(0, 40f, 0);
                var art = Spawn(holder, BottlePrefab, .24f);
                if (art != null) art.position += Vector3.up * (spot.y - Bounds(art).min.y);
                else Plain(Box(holder, "Bottle", Mat("LA_DeadScreenGlass"), spot + new Vector3(-.035f, 0, -.035f), spot + new Vector3(.035f, .22f, .035f), false));
                Clue(holder, "final:water-bottle", "물병", "", null, null, .3f, 1.5f);
                // 살펴본 뒤(대상이 열린 뒤) 옆에 토큰이 놓인다.
                var token = Child(root, "WaterBottleToken"); token.position = spot + holder.right * .22f;
                var found = token.gameObject.AddComponent<FoundItemSpot>(); var fso = new SerializedObject(found);
                fso.FindProperty("spotId").stringValue = "final-water-bottle-token"; fso.FindProperty("uidOffset").intValue = FreeUidOffset(layout.gameObject.scene);
                fso.FindProperty("itemId").stringValue = ExplorationDrops.LargeTokenId; fso.FindProperty("itemName").stringValue = "공연 토큰 (자금 100원)";
                fso.FindProperty("availableAfter").stringValue = "final:water-bottle"; fso.ApplyModifiedPropertiesWithoutUndo();
                notes.Add($"물병 {where} {spot:F1}");
            }

            // 낡은 인형 옷 4벌(곰·토끼·삐에로·인형)의 살펴보기 대상은 13단계(ReplaceDisplayCostumes)가 만든다. 이 단계를 다시 돌린 뒤에는 13단계도 다시 돌린다.

            // ---- 찢겨진 노트 낱장 4장 ----
            {
                var paper = Mat("LA_Paper");
                Transform Page(int n, Vector3 at, float yaw)
                {
                    var holder = Child(root, "TornNote_" + n); holder.position = at; holder.rotation = Quaternion.Euler(0, yaw, 0);
                    var quad = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.DestroyImmediate(quad.GetComponent<Collider>()); quad.name = "Page";
                    quad.transform.SetParent(holder, false); quad.transform.SetPositionAndRotation(at + Vector3.up * .004f, Quaternion.Euler(90f, yaw, 0)); quad.transform.localScale = new Vector3(.15f, .21f, 1f);
                    var r = quad.GetComponent<Renderer>(); r.sharedMaterial = paper; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    // 찢긴 자국: 한쪽 모서리를 덮는 어두운 조각.
                    var tear = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.DestroyImmediate(tear.GetComponent<Collider>()); tear.name = "TornCorner";
                    tear.transform.SetParent(holder, false); tear.transform.SetPositionAndRotation(at + Vector3.up * .0045f + holder.rotation * new Vector3(.055f, 0, -.085f), Quaternion.Euler(90f, yaw + 28f, 0)); tear.transform.localScale = new Vector3(.07f, .05f, 1f);
                    tear.GetComponent<Renderer>().sharedMaterial = Mat("LA_FloorConcrete") ?? paper; tear.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    Clue(holder, "final:torn-note-" + n, "찢겨진 노트 낱장", "", null, null, .36f, 1.2f);
                    return holder;
                }
                // 네 장 모두 보상이 없던 캐비넷·상자·서랍 안(2026-11-14 사용자). 열면 그 앞 바닥에 드러난다(지도 단서와 같은 방식).
                // 1: 숙소 캐비넷 1, 2: 폐기물 보관소 자물쇠 트렁크, 3: 화물 적재 장소 잠긴 상자 2, 4: 서랍(물체 이름은 managementDrawer 그대로, 2026-11-14 사용자 요청으로 이전 세대 공연자 숙소 소품 방 남쪽 벽 앞으로 옮김).
                int placed = 0;
                foreach (var (n, path) in new[] { (1, "S02_Dormitory/Exploration_A/emptyCabinet"), (2, "S05_JunkStorage/TrialPadlockedTrunk"),
                    (3, "S12_CargoBay/Exploration_A/lockedBox2"), (4, "B1_Office/Exploration_A/managementDrawer") })
                {
                    var box = layout.Find(path); var target = box != null ? box.GetComponent<ToolTarget>() : null;
                    if (target == null) { notes.Add($"낱장 {n}: {path} 없음"); continue; }
                    var at = FrontFloor(box, .7f);
                    var page = Page(n, at, box.eulerAngles.y + 15f);
                    Apply(new SerializedObject(page.GetComponent<ClueInspectable>()), "availableAfter", target.TargetId);
                    // 열릴 때 보이게: showOnOpen에 추가(이미 있는 항목은 유지).
                    var so = new SerializedObject(target); var show = so.FindProperty("showOnOpen");
                    var keep = Enumerable.Range(0, show.arraySize).Select(i => show.GetArrayElementAtIndex(i).objectReferenceValue).Where(o => o != null && o.name != page.name).ToList();
                    keep.Add(page.gameObject); show.arraySize = keep.Count; for (int i = 0; i < keep.Count; i++) show.GetArrayElementAtIndex(i).objectReferenceValue = keep[i];
                    so.ApplyModifiedProperties(); PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                    page.gameObject.SetActive(false); placed++;
                }
                notes.Add($"낱장 {placed}/4");
            }

            // ---- 키 카드 복제본: 캐비넷 3(2026-11-14: 무대 장치 보관실 2층 통로 위)에서 나온다 ----
            // 캐비넷이 열리면 오버뷰(FinalLayout/Objects/KeycardReveal, ClueDropReveal)가 먼저 뜨고, 닫으면 바닥에 떨어진다.
            // 그래서 캐비넷의 직접 보상은 비워 둔다. KeycardReveal은 씬에서 직접 만든 것이라 이 단계가 지우면 다시 만들어야 한다.
            {
                var cabinet = layout.Find("S11_StageGear/Exploration_A/cabinet3"); var target = cabinet != null ? cabinet.GetComponent<ToolTarget>() : null;
                if (target == null) notes.Add("캐비넷 3 없음");
                else if (root.Find("KeycardReveal") == null) notes.Add("키 카드: KeycardReveal이 없습니다(오버뷰·낙하 연출을 다시 만들어야 합니다)");
            }

            // ---- 상자 보상 비율(하나만 나오는 낡은 상자 전부) ----
            int boxes = 0;
            foreach (var drops in Object.FindObjectsByType<ExplorationDrops>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (drops.gameObject.scene != layout.gameObject.scene || !drops.exclusiveWeighted || drops.entries == null || drops.entries.Length < 2) continue;
                Undo.RecordObject(drops, "Old box loot");
                drops.ConfigureOldBoxLoot(drops.entries[0].uidOffset, drops.entries[1].uidOffset);
                EditorUtility.SetDirty(drops); PrefabUtility.RecordPrefabInstancePropertyModifications(drops); boxes++;
            }
            notes.Add($"상자 보상 {boxes}개 갱신");

            // ---- 주울 수 없던 발견품 자리 옮기기 ----
            Physics.SyncTransforms();
            var zones = layout.GetComponentInChildren<ExplorationTimerZones>(true);
            foreach (var spot in Object.FindObjectsByType<FoundItemSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (spot.gameObject.scene != layout.gameObject.scene || spot.transform.IsChildOf(root)) continue;
                var p = spot.FloorPoint;
                if (!Broken(p, zones)) continue; // 벽·선반 속에 묻혔거나 방 밖인 자리만 옮긴다(조금 좁은 자리는 그대로 둔다)
                // 바닥으로 내려 가까운 빈 자리를 찾는다.
                Vector3 best = default; bool found = false;
                for (float d = .25f; d <= 8f && !found; d += .25f)
                    for (int a = 0; a < 16 && !found; a++)
                    {
                        var probe = new Vector3(p.x + Mathf.Cos(a * Mathf.PI / 8) * d, p.y + 1.2f, p.z + Mathf.Sin(a * Mathf.PI / 8) * d);
                        foreach (var hit in Physics.RaycastAll(probe, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
                            if (hit.normal.y > .9f && Reachable(hit.point, zones)) { best = hit.point; found = true; break; }
                    }
                if (!found) { notes.Add($"{spot.name}: 옮길 자리를 찾지 못함"); continue; }
                Undo.RecordObject(spot.transform, "Move buried spot"); var from = spot.transform.position; spot.transform.position = best;
                PrefabUtility.RecordPrefabInstancePropertyModifications(spot.transform);
                notes.Add($"{spot.name} {from:F1} → {best:F1}");
            }

            Undo.CollapseUndoOperations(undo); EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
            return string.Join(" / ", notes.Where(s => !string.IsNullOrEmpty(s)));
        }

        static bool Apply(SerializedObject so, string field, string value) { so.FindProperty(field).stringValue = value; so.ApplyModifiedPropertiesWithoutUndo(); return true; }

        // 조사 단서 컴포넌트. 이름·설명은 문구 파일(target.<id>.name/.desc/.secret)이 우선한다.
        static ClueInspectable Clue(Transform holder, string id, string title, string description, Transform body, Transform secretMark, float viewSize, float light)
        {
            var clue = holder.gameObject.AddComponent<ClueInspectable>(); var so = new SerializedObject(clue);
            so.FindProperty("targetId").stringValue = id; so.FindProperty("title").stringValue = title; so.FindProperty("description").stringValue = description;
            so.FindProperty("body").objectReferenceValue = body; so.FindProperty("secretMark").objectReferenceValue = secretMark;
            so.FindProperty("viewSize").floatValue = viewSize; so.FindProperty("inspectLight").floatValue = light;
            so.ApplyModifiedPropertiesWithoutUndo();
            return clue;
        }

        // longest: 가장 긴 변을 이 길이(m)로 맞춘다(에셋 원본 크기가 제각각이다). 0이면 원본 크기.
        static Transform Spawn(Transform parent, string prefabPath, float longest = 0f)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath); if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            GameObjectUtility.SetStaticEditorFlags(go, 0);
            if (longest > 0f) { var size = Bounds(go.transform).size; float max = Mathf.Max(size.x, Mathf.Max(size.y, size.z)); if (max > .0001f) go.transform.localScale *= longest / max; }
            return go.transform;
        }

        // 후보 중 바닥이 있고 반지름 r 안에 다른 충돌체가 없는 첫 자리(바닥 높이로 맞춘다).
        static Vector3 FirstFree(Vector3[] candidates, float r, float clearHeight = 0f)
        {
            foreach (var c in candidates)
                if (Physics.Raycast(c + Vector3.up * 1f, Vector3.down, out var hit, 2f, ~0, QueryTriggerInteraction.Ignore) && hit.normal.y > .9f
                    && !Physics.CheckSphere(hit.point + Vector3.up * (r + .05f), r, ~0, QueryTriggerInteraction.Ignore)
                    // 위에 가구(의자·세면대)가 덮고 있지 않은 자리만.
                    && (clearHeight <= 0f || !Physics.CheckCapsule(hit.point + Vector3.up * (r + .05f), hit.point + Vector3.up * clearHeight, r, ~0, QueryTriggerInteraction.Ignore))
                    && !Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Any(o => o.enabled && o.bounds.size.magnitude < 3f && o.bounds.size.y > .05f && o.bounds.Intersects(new Bounds(hit.point + Vector3.up * .3f, new Vector3(r * 2f, .5f, r * 2f))))) return hit.point;
            return candidates[0];
        }

        // 상자·캐비넷 앞 바닥: 사방 중 가장 멀리 트인 쪽(방 안쪽)이 앞이다.
        static Vector3 FrontFloor(Transform box, float distance)
        {
            var b = Bounds(box); var centre = new Vector3(b.center.x, b.min.y, b.center.z); Vector3 best = box.forward; float far = -1f;
            foreach (var dir in new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left })
            {
                float half = Mathf.Abs(dir.x) > .5f ? b.extents.x : b.extents.z;
                var from = centre + Vector3.up * 1f + dir * (half + .06f);
                if (Physics.CheckSphere(from, .04f, ~0, QueryTriggerInteraction.Ignore)) continue;
                float open = Physics.Raycast(from, dir, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore) ? hit.distance : 4f;
                if (open > far) { far = open; best = dir; }
            }
            float reach = (Mathf.Abs(best.x) > .5f ? b.extents.x : b.extents.z) + distance * .6f;
            var at = centre + best * reach;
            return Physics.Raycast(at + Vector3.up * 1f, Vector3.down, out var floor, 2f, ~0, QueryTriggerInteraction.Ignore) ? floor.point + Vector3.up * .02f : at;
        }

        // 확실히 주울 수 없는 자리인가: 물체 속에 묻혔거나, 어느 구역에도 들지 않거나(방 밖), 사방이 모두 막혔다.
        static bool Broken(Vector3 p, ExplorationTimerZones zones)
        {
            var target = p + Vector3.up * .125f;
            if (Physics.CheckSphere(target, .05f, ~0, QueryTriggerInteraction.Ignore)) return true;
            if (zones != null && zones.ZoneAt(p) == "Area_Main") return true;
            for (int a = 0; a < 8; a++)
            {
                var from = p + new Vector3(Mathf.Cos(a * Mathf.PI / 4), .9f, Mathf.Sin(a * Mathf.PI / 4));
                if (!Physics.CheckSphere(from, .25f, ~0, QueryTriggerInteraction.Ignore) && !Physics.Linecast(from, target, ~0, QueryTriggerInteraction.Ignore)) return false;
            }
            return true;
        }

        // 주울 수 있는 자리인가: 구역 안이고, 기물이 묻히지 않고, 사방 1m 중 절반 이상에서 시야가 트인다.
        static bool Reachable(Vector3 p, ExplorationTimerZones zones)
        {
            var target = p + Vector3.up * .125f;
            if (Physics.CheckSphere(target, .05f, ~0, QueryTriggerInteraction.Ignore)) return false;
            if (zones != null && zones.ZoneAt(p) == "Area_Main") return false;
            if (!Physics.Raycast(p + Vector3.up * .3f, Vector3.down, .6f, ~0, QueryTriggerInteraction.Ignore)) return false;
            int open = 0;
            for (int a = 0; a < 8; a++)
            {
                var from = p + new Vector3(Mathf.Cos(a * Mathf.PI / 4), .9f, Mathf.Sin(a * Mathf.PI / 4));
                if (!Physics.CheckSphere(from, .25f, ~0, QueryTriggerInteraction.Ignore) && !Physics.Linecast(from, target, ~0, QueryTriggerInteraction.Ignore)) open++;
            }
            return open >= 4;
        }

        // 발견품 UID 번호(1~99) 중 쓰지 않는 것.
        static int FreeUidOffset(UnityEngine.SceneManagement.Scene scene)
        {
            var used = new HashSet<int>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var m in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (m == null) continue;
                    if (m is ExplorationDrops drops) { if (drops.entries != null) foreach (var e in drops.entries) if (e != null) used.Add(e.uidOffset); continue; }
                    var so = new SerializedObject(m);
                    var uid = so.FindProperty("uidOffset"); if (uid != null && uid.propertyType == SerializedPropertyType.Integer && m is FoundItemSpot) used.Add(uid.intValue);
                    var reward = so.FindProperty("rewardUidOffset"); var item = so.FindProperty("rewardItemId");
                    if (reward != null && item != null && !string.IsNullOrEmpty(item.stringValue)) used.Add(reward.intValue);
                }
            for (int n = 31; n <= 99; n++) if (!used.Contains(n)) return n;
            for (int n = 1; n <= 30; n++) if (!used.Contains(n)) return n;
            return 99;
        }

        // 기물 목록에 키 카드 복제본을 넣는다(구매 불가, 한 번 쓰면 사라진다).
        static string EnsureKeycardItem()
        {
            var catalog = Resources.Load<ItemCatalog>(ItemCatalog.ResourcePath); if (catalog == null) return "기물 목록 없음";
            var so = new SerializedObject(catalog); var items = so.FindProperty("items"); SerializedProperty entry = null;
            for (int i = 0; i < items.arraySize; i++) if (items.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue == KeycardId) entry = items.GetArrayElementAtIndex(i);
            bool added = entry == null;
            if (added) { items.InsertArrayElementAtIndex(items.arraySize); entry = items.GetArrayElementAtIndex(items.arraySize - 1); }
            entry.FindPropertyRelative("id").stringValue = KeycardId; entry.FindPropertyRelative("displayName").stringValue = "키 카드 복제본";
            entry.FindPropertyRelative("wornName").stringValue = ""; entry.FindPropertyRelative("wornAtOrBelow").intValue = -1;
            entry.FindPropertyRelative("use").enumValueIndex = (int)ItemUse.Exploration; entry.FindPropertyRelative("kind").enumValueIndex = (int)ItemKind.Tool;
            entry.FindPropertyRelative("newUses").intValue = 1; entry.FindPropertyRelative("foundUses").intValue = 1; entry.FindPropertyRelative("price").intValue = 0;
            entry.FindPropertyRelative("cooldown").floatValue = 0; entry.FindPropertyRelative("effect").floatValue = 0; entry.FindPropertyRelative("effect2").floatValue = 0;
            const string text = "누군가 복제한 관리실의 키 카드. 관리실의 문을 열 수 있다. 공연 준비자들은 탈출하려 했고, 실패했다. 동료들이 끌려가던 순간, 한 명이 키 카드를 캐비넷에 밀어 넣었다. 부디 다른 사람이 찾아낼 수 있기를.";
            entry.FindPropertyRelative("summary").stringValue = "관리실 문 옆 단말기에 대면 문이 열린다. 한 번 쓰면 사라진다. 구매 불가.";
            entry.FindPropertyRelative("shopDescription").stringValue = text; entry.FindPropertyRelative("shopTooltip").stringValue = ""; entry.FindPropertyRelative("useCue").stringValue = "";
            so.ApplyModifiedProperties(); EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssetIfDirty(catalog);
            return added ? "키 카드 기물 추가" : "";
        }
    }
}
