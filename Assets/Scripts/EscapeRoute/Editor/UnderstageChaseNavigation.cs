using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using NavBuilder = UnityEngine.AI.NavMeshBuilder;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static class UnderstageChaseNavigation
    {
        const string Folder = "Assets/Scenes/Tests/EscapeRoute/Pursuer";

        [MenuItem("Tools/CurtainCall/탈출로/추격자 테스트 씬 이동면 굽기")]
        public static void Bake()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || (scene.path != Folder + "/UnderstagePursuerPlaytest.unity" && scene.path != EscapePursuerLayoutReview.SourceScene))
                throw new InvalidOperationException("탈출로 A 또는 추격자 테스트 씬의 편집 모드에서 실행하세요.");
            var frame = scene.GetRootGameObjects();
            var module = frame.SelectMany(r => r.GetComponentsInChildren<EscapePursuerRoute>(true)).SingleOrDefault();
            var surface = module ? module.GetComponent<NavMeshSurface>() : null;
            if (!surface) throw new InvalidOperationException("EscapePursuer_A의 이동면이 필요합니다.");
            var settings = surface.GetBuildSettings();
            settings.agentRadius = 1.5f;
            settings.agentHeight = 3.55f;
            settings.agentClimb = .22f;
            settings.overrideVoxelSize = true;
            settings.voxelSize = .1f;
            var sources = new List<NavMeshBuildSource>();
            Physics.SyncTransforms();
            NavBuilder.CollectSources(module.transform.root, surface.layerMask, NavMeshCollectGeometry.PhysicsColliders,
                0, new List<NavMeshBuildMarkup>(), sources);
            sources.RemoveAll(s => s.component is Collider c && (c.isTrigger || c.GetComponentInParent<EscapeUnderstageChase>()
                || c.name == "Personnel light curtain" || c.name == "Counterweighted shutter"));
            sources.RemoveAll(s => s.component && s.component.TryGetComponent<NavMeshModifier>(out var m) &&
                m.isActiveAndEnabled && m.ignoreFromBuild && m.AffectsAgentType(surface.agentTypeID));
            foreach (var volume in surface.GetComponentsInChildren<NavMeshModifierVolume>())
            {
                if (!volume.isActiveAndEnabled || !volume.AffectsAgentType(surface.agentTypeID) ||
                    (surface.layerMask & (1 << volume.gameObject.layer)) == 0) continue;
                var scale = volume.transform.lossyScale;
                sources.Add(new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.ModifierBox,
                    transform = Matrix4x4.TRS(volume.transform.TransformPoint(volume.center), volume.transform.rotation, Vector3.one),
                    size = Vector3.Scale(volume.size, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z))),
                    area = volume.area
                });
            }
            var data = NavBuilder.BuildNavMeshData(settings, sources, new Bounds(new Vector3(30, -2, 38), new Vector3(100, 45, 110)),
                module.transform.root.position, module.transform.root.rotation);
            if (!data) throw new InvalidOperationException("추격자 이동면 생성 실패");
            data.name = "EscapeAChaseNavigation";
            const string path = "Assets/Resources/Prefabs/Characters/Enemies/UnderstagePursuer/EscapeAChaseNavigation.asset";
            var saved = AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
            surface.RemoveData();
            if (saved)
            {
                EditorUtility.CopySerialized(data, saved);
                UnityEngine.Object.DestroyImmediate(data);
                EditorUtility.SetDirty(saved);
            }
            else
            {
                saved = data;
                AssetDatabase.CreateAsset(saved, path);
            }
            surface.navMeshData = saved;
            surface.AddData();
            AssetDatabase.SaveAssetIfDirty(saved);
            EditorUtility.SetDirty(surface);
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
