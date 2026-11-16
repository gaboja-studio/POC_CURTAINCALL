using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CurtainCall.Settings;

namespace CurtainCall.EscapeRoute.EditorTools
{
    [Category("EscapeCablePolish")]
    public sealed class EscapeCablePolishTests
    {
        const string ScenePath = "Assets/Scenes/Levels/escape_route.unity";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
        static void WithScene(Action<EscapeCuttableCable[], EscapeExitSaw[]> test)
        {
            var scene = SceneManager.GetSceneByPath(ScenePath); bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects(); Physics.SyncTransforms();
                test(roots.SelectMany(r => r.GetComponentsInChildren<EscapeCuttableCable>()).ToArray(),
                    roots.SelectMany(r => r.GetComponentsInChildren<EscapeExitSaw>()).ToArray());
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        static Vector3 AtHeight(EscapeCuttableCable cable, float height)
        {
            var path = Field<Vector3[]>(cable, "path");
            var best = cable.transform.TransformPoint(path[0]);
            for (int i = 1; i < path.Length; i++)
            {
                var a = cable.transform.TransformPoint(path[i - 1]); var b = cable.transform.TransformPoint(path[i]);
                var p = Vector3.Lerp(a, b, Mathf.Abs(b.y - a.y) > .0001f ? Mathf.Clamp01((height - a.y) / (b.y - a.y)) : 0);
                if (Mathf.Abs(p.y - height) < Mathf.Abs(best.y - height)) best = p;
            }
            return best;
        }

        [Test]
        public void EveryCircuitHasARealBladePathFromSupportedFloorWithinTheCommonJumpHeight()
        {
            WithScene((cables, saws) =>
            {
                Assert.That(cables.Length, Is.EqualTo(24)); var saw = saws[0]; int jumpOnly = 0;
                float height = Field<Transform>(saw, "muzzle").position.y - saw.CarryRoot.position.y;
                foreach (var cable in cables)
                {
                    bool Reach(float rise)
                    {
                        var target = AtHeight(cable, 25 + height + rise);
                        var origin = new Vector3(target.x, 25 + height + rise, 40);
                        Assert.IsTrue(Physics.Raycast(new Vector3(origin.x, 25.2f, 40), Vector3.down, .4f, ~0,
                            QueryTriggerInteraction.Ignore), "Firing position has no real floor: " + cable.name);
                        var direction = target.z > 40 ? Vector3.forward : Vector3.back;
                        object[] args = { origin, direction, Mathf.Abs(target.z - 40) + 1, 0f };
                        typeof(EscapeExitSaw).GetMethod("ObstructionDistance", Private).Invoke(saw, args);
                        var end = origin + direction * (float)args[3];
                        return cable.TryIntersectDisk(origin, end, Vector3.up, Field<float>(saw, "projectileRadius"),
                            Field<float>(saw, "projectileHalfThickness"), out _, out _);
                    }
                    if (!Reach(0)) jumpOnly++;
                    Assert.IsTrue(Reach(GameSettings.Base.Character.JumpHeight), cable.transform.parent.name + "/" + cable.name);
                }
                Assert.That(jumpOnly, Is.EqualTo(4), "Twenty ordinary and four raised circuits are intentionally authored.");
                for (float x = -566; x >= -594; x -= .5f)
                {
                    Assert.IsTrue(Physics.Raycast(new Vector3(x, 25.2f, 40), Vector3.down, .4f, ~0,
                        QueryTriggerInteraction.Ignore), $"Missing approach floor at {x}.");
                    Assert.IsFalse(Physics.CheckCapsule(new Vector3(x, 25.31f, 40), new Vector3(x, 26.2f, 40),
                        .29f, ~0, QueryTriggerInteraction.Ignore), $"Blocked approach at {x}.");
                }
            });
        }

        [Test]
        public void AllFourLaunchersStillReferenceTheSameTwentyFourRequiredCircuits()
        {
            WithScene((cables, saws) =>
            {
                Assert.That(saws.Length, Is.EqualTo(4)); Assert.That(cables.Length, Is.EqualTo(24));
                foreach (var saw in saws)
                {
                    var references = Field<EscapeCuttableCable[]>(saw, "cables");
                    CollectionAssert.AreEquivalent(cables, references); Assert.That(references.Distinct().Count(), Is.EqualTo(24));
                    Assert.That(saw.RequiredCableCount, Is.EqualTo(24));
                }
            });
        }

        [Test]
        public void CurvesAreSeparatedAndTheirFreeSpanDoesNotPassThroughScenery()
        {
            WithScene((cables, _) =>
            {
                foreach (var side in cables.GroupBy(c => c.GetComponent<Renderer>().bounds.center.z > 40))
                {
                    var ordered = side.OrderBy(c => c.GetComponent<Renderer>().bounds.center.x).ToArray();
                    Assert.That(ordered.Length, Is.EqualTo(12));
                    for (int i = 1; i < ordered.Length; i++)
                        Assert.That(ordered[i].GetComponent<Renderer>().bounds.min.x - ordered[i - 1].GetComponent<Renderer>().bounds.max.x,
                            Is.GreaterThan(.6f), "Strands must remain visually separate along each wall.");
                }
                foreach (var cable in cables)
                {
                    var path = Field<Vector3[]>(cable, "path");
                    var world = path.Select(p => cable.transform.TransformPoint(p)).ToArray();
                    for (int i = 1; i < world.Length - 1; i++)
                    {
                        if (Vector3.Distance(world[i], world[0]) < .35f || Vector3.Distance(world[i], world.Last()) < .35f) continue;
                        var hits = Physics.OverlapSphere(world[i], .09f, ~0, QueryTriggerInteraction.Ignore)
                            .Where(c => c.GetComponentInParent<EscapeCuttableCable>() == null).ToArray();
                        Assert.That(hits.Length, Is.Zero, cable.transform.parent.name + "/" + cable.name + ": " + string.Join(",", hits.Select(h => h.name)));
                    }
                    foreach (var end in new[] { world[0], world.Last() })
                    {
                        var direction = end.z > 40 ? Vector3.forward : Vector3.back;
                        var wall = Physics.RaycastAll(end - direction * .1f, direction, .9f, ~0, QueryTriggerInteraction.Ignore)
                            .Any(h => h.collider.GetComponentInParent<EscapeExitSaw>() == null && h.collider.bounds.size.y > 2.5f);
                        Assert.IsTrue(wall, "Terminal has no nearby wall attachment: " + cable.name);
                    }
                }
            });
        }

        [Test]
        public void SavedCutEffectsAreBoundedAndEveryCircuitHasItsOwnStatusLens()
        {
            WithScene((cables, _) =>
            {
                foreach (var cable in cables)
                {
                    var sparks = Field<ParticleSystem>(cable, "cutSparks"); var flash = Field<Light>(cable, "cutFlash");
                    Assert.IsNotNull(sparks); Assert.IsNotNull(flash); Assert.IsFalse(flash.enabled);
                    Assert.IsFalse(sparks.main.playOnAwake); Assert.IsFalse(sparks.main.loop);
                    Assert.AreEqual(ParticleSystemSimulationSpace.World, sparks.main.simulationSpace);
                    Assert.That(sparks.main.maxParticles, Is.LessThanOrEqualTo(64));
                    Assert.That(sparks.emission.burstCount, Is.EqualTo(1));
                    Assert.That(sparks.emission.rateOverTime.constantMax, Is.Zero);
                    Assert.IsNotNull(sparks.GetComponent<Renderer>().sharedMaterial);
                    Assert.IsNotNull(Field<AudioSource>(cable, "cutAudio").clip);
                    var pilots = cable.gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EscapeBankPilot>())
                        .Where(p => Field<EscapeCuttableCable[]>(p, "strands").Contains(cable)).ToArray();
                    Assert.That(pilots.Length, Is.EqualTo(1));
                    Assert.That(Field<EscapeCuttableCable[]>(pilots[0], "strands").Length, Is.EqualTo(1));
                    Assert.IsNotNull(Field<Renderer>(pilots[0], "litLens"));
                }
            });
        }
    }
}
