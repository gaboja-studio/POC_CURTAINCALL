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
        static int TintSlot(Component model)
        {
            var slots = (int[])model.GetType().GetField("PlayerTintMaterialSlots").GetValue(model);
            return slots != null && slots.Length > 0 ? slots[0] : 0;
        }
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
                Assert.That(rb.GetComponentInChildren<SkinnedMeshRenderer>(), Is.Not.Null);
                Assert.That(rb.GetComponentsInChildren<CharacterJoint>().Length, Is.EqualTo(1));
                Assert.That(rb.GetComponentsInChildren<Rigidbody>().Length, Is.EqualTo(2));
                Assert.That(rb.useGravity, Is.True);
                Assert.That(rb.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh.vertexCount, Is.GreaterThan(0));
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
        public IEnumerator DetachedLimbsKeepCutSurfaceAndBendTheirRenderedSkinImmediately()
        {
            var player = Player();
            player.transform.position += Vector3.up * 10f;
            yield return null;
            var view = Model(player);
            var limbs = (SkinnedMeshRenderer[])view.GetType().GetField("Limbs").GetValue(view);
            for (int index = 0; index < 4; index++)
            {
                var before = new Mesh();
                var after = new Mesh();
                try
                {
                    limbs[index].BakeMesh(before);
                    var expected = before.vertices.Select(v => limbs[index].transform.TransformPoint(v)).ToArray();
                    Mask(player, new[] { 4, 12, 13, 15 }[index]);
                    var root = (Rigidbody)Call(view, "GetPieceBody", index);
                    var renderer = root.GetComponentInChildren<SkinnedMeshRenderer>();
                    Assert.That(renderer, Is.Not.Null);
                    Assert.That(renderer.sharedMesh.subMeshCount, Is.EqualTo(limbs[index].sharedMesh.subMeshCount));
                    renderer.BakeMesh(after);
                    var initial = after.vertices;
                    for (int i = 0; i < initial.Length; i++)
                        Assert.That(Vector3.Distance(renderer.transform.TransformPoint(initial[i]), expected[i]), Is.LessThan(.003f), $"절단 순간의 위치·캡을 유지해야 한다(부위{index}, 정점{i}).");
                    foreach (var bone in renderer.bones)
                        Assert.That(bone.IsChildOf(root.transform) || bone == root.transform, Is.True, "살아 있는 원본 뼈를 참조하면 안 된다.");
                    var lower = root.GetComponentInChildren<CharacterJoint>();
                    Quaternion held = Quaternion.Inverse(root.rotation) * lower.transform.rotation;
                    foreach (var rb in root.GetComponentsInChildren<Rigidbody>()) Assert.That(rb.isKinematic, Is.False);
                    for (int i = 0; i < 25; i++) yield return new WaitForFixedUpdate();
                    Assert.That(Quaternion.Angle(held, Quaternion.Inverse(root.rotation) * lower.transform.rotation), Is.GreaterThan(15f), "절단 직후 무릎/팔꿈치가 물리로 움직여야 한다.");
                    renderer.BakeMesh(after);
                    Assert.That(after.vertices.Zip(initial, Vector3.Distance).Max(), Is.GreaterThan(.03f), "덩어리 회전뿐 아니라 실제 표시 메시가 구부러져야 한다.");
                }
                finally { Object.Destroy(before); Object.Destroy(after); }
            }
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
                original.GetPropertyBlock(block, TintSlot(view));
                Assert.That(block.GetColor("_BaseColor"), Is.EqualTo(colors[i]));
                original.GetPropertyBlock(block, 0);
                Assert.That(block.GetColor("_BaseColor"), Is.EqualTo(Color.white), "원화 고유색은 플레이어 색으로 덮지 않는다.");
            }
            Color snapshot = (Color)Get(view, "SurfaceColor");
            Mask(player, 4);
            var piece = (Rigidbody)Call(view, "GetPieceBody", 0);
            Call(view, "SetColor", Color.magenta);
            var pieceBlock = new MaterialPropertyBlock();
            piece.GetComponentInChildren<Renderer>().GetPropertyBlock(pieceBlock, TintSlot(view));
            Assert.That(pieceBlock.GetColor("_BaseColor"), Is.EqualTo(snapshot));
            Assert.That(cap.color, Is.EqualTo(capBefore));
            for (int i = 0; i < materials.Length; i++) Assert.That(materials[i].color, Is.EqualTo(shared[i]));
            Assert.That(Physics.GetIgnoreCollision(piece.GetComponentInChildren<Collider>(), player.GetComponent<CharacterController>()), Is.True);
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
            Assert.That(previous.GetComponents<Collider>().All(c => !c.enabled), Is.True,
                "관절 래그돌에서는 루트 충돌체가 없거나 비활성화되어야 한다.");
            var animator = (Animator)previous.GetType().GetField("Animator").GetValue(previous);
            var collider = animator.GetBoneTransform(HumanBodyBones.Hips).GetComponentInChildren<CapsuleCollider>();
            Assert.That(collider, Is.Not.Null);
            Assert.That(collider.enabled, Is.True);
            Assert.That(previous.GetComponentsInChildren<CharacterJoint>().Length, Is.GreaterThan(1));
            foreach (var body in previous.GetComponentsInChildren<Rigidbody>())
            {
                Assert.That(body.isKinematic, Is.False);
                Assert.That(body.useGravity, Is.True);
            }
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
        public IEnumerator HangingDeathImmediatelyReleasesAnimatedArmsAndFalls()
        {
            var player = Player();
            player.transform.position += Vector3.up * 8f;
            yield return null;
            var view = Model(player);
            var animator = (Animator)view.GetType().GetField("Animator").GetValue(view);
            animator.Play("Hanging", 0, .5f);
            animator.Update(0f);
            var hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Vector3 heldHand = hips.InverseTransformPoint(hand.position);
            var skin = (SkinnedMeshRenderer)view.GetType().GetField("Original").GetValue(view);
            var mesh = new Mesh();
            skin.BakeMesh(mesh);
            var heldSkin = mesh.vertices.Select(v => hips.InverseTransformPoint(skin.transform.TransformPoint(v))).ToArray();
            Object.Destroy(mesh);
            float startY = hips.position.y;
            Quaternion startRotation = hips.rotation;
            var ragdoll = Part(player, "PlayerRagdoll");
            Call(ragdoll, "GoLimp");
            Assert.That(animator.enabled, Is.False, "사망 호출 프레임부터 애니메이션을 중지해야 한다.");
            Vector3 spin = view.transform.InverseTransformDirection(hips.GetComponent<Rigidbody>().angularVelocity);
            Assert.That(Mathf.Abs(spin.x), Is.GreaterThan(1f));
            Assert.That(Mathf.Abs(spin.y), Is.GreaterThan(1f));
            Assert.That(Mathf.Abs(spin.z), Is.GreaterThan(1f), "몸통도 한 축이 아닌 세 축으로 회전하며 낙하해야 한다.");
            Assert.That(view.GetComponentsInChildren<Rigidbody>().Length, Is.EqualTo(11));
            Assert.That(view.GetComponentsInChildren<CharacterJoint>().Length, Is.EqualTo(10));
            Call(ragdoll, "GoLimp");
            Assert.That(view.GetComponentsInChildren<Rigidbody>().Length, Is.EqualTo(11), "사망 재확정은 멱등이어야 한다.");
            for (int i = 0; i < 35; i++) yield return new WaitForFixedUpdate();
            Assert.That(hips.position.y, Is.LessThan(startY - 1f));
            Assert.That(Quaternion.Angle(startRotation, hips.rotation), Is.GreaterThan(45f), "낙하 중 몸통의 실제 회전도 눈에 띄게 변해야 한다.");
            Assert.That(Vector3.Distance(heldHand, hips.InverseTransformPoint(hand.position)), Is.GreaterThan(.2f),
                "몸 전체 회전과 낙하를 제외하고도 손이 20cm 이상 풀려야 한다.");
            mesh = new Mesh();
            skin.BakeMesh(mesh);
            var releasedSkin = mesh.vertices.Select(v => hips.InverseTransformPoint(skin.transform.TransformPoint(v))).ToArray();
            Object.Destroy(mesh);
            Assert.That(releasedSkin.Zip(heldSkin, Vector3.Distance).Max(), Is.GreaterThan(.2f), "표시 스킨도 Hanging 스냅샷에 고정되면 안 된다.");
        }

        [UnityTest]
        public IEnumerator HangPalmsStayOnContactAndRopePoleSwayAreIndependent()
        {
            var player = Player();
            yield return null;
            var view = Model(player);
            var animator = (Animator)view.GetType().GetField("Animator").GetValue(view);
            var presentation = Part(player, "HumanoidPlayerPresentation");
            var rules = Get(TypeFor("CurtainCall.Settings.GameSettings").GetProperty("Base").GetValue(null), "Presentation");
            var rope = rules.GetType().GetField("ropeHangSwayAngle", BindingFlags.Instance | BindingFlags.NonPublic);
            var pole = rules.GetType().GetField("poleHangSwayAngle", BindingFlags.Instance | BindingFlags.NonPublic);
            object oldRope = rope.GetValue(rules), oldPole = pole.GetValue(rules);
            var restore = presentation.GetType().GetMethod("RestoreHangAlignment", BindingFlags.Instance | BindingFlags.NonPublic);
            var pose = presentation.GetType().GetMethod("ApplyHangPose", BindingFlags.Instance | BindingFlags.NonPublic);
            var palm = presentation.GetType().GetMethod("Palm", BindingFlags.Static | BindingFlags.NonPublic);
            Vector3 grip = player.transform.position + Vector3.up * 2f;
            Quaternion Pose(bool onRope)
            {
                restore.Invoke(presentation, null);
                animator.Play("Hanging", 0, .5f);
                animator.Update(0f);
                Vector3 axis = onRope ? Vector3.forward : Vector3.right;
                pose.Invoke(presentation, new object[] { grip, axis, onRope });
                Vector3 contact = grip + (onRope ? Vector3.zero : Vector3.down * .06f);
                foreach (bool left in new[] { true, false })
                {
                    var actual = (Vector3)palm.Invoke(null, new object[] { animator, left });
                    Assert.That(Vector3.Distance(actual, contact + axis * (left ? -.18f : .18f)), Is.LessThan(.015f),
                        $"{(onRope ? "줄" : "봉")} {(left ? "왼손" : "오른손")} 접점이 흔들림 중에도 유지되어야 한다.");
                }
                return view.transform.rotation;
            }
            try
            {
                while (Mathf.Abs(Mathf.Sin(Time.time * Mathf.PI * 1.6f)) < .5f) yield return null;
                rope.SetValue(rules, 0f); pole.SetValue(rules, 0f);
                Quaternion ropeStill = Pose(true), poleStill = Pose(false);
                pole.SetValue(rules, 20f);
                Assert.That(Quaternion.Angle(ropeStill, Pose(true)), Is.LessThan(.01f));
                Assert.That(Quaternion.Angle(poleStill, Pose(false)), Is.GreaterThan(5f));
                pole.SetValue(rules, 0f); rope.SetValue(rules, 20f);
                Assert.That(Quaternion.Angle(poleStill, Pose(false)), Is.LessThan(.01f));
                Assert.That(Quaternion.Angle(ropeStill, Pose(true)), Is.GreaterThan(5f));
                rope.SetValue(rules, 45f); pole.SetValue(rules, 45f);
                Pose(true); Pose(false);
            }
            finally
            {
                restore.Invoke(presentation, null);
                rope.SetValue(rules, oldRope); pole.SetValue(rules, oldPole);
            }
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
            renderer.GetPropertyBlock(block, TintSlot(view));
            var faded = block.GetColor("_BaseColor");
            Assert.That(faded.r, Is.EqualTo(color.r).Within(.00001f));
            Assert.That(faded.a, Is.EqualTo(.2f).Within(.001f));
            var fade = player.GetComponent(type);
            if (fade == null) fade = player.AddComponent(type);
            var entries = (IDictionary)type.GetField("faded", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(fade);
            entries.Add(renderer, entry);
            type.GetMethod("Restore", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(fade, new object[] { renderer });
            renderer.GetPropertyBlock(block, TintSlot(view));
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
            piece.GetComponentInChildren<Renderer>().GetPropertyBlock(block, TintSlot(view));
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
                piece.GetComponentInChildren<Renderer>().GetPropertyBlock(block, TintSlot(view));
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
        public IEnumerator RabbitSurfaceStatesPreserveCapsTintAndDetachedSnapshots()
        {
            var player = Player();
            yield return null;
            var view = Model(player);
            ((Behaviour)Part(player, "HumanoidPlayerPresentation")).enabled = false;
            var partType = TypeFor("CurtainCall.Player.BodyPart");
            int lastMask = 0;
            void Cut(int mask)
            {
                Call(view, "Apply", Enum.ToObject(partType, lastMask), Enum.ToObject(partType, mask), true, null);
                lastMask = mask;
            }
            var state = view.GetComponent(TypeFor("CurtainCall.CharacterArt.RabbitSurfaceStates"));
            Assert.That(state, Is.Not.Null);
            var stateType = state.GetType().GetNestedType("SurfaceState");
            void SetSurface(int value) => Call(state, "SetState", Enum.ToObject(stateType, value));
            var original = (SkinnedMeshRenderer)view.GetType().GetField("Original").GetValue(view);
            var bodies = (SkinnedMeshRenderer[])view.GetType().GetField("Bodies").GetValue(view);
            var limbs = (SkinnedMeshRenderer[])view.GetType().GetField("Limbs").GetValue(view);
            var skins = new[] { original }.Concat(bodies).Concat(limbs).ToArray();
            var meshes = skins.Select(s => s.sharedMesh).ToArray();
            var caps = skins.Select(s => s.sharedMaterials.Skip(2).ToArray()).ToArray();
            var clean = original.sharedMaterials;
            Color tint = new Color(.12f, .45f, .8f, .65f);
            Call(view, "SetColor", tint);
            foreach (int value in new[] { 1, 2, 0 })
            {
                SetSurface(value);
                for (int i = 0; i < skins.Length; i++)
                {
                    var mats = skins[i].sharedMaterials;
                    Assert.That(skins[i].sharedMesh, Is.SameAs(meshes[i]));
                    Assert.That(mats.Skip(2), Is.EqualTo(caps[i]), "단면 재질은 상태 교체 대상이 아니다.");
                    if (value == 0) Assert.That(mats.Take(2), Is.EqualTo(clean));
                    else
                    {
                        Assert.That(mats[0].name, Is.EqualTo(value == 1 ? "RabbitCostume_Light" : "RabbitCostume_Heavy"));
                        Assert.That(mats[1], Is.SameAs(mats[0]));
                    }
                    var block = new MaterialPropertyBlock();skins[i].GetPropertyBlock(block, TintSlot(view));
                    Assert.That(Vector4.Distance(block.GetColor("_BaseColor"), tint), Is.LessThan(1e-6f));
                }
            }
            SetSurface(1);Cut(4);
            var first = ((Rigidbody)Call(view, "GetPieceBody", 0)).GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.That(first.sharedMaterials[0].name, Is.EqualTo("RabbitCostume_Light"));
            SetSurface(2);Cut(12);
            var second = ((Rigidbody)Call(view, "GetPieceBody", 1)).GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.That(first.sharedMaterials[0].name, Is.EqualTo("RabbitCostume_Light"), "기존 조각은 절단 당시 상태를 유지한다.");
            Assert.That(second.sharedMaterials[0].name, Is.EqualTo("RabbitCostume_Heavy"));
            Cut(0);
            Assert.That((int)Get(view, "PieceCount"), Is.Zero);
            Assert.That(original.sharedMaterials[0].name, Is.EqualTo("RabbitCostume_Heavy"), "부위 복구는 표면을 자동 세척하지 않는다.");
            SetSurface(0);
        }

        [UnityTest]
        public IEnumerator InjuryCountSelectsSurfaceAndRestoresAfterModelReplacement()
        {
            var player = Player();
            yield return null;
            string Surface(Component view) => Get(view.GetComponent(TypeFor("CurtainCall.CharacterArt.RabbitSurfaceStates")), "State").ToString();
            Assert.That(Surface(Model(player)), Is.EqualTo("Clean"));
            foreach (var entry in new[] { (4, "Light"), (12, "Light"), (13, "Heavy"), (15, "Heavy"), (0, "Clean") })
            {
                Mask(player, entry.Item1);
                Assert.That(Surface(Model(player)), Is.EqualTo(entry.Item2));
                yield return null;
                Assert.That(Surface(Model(player)), Is.EqualTo(entry.Item2));
            }
            Mask(player, 13);
            var slot = Part(player, "PlayerModelSlot");
            Call(slot, "SetModel", Get(slot, "CurrentPrefab"));
            Assert.That(Surface(Model(player)), Is.EqualTo("Heavy"));
            Mask(player, 0);
            Assert.That(Surface(Model(player)), Is.EqualTo("Clean"));
            var network = player.GetComponent(TypeFor("CurtainCall.Network.PlayerSync.NetworkPlayer"));
            var variable = network.GetType().GetField("state", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(network);
            var valueProperty = variable.GetType().GetProperty("Value");
            foreach (int value in new[] { 1, 3, 0 })
            {
                valueProperty.SetValue(variable, Enum.ToObject(valueProperty.PropertyType, value));
                yield return null;
                Assert.That(Surface(Model(player)), Is.EqualTo(value == 0 ? "Clean" : "Heavy"), "Fallen/Retired death without missing limbs");
            }
        }

        [UnityTest]
        public IEnumerator BearCatalogModelKeepsMovementCutsSurfacesAndOutfits()
        {
            var catalog = Resources.Load("Settings/PlayerAppearanceCatalog");
            var options = (Array)catalog.GetType().GetField("models").GetValue(catalog);
            int bearIndex = Enumerable.Range(0, options.Length).Single(i =>
                ((GameObject)options.GetValue(i).GetType().GetField("prefab").GetValue(options.GetValue(i))).name == "Bear");
            var bear = (GameObject)Call(catalog, "PrefabFor", bearIndex, 0);
            for (int outfit = 0; outfit < 4; outfit++) Assert.That(Call(catalog, "PrefabFor", bearIndex, outfit), Is.SameAs(bear));
            var player = Player();
            yield return null;
            var slot = Part(player, "PlayerModelSlot");
            Call(slot, "SetModel", bear);
            yield return null;
            var view = Model(player);
            var animator = (Animator)view.GetType().GetField("Animator").GetValue(view);
            Assert.That(animator.avatar.isValid && animator.avatar.isHuman, Is.True);
            Assert.That(animator.applyRootMotion, Is.False);
            var mover = Part(player, "PlayerMover");
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
            input.Sprint = false; input.Crouch = true;
            yield return null;
            yield return null;
            Assert.That((string)Get(view, "Motion"), Is.EqualTo("CrouchWalk"));
            input.Crouch = false;
            yield return null;
            Call(mover, "RequestJump");
            yield return null;
            yield return null;
            Assert.That(new[] { "JumpStart", "JumpLoop" }, Does.Contain((string)Get(view, "Motion")));
            Object.Destroy(input);
            var surface = view.GetComponent(TypeFor("CurtainCall.CharacterArt.RabbitSurfaceStates"));
            var original = (SkinnedMeshRenderer)view.GetType().GetField("Original").GetValue(view);
            var colors = (Color[])catalog.GetType().GetField("outfitColors").GetValue(catalog);
            var block = new MaterialPropertyBlock();
            var network = player.GetComponent(TypeFor("CurtainCall.Network.PlayerSync.NetworkPlayer"));
            var outfitVariable = network.GetType().GetField("appearanceOutfit", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(network);
            for (int outfit = 0; outfit < 4; outfit++)
            {
                outfitVariable.GetType().GetProperty("Value").SetValue(outfitVariable, outfit);
                yield return null;
                original.GetPropertyBlock(block, 0);
                Assert.That(((Vector4)block.GetColor("_VariantColor") - (Vector4)colors[outfit]).sqrMagnitude, Is.LessThan(1e-6f));
            }
            Assert.That(Get(surface, "State").ToString(), Is.EqualTo("Clean"));
            for (int stage = 1; stage <= 4; stage++)
            {
                Mask(player, new[] { 0, 4, 12, 13, 15 }[stage]);
                Assert.That((int)Get(view, "Stage"), Is.EqualTo(stage));
                Assert.That((int)Get(view, "PieceCount"), Is.EqualTo(stage));
                Assert.That(Get(surface, "State").ToString(), Is.EqualTo(stage <= 2 ? "Light" : "Heavy"));
                var rb = (Rigidbody)Call(view, "GetPieceBody", stage - 1);
                var detached = rb.GetComponentInChildren<SkinnedMeshRenderer>();
                Assert.That(rb.GetComponentsInChildren<Rigidbody>().Length, Is.EqualTo(2));
                Assert.That(rb.GetComponentsInChildren<CharacterJoint>().Length, Is.EqualTo(1));
                Assert.That(detached.bones.All(b => b.IsChildOf(rb.transform) || b == rb.transform), Is.True);
                detached.GetPropertyBlock(block, 0);
                Assert.That(((Vector4)block.GetColor("_VariantColor") - (Vector4)colors[3]).sqrMagnitude, Is.LessThan(1e-6f));
                Vector3 start = rb.position;
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();
                Assert.That(Vector3.Distance(start, rb.position), Is.GreaterThan(.001f));
            }
            Call(Part(player, "PlayerRagdoll"), "Restore");
            Mask(player, 0);
            yield return null;
            Assert.That((int)Get(Model(player), "Stage"), Is.Zero);
            Assert.That((int)Get(Model(player), "PieceCount"), Is.Zero);
            Assert.That(Get(Model(player).GetComponent(surface.GetType()), "State").ToString(), Is.EqualTo("Clean"));
            Mask(player, 13);
            Call(slot, "SetModel", bear);
            Assert.That(Get(Model(player).GetComponent(surface.GetType()), "State").ToString(), Is.EqualTo("Heavy"));
            Mask(player, 0);
            var variable = network.GetType().GetField("state", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(network);
            var valueProperty = variable.GetType().GetProperty("Value");
            valueProperty.SetValue(variable, Enum.ToObject(valueProperty.PropertyType, 1));
            yield return null;
            Assert.That(Get(Model(player).GetComponent(surface.GetType()), "State").ToString(), Is.EqualTo("Heavy"));
            valueProperty.SetValue(variable, Enum.ToObject(valueProperty.PropertyType, 0));
            yield return null;
            Assert.That(Get(Model(player).GetComponent(surface.GetType()), "State").ToString(), Is.EqualTo("Clean"));
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
