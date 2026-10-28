using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        public static string PolishAPlacement()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("A 편집 씬이 필요합니다.");
            var background = GameObject.Find(RootName).transform;
            if (background.Find("21_PlacementPolish") != null) return "A placement polish already applied.";
            bConcrete = RunnerMaterial("brutal-concrete"); bRecess = RunnerMaterial("brutal-recess");
            bSteel = RunnerMaterial("brutal-steel"); bBulb = RunnerMaterial("concrete-service-bulb");
            wornSteel = RunnerMaterial("working-steel"); wornPipe = RunnerMaterial("working-pipe");
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Align A services, lighting and stair support");
            var g = VerticalChild(background, "21_PlacementPolish");
            g.localScale = new Vector3(1 / background.lossyScale.x, 1 / background.lossyScale.y, 1 / background.lossyScale.z);
            var context = background.Find("15_ServiceContext");
            // The old context used a wider floor. Pull the cart/pallet wholly onto the new 10m edge.
            MovePolish(context.Find("작업대"), new Vector3(11.05f, 0, 45.25f));
            MovePolish(context.Find("정비용 팔레트"), new Vector3(11.2f, 0, 40.5f));
            MovePolish(context.Find("벽면 점검함"), new Vector3(7.63f, 1.55f, 45.7f));
            MovePolish(context.Find("Wayfinding_벽면 점검 표식"), new Vector3(7.77f, 2.45f, 45.7f));
            foreach (var t in context.Cast<Transform>().Where(t => t.name == "점검함으로 모이는 케이블"))
                MovePolish(t, new Vector3(7.58f, t.position.y, 44.6f));
            // Fill the underside of stairs, keeping all existing 0.2m treads and endpoints.
            for (int i = 0; i < 40; i++)
            {
                float top = -8 + (i + 1) * .2f - .4f;
                BFloor(g, "06 계단 받침 " + (i + 1), 119, 127, 50 + i * .35f, 50 + (i + 1) * .35f, top, .8f);
            }
            for (int i = 0; i < 10; i++)
            {
                float top = 2 - (i + 1) * .2f - .5f;
                BFloor(g, "04 계단 받침 " + (i + 1), 64, 72, 60.5f - (i + 1) * .5f, 60.5f - i * .5f, top, .8f);
            }
            // Heavy equipment beside the runner was floating over the trench.
            foreach (float x in new[] { 176f, 196f, 216f, 329f })
            {
                bool south = x == 176 || x == 216;
                float z = south ? 64 : 92;
                BFloor(g, "반출 구동기 전용 받침", x - 8, x - 4, z - 2, z + 2, 0, 2);
                BMass(g, "반출 구동기 받침 기둥", new Vector3(x - 6, -25, z), new Vector3(2.5f, 46, 2.5f));
            }
            foreach (float x in new[] { 168.5f, 227.5f, 313.5f, 368.5f })
                BMass(g, "측면 정비대 하부 지지", new Vector3(x, -24.9f, 89), new Vector3(2, 46.2f, 3));
            var swing = background.Find(RunnerGroup + "/04_SwingChasm_276_298");
            foreach (var beam in swing.Cast<Transform>().Where(t => t.name.EndsWith("하중 보")))
            {
                Undo.RecordObject(beam, "Seat chasm support below the walking slab");
                beam.localPosition = new Vector3(beam.localPosition.x, -5.25f, beam.localPosition.z);
                beam.localScale = new Vector3(4, 5.5f, 14);
            }

            var lighting = VerticalChild(g, "MountedWorkLights");
            Physics.SyncTransforms();
            int mounts = MountExistingLights(background, lighting);
            // Give the two descending landings an aimed fill without lifting the whole cavern exposure.
            BLight(lighting, new Vector3(72, 4, 58), new Vector3(68, .5f, 55.8f), 150, 13);
            BLight(lighting, new Vector3(126.6f, -3.8f, 48), new Vector3(122, -8, 47), 180, 14);
            MountExistingLights(lighting, VerticalChild(g, "LandingLampMounts"));
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.36f, .37f, .38f);
            if (Camera.main != null) { Undo.RecordObject(Camera.main, "Exterior sky and surface depth precision"); Camera.main.clearFlags = CameraClearFlags.Skybox; Camera.main.nearClipPlane = .15f; }
            EditorSceneManager.MarkSceneDirty(scene); Undo.CollapseUndoOperations(undo);
            return "A placement aligned; " + mounts + " work light mounts; 50 stair supports; 8 equipment supports.";
        }

        static void MovePolish(Transform t, Vector3 world)
        {
            if (t == null) return;
            Undo.RecordObject(t, "Align service asset with current structure"); t.position = world;
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
        }

        public static string AlignVariantExterior()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || (scene.path != VariantBPath && scene.path != VariantCPath))
                throw new InvalidOperationException("B/C 편집 씬이 필요합니다.");
            var background = GameObject.Find(RootName).transform;
            var outside = background.Cast<Transform>().First(t => t.name.StartsWith("08_Reeds"));
            if ((outside.lossyScale - new Vector3(1.25f, 1, 1.25f)).sqrMagnitude < .0001f) return "Exterior already aligned.";
            if ((outside.lossyScale - Vector3.one).sqrMagnitude > .0001f)
                throw new InvalidOperationException("외부 배율이 예상과 다릅니다. 수동 변경을 보존합니다.");
            var props = outside.Cast<Transform>().Where(t => t.name.StartsWith("SM_Grass") || t.name.StartsWith("Label_")).ToArray();
            var sizes = props.Select(t => t.lossyScale).ToArray();
            Undo.RecordObject(outside, "Align original exterior to expanded B/C exit");
            outside.localScale = new Vector3(1.25f / background.lossyScale.x, 1 / background.lossyScale.y, 1.25f / background.lossyScale.z);
            outside.position = new Vector3(0, 0, scene.path == VariantCPath ? 32.5f : 0);
            for (int i = 0; i < props.Length; i++)
            {
                Undo.RecordObject(props[i], "Preserve vegetation and label size");
                props[i].localScale = new Vector3(sizes[i].x / 1.25f, sizes[i].y, sizes[i].z / 1.25f);
                PrefabUtility.RecordPrefabInstancePropertyModifications(props[i]);
            }
            Physics.SyncTransforms(); EditorSceneManager.MarkSceneDirty(scene);
            return "Exterior floor now begins at X210; B/C exit and grass aligned, original grass size preserved.";
        }

        public static int PolishExteriorVegetation()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || !new[] { ScenePath, VariantBPath, VariantCPath }.Contains(scene.path))
                throw new InvalidOperationException("탈출로 편집 씬이 필요합니다.");
            var outside = GameObject.Find(RootName).transform.Cast<Transform>().First(t => t.name.StartsWith("08_Reeds"));
            int count = 0;
            foreach (var t in outside.Cast<Transform>().Where(t => t.name == "갈대 군락 (그레이박스)" && t.gameObject.activeSelf))
            {
                Undo.RecordObject(t.gameObject, "Real vegetation replaces old reed blockout");
                t.gameObject.SetActive(false); count++;
            }
            if (count > 0) EditorSceneManager.MarkSceneDirty(scene);
            return count;
        }

        // A work-area placeholder was removed after B's first surface pass. Restore the
        // continuous slab it used to cover; its original mesh asset and collider are unchanged.
        public static int RestoreVariantWorkroomFloor()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || (scene.path != VariantBPath && scene.path != VariantCPath))
                throw new InvalidOperationException("B/C 편집 씬이 필요합니다.");
            var background = GameObject.Find(RootName).transform;
            var shell = background.Find("20_VariantStructureFinish/01_ContinuousConcreteShell");
            Vector3 covered = new Vector3(195, -.1f, scene.path == VariantBPath ? 17 : 49.5f);
            int restored = 0;
            foreach (var f in shell.GetComponentsInChildren<MeshFilter>().Where(f => f.name == "SM_ConcreteFloor"))
            {
                var collider = f.GetComponent<BoxCollider>();
                if (collider == null || !collider.bounds.Contains(covered) || !f.sharedMesh.name.StartsWith("polished-")) continue;
                var size = f.transform.localScale;
                string key = string.Join("x", new[] { size.x, size.y, size.z }.Select(n => n.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)));
                var original = AssetDatabase.LoadAssetAtPath<Mesh>(BrutalMeshes + "/block-" + key + ".asset");
                if (original == null) throw new InvalidOperationException("원본 반출실 바닥 메시가 없습니다.");
                Undo.RecordObject(f, "Restore floor exposed by removed work slab"); f.sharedMesh = original; restored++;
            }
            if (restored > 0) EditorSceneManager.MarkSceneDirty(scene);
            return restored;
        }

        static int MountExistingLights(Transform source, Transform output)
        {
            int count = 0;
            foreach (var light in source.GetComponentsInChildren<Light>())
            {
                if (light.type == LightType.Directional || light.transform.IsChildOf(output)) continue;
                var at = light.transform.position + Vector3.up * .28f;
                RaycastHit? nearest = null;
                foreach (var direction in new[] { Vector3.left, Vector3.right, Vector3.forward, Vector3.back, Vector3.up })
                {
                    foreach (var hit in Physics.RaycastAll(at, direction, 6, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (!hit.collider.transform.IsChildOf(GameObject.Find(RootName).transform) || hit.distance < .15f) continue;
                        if (nearest == null || hit.distance < nearest.Value.distance) nearest = hit;
                    }
                }
                if (nearest.HasValue)
                {
                    var end = nearest.Value.point;
                    VerticalCable(output, "시설 작업등 고정 암", at, end, .065f, bSteel);
                    BMass(output, "작업등 앵커", end, Vector3.one * .14f, bSteel, false);
                    count++;
                }
            }
            return count;
        }

        // A flat decal must be supported across its footprint; do not let it draw across a chasm or stair riser.
        public static string FitSurfaceDressing()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || !scene.path.StartsWith("Assets/Scenes/Levels/escape_route"))
                throw new InvalidOperationException("탈출로 편집 씬이 필요합니다.");
            Physics.SyncTransforms(); int fitted = 0, hidden = 0;
            var background = GameObject.Find(RootName);
            foreach (var r in background.GetComponentsInChildren<MeshRenderer>())
            {
                if (r.sharedMaterial == null || r.sharedMaterial.shader.name != "CurtainCall/Escape/SurfaceWear" ||
                    Mathf.Abs(Vector3.Dot(r.transform.forward, Vector3.up)) < .99f) continue;
                var t = r.transform;
                bool Supported(Vector3 p) => Physics.Raycast(p + Vector3.up * .12f, Vector3.down, out var hit, .25f, ~0, QueryTriggerInteraction.Ignore)
                    && Vector3.Dot(hit.normal, Vector3.up) > .95f && p.y - hit.point.y > .002f;
                if (!Supported(t.position)) { Undo.RecordObject(r, "Hide unsupported old floor wear"); r.enabled = false; hidden++; continue; }
                bool fits = false;
                for (int n = 0; n < 9; n++)
                {
                    fits = new[] { new Vector3(-.49f,-.49f,0), new Vector3(-.49f,.49f,0), new Vector3(.49f,-.49f,0), new Vector3(.49f,.49f,0), new Vector3(0,.49f,0), new Vector3(0,-.49f,0) }.All(p => Supported(t.TransformPoint(p)));
                    if (fits) break;
                    Undo.RecordObject(t, "Keep wear inside supported floor"); t.localScale = new Vector3(t.localScale.x * .85f, t.localScale.y * .85f, t.localScale.z); fitted++;
                }
                if (!fits) { Undo.RecordObject(r, "Hide unsupported floor wear"); r.enabled = false; hidden++; }
            }
            EditorSceneManager.MarkSceneDirty(scene);
            return "Wear adjustments=" + fitted + "; unsupported wear hidden=" + hidden + ".";
        }
    }
}
