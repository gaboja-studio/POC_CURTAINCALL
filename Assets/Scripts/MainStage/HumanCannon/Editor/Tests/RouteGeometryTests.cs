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

        [TestCase(0f)]
        [TestCase(45f)]
        [TestCase(90f)]
        public void RoundLoad_FitsIndependentlyOfYaw(float yaw)
        {
            Set(trampoline, "circularSurface", true);
            Set(trampoline, "loadHalfSize", Vector2.one * 1.4f);
            trampoline.transform.rotation = Quaternion.Euler(0, yaw, 0);
            Assert.IsTrue(transport.ContainsLoad(trampoline));
        }

        [Test]
        public void LoadCheck_UsesOuterFrameRatherThanOnlyTheBounceFabric()
        {
            Set(trampoline, "circularSurface", true);
            Set(trampoline, "loadHalfSize", Vector2.one * 1.4f);
            trampoline.transform.position = Vector3.right * .7f;
            Assert.IsFalse(transport.ContainsLoad(trampoline));
            Set(trampoline, "loadHalfSize", Vector2.zero);
            Assert.IsTrue(transport.ContainsLoad(trampoline));
        }

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
        public void Barrier_SurroundsPlatformOnlyWhileSealedWithoutRoof()
        {
            transport.PresentBarrier(false);
            Assert.IsFalse(transport.BarrierActive);
            transport.PresentBarrier(true);
            Assert.IsTrue(transport.BarrierActive);
            var walls = platform.Find("BoardingBarrier").GetComponents<BoxCollider>();
            Assert.That(walls.Length, Is.EqualTo(4));
            foreach (var wall in walls)
            {
                // 벽은 발판 영역 바깥에만 있어 운반자·트램폴린과 겹치지 않는다(비활성 루트라 bounds 대신 직접 계산).
                var center = wall.transform.TransformPoint(wall.center);
                Assert.IsFalse(transport.ContainsPassenger(center), center.ToString());
                Assert.That(center.y - wall.size.y / 2, Is.EqualTo(platform.position.y).Within(.0001f));
            }
            transport.PresentBarrier(false);
            Assert.IsFalse(transport.BarrierActive);
        }

        [Test]
        public void OutsideBarrier_MovesStowawayPastNearestSideWall()
        {
            origin.rotation = Quaternion.Euler(0, 90, 0);
            transport.Present(0);
            Vector3 Local(Vector3 world) => Quaternion.Inverse(origin.rotation) * (world - platform.position);
            float clear = HumanCannonTransport.BarrierThickness + 0.5f;
            var x = Local(transport.OutsideBarrier(platform.position + origin.rotation * new Vector3(1.5f, 1, 0.5f), 0.5f));
            Assert.That(x.x, Is.GreaterThan(2 + clear));
            Assert.That(x.z, Is.EqualTo(0.5f).Within(.0001f));
            Assert.That(x.y, Is.EqualTo(1f).Within(.0001f));
            var z = Local(transport.OutsideBarrier(platform.position + origin.rotation * new Vector3(0.2f, 0, -2.8f), 0.5f));
            Assert.That(z.z, Is.LessThan(-(3 + clear)));
            Assert.IsFalse(transport.ContainsPassenger(platform.position + origin.rotation * z));
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
        public void AirColumn_CoversHighTrampolineBounceButNotOutsideOrBelow()
        {
            origin.SetPositionAndRotation(new Vector3(12, 2, -8), Quaternion.Euler(0, 90, 0));
            transport.Present(0.5f);
            foreach (float height in new[] { -0.1f, 2f, 6f, 30f })
                Assert.IsTrue(transport.ContainsAirColumn(platform.position + origin.rotation * new Vector3(1.9f, height, 2.9f)), "높이 " + height);
            foreach (var outside in new[] { new Vector3(2.01f, 6, 0), new Vector3(0, 6, 3.01f), new Vector3(0, -0.2f, 0) })
                Assert.IsFalse(transport.ContainsAirColumn(platform.position + origin.rotation * outside));
        }

        [Test]
        public void BoardingSide_IsBehindTheStartPlatformOppositeTheTravel()
        {
            origin.SetPositionAndRotation(new Vector3(12, 2, -8), Quaternion.Euler(0, 90, 0));
            destination.position = new Vector3(30, 2, -8);
            transport.Present(0);
            Vector3 side = transport.BoardingSide(.5f);
            Assert.IsFalse(transport.ContainsPassenger(side), "발판 밖이다.");
            Assert.That(side.y, Is.EqualTo(origin.position.y).Within(.001f));
            Assert.That(Vector3.Dot(side - origin.position, Vector3.right), Is.LessThan(-transport.HalfSize.y), "진행 반대쪽 갑판이다.");
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
                foreach (var (occupied, rejected, expected) in new[] { (true, false, HumanCannonTransport.OccupiedColor),
                    (false, true, HumanCannonTransport.RejectedColor), (false, false, idle) })
                {
                    transport.PresentOccupied(occupied, rejected);
                    foreach (var floor in new[] { leftFloor, rightFloor })
                    {
                        floor.GetPropertyBlock(block);
                        Assert.That(Vector4.Distance(block.GetColor(baseColor), expected), Is.LessThan(0.00001f));
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

        void UseStages()
        {
            var second = Child("Stage2"); second.position = Vector3.forward * 5;
            var third = Child("Stage3"); third.position = Vector3.forward * 12;
            Set(fire, "stageSpeedMultipliers", new[] { 1.5f, 2f });
            Set(fire, "stageLines", new[] { second, third });
        }

        [Test]
        public void FireStages_SpeedUpFromArrivalTimeWithoutJumpingForwardAndShortenTheEnd()
        {
            UseStages();
            Assert.IsFalse(fire.Boost(0, 9), "켜지기 전에는 단계가 오르지 않는다");
            fire.Arm(10);
            Assert.IsTrue(fire.Boost(0, 20));
            Assert.IsFalse(fire.Boost(0, 21), "같은 단계는 한 번만");
            Near(fire.Front(20), Vector3.forward * 10);
            Near(fire.Front(22), Vector3.forward * 16);
            Assert.That(fire.EndTime, Is.EqualTo(20 + 10 / 3d).Within(1e-9));
            Assert.IsTrue(fire.Boost(1, 21));
            Near(fire.Front(22), Vector3.forward * 17);
            Assert.That(fire.EndTime, Is.EqualTo(22.75).Within(1e-9));
            Assert.That(fire.CurrentStage(22), Is.EqualTo(2));
            Assert.That(fire.SpeedAt(22), Is.EqualTo(4).Within(1e-6));
            Assert.IsFalse(fire.Tick(22.7));
            Assert.IsTrue(fire.Tick(22.75));
            fire.ResetWall();
            Assert.That(fire.StageAt(0), Is.EqualTo(-1));
            Assert.That(fire.StageAt(1), Is.EqualTo(-1));
        }

        [Test]
        public void FireStages_SkippedStageRisesTogether_AndArrivalDuringWarningStartsTheChaseAtOnce()
        {
            UseStages();
            fire.Arm(0);
            Assert.IsTrue(fire.Boost(1, 7));
            Assert.That(fire.StageAt(0), Is.EqualTo(7));
            Assert.That(fire.AdvanceAt, Is.EqualTo(5), "예고가 끝난 뒤 도착은 출발 시각을 바꾸지 않는다");
            Assert.That(fire.Travelled(8), Is.EqualTo(2 * 2 + 1 * 4).Within(1e-9));
            fire.ResetWall();
            fire.Arm(0);
            Assert.IsFalse(fire.Advancing(1.9));
            Assert.IsTrue(fire.Boost(0, 2));
            // 예고 5초 중 2초에 전원 도착 → 남은 3초 예고를 건너뛰고 2구간 속도로 바로 전진.
            Assert.That(fire.AdvanceAt, Is.EqualTo(2));
            Assert.That(fire.RemainingWarning(2), Is.EqualTo(0));
            Assert.IsTrue(fire.Advancing(2));
            Assert.That(fire.Travelled(2), Is.EqualTo(0));
            Assert.That(fire.Travelled(5), Is.EqualTo(9).Within(1e-9));
            Assert.That(fire.EndTime, Is.EqualTo(2 + 20 / 3d).Within(1e-9));
            fire.Receive(0, false, new[] { 2d, 4d });
            Assert.That(fire.Travelled(6), Is.EqualTo(2 * 3 + 2 * 4).Within(1e-9));
        }

        [Test]
        public void FireStages_ArrivalUsesTheFireDirectionLine()
        {
            UseStages();
            Assert.IsFalse(fire.Reached(0, new Vector3(0, 0, 4.99f)));
            Assert.IsTrue(fire.Reached(0, new Vector3(3, 6, 5)));
            Assert.IsFalse(fire.Reached(1, new Vector3(0, 0, 11.9f)));
            Assert.IsTrue(fire.Reached(1, new Vector3(0, 0, 30)));
            Assert.IsFalse(fire.Reached(2, new Vector3(0, 0, 30)));
        }

        [Test]
        public void FireEffect_ReplacesBoxAtTheFrontLine_WeakDuringWarningAndHiddenAfterward()
        {
            var effect = Child("Effect");
            var flames = Child("Flames"); flames.SetParent(effect, false);
            var system = flames.gameObject.AddComponent<ParticleSystem>();
            var emission = system.emission; emission.rateOverTimeMultiplier = 100;
            visual.gameObject.AddComponent<MeshRenderer>();
            Set(fire, "effect", effect);
            Set(fire, "appearSeconds", 1f);
            fire.Arm(10);
            fire.Present(12);
            Assert.IsTrue(effect.gameObject.activeSelf);
            Assert.IsFalse(visual.GetComponent<MeshRenderer>().enabled);
            Near(effect.position, fireStart.position);
            Assert.That(effect.localScale.x, Is.EqualTo(6).Within(1e-5));
            Assert.That(system.emission.rateOverTimeMultiplier, Is.EqualTo(35).Within(0.01), "예고 중 약하게");
            fire.Present(20);
            Near(effect.position, Vector3.forward * 10);
            Assert.That(system.emission.rateOverTimeMultiplier, Is.EqualTo(100).Within(0.01));
            fire.ResetWall();
            Assert.IsFalse(effect.gameObject.activeSelf);
        }

        [Test]
        public void FireEffect_GrowsFromSmallWhenItAppears()
        {
            var effect = Child("Effect");
            var flames = Child("Flames"); flames.SetParent(effect, false);
            var system = flames.gameObject.AddComponent<ParticleSystem>();
            var main = system.main; main.startSizeMultiplier = 4; main.startSpeedMultiplier = 10; main.prewarm = true;
            var emission = system.emission; emission.rateOverTimeMultiplier = 100;
            var light = Child("Glow").gameObject.AddComponent<Light>(); light.intensity = 30;
            light.transform.SetParent(effect, false);
            Set(fire, "effect", effect);
            Set(fire, "appearSeconds", 4f);
            Set(fire, "appearStartScale", 0.1f);
            Assert.AreEqual(0f, fire.Appear(10), "켜지기 전");
            fire.Arm(10);
            fire.Present(10);
            Assert.That(system.main.startSizeMultiplier, Is.EqualTo(0.4f).Within(1e-4), "나타나는 순간 작게");
            Assert.That(system.main.startSpeedMultiplier, Is.EqualTo(1f).Within(1e-4));
            Assert.That(light.intensity, Is.EqualTo(0).Within(1e-4));
            Assert.IsTrue(effect.gameObject.activeSelf);
            Assert.IsFalse(system.main.prewarm, "켜질 때 다 큰 불꽃을 미리 채우지 않는다");
            fire.Present(12);
            Assert.That(system.main.startSizeMultiplier, Is.InRange(0.41f, 3.99f), "점점 커진다");
            Assert.That(fire.Appear(12), Is.EqualTo(0.5f).Within(1e-4));
            fire.Present(14);
            Assert.That(system.main.startSizeMultiplier, Is.EqualTo(4f).Within(1e-4));
            Assert.That(system.main.startSpeedMultiplier, Is.EqualTo(10f).Within(1e-4));
            Assert.That(light.intensity, Is.EqualTo(30).Within(1e-4));
            Assert.That(system.emission.rateOverTimeMultiplier, Is.EqualTo(35).Within(0.01), "예고 중 방출은 그대로");
            fire.ResetWall();
            fire.Arm(10);
            fire.Present(15);
            Assert.IsTrue(system.main.prewarm, "다 자란 뒤 다시 보이면 원래 미리 채우기");
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

        [TestCase(CannonHazardKind.FireJet, false)]

        [TestCase(CannonHazardKind.FallingLoad, true)]
        public void LimbCutFireKeepsItsBoxContactAndUnrotatedVisual(CannonHazardKind kind, bool lethal)
        {
            var hazardRoot = Child("Contact hazard");
            var hazard = hazardRoot.gameObject.AddComponent<HumanCannonHazard>();
            var model = Child("Contact hazard visual");
            Set(hazard, "kind", kind);
            Set(hazard, "visual", model);
            Set(hazard, "boxSize", new Vector3(4, 3, 1));
            Set(hazard, "travel", Vector3.zero);
            Assert.That(hazard.KillsOnContact, Is.EqualTo(lethal));
            Assert.That(hazard.Touches(hazardRoot.position + Vector3.right * 1.8f, 1.5f, .29f, 3, 1), Is.True,
                "A nonlethal flame must retain the same full box footprint.");
            Assert.That(hazard.Touches(hazardRoot.position + Vector3.right * 3, 1.5f, .29f, 3, 1), Is.False);
            hazard.Present(3, 1);
            Assert.That(Quaternion.Angle(model.rotation, hazardRoot.rotation), Is.LessThan(.001f));
        }

        [Test]
        public void NonlethalFlamesAreNeverRemovedByTheSawDensitySetting()
        {
            var course = Child("Flame course").gameObject.AddComponent<HumanCannonHazardCourse>();
            var hazards = new HumanCannonHazard[10];
            for (int i = 0; i < hazards.Length; i++)
            {
                hazards[i] = Child("Flame " + i).gameObject.AddComponent<HumanCannonHazard>();
                Set(hazards[i], "kind", CannonHazardKind.FireJet);
            }
            Set(course, "hazards", hazards);
            Set(course, "useAuthoredHazardCount", false);
            for (int i = 0; i < hazards.Length; i++) Assert.That(course.IsHazardEnabled(i), Is.True);
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
