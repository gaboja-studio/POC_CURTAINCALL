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
    /// 수치: 줄 높이 2.5m·줄 간격 3m·연속 봉 간격 8m·봉 높이(줄 위 0.6m에 발)는 리허설 값. 나머지 길이는 링(반지름 20m)에 맞췄다.
    /// 링의 옛 연습 소품(탑·사다리·그물·옛 줄·장식 대포·장식 트램펄린)은 지운다.
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
        const float SupportReach = 19.3f;

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

        static List<BoxCollider> BuildRopes(Transform parent, List<HumanCannonPole> poles)
        {
            supports = Group(parent, "Supports");
            var list = new List<BoxCollider>();
            // ── 위: 외줄타기(왼→오) ──
            Floor(parent, "Tightrope_Start", -13f, -10f, 5.5f, 17.5f);
            Stair(parent, "Tightrope_StartStair", -11.5f, 5.5f, new Vector3(0, 0, -1));
            for (int i = 0; i < 4; i++) list.Add(Rope(parent, "Tightrope_Rope" + (i + 1), -10f, 8f, 7f + i * 3f));
            Floor(parent, "Tightrope_Finish", 8f, 13f, 5.5f, 17.5f);
            Floor(parent, "Tightrope_FinishWalk", 13f, 20.3f, 5.5f, 8.5f);
            // 도착 지역 → 가운데 구간 오른쪽 발판(위→아래)
            poles.Add(Pole(parent, "Swing_ToSwingLane", 19.3f, 6.5f, Height + PoleGrip, Vector3.back));
            // ── 가운데: 인간 그네(오→왼) ──
            Floor(parent, "Swing_Start", 17.8f, 21.3f, -5.5f, .5f);
            for (int i = 0; i < 3; i++) poles.Add(Pole(parent, "Swing_Chain" + (i + 1), 18.8f - i * 8f, -2.5f, Height + PoleGrip, Vector3.left));
            Floor(parent, "Swing_Middle", -4.7f, -2.2f, -5.5f, .5f);
            for (int i = 0; i < 2; i++)
            {
                float v = -1f - i * 3f;
                list.Add(Rope(parent, "Swing_RopeA" + (i + 1), -4.7f, -7.7f, v, endPost: true));
                poles.Add(Pole(parent, "Swing_Pair" + (i + 1), -6.7f, v, Height + PoleGrip, Vector3.left));
                list.Add(Rope(parent, "Swing_RopeB" + (i + 1), -15.7f, -18.7f, v, startPost: true));
            }
            Floor(parent, "Swing_Finish", -21.2f, -18.7f, -5.5f, 3.9f);
            Stair(parent, "Swing_FinishStair", -18.7f, 2.2f, Vector3.right);
            return list;
        }

        static HumanCannonStation[] BuildCannonLane(Transform parent, List<HumanCannonPole> poles, out Transform trampolineSpot)
        {
            supports = Group(parent, "Supports");
            var stationPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CannonKit + "CannonStationPrepared.prefab");
            var stations = new HumanCannonStation[2];
            for (int i = 0; i < 2; i++)
            {
                var cannon = (GameObject)PrefabUtility.InstantiatePrefab(stationPrefab, parent);
                cannon.name = "Cannon_" + (i + 1);
                cannon.transform.SetPositionAndRotation(At(-12f, -11f - i * 3.5f, 0f), Quaternion.LookRotation(Vector3.right));
                stations[i] = cannon.GetComponent<HumanCannonStation>();
            }
            trampolineSpot = new GameObject("TrampolineSpot").transform;
            trampolineSpot.SetParent(parent, false);
            trampolineSpot.position = At(-14.5f, -7.5f, 0f);
            // 대포 탄도(앞 8·위 12m/s) 꼭대기 바로 아래. 전구 줄(바닥에서 약 8.2m)보다 낮게 건다.
            poles.Add(Pole(parent, "Cannon_Swing", -1f, -12.75f, 7.8f, Vector3.right));
            Floor(parent, "Cannon_Middle", 1.5f, 5.5f, -14.75f, -10.75f);
            Floor(parent, "Cannon_Finish", 8.5f, 12.5f, -14.75f, -10.75f);
            Stair(parent, "Cannon_FinishStair", 10.5f, -10.75f, new Vector3(0, 0, 1));
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
