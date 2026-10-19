using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    public sealed class RouteGeometryTests
    {
        const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        GameObject root;
        HumanCannonTransport transport;
        HumanCannonTrampoline trampoline;
        HumanCannonFireWall fire;
        Transform platform, origin, destination, fireStart, boundary, visual;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("RouteGeometryTest");
            // 장면의 트램폴린 singleton이나 네트워크 이벤트를 등록하지 않고 배치만 검사한다.
            root.SetActive(false);
            transport = Child("Transport").gameObject.AddComponent<HumanCannonTransport>();
            platform = Child("Platform");
            origin = Child("Origin");
            destination = Child("Destination");
            destination.position = new Vector3(0, 4, 10);
            Set(transport, "platform", platform);
            Set(transport, "start", origin);
            Set(transport, "destination", destination);
            Set(transport, "halfSize", new Vector2(2, 3));
            transport.Present(0);

            trampoline = Child("Trampoline").gameObject.AddComponent<HumanCannonTrampoline>();
            Set(trampoline, "halfSize", Vector2.one);
            var handle = Child("Handle");
            handle.SetParent(trampoline.transform, false);
            Set(trampoline, "handle", handle);

            fire = Child("Fire").gameObject.AddComponent<HumanCannonFireWall>();
            fireStart = Child("FireStart");
            boundary = Child("Boundary");
            boundary.position = Vector3.forward * 20;
            visual = Child("Visual");
            Set(fire, "start", fireStart);
            Set(fire, "boundary", boundary);
            Set(fire, "visual", visual);
            Set(fire, "warningSeconds", 5f);
            Set(fire, "speed", 2f);
            Set(fire, "halfWidth", 3f);
            Set(fire, "height", 10f);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void Fire_UnavailableCannotArmDamageOrShowEvenWithReceivedState_AndCanBeRestored()
        {
            Set(fire, "available", false);
            Assert.IsFalse(fire.Arm(10));
            fire.Receive(10, false);
            Assert.IsFalse(fire.Advancing(20));
            Assert.IsFalse(fire.Threatens(Vector3.forward * 5, 0.3f, 20));
            Assert.IsFalse(fire.Tick(30));
            fire.Present(20);
            Assert.IsFalse(visual.gameObject.activeSelf);
            fire.ResetWall();
            Set(fire, "available", true);
            Assert.IsTrue(fire.Arm(10));
            Assert.IsTrue(fire.Threatens(Vector3.forward * 5, 0.3f, 20));
            fire.Present(20);
            Assert.IsTrue(visual.gameObject.activeSelf);
        }

        [TestCase(1.85f, 0f, 0f, true)]
        [TestCase(-1.85f, 0f, 0f, true)]
        [TestCase(1.851f, 0f, 0f, false)]
        [TestCase(-1.851f, 0f, 0f, false)]
        [TestCase(0f, 0f, 2.85f, true)]
        [TestCase(0f, 0f, -2.85f, true)]
        [TestCase(0f, 0f, 2.851f, false)]
        [TestCase(0f, 0f, -2.851f, false)]
        [TestCase(0f, -0.15f, 0f, true)]
        [TestCase(0f, 0.3f, 0f, true)]
        [TestCase(0f, -0.151f, 0f, false)]
        [TestCase(0f, 0.301f, 0f, false)]
        public void Feet_RequireInsetRectangleAndPlatformHeight(float x, float y, float z, bool expected)
        {
            Assert.That(transport.ContainsFeet(new Vector3(x, y, z)), Is.EqualTo(expected));
        }

        [Test]
        public void Feet_FollowTranslatedRotatedMovingPlatform()
        {
            origin.SetPositionAndRotation(new Vector3(12, 2, -8), Quaternion.Euler(0, 90, 0));
            destination.position = new Vector3(12, 6, 12);
            transport.Present(0.5f);
            Near(platform.position, new Vector3(12, 4, 2));
            Assert.IsTrue(transport.ContainsFeet(platform.position + origin.rotation * new Vector3(1.8f, 0.1f, 2.8f)));
            Assert.IsFalse(transport.ContainsFeet(platform.position + origin.rotation * new Vector3(1.9f, 0.1f, 0)));
            Assert.IsFalse(transport.ContainsFeet(origin.position));
            Near(transport.Position(-1), origin.position);
            Near(transport.Position(2), destination.position);
        }

        [Test]
        public void Passenger_InsideEdgeCountsEvenOutsideTheBoardingInset()
        {
            var feet = new Vector3(1.99f, 0, 2.99f);
            Assert.IsFalse(transport.ContainsFeet(feet));
            Assert.IsTrue(transport.ContainsPassenger(feet));
            Assert.IsTrue(transport.ContainsPassenger(new Vector3(-1.99f, 0, -2.99f)));
        }

        [Test]
        public void Passenger_ExactlyOnOrBeyondAnyPlatformEdgeDoesNotCount()
        {
            foreach (float direction in new[] { -1f, 1f })
            {
                Assert.IsFalse(transport.ContainsPassenger(new Vector3(direction * 2f, 0, 0)));
                Assert.IsFalse(transport.ContainsPassenger(new Vector3(direction * 2.01f, 0, 0)));
                Assert.IsFalse(transport.ContainsPassenger(new Vector3(0, 0, direction * 3f)));
                Assert.IsFalse(transport.ContainsPassenger(new Vector3(0, 0, direction * 3.01f)));
            }
        }

        [Test]
        public void Passenger_JumpAndTrampolineRiderCountButFallenOrHighFlyerDoesNot()
        {
            foreach (float height in new[] { -0.15f, 0f, 0.5f, 1.2f, 2f })
                Assert.IsTrue(transport.ContainsPassenger(Vector3.up * height), "탑승 높이 " + height);
            Assert.IsFalse(transport.ContainsPassenger(Vector3.down * 0.151f));
            Assert.IsFalse(transport.ContainsPassenger(Vector3.up * 2.001f));
        }

        [Test]
        public void Passenger_UsesRotatedMovingPlatformPositionAndLocalWidth()
        {
            origin.SetPositionAndRotation(new Vector3(12, 2, -8), Quaternion.Euler(0, 90, 0));
            destination.position = new Vector3(12, 6, 12);
            transport.Present(0.5f);
            Assert.IsTrue(transport.ContainsPassenger(platform.position + origin.rotation * new Vector3(1.9f, 1.2f, 2.9f)));
            Assert.IsFalse(transport.ContainsPassenger(platform.position + origin.rotation * new Vector3(2.1f, 1.2f, 0)));
            Assert.IsFalse(transport.ContainsPassenger(platform.position + Vector3.down * 0.2f));
            Assert.IsFalse(transport.ContainsPassenger(origin.position));
        }

        [Test]
        public void TrapOpening_RemovesBothSupportsImmediatelyBeforePanelsRotate()
        {
            CreateTrapDoors(out var left, out var right, out var floors);
            Assert.IsTrue(transport.HasTrapDoors);
            Assert.IsTrue(floors[0].enabled);
            Assert.IsTrue(floors[1].enabled);
            transport.PresentTrap(true, 10, 10);
            Assert.IsFalse(floors[0].enabled);
            Assert.IsFalse(floors[1].enabled);
            Near(left.localRotation * Vector3.right, Vector3.right);
            Near(right.localRotation * Vector3.left, Vector3.left);
        }

        [Test]
        public void FallColumn_CoversDescendingTrampolineButNotAdjacentGroundOrHighFlyers()
        {
            origin.SetPositionAndRotation(new Vector3(12, 2, -8), Quaternion.Euler(0, 90, 0));
            transport.Present(0.5f);
            foreach (float height in new[] { 2f, 0.5f, -0.5f, -4f })
                Assert.IsTrue(transport.ContainsFallColumn(platform.position + origin.rotation * new Vector3(1.9f, height, 2.9f)));
            foreach (var outside in new[] { new Vector3(2.01f, -2, 0), new Vector3(0, -2, 3.01f), new Vector3(0, 2.01f, 0) })
                Assert.IsFalse(transport.ContainsFallColumn(platform.position + origin.rotation * outside));
        }

        [Test]
        public void TrapOpening_HalfwayTiltsBothInnerEdgesDownSymmetrically()
        {
            CreateTrapDoors(out var left, out var right, out var floors);
            transport.PresentTrap(true, 10, 10.175);
            float diagonal = Mathf.Sqrt(0.5f);
            Near(left.localRotation * Vector3.right, new Vector3(diagonal, -diagonal, 0));
            Near(right.localRotation * Vector3.left, new Vector3(-diagonal, -diagonal, 0));
            Assert.IsFalse(floors[0].enabled);
            Assert.IsFalse(floors[1].enabled);
        }

        [Test]
        public void TrapOpening_ReachesVerticalAtDurationAndStaysThereOnRotatedPlatform()
        {
            CreateTrapDoors(out var left, out var right, out _);
            origin.rotation = Quaternion.Euler(0, 90, 0);
            transport.Present(0.5f);
            foreach (double now in new[] { 10.35, 20d })
            {
                transport.PresentTrap(true, 10, now);
                Near(left.TransformDirection(Vector3.right), Vector3.down);
                Near(right.TransformDirection(Vector3.left), Vector3.down);
                Near(left.localPosition, new Vector3(-2, 0, 0));
                Near(right.localPosition, new Vector3(2, 0, 0));
            }
        }

        [Test]
        public void TrapClosing_RestoresLevelPanelsAndBothColliders()
        {
            CreateTrapDoors(out var left, out var right, out var floors);
            origin.rotation = Quaternion.Euler(0, 90, 0);
            transport.Present(0.5f);
            transport.PresentTrap(true, 10, 20);
            transport.PresentTrap(false, 0, 0);
            Near(left.localRotation * Vector3.right, Vector3.right);
            Near(right.localRotation * Vector3.left, Vector3.left);
            Near(left.TransformDirection(Vector3.right), platform.right);
            Near(right.TransformDirection(Vector3.left), -platform.right);
            Assert.IsTrue(floors[0].enabled);
            Assert.IsTrue(floors[1].enabled);
        }

        [Test]
        public void OccupiedIndicator_TintsOnlyFloorRenderersAndRestoresIdleWithoutChangingSharedMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                int baseColor = Shader.PropertyToID("_BaseColor");
                material.SetColor(baseColor, new Color(0.1f, 0.55f, 0.95f, 1));
                Color idle = material.GetColor(baseColor);
                var leftFloor = Child("LeftFloor").gameObject.AddComponent<MeshRenderer>();
                var rightFloor = Child("RightFloor").gameObject.AddComponent<MeshRenderer>();
                var decoration = Child("Decoration").gameObject.AddComponent<MeshRenderer>();
                foreach (var renderer in new[] { leftFloor, rightFloor, decoration }) renderer.sharedMaterial = material;
                Set(transport, "boardingIndicators", new Renderer[] { leftFloor, rightFloor });
                var block = new MaterialPropertyBlock();
                foreach (bool occupied in new[] { true, false })
                {
                    transport.PresentOccupied(occupied);
                    foreach (var floor in new[] { leftFloor, rightFloor })
                    {
                        floor.GetPropertyBlock(block);
                        Assert.That(Vector4.Distance(block.GetColor(baseColor), occupied ? HumanCannonTransport.OccupiedColor : idle), Is.LessThan(0.00001f));
                        Assert.That(floor.sharedMaterial, Is.SameAs(material));
                    }
                    decoration.GetPropertyBlock(block);
                    Assert.IsTrue(block.isEmpty, "바닥 외 장식은 탑승 색상을 적용하지 않습니다.");
                    Assert.That(decoration.sharedMaterial, Is.SameAs(material));
                    Assert.That(material.GetColor(baseColor), Is.EqualTo(idle));
                }
            }
            finally { Object.DestroyImmediate(material); }
        }

        [TestCase(1f, 0f)]
        [TestCase(-1f, 0f)]
        [TestCase(0f, 1f)]
        [TestCase(0f, -1f)]
        public void Load_RejectsAnyCornerOutsideEvenWhenHandleIsInside(float x, float z)
        {
            Set(transport, "halfSize", new Vector2(2, 2));
            trampoline.transform.position = new Vector3(x * 0.94f, 0, z * 0.94f);
            Assert.IsTrue(transport.ContainsLoad(trampoline));
            trampoline.transform.position = new Vector3(x * 0.96f, 0, z * 0.96f);
            Assert.IsTrue(transport.ContainsFeet(trampoline.Handle.position));
            Assert.IsFalse(transport.ContainsLoad(trampoline));
        }

        [Test]
        public void Load_UsesAllRotatedCornersAndRequiresHandleOnPlatform()
        {
            Set(transport, "halfSize", new Vector2(2, 2));
            Set(trampoline, "halfSize", new Vector2(1.8f, 0.4f));
            trampoline.transform.rotation = Quaternion.Euler(0, 45, 0);
            Assert.IsTrue(transport.ContainsLoad(trampoline));
            trampoline.transform.position = Vector3.right * 0.5f;
            Assert.IsTrue(transport.ContainsFeet(trampoline.Handle.position));
            Assert.IsFalse(transport.ContainsLoad(trampoline));
            trampoline.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            trampoline.Handle.localPosition = Vector3.right * 1.9f;
            Assert.IsFalse(transport.ContainsLoad(trampoline));
        }

        [Test]
        public void Load_RejectsCornerHeightOutsidePlatformEvenWithHandleAtFeet()
        {
            trampoline.transform.rotation = Quaternion.Euler(30, 0, 0);
            Assert.IsTrue(transport.ContainsFeet(trampoline.Handle.position));
            Assert.IsFalse(transport.ContainsLoad(trampoline));
            Assert.IsFalse(transport.ContainsLoad(null));
        }

        [Test]
        public void Fire_FirstDepartureArmsOnceAndLaterDeparturesDoNotDelayWarning()
        {
            Assert.IsFalse(fire.Armed);
            Assert.IsFalse(fire.Threatens(Vector3.zero, 0.3f, 100));
            Assert.IsTrue(fire.Arm(10));
            Assert.IsFalse(fire.Arm(14));
            Assert.That(fire.ArmedAt, Is.EqualTo(10));
            Assert.That(fire.RemainingWarning(14), Is.EqualTo(1));
            Assert.That(fire.EndTime, Is.EqualTo(25));
            Assert.IsFalse(fire.Advancing(14.999));
            Assert.IsFalse(fire.Threatens(Vector3.zero, 0.3f, 14.999));
            Assert.IsTrue(fire.Advancing(15));
            Assert.IsTrue(fire.Threatens(Vector3.zero, 0.3f, 15));
        }

        [Test]
        public void Fire_PressuresGroundAndAirButLeavesUnreachedAreaAndBoundarySafe()
        {
            fire.Arm(10);
            Near(fire.Front(20), Vector3.forward * 10);
            Assert.IsTrue(fire.Threatens(new Vector3(0, 0, 3), 0.3f, 20));
            Assert.IsTrue(fire.Threatens(new Vector3(0, 9, 3), 0.3f, 20));
            Assert.IsFalse(fire.Threatens(new Vector3(0, 11, 3), 0.3f, 20));
            Assert.IsFalse(fire.Threatens(new Vector3(3.31f, 0, 3), 0.3f, 20));
            Assert.IsFalse(fire.Threatens(new Vector3(0, 0, 10.31f), 0.3f, 20));
            Assert.IsFalse(fire.Threatens(new Vector3(0, 0, -0.31f), 0.3f, 20));
            Assert.IsTrue(fire.Threatens(new Vector3(0, 0, 19.9f), 0.3f, 25));
            Assert.IsFalse(fire.Threatens(new Vector3(0, 0, 20), 0.3f, 25));
            Assert.IsFalse(fire.Threatens(new Vector3(0, 0, 21), 0.3f, 25));
        }

        [Test]
        public void Fire_TranslatedQuarterTurnUsesItsRouteDirectionAndWidth()
        {
            fireStart.SetPositionAndRotation(new Vector3(20, 3, 7), Quaternion.Euler(0, 90, 0));
            boundary.position = fireStart.position + Vector3.right * 20;
            fire.Arm(10);
            Near(fire.Front(20), fireStart.position + Vector3.right * 10);
            Assert.IsTrue(fire.Threatens(fireStart.position + new Vector3(5, 8, 2), 0.3f, 20));
            Assert.IsFalse(fire.Threatens(fireStart.position + new Vector3(5, 0, 3.31f), 0.3f, 20));
            Assert.IsFalse(fire.Threatens(boundary.position, 0.3f, 25));
        }

        [Test]
        public void Fire_FinishesOnceAtBoundaryAndResetRequiresNewDeparture()
        {
            fire.Arm(10);
            Assert.IsFalse(fire.Tick(24.999));
            Assert.IsTrue(fire.Tick(25));
            Assert.IsTrue(fire.Finished);
            Assert.IsFalse(fire.Tick(30));
            Assert.IsFalse(fire.Threatens(Vector3.forward * 5, 0.3f, 30));
            Assert.IsFalse(fire.Arm(30));
            fire.Present(30);
            Assert.IsFalse(visual.gameObject.activeSelf);
            fire.ResetWall();
            Assert.IsFalse(fire.Armed);
            Assert.IsFalse(fire.Finished);
            Assert.That(fire.ArmedAt, Is.EqualTo(-1));
            Assert.IsFalse(fire.Tick(100));
            Assert.IsFalse(fire.Threatens(Vector3.zero, 0.3f, 100));
            Assert.IsTrue(fire.Arm(100));
            Assert.IsFalse(fire.Tick(25));
            Assert.IsFalse(fire.Advancing(25));
            Assert.IsFalse(fire.Threatens(Vector3.zero, 0.3f, 25));
            Assert.That(fire.ArmedAt, Is.EqualTo(100));
            Assert.That(fire.EndTime, Is.EqualTo(115));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        [TestCase(-1d)]
        public void Fire_InvalidClockCannotArmAdvanceOrFinish(double now)
        {
            Assert.IsFalse(fire.Arm(now));
            Assert.IsTrue(fire.Arm(10));
            Assert.IsFalse(fire.Advancing(now));
            Assert.IsFalse(fire.Threatens(Vector3.zero, 0.3f, now));
            Assert.IsFalse(fire.Tick(now));
            Assert.IsFalse(fire.Finished);
            Assert.That(fire.ArmedAt, Is.EqualTo(10));
        }

        void CreateTrapDoors(out Transform left, out Transform right, out Collider[] floors)
        {
            left = Child("LeftHinge");
            right = Child("RightHinge");
            left.SetParent(platform, false);
            right.SetParent(platform, false);
            left.localPosition = new Vector3(-2, 0, 0);
            right.localPosition = new Vector3(2, 0, 0);
            var leftPanel = Child("LeftPanel");
            var rightPanel = Child("RightPanel");
            leftPanel.SetParent(left, false);
            rightPanel.SetParent(right, false);
            leftPanel.localPosition = Vector3.right;
            rightPanel.localPosition = Vector3.left;
            floors = new Collider[] { leftPanel.gameObject.AddComponent<BoxCollider>(), rightPanel.gameObject.AddComponent<BoxCollider>() };
            foreach (var floor in floors) ((BoxCollider)floor).size = new Vector3(2, 0.3f, 6);
            Set(transport, "leftHinge", left);
            Set(transport, "rightHinge", right);
            Set(transport, "trapColliders", floors);
            Set(transport, "doorOpenSeconds", 0.35f);
        }

        Transform Child(string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(root.transform, false);
            return child;
        }

        static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, PrivateInstance).SetValue(target, value);

        static void Near(Vector3 actual, Vector3 expected) =>
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.0001f));
    }
}
