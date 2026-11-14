using System.Collections.Generic;
using CurtainCall.EscapeRoute.EditorTools;
using CurtainCall.HumanCannon;
using CurtainCall.Settings;
using CurtainCall.Tightrope;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 연습실 자유 연습 공간 배치(Task-125, 사용자 그림 2026-11-13 route-diagram). 다시 실행하면 지우고 새로 만든다.
    /// 그림의 오른쪽 = +x, 위쪽 = +z. 좌표 (u, v)는 링 가운데 기준(m).
    /// - 위: 외줄 4개(왼→오). 계단 → 출발대 → 외줄 → 도착 지역 → 인간 그네 1개로 아래 구간 오른쪽 발판.
    /// - 가운데: 인간 그네(오→왼). 발판 → 연속 그네 3개 → 발판 → 외줄 2개 → 나란한 그네 2개 → 외줄 2개 → 발판 → 계단.
    /// - 아래: 인간 대포(왼→오). 대포 2문·트램펄린 자리 → 그네 1개 → 중간 발판 → 도착 지역 → 계단.
    /// 기물은 공연 준비 프리팹을 그대로 쓴다(바닥·계단·외줄·봉·대포). 줄은 탈출로와 같은 "씬에 놓은 독립 외줄" 방식이다.
    /// 수치: 줄 높이 2.5m·줄 간격 3m·연속 봉 간격 8m·봉 높이(줄 위 0.6m에 발)는 리허설 값. 나머지 길이는 링에 맞췄다.
    /// 링의 옛 연습 소품(탑·사다리·그물·옛 줄·장식 대포·장식 트램펄린)은 지운다.
    /// 2026-11-13 사용자: 관객석을 없애고 링 바닥을 연습실 벽(반지름 26.5m)까지 넓혔다. 난간·옆 계단도 지우고, 입구 계단은 통로 안으로 옮겼다.
    /// 기물은 넓어진 바닥에 맞춰 길이와 자리를 늘렸다(조명탑 네 개는 그대로 두고 피해서 놓는다).
    /// </summary>
    public static class PracticeRoomFreePlayBuilder
    {
        const string RootName = "PracticeFreePlay";
        const string Kit = "Assets/Resources/Prefabs/Objects/ETCs/PerformanceArtKit/Prepared/";
        const string CannonKit = "Assets/Resources/Prefabs/Objects/ETCs/HumanCannonArtKit/Prepared/";
        /// <summary>발판·줄 윗면 높이(링 바닥 기준). 리허설 코스 높이와 같다.</summary>
        const float Height = 2.5f;
        /// <summary>봉 높이(발판 기준): 매달린 발이 줄 위 0.6m(리허설의 낮은 봉) + 매달린 길이.</summary>
        static float PoleGrip => .6f + GameSettings.Poles.HangLength;
        /// <summary>연습실 둥근 벽 안쪽 반지름(벽 외형 기준). 링 바닥을 여기까지 넓힌다.</summary>
        const float RoomRadius = 26.5f;
        /// <summary>넓히기 전 링 반지름(바닥·별무늬·나무 테의 원래 배율 기준).</summary>
        const float OriginalRingRadius = 20f;
        const float SupportReach = RoomRadius - 1f;

        static Vector3 center;
        static Transform supports;
        static Material supportMaterial;

        static Vector3 At(float u, float v, float height) => center + new Vector3(u, height, v);

        [MenuItem("CurtainCall/Waiting Room/Build practice room free play")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("편집 모드에서 실행하세요.");
            var room = WaitingRoomScenes.FindPracticeRoom(EditorSceneManager.GetActiveScene());
            if (room == null) throw new System.InvalidOperationException("대기실 씬(연습실)을 열고 실행하세요.");
            FillRoomWithRing(room);
            Physics.SyncTransforms();
            var deck = room.Find("LegacyRing/PracticeRing/DeckCollider").GetComponent<Collider>().bounds;
            center = new Vector3(deck.center.x, deck.max.y, deck.center.z);

            // 옛 연습 소품을 지우기 전에 기둥 재질만 빌린다(에셋일 때).
            var lanes = room.Find("LegacyRing/PracticeLanes");
            var post = lanes != null ? lanes.Find("TowerPost") : null;
            var borrowed = post != null ? post.GetComponent<Renderer>()?.sharedMaterial : null;
            if (borrowed != null && AssetDatabase.Contains(borrowed)) supportMaterial = borrowed;
            var old = room.Find(RootName);
            if (old != null)
            {
                var oldSupports = old.Find("RopesAndSwings/Supports");
                if (supportMaterial == null && oldSupports != null) supportMaterial = oldSupports.GetComponentInChildren<Renderer>(true)?.sharedMaterial;
                Object.DestroyImmediate(old.gameObject);
            }
            foreach (string path in new[] { "LegacyRing/PracticeLanes", "LegacyRing/CollisionRepairs" })
            {
                var prop = room.Find(path);
                if (prop != null) Object.DestroyImmediate(prop.gameObject);
            }
            // 장식 대포·트램펄린을 가리키던 "연습실로 옮겨진 기물" 목록을 비운다.
            foreach (var discovery in Object.FindObjectsByType<PerformancePropDiscovery>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var data = new SerializedObject(discovery);
                data.FindProperty("practiceProps").arraySize = 0;
                data.ApplyModifiedPropertiesWithoutUndo();
            }

            var root = new GameObject(RootName).transform;
            root.SetParent(room, false);
            root.position = center;
            var ropes = Group(root, "RopesAndSwings");
            var cannon = Group(root, "CannonLane");

            var ropePoles = new List<HumanCannonPole>();
            var supportColliders = BuildRopes(ropes, ropePoles);
            var cannonPoles = new List<HumanCannonPole>();
            var stations = BuildCannonLane(cannon, cannonPoles, out var trampolineSpot);

            // 독립 외줄 코스(탈출로와 같은 구성). 판정·도착·투표 없는 이동 전용이다.
            ropes.gameObject.SetActive(false);
            var course = ropes.gameObject.AddComponent<TightropeCourse>();
            var courseData = new SerializedObject(course);
            courseData.FindProperty("useSceneGeometry").boolValue = true;
            courseData.FindProperty("independentSceneRopes").boolValue = true;
            courseData.ApplyModifiedPropertiesWithoutUndo();
            var shapeField = typeof(TightropeCourse).GetField("sceneShape", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            JsonUtility.FromJsonOverwrite(GameSettings.Tightrope.Course.ToJson(), shapeField.GetValue(course));
            courseData.Update();
            var shape = courseData.FindProperty("sceneShape");
            shape.FindPropertyRelative("laneCount").intValue = supportColliders.Count;
            shape.FindPropertyRelative("swingGapEnabled").boolValue = false;
            shape.FindPropertyRelative("relayGapEnabled").boolValue = false;
            shape.FindPropertyRelative("trapezeEnabled").boolValue = false;
            var ends = shape.FindPropertyRelative("laneEndDistances");
            var sceneRopes = courseData.FindProperty("sceneRopes");
            ends.arraySize = sceneRopes.arraySize = supportColliders.Count;
            float longest = 0f;
            for (int i = 0; i < supportColliders.Count; i++)
            {
                sceneRopes.GetArrayElementAtIndex(i).objectReferenceValue = supportColliders[i];
                ends.GetArrayElementAtIndex(i).floatValue = supportColliders[i].size.z;
                longest = Mathf.Max(longest, supportColliders[i].size.z);
            }
            shape.FindPropertyRelative("finishDistance").floatValue = longest;
            courseData.ApplyModifiedPropertiesWithoutUndo();
            ropes.gameObject.AddComponent<CourseControlSwitcher>();
            ropes.gameObject.AddComponent<TightropeRun>();
            ropes.gameObject.AddComponent<TightropeHangSystem>();
            ropes.gameObject.AddComponent<TightropeRopeSway>();
            ropes.gameObject.AddComponent<RopeLaneJumpRules>();
            if (ropes.GetComponent<TightropeCatchSystem>() == null) ropes.gameObject.AddComponent<TightropeCatchSystem>();
            if (ropes.GetComponent<TightropeBalancePoseView>() == null) ropes.gameObject.AddComponent<TightropeBalancePoseView>();
            EscapeBackgroundBuilder.InstallOriginalBalanceDisplay(ropes);
            course.Rebuild();

            // 봉은 공통 봉 시스템 하나가 맡는다(늘 켜 둔다. 숨긴 구간의 봉은 상호작용을 끈다).
            var poles = root.gameObject.AddComponent<HumanCannonPoleSystem>();
            var poleData = new SerializedObject(poles);
            var list = poleData.FindProperty("poles");
            var all = new List<HumanCannonPole>(ropePoles); all.AddRange(cannonPoles);
            list.arraySize = all.Count;
            for (int i = 0; i < all.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = all[i];
            poleData.ApplyModifiedPropertiesWithoutUndo();

            cannon.gameObject.SetActive(false);
            var freePlay = root.gameObject.AddComponent<PracticeRoomFreePlay>();
            freePlay.Configure(ropes.gameObject, cannon.gameObject, stations, trampolineSpot, ropePoles.ToArray(), cannonPoles.ToArray());
            EditorUtility.SetDirty(freePlay);
            EditorSceneManager.MarkSceneDirty(room.gameObject.scene);
            Debug.Log($"[PracticeFreePlay] 배치 완료: 외줄 {supportColliders.Count}개, 봉 {all.Count}개, 대포 {stations.Length}문.");
        }

        /// <summary>
        /// 관객석을 없애고 링 바닥을 연습실 벽까지 넓힌다(2026-11-13 사용자). 다시 실행해도 같은 결과가 된다.
        /// 지우는 것: 관객석, 링 가장자리 난간·깃발·전구·옆 치마, 옆 계단 두 벌. 입구 계단은 벽 밖 통로 안으로 옮긴다(바닥 높이 0.6m 그대로).
        /// </summary>
        static void FillRoomWithRing(Transform room)
        {
            foreach (string path in new[] { "RoundArena/RingSeating", "RoundArena/Stand" })
            {
                var seats = room.Find(path);
                if (seats != null) Object.DestroyImmediate(seats.gameObject);
            }
            var ring = room.Find("LegacyRing/PracticeRing");
            float grow = RoomRadius / OriginalRingRadius;
            for (int i = ring.childCount - 1; i >= 0; i--)
            {
                var part = ring.GetChild(i);
                // 원래 배율: 바닥 충돌체 지름 40, 별무늬·나무 테 2.5.
                if (part.name == "DeckCollider") part.localScale = new Vector3(OriginalRingRadius * 2f * grow, part.localScale.y, OriginalRingRadius * 2f * grow);
                else if (part.name == "StarFloor" || part.name == "WoodenRim") part.localScale = new Vector3(2.5f * grow, 1f, 2.5f * grow);
                else Object.DestroyImmediate(part.gameObject);
            }
            var deck = ring.Find("DeckCollider");
            var middle = new Vector3(deck.position.x, 0f, deck.position.z);

            // 입구 계단: 통로 축(링 가운데 → 연습실 문)을 따라 벽 바로 밖에 둔다.
            var access = room.Find("LegacyRing/RaisedRingAccess");
            var gate = GameObject.Find(NightDoorLock.RehearsalPassageName);
            if (access != null && gate != null)
            {
                Vector3 axis = gate.transform.position - middle; axis.y = 0f; axis.Normalize();
                Vector3 shift = Vector3.zero; int step = 0;
                var steps = new List<Transform>();
                for (int i = access.childCount - 1; i >= 0; i--)
                {
                    var part = access.GetChild(i);
                    if (!part.name.EndsWith("_Entry")) Object.DestroyImmediate(part.gameObject);
                    else if (part.name == "RingAccessStep_Entry") steps.Add(part);
                }
                // 높은 단(바닥과 같은 0.6m)이 벽 쪽, 낮은 단이 통로 쪽. 한 단 깊이 0.45m.
                steps.Sort((a, b) => b.localScale.y.CompareTo(a.localScale.y));
                foreach (var part in steps)
                {
                    Vector3 at = middle + axis * (RoomRadius + .45f * (step++ + .5f)) + Vector3.up * part.position.y;
                    if (step == 1) shift = at - part.position;
                    part.position = at;
                }
                var ramp = access.Find("RingStairCollision_Entry");
                if (ramp != null) ramp.position += shift;
            }
            // 관객석 통로(바닥 높이 0)에 서 있던 추격 배치물은 넓어진 바닥 위로 올린다.
            var placements = room.Find("PursuitPlacements");
            float top = deck.GetComponent<Collider>().bounds.max.y;
            if (placements != null)
                foreach (Transform placed in placements)
                    if (placed.position.y < top - .01f) placed.position = new Vector3(placed.position.x, top, placed.position.z);
        }

        static List<BoxCollider> BuildRopes(Transform parent, List<HumanCannonPole> poles)
        {
            supports = Group(parent, "Supports");
            var list = new List<BoxCollider>();
            // 조명탑 네 개(가운데에서 대각선 약 ±16.7m, 하나는 (11.1, -20.8))를 피해서 놓는다.
            // ── 위: 외줄타기(왼→오) ──
            // 2026-11-14 사용자: 외줄은 2개만 둔다(4개가 자리를 너무 차지했다). 길이 19.6m, 계단은 구조도처럼 출발대 왼쪽.
            Floor(parent, "Tightrope_Start", -11.3f, -7.8f, 6.5f, 12.5f);
            Stair(parent, "Tightrope_StartStair", -11.3f, 9.5f, new Vector3(-1, 0, 0));
            for (int i = 0; i < 2; i++) list.Add(Rope(parent, "Tightrope_Rope" + (i + 1), -7.8f, 11.8f, 8f + i * 3f));
            Floor(parent, "Tightrope_Finish", 11.8f, 15.3f, 6.5f, 12.5f);
            Floor(parent, "Tightrope_FinishWalk", 15.3f, 23.5f, 6.5f, 9.5f);
            // 도착 지역 → 가운데 구간 오른쪽 발판(위→아래)
            poles.Add(Pole(parent, "Swing_ToSwingLane", 22.5f, 7.5f, Height + PoleGrip, Vector3.back));
            // ── 가운데: 인간 그네(오→왼) ──
            Floor(parent, "Swing_Start", 21f, 24.5f, -4.5f, 1.5f);
            for (int i = 0; i < 3; i++) poles.Add(Pole(parent, "Swing_Chain" + (i + 1), 22f - i * 8f, -1.5f, Height + PoleGrip, Vector3.left));
            Floor(parent, "Swing_Middle", -1.5f, 1f, -4.5f, 1.5f);
            for (int i = 0; i < 2; i++)
            {
                float v = -i * 3f;
                list.Add(Rope(parent, "Swing_RopeA" + (i + 1), -1.5f, -7.5f, v, endPost: true));
                poles.Add(Pole(parent, "Swing_Pair" + (i + 1), -6.5f, v, Height + PoleGrip, Vector3.left));
                list.Add(Rope(parent, "Swing_RopeB" + (i + 1), -15.5f, -21.5f, v, startPost: true));
            }
            Floor(parent, "Swing_Finish", -24f, -21.5f, -4.5f, 4.9f);
            Stair(parent, "Swing_FinishStair", -21.5f, 3.2f, Vector3.right);
            return list;
        }

        static HumanCannonStation[] BuildCannonLane(Transform parent, List<HumanCannonPole> poles, out Transform trampolineSpot)
        {
            supports = Group(parent, "Supports");
            var stationPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CannonKit + "CannonStationPrepared.prefab");
            var stations = new HumanCannonStation[2];
            for (int i = 0; i < 2; i++)
            {
                // 2026-11-14 사용자: 공연에 쓰는 대포를 그대로 가져온다(포신 연출·공연 기물 그림 포함). 공연 코스 사본의 대포를 복제한다.
                var performance = AssetDatabase.LoadAssetAtPath<GameObject>(CannonKit + "RehearsalCourseFromPerformance.prefab");
                var source = performance != null ? performance.transform.Find("Cannon_1") : null;
                var cannon = source != null ? Object.Instantiate(source.gameObject, parent) : (GameObject)PrefabUtility.InstantiatePrefab(stationPrefab, parent);
                cannon.SetActive(true);
                cannon.name = "Cannon_" + (i + 1);
                cannon.transform.SetPositionAndRotation(At(-14f, -10.5f - i * 4f, 0f), Quaternion.LookRotation(Vector3.right));
                stations[i] = cannon.GetComponent<HumanCannonStation>();
            }
            // 2026-11-14 사용자: 대포 구간을 넓힌다. 인간 그네 구간 아래(v -7 ~ -19)를 통째로 쓴다.
            // 대포 2문(4m 간격) → 트램펄린을 들고 받는 빈 바닥 → 그네 → 중간 발판 → 도착 지역(계단은 오른쪽).
            trampolineSpot = new GameObject("TrampolineSpot").transform;
            trampolineSpot.SetParent(parent, false);
            trampolineSpot.position = At(-17.5f, -7.5f, 0f);
            // 공연 대포는 앞 7.1·위 12m/s다(포구 u -12.5, 높이 2m). 꼭대기는 u -3.8(높이 약 9.3m),
            // 그네를 잡지 않으면 발판 높이(2.5m)에 u 4.5쯤 떨어진다. 그네·발판을 이 탄도에 맞췄다(초안, 플레이 확인 필요).
            poles.Add(Pole(parent, "Cannon_Swing", -4.1f, -12.5f, 7.8f, Vector3.right));
            Floor(parent, "Cannon_Middle", -2f, 2.5f, -16f, -9f);
            Floor(parent, "Cannon_Finish", 3.5f, 11.5f, -16.5f, -8.5f);
            Stair(parent, "Cannon_FinishStair", 11.5f, -10.5f, new Vector3(1, 0, 0));
            return stations;
        }

        static Transform Group(Transform parent, string name)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        /// <summary>윗면이 줄 높이인 발판. 링 안쪽 모서리마다 받침 기둥을 세운다.</summary>
        static void Floor(Transform parent, string name, float u0, float u1, float v0, float v1)
        {
            var floor = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Kit + "FloorModulePrepared.prefab"), parent);
            floor.name = name;
            floor.transform.position = At((u0 + u1) * .5f, (v0 + v1) * .5f, Height);
            // 원본 바닥은 4×4m, 윗면이 y=0.
            floor.transform.localScale = new Vector3(Mathf.Abs(u1 - u0) / 4f, 1f, Mathf.Abs(v1 - v0) / 4f);
            foreach (float u in new[] { Mathf.Min(u0, u1) + .3f, Mathf.Max(u0, u1) - .3f })
                foreach (float v in new[] { Mathf.Min(v0, v1) + .3f, Mathf.Max(v0, v1) - .3f })
                    Support(name + "_Leg", u, v, Height - .25f);
        }

        /// <summary>계단 한 벌(원본 높이 약 1.83m)을 줄 높이에 맞게 같은 비율로 키운다. (u, v)는 맞닿는 발판 가장자리, toward는 내려가는 방향.</summary>
        static void Stair(Transform parent, string name, float u, float v, Vector3 toward)
        {
            var stair = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Kit + "StairModulePrepared.prefab"), parent);
            stair.name = name;
            float scale = Height / 1.83f, half = 1.25f * scale;
            // 원본 계단은 로컬 +z가 아래쪽 진입, -z가 위쪽이다.
            stair.transform.SetPositionAndRotation(At(u + toward.x * half, v + toward.z * half, 0f), Quaternion.LookRotation(toward));
            stair.transform.localScale = Vector3.one * scale;
            // 원본 계단은 첫 단이 0.51m(키우면 0.7m)라 점프해야 올라갈 수 있었다(2026-11-14 사용자). 아래에 두 단을 덧대 끊김 없이 오른다.
            var material = stair.GetComponentInChildren<Renderer>()?.sharedMaterial;
            foreach (var (height, z) in new[] { (.34f, 1.405f), (.17f, 1.715f) })
            {
                var step = GameObject.CreatePrimitive(PrimitiveType.Cube);
                step.name = "ExtraStep";
                step.transform.SetParent(stair.transform, false);
                step.transform.localPosition = new Vector3(0f, height * .5f, z);
                step.transform.localScale = new Vector3(2.5f, height, .31f);
                if (material != null) step.GetComponent<Renderer>().sharedMaterial = material;
            }
        }

        /// <summary>줄 하나 = 충돌 상자 하나(코스의 줄 번호 순서) + 공연 외줄 외형.</summary>
        static BoxCollider Rope(Transform parent, string name, float u0, float u1, float v, bool startPost = false, bool endPost = false)
        {
            Vector3 from = At(u0, v, Height), to = At(u1, v, Height);
            float length = Vector3.Distance(from, to);
            var rope = new GameObject(name);
            rope.transform.SetParent(parent, false);
            rope.transform.SetPositionAndRotation(from, Quaternion.LookRotation(to - from));
            var settings = GameSettings.Tightrope.Course;
            var support = rope.AddComponent<BoxCollider>();
            support.center = new Vector3(0, -settings.RopeThickness * .5f, length * .5f);
            support.size = new Vector3(settings.RopeWalkWidth, settings.RopeThickness, length);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Kit + "RopeSegmentPrepared.prefab"), rope.transform);
            visual.name = "Visual";
            // 원본 외줄 조각은 길이 3m.
            visual.transform.localScale = new Vector3(1f, 1f, length / 3f);
            foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            // 허공에서 끝나는 줄 끝은 기둥으로 받친다.
            if (startPost) Support(name + "_Post", u0, v, Height);
            if (endPost) Support(name + "_Post", u1, v, Height);
            return support;
        }

        /// <summary>공연 봉(인간 그네). grip은 링 바닥 기준 봉 높이, forward는 넘어가는 방향.</summary>
        static HumanCannonPole Pole(Transform parent, string name, float u, float v, float grip, Vector3 forward)
        {
            var pole = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CannonKit + "ReleasePolePrepared.prefab"), parent);
            pole.name = name;
            pole.transform.SetPositionAndRotation(At(u, v, grip), Quaternion.LookRotation(forward));
            return pole.GetComponent<HumanCannonPole>();
        }

        static void Support(string name, float u, float v, float height)
        {
            // 링 바닥 위에 설 수 있는 자리에만 세운다.
            if (new Vector2(u, v).magnitude > SupportReach) return;
            var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leg.name = name;
            leg.transform.SetParent(supports, false);
            leg.transform.position = At(u, v, height * .5f);
            leg.transform.localScale = new Vector3(.18f, height, .18f);
            if (supportMaterial != null) leg.GetComponent<Renderer>().sharedMaterial = supportMaterial;
        }
    }
}
