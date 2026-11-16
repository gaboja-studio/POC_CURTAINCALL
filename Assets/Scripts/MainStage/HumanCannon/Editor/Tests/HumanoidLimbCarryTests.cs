using System;
using System.Reflection;
using System.Linq;
using CurtainCall.Damage;
using CurtainCall.Player;
using CurtainCall.Settings;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Range = NUnit.Framework.RangeAttribute;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    [Category("CommonProgressionRegression"), Category("HumanoidLimbCarryRegression")]
    public sealed class HumanoidLimbCarryTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        // Use the production baking path and authored player model. A hand-made marked box hid this bug.
        static void WithPiece(int index, Action<GameObject> check)
        {
            GameObject modelRoot = null, pieceRoot = null;
            Mesh baked = null;
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Resources/Prefabs/Characters/Players/Models/Rabbit/Rabbit.prefab");
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

        [Test]
        public void ActualCutDoesNotBlockCarryOrLiftTheLoad([Range(0, 3)] int limb, [Values(0f, 90f)] float yaw)
        {
            WithPiece(limb, piece =>
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
        public void ActualCutPassesTrampolineButKeepsWorldCollision([Range(0, 3)] int limb, [Values(false, true)] bool reenable)
        {
            WithPiece(limb, piece =>
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
        public void SavedHumanCannonTrampolineCrossesActualCutButStopsAtSolidWall([Range(0, 3)] int limb)
        {
            WithPiece(limb, piece =>
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
        public void ActualCutIsIgnoredByLaterPlayerAndNeverSupportsADroppedTrampoline([Range(0, 3)] int limb)
        {
            WithPiece(limb, piece =>
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
