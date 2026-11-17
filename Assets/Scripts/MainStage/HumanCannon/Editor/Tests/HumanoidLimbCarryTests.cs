using System;
using System.Reflection;
using System.Linq;
using CurtainCall.Damage;
using CurtainCall.Player;
using CurtainCall.Player.Validation;
using CurtainCall.Settings;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using Range = NUnit.Framework.RangeAttribute;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    [Category("CommonProgressionRegression"), Category("HumanoidLimbCarryRegression")]
    public sealed class HumanoidLimbCarryTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        // Use the production baking path and authored player model. A hand-made marked box hid this bug.
        static void WithPiece(string modelName, int index, Action<GameObject> check)
        {
            GameObject modelRoot = null, pieceRoot = null;
            Mesh baked = null;
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    $"Assets/Resources/Prefabs/Characters/Players/Models/{modelName}/{modelName}.prefab");
                Assert.IsNotNull(prefab);
                modelRoot = Object.Instantiate(prefab);
                modelRoot.transform.position = new Vector3(12000, 100, 12000);
                var model = modelRoot.GetComponent<HumanoidModel>();
                Assert.IsNotNull(model);
                model.Animator.Rebind();
                model.Animator.Update(0f);
                var piece = typeof(HumanoidModel).GetMethod("BakePiece", Private).Invoke(model, new object[] { index, null });
                pieceRoot = (GameObject)piece.GetType().GetField("Object").GetValue(piece);
                baked = (Mesh)piece.GetType().GetField("Mesh").GetValue(piece);
                pieceRoot.transform.position += Vector3.right * 20f;
                pieceRoot.SetActive(true);
                // Non-ExecuteAlways lifecycle callbacks require explicit invocation in EditMode.
                if (pieceRoot.TryGetComponent<DismemberedLimb>(out var marker))
                    typeof(DismemberedLimb).GetMethod("OnEnable", Private).Invoke(marker, null);
                foreach (var body in pieceRoot.GetComponentsInChildren<Rigidbody>()) body.isKinematic = true;
                Physics.SyncTransforms();
                check(pieceRoot);
            }
            finally
            {
                if (pieceRoot != null && pieceRoot.TryGetComponent<DismemberedLimb>(out var marker))
                    typeof(DismemberedLimb).GetMethod("OnDisable", Private).Invoke(marker, null);
                Object.DestroyImmediate(pieceRoot);
                Object.DestroyImmediate(baked);
                Object.DestroyImmediate(modelRoot);
            }
        }

        // The runtime schedules joint and temporary mesh disposal with Destroy.
        // Match the specific diagnostic from invoking those runtime paths in EditMode.
        static void ExpectLegacyDestroyDiagnostic() =>
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(
                "^Destroy may not be called from edit mode! Use DestroyImmediate instead"));

        [Test]
        public void LegacyCutPathsRegisterTheirActualFragments([Values("BlockDoll", "Rabbit/Rabbit")] string path,
            [Range(0, 3)] int limb)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/Resources/Prefabs/Characters/Players/Models/{path}.prefab");
            Assert.IsNotNull(prefab);
            var model = Object.Instantiate(prefab);
            var player = new GameObject("Legacy cut fixture");
            var legacy = player.AddComponent<LimbDismemberment>();
            var pieces = (System.Collections.Generic.List<GameObject>)typeof(LimbDismemberment)
                .GetField("pieces", Private).GetValue(legacy);
            var meshes = (System.Collections.Generic.List<(GameObject piece, Mesh mesh)>)typeof(LimbDismemberment)
                .GetField("createdMeshes", Private).GetValue(legacy);
            try
            {
                if (model.TryGetComponent<HumanoidModel>(out var humanoid))
                {
                    humanoid.Animator.Rebind();
                    humanoid.Animator.Update(0f);
                    var source = humanoid.Limbs[limb];
                    var limbRoot = ModelLimbs.Find(model, PlayerCondition.CutOrder[limb]);
                    Assert.IsNotNull(limbRoot);
                    ExpectLegacyDestroyDiagnostic();
                    typeof(LimbDismemberment).GetMethod("SpawnSkinnedPiece", Private)
                        .Invoke(legacy, new object[] { PlayerCondition.CutOrder[limb],
                            new System.Collections.Generic.List<SkinnedMeshRenderer> { source }, limbRoot });
                }
                else
                {
                    ExpectLegacyDestroyDiagnostic();
                    typeof(LimbDismemberment).GetMethod("Remove", Private)
                        .Invoke(legacy, new object[] { model, PlayerCondition.CutOrder[limb], true });
                }
                var piece = pieces.Single();
                var shapes = piece.GetComponentsInChildren<Collider>();
                Assert.IsNotEmpty(shapes);
                foreach (var shape in shapes) Assert.IsTrue(DismemberedLimb.Contains(shape), shape.name);
            }
            finally
            {
                foreach (var piece in pieces) Object.DestroyImmediate(piece);
                foreach (var (_, mesh) in meshes) Object.DestroyImmediate(mesh);
                pieces.Clear();
                meshes.Clear();
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(model);
            }
        }

        [Test]
        public void SingleCutValidationPrefabRegistersItsFragment()
        {
            const string path = "Assets/_ThirdParty/Task062/HumanoidValidation/Validation.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) Assert.Ignore("Optional private validation prefab is unavailable.");
            var root = Object.Instantiate(prefab);
            var validation = root.GetComponent<HumanoidCutValidation>();
            Assert.IsNotNull(validation);
            try
            {
                ExpectLegacyDestroyDiagnostic();
                typeof(HumanoidCutValidation).GetMethod("Spawn", Private).Invoke(validation, null);
                Assert.IsNotNull(validation.PieceBody);
                Assert.IsTrue(DismemberedLimb.Contains(validation.PieceBody.GetComponent<Collider>()));
            }
            finally
            {
                if (validation.PieceBody != null) Object.DestroyImmediate(validation.PieceBody.gameObject);
                var mesh = typeof(HumanoidCutValidation).GetField("pieceMesh", Private);
                Object.DestroyImmediate((Mesh)mesh.GetValue(validation));
                mesh.SetValue(validation, null);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SequentialValidationPrefabRegistersItsBakedCut([Range(0, 3)] int limb)
        {
            const string path = "Assets/_ThirdParty/Task062/SequentialValidation/Validation.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) Assert.Ignore("Optional private validation prefab is unavailable.");
            var root = Object.Instantiate(prefab);
            GameObject fragment = null;
            Mesh mesh = null;
            try
            {
                var validation = root.GetComponent<SequentialCutValidation>();
                Assert.IsNotNull(validation);
                var piece = typeof(SequentialCutValidation).GetMethod("BakePiece", Private)
                    .Invoke(validation, new object[] { limb });
                fragment = (GameObject)piece.GetType().GetField("Object").GetValue(piece);
                mesh = (Mesh)piece.GetType().GetField("Mesh").GetValue(piece);
                fragment.SetActive(true);
                var collider = fragment.GetComponent<Collider>();
                Assert.IsTrue(DismemberedLimb.Contains(collider));
            }
            finally
            {
                Object.DestroyImmediate(fragment);
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ActualCutDoesNotBlockCarryOrLiftTheLoad([Values("Rabbit", "Bear", "PinkGirl", "BluePierrot")] string modelName, [Range(0, 3)] int limb, [Values(0f, 90f)] float yaw)
        {
            WithPiece(modelName, limb, piece =>
            {
                var root = new GameObject("Trampoline carry probe");
                try
                {
                    root.layer = LayerMask.NameToLayer(GamePhysicsLayers.Trampoline);
                    var body = root.AddComponent<BoxCollider>(); body.size = Vector3.one * .6f;
                    piece.transform.Rotate(Vector3.up, yaw, Space.World);
                    Physics.SyncTransforms();
                    foreach (var shape in piece.GetComponentsInChildren<Collider>())
                    {
                        var target = shape.bounds.center;
                        root.transform.position = target - Vector3.forward * 2f;
                        Physics.SyncTransforms();
                        Assert.IsTrue(Physics.ComputePenetration(body, target, Quaternion.identity, shape,
                            shape.transform.position, shape.transform.rotation, out _, out var depth) && depth > .02f,
                            "Fixture must intersect the real severed shape, not miss it.");
                        Assert.IsTrue(HumanCannonCarry.CanMove(root.transform, body, target, Quaternion.identity, null),
                            $"Actual limb {limb}/{shape.name} blocked the flat carry path, causing step-up or rollback.");
                    }
                }
                finally { Object.DestroyImmediate(root); }
            });
        }

        [Test]
        public void ActualCutPassesTrampolineButKeepsWorldCollision([Values("Rabbit", "Bear", "PinkGirl", "BluePierrot")] string modelName, [Range(0, 3)] int limb, [Values(false, true)] bool reenable)
        {
            WithPiece(modelName, limb, piece =>
            {
                var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
                try
                {
                    var physics = scene.GetPhysicsScene();
                    Assert.AreNotEqual(Physics.defaultPhysicsScene, physics);
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(piece, scene);
                    var shapes = piece.GetComponentsInChildren<Collider>();
                    Assert.That(shapes.Length, Is.EqualTo(2));
                    var bounds = shapes[0].bounds; bounds.Encapsulate(shapes[1].bounds);
                    piece.transform.position += Vector3.up * 3 - bounds.center;
                    if (reenable) { piece.SetActive(false); piece.SetActive(true); }
                    var deck = new GameObject("Trampoline frame");
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(deck, scene);
                    deck.layer = LayerMask.NameToLayer(GamePhysicsLayers.Trampoline);
                    deck.AddComponent<BoxCollider>().size = new Vector3(10, .4f, 10);
                    var floor = new GameObject("World floor");
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(floor, scene);
                    floor.transform.position = Vector3.down * 2f;
                    floor.AddComponent<BoxCollider>().size = new Vector3(10, .5f, 10);
                    foreach (var shape in shapes)
                    {
                        Assert.IsTrue(DismemberedLimb.Contains(shape), "Actual model generation must register each child collider.");
                        Assert.IsTrue(Physics.GetIgnoreLayerCollision(deck.layer, shape.gameObject.layer));
                        Assert.IsFalse(Physics.GetIgnoreLayerCollision(floor.layer, shape.gameObject.layer));
                    }
                    foreach (var body in piece.GetComponentsInChildren<Rigidbody>())
                    { body.isKinematic = false; body.useGravity = true; }
                    Physics.SyncTransforms();
                    for (int i = 0; i < 150; i++) physics.Simulate(.02f);
                    foreach (var shape in shapes)
                    {
                        Assert.That(shape.bounds.max.y, Is.LessThan(-.3f), "Must fall through trampoline, not rest on it.");
                        Assert.That(shape.bounds.min.y, Is.GreaterThan(-2.05f), "Solid floor must still catch the limb.");
                    }
                    // Let the common fixture own piece cleanup after closing the isolated physics scene.
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(piece,
                        UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                }
                finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
            });
        }

        [Test]
        public void SavedHumanCannonTrampolineCrossesActualCutButStopsAtSolidWall([Values("Rabbit", "Bear", "PinkGirl", "BluePierrot")] string modelName, [Range(0, 3)] int limb)
        {
            WithPiece(modelName, limb, piece =>
            {
                GameObject root;
                var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(
                    "Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity");
                try
                {
                    var saved = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<HumanCannonTrampoline>(true)).Single();
                    root = Object.Instantiate(saved.gameObject);
                    root.transform.localScale = saved.transform.lossyScale;
                    SceneManager.MoveGameObjectToScene(root, SceneManager.GetActiveScene());
                }
                finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
                var wall = new GameObject("Solid carry obstruction");
                try
                {
                    var trampoline = root.GetComponent<HumanCannonTrampoline>();
                    Assert.IsNotNull(trampoline);
                    trampoline.enabled = false;
                    typeof(HumanCannonTrampoline).GetMethod("RefreshCarryBody", Private).Invoke(trampoline, new object[] { null });
                    var body = root.GetComponent<Collider>();
                    var shape = piece.GetComponentInChildren<Collider>();
                    Physics.SyncTransforms();
                    var target = shape.bounds.center - (body.bounds.center - root.transform.position);
                    root.transform.position = target - Vector3.forward * 3;
                    Physics.SyncTransforms();
                    Assert.IsTrue(HumanCannonCarry.CanMove(root.transform, body, target, root.transform.rotation, null));
                    var obstacle = wall.AddComponent<BoxCollider>(); obstacle.size = Vector3.one;
                    wall.transform.position = shape.bounds.center;
                    Physics.SyncTransforms();
                    Assert.IsFalse(HumanCannonCarry.CanMove(root.transform, body, target, root.transform.rotation, null),
                        "The fix must exclude limbs without allowing real scenery to be crossed.");
                }
                finally { Object.DestroyImmediate(root); Object.DestroyImmediate(wall); }
            });
        }

        [Test]
        public void AuthoredTrampolineCanParkOnFloorBelowSeveredLimb(
            [Values("cannon", "escape", "waiting", "rehearsal")] string source)
        {
            WithPiece("Rabbit", 0, piece =>
            {
                const string prepared = "Assets/Resources/Prefabs/Objects/ETCs/HumanCannonArtKit/Prepared/";
                Scene preview = default;
                GameObject root = null;
                var floor = new GameObject("Placement floor");
                try
                {
                    HumanCannonTrampoline original;
                    if (source == "cannon" || source == "escape")
                    {
                        preview = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(source == "cannon"
                            ? "Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity"
                            : "Assets/Scenes/Levels/escape_route.unity");
                        original = preview.GetRootGameObjects()
                            .SelectMany(r => r.GetComponentsInChildren<HumanCannonTrampoline>(true)).Single();
                    }
                    else
                    {
                        string prefab = source == "waiting" ? "RoundTrampolinePrepared" : "RehearsalCourseFromPerformance";
                        original = AssetDatabase.LoadAssetAtPath<GameObject>(prepared + prefab + ".prefab")
                            .GetComponentsInChildren<HumanCannonTrampoline>(true).Single();
                    }
                    root = Object.Instantiate(original.gameObject);
                    root.transform.localScale = original.transform.lossyScale;
                    SceneManager.MoveGameObjectToScene(root, SceneManager.GetActiveScene());
                    root.SetActive(true);
                    var trampoline = root.GetComponent<HumanCannonTrampoline>();
                    trampoline.enabled = false;
                    Assert.IsNotNull(trampoline.Handle);
                    typeof(HumanCannonTrampoline).GetMethod("RefreshCarryBody", Private)
                        .Invoke(trampoline, new object[] { null });
                    var center = piece.GetComponentInChildren<Collider>().bounds.center;
                    var support = floor.AddComponent<BoxCollider>();
                    support.size = new Vector3(20, .2f, 20);
                    floor.transform.position = center + Vector3.down * 2;
                    root.transform.position = center + Vector3.up;
                    Physics.SyncTransforms();
                    object[] args = { root.transform.position, null, 6f, Vector3.zero };
                    Assert.IsTrue((bool)typeof(HumanCannonTrampoline).GetMethod("TryParkBelow", Private)
                        .Invoke(trampoline, args), "The authored footprint must fit on the floor beneath the loose limb.");
                    Assert.That(((Vector3)args[3]).y, Is.EqualTo(support.bounds.max.y).Within(.001f));
                }
                finally
                {
                    Object.DestroyImmediate(root);
                    Object.DestroyImmediate(floor);
                    if (preview.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview);
                }
            });
        }

        [Test]
        public void ActualCutIsIgnoredByLaterPlayerAndNeverSupportsADroppedTrampoline([Values("Rabbit", "Bear", "PinkGirl", "BluePierrot")] string modelName, [Range(0, 3)] int limb)
        {
            WithPiece(modelName, limb, piece =>
            {
                var root = new GameObject("Drop probe"); root.SetActive(false);
                var floor = new GameObject("Drop floor");
                var player = new GameObject("Player spawned after the cut");
                try
                {
                    var shape = piece.GetComponentInChildren<Collider>();
                    var center = shape.bounds.center;
                    root.layer = LayerMask.NameToLayer(GamePhysicsLayers.Trampoline);
                    var trampoline = root.AddComponent<HumanCannonTrampoline>(); trampoline.enabled = false;
                    var support = floor.AddComponent<BoxCollider>(); support.size = new Vector3(5, .2f, 5);
                    floor.transform.position = center + Vector3.down * 2;
                    var body = player.AddComponent<CharacterController>();
                    player.transform.position = center + Vector3.right * 3;
                    typeof(DismemberedLimb).GetMethod("IgnoreFor", BindingFlags.Static | BindingFlags.NonPublic)
                        .Invoke(null, new object[] { body });
                    foreach (var collider in piece.GetComponentsInChildren<Collider>())
                        Assert.IsTrue(Physics.GetIgnoreCollision(body, collider), "A later player must not step on loose limbs.");
                    Physics.SyncTransforms();
                    object[] args = { center + Vector3.up * 2, Vector3.zero, false };
                    Assert.IsTrue((bool)typeof(HumanCannonTrampoline).GetMethod("TryFindGround", Private).Invoke(trampoline, args));
                    Assert.That(((Vector3)args[1]).y, Is.EqualTo(support.bounds.max.y).Within(.001f));
                }
                finally { Object.DestroyImmediate(root); Object.DestroyImmediate(floor); Object.DestroyImmediate(player); }
            });
        }
    }
}
