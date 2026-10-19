using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.Tests.HumanoidValidation
{
    public sealed class SequentialValidationTests
    {
        const string Root = "Assets/_ThirdParty/Task062/SequentialValidation/";
        static readonly string[] LimbNames = { "LeftLeg", "RightLeg", "LeftArm", "RightArm" };
        static readonly int[] Masks = { 0, 4, 12, 13, 15 };
        GameObject instance;
        GameObject floor;
        bool previousBackground;
        bool changedBackground;

        // 기존 테스트 asmdef는 런타임 어셈블리를 참조하지 않으므로 공개 계약만 리플렉션으로 호출한다.
        sealed class Validation
        {
            readonly Type type;
            readonly Component component;
            readonly Type conditionType;
            readonly Component condition;
            readonly Type bodyPartType;

            public Validation(GameObject target)
            {
                type = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("CurtainCall.Player.Validation.SequentialCutValidation"))
                    .FirstOrDefault(t => t != null);
                Assert.That(type, Is.Not.Null, "SequentialCutValidation 런타임을 먼저 컴파일해야 합니다.");
                component = target.GetComponent(type);
                Assert.That(component, Is.Not.Null);
                conditionType = type.Assembly.GetType("CurtainCall.Player.PlayerCondition");
                bodyPartType = type.Assembly.GetType("CurtainCall.Player.BodyPart");
                Assert.That(conditionType, Is.Not.Null);
                Assert.That(bodyPartType, Is.Not.Null);
                condition = target.GetComponent(conditionType);
                Assert.That(condition, Is.Not.Null);
            }

            public T Field<T>(string name) => (T)type.GetField(name).GetValue(component);
            public void SetField(string name, object value) => type.GetField(name).SetValue(component, value);
            public T Property<T>(string name) => (T)type.GetProperty(name).GetValue(component);
            public object Call(string name, params object[] args) => type.GetMethod(name).Invoke(component, args);
            public int Stage => Property<int>("Stage");
            public int PieceCount => Property<int>("PieceCount");
            public Animator Animator => Field<Animator>("Animator");
            public string Failure => Field<string>("Failure");
            public Rigidbody Piece(int index) => (Rigidbody)Call("GetPieceBody", index);
            public void SetMask(int mask) => conditionType.GetMethod("SetLostParts")
                .Invoke(condition, new[] { Enum.ToObject(bodyPartType, mask) });
            public void SetEnabled(bool enabled) => ((Behaviour)component).enabled = enabled;
            public int Mask => Convert.ToInt32(conditionType.GetProperty("LostParts").GetValue(condition));
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            // 어설션 실패 시에도 독립 파편을 지우고 Edit Mode로 돌아간다.
            if (instance != null) Object.DestroyImmediate(instance);
            if (floor != null) Object.DestroyImmediate(floor);
            instance = null;
            floor = null;
            if (changedBackground) Application.runInBackground = previousBackground;
            changedBackground = false;
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        Validation InstantiateValidation()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Validation.prefab");
            Assert.That(prefab, Is.Not.Null, "순차 검증 빌더로 프리팹을 먼저 생성해야 합니다.");
            // 필드를 사후 AddComponent로 넣지 않는다. OnEnable은 직렬화된 프리팹 참조를 사용한다.
            instance = Object.Instantiate(prefab);
            instance.transform.position = new Vector3(20f, 0f, 0f);
            var validation = new Validation(instance);
            Assert.That(string.IsNullOrEmpty(validation.Failure), Is.True, validation.Failure);
            return validation;
        }

        static void AssertStage(Validation validation, int stage, int pieces)
        {
            Assert.That(validation.Stage, Is.EqualTo(stage));
            Assert.That(validation.Mask, Is.EqualTo(Masks[stage]));
            Assert.That(validation.PieceCount, Is.EqualTo(pieces));
            Assert.That(validation.Property<bool>("IsDead"), Is.EqualTo(stage == 4));
            Assert.That(string.IsNullOrEmpty(validation.Failure), Is.True, validation.Failure);
            Assert.That(validation.Field<SkinnedMeshRenderer>("Original").enabled, Is.EqualTo(stage == 0));
            var bodies = validation.Field<SkinnedMeshRenderer[]>("Bodies");
            var limbs = validation.Field<SkinnedMeshRenderer[]>("Limbs");
            Assert.That(bodies.Length, Is.EqualTo(4));
            Assert.That(limbs.Length, Is.EqualTo(4));
            for (int i = 0; i < 4; i++)
            {
                Assert.That(bodies[i].enabled, Is.EqualTo(stage == i + 1), "몸통 단계 " + i);
                Assert.That(limbs[i].enabled, Is.False, "스킨 파편은 직접 표시하면 안 됩니다.");
            }
        }

        [Test]
        public void CumulativeMeshesKeepWeightsBindposesAndEveryBodyCap()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Validation.prefab");
            Assert.That(prefab, Is.Not.Null, "순차 검증 에셋을 먼저 생성해야 합니다.");
            var validation = new Validation(prefab);
            var original = validation.Field<SkinnedMeshRenderer>("Original");
            var bodies = validation.Field<SkinnedMeshRenderer[]>("Bodies");
            var limbs = validation.Field<SkinnedMeshRenderer[]>("Limbs");
            Assert.That(original.sharedMesh.subMeshCount, Is.EqualTo(1));
            Assert.That(bodies.Length, Is.EqualTo(4));
            Assert.That(limbs.Length, Is.EqualTo(4));
            for (int stage = 0; stage < 4; stage++)
            {
                foreach (bool isBody in new[] { true, false })
                {
                    string name = isBody ? "Body" + (stage + 1) : LimbNames[stage];
                    Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Root + name + ".asset");
                    Assert.That(mesh, Is.Not.Null, name);
                    Assert.That(mesh.isReadable, Is.True, name);
                    Assert.That(mesh.vertexCount, Is.GreaterThan(0), name);
                    Assert.That(mesh.subMeshCount, Is.EqualTo(stage + 2), name);
                    Assert.That(mesh.GetTriangles(stage + 1).Length, Is.GreaterThan(0), "마지막 절단면: " + name);
                    if (isBody)
                        for (int cap = 1; cap <= stage + 1; cap++)
                            Assert.That(mesh.GetTriangles(cap).Length, Is.GreaterThan(0), "누적 절단면 보존: " + name);
                    CollectionAssert.AreEqual(original.sharedMesh.bindposes, mesh.bindposes, name);
                    Assert.That(mesh.boneWeights.Length, Is.EqualTo(mesh.vertexCount), name);
                    foreach (BoneWeight weight in mesh.boneWeights)
                    {
                        float[] values = { weight.weight0, weight.weight1, weight.weight2, weight.weight3 };
                        Assert.That(values.All(w => float.IsFinite(w) && w >= 0f), Is.True, name);
                        Assert.That(values.Sum(), Is.EqualTo(1f).Within(0.0001f), name);
                        foreach (int index in new[] { weight.boneIndex0, weight.boneIndex1, weight.boneIndex2, weight.boneIndex3 })
                            Assert.That(index, Is.InRange(0, mesh.bindposes.Length - 1), name);
                    }
                    foreach (Vector3 vertex in mesh.vertices)
                        Assert.That(float.IsFinite(vertex.x) && float.IsFinite(vertex.y) && float.IsFinite(vertex.z), Is.True, name);
                    foreach (int index in mesh.triangles) Assert.That(index, Is.InRange(0, mesh.vertexCount - 1), name);
                    var renderer = isBody ? bodies[stage] : limbs[stage];
                    Assert.That(renderer.sharedMesh, Is.SameAs(mesh));
                    CollectionAssert.AreEqual(original.bones, renderer.bones, name);
                    Assert.That(renderer.rootBone, Is.SameAs(original.rootBone), name);
                    Assert.That(renderer.sharedMaterials.Length, Is.EqualTo(mesh.subMeshCount), name);
                    Assert.That(renderer.sharedMaterials[0], Is.SameAs(original.sharedMaterials[0]), name);
                    for (int cap = 1; cap < mesh.subMeshCount; cap++)
                    {
                        var material = renderer.sharedMaterials[cap];
                        Assert.That(material, Is.Not.Null, name);
                        Assert.That(material, Is.SameAs(renderer.sharedMaterials[mesh.subMeshCount - 1]), name);
                        Assert.That(material.color.r, Is.GreaterThan(material.color.g), name);
                        Assert.That(material.color.r, Is.GreaterThan(material.color.b), name);
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator EveryCutKeepsEarlierPiecesLaunchesFallsLandsAndRestoresInReverse()
        {
            yield return new EnterPlayMode();
            previousBackground = Application.runInBackground;
            changedBackground = true;
            Application.runInBackground = true;
            Validation validation = InstantiateValidation();
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Sequential test floor";
            floor.transform.position = new Vector3(20f, -0.05f, 0f);
            floor.transform.localScale = new Vector3(20f, 0.1f, 20f);
            Physics.SyncTransforms();
            AssertStage(validation, 0, 0);
            Vector3 rootPosition = instance.transform.position;
            Quaternion rootRotation = instance.transform.rotation;
            var earlier = new List<Rigidbody>();
            for (int stage = 1; stage <= 4; stage++)
            {
                var source = validation.Field<SkinnedMeshRenderer[]>("Limbs")[stage - 1];
                var expected = new Mesh();
                try
                {
                    source.BakeMesh(expected, false);
                    validation.Call("CutNext");
                    AssertStage(validation, stage, stage);
                    Assert.That(validation.Property<float>("LastPoseError"), Is.LessThanOrEqualTo(0.00001f));
                    Rigidbody piece = validation.Piece(stage - 1);
                    Assert.That(piece, Is.Not.Null);
                    Assert.That(earlier.Contains(piece), Is.False);
                    Assert.That(piece.transform.parent, Is.Null);
                    Assert.That(piece.GetComponentsInChildren<Joint>(), Is.Empty);
                    Assert.That(piece.GetComponentsInChildren<SkinnedMeshRenderer>(), Is.Empty);
                    Assert.That(piece.GetComponents<Rigidbody>().Length, Is.EqualTo(1));
                    Assert.That(piece.useGravity, Is.True);
                    Assert.That(piece.isKinematic, Is.False);
                    Assert.That(piece.collisionDetectionMode, Is.EqualTo(CollisionDetectionMode.ContinuousDynamic));
                    Assert.That(piece.interpolation, Is.EqualTo(RigidbodyInterpolation.Interpolate));
                    var meshFilter = piece.GetComponent<MeshFilter>();
                    Assert.That(meshFilter, Is.Not.Null);
                    var vertices = meshFilter.sharedMesh.vertices;
                    var expectedVertices = expected.vertices;
                    Assert.That(vertices.Length, Is.EqualTo(expectedVertices.Length));
                    for (int i = 0; i < vertices.Length; i++)
                        Assert.That(Vector3.Distance(source.transform.TransformPoint(expectedVertices[i]), piece.transform.TransformPoint(vertices[i])),
                            Is.LessThanOrEqualTo(0.00001f), "절단 즉시 월드 포즈: 단계 " + stage);
                    for (int i = 0; i < earlier.Count; i++)
                        Assert.That(validation.Piece(i), Is.SameAs(earlier[i]), "기존 파편을 다시 만들면 안 됩니다.");
                    earlier.Add(piece);
                    Vector3 start = piece.worldCenterOfMass;
                    float peak = start.y;
                    bool rose = false;
                    bool fell = false;
                    float started = Time.fixedTime;
                    while (Time.fixedTime - started < 3f)
                    {
                        yield return new WaitForFixedUpdate();
                        Assert.That(piece, Is.Not.Null);
                        peak = Mathf.Max(peak, piece.worldCenterOfMass.y);
                        rose |= piece.linearVelocity.y > 1f;
                        fell |= piece.linearVelocity.y < -1f;
                    }
                    Assert.That(rose && fell, Is.True, "발사와 중력 낙하: 단계 " + stage);
                    Assert.That(peak - start.y, Is.GreaterThan(0.1f));
                    Assert.That(Vector3.ProjectOnPlane(piece.worldCenterOfMass - start, Vector3.up).magnitude, Is.GreaterThan(0.5f));
                    Assert.That(piece.worldCenterOfMass.y, Is.LessThan(peak - 0.1f));
                    Assert.That(piece.GetComponent<BoxCollider>().bounds.min.y,
                        Is.EqualTo(floor.GetComponent<Collider>().bounds.max.y).Within(0.1f), "바닥 착지: 단계 " + stage);
                    Assert.That(Vector3.Distance(instance.transform.position, rootPosition), Is.LessThan(0.00001f), "본체는 발사하면 안 됩니다.");
                    Assert.That(Quaternion.Angle(instance.transform.rotation, rootRotation), Is.LessThan(0.001f));
                    for (int i = 0; i < earlier.Count; i++) Assert.That(validation.Piece(i), Is.SameAs(earlier[i]));
                }
                finally { Object.DestroyImmediate(expected); }
            }
            for (int stage = 3; stage >= 0; stage--)
            {
                validation.Call("RestorePrevious");
                AssertStage(validation, stage, stage);
                for (int i = 0; i < stage; i++) Assert.That(validation.Piece(i), Is.SameAs(earlier[i]));
                for (int i = stage; i < 4; i++) Assert.That(validation.Piece(i), Is.Null);
                yield return null;
                Assert.That(earlier[stage] == null, Is.True, "복구한 파편 오브젝트를 제거해야 합니다.");
            }
            validation.Call("RestorePrevious");
            validation.Call("RestoreAll");
            AssertStage(validation, 0, 0);
        }

        [UnityTest]
        public IEnumerator InitialLossAndReenableSkipHistoryAndBatchLossCreatesExactlyFourPieces()
        {
            yield return new EnterPlayMode();
            Validation validation = InstantiateValidation();
            instance.SetActive(false);
            validation.SetMask(12);
            instance.SetActive(true);
            AssertStage(validation, 2, 0);
            validation.SetEnabled(false);
            validation.SetEnabled(true);
            AssertStage(validation, 2, 0);
            validation.Call("CutNext");
            AssertStage(validation, 3, 1);
            Assert.That(validation.Piece(0), Is.Null);
            Assert.That(validation.Piece(1), Is.Null);
            Assert.That(validation.Piece(2), Is.Not.Null);
            instance.SetActive(false);
            instance.SetActive(true);
            AssertStage(validation, 3, 0);
            for (int i = 0; i < 4; i++) Assert.That(validation.Piece(i), Is.Null);
            validation.Call("RestoreAll");
            AssertStage(validation, 0, 0);
            validation.SetMask(15);
            AssertStage(validation, 4, 4);
            var pieces = Enumerable.Range(0, 4).Select(validation.Piece).ToArray();
            Assert.That(pieces.All(p => p != null), Is.True);
            Assert.That(pieces.Select(p => p.GetInstanceID()).Distinct().Count(), Is.EqualTo(4));
            validation.SetMask(15);
            validation.Call("CutNext");
            AssertStage(validation, 4, 4);
            for (int i = 0; i < 4; i++) Assert.That(validation.Piece(i), Is.SameAs(pieces[i]));
            validation.Call("RestoreAll");
            AssertStage(validation, 0, 0);
            yield return null;
            Assert.That(pieces.All(p => p == null), Is.True);
        }

        [UnityTest]
        public IEnumerator MissingInjuryMotionsNeverFallbackAndDeathDisallowsEveryMotion()
        {
            yield return new EnterPlayMode();
            Validation validation = InstantiateValidation();
            // 실제 클립 보유 여부와 무관하게 누락 계약을 검증한다. 프리팹 에셋은 바꾸지 않는다.
            foreach (string field in new[] { "IdleClips", "MoveClips", "LowMoveClips", "JumpClips" })
                validation.SetField(field, new AnimationClip[5]);
            for (int stage = 1; stage <= 3; stage++)
            {
                validation.Call("CutNext");
                AssertStage(validation, stage, stage);
                for (int motion = 0; motion < 4; motion++)
                {
                    Assert.That((bool)validation.Call("SelectMotion", motion), Is.False);
                    Assert.That(validation.Property<string>("MotionStatus"), Does.Contain("MISSING"));
                    Assert.That(validation.Animator.speed, Is.Zero);
                    Assert.That(validation.Animator.applyRootMotion, Is.False);
                }
            }
            validation.Call("CutNext");
            AssertStage(validation, 4, 4);
            for (int motion = 0; motion < 4; motion++)
            {
                Assert.That((bool)validation.Call("SelectMotion", motion), Is.False);
                Assert.That(validation.Property<string>("MotionStatus"), Does.Contain("Dead"));
                Assert.That(validation.Animator.speed, Is.Zero);
            }
            validation.Call("RestoreAll");
            AssertStage(validation, 0, 0);
            Assert.That((bool)validation.Call("SelectMotion", 0), Is.True);
            Assert.That(validation.Animator.speed, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator UnsupportedMaskReportsFailureAndRestoreRecovers()
        {
            yield return new EnterPlayMode();
            Validation validation = InstantiateValidation();
            LogAssert.Expect(LogType.Error, new Regex("ERROR: Unsupported non-sequential mask:"));
            validation.SetMask(8);
            Assert.That(validation.Failure, Does.Contain("Unsupported non-sequential mask"));
            Assert.That(validation.PieceCount, Is.Zero);
            Assert.That(validation.Stage, Is.Zero);
            for (int motion = 0; motion < 4; motion++)
                Assert.That((bool)validation.Call("SelectMotion", motion), Is.False);
            validation.Call("CutNext");
            Assert.That(validation.PieceCount, Is.Zero);
            Assert.That(validation.Mask, Is.EqualTo(8));
            validation.Call("RestoreAll");
            AssertStage(validation, 0, 0);
            Assert.That((bool)validation.Call("SelectMotion", 0), Is.True);
            validation.Call("CutNext");
            AssertStage(validation, 1, 1);
        }
    }
}
