using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.WaitingRoom.Editor
{
    // PR #194 통합: 아트 배치는 유지하고 실제 플레이 기능을 그레이박스에 모은다.
    public static class WaitingRoomArtIntegration
    {
        const string ScenePath = "Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity";

        public static void Integrate()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            var greybox = roots.Single(g => g.name == "WaitingRoomGreybox");
            greybox.SetActive(true);
            foreach (string name in new[] { "WaitingRoomGameplay", "NightSchedule", "ExplorationTimer" })
            {
                var root = roots.SingleOrDefault(g => g.name == name);
                if (root != null) root.transform.SetParent(greybox.transform, false);
            }
            var legacy = roots.Where(g => g.name == "WaitingRoom" || g.name == "대기실").ToArray();
            foreach (var component in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Component>(true)))
            {
                if (component == null || legacy.Any(g => component.transform.IsChildOf(g.transform))) continue;
                var serialized = new SerializedObject(component);
                var property = serialized.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    var target = property.objectReferenceValue;
                    var targetObject = target as GameObject ?? (target as Component)?.gameObject;
                    if (targetObject == null || !legacy.Any(g => targetObject.transform.IsChildOf(g.transform))) continue;
                    if (component is WaitingRoomFlickerLight && target is Renderer bulb)
                    {
                        // 복제된 조명이 옛 원본 전구를 가리키던 참조를 같은 조명 기구의 전구로 바꾼다.
                        var candidates = component.transform.parent.GetComponentsInChildren<Renderer>(true)
                            .Where(r => r.name == bulb.name && r.sharedMaterial == bulb.sharedMaterial)
                            .OrderBy(r => Vector3.SqrMagnitude(r.bounds.center - component.transform.position)).ToArray();
                        if (candidates.Length == 0) throw new InvalidOperationException("복제 전구를 찾지 못했습니다: " + component.name);
                        property.objectReferenceValue = candidates[0];
                        Debug.Log($"[PR194] 전구 참조 이전: {component.name} → {candidates[0].name}");
                    }
                    else throw new InvalidOperationException($"옛 대기실 참조를 먼저 이전해야 합니다: {component.name}.{property.propertyPath}");
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (var root in legacy) Object.DestroyImmediate(root);
            Physics.SyncTransforms();
            Validate(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("대기실 씬 저장 실패");
            Debug.Log("[PR194] 옛 대기실 제거·그레이박스 기능 이전·씬 검증 완료");
        }

        public static void Verify() => Validate(EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single));

        static void Validate(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            if (roots.Any(g => g.name == "WaitingRoom" || g.name == "대기실"))
                throw new InvalidOperationException("옛 대기실 루트가 남아 있습니다");
            var greybox = roots.Single(g => g.name == "WaitingRoomGreybox");
            if (!greybox.activeInHierarchy) throw new InvalidOperationException("그레이박스 비활성화");
            foreach (var transform in greybox.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) != 0)
                    throw new InvalidOperationException("끊어진 스크립트: " + transform.name);
            foreach (var type in new[] { typeof(WaitingRoomSpawn), typeof(StageEntranceGather), typeof(HandoverShelfView), typeof(ScheduleBoardView), typeof(ShopPhone), typeof(ShopDeliverySlot), typeof(ExpertRoomView), typeof(NightDoorLock), typeof(WaitingRoomExitWatch) })
            {
                var matches = greybox.GetComponentsInChildren(type, false);
                if (matches.Length != 1) throw new InvalidOperationException($"활성 기능 개수 오류: {type.Name}={matches.Length}");
            }
            var shelf = greybox.GetComponentInChildren<HandoverShelfView>();
            var boards = new SerializedObject(shelf).FindProperty("boards");
            if (boards.arraySize != 4) throw new InvalidOperationException("물려줄 선반은 4칸이어야 합니다");
            for (int i = 0; i < boards.arraySize; i++)
                if (boards.GetArrayElementAtIndex(i).objectReferenceValue == null)
                    throw new InvalidOperationException("물려줄 선반 판자 참조 누락");
            var gather = greybox.GetComponentInChildren<StageEntranceGather>();
            gather.ApplyArea();
            var spawn = greybox.GetComponentInChildren<WaitingRoomSpawn>();
            Debug.Log($"[PR194] 검증 PASS: roots={roots.Length}, spawn={spawn.transform.position}, gather={gather.transform.position}, shelf={shelf.transform.position}, boards=4");
        }
    }
}
