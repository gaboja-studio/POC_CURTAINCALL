using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    /// <summary>Repositions the existing 24 circuits without replacing launcher references or exit requirements.</summary>
    public static class EscapeCableLayoutPolish
    {
        [MenuItem("Tools/CurtainCall/Escape Route/Polish exit cable placement")]
        public static void ApplyMenu() => Debug.Log(Apply());

        public static string Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != "Assets/Scenes/Levels/escape_route.unity" || scene.isDirty)
                throw new InvalidOperationException("Saved escape scene in EditMode required.");
            var cables = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EscapeCuttableCable>()).ToArray();
            var banks = cables.Select(c => c.transform.parent).Distinct().OrderBy(t => t.name, StringComparer.Ordinal).ToArray();
            if (cables.Length != 24 || banks.Length != 6) throw new InvalidOperationException("Expected the existing six banks / 24 strands.");
            Physics.SyncTransforms();
            // Resolve all attachment faces before changing any scene geometry.
            var top = new Vector3[24]; var bottom = new Vector3[24];
            for (int bank = 0; bank < 6; bank++) for (int strand = 0; strand < 4; strand++)
            {
                int side = bank % 2, column = bank / 2 * 4 + strand, index = bank * 4 + strand;
                float x = -568.5f - column * 2.15f - side * .35f;
                // The recessed bays meet solid returns here; leave room for the full .55 m blade radius.
                if (side == 0 && column == 4) x -= .65f;
                if (side == 1 && column == 9) x += .65f;
                bool raised = column == 2 || column == 8;
                float low = raised ? 1.85f : .5f + (column % 3) * .17f;
                float high = raised ? 4.8f : new[] { 3.6f, 5.1f, 4.2f, 4.7f }[column % 4];
                float slant = (column % 3 - 1) * .24f;
                Vector3 direction = side == 0 ? Vector3.forward : Vector3.back;
                top[index] = Wall(x + slant, 25 + high, direction) - direction * .65f;
                bottom[index] = Wall(x - slant, 25 + low, direction) - direction * .65f;
            }
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Separate exit circuits and strengthen cut feedback");
            var pilots = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EscapeBankPilot>()).ToArray();
            Material pilotMaterial = pilots.Select(p => new SerializedObject(p).FindProperty("litLens").objectReferenceValue as Renderer)
                .Where(r => r != null).Select(r => r.sharedMaterial).FirstOrDefault();
            foreach (var old in pilots.Select(p => p.transform.parent).Distinct()
                .Where(t => t != null && t.name == "출구 · 배선별 분전반 통전창").ToArray())
                Undo.DestroyObjectImmediate(old.gameObject);
            for (int bank = 0; bank < 6; bank++)
            {
                var parent = banks[bank];
                var strands = parent.GetComponentsInChildren<EscapeCuttableCable>().OrderBy(c => c.name, StringComparer.Ordinal).ToArray();
                var terminal = parent.Find("상부 전력 분배함");
                var priorMounts = parent.Find("IndividualCircuitMounts");
                Material metal = terminal != null ? terminal.GetComponent<Renderer>().sharedMaterial
                    : priorMounts.GetComponentInChildren<Renderer>().sharedMaterial;
                foreach (var child in parent.Cast<Transform>().Where(t => !t.TryGetComponent<EscapeCuttableCable>(out _)).ToArray())
                    Undo.DestroyObjectImmediate(child.gameObject);
                var mounts = Child(parent, "IndividualCircuitMounts");
                // Identity world scale avoids the old .58 parent compression of both cables and particles.
                foreach (var strand in strands) Undo.RecordObject(strand.transform, "Preserve cable identity");
                Undo.RecordObject(parent, "Normalize cable bank scale");
                parent.localScale = Vector3.one;
                for (int j = 0; j < 4; j++)
                {
                    int index = bank * 4 + j;
                    var cable = strands[j]; var direction = bank % 2 == 0 ? Vector3.forward : Vector3.back;
                    cable.transform.SetPositionAndRotation(bottom[index], Quaternion.identity);
                    var scale = cable.transform.parent.lossyScale;
                    cable.transform.localScale = new Vector3(1 / scale.x, 1 / scale.y, 1 / scale.z);
                    var data = new SerializedObject(cable);
                    var points = data.FindProperty("localPoints"); points.arraySize = 4;
                    var delta = bottom[index] - top[index];
                    var world = new[] { top[index], top[index] + delta * .3f - direction * .25f,
                        top[index] + delta * .7f - direction * .38f, bottom[index] };
                    for (int k = 0; k < world.Length; k++) points.GetArrayElementAtIndex(k).vector3Value = cable.transform.InverseTransformPoint(world[k]);
                    data.FindProperty("curveSubdivisions").intValue = 10;
                    data.FindProperty("breakGap").floatValue = .9f;
                    data.FindProperty("droopDistance").floatValue = .4f;
                    data.FindProperty("snapSeconds").floatValue = .55f;
                    var sparks = (ParticleSystem)data.FindProperty("cutSparks").objectReferenceValue;
                    ConfigureSparks(sparks);
                    var flashTransform = cable.transform.Find("Cut contact flash");
                    if (flashTransform == null) flashTransform = Child(cable.transform, "Cut contact flash");
                    var flash = flashTransform.GetComponent<Light>();
                    if (flash == null) flash = Undo.AddComponent<Light>(flashTransform.gameObject);
                    Undo.RecordObject(flash, "Brief cut contact light");
                    flash.type = LightType.Point; flash.range = 2.8f; flash.intensity = 4;
                    flash.color = new Color(1f, .7f, .3f); flash.shadows = LightShadows.None; flash.enabled = false;
                    data.FindProperty("cutFlash").objectReferenceValue = flash;
                    data.ApplyModifiedProperties(); cable.Rebuild();
                    var mount = Child(mounts, $"{j + 1:00}_CircuitTerminals");
                    foreach (var end in new[] { top[index], bottom[index] })
                    {
                        // Brackets run all the way from the connector to the actual wall face.
                        Box(mount, "Wall bracket", end + direction * .39f, new Vector3(.24f, .12f, .55f), metal, false);
                        Box(mount, "Terminal", end + direction * .12f, new Vector3(.46f, .28f, .32f), metal, true);
                    }
                    var lens = Box(mount, "Live circuit indicator", bottom[index] + Vector3.down * .06f - direction * .047f,
                        new Vector3(.2f, .07f, .025f), pilotMaterial != null ? pilotMaterial
                            : data.FindProperty("insulationMaterial").objectReferenceValue as Material, false);
                    var pilot = Undo.AddComponent<EscapeBankPilot>(mount.gameObject); var pilotData = new SerializedObject(pilot);
                    pilotData.FindProperty("strands").arraySize = 1;
                    pilotData.FindProperty("strands").GetArrayElementAtIndex(0).objectReferenceValue = cable;
                    pilotData.FindProperty("litLens").objectReferenceValue = lens;
                    pilotData.ApplyModifiedProperties();
                }
            }
            Physics.SyncTransforms(); EditorSceneManager.MarkSceneDirty(scene); Undo.CollapseUndoOperations(group);
            return "24 existing circuits dispersed across both walls; four raised circuits; individual anchored terminals and cut feedback.";
        }

        static Vector3 Wall(float x, float y, Vector3 direction)
        {
            var origin = new Vector3(x, y, 40);
            var hits = Physics.RaycastAll(origin, direction, 30, ~0, QueryTriggerInteraction.Ignore)
                .Where(h => h.collider.GetComponentInParent<EscapeExitSaw>() == null && h.collider.bounds.size.y > 2.5f
                    && Vector3.Dot(h.normal, direction) < -.7f).OrderBy(h => h.distance).ToArray();
            if (hits.Length == 0) throw new InvalidOperationException($"No real wall attachment at {origin}/{direction}.");
            return hits[0].point;
        }

        static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name); Undo.RegisterCreatedObjectUndo(go, "Cable mounting");
            go.transform.SetParent(parent, false); return go.transform;
        }

        static Renderer Box(Transform parent, string name, Vector3 position, Vector3 size, Material material, bool collision)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); Undo.RegisterCreatedObjectUndo(go, "Cable terminal");
            go.name = name; go.transform.SetPositionAndRotation(position, Quaternion.identity);
            go.transform.localScale = size; go.transform.SetParent(parent, true);
            var view = go.GetComponent<Renderer>(); view.sharedMaterial = material;
            if (!collision) Undo.DestroyObjectImmediate(go.GetComponent<Collider>());
            return view;
        }

        static void ConfigureSparks(ParticleSystem ps)
        {
            if (ps == null) throw new InvalidOperationException("Missing authored cable sparks.");
            Undo.RecordObject(ps, "Cable spark burst"); Undo.RecordObject(ps.GetComponent<ParticleSystemRenderer>(), "Spark streaks");
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.playOnAwake = false; main.loop = false; main.duration = .2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.scalingMode = ParticleSystemScalingMode.Shape;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.18f, .5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 8.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(.018f, .038f); main.gravityModifier = 1.7f; main.maxParticles = 64;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1.8f, 1.25f, .35f), new Color(1.5f, 1.5f, 1.15f));
            var emission = ps.emission; emission.enabled = true; emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0, 42) });
            var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .035f;
            var color = ps.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0),
                new GradientColorKey(new Color(1, .6f, .12f), .5f), new GradientColorKey(new Color(.8f, .2f, .015f), 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, .55f), new GradientAlphaKey(0, 1) });
            color.color = gradient;
            var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = .025f; renderer.lengthScale = 1.5f;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        }
    }
}
