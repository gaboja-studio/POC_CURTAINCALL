using System.Reflection;
using CurtainCall.Settings;
using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    public sealed class HumanCannonPoleSawTests
    {
        GameObject root;
        HumanCannonPole pole;
        HumanCannonPoleSaw saw;
        HumanCannonHazard hazard;
        Transform bar;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("PoleSawTest");
            var poleObject = new GameObject("Pole");
            poleObject.transform.SetParent(root.transform);
            poleObject.transform.SetPositionAndRotation(new Vector3(4, 8, 12), Quaternion.Euler(0, 90, 0));
            pole = poleObject.AddComponent<HumanCannonPole>();
            bar = new GameObject("VisualBar").transform;
            bar.SetParent(pole.transform, false);
            Set(pole, "visualBar", bar);
            var sawObject = new GameObject("IndependentSaw");
            sawObject.transform.SetParent(root.transform, false);
            hazard = sawObject.AddComponent<HumanCannonHazard>();
            Set(hazard, "kind", CannonHazardKind.GroundSaw);
            Set(hazard, "travel", new Vector3(3, 0, 0));
            Set(hazard, "periodSeconds", 4f);
            Set(hazard, "radius", 0.55f);
            Set(hazard, "bladeHalfThickness", 0.12f);
            saw = sawObject.AddComponent<HumanCannonPoleSaw>();
            saw.Configure(pole, hazard);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void DiagonalSweepOnlyAppliesAfterThePoleSawExceptionIsRemoved()
        {
            Vector3 original = hazard.Travel;
            hazard.SetVisible(true, 0);
            AssertVector(hazard.Travel, original);
            Object.DestroyImmediate(saw);
            hazard.SetVisible(true, 0);
            Vector3 first = hazard.Travel;
            Assert.That(Vector3.Angle(first, original), Is.EqualTo(30f).Within(.001f));
            hazard.SetVisible(true, 1);
            Vector3 second = hazard.Travel;
            Assert.That(Vector3.Angle(second, original), Is.EqualTo(30f).Within(.001f));
            Assert.That(Vector3.Angle(first, second), Is.EqualTo(60f).Within(.001f));
        }

        [TestCase(-65f)]
        [TestCase(65f)]
        public void SwingingBar_DoesNotDragSawAwayFromFixedTorsoPlane(float angle)
        {
            Vector3 center = pole.HangingPosition(0) + Vector3.up * GameSettings.Base.Character.CapsuleHeight * 0.5f;
            Assert.That(pole.HoldSeconds, Is.Zero, "새 봉의 기본은 시간 제한 없음");
            AssertVector(hazard.transform.position, center);
            pole.PresentSwing(angle);
            Assert.IsTrue(saw.AlignToPole());
            Assert.That(Vector3.Distance(bar.position, pole.Grip.position), Is.GreaterThan(1));
            AssertVector(hazard.transform.position, center);
            AssertVector(hazard.transform.forward, pole.Forward);
        }

        [Test]
        public void IndependentSaw_KeepsCrossingWhileLinkedGateIsHeldIndefinitely()
        {
            var gate = new PoleGateState();
            Assert.IsTrue(gate.TryGrab(7, 0, 0, 0));
            Vector3 center = saw.RestCenter;
            Vector3 side = hazard.transform.right * 3;
            AssertVector(hazard.Position(1, 1), center + side);
            AssertVector(hazard.Position(3, 1), center - side);
            AssertVector(hazard.Position(4, 1), center);
            AssertVector(hazard.Position(5, 1), center + side);
            Assert.IsFalse(gate.Tick(1000, 2));
            Assert.That(gate.Phase, Is.EqualTo(PoleSawPhase.Retracted));
            Assert.IsTrue(gate.IsUnlimitedHold);
        }

        [TestCase(-50f)]
        [TestCase(50f)]
        public void NeutralTorsoIsHit_ButForwardAndBackwardSwingClearTheSaw(float angle)
        {
            float height = GameSettings.Base.Character.CapsuleHeight;
            float radius = GameSettings.Base.Character.CapsuleRadius;
            Assert.IsTrue(hazard.Touches(pole.HangingPosition(0), height, radius, 0, 1), "중립 자세의 몸통을 지난다.");
            Assert.IsFalse(hazard.Touches(pole.HangingPosition(angle), height, radius, 0, 1), "W/S 반동으로 앞뒤 어느 쪽이든 회피 가능하다.");
        }

        [Test]
        public void ExplicitTorsoHeightAndRelocatedPole_KeepTheRailAtRestPose()
        {
            pole.transform.SetPositionAndRotation(new Vector3(-3, 15, 22), Quaternion.Euler(0, -45, 0));
            saw.Configure(pole, hazard, 0.45f);
            AssertVector(hazard.transform.position, pole.HangingPosition(0) + Vector3.up * 0.45f);
            AssertVector(hazard.Travel.normalized, Quaternion.Euler(0, -45, 0) * Vector3.right);
        }

        [Test]
        public void MapWideSweep_UsesBothMapEdgesRegardlessOfPoleOffset_AndUpdatesRail()
        {
            var rail = hazard.gameObject.AddComponent<LineRenderer>();
            rail.useWorldSpace = false;
            pole.transform.rotation = Quaternion.identity;
            foreach (float x in new[] { -7f, 0f, 7f })
            {
                pole.transform.position = new Vector3(x, 8, 12);
                saw.ConfigureAcrossMap(pole, hazard, -15, 15, 4);
                Assert.That(hazard.Position(1, 1).x, Is.EqualTo(15).Within(0.001f));
                Assert.That(hazard.Position(3, 1).x, Is.EqualTo(-15).Within(0.001f));
                Assert.That(hazard.PeriodSeconds, Is.EqualTo(4));
                AssertVector(hazard.transform.position, saw.RailCenter);
                AssertVector(rail.transform.TransformPoint(rail.GetPosition(0)), saw.RailCenter + Vector3.left * 15);
                AssertVector(rail.transform.TransformPoint(rail.GetPosition(1)), saw.RailCenter + Vector3.right * 15);
                pole.PresentSwing(50);
                saw.AlignToPole();
                Assert.That(hazard.transform.position.x, Is.Zero.Within(0.0001f));
                Assert.That(hazard.transform.position.z, Is.EqualTo(12).Within(0.0001f));
            }
            float centerSpeed = Vector3.Distance(hazard.Position(0, 1), hazard.Position(0.01, 1)) / 0.01f;
            Assert.That(centerSpeed, Is.GreaterThan(20), "기존 3m/4.5초 왕복보다 빠르게 코스 전체를 지난다.");
        }

        [Test]
        public void FasterFullWidthSaw_SweptSamplesStillDetectCrossingBetweenFrames()
        {
            pole.transform.SetPositionAndRotation(new Vector3(0, 8, 12), Quaternion.identity);
            saw.ConfigureAcrossMap(pole, hazard, -15, 15, 4);
            Vector3 feet = pole.HangingPosition(0);
            float height = GameSettings.Base.Character.CapsuleHeight;
            float radius = GameSettings.Base.Character.CapsuleRadius;
            const double before = 1.9, after = 2.1;
            Assert.IsFalse(hazard.Touches(feet, height, radius, before, 1));
            Assert.IsFalse(hazard.Touches(feet, height, radius, after, 1));
            int steps = hazard.SweepSteps(feet, feet, after - before, 1);
            bool touched = false;
            for (int i = 0; i <= steps; i++)
                touched |= hazard.Touches(feet, height, radius, before + (after - before) * i / steps, 1);
            Assert.IsTrue(touched, "빠른 톱이 프레임 사이를 통과해도 호스트 접촉 검사에 잡힌다.");
        }

        [Test]
        public void MissingReferenceOrAncestorTarget_DoesNotMoveThePole()
        {
            Vector3 original = pole.transform.position;
            saw.Configure(null, hazard);
            Assert.IsFalse(saw.AlignToPole());
            var incorrect = root.AddComponent<HumanCannonHazard>();
            saw.Configure(pole, incorrect);
            Assert.IsFalse(saw.AlignToPole(), "봉 자신이나 부모를 이동시키는 잘못된 연결을 거부한다.");
            AssertVector(pole.transform.position, original);
        }

        static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        static void AssertVector(Vector3 actual, Vector3 expected) =>
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.0001f));
    }
}
