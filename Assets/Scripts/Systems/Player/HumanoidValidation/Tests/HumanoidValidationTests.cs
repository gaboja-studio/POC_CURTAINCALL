using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.Tests.HumanoidValidation
{
    public sealed class HumanoidValidationTests
    {
        const string Root = "Assets/_ThirdParty/Task062/HumanoidValidation/";

        [Test]
        public void GeneratedMeshesHaveWeightsBindposesAndCaps()
        {
            foreach (string name in new[] { "Body.asset", "LeftLeg.asset" })
            {
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Root + name);
                Assert.That(mesh, Is.Not.Null, "Build Task062 assets first");
                Assert.That(mesh.vertexCount, Is.GreaterThan(0));
                Assert.That(mesh.subMeshCount, Is.EqualTo(2));
                Assert.That(mesh.GetTriangles(1).Length, Is.GreaterThan(0));
                Assert.That(mesh.boneWeights.Length, Is.EqualTo(mesh.vertexCount));
                foreach (var w in mesh.boneWeights)
                {
                    Assert.That(w.weight0 + w.weight1 + w.weight2 + w.weight3, Is.EqualTo(1f).Within(0.0001f));
                    foreach (int index in new[] { w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3 })
                        Assert.That(index, Is.InRange(0, mesh.bindposes.Length - 1));
                }
                foreach (var v in mesh.vertices) Assert.That(float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z), Is.True);
                foreach (int index in mesh.triangles) Assert.That(index, Is.InRange(0, mesh.vertexCount - 1));
            }
        }

        [UnityEngine.TestTools.UnityTest]
        public System.Collections.IEnumerator DetachedLegLaunchesFallsAndLandsInEveryState()
        {
            yield return new UnityEngine.TestTools.EnterPlayMode();
            bool background = Application.runInBackground;
            Application.runInBackground = true;
            var instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Validation.prefab"));
            instance.transform.position = new Vector3(20f, 0f, 0f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(20f, -0.08f, 0f);
            floor.transform.localScale = new Vector3(16f, 0.1f, 16f);
            try
            {
                var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("CurtainCall.Player.Validation.HumanoidCutValidation")).First(t => t != null);
                var conditionType = type.Assembly.GetType("CurtainCall.Player.PlayerCondition");
                var bodyPart = type.Assembly.GetType("CurtainCall.Player.BodyPart");
                var component = instance.GetComponent(type);
                var condition = instance.GetComponent(conditionType);
                var animator = (Animator)type.GetField("Animator").GetValue(component);
                for (int state = 0; state < 5; state++)
                {
                    type.GetMethod("SelectState").Invoke(component, new object[] { state });
                    animator.Update(0.2f);
                    var rootPosition = instance.transform.position;
                    conditionType.GetMethod("LosePart").Invoke(condition, new[] { Enum.Parse(bodyPart, "LeftLeg") });
                    var body = (Rigidbody)type.GetProperty("PieceBody").GetValue(component);
                    Assert.That(body, Is.Not.Null);
                    Assert.That(body.transform.parent, Is.Null);
                    Assert.That(body.GetComponentsInChildren<Joint>(), Is.Empty);
                    Assert.That(body.GetComponentsInChildren<SkinnedMeshRenderer>(), Is.Empty);
                    Assert.That(body.useGravity, Is.True);
                    Vector3 start = body.worldCenterOfMass;
                    float peak = start.y;
                    bool rose = false;
                    bool fell = false;
                    float started = Time.fixedTime;
                    while (Time.fixedTime - started < 3f)
                    {
                        yield return new WaitForFixedUpdate();
                        peak = Mathf.Max(peak, body.worldCenterOfMass.y);
                        rose |= body.linearVelocity.y > 1f;
                        fell |= body.linearVelocity.y < -1f;
                    }
                    Assert.That(rose && fell, Is.True, "Launch / gravity: state " + state);
                    Assert.That(peak - start.y, Is.GreaterThan(0.1f));
                    Assert.That(Vector3.ProjectOnPlane(body.worldCenterOfMass - start, Vector3.up).magnitude, Is.GreaterThan(0.5f));
                    Assert.That(body.worldCenterOfMass.y, Is.LessThan(peak - 0.1f));
                    Assert.That(body.GetComponent<BoxCollider>().bounds.min.y, Is.EqualTo(-0.03f).Within(0.08f), "Floor collision: state " + state);
                    Assert.That(instance.transform.position, Is.EqualTo(rootPosition), "Body must not launch");
                    conditionType.GetMethod("RestoreAll").Invoke(condition, null);
                    Assert.That(type.GetProperty("PieceCount").GetValue(component), Is.EqualTo(0));
                    yield return null;
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
                UnityEngine.Object.DestroyImmediate(floor);
                Application.runInBackground = background;
            }
            yield return new UnityEngine.TestTools.ExitPlayMode();
        }

        [UnityEngine.TestTools.UnityTest]
        public System.Collections.IEnumerator InitialLossAndReenableDoNotSpawnHistoricalPieces()
        {
            yield return new UnityEngine.TestTools.EnterPlayMode();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Validation.prefab");
            Assert.That(prefab, Is.Not.Null, "Build Task062 assets first");
            var instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("CurtainCall.Player.Validation.HumanoidCutValidation")).First(t => t != null);
                var conditionType = type.Assembly.GetType("CurtainCall.Player.PlayerCondition");
                var bodyPart = type.Assembly.GetType("CurtainCall.Player.BodyPart");
                var component = instance.GetComponent(type);
                var condition = instance.GetComponent(conditionType);
                var lose = conditionType.GetMethod("LosePart");
                instance.SetActive(false);
                lose.Invoke(condition, new[] { Enum.Parse(bodyPart, "LeftLeg") });
                instance.SetActive(true);
                Assert.That(type.GetProperty("PieceCount").GetValue(component), Is.EqualTo(0));
                Assert.That(((SkinnedMeshRenderer)type.GetField("Body").GetValue(component)).enabled, Is.True);
                instance.SetActive(false);
                instance.SetActive(true);
                Assert.That(type.GetProperty("PieceCount").GetValue(component), Is.EqualTo(0));
                conditionType.GetMethod("RestoreAll").Invoke(condition, null);
                Assert.That(((SkinnedMeshRenderer)type.GetField("Original").GetValue(component)).enabled, Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
            yield return new UnityEngine.TestTools.ExitPlayMode();
        }
    }
}
