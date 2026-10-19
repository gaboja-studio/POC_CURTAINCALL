using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CurtainCall.Settings;

namespace CurtainCall.HumanCannon.EditorTools
{
    /// <summary>043 첫 배치: 쿠션 뒤 독립 대포 4대와 탄도 확인용 안전 착지판. 결과/톱 단계는 별도다.</summary>
    public static class HumanCannonFinalCannonBuilder
    {
        const string MaterialFolder = "Assets/Scenes/Tests/MainStage/HumanCannon/Materials/";

        [MenuItem("Tools/CurtainCall/인간 대포/043 최종 대포 4대 추가")]
        public static void AddToOpenScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty
                || scene.path != HumanCannonTestSceneBuilder.ScenePath)
                throw new InvalidOperationException("Play 종료 후 저장된 HumanCannon 전용 씬을 여세요.");
            var roots = scene.GetRootGameObjects();
            if (roots.Any(g => g.name == "FinalCannonCourse"))
                throw new InvalidOperationException("최종 대포가 이미 있어 덮어쓰지 않았습니다.");
            var system = roots.SelectMany(g => g.GetComponentsInChildren<HumanCannonSystem>(true)).Single();
            var systemData = new SerializedObject(system);
            var stations = systemData.FindProperty("stations");
            if (stations.arraySize != 6) throw new InvalidOperationException("042의 대포 6대가 연결된 씬이어야 합니다.");
            var template = (HumanCannonStation)stations.GetArrayElementAtIndex(3).objectReferenceValue;
            var cushion = roots.Single(g => g.name == "LandingCushion").GetComponent<BoxCollider>();
            if (template == null || cushion == null || !cushion.TryGetComponent<HumanCannonLandingZone>(out _))
                throw new InvalidOperationException("쉼터 대포/안전 쿠션 연결을 확인하세요.");
            var floorMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "route-floor.mat");
            var barrelMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "route-transport.mat");
            var landingMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "landing-cushion.mat");
            if (floorMaterial == null || barrelMaterial == null || landingMaterial == null)
                throw new InvalidOperationException("기존 코스 머티리얼이 없습니다.");
            float gravity = GameSettings.Base.Character.Gravity;
            if (gravity <= 0f) throw new InvalidOperationException("탄도 계산에는 양수 중력이 필요합니다.");

            Undo.IncrementCurrentGroup();
            int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("043 최종 대포 4대");
            Undo.RecordObject(system, "최종 대포 연결");
            try
            {
                var root = new GameObject("FinalCannonCourse");
                Undo.RegisterCreatedObjectUndo(root, "최종 대포 구간 추가");
                float top = cushion.bounds.max.y;
                float deckStart = cushion.bounds.max.z;
                float stationZ = deckStart + 5.5f;
                // 쿠션 자체와 기존 코스는 그대로 둔다. 새 바닥의 윗면만 같은 높이로 잇는다.
                Floor(root.transform, "PreparationDeck", new Vector3(0, top - 0.15f, deckStart + 5f),
                    new Vector3(18, 0.3f, 10), floorMaterial);
                stations.arraySize = 10;
                float[] xs = { -6, -2, 2, 6 };
                HumanCannonStation first = null;
                for (int i = 0; i < 4; i++)
                {
                    var clone = UnityEngine.Object.Instantiate(template.gameObject, root.transform);
                    clone.name = "FinalCannon" + (i + 1);
                    clone.transform.SetPositionAndRotation(new Vector3(xs[i], top, stationZ), Quaternion.identity);
                    var station = clone.GetComponent<HumanCannonStation>();
                    var data = new SerializedObject(station);
                    data.FindProperty("forwardSpeed").floatValue = 8;
                    data.FindProperty("upwardSpeed").floatValue = 12;
                    data.FindProperty("airControlSpeed").floatValue = 3;
                    data.FindProperty("boardingDistance").floatValue = 2;
                    data.ApplyModifiedPropertiesWithoutUndo();
                    foreach (var renderer in clone.GetComponentsInChildren<MeshRenderer>()) renderer.sharedMaterial = barrelMaterial;
                    stations.GetArrayElementAtIndex(6 + i).objectReferenceValue = station;
                    Label(root.transform, "CannonLabel" + (i + 1), (i + 1).ToString(),
                        new Vector3(xs[i], top + 0.025f, stationZ - 2.5f), 0.8f);
                    if (i == 0) first = station;
                }
                systemData.ApplyModifiedProperties();
                // 같은 높이의 안전판은 새 대포의 실제 탄도에 맞춘다. 성공 판정 컴포넌트는 아직 없다.
                float up = first.LaunchVelocity.y;
                float drop = first.Muzzle.position.y - top;
                float flightTime = (up + Mathf.Sqrt(up * up + 2 * gravity * drop)) / gravity;
                float landingZ = first.Muzzle.position.z + first.LaunchVelocity.z * flightTime;
                Floor(root.transform, "FlightLandingPad", new Vector3(0, top - 0.15f, landingZ),
                    new Vector3(20, 0.3f, 10), landingMaterial);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("043 테스트 씬 저장 실패");
                Undo.CollapseUndoOperations(undo);
                Debug.Log("[HumanCannonFinale] 독립 대포4대·안전 착지판 저장. F 탑승/새 F 발사, 톱·결과는 후속 단계.");
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }

        static void Floor(Transform parent, string name, Vector3 center, Vector3 size, Material material)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = name;
            floor.transform.SetParent(parent, false);
            floor.transform.position = center;
            floor.transform.localScale = size;
            floor.GetComponent<Renderer>().sharedMaterial = material;
            floor.AddComponent<HumanCannonLandingZone>();
        }

        static void Label(Transform parent, string name, string text, Vector3 position, float size)
        {
            var label = new GameObject(name);
            label.transform.SetParent(parent, false);
            label.transform.SetPositionAndRotation(position, Quaternion.Euler(90, 0, 0));
            var mesh = label.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.fontSize = 48;
            mesh.characterSize = size;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.color = Color.white;
        }
    }
}
