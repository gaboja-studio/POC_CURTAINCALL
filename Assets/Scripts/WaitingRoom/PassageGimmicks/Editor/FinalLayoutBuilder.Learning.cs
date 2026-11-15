using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CurtainCall.WaitingRoom.PassageGimmicks
{
    // 14단계: 탐색 학습용 기물·단서(2026-11-13 사용자 기획 "탐색 시스템 학습용 기물"). 11·12단계 뒤에 실행한다. 다시 실행할 수 있다.
    // - 찢겨진 노트 낱장 4장: 사용자가 준 그림으로 바꾼다. 2번 낱장은 대기실 로비(휴게 공간 탁자)로 옮긴다. 1·3·4번은 기존 자리.
    // - 떨어진 포스트잇: 소품실 물려줄 선반 아래 바닥.
    public static partial class FinalLayoutBuilder
    {
        const string ClueTextures = "Assets/Resources/Textures/WaitingRoom/Clues/";
        const string PostItId = "final:post-it";
        // 종이 단서를 살펴볼 때의 조명 세기(기본 2.5). 신문은 이미 따로 낮춰 두었다.
        const float PaperInspectLight = .35f;

        [MenuItem("CurtainCall/Waiting Room/Final Layout/14. Build Exploration Learning")]
        static void LearningMenu() => Debug.Log(BuildExplorationLearning());

        public static string BuildExplorationLearning()
        {
            var layout = GameObject.Find(LayoutPath)?.transform;
            if (layout == null) return "WaitingRoom 씬을 여세요.";
            int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Final layout: exploration learning");
            var old = layout.Find(RootName + "/Learning"); if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var root = Child(Child(layout, RootName), "Learning");
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Learning");
            Physics.SyncTransforms();
            var notes = new List<string> { TornNotePictures(layout), PostIt(layout, root), PowerDoors(layout, root), TrashPile(layout, root), HiddenRoomTwo(layout, root), OldBoxes(layout, root), HiddenRoomOne(layout, root), RoutePowerDoor(layout, root, "B1_MachineRoom/Exploration_A/machineDoor", "MachinePowerDoor", "B1_MachineRoom/Exploration_A/HiddenPanel"), VestibuleGaps(layout, root), CautionDoors(layout, root), CircuitBreakEffects(layout, root) };
            Physics.SyncTransforms();
            Undo.CollapseUndoOperations(undo); EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
            return string.Join(" / ", notes.Where(n => !string.IsNullOrEmpty(n)));
        }

        // ---- 전기로 열리는 문 2개(작업실·소품실 전실, 기계실 입구) ----
        // 푸른 문, 문 가운데 알림 조명(닫힘 빨강·열림 초록), 전기 표식. 위아래로 열린다.
        // 배터리를 소켓에 꽂으면 열리고 빼면 닫힌다. 전실 문은 작업실 벽 배관함을 부수고 회로를 망가뜨리면 열린 채 고정된다.
        const string CraftSocketId = WaitingRoomShop.CraftRoomsSocketId, CraftUnlockedId = WaitingRoomShop.CraftRoomsUnlockedId;
        const string CraftPipeBoxId = "learning:craft-pipebox", CraftCircuitId = "learning:craft-circuit", MachineSocketId = "dorm-vent:power-socket";
        const string SignFolder = "Assets/Resources/Textures/WaitingRoom/Signs";
        // 전실 문 자리: 로비에서 전실로 들어가는 목(전실 안쪽으로 조금 들어간 곳). 전실 폭 x -5.2~-2.1, 천장 4.3m.
        const float CraftDoorZ = 62.9f, CraftDoorTop = 4.25f;

        static string PowerDoors(Transform layout, Transform root)
        {
            var blue = SimpleMaterial("PowerDoorBlue", new Color(.12f, .27f, .56f), .35f, .45f, false);
            var trim = SimpleMaterial("PowerDoorTrim", new Color(.07f, .1f, .16f), .5f, .4f, false);
            var lampMat = SimpleMaterial("PowerDoorLamp", new Color(1f, .12f, .08f), 0f, .6f, true);
            var sign = PowerSignMaterial(); var notes = new List<string>();

            // (1) 전실 문: 위쪽 문짝은 위로, 아래쪽 문짝은 바닥 속으로 내려간다.
            {
                string openedBy = CraftSocketId + "|" + CraftCircuitId;
                float x0 = VestibuleX0, x1 = VestibuleX1, mid = CraftDoorTop * .5f, half = .07f, z = CraftDoorZ;
                var door = Child(root, "CraftPowerDoor");
                var slab = door.gameObject.AddComponent<BoxCollider>(); slab.center = new Vector3((x0 + x1) * .5f, mid, z); slab.size = new Vector3(x1 - x0, CraftDoorTop, .2f);
                door.gameObject.AddComponent<Pursuit.PursuitDoorObstacle>().Source = slab;
                var gate = door.gameObject.AddComponent<ExplorationGate>(); var gso = new SerializedObject(gate);
                gso.FindProperty("targetId").stringValue = "learning:craft-door"; gso.FindProperty("label").stringValue = "전기 문";
                gso.FindProperty("kind").enumValueIndex = (int)ExplorationGateKind.Open; gso.FindProperty("openedBy").stringValue = openedBy;
                gso.FindProperty("blocker").objectReferenceValue = slab; gso.ApplyModifiedPropertiesWithoutUndo();
                var lamps = new List<Renderer>();
                foreach (var (name, y0, y1, travel) in new[] { ("UpperLeaf", mid, CraftDoorTop, mid + .05f), ("LowerLeaf", 0f, mid, -(mid + .05f)) })
                {
                    var leaf = Child(door, name); leaf.position = new Vector3((x0 + x1) * .5f, (y0 + y1) * .5f, z);
                    Plain(Box(leaf, "Panel", blue, new Vector3(x0, y0 + .005f, z - half), new Vector3(x1, y1 - .005f, z + half), false));
                    foreach (float side in new[] { -1f, 1f })
                    {
                        float face = z + side * half;
                        // 가로 보강대와 맞물리는 쪽 테두리.
                        Plain(Box(leaf, "Rib", trim, new Vector3(x0 + .1f, (y0 + y1) * .5f - .06f, face - (side < 0 ? .03f : 0)), new Vector3(x1 - .1f, (y0 + y1) * .5f + .06f, face + (side > 0 ? .03f : 0)), false));
                        float seam = name == "UpperLeaf" ? y0 : y1 - .1f;
                        Plain(Box(leaf, "SeamEdge", trim, new Vector3(x0, seam, face - (side < 0 ? .02f : 0)), new Vector3(x1, seam + .1f, face + (side > 0 ? .02f : 0)), false));
                        if (name != "UpperLeaf") continue;
                        // 문 가운데 알림 조명(위쪽 문짝 아래 끝)과 전기 표식.
                        var lamp = Box(leaf, "CenterLamp", lampMat, new Vector3(-3.65f - .14f, y0 + .12f, face - (side < 0 ? .05f : 0)), new Vector3(-3.65f + .14f, y0 + .4f, face + (side > 0 ? .05f : 0)), false);
                        Plain(lamp); lamps.Add(lamp.GetComponent<Renderer>());
                        SignQuad(leaf, sign, new Vector3(-3.65f, y0 + 1.15f, face + side * .034f), side > 0 ? Vector3.forward : Vector3.back, .7f);
                    }
                    var slide = leaf.gameObject.AddComponent<ExplorationSlide>(); var sso = new SerializedObject(slide);
                    sso.FindProperty("openedBy").stringValue = openedBy; sso.FindProperty("openOffset").vector3Value = new Vector3(0, travel, 0); sso.FindProperty("seconds").floatValue = 1.6f;
                    sso.ApplyModifiedPropertiesWithoutUndo();
                }
                // 문틀 위 알림 조명(문이 열려 문짝이 사라져도 보인다). 로비 쪽과 전실 쪽 한 개씩.
                foreach (float side in new[] { -1f, 1f })
                {
                    var head = Box(door, "FrameLamp", lampMat, new Vector3(-3.65f - .16f, CraftDoorTop - .28f, z + side * .12f - .03f), new Vector3(-3.65f + .16f, CraftDoorTop - .06f, z + side * .12f + .03f), false);
                    Plain(head); lamps.Add(head.GetComponent<Renderer>());
                    Plain(Box(door, "FrameHeader", trim, new Vector3(x0, CraftDoorTop - .34f, z + side * .09f - .02f), new Vector3(x1, CraftDoorTop, z + side * .09f + .02f), false));
                }
                AddLamp(door, openedBy, lamps, new Vector3(-3.65f, 2.3f, z + .5f));
                AddGuide(door, new Vector3(-3.65f, 0, z + 2f));

                // 소켓: 문 앞(로비 쪽) 전실 서벽. 벽에 붙은 상자와 꽂는 구멍.
                float wallX = x0, sz = z + .55f;
                var socket = Child(root, "CraftSocket"); socket.position = new Vector3(wallX + .1f, 1.2f, sz);
                var sv = Child(socket, "Visual");
                Plain(Box(sv, "SocketBox", trim, new Vector3(wallX, .85f, sz - .22f), new Vector3(wallX + .16f, 1.55f, sz + .22f), false));
                Plain(Box(sv, "SocketSlot", lampMat, new Vector3(wallX + .16f, 1.08f, sz - .1f), new Vector3(wallX + .175f, 1.32f, sz + .1f), false));
                SignQuad(sv, sign, new Vector3(wallX + .004f, 2.0f, sz), Vector3.right, .5f);
                var point = Child(socket, "InteractionPoint"); point.position = new Vector3(wallX + .3f, 1.2f, sz);
                var sg = socket.gameObject.AddComponent<ExplorationGate>(); var so = new SerializedObject(sg);
                so.FindProperty("targetId").stringValue = CraftSocketId; so.FindProperty("label").stringValue = "동력 소켓";
                so.FindProperty("kind").enumValueIndex = (int)ExplorationGateKind.Open; so.FindProperty("toolId").stringValue = ItemUses.PowerBattery;
                so.FindProperty("bareHandHits").intValue = 0; so.FindProperty("socket").boolValue = true;
                // 로비 쪽(문 북쪽)에 선 사람만 닿는다. 회로가 부서져 문이 고정되면 소켓 안내는 사라진다(2026-11-14 사용자).
                var reachFront = Child(socket, "ReachFront"); reachFront.SetPositionAndRotation(new Vector3(socket.position.x, 1.2f, z + .12f), Quaternion.LookRotation(Vector3.forward));
                so.FindProperty("reachFront").objectReferenceValue = reachFront; so.FindProperty("retiredBy").stringValue = CraftCircuitId;
                so.FindProperty("removeGuideId").stringValue = "power-circuit"; // 배터리를 빼면 문을 고정하는 방법을 한 번 알려 준다
                so.FindProperty("interactionPoint").objectReferenceValue = point; so.ApplyModifiedPropertiesWithoutUndo();
                // 한 번이라도 열면 "해금"으로 남는다(상점의 공연 기물 구매 조건). 배터리를 빼도 지워지지 않는다.
                foreach (var way in new[] { CraftSocketId, CraftCircuitId })
                {
                    var latch = root.gameObject.AddComponent<ExplorationAllOf>(); var lso = new SerializedObject(latch);
                    var required = lso.FindProperty("required"); required.arraySize = 1; required.GetArrayElementAtIndex(0).stringValue = way;
                    lso.FindProperty("openTarget").stringValue = CraftUnlockedId; lso.ApplyModifiedPropertiesWithoutUndo();
                }

                // 배관함과 회로: 작업실 동벽(전실과 맞닿은 벽) 안쪽. 배관함 덮개를 부수면 회로가 드러나고, 회로를 부수면 문이 열린 채 고정된다.
                // 문·창살이 아닌 막힌 벽면을 찾는다: 높이 세 곳·좌우 0.45m가 모두 같은 벽면(이름에 Wall)이어야 한다.
                float bx = x0 - .5f, bz = 58.2f; bool solid = false;
                for (float tryZ = 54.6f; tryZ <= 61f && !solid; tryZ += .4f) // 문 남쪽, 기둥(충돌체 없음, z 52.6~53)과 문 사이 벽면부터
                {
                    float? plane = null; solid = true;
                    foreach (float dy in new[] { .85f, 1.3f, 1.8f })
                        foreach (float dz in new[] { -.45f, 0f, .45f })
                        {
                            if (!Physics.Raycast(new Vector3(x0 - 2.5f, dy, tryZ + dz), Vector3.right, out var wall, 4f, ~0, QueryTriggerInteraction.Ignore) || !wall.collider.name.Contains("Wall")) { solid = false; continue; }
                            plane ??= wall.point.x; if (Mathf.Abs(wall.point.x - plane.Value) > .01f) solid = false;
                        }
                    // 기둥·가구가 앞을 가리지 않아야 한다(벽 앞 0.6m가 비어 있어야 손이 닿는다).
                    if (solid && plane is float clear && Physics.CheckBox(new Vector3(clear - .36f, 1.3f, tryZ), new Vector3(.3f, .5f, .5f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) solid = false;
                    if (solid && plane is float found) { bx = found; bz = tryZ; }
                }
                if (!solid) notes.Add("배관함: 작업실 동벽에서 막힌 벽면을 찾지 못해 기본 자리에 둠");
                var box = Child(root, "CraftPipeBox"); box.position = new Vector3(bx - .1f, 1.3f, bz);
                var cover = Child(box, "Cover");
                Plain(Box(cover, "CoverPlate", Mat("LA_SteelPlate") ?? trim, new Vector3(bx - .14f, .8f, bz - .4f), new Vector3(bx - .1f, 1.8f, bz + .4f), false));
                Plain(Box(box, "BoxBack", trim, new Vector3(bx - .1f, .78f, bz - .42f), new Vector3(bx, 1.82f, bz + .42f), false));
                SignQuad(cover, sign, new Vector3(bx - .144f, 1.45f, bz), Vector3.left, .34f);
                var bp = Child(box, "InteractionPoint"); bp.position = new Vector3(bx - .35f, 1.3f, bz);
                var bg = box.gameObject.AddComponent<ExplorationGate>(); var bso = new SerializedObject(bg);
                bso.FindProperty("targetId").stringValue = CraftPipeBoxId; bso.FindProperty("label").stringValue = "배관함";
                bso.FindProperty("kind").enumValueIndex = (int)ExplorationGateKind.Breakable; bso.FindProperty("toolId").stringValue = ItemUses.StakePuller;
                bso.FindProperty("requiredHits").intValue = 1; bso.FindProperty("bareHandHits").intValue = 3;
                bso.FindProperty("interactionPoint").objectReferenceValue = bp; Refs(bso.FindProperty("hideOnOpen"), cover.gameObject);
                bso.ApplyModifiedPropertiesWithoutUndo();
                var circuit = Child(root, "CraftCircuit"); circuit.position = new Vector3(bx - .08f, 1.3f, bz);
                var cv = Child(circuit, "Visual");
                // 회로 기판: 초록 기판(배선·칩 무늬) 위에 숫자판 단추와 상태등. 살아 있을 때의 부품(Live)과 부서진 뒤(Broken)를 나눠 둔다.
                // 배관함 뒤판(벽에서 10cm)보다 앞에 놓아야 보인다. 덮개(10~14cm)가 닫혀 있는 동안은 덮개 속에 가려진다.
                float boardFace = bx - .108f;
                Plain(Box(cv, "BoardBack", trim, new Vector3(bx - .07f, 1.0f, bz - .3f), new Vector3(bx - .05f, 1.62f, bz + .3f), false));
                var pcb = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.DestroyImmediate(pcb.GetComponent<Collider>()); pcb.name = "CircuitBoard"; pcb.transform.SetParent(cv, false);
                pcb.transform.SetPositionAndRotation(new Vector3(boardFace, 1.31f, bz), Quaternion.LookRotation(Vector3.right, Vector3.up)); pcb.transform.localScale = new Vector3(.58f, .6f, 1f);
                pcb.GetComponent<Renderer>().sharedMaterial = CircuitMaterial(); pcb.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var live = Child(cv, "Live"); var keyMat = SimpleMaterial("CircuitKey", new Color(.78f, .78f, .74f), .1f, .4f, false);
                for (int row = 0; row < 4; row++) for (int col = 0; col < 3; col++)
                {
                    float ky = 1.5f - row * .085f, kz = bz + .08f + col * .075f;
                    Plain(Box(live, "Key", keyMat, new Vector3(boardFace - .018f, ky - .03f, kz - .028f), new Vector3(boardFace, ky + .03f, kz + .028f), false));
                }
                foreach (float dz in new[] { -.22f, -.15f, -.08f })
                    Plain(Box(live, "StatusLed", lampMat, new Vector3(boardFace - .012f, 1.5f, bz + dz - .018f), new Vector3(boardFace, 1.54f, bz + dz + .018f), false));
                foreach (var (y, z0c, z1c) in new[] { (1.36f, -.24f, -.06f), (1.2f, -.24f, -.1f), (1.1f, -.2f, .0f) })
                    Plain(Box(cv, "Chip", trim, new Vector3(boardFace - .02f, y - .04f, bz + z0c), new Vector3(boardFace, y + .04f, bz + z1c), false));
                foreach (float dz in new[] { -.2f, -.12f, .2f }) // 원통 부품(축전기)
                {
                    var cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.DestroyImmediate(cap.GetComponent<Collider>()); cap.name = "Capacitor"; cap.transform.SetParent(cv, false);
                    cap.transform.SetPositionAndRotation(new Vector3(boardFace - .03f, 1.04f, bz + dz), Quaternion.Euler(0, 0, 90f)); cap.transform.localScale = new Vector3(.04f, .03f, .04f);
                    cap.GetComponent<Renderer>().sharedMaterial = blue;
                }
                // 부서진 뒤: 그을린 자국과 지지직거리는 불꽃(폐기물 보관소의 고장 난 접속함 불꽃을 그대로 가져온다).
                var broken = Child(circuit, "Broken");
                Plain(Box(broken, "Scorch", SimpleMaterial("CircuitScorch", new Color(.03f, .03f, .03f), 0f, .1f, false), new Vector3(boardFace - .004f, 1.18f, bz - .02f), new Vector3(boardFace - .002f, 1.56f, bz + .28f), false));
                var sparkSource = layout.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "FaultyJunction_Sparks");
                if (sparkSource != null)
                {
                    var sparks = Object.Instantiate(sparkSource.gameObject, broken); sparks.name = "CircuitSparks"; sparks.SetActive(true);
                    sparks.transform.SetPositionAndRotation(new Vector3(boardFace - .04f, 1.36f, bz + .1f), Quaternion.LookRotation(Vector3.left, Vector3.up));
                    foreach (var t in sparks.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
                    // 불꽃만 쓴다: 함께 딸려 온 접속함 모양·충돌체·조명 스크립트 외 부품은 끈다.
                    foreach (var mesh in sparks.GetComponentsInChildren<MeshRenderer>(true)) mesh.enabled = false;
                    foreach (var collider in sparks.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                }
                var cp = Child(circuit, "InteractionPoint"); cp.position = new Vector3(bx - .35f, 1.3f, bz);
                var cg = circuit.gameObject.AddComponent<ExplorationGate>(); var cso = new SerializedObject(cg);
                cso.FindProperty("targetId").stringValue = CraftCircuitId; cso.FindProperty("label").stringValue = "회로";
                cso.FindProperty("kind").enumValueIndex = (int)ExplorationGateKind.Breakable; cso.FindProperty("toolId").stringValue = ItemUses.StakePuller;
                cso.FindProperty("requiredHits").intValue = 1; cso.FindProperty("bareHandHits").intValue = 3;
                var needs = cso.FindProperty("requiredTargets"); needs.arraySize = 1; needs.GetArrayElementAtIndex(0).stringValue = CraftPipeBoxId;
                cso.FindProperty("revealTarget").stringValue = CraftPipeBoxId;
                cso.FindProperty("interactionPoint").objectReferenceValue = cp; Refs(cso.FindProperty("hideOnOpen"), cv.Find("Live").gameObject);
                Refs(cso.FindProperty("showOnOpen"), circuit.Find("Broken").gameObject);
                Refs(cso.FindProperty("hiddenVisuals"), cv.gameObject);
                cso.ApplyModifiedPropertiesWithoutUndo();
                notes.Add($"전실 전기 문 z {z:F1}, 소켓 ({wallX:F1}, {sz:F1}), 배관함·회로 ({bx:F1}, {bz:F1})");
            }

            // (2) 기계실 입구 문: 기존 셔터(위로 올라감)를 같은 푸른 문으로 바꾸고 소켓을 새 규칙으로 바꾼다. 입구 앞 배터리는 없앤다.
            {
                var group = layout.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "PowerSocket" && t.Find("Shutter") != null);
                var socketGate = group != null ? group.Find("Socket")?.GetComponent<ExplorationGate>() : null;
                if (socketGate == null) notes.Add("기계실 입구: 소켓을 찾지 못했습니다");
                else
                {
                    var so = new SerializedObject(socketGate); so.FindProperty("socket").boolValue = true; so.ApplyModifiedProperties(); PrefabUtility.RecordPrefabInstancePropertyModifications(socketGate);
                    if (group.Find("BatterySpot") is Transform spare && spare.gameObject.activeSelf) { Undo.RecordObject(spare.gameObject, "Remove spare battery"); spare.gameObject.SetActive(false); }
                    var shutter = group.Find("Shutter"); var leaves = shutter.Find("RustDoor");
                    var lamps = new List<Renderer>(); int painted = 0;
                    foreach (var stale in shutter.Cast<Transform>().Where(t => t.name == "PowerDoorDress").ToArray()) Undo.DestroyObjectImmediate(stale.gameObject);
                    var dress = Child(shutter, "PowerDoorDress"); dress.position = Vector3.zero;
                    if (leaves != null)
                        foreach (var leaf in leaves.GetComponentsInChildren<Renderer>(true).Where(r => r.name == "Leaf"))
                        {
                            Undo.RecordObject(leaf, "Paint power door"); leaf.sharedMaterial = blue; painted++;
                            var b = leaf.bounds; bool faceEast = b.center.x > shutter.position.x; float face = faceEast ? b.max.x : b.min.x, side = faceEast ? 1f : -1f;
                            var lamp = Box(dress, "CenterLamp", lampMat, new Vector3(face - (side < 0 ? .05f : 0), b.center.y - .14f, b.center.z - .14f), new Vector3(face + (side > 0 ? .05f : 0), b.center.y + .14f, b.center.z + .14f), false);
                            Plain(lamp); lamps.Add(lamp.GetComponent<Renderer>());
                            SignQuad(dress, sign, new Vector3(face + side * .004f, b.center.y + .62f, b.center.z), side > 0 ? Vector3.right : Vector3.left, .5f);
                        }
                    AddLamp(dress, MachineSocketId, lamps, shutter.position + Vector3.up * 1.2f);
                    var guideAt = socketGate.transform.position; guideAt.y = shutter.position.y;
                    AddGuide(Child(root, "MachineDoorGuide"), guideAt);
                    notes.Add($"기계실 입구 문: 문짝 {painted}장 푸른색, 소켓 새 규칙, 입구 앞 배터리 제거");
                }
            }
            return string.Join(" / ", notes);
        }

        // ---- 탈출로 입구 문: 겉모습만 전기 문(2026-11-13 사용자). 여는 조건은 그대로(관리실 컴퓨터 암호 + 탈출로 배선) ----
        // 옛 문짝(철문·리벳·잠금 바퀴 등)은 끄고 푸른 문짝 두 장을 세운다. 위쪽은 위로, 아래쪽은 바닥 속으로 열린다.
        // 2026-11-14 사용자: 탈출로 문은 옛 외형으로 되돌리고, 대신 탈출로로 가는 길(기계실 숨겨진 문)을 전기 문 모습으로 한다.
        // gatePath의 문(x면에 선 문)에 푸른 문짝을 세운다. alsoHide는 함께 끌 옛 겉모습(벽처럼 보이던 판 등).
        static string RoutePowerDoor(Transform layout, Transform root, string gatePath, string doorName, params string[] alsoHide)
        {
            var gate = layout.Find(gatePath)?.GetComponent<ExplorationGate>();
            var slab = gate != null ? gate.GetComponent<BoxCollider>() : null;
            if (slab == null) return doorName + ": 대상을 찾지 못했습니다";
            foreach (var path in alsoHide)
                if (layout.Find(path) is Transform extra && extra.gameObject.activeSelf) { Undo.RecordObject(extra.gameObject, "Hide old door look"); extra.gameObject.SetActive(false); }
            var so = new SerializedObject(gate); string openedBy = so.FindProperty("openedBy").stringValue;
            // 옛 문짝 끄기: 문이 열릴 때 사라지던 것들. 다시 켜지지 않게 두 목록에서 뺀다.
            var body = so.FindProperty("hideOnOpen"); var shown = so.FindProperty("hiddenVisuals"); var oldParts = new HashSet<Object>(); int hiddenOld = 0;
            for (int i = 0; i < body.arraySize; i++) if (body.GetArrayElementAtIndex(i).objectReferenceValue is GameObject part) oldParts.Add(part);
            if (oldParts.Count > 0)
            {
                foreach (GameObject part in oldParts) if (part.activeSelf) { Undo.RecordObject(part, "Hide old escape door"); part.SetActive(false); hiddenOld++; }
                for (int i = shown.arraySize - 1; i >= 0; i--)
                {
                    var element = shown.GetArrayElementAtIndex(i);
                    if (element.objectReferenceValue == null || oldParts.Contains(element.objectReferenceValue)) { element.objectReferenceValue = null; shown.DeleteArrayElementAtIndex(i); }
                }
                body.arraySize = 0;
            }
            so.FindProperty("openSlide").vector3Value = Vector3.zero; so.FindProperty("openSlideSeconds").floatValue = 0f;
            so.ApplyModifiedProperties(); PrefabUtility.RecordPrefabInstancePropertyModifications(gate);

            var blue = SimpleMaterial("PowerDoorBlue", new Color(.12f, .27f, .56f), .35f, .45f, false);
            var trim = SimpleMaterial("PowerDoorTrim", new Color(.07f, .1f, .16f), .5f, .4f, false);
            var lampMat = SimpleMaterial("PowerDoorLamp", new Color(1f, .12f, .08f), 0f, .6f, true);
            var sign = PowerSignMaterial();
            // 문은 x면에 서 있다(두께 x, 폭 z).
            var b = slab.bounds; float x = b.center.x, z0 = b.min.z, z1 = b.max.z, y0 = b.min.y, top = b.max.y, mid = (y0 + top) * .5f, half = .07f, zc = b.center.z;
            var door = Child(root, doorName); var lamps = new List<Renderer>();
            foreach (var (name, low, high, travel) in new[] { ("UpperLeaf", mid, top, top - mid + .05f), ("LowerLeaf", y0, mid, -(mid - y0 + .05f)) })
            {
                var leaf = Child(door, name); leaf.position = new Vector3(x, (low + high) * .5f, zc);
                Plain(Box(leaf, "Panel", blue, new Vector3(x - half, low + .005f, z0), new Vector3(x + half, high - .005f, z1), false));
                foreach (float side in new[] { -1f, 1f })
                {
                    float face = x + side * half;
                    Plain(Box(leaf, "Rib", trim, new Vector3(face - (side < 0 ? .03f : 0), (low + high) * .5f - .06f, z0 + .1f), new Vector3(face + (side > 0 ? .03f : 0), (low + high) * .5f + .06f, z1 - .1f), false));
                    float seam = name == "UpperLeaf" ? low : high - .1f;
                    Plain(Box(leaf, "SeamEdge", trim, new Vector3(face - (side < 0 ? .02f : 0), seam, z0), new Vector3(face + (side > 0 ? .02f : 0), seam + .1f, z1), false));
                    if (name != "UpperLeaf") continue;
                    var lamp = Box(leaf, "CenterLamp", lampMat, new Vector3(face - (side < 0 ? .05f : 0), low + .12f, zc - .14f), new Vector3(face + (side > 0 ? .05f : 0), low + .4f, zc + .14f), false);
                    Plain(lamp); lamps.Add(lamp.GetComponent<Renderer>());
                    SignQuad(leaf, sign, new Vector3(face + side * .034f, low + 1.0f, zc), side > 0 ? Vector3.right : Vector3.left, .6f);
                }
                var slide = leaf.gameObject.AddComponent<ExplorationSlide>(); var sso = new SerializedObject(slide);
                // followGate: 보이는 문짝이 통과 판정(막음 충돌)과 같은 상태를 따른다.
                sso.FindProperty("openedBy").stringValue = openedBy; sso.FindProperty("followGate").objectReferenceValue = gate;
                sso.FindProperty("openOffset").vector3Value = new Vector3(0, travel, 0); sso.FindProperty("seconds").floatValue = 1.6f;
                sso.ApplyModifiedPropertiesWithoutUndo();
            }
            AddLamp(door, openedBy, lamps, new Vector3(x + .5f, mid, zc));
            return $"{doorName}: 푸른 문짝 2장(위·아래로 열림), 옛 겉모습 {hiddenOld}개 끔, 여는 조건 그대로({openedBy})";
        }

        // ---- 전기 문 세 곳의 겉모습을 경고 줄무늬 철문 에셋으로(2026-11-14 사용자) ----
        // 푸른 문짝(도형)은 끄고 에셋 문틀을 세운다. 문짝 높이를 문 자리 높이에 맞춰 키우고(문틀 바깥쪽은 벽·바닥 속에 묻힌다),
        // 문짝은 위로 올라가 열린다. 여는 조건·막음 충돌은 그대로다. 문짝 양옆에 남는 문틀 자리는 충돌체로 막는다.
        const string CautionDoorPrefab = "Assets/_ThirdParty/Synty/PolygonApocalypse/Prefabs/Buildings/SM_Bld_Bunker_Wall_Caution_Door_01.prefab";
        // 에셋 치수(m): 문틀 2.5 x 3.0, 문짝 0.96 x 2.03(바닥에서 0.29 위), 문짝 가운데 = 문틀 가운데.
        const float CautionFrameWidth = 2.5f, CautionLeafWidth = .96f, CautionLeafHeight = 2.03f, CautionSill = .29f;

        static string CautionDoors(Transform layout, Transform root)
        {
            var notes = new List<string>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CautionDoorPrefab);
            if (prefab == null) return "경고 줄무늬 문 에셋 없음";
            var lampMat = SimpleMaterial("PowerDoorLamp", new Color(1f, .12f, .08f), 0f, .6f, true);

            // (1) 전실 문: 벽 없이 서 있는 문이라 문틀 위 알림 조명은 그대로 쓴다.
            if (root.Find("CraftPowerDoor") is Transform craft && craft.GetComponent<BoxCollider>() is BoxCollider craftSlab)
                notes.Add(One(craft, craftSlab.bounds, false, craft.GetComponent<ExplorationGate>(), null, null,
                    new[] { craft.Find("UpperLeaf"), craft.Find("LowerLeaf") }));
            // (2) 탈출로로 가는 길(기계실 숨겨진 문).
            var hiddenGate = layout.Find("B1_MachineRoom/Exploration_A/machineDoor")?.GetComponent<ExplorationGate>();
            if (root.Find("MachinePowerDoor") is Transform machine && hiddenGate != null && hiddenGate.GetComponent<BoxCollider>() is BoxCollider hiddenSlab)
                notes.Add(One(machine, hiddenSlab.bounds, true, hiddenGate, machine.GetComponent<PowerDoorLamp>(), new Vector2(-4.3f, -3.8f),
                    new[] { machine.Find("UpperLeaf"), machine.Find("LowerLeaf") }));
            // (3) 기계실 입구(설비실) 문: 셔터는 막음 충돌을 들고 올라가는 역할만 남기고 겉모습을 끈다.
            var group = layout.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "PowerSocket" && t.Find("Shutter") != null);
            var socket = group != null ? group.Find("Socket")?.GetComponent<ExplorationGate>() : null;
            if (socket != null)
            {
                var shutter = group.Find("Shutter"); var dress = shutter.Find("PowerDoorDress");
                var hide = shutter.Cast<Transform>().Where(t => t.name == "ShutterSlat" || t.name == "RustDoor")
                    .Concat(dress != null ? dress.Cast<Transform>().Where(t => t.GetComponent<Renderer>() != null) : Enumerable.Empty<Transform>()).ToArray();
                var opening = new Bounds(); opening.SetMinMax(new Vector3(23.30f, -5f, 77.16f), new Vector3(23.85f, -3.06f, 78.44f));
                notes.Add(One(group, opening, true, socket, dress != null ? dress.GetComponent<PowerDoorLamp>() : null, new Vector2(23.30f, 23.85f), hide));
            }
            return string.Join(" / ", notes);

            // owner 아래에 CautionDoor를 만든다. opening: 문 자리(두께 방향 포함), xPlane: 문이 x면에 서 있는가.
            // wall: 문이 박힌 벽의 두께 범위(x). 주면 벽 양쪽 면 문 위에 알림 조명을 새로 달아 lamp에 연결한다.
            string One(Transform owner, Bounds opening, bool xPlane, ExplorationGate follow, PowerDoorLamp lamp, Vector2? wall, Transform[] hide)
            {
                foreach (var stale in owner.Cast<Transform>().Where(t => t.name == "CautionDoor").ToArray()) Undo.DestroyObjectImmediate(stale.gameObject);
                foreach (var old in hide) if (old != null && old.gameObject.activeSelf) { Undo.RecordObject(old.gameObject, "Hide blue leaf"); old.gameObject.SetActive(false); }
                float width = xPlane ? opening.size.z : opening.size.x, height = opening.size.y, floor = opening.min.y;
                float scale = height / CautionLeafHeight, leafWidth = CautionLeafWidth * scale;
                var holder = new GameObject("CautionDoor").transform; holder.SetParent(owner, false); holder.position = Vector3.zero; holder.rotation = Quaternion.identity;
                Undo.RegisterCreatedObjectUndo(holder.gameObject, "Caution door");
                var frame = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, holder)).transform; frame.name = "Frame";
                foreach (var c in frame.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c); // 문틀 통짜 충돌체는 문짝 자리까지 막는다
                foreach (var t in frame.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
                frame.localScale = Vector3.one * scale;
                // 에셋: 문틀이 로컬 x -2.5~0, 앞면이 +z. x면 문은 90도 돌려 앞면이 +x를 본다.
                Vector3 centre = opening.center;
                if (xPlane) { frame.rotation = Quaternion.Euler(0, 90f, 0); frame.position = new Vector3(centre.x, floor - CautionSill * scale, centre.z - CautionFrameWidth * .5f * scale); }
                else frame.position = new Vector3(centre.x + CautionFrameWidth * .5f * scale, floor - CautionSill * scale, centre.z);
                var leaf = frame.Cast<Transform>().First();
                var slide = leaf.gameObject.AddComponent<ExplorationSlide>(); var sso = new SerializedObject(slide);
                sso.FindProperty("openedBy").stringValue = follow.IsSocket ? follow.TargetId : follow.OpenedBy; sso.FindProperty("followGate").objectReferenceValue = follow;
                // 이동 거리는 문짝의 부모(문틀) 기준이라 배율로 나눈다.
                sso.FindProperty("openOffset").vector3Value = new Vector3(0, (height + .05f) / scale, 0); sso.FindProperty("seconds").floatValue = 1.6f;
                sso.ApplyModifiedPropertiesWithoutUndo();
                // 문짝 양옆에 남는 문틀 자리: 문이 열려도 지나갈 수 없게 막는다.
                float gap = (width - leafWidth) * .5f;
                if (gap > .05f)
                    foreach (float side in new[] { -1f, 1f })
                    {
                        var jamb = new GameObject("JambCollider").transform; jamb.SetParent(holder, false);
                        float along = (xPlane ? centre.z : centre.x) + side * (leafWidth * .5f + gap * .5f);
                        jamb.position = xPlane ? new Vector3(centre.x, floor + height * .5f, along) : new Vector3(along, floor + height * .5f, centre.z);
                        jamb.gameObject.AddComponent<BoxCollider>().size = xPlane ? new Vector3(.3f, height, gap) : new Vector3(gap, height, .3f);
                    }
                int lamps = 0;
                if (lamp != null && wall is Vector2 faces)
                {
                    var list = new List<Renderer>();
                    foreach (float face in new[] { faces.x, faces.y })
                    {
                        float outward = face < centre.x ? -1f : 1f;
                        var box = Box(holder, "WallLamp", lampMat, new Vector3(Mathf.Min(face, face + outward * .05f), opening.max.y + .1f, centre.z - .14f),
                            new Vector3(Mathf.Max(face, face + outward * .05f), opening.max.y + .38f, centre.z + .14f), false);
                        Plain(box); list.Add(box.GetComponent<Renderer>());
                    }
                    var lso = new SerializedObject(lamp); Refs(lso.FindProperty("lamps"), list.Cast<Object>().ToArray()); lso.ApplyModifiedPropertiesWithoutUndo(); lamps = list.Count;
                }
                return $"{owner.name}: 경고 줄무늬 문(배율 {scale:F2}, 문짝 {leafWidth:F2} x {height:F2}m, 옆 막음 {(gap > .05f ? gap.ToString("F2") + "m" : "없음")}, 새 조명 {lamps})";
            }
        }

        // ---- 전실 양옆 틈 메우기(2026-11-14 사용자: 작업실·소품실 통로에서 밖이 보임) ----
        // 전실 옆벽(로비 쪽 목)과 두 방의 바깥벽이 맞닿지 않아, 그 사이(동쪽 약 30cm, 서쪽 약 17cm)로 벽 뒤 빈 공간이 보였다.
        // 두 벽을 잇는 벽 조각으로 메운다. 문틀은 건드리지 않는다.
        static string VestibuleGaps(Transform layout, Transform root)
        {
            var mat = Mat("LA_ConcreteRough") ?? Mat("LA_WallConcrete"); var fill = Child(root, "VestibuleGapFill");
            var east = layout.Find(RootName + "/LobbyConnections/CraftVestibule")?.Cast<Transform>().Where(t => t.name == "SideWall").Select(t => t.GetComponent<Renderer>().bounds).OrderByDescending(b => b.center.x).FirstOrDefault();
            var west = layout.Find(RootName + "/LobbyConnections/CraftVestibule")?.Cast<Transform>().Where(t => t.name == "SideWall").Select(t => t.GetComponent<Renderer>().bounds).OrderBy(b => b.center.x).FirstOrDefault();
            if (east == null || west == null || east.Value.size == Vector3.zero) return "전실 틈: 옆벽을 찾지 못했습니다";
            Bounds e = east.Value, w = west.Value;
            // 동쪽: 소품실 바깥벽 서쪽 끝(x -1.5)과 전실 옆벽(x -2.1~-1.8) 사이. 옆벽 안쪽 면에 맞춰 방 바깥벽 속까지 겹쳐 넣는다.
            Box(fill, "GapFillEast", mat, new Vector3(e.min.x, 0, 61.44f), new Vector3(-1.46f, 5.6f, e.min.z + .06f), true);
            Box(fill, "GapFillEastBack", mat, new Vector3(e.max.x - .02f, 0, e.min.z), new Vector3(-1.46f, 5.6f, e.min.z + .3f), true);
            // 서쪽: 작업실 바깥벽 동쪽 끝(x -5.67)과 전실 옆벽(x -5.5~-5.2) 사이.
            Box(fill, "GapFillWest", mat, new Vector3(-5.7f, 0, 61.14f), new Vector3(w.max.x, 6f, w.min.z + .06f), true);
            Box(fill, "GapFillWestBack", mat, new Vector3(-5.7f, 0, w.min.z), new Vector3(w.min.x + .02f, 6f, w.min.z + .3f), true);
            return "전실 양옆 틈 메움(벽 조각 4개)";
        }

        // 회로 고장 연출(2026-11-14 사용자): 작업실·소품실 회로를 부수면 조명 전체가 3초 깜빡이고, 전실 문이 열렸다 닫혔다 오작동하다 열린 채 굳고, 알림 조명은 노란색이 된다.
        static string CircuitBreakEffects(Transform layout, Transform root)
        {
            var craft = root.Find("CraftPowerDoor"); if (craft == null) return "회로 고장 연출: 전실 문 없음";
            if (craft.GetComponent<PowerDoorLamp>() is PowerDoorLamp lamp) { var lso = new SerializedObject(lamp); lso.FindProperty("brokenBy").stringValue = CraftCircuitId; lso.ApplyModifiedPropertiesWithoutUndo(); }
            int leaves = 0;
            foreach (var slide in craft.GetComponentsInChildren<ExplorationSlide>(true))
            { var sso = new SerializedObject(slide); sso.FindProperty("glitchBy").stringValue = CraftCircuitId; sso.FindProperty("glitchSeconds").floatValue = 3f; sso.ApplyModifiedPropertiesWithoutUndo(); leaves++; }
            if (layout.GetComponentInChildren<NightPower>(true) is NightPower power)
            {
                var nso = new SerializedObject(power); var flicker = nso.FindProperty("flickerOnOpen"); flicker.arraySize = 1; flicker.GetArrayElementAtIndex(0).stringValue = CraftCircuitId;
                nso.FindProperty("flickerSeconds").floatValue = 3f; nso.ApplyModifiedProperties(); PrefabUtility.RecordPrefabInstancePropertyModifications(power);
            }
            return $"회로 고장 연출: 문짝 {leaves}개 오작동, 조명 깜빡임 3초, 알림 조명 노란색";
        }

        static void AddLamp(Transform owner, string openedBy, List<Renderer> lamps, Vector3 glowAt)
        {
            var glowGo = new GameObject("LampGlow"); glowGo.transform.SetParent(owner, false); glowGo.transform.position = glowAt;
            var glow = glowGo.AddComponent<Light>(); glow.type = LightType.Point; glow.range = 3.5f; glow.intensity = 1.2f; glow.color = new Color(1f, .12f, .08f); glow.shadows = LightShadows.None;
            var lamp = owner.gameObject.AddComponent<PowerDoorLamp>(); var so = new SerializedObject(lamp);
            so.FindProperty("openedBy").stringValue = openedBy; so.FindProperty("glow").objectReferenceValue = glow;
            Refs(so.FindProperty("lamps"), lamps.Cast<Object>().ToArray()); so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 기믹 안내(왼쪽 안내 패널): 문 앞에 처음 다가가면 한 번 뜬다. 문구는 문구 파일 guide.power-door.*.
        static void AddGuide(Transform owner, Vector3 at)
        {
            var go = new GameObject("Guide"); go.transform.SetParent(owner, false); go.transform.position = at;
            var guide = go.AddComponent<GimmickGuide>(); var so = new SerializedObject(guide);
            so.FindProperty("guideId").stringValue = "power-door"; so.FindProperty("title").stringValue = "잠긴 문";
            so.FindProperty("body").stringValue = "잠긴 문을 열려면 전력이 필요합니다. 전력을 공급할 도구를 찾아보세요.";
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 표식 한 장(Quad). outward: 표식이 바라보는 방향.
        static void SignQuad(Transform parent, Material material, Vector3 at, Vector3 outward, float size)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.DestroyImmediate(quad.GetComponent<Collider>()); quad.name = "PowerSign";
            quad.transform.SetParent(parent, false); quad.transform.SetPositionAndRotation(at, Quaternion.LookRotation(-outward, Vector3.up)); quad.transform.localScale = new Vector3(size, size, 1f);
            var r = quad.GetComponent<Renderer>(); r.sharedMaterial = material; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static Material SimpleMaterial(string name, Color color, float metallic, float smoothness, bool emissive)
        {
            if (!AssetDatabase.IsValidFolder(SignFolder)) AssetDatabase.CreateFolder("Assets/Resources/Textures/WaitingRoom", "Signs");
            string path = $"{SignFolder}/{name}.mat"; var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name }; AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_BaseColor", color); material.SetFloat("_Metallic", metallic); material.SetFloat("_Smoothness", smoothness);
            if (emissive) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", color * 2.5f); material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; }
            EditorUtility.SetDirty(material);
            return material;
        }

        // 회로 기판 그림: 짙은 초록 바탕에 구리색 배선과 납땜 자리.
        static Material CircuitMaterial()
        {
            if (!AssetDatabase.IsValidFolder(SignFolder)) AssetDatabase.CreateFolder("Assets/Resources/Textures/WaitingRoom", "Signs");
            string texturePath = SignFolder + "/CircuitBoard.png", materialPath = SignFolder + "/CircuitBoard.mat";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath) == null)
            {
                const int n = 256; var tex = new Texture2D(n, n, TextureFormat.RGBA32, false); var rng = new System.Random(1114);
                var px = new Color[n * n]; var board = new Color(.05f, .26f, .13f); var trace = new Color(.72f, .5f, .2f); var pad = new Color(.8f, .8f, .78f);
                for (int i = 0; i < px.Length; i++) px[i] = board * (.9f + (float)rng.NextDouble() * .12f);
                void Rect(int x0, int y0, int x1, int y1, Color c) { for (int y = Mathf.Max(0, y0); y < Mathf.Min(n, y1); y++) for (int x = Mathf.Max(0, x0); x < Mathf.Min(n, x1); x++) px[y * n + x] = c; }
                for (int t = 0; t < 26; t++)
                {
                    // 꺾인 배선: 가로로 가다가 세로로 꺾인다. 끝에는 납땜 자리.
                    int x = rng.Next(10, n - 10), y = rng.Next(10, n - 10), lx = rng.Next(20, 110) * (rng.Next(2) == 0 ? 1 : -1), ly = rng.Next(20, 110) * (rng.Next(2) == 0 ? 1 : -1);
                    Rect(Mathf.Min(x, x + lx), y - 1, Mathf.Max(x, x + lx), y + 2, trace);
                    Rect(x + lx - 1, Mathf.Min(y, y + ly), x + lx + 2, Mathf.Max(y, y + ly), trace);
                    Rect(x - 4, y - 4, x + 5, y + 5, pad); Rect(x + lx - 4, y + ly - 4, x + lx + 5, y + ly + 5, pad);
                }
                for (int i = 0; i < px.Length; i++) px[i].a = 1f;
                tex.SetPixels(px); tex.Apply(); System.IO.File.WriteAllBytes(texturePath, tex.EncodeToPNG()); Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(texturePath);
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "CircuitBoard" }; AssetDatabase.CreateAsset(material, materialPath); }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath)); material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", .35f); EditorUtility.SetDirty(material);
            return material;
        }

        // 전기 표식: 노란 마름모 안의 검은 번개(전기가 필요한 문이라는 표시).
        static Material PowerSignMaterial()
        {
            if (!AssetDatabase.IsValidFolder(SignFolder)) AssetDatabase.CreateFolder("Assets/Resources/Textures/WaitingRoom", "Signs");
            string texturePath = SignFolder + "/PowerSign.png", materialPath = SignFolder + "/PowerSign.mat";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath) == null)
            {
                const int n = 256; var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                // 번개 모양(위에서 아래로 꺾인 다각형), 좌표는 -1~1.
                var bolt = new[] { new Vector2(.12f, .62f), new Vector2(-.34f, -.06f), new Vector2(-.04f, -.06f), new Vector2(-.16f, -.64f), new Vector2(.34f, .08f), new Vector2(.04f, .08f) };
                for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                {
                    var p = new Vector2((x + .5f) / n * 2f - 1f, (y + .5f) / n * 2f - 1f); float diamond = Mathf.Abs(p.x) + Mathf.Abs(p.y);
                    bool inside = false;
                    for (int i = 0, j = bolt.Length - 1; i < bolt.Length; j = i++)
                        if ((bolt[i].y > p.y) != (bolt[j].y > p.y) && p.x < (bolt[j].x - bolt[i].x) * (p.y - bolt[i].y) / (bolt[j].y - bolt[i].y) + bolt[i].x) inside = !inside;
                    Color c = diamond > .98f ? new Color(0, 0, 0, 0) : diamond > .86f || inside ? new Color(.06f, .06f, .06f, 1f) : new Color(.98f, .8f, .1f, 1f);
                    tex.SetPixel(x, y, c);
                }
                tex.Apply(); System.IO.File.WriteAllBytes(texturePath, tex.EncodeToPNG()); Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(texturePath);
                if (AssetImporter.GetAtPath(texturePath) is TextureImporter importer) { importer.alphaIsTransparency = true; importer.wrapMode = TextureWrapMode.Clamp; importer.SaveAndReimport(); }
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "PowerSign" }; AssetDatabase.CreateAsset(material, materialPath); }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath)); material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_AlphaClip", 1f); material.SetFloat("_Cutoff", .5f); material.EnableKeyword("_ALPHATEST_ON");
            material.SetFloat("_Smoothness", .2f); material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---- 쓰레기 더미: 상점 기둥과 의료용 승강기 사이 빈 바닥. 쓸 만한 물건 3개(마술사의 머리핀·구조 장갑·배터리)가 섞여 있다 ----
        const string ApocalypseProps = "Assets/_ThirdParty/Synty/PolygonApocalypse/Prefabs/Props/";
        static string TrashPile(Transform layout, Transform root)
        {
            // 후보 자리 중 2.4m 사방이 비어 있는 첫 자리.
            Vector3? chosen = null;
            foreach (var spot in new[] { new Vector3(13.2f, 0, 94.7f), new Vector3(12.4f, 0, 94.5f), new Vector3(13.4f, 0, 91.6f), new Vector3(9f, 0, 87.5f) })
                if (!Physics.CheckBox(spot + Vector3.up * .8f, new Vector3(1.1f, .6f, 1.1f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)
                    && Physics.Raycast(spot + Vector3.up, Vector3.down, out var ground, 2f, ~0, QueryTriggerInteraction.Ignore))
                { chosen = ground.point; break; }
            if (chosen == null) return "쓰레기 더미: 빈 자리를 찾지 못했습니다";
            var at = chosen.Value; var pile = Child(root, "TrashPile"); pile.position = at;
            int pieces = 0;
            foreach (var (prefab, offset, yaw, size) in new[] {
                ("SM_Prop_TrashPile_02", Vector3.zero, 20f, 1.9f), ("SM_Prop_TrashPile_06", new Vector3(.55f, 0, -.35f), 140f, 1.3f),
                ("SM_Prop_TrashBag_04", new Vector3(-.7f, 0, .45f), 60f, .7f), ("SM_Prop_TrashBag_06", new Vector3(.25f, 0, .75f), 200f, .65f) })
            {
                var piece = Spawn(pile, ApocalypseProps + prefab + ".prefab", size); if (piece == null) continue;
                piece.SetPositionAndRotation(at + offset, Quaternion.Euler(0, yaw, 0));
                // 바닥에 닿게 내린다(에셋 원점이 가운데인 것도 있다).
                var pb = Bounds(piece); piece.position += Vector3.up * (at.y - pb.min.y);
                foreach (var collider in piece.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider); // 물건을 집을 수 있게 더미는 막지 않는다
                pieces++;
            }
            // 더미 가장자리에 반쯤 묻힌 물건들. 자리마다 한 번 놓이고 F로 줍는다.
            int made = 0;
            foreach (var (spotId, itemId, itemName, offset) in new[] {
                ("learning-trash-hairpin", ItemUses.MagicianHairpin, "마술사의 머리핀", new Vector3(-.95f, 0, -.35f)),
                ("learning-trash-gloves", ItemUses.RescueGloves, "구조 장갑", new Vector3(.2f, 0, -1.0f)),
                ("learning-trash-battery", ItemUses.PowerBattery, "배터리", new Vector3(1.0f, 0, .35f)) })
            {
                var go = new GameObject("Found_" + itemName); go.transform.SetParent(pile, false); go.transform.position = at + offset; go.SetActive(false);
                var spot = go.AddComponent<FoundItemSpot>(); var so = new SerializedObject(spot);
                so.FindProperty("spotId").stringValue = spotId; so.FindProperty("uidOffset").intValue = FreeUidOffset(layout.gameObject.scene);
                so.FindProperty("itemId").stringValue = itemId; so.FindProperty("itemName").stringValue = itemName; so.ApplyModifiedPropertiesWithoutUndo();
                go.SetActive(true); made++;
            }
            return $"쓰레기 더미 {at:F1}(더미 {pieces}조각, 물건 {made}개)";
        }

        // ---- 숨겨진 방 2: 무대 입구 통로 서쪽, 무대에서 두 번째 커튼 방(Room_Closed_N_3) ----
        // 가까이 가면 커튼이 걷히고 녹슨 문이 드러난다. 문은 맨몸 6회 / 천막 말뚝 뽑개 1회. 안은 분장실이고 공연 기물 4개가 있다.
        const string HiddenRoom2DoorId = "learning:hidden-room-2-door";
        static string HiddenRoomTwo(Transform layout, Transform root)
        {
            var room = layout.Find("S06_StageCorridor/CurtainBayRooms/Room_Closed_N_3");
            var curtain = layout.Find("S06_StageCorridor/StageProcessionalRoute/GreyboxComposition/Curtain_CurtainRoom_Closed_N_3");
            if (room == null || curtain == null) return "숨겨진 방 2: 커튼 방을 찾지 못했습니다";
            var floor = room.Find("Floor").GetComponent<Renderer>().bounds; var back = room.Find("BackWall").GetComponent<Renderer>().bounds;
            float x0 = back.max.x, x1 = floor.max.x, z0 = floor.min.z, z1 = floor.max.z, y0 = floor.max.y, top = room.Find("Ceiling").GetComponent<Renderer>().bounds.min.y;
            var hidden = Child(root, "HiddenRoom2");

            // 커튼: 다가가면 남쪽 끝으로 걷힌다.
            var pc = curtain.GetComponent<ProximityCurtain>() ?? Undo.AddComponent<ProximityCurtain>(curtain.gameObject);
            var cb = Bounds(curtain); int axis = 0; float bestDot = 0f;
            for (int i = 0; i < 3; i++) { var d = Vector3.zero; d[i] = 1; float dot = Mathf.Abs(curtain.TransformDirection(d).z); if (dot > bestDot) { bestDot = dot; axis = i; } }
            var pso = new SerializedObject(pc); pso.FindProperty("axis").intValue = axis; pso.FindProperty("edge").vector3Value = new Vector3(cb.center.x, cb.center.y, cb.min.z);
            // 같은 자리에 천으로 된 커튼(CurtainController)이 한 겹 더 있다. 그것도 함께 걷는다.
            pso.FindProperty("velvet").objectReferenceValue = layout.Find("S06_StageCorridor/StageProcessionalRoute/CorridorVelvetCurtains/Velvet_Closed_N_3")?.gameObject;
            pso.ApplyModifiedProperties(); PrefabUtility.RecordPrefabInstancePropertyModifications(pc);
            foreach (var t in curtain.GetComponentsInChildren<Transform>(true)) if (GameObjectUtility.GetStaticEditorFlags(t.gameObject) != 0) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
            // 들어가지 못하게 막던 충돌체는 끄고, 검은 벽은 분장실 벽 재질로 바꾼다.
            if (room.Find("NoEntryBarrier") is Transform barrier && barrier.gameObject.activeSelf) { Undo.RecordObject(barrier.gameObject, "Open bay"); barrier.gameObject.SetActive(false); }
            var wallMat = Mat("LA_WallConcrete"); var floorMat = Mat("LA_FloorConcrete");
            foreach (var r in room.GetComponentsInChildren<Renderer>(true))
            {
                var m = r.name == "Floor" ? floorMat : wallMat; if (m == null) continue;
                Undo.RecordObject(r, "Dressing room wall"); r.sharedMaterial = m; PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            }

            // 녹슨 문: 방 입구 전체를 막는 녹슨 철판 벽 + 가운데 문짝. 문짝을 부수면 문짝만 사라진다.
            var rust = AssetDatabase.LoadAssetAtPath<Material>("Assets/_ThirdParty/Materials/WaitingArea/V4_RustyMetal.mat") ?? wallMat;
            var iron = Mat("LA_SteelPlate") ?? rust;
            float dx = x1 - .08f, mid = (z0 + z1) * .5f, dw = .9f, dh = 2.5f;
            var frame = Child(hidden, "RustFrame");
            Box(frame, "RustPanelS", rust, new Vector3(dx - .05f, y0, z0), new Vector3(dx + .05f, top, mid - dw), true);
            Box(frame, "RustPanelN", rust, new Vector3(dx - .05f, y0, mid + dw), new Vector3(dx + .05f, top, z1), true);
            Box(frame, "RustPanelTop", rust, new Vector3(dx - .05f, y0 + dh, mid - dw), new Vector3(dx + .05f, top, mid + dw), true);
            foreach (float z in new[] { mid - dw - .06f, mid + dw })
                Box(frame, "DoorPost", iron, new Vector3(dx - .08f, y0, z), new Vector3(dx + .08f, y0 + dh + .08f, z + .06f), false);
            Box(frame, "DoorLintel", iron, new Vector3(dx - .08f, y0 + dh, mid - dw - .06f), new Vector3(dx + .08f, y0 + dh + .08f, mid + dw + .06f), false);
            var door = Child(hidden, "RustDoor"); door.position = new Vector3(dx, y0, mid);
            var dv = Child(door, "Visual");
            Plain(Box(dv, "Leaf", rust, new Vector3(dx - .04f, y0 + .01f, mid - dw + .01f), new Vector3(dx + .04f, y0 + dh - .01f, mid + dw - .01f), false));
            foreach (float y in new[] { .5f, 1.3f, 2.1f })
                Plain(Box(dv, "LeafBand", iron, new Vector3(dx + .04f, y0 + y, mid - dw + .06f), new Vector3(dx + .06f, y0 + y + .1f, mid + dw - .06f), false));
            Plain(Box(dv, "Handle", iron, new Vector3(dx + .04f, y0 + 1.05f, mid + dw - .32f), new Vector3(dx + .1f, y0 + 1.2f, mid + dw - .24f), false));
            var slab = door.gameObject.AddComponent<BoxCollider>(); slab.center = new Vector3(0, dh * .5f, 0); slab.size = new Vector3(.16f, dh, dw * 2f);
            door.gameObject.AddComponent<Pursuit.PursuitDoorObstacle>().Source = slab;
            var point = Child(door, "InteractionPoint"); point.position = new Vector3(dx + .15f, y0 + 1.2f, mid);
            var gate = door.gameObject.AddComponent<ExplorationGate>(); var so = new SerializedObject(gate);
            so.FindProperty("targetId").stringValue = HiddenRoom2DoorId; so.FindProperty("label").stringValue = "녹슨 문";
            so.FindProperty("kind").enumValueIndex = (int)ExplorationGateKind.Breakable; so.FindProperty("toolId").stringValue = ItemUses.StakePuller;
            so.FindProperty("requiredHits").intValue = 1; so.FindProperty("bareHandHits").intValue = 6;
            so.FindProperty("interactionPoint").objectReferenceValue = point; so.FindProperty("blocker").objectReferenceValue = slab;
            Refs(so.FindProperty("hideOnOpen"), dv.gameObject); so.ApplyModifiedPropertiesWithoutUndo();

            // 분장실: 뒷벽에 붙은 화장대(거울·전구), 의자, 옷걸이 봉.
            var dress = Child(hidden, "DressingRoom"); var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/_ThirdParty/Materials/WaitingArea/V4_Wood_Old.mat") ?? iron; var bulb = SimpleMaterial("DressingBulb", new Color(1f, .82f, .55f), 0f, .5f, true);
            float tz = mid - .2f, tw = 1.5f;
            Box(dress, "VanityTop", wood, new Vector3(x0, y0 + .74f, tz - tw * .5f), new Vector3(x0 + .55f, y0 + .8f, tz + tw * .5f), true);
            foreach (float z in new[] { tz - tw * .5f + .04f, tz + tw * .5f - .1f })
                Box(dress, "VanityLeg", wood, new Vector3(x0 + .04f, y0, z), new Vector3(x0 + .5f, y0 + .74f, z + .06f), false);
            Box(dress, "MirrorFrame", wood, new Vector3(x0, y0 + .9f, tz - .6f), new Vector3(x0 + .05f, y0 + 2.0f, tz + .6f), false);
            Box(dress, "Mirror", Mat("LA_DeadScreenGlass") ?? iron, new Vector3(x0 + .05f, y0 + .96f, tz - .54f), new Vector3(x0 + .06f, y0 + 1.94f, tz + .54f), false);
            foreach (float z in new[] { -.5f, -.25f, 0f, .25f, .5f })
                Plain(Box(dress, "MirrorBulb", bulb, new Vector3(x0 + .05f, y0 + 2.0f, tz + z - .04f), new Vector3(x0 + .12f, y0 + 2.08f, tz + z + .04f), false));
            var glowGo = new GameObject("MirrorGlow"); glowGo.transform.SetParent(dress, false); glowGo.transform.position = new Vector3(x0 + .6f, y0 + 1.9f, tz);
            var glow = glowGo.AddComponent<Light>(); glow.type = LightType.Point; glow.range = 5f; glow.intensity = 3f; glow.color = new Color(1f, .82f, .55f); glow.shadows = LightShadows.None;
            if (Spawn(dress, ApocalypseProps + "SM_Prop_Study_Chair_01.prefab") is Transform chair)
            { chair.SetPositionAndRotation(new Vector3(x0 + 1.05f, y0, tz + .15f), Quaternion.Euler(0, 250f, 0)); var cbb = Bounds(chair); chair.position += Vector3.up * (y0 - cbb.min.y); }
            // 옷걸이 봉: 남쪽 벽에서 북쪽 벽까지 가로지르는 대신, 북쪽 구석에 양쪽 기둥으로 세운다.
            float rz0 = z1 - 1.5f, rz1 = z1 - .15f, rx = x0 + .35f;
            foreach (float z in new[] { rz0, rz1 - .05f }) Box(dress, "RackPost", iron, new Vector3(rx - .025f, y0, z), new Vector3(rx + .025f, y0 + 1.7f, z + .05f), false);
            Box(dress, "RackBar", iron, new Vector3(rx - .02f, y0 + 1.66f, rz0), new Vector3(rx + .02f, y0 + 1.7f, rz1), false);

            // 공연 기물 4개: 문이 열린 뒤에 놓인다.
            int made = 0;
            foreach (var (spotId, itemId, itemName, at) in new[] {
                ("learning-room2-rescue", ItemUses.RescueGloves, "구조 장갑", new Vector3(x0 + .3f, y0 + .82f, tz - .45f)),
                ("learning-room2-sticky-1", ItemUses.StickyGloves, "끈끈이 장갑", new Vector3(x0 + .3f, y0 + .82f, tz + .45f)),
                ("learning-room2-sticky-2", ItemUses.StickyGloves, "끈끈이 장갑", new Vector3(x0 + 1.3f, y0, z0 + .6f)),
                ("learning-room2-balloon", ItemUses.BalloonPants, "광대 벌룬 바지", new Vector3(rx + .5f, y0, (rz0 + rz1) * .5f)) })
            {
                var go = new GameObject("Found_" + spotId); go.transform.SetParent(hidden, false); go.transform.position = at; go.SetActive(false);
                var spot = go.AddComponent<FoundItemSpot>(); var fso = new SerializedObject(spot);
                fso.FindProperty("spotId").stringValue = spotId; fso.FindProperty("uidOffset").intValue = FreeUidOffset(layout.gameObject.scene);
                fso.FindProperty("itemId").stringValue = itemId; fso.FindProperty("itemName").stringValue = itemName; fso.FindProperty("availableAfter").stringValue = HiddenRoom2DoorId;
                fso.ApplyModifiedPropertiesWithoutUndo(); go.SetActive(true); made++;
            }
            return $"숨겨진 방 2: 커튼 방 x {x0:F1}~{x1:F1} z {z0:F1}~{z1:F1}, 녹슨 문 폭 {dw * 2f:F1}m, 기물 {made}개";
        }

        // ---- 숨겨진 방 1: 상점 기둥 뒤(동쪽) 벽을 뚫고 녹슨 벽으로 막은 작은 공간. 부수면 10원짜리 동전 5개 ----
        // 기존 벽(통짜 한 장)은 끄고 같은 재질의 판 세 장으로 다시 세워 구멍을 낸다(2026-11-13 사용자: 뚫어서 만들기).
        const string HiddenRoom1WallId = "learning:hidden-room-1-wall";
        static string HiddenRoomOne(Transform layout, Transform root)
        {
            var pillar = layout.Find("S03_Shop/Pillar"); if (pillar == null) return "숨겨진 방 1: 상점 기둥을 찾지 못했습니다";
            float holeZ = pillar.position.z, half = .85f, holeTop = 2.3f, depth = 1.9f;
            // 기둥 동쪽의 벽(꺼 둔 뒤에도 찾을 수 있게 메시 크기로 찾는다).
            var wall = layout.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name == "GalleryWall")
                .Select(r => (r, b: MeshBounds(r))).Where(x => x.b.size.x < .6f && x.b.min.z < holeZ - 1f && x.b.max.z > holeZ + 1f && x.b.center.x > pillar.position.x && x.b.center.x < pillar.position.x + 6f)
                .OrderBy(x => x.b.center.x).Select(x => x.r).FirstOrDefault();
            if (wall == null) return "숨겨진 방 1: 기둥 뒤 벽을 찾지 못했습니다";
            var wb = MeshBounds(wall); var wallMat = wall.sharedMaterial; float floorY = wb.min.y;
            if (Physics.Raycast(new Vector3(wb.min.x - .5f, wb.min.y + 1f, holeZ), Vector3.down, out var ground, 3f, ~0, QueryTriggerInteraction.Ignore)) floorY = ground.point.y;
            Undo.RecordObject(wall, "Cut gallery wall"); wall.enabled = false; PrefabUtility.RecordPrefabInstancePropertyModifications(wall);
            foreach (var c in wall.GetComponents<Collider>()) { Undo.RecordObject(c, "Cut gallery wall"); c.enabled = false; PrefabUtility.RecordPrefabInstancePropertyModifications(c); }
            var hidden = Child(root, "HiddenRoom1"); var rebuilt = Child(hidden, "GalleryWallRebuilt");
            Box(rebuilt, "WallSouth", wallMat, new Vector3(wb.min.x, wb.min.y, wb.min.z), new Vector3(wb.max.x, wb.max.y, holeZ - half), true);
            Box(rebuilt, "WallNorth", wallMat, new Vector3(wb.min.x, wb.min.y, holeZ + half), new Vector3(wb.max.x, wb.max.y, wb.max.z), true);
            Box(rebuilt, "WallAbove", wallMat, new Vector3(wb.min.x, floorY + holeTop, holeZ - half), new Vector3(wb.max.x, wb.max.y, holeZ + half), true);

            // 작은 공간: 벽 뒤 1.9m 깊이.
            var inner = Mat("LA_WallCementRust") ?? Mat("LA_WallConcrete") ?? wallMat; var floorMat = Mat("LA_FloorConcrete") ?? wallMat;
            float x0 = wb.max.x, x1 = x0 + depth, z0 = holeZ - half, z1 = holeZ + half, t = .15f;
            var nook = Child(hidden, "Nook");
            Box(nook, "Floor", floorMat, new Vector3(x0 - (wb.max.x - wb.min.x), floorY - .2f, z0), new Vector3(x1, floorY, z1), true);
            Box(nook, "Ceiling", inner, new Vector3(x0, floorY + holeTop, z0 - t), new Vector3(x1 + t, floorY + holeTop + t, z1 + t), false);
            Box(nook, "BackWall", inner, new Vector3(x1, floorY, z0 - t), new Vector3(x1 + t, floorY + holeTop, z1 + t), true);
            Box(nook, "SideWallS", inner, new Vector3(x0, floorY, z0 - t), new Vector3(x1, floorY + holeTop, z0), true);
            Box(nook, "SideWallN", inner, new Vector3(x0, floorY, z1), new Vector3(x1, floorY + holeTop, z1 + t), true);
            var glowGo = new GameObject("NookGlow"); glowGo.transform.SetParent(nook, false); glowGo.transform.position = new Vector3(x0 + depth * .5f, floorY + 1.8f, holeZ);
            var glow = glowGo.AddComponent<Light>(); glow.type = LightType.Point; glow.range = 3f; glow.intensity = 1.5f; glow.color = new Color(1f, .8f, .5f); glow.shadows = LightShadows.None;

            // 녹슨 벽: 구멍을 로비 쪽 벽면에 맞춰 막는다. 맨몸 6회 / 천막 말뚝 뽑개 1회(숨겨진 방 2의 문과 같은 초안).
            var rust = AssetDatabase.LoadAssetAtPath<Material>("Assets/_ThirdParty/Materials/WaitingArea/V4_RustyMetal.mat") ?? inner; var iron = Mat("LA_SteelPlate") ?? rust;
            var gateGo = Child(hidden, "RustWall"); gateGo.position = new Vector3(wb.center.x, floorY, holeZ);
            var visual = Child(gateGo, "Visual");
            Plain(Box(visual, "RustPlate", rust, new Vector3(wb.min.x - .02f, floorY + .01f, z0 + .005f), new Vector3(wb.max.x, floorY + holeTop - .005f, z1 - .005f), false));
            foreach (float y in new[] { .55f, 1.5f })
                Plain(Box(visual, "RustBand", iron, new Vector3(wb.min.x - .04f, floorY + y, z0 + .05f), new Vector3(wb.min.x - .02f, floorY + y + .1f, z1 - .05f), false));
            var slab = gateGo.gameObject.AddComponent<BoxCollider>(); slab.center = new Vector3(0, holeTop * .5f, 0); slab.size = new Vector3(wb.size.x + .04f, holeTop, half * 2f);
            var point = Child(gateGo, "InteractionPoint"); point.position = new Vector3(wb.min.x - .15f, floorY + 1.2f, holeZ);
            var gate = gateGo.gameObject.AddComponent<ExplorationGate>(); var so = new SerializedObject(gate);
            so.FindProperty("targetId").stringValue = HiddenRoom1WallId; so.FindProperty("label").stringValue = "녹슨 벽";
            so.FindProperty("kind").enumValueIndex = (int)ExplorationGateKind.Breakable; so.FindProperty("toolId").stringValue = ItemUses.StakePuller;
            so.FindProperty("requiredHits").intValue = 1; so.FindProperty("bareHandHits").intValue = 6;
            so.FindProperty("interactionPoint").objectReferenceValue = point; so.FindProperty("blocker").objectReferenceValue = slab;
            Refs(so.FindProperty("hideOnOpen"), visual.gameObject); so.ApplyModifiedPropertiesWithoutUndo();

            // 10원짜리 동전 5개: 벽이 부서진 뒤 바닥에 놓인다.
            int made = 0;
            foreach (var offset in new[] { new Vector2(.45f, -.4f), new Vector2(.8f, .3f), new Vector2(1.15f, -.15f), new Vector2(1.45f, .45f), new Vector2(1.5f, -.5f) })
            {
                var go = new GameObject("Found_Coin_" + (made + 1)); go.transform.SetParent(hidden, false); go.transform.position = new Vector3(x0 + offset.x, floorY, holeZ + offset.y); go.SetActive(false);
                var spot = go.AddComponent<FoundItemSpot>(); var fso = new SerializedObject(spot);
                fso.FindProperty("spotId").stringValue = "learning-room1-coin-" + (made + 1); fso.FindProperty("uidOffset").intValue = FreeUidOffset(layout.gameObject.scene);
                fso.FindProperty("itemId").stringValue = ExplorationDrops.CoinTokenId; fso.FindProperty("itemName").stringValue = "공연 토큰 (자금 10원)"; fso.FindProperty("availableAfter").stringValue = HiddenRoom1WallId;
                fso.ApplyModifiedPropertiesWithoutUndo(); go.SetActive(true); made++;
            }
            return $"숨겨진 방 1: 벽 x {wb.min.x:F1}~{wb.max.x:F1} 구멍 z {z0:F1}~{z1:F1} 높이 {holeTop}m, 공간 깊이 {depth}m, 동전 {made}개";
        }

        // ---- 낡은 상자 2개: 무대 입구 통로 들머리 양옆 벽 앞. 화물 적재 장소의 낡은 상자와 같은 에셋·규칙 ----
        static string OldBoxes(Transform layout, Transform root)
        {
            var source = layout.Find("S12_CargoBay/Exploration_A/cargoBox1");
            if (source == null) return "낡은 상자: 원본을 찾지 못했습니다";
            var boxes = Child(root, "OldBoxes"); var placed = new List<string>();
            int n = 0;
            foreach (var (z, side) in new[] { (100.5f, -1f), (106.5f, 1f) })
            {
                n++;
                // 통로 가운데(x -9.7)에서 옆벽까지 재서 벽 앞 0.7m에 둔다.
                if (!Physics.Raycast(new Vector3(-9.7f, .6f, z), Vector3.right * side, out var wall, 12f, ~0, QueryTriggerInteraction.Ignore)) continue;
                var at = new Vector3(wall.point.x - side * .7f, 0, z);
                if (Physics.Raycast(at + Vector3.up, Vector3.down, out var ground, 2f, ~0, QueryTriggerInteraction.Ignore)) at = ground.point;
                var copy = Object.Instantiate(source.gameObject, boxes); copy.name = "OldBox_" + n; copy.SetActive(true);
                copy.transform.SetPositionAndRotation(at, Quaternion.Euler(0, side > 0 ? 15f : -20f, 0));
                var gate = copy.GetComponent<ExplorationGate>(); var so = new SerializedObject(gate);
                so.FindProperty("targetId").stringValue = "learning:old-box-" + n; so.ApplyModifiedPropertiesWithoutUndo();
                if (copy.GetComponent<ExplorationDrops>() is ExplorationDrops drops)
                {
                    int first = FreeUidOffset(layout.gameObject.scene); drops.ConfigureOldBoxLoot(first, first);
                    int second = FreeUidOffset(layout.gameObject.scene); drops.ConfigureOldBoxLoot(first, second); EditorUtility.SetDirty(drops);
                }
                placed.Add($"({at.x:F1}, {at.z:F1})");
            }
            return "낡은 상자 " + string.Join(" ", placed);
        }

        // 그림이 그려진 종이 재질(가장자리는 투명하게 잘라 낸다).
        static Material PaperPicture(string textureName)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ClueTextures + textureName + ".png");
            if (texture == null) return null;
            if (AssetImporter.GetAtPath(ClueTextures + textureName + ".png") is TextureImporter importer && (!importer.alphaIsTransparency || importer.wrapMode != TextureWrapMode.Clamp))
            { importer.alphaIsTransparency = true; importer.wrapMode = TextureWrapMode.Clamp; importer.SaveAndReimport(); }
            string path = ClueTextures + textureName + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = textureName }; AssetDatabase.CreateAsset(material, path); }
            material.SetTexture("_BaseMap", texture); material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_AlphaClip", 1f); material.SetFloat("_Cutoff", .5f); material.EnableKeyword("_ALPHATEST_ON");
            material.SetFloat("_Smoothness", .05f); material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---- 찢겨진 노트 낱장 ----
        const bool SecondNoteOnLoungeTable = false;
        static string TornNotePictures(Transform layout)
        {
            int done = 0; string moved = "";
            for (int n = 1; n <= 4; n++)
            {
                var holder = layout.Find($"{RootName}/Objects/TornNote_{n}"); var material = PaperPicture("TornNote_" + n);
                var page = holder != null ? holder.Find("Page") : null;
                if (page == null || material == null) continue;
                Undo.RecordObject(page, "Note picture"); Undo.RecordObject(page.GetComponent<Renderer>(), "Note picture");
                page.GetComponent<Renderer>().sharedMaterial = material;
                // 그림 비율(1086 x 1448)에 맞춘 A5 남짓 크기. 찢긴 가장자리는 그림에 들어 있어 덧댄 조각은 끈다.
                page.localScale = new Vector3(.165f, .22f, 1f);
                if (holder.Find("TornCorner") is Transform corner && corner.gameObject.activeSelf) { Undo.RecordObject(corner.gameObject, "Hide corner"); corner.gameObject.SetActive(false); }
                done++;
            }
            // 2번: 화물 적재장 상자 안 → 대기실 로비 휴게 공간 탁자 위(처음부터 보인다).
            // 2026-11-14 사용자: 낱장은 모두 보상 없던 상자·캐비넷 안으로 다시 나눴다(11단계). 탁자로 옮기지 않는다.
            var second = SecondNoteOnLoungeTable ? layout.Find($"{RootName}/Objects/TornNote_2") : null;
            if (second != null)
            {
                foreach (var box in layout.GetComponentsInChildren<ToolTarget>(true))
                {
                    var so = new SerializedObject(box); var shown = so.FindProperty("showOnOpen"); bool changed = false;
                    for (int i = shown.arraySize - 1; i >= 0; i--)
                        if (shown.GetArrayElementAtIndex(i).objectReferenceValue == second.gameObject)
                        { shown.GetArrayElementAtIndex(i).objectReferenceValue = null; shown.DeleteArrayElementAtIndex(i); changed = true; }
                    if (changed) { so.ApplyModifiedProperties(); PrefabUtility.RecordPrefabInstancePropertyModifications(box); }
                }
                // 휴게 공간 탁자: 깔개 한가운데 탁자 윗면에서 빈 자리를 찾는다.
                var seating = layout.Find("LG_Lounge/Seating");
                Vector3 centre = seating != null ? Bounds(seating).center : new Vector3(3.3f, 0, 80.6f);
                Vector3? spot = null; float best = float.MaxValue;
                for (float dx = -1.2f; dx <= 1.2f; dx += .15f)
                    for (float dz = -1.2f; dz <= 1.2f; dz += .15f)
                    {
                        var from = new Vector3(centre.x + dx, 2f, centre.z + dz);
                        if (!Physics.Raycast(from, Vector3.down, out var hit, 2.2f, ~0, QueryTriggerInteraction.Ignore) || hit.normal.y < .98f || hit.point.y < .3f || hit.point.y > 1.0f) continue;
                        // 낱장 네 귀퉁이가 같은 윗면에 놓이고 위에 다른 물건이 없어야 한다.
                        bool flat = true;
                        foreach (var corner in new[] { new Vector3(.09f, 0, .12f), new Vector3(-.09f, 0, .12f), new Vector3(.09f, 0, -.12f), new Vector3(-.09f, 0, -.12f) })
                            flat &= Physics.Raycast(hit.point + corner + Vector3.up * .5f, Vector3.down, out var under, .6f, ~0, QueryTriggerInteraction.Ignore) && Mathf.Abs(under.point.y - hit.point.y) < .005f;
                        float score = dx * dx + dz * dz;
                        if (flat && score < best) { best = score; spot = hit.point; }
                    }
                if (spot is Vector3 at)
                {
                    Undo.RecordObject(second, "Move note 2"); Undo.RecordObject(second.gameObject, "Move note 2");
                    second.SetPositionAndRotation(at, Quaternion.Euler(0, 20f, 0)); second.gameObject.SetActive(true);
                    moved = $", 2번 → 로비 휴게 공간 탁자 {at:F1}";
                }
                else moved = ", 2번: 탁자 위 빈 자리를 찾지 못해 옮기지 못함";
            }
            // 살펴볼 때 조명: 밝은 종이는 글씨가 날아가서 낮춘다(2026-11-14 사용자: 낱장·지도가 너무 밝아 안 보임).
            foreach (var clue in layout.GetComponentsInChildren<ClueInspectable>(true))
                if (clue.TargetId.StartsWith("final:torn-note-") || clue.TargetId == "structure-a:map")
                { var cso = new SerializedObject(clue); cso.FindProperty("inspectLight").floatValue = PaperInspectLight; cso.ApplyModifiedProperties(); PrefabUtility.RecordPrefabInstancePropertyModifications(clue); }
            return $"낱장 그림 {done}장{moved}";
        }

        // ---- 떨어진 포스트잇 ----
        static string PostIt(Transform layout, Transform root)
        {
            var shelf = layout.Find("S04_PropRoom/ShelfZone/HandoverShelf_4Tier/Shelf"); var material = PaperPicture("PostItRun");
            if (shelf == null || material == null) return "포스트잇: 선반 또는 그림을 찾지 못했습니다";
            var sb = Bounds(shelf);
            // 선반 앞쪽 가장자리 바로 아래 바닥(선반 발치). 선반은 방 안쪽(+z)을 본다.
            var at = new Vector3(sb.min.x + sb.size.x * .3f, sb.min.y + 1f, sb.max.z + .12f);
            if (!Physics.Raycast(at, Vector3.down, out var floor, 2f, ~0, QueryTriggerInteraction.Ignore)) return "포스트잇: 바닥을 찾지 못했습니다";
            var holder = Child(root, "PostIt"); holder.SetPositionAndRotation(floor.point, Quaternion.Euler(0, -18f, 0));
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.DestroyImmediate(quad.GetComponent<Collider>()); quad.name = "Page";
            quad.transform.SetParent(holder, false);
            quad.transform.SetPositionAndRotation(floor.point + Vector3.up * .004f, Quaternion.Euler(90f, -18f, 0)); quad.transform.localScale = new Vector3(.1f, .1f, 1f);
            var qr = quad.GetComponent<Renderer>(); qr.sharedMaterial = material; qr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var clue = Clue(holder, PostItId, "떨어진 포스트잇", "", null, null, .3f, PaperInspectLight);
            var face = new GameObject("ViewFace").transform; face.SetParent(holder, false);
            if (PageFrame(quad.GetComponent<MeshFilter>(), out var outward, out var up)) face.SetPositionAndRotation(quad.transform.position, Quaternion.LookRotation(-outward, up));
            var so = new SerializedObject(clue); so.FindProperty("viewFace").objectReferenceValue = face; so.ApplyModifiedPropertiesWithoutUndo();
            return $"포스트잇 {floor.point:F1}";
        }
    }
}
