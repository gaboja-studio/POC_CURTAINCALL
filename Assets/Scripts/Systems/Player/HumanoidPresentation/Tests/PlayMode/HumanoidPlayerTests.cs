using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace CurtainCall.Tests.HumanoidPresentation
{
    [DefaultExecutionOrder(-40)]
    public sealed class HumanoidMovementTestInput : MonoBehaviour
    {
        public Component Mover;
        public bool Sprint;
        public bool Crouch;
        void Update()
        {
            if (Mover == null) return;
            var type = Mover.GetType();
            type.GetMethod("SetMoveInput", new[] { typeof(float), typeof(float) }).Invoke(Mover, new object[] { 0f, 1f });
            type.GetMethod("SetLocomotionInput").Invoke(Mover, new object[] { Sprint, Crouch });
        }
    }

    public sealed class HumanoidPlayerTests
    {
        readonly List<GameObject> objects = new();
        bool previousBackground;
        static Type TypeFor(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
        static Component Part(GameObject player, string name) => player.GetComponent(TypeFor("CurtainCall.Player." + name));
        static object Get(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name).Invoke(target, args);
        static Component Model(GameObject player) => (Component)Get(Part(player, "HumanoidPlayerPresentation"), "Model");
        static void Mask(GameObject player, int mask)
        {
            Call(Part(player, "PlayerCondition"), "SetLostParts", Enum.ToObject(TypeFor("CurtainCall.Player.BodyPart"), mask));
        }
        GameObject Player()
        {
            var prefab = Resources.Load<GameObject>("Prefabs/Characters/Players/Player");
            Assert.That(prefab, Is.Not.Null);
            var player = Object.Instantiate(prefab, new Vector3(objects.Count * 4f, 0, 0), Quaternion.identity);
            objects.Add(player);
            var input = player.GetComponent(TypeFor("CurtainCall.Player.PlayerController")) as Behaviour;
            if (input != null) input.enabled = false;
            Call(Part(player, "PlayerBalance"), "SetBalanceActive", false);
            return player;
        }
        [SetUp]
        public void Setup()
        {
            previousBackground = Application.runInBackground;
            Application.runInBackground = true;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(0, -.05f, 0);
            floor.transform.localScale = new Vector3(100, .1f, 100);
            objects.Add(floor);
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            foreach (var target in objects) if (target != null) Object.Destroy(target);
            objects.Clear();
            yield return null;
            Application.runInBackground = previousBackground;
        }

        [UnityTest]
        public IEnumerator ActualPrefabCutsAllLimbsAndRestoresWithoutLegacyPieces()
        {
            var player = Player();
            yield return null;
            var view = Model(player);
            Assert.That(view, Is.Not.Null);
            var animator = (Animator)view.GetType().GetField("Animator").GetValue(view);
            Assert.That(animator.applyRootMotion, Is.False);
            var legacy = player.GetComponent(TypeFor("CurtainCall.Damage.LimbDismemberment"));
            foreach (int stage in Enumerable.Range(1, 4))
            {
                int mask = new[] { 0, 4, 12, 13, 15 }[stage];
                Mask(player, mask);
                Assert.That((int)Get(view, "Stage"), Is.EqualTo(stage));
                Assert.That((int)Get(view, "PieceCount"), Is.EqualTo(stage));
                var rb = (Rigidbody)Call(view, "GetPieceBody", stage - 1);
                Assert.That(rb, Is.Not.Null);
                Assert.That(rb.GetComponent<SkinnedMeshRenderer>(), Is.Null);
                Assert.That(rb.GetComponents<Joint>(), Is.Empty);
                Assert.That(rb.useGravity, Is.True);
                Assert.That(rb.GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.GreaterThan(0));
                Assert.That((int)Get(legacy, "PieceCount"), Is.Zero);
                Vector3 start = rb.position;
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();
                Assert.That(Vector3.Distance(start, rb.position), Is.GreaterThan(.001f));
            }
            // 사망으로 분리된 모델도 기존 래그돌 복구 경로를 사용한다.
            Call(Part(player, "PlayerRagdoll"), "Restore");
            Mask(player, 0);
            yield return null;
            view = Model(player);
            Assert.That((int)Get(view, "Stage"), Is.Zero);
            Assert.That((int)Get(view, "PieceCount"), Is.Zero);
        }

        [UnityTest]
        public IEnumerator InitialInjuryRebindBatchCutsAndDisableDoNotReplayHistory()
        {
            var player = Player();
            yield return null;
            var presenter = (Behaviour)Part(player, "HumanoidPlayerPresentation");
            presenter.enabled = false;
            Mask(player, 12);
            presenter.enabled = true;
            var view = Model(player);
            Assert.That((int)Get(view, "Stage"), Is.EqualTo(2));
            Assert.That((int)Get(view, "PieceCount"), Is.Zero);
            Mask(player, 13);
            Assert.That((int)Get(view, "PieceCount"), Is.EqualTo(1));
            Mask(player, 12);
            Assert.That((int)Get(view, "PieceCount"), Is.Zero);
            Mask(player, 0);
            Mask(player, 13);
            Assert.That((int)Get(view, "PieceCount"), Is.EqualTo(3));
            var slot = Part(player, "PlayerModelSlot");
            Call(slot, "SetModel", Get(slot, "CurrentPrefab"));
            view = Model(player);
            Assert.That((int)Get(view, "Stage"), Is.EqualTo(3));
            Assert.That((int)Get(view, "PieceCount"), Is.Zero);
            yield return null;
            Assert.That((string)Get(view, "Motion"), Is.EqualTo("Crawl"));
            Assert.That(((Animator)view.GetType().GetField("Animator").GetValue(view)).speed, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator InitialLegLossStartsProneWithoutWaitingForMovement()
        {
            var player = Player();
            yield return null;
            var presenter = (Behaviour)Part(player, "HumanoidPlayerPresentation");
            foreach (int mask in new[] { 12, 13 })
            {
                presenter.enabled = false;
                Mask(player, mask);
                presenter.enabled = true;
                var view = Model(player);
                var animator = (Animator)view.GetType().GetField("Animator").GetValue(view);
                Assert.That((string)Get(view, "Motion"), Is.EqualTo("Crawl"));
                Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("Crawl"), "첫 표시부터 서 있는 기본 상태 대신 기어가기 상태");
                Assert.IsFalse(animator.IsInTransition(0), "정지 속도로 느려진 전환을 기다리지 않는다");
                yield return null;
                Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("Crawl"));
            }
        }

        [UnityTest]
        public IEnumerator FourColorsPreserveCapsSharedMaterialsAndFragmentSnapshot()
        {
            var player = Player();
            yield return null;
            var view = Model(player);
            var original = (SkinnedMeshRenderer)view.GetType().GetField("Original").GetValue(view);
            var bodies = (SkinnedMeshRenderer[])view.GetType().GetField("Bodies").GetValue(view);
            var materials = original.sharedMaterials;
            var shared = materials.Select(m => m.color).ToArray();
            var cap = bodies[0].sharedMaterials.Last();
            Color capBefore = cap.color;
            var settingsType = TypeFor("CurtainCall.Settings.GameSettings");
            var settings = settingsType.GetProperty("Base", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            var character = Get(settings, "Character");
            var colors = Enumerable.Range(0, 4).Select(i => (Color)Call(character, "GetPlayerColor", i)).ToArray();
            Assert.That(colors.Distinct().Count(), Is.EqualTo(4));
            for (int i = 0; i < 4; i++)
            {
                Call(view, "SetColor", colors[i]);
                var block = new MaterialPropertyBlock();
                original.GetPropertyBlock(block, 0);
                Assert.That(block.GetColor("_BaseColor"), Is.EqualTo(colors[i]));
            }
            Color snapshot = (Color)Get(view, "SurfaceColor");
            Mask(player, 4);
            var piece = (Rigidbody)Call(view, "GetPieceBody", 0);
            Call(view, "SetColor", Color.magenta);
            var pieceBlock = new MaterialPropertyBlock();
            piece.GetComponent<Renderer>().GetPropertyBlock(pieceBlock, 0);
            Assert.That(pieceBlock.GetColor("_BaseColor"), Is.EqualTo(snapshot));
            Assert.That(cap.color, Is.EqualTo(capBefore));
            for (int i = 0; i < materials.Length; i++) Assert.That(materials[i].color, Is.EqualTo(shared[i]));
            Assert.That(Physics.GetIgnoreCollision(piece.GetComponent<Collider>(), player.GetComponent<CharacterController>()), Is.True);
            Assert.That(piece.gameObject.layer, Is.EqualTo(Physics.IgnoreRaycastLayer));
        }

        [UnityTest]
        public IEnumerator RagdollReplacementPreservesInjuryWithoutReplayingFragments()
        {
            var player = Player();
            yield return null;
            var slot = Part(player, "PlayerModelSlot");
            var ragdoll = Part(player, "PlayerRagdoll");
            Mask(player, 4);
            var previous = Model(player);
            Call(ragdoll, "GoLimp");
            Assert.That((bool)Get(ragdoll, "IsLimp"), Is.True);
            Assert.That(Model(player), Is.Null);
            Assert.That(((Animator)previous.GetType().GetField("Animator").GetValue(previous)).enabled, Is.False);
            var collider = previous.GetComponent<BoxCollider>();
            Assert.That(collider, Is.Not.Null);
            Assert.That(collider.enabled, Is.True);
            Assert.That(collider.size.sqrMagnitude, Is.GreaterThan(.001f));
            previous.transform.position += new Vector3(2f, 1f, 0f);
            Vector3 detachedPosition = previous.transform.position;
            Mask(player, 15);
            Assert.That(Vector3.Distance(previous.transform.position, detachedPosition), Is.LessThan(.001f));
            Assert.That((int)Get(previous, "Stage"), Is.EqualTo(4));
            for (int i = 0; i < 100; i++) yield return new WaitForFixedUpdate();
            Assert.That(collider.bounds.min.y, Is.GreaterThan(-.1f));
            Mask(player, 4);
            Call(ragdoll, "Restore");
            yield return null;
            var current = Model(player);
            Assert.That(current, Is.Not.Null);
            Assert.That(current, Is.Not.SameAs(previous));
            Assert.That((int)Get(current, "Stage"), Is.EqualTo(1));
            Assert.That((int)Get(current, "PieceCount"), Is.Zero);
        }

        [UnityTest]
        public IEnumerator PropertyBlockColorSurvivesCameraFadeAndRestore()
        {
            var player = Player();
            yield return null;
            var view = Model(player);
            var renderer = (SkinnedMeshRenderer)view.GetType().GetField("Original").GetValue(view);
            var originals = renderer.sharedMaterials;
            Color color = new(.2f, .4f, .7f, .8f);
            Call(view, "SetColor", color);
            var type = TypeFor("CurtainCall.Cameras.CameraOcclusionFade");
            var flags = BindingFlags.NonPublic | BindingFlags.Static;
            var entry = type.GetMethod("CreateFaded", flags).Invoke(null, new object[] { renderer });
            Assert.That(entry, Is.Not.Null);
            entry.GetType().GetField("Alpha").SetValue(entry, .25f);
            type.GetMethod("ApplyAlpha", flags).Invoke(null, new[] { entry });
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block, 0);
            var faded = block.GetColor("_BaseColor");
            Assert.That(faded.r, Is.EqualTo(color.r).Within(.00001f));
            Assert.That(faded.a, Is.EqualTo(.2f).Within(.001f));
            var fade = player.GetComponent(type);
            if (fade == null) fade = player.AddComponent(type);
            var entries = (IDictionary)type.GetField("faded", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(fade);
            entries.Add(renderer, entry);
            type.GetMethod("Restore", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(fade, new object[] { renderer });
            renderer.GetPropertyBlock(block, 0);
            Assert.That(Vector4.Distance(block.GetColor("_BaseColor"), color), Is.LessThan(.00001f));
            Assert.That(renderer.sharedMaterials, Is.EqualTo(originals));
        }

        [UnityTest]
        public IEnumerator CrawlSamplesRecordGroundClearanceWithoutClaimingFootContact()
        {
            var player = Player();
            yield return null;
            var view = Model(player);
            ((Behaviour)Part(player, "HumanoidPlayerPresentation")).enabled = false;
            ((Behaviour)Part(player, "PlayerMover")).enabled = false;
            var animator = (Animator)view.GetType().GetField("Animator").GetValue(view);
            var bodies = (SkinnedMeshRenderer[])view.GetType().GetField("Bodies").GetValue(view);
            var mesh = new Mesh();
            try
            {
                foreach (int mask in new[] { 12, 13 })
                {
                    Call(view, "Apply", Enum.ToObject(TypeFor("CurtainCall.Player.BodyPart"), 0),
                        Enum.ToObject(TypeFor("CurtainCall.Player.BodyPart"), mask), false, player.GetComponent<CharacterController>());
                    float minimum = float.PositiveInfinity, maximum = float.NegativeInfinity;
                    var renderer = bodies[mask == 12 ? 1 : 2];
                    for (int i = 0; i <= 120; i++)
                    {
                        animator.Play("Crawl", 0, i / 120f);
                        animator.Update(0f);
                        renderer.BakeMesh(mesh);
                        float lowest = mesh.vertices.Min(v => renderer.transform.TransformPoint(v).y - player.transform.position.y);
                        Assert.That(float.IsNaN(lowest) || float.IsInfinity(lowest), Is.False);
                        minimum = Mathf.Min(minimum, lowest);
                        maximum = Mathf.Max(maximum, lowest);
                    }
                    TestContext.WriteLine($"Crawl mask={mask}, 121 samples, skin lowest range={minimum:F4}..{maximum:F4} m relative to root/floor. Contact quality is not asserted.");
                }
            }
            finally { Object.Destroy(mesh); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator CrawlSlopeAndJumpRecordClearanceWithoutClaimingContactQuality()
        {
            var floor = objects[0];
            floor.transform.rotation = Quaternion.Euler(-10f, 0f, 0f);
            var player = Player();
            yield return null;
            Mask(player, 12);
            var mover = Part(player, "PlayerMover");
            mover.GetType().GetProperty("FreeMovement").SetValue(mover, true);
            var input = player.AddComponent<HumanoidMovementTestInput>();
            input.Mover = mover;
            for (int i = 0; i < 8; i++) yield return new WaitForFixedUpdate();
            Call(mover, "RequestJump");
            var view = Model(player);
            var renderer = ((SkinnedMeshRenderer[])view.GetType().GetField("Bodies").GetValue(view))[1];
            var mesh = new Mesh();
            bool sawAir = false, landed = false;
            float minimum = float.PositiveInfinity, maximum = float.NegativeInfinity;
            float end = Time.realtimeSinceStartup + 6f;
            Vector3 normal = floor.transform.up;
            Vector3 surface = floor.transform.position + normal * .05f;
            try
            {
                do
                {
                    yield return new WaitForFixedUpdate();
                    bool air = (bool)Get(mover, "IsAirborne");
                    sawAir |= air;
                    landed = sawAir && !air;
                    renderer.BakeMesh(mesh);
                    float lowest = mesh.vertices.Min(v => Vector3.Dot(renderer.transform.TransformPoint(v) - surface, normal));
                    Assert.That(float.IsNaN(lowest) || float.IsInfinity(lowest), Is.False);
                    minimum = Mathf.Min(minimum, lowest);
                    maximum = Mathf.Max(maximum, lowest);
                } while (!landed && Time.realtimeSinceStartup < end);
                Assert.That(sawAir, Is.True, "실제 Mover 점프 진입");
                Assert.That(landed, Is.True, "경사 바닥에 실제 착지");
                Assert.That((string)Get(view, "Motion"), Is.EqualTo("Crawl"));
                TestContext.WriteLine($"Crawl 10-degree slope and jump/landing: skin plane clearance={minimum:F4}..{maximum:F4} m. Contact quality is not asserted.");
            }
            finally { Object.Destroy(mesh); }
        }

        [UnityTest]
        public IEnumerator LiveSettingsColorReachesBodyButNotOldFragment()
        {
            var player = Player();
            yield return null;
            var view = Model(player);
            Mask(player, 4);
            var piece = (Rigidbody)Call(view, "GetPieceBody", 0);
            var block = new MaterialPropertyBlock();
            piece.GetComponent<Renderer>().GetPropertyBlock(block, 0);
            Color snapshot = block.GetColor("_BaseColor");
            var settings = TypeFor("CurtainCall.Settings.GameSettings").GetProperty("Base", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            var character = Get(settings, "Character");
            var field = character.GetType().GetField("playerColorRed", BindingFlags.NonPublic | BindingFlags.Instance);
            Color original = (Color)field.GetValue(character);
            try
            {
                field.SetValue(character, Color.cyan);
                yield return null;
                yield return null;
                Assert.That((Color)Get(view, "SurfaceColor"), Is.EqualTo(Color.cyan));
                piece.GetComponent<Renderer>().GetPropertyBlock(block, 0);
                Assert.That(block.GetColor("_BaseColor"), Is.EqualTo(snapshot));
            }
            finally { field.SetValue(character, original); }
        }

        [UnityTest]
        public IEnumerator InitialNetworkConditionDoesNotSuppressNextSameFrameCut()
        {
            var player = Player();
            yield return null;
            var network = player.GetComponent(TypeFor("CurtainCall.Network.PlayerSync.NetworkPlayer"));
            var flag = network.GetType().GetProperty("IsApplyingInitialCondition");
            try
            {
                flag.SetValue(network, true);
                Mask(player, 4);
                Assert.That((int)Get(Model(player), "PieceCount"), Is.Zero);
            }
            finally { flag.SetValue(network, false); }
            Mask(player, 12);
            Assert.That((int)Get(Model(player), "PieceCount"), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator KeyboardEventsReachReaderControllerAndHumanoidAnimation()
        {
            var originalSettings = InputSystem.settings;
            var previousBackground = originalSettings.backgroundBehavior;
            var previousEditorInput = originalSettings.editorInputBehaviorInPlayMode;
            var working = originalSettings;
            var previousKeyboard = Keyboard.current;
            Keyboard keyboard = null;
            try
            {
                working.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                working.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                keyboard = InputSystem.AddDevice<Keyboard>("Humanoid Input Test");
                keyboard.MakeCurrent();
                var player = Player();
                var controller = (Behaviour)Part(player, "PlayerController");
                var scheme = Resources.Load<ScriptableObject>("Settings/Control/PlatformControlScheme");
                Assert.That(scheme, Is.Not.Null);
                Call(controller, "SetScheme", scheme);
                controller.enabled = true;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                for (int i = 0; i < 4; i++) yield return null;
                var mover = Part(player, "PlayerMover");
                var view = Model(player);
                Vector3 start = player.transform.position;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                for (int i = 0; i < 5; i++) yield return null;
                Assert.That((bool)Get(mover, "IsMoving"), Is.True);
                Assert.That((string)Get(view, "Motion"), Is.EqualTo("Walk"));
                Assert.That(Vector3.Distance(start, player.transform.position), Is.GreaterThan(.001f));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.LeftShift));
                for (int i = 0; i < 3; i++) yield return null;
                Assert.That((bool)Get(mover, "IsSprinting"), Is.True);
                Assert.That((string)Get(view, "Motion"), Is.EqualTo("Run"));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.LeftCtrl));
                for (int i = 0; i < 3; i++) yield return null;
                Assert.That((bool)Get(mover, "IsCrouched"), Is.True);
                Assert.That((string)Get(view, "Motion"), Is.EqualTo("CrouchWalk"));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                for (int i = 0; i < 3; i++) yield return null;
                Assert.That((bool)Get(mover, "IsCrouched"), Is.False);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                yield return null;
                yield return null;
                Assert.That(Get(mover, "CurrentJump").ToString(), Is.EqualTo("InPlace"));
                Assert.That(new[] { "JumpStart", "JumpLoop" }, Does.Contain((string)Get(view, "Motion")));
            }
            finally
            {
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
                originalSettings.backgroundBehavior = previousBackground;
                originalSettings.editorInputBehaviorInPlayMode = previousEditorInput;
            }
        }

        [UnityTest]
        public IEnumerator ActualMoverDrivesWalkRunCrouchAndJumpStates()
        {
            var player = Player();
            yield return null;
            var mover = Part(player, "PlayerMover");
            var view = Model(player);
            mover.GetType().GetProperty("FreeMovement").SetValue(mover, true);
            var input = player.AddComponent<HumanoidMovementTestInput>();
            input.Mover = mover;
            yield return null;
            yield return null;
            Assert.That((string)Get(view, "Motion"), Is.EqualTo("Walk"));
            input.Sprint = true;
            yield return null;
            yield return null;
            Assert.That((string)Get(view, "Motion"), Is.EqualTo("Run"));
            input.Sprint = false;
            input.Crouch = true;
            yield return null;
            yield return null;
            Assert.That((string)Get(view, "Motion"), Is.EqualTo("CrouchWalk"));
            input.Crouch = false;
            yield return null;
            Call(mover, "RequestJump");
            yield return null;
            yield return null;
            Assert.That(new[] { "JumpStart", "JumpLoop" }, Does.Contain((string)Get(view, "Motion")));
            Assert.That(((Animator)view.GetType().GetField("Animator").GetValue(view)).applyRootMotion, Is.False);
        }
    }
}
