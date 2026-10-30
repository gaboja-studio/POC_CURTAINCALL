using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace CurtainCall.WaitingRoom.Editor
{
    /// <summary>
    /// 대기실 최적화 1단계(2026-10-30, waiting-room-optimization.md): 작은 소품 그림자 끄기, 움직이지 않는 물체 오클루전 정적 표시, 오클루전 굽기.
    /// 프로젝트 공용 설정(URP 에셋)은 건드리지 않는다. 다시 실행해도 같은 결과다.
    /// 움직이거나 켜졌다 꺼지는 물체는 정적에서 뺀다: 게임 스크립트·Rigidbody·Animator·NetworkObject가 붙은 묶음, 게임 스크립트가 참조하는 오브젝트(문짝·철창·숨김 대상 등)와 그 아래 전부.
    /// </summary>
    public static class WaitingRoomOptimizationPass
    {
        const float SmallProp = .6f, LowProp = .8f, OccluderMin = 2f;
        const StaticEditorFlags Occlusion = StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;

        [MenuItem("Tools/CurtainCall/대기실/최적화 1단계 적용")]
        public static void ApplyMenu() => Debug.Log(Apply());

        [MenuItem("Tools/CurtainCall/대기실/오클루전 굽기")]
        public static void BakeMenu() => Debug.Log(StartBake());

        public static string Apply()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var dynamicRoots = DynamicRoots(scene);
            int shadowsOff = 0, occluders = 0, occludees = 0, skipped = 0;
            foreach (var r in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true)))
            {
                var b = r.bounds; float largest = Mathf.Max(b.size.x, b.size.y, b.size.z);
                // 작은·낮은 소품은 그림자를 만들지 않는다(움직이는 것도 겉모습만 바뀐다).
                if (largest < SmallProp && b.size.y < LowProp && r.shadowCastingMode != ShadowCastingMode.Off)
                { Undo.RecordObject(r, "Small prop shadows off"); r.shadowCastingMode = ShadowCastingMode.Off; shadowsOff++; }

                if (IsDynamic(r.transform, dynamicRoots)) { skipped++; continue; }
                var flags = GameObjectUtility.GetStaticEditorFlags(r.gameObject);
                // 가리는 쪽은 큰 불투명 물체만, 가려지는 쪽은 모든 정적 물체.
                bool opaque = r.sharedMaterials.All(m => m == null || m.renderQueue < (int)RenderQueue.AlphaTest + 50);
                var want = flags | StaticEditorFlags.OccludeeStatic;
                if (largest >= OccluderMin && opaque) { want |= StaticEditorFlags.OccluderStatic; occluders++; }
                else want &= ~StaticEditorFlags.OccluderStatic;
                occludees++;
                if (want != flags) { Undo.RecordObject(r.gameObject, "Occlusion static"); GameObjectUtility.SetStaticEditorFlags(r.gameObject, want); }
            }
            EditorSceneManager.MarkSceneDirty(scene);
            return $"shadowsOff {shadowsOff}, occludee {occludees} (occluder {occluders}), dynamic skipped {skipped}, dynamic roots {dynamicRoots.Count}";
        }

        public static string StartBake()
        {
            StaticOcclusionCulling.smallestOccluder = 1.5f;
            StaticOcclusionCulling.smallestHole = .25f;
            StaticOcclusionCulling.backfaceThreshold = 100;
            return StaticOcclusionCulling.GenerateInBackground() ? "occlusion bake started" : "occlusion bake failed to start";
        }

        public static bool IsBaking => StaticOcclusionCulling.isRunning;

        static bool IsDynamic(Transform t, HashSet<Transform> roots)
        {
            for (var p = t; p != null; p = p.parent) if (roots.Contains(p)) return true;
            return false;
        }

        // 런타임에 움직이거나 켜고 끄는 묶음의 뿌리들.
        static HashSet<Transform> DynamicRoots(UnityEngine.SceneManagement.Scene scene)
        {
            var roots = new HashSet<Transform>();
            foreach (var go in scene.GetRootGameObjects())
            {
                foreach (var c in go.GetComponentsInChildren<Component>(true))
                {
                    if (c == null) continue;
                    if (c is Rigidbody || c is Animator || c is Animation || c is NetworkObject || c is Cloth) { roots.Add(c.transform); continue; }
                    if (c is not MonoBehaviour mb) continue;
                    var ns = mb.GetType().Namespace ?? "";
                    if (!ns.StartsWith("CurtainCall")) continue;
                    // 순수 표시용(외곽선 기준판 등)도 포함해 안전하게 뺀다.
                    roots.Add(mb.transform);
                    var so = new SerializedObject(mb); var it = so.GetIterator();
                    while (it.Next(true))
                        if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue != null)
                        {
                            if (it.objectReferenceValue is GameObject g) roots.Add(g.transform);
                            else if (it.objectReferenceValue is Component rc && rc.gameObject.scene == scene) roots.Add(rc.transform);
                        }
                }
            }
            // 씬 최상위·방 묶음 같은 큰 그룹이 참조돼 전체가 빠지지 않게, 자식이 많은 그룹은 참조 뿌리에서 뺀다(컴포넌트가 직접 붙은 경우는 남긴다).
            roots.RemoveWhere(t => t.parent == null || (t.GetComponentsInChildren<Renderer>(true).Length > 400 && !t.GetComponents<MonoBehaviour>().Any(m => m != null && (m.GetType().Namespace ?? "").StartsWith("CurtainCall"))));
            return roots;
        }
    }
}
