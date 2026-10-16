using System;
using System.Linq;
using CurtainCall.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.HumanCannon.EditorTools
{
    /// <summary>
    /// 043 최종 대포의 실제 탄도에 가로 톱 2개와 수직 톱 공유 구간을 추가한다.
    /// 배치·크기·예고·속도는 플레이 확인을 위한 조정 가능한 임시값이며 최종 밸런스가 아니다.
    /// 기존 대포·바닥·연습 템플릿은 변경하지 않는다.
    /// </summary>
    public static class HumanCannonFinalSawBuilder
    {
        const string MaterialFolder = "Assets/Scenes/Tests/MainStage/HumanCannon/Materials/";

        [MenuItem("Tools/CurtainCall/인간 대포/043 최종 톱 구간 추가")]
        public static void AddToOpenScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty
                || scene.path != HumanCannonTestSceneBuilder.ScenePath)
                throw new InvalidOperationException("Play 종료 후 저장된 HumanCannon 전용 씬을 여세요.");

            var roots = scene.GetRootGameObjects();
            var course = roots.SingleOrDefault(root => root.name == "FinalCannonCourse");
            if (course == null) throw new InvalidOperationException("최종 대포 4대가 있는 원본 FinalCannonCourse가 필요합니다.");
            if (course.transform.Find("FinalSaws") != null
                || course.GetComponentInChildren<HumanCannonFinalSaws>(true) != null)
                throw new InvalidOperationException("최종 톱 구간이 이미 있어 덮어쓰지 않았습니다.");
            if (course.transform.position != Vector3.zero || course.transform.rotation != Quaternion.identity
                || course.transform.lossyScale != Vector3.one)
                throw new InvalidOperationException("원본 FinalCannonCourse의 원점·방향·배율을 확인하세요. 기존 배치는 변경하지 않았습니다.");

            var systems = roots.Where(root => root.GetComponent<HumanCannonSoloPractice>() == null)
                .SelectMany(root => root.GetComponentsInChildren<HumanCannonSystem>(true)).ToArray();
            if (systems.Length != 1 || systems[0].StationCount != 10)
                throw new InvalidOperationException("원본 대포 10대가 연결된 코스여야 합니다.");
            var station = systems[0].Station(6);
            var deckTransform = course.transform.Find("PreparationDeck");
            var deck = deckTransform != null ? deckTransform.GetComponent<BoxCollider>() : null;
            if (station == null || !station.transform.IsChildOf(course.transform)
                || deck == null || !deck.enabled || deck.isTrigger || !deck.gameObject.activeInHierarchy)
                throw new InvalidOperationException("최종 첫 대포 또는 준비 발판 연결을 확인하세요.");

            var silver = Material("pole-gate-silver");
            var black = Material("pole-gate-black");
            var yellow = Material("pole-gate-yellow");
            Vector3 muzzle = station.Muzzle.position;
            Vector3 velocity = station.LaunchVelocity;
            float gravity = GameSettings.Base.Character.Gravity;
            if (!Finite(gravity) || gravity <= 0 || !Finite(velocity.x) || !Finite(velocity.y)
                || !Finite(velocity.z) || velocity.z <= 0 || Mathf.Abs(velocity.x) > 0.001f
                || !Finite(muzzle.x) || !Finite(muzzle.y) || !Finite(muzzle.z))
                throw new InvalidOperationException("최종 대포는 유효한 양수 중력과 +Z 방향 탄도가 필요합니다.");
            float apexY = muzzle.y + velocity.y * velocity.y / (2 * gravity);
            if (!Finite(apexY)) throw new InvalidOperationException("최종 대포 정점 높이를 계산할 수 없습니다.");

            Undo.IncrementCurrentGroup();
            int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("043 최종 톱 구간");
            try
            {
                var root = new GameObject("FinalSaws");
                root.transform.SetParent(course.transform, false);
                Undo.RegisterCreatedObjectUndo(root, "최종 톱 구간 추가");
                var system = root.AddComponent<HumanCannonFinalSaws>();

                var entry = Point(root.transform, "EntryZone", new Vector3(deck.bounds.center.x,
                    deck.bounds.max.y - 0.2f + 4, (deck.bounds.min.z + deck.bounds.max.z + 1) * 0.5f))
                    .gameObject.AddComponent<BoxCollider>();
                entry.isTrigger = true;
                entry.size = new Vector3(18, 8, deck.bounds.size.z + 1);

                var origins = new Transform[2];
                var views = new Transform[2];
                float[] distances = { 7, 13 };
                for (int i = 0; i < origins.Length; i++)
                {
                    float time = distances[i] / velocity.z;
                    float feetY = muzzle.y + velocity.y * time - 0.5f * gravity * time * time;
                    if (!Finite(feetY)) throw new InvalidOperationException("가로 톱의 탄도 높이를 계산할 수 없습니다.");
                    origins[i] = Point(root.transform, "HorizontalOrigin" + (i + 1),
                        new Vector3(0, feetY + 0.45f, muzzle.z + distances[i]));
                    views[i] = Saw(root.transform, "HorizontalSaw" + (i + 1), 1, 0.2f, silver, black);
                    views[i].position = origins[i].position;
                }

                var verticalStart = Point(root.transform, "VerticalStart", new Vector3(0, apexY + 0.3f, muzzle.z + 18));
                var verticalEnd = Point(root.transform, "VerticalEnd", new Vector3(0, apexY + 0.3f, muzzle.z + 2));
                var verticalView = Saw(root.transform, "VerticalSaw", 1.2f, 0.2f, silver, black);
                verticalView.position = verticalStart.position;
                // 모델은 외줄과 같은 +Y 법선이다. 실행 시 수직 뷰와 예고 링을 함께 +X 법선으로 돌린다.
                var warningView = WarningRing(root.transform, verticalStart.position, yellow);
                warningView.gameObject.SetActive(false);

                using var data = new SerializedObject(system);
                Property(data, "entryZone").objectReferenceValue = entry;
                References(data, "horizontalOrigins", origins);
                References(data, "horizontalViews", views);
                Property(data, "verticalStart").objectReferenceValue = verticalStart;
                Property(data, "verticalEnd").objectReferenceValue = verticalEnd;
                Property(data, "verticalView").objectReferenceValue = verticalView;
                Property(data, "warningView").objectReferenceValue = warningView;
                Property(data, "warningSeconds").floatValue = 2.5f;
                Property(data, "travelSeconds").floatValue = 4;
                Property(data, "horizontalSpeed").floatValue = 2;
                Property(data, "horizontalHalfWidth").floatValue = 8;
                Property(data, "horizontalRadius").floatValue = 1;
                Property(data, "verticalRadius").floatValue = 1.2f;
                Property(data, "halfThickness").floatValue = 0.1f;
                var lanes = Property(data, "verticalLanes");
                lanes.arraySize = 4;
                for (int i = 0; i < lanes.arraySize; i++) lanes.GetArrayElementAtIndex(i).floatValue = -6 + i * 4;
                data.ApplyModifiedPropertiesWithoutUndo();

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("043 최종 톱 구간 저장 실패");
                Undo.CollapseUndoOperations(undo);
                Debug.Log("[HumanCannonFinalSaws] 원본 대포·바닥을 유지하고 공유 톱 구간 저장. 배치·예고·속도는 임시 조정값입니다.");
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }

        static Transform Saw(Transform parent, string name, float radius, float thickness, Material silver, Material black)
        {
            var saw = Point(parent, name, Vector3.zero);
            float diameter = radius * 2;
            // TightropeSaws.CreateSaw의 기본 원판 + 톱니 8개 형태만 재사용한다. 공용 코드/자산은 수정하지 않는다.
            Part(saw, "Blade", PrimitiveType.Cylinder, new Vector3(diameter * 0.9f, thickness * 0.5f, diameter * 0.9f), silver);
            for (int i = 0; i < 8; i++)
            {
                var tooth = Part(saw, "Tooth" + i, PrimitiveType.Cube,
                    new Vector3(diameter * 0.12f, thickness, diameter * 0.12f), black);
                tooth.localRotation = Quaternion.Euler(0, i * 45 + 45, 0);
                tooth.localPosition = tooth.localRotation * Vector3.forward * (diameter * 0.44f);
            }
            return saw;
        }

        static Transform WarningRing(Transform parent, Vector3 position, Material yellow)
        {
            var marker = Point(parent, "VerticalWarning", position);
            var ring = marker.gameObject.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = 32;
            ring.startWidth = ring.endWidth = 0.08f;
            ring.sharedMaterial = yellow;
            for (int i = 0; i < ring.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2 / ring.positionCount;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 1.35f);
            }
            return marker;
        }

        static Transform Part(Transform parent, string name, PrimitiveType primitive, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(primitive);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localScale = scale;
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part.transform;
        }

        static Transform Point(Transform parent, string name, Vector3 position)
        {
            var point = new GameObject(name).transform;
            point.SetParent(parent, false);
            point.position = position;
            return point;
        }

        static Material Material(string name) => AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + name + ".mat")
            ?? throw new InvalidOperationException("기존 코스 머티리얼이 없습니다: " + name);

        static SerializedProperty Property(SerializedObject data, string name) => data.FindProperty(name)
            ?? throw new InvalidOperationException("최종 톱 연결 필드가 없습니다: " + name);

        static void References(SerializedObject data, string name, Transform[] values)
        {
            var property = Property(data, name);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
