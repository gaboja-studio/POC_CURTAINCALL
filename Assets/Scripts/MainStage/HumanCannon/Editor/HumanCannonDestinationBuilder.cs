using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CurtainCall.HumanCannon.EditorTools
{
    /// <summary>기존 최종 착지판을 실제 목적지로 연결하고 충돌 없는 녹색 표식만 추가한다.</summary>
    public static class HumanCannonDestinationBuilder
    {
        const string MaterialPath = "Assets/Scenes/Tests/MainStage/HumanCannon/Materials/route-shelter.mat";
        const string FontPath = "Assets/Scenes/Tests/MainStage/HumanCannon/Labels/distance-font.asset";

        [MenuItem("Tools/CurtainCall/인간 대포/043 실제 목적지 연결")]
        public static void AddToOpenScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty
                || scene.path != HumanCannonTestSceneBuilder.ScenePath)
                throw new InvalidOperationException("Play 종료 후 저장된 HumanCannon 전용 씬을 여세요.");
            var course = scene.GetRootGameObjects().SingleOrDefault(root => root.name == "FinalCannonCourse");
            if (course == null) throw new InvalidOperationException("원본 FinalCannonCourse가 필요합니다.");
            if (course.GetComponent<HumanCannonDestination>() != null || course.transform.Find("DestinationMarkers") != null)
                throw new InvalidOperationException("목적지가 이미 연결되어 있어 덮어쓰지 않았습니다.");

            var pad = course.transform.Find("FlightLandingPad");
            var zone = pad != null ? pad.GetComponent<HumanCannonLandingZone>() : null;
            var surface = pad != null ? pad.GetComponent<BoxCollider>() : null;
            if (zone == null || !zone.enabled || surface == null || !surface.enabled || surface.isTrigger
                || !surface.gameObject.activeInHierarchy)
                throw new InvalidOperationException("기존 FlightLandingPad의 안전 착지 연결을 확인하세요.");
            var green = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (green == null || font == null || font.material == null)
                throw new InvalidOperationException("기존 녹색 머티리얼 또는 거리 라벨 폰트가 없습니다.");

            Undo.IncrementCurrentGroup();
            int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("043 실제 목적지 연결");
            try
            {
                var destination = Undo.AddComponent<HumanCannonDestination>(course);
                using var data = new SerializedObject(destination);
                var target = data.FindProperty("destination");
                var difficulty = data.FindProperty("difficultyLevel");
                if (target == null || difficulty == null)
                    throw new InvalidOperationException("목적지 컴포넌트의 destination/difficultyLevel 필드가 없습니다.");
                target.objectReferenceValue = zone;
                difficulty.intValue = 1;
                data.ApplyModifiedPropertiesWithoutUndo();

                var markers = new GameObject("DestinationMarkers");
                markers.transform.SetParent(course.transform, false);
                Undo.RegisterCreatedObjectUndo(markers, "충돌 없는 목적지 표식");
                Bounds bounds = surface.bounds;
                float top = bounds.max.y;
                float middleX = bounds.center.x;
                float middleZ = bounds.center.z;
                float edgeY = top + 0.03f;
                // 착지판 자체의 이름·위치·크기·머티리얼·충돌체는 수정하지 않는다.
                Part(markers.transform, "NearEdge", new Vector3(middleX, edgeY, bounds.min.z + 0.12f),
                    new Vector3(bounds.size.x, 0.04f, 0.24f), green);
                Part(markers.transform, "FarEdge", new Vector3(middleX, edgeY, bounds.max.z - 0.12f),
                    new Vector3(bounds.size.x, 0.04f, 0.24f), green);
                foreach (int side in new[] { -1, 1 })
                {
                    string prefix = side < 0 ? "Left" : "Right";
                    Part(markers.transform, prefix + "Edge", new Vector3(middleX + side * (bounds.extents.x - 0.12f), edgeY, middleZ),
                        new Vector3(0.24f, 0.04f, bounds.size.z), green);
                    Part(markers.transform, prefix + "FinishPost",
                        new Vector3(middleX + side * (bounds.extents.x + 0.3f), top + 1.75f, bounds.max.z + 0.3f),
                        new Vector3(0.3f, 3.5f, 0.3f), green);
                }
                Part(markers.transform, "FinishBanner", new Vector3(middleX, top + 3.5f, bounds.max.z + 0.3f),
                    new Vector3(bounds.size.x + 0.9f, 0.8f, 0.16f), green);
                Label(markers.transform, "FinishSign", font, new Vector3(middleX, top + 3.5f, bounds.max.z + 0.2f),
                    Quaternion.identity, new Vector2(10, 1), 8);
                Label(markers.transform, "FinishGroundLabel", font, new Vector3(middleX, top + 0.04f, middleZ),
                    Quaternion.Euler(90, 0, 0), new Vector2(10, 2), 12);

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("043 실제 목적지 연결 저장 실패");
                Undo.CollapseUndoOperations(undo);
                Debug.Log("[HumanCannonDestination] 기존 최종 착지판을 실제 목적지로 연결하고 녹색 FINISH 표식을 저장했습니다.");
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }

        static void Part(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.SetPositionAndRotation(position, Quaternion.identity);
            part.transform.localScale = scale;
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
            part.GetComponent<Renderer>().sharedMaterial = material;
        }

        static void Label(Transform parent, string name, TMP_FontAsset font, Vector3 position,
            Quaternion rotation, Vector2 size, float fontSize)
        {
            var label = new GameObject(name);
            label.transform.SetParent(parent, false);
            label.transform.SetPositionAndRotation(position, rotation);
            var text = label.AddComponent<TextMeshPro>();
            text.font = font;
            text.fontSharedMaterial = font.material;
            text.text = "FINISH";
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta = size;
            text.color = Color.white;
            var renderer = text.GetComponent<Renderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}
