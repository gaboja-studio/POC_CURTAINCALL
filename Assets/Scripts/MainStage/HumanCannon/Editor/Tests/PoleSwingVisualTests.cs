using System.Reflection;
using CurtainCall.Settings;
using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    public sealed class PoleSwingVisualTests
    {
        GameObject root;
        HumanCannonPole pole;
        Transform grip, bar, leftCable, rightCable, leftAnchor, rightAnchor;
        string savedRules;

        [SetUp]
        public void SetUp()
        {
            savedRules = JsonUtility.ToJson(GameSettings.Poles);
            root = new GameObject("PoleSwingVisualTest");
            root.SetActive(false);
            root.transform.SetPositionAndRotation(new Vector3(3, 2, 5), Quaternion.Euler(0, 90, 0));
            pole = root.AddComponent<HumanCannonPole>();
            grip = Child("Grip", Vector3.zero);
            bar = Child("VisualBar", Vector3.zero);
            leftAnchor = Child("LeftAnchor", new Vector3(-2.2f, 8, 0));
            rightAnchor = Child("RightAnchor", new Vector3(2.2f, 8, 0));
            leftCable = Child("LeftCable", new Vector3(-2.2f, 4, 0));
            rightCable = Child("RightCable", new Vector3(2.2f, 4, 0));
            leftCable.localScale = rightCable.localScale = new Vector3(0.1f, 8, 0.1f);
            Set("grip", grip); Set("visualBar", bar);
            Set("leftCable", leftCable); Set("rightCable", rightCable);
            Set("leftAnchor", leftAnchor); Set("rightAnchor", rightAnchor);
        }

        [TearDown]
        public void TearDown()
        {
            // 일반 MonoBehaviour의 OnDestroy는 EditMode 모사에서 실행되지 않을 수 있다.
            // 실제 Unity 콜백은 WaitingRoomCommonPolePlayTests에서 별도로 검사한다.
            if (pole != null) DestroySharedVisual();
            if (root != null) Object.DestroyImmediate(root);
            JsonUtility.FromJsonOverwrite(savedRules, GameSettings.Poles);
        }

        [Test]
        public void LegacyHorizontalBoostAndUpwardCapCannotOverrideSharedLaunch()
        {
            Assert.IsNull(typeof(HumanCannonPole).GetField("maxReleaseUpwardSpeed",BindingFlags.Instance|BindingFlags.NonPublic));
            var original=pole.ReleaseVelocity(30,100);var point=pole.HangingPosition(30);
            Assert.IsNull(typeof(HumanCannonPole).GetField("horizontalReleaseMultiplier",BindingFlags.Instance|BindingFlags.NonPublic));
            var boosted=pole.ReleaseVelocity(30,100);
            Assert.That(Vector3.ProjectOnPlane(boosted,Vector3.up).magnitude,
                Is.EqualTo(Vector3.ProjectOnPlane(original,Vector3.up).magnitude).Within(.001f));
            Assert.That(boosted.y,Is.EqualTo(original.y).Within(.001f));
            Assert.That(pole.HangingPosition(30),Is.EqualTo(point));
        }

        [Test]
        public void ForwardAndBackwardSwing_MovesBarWithPlayerWhileGripAndCeilingStayFixed()
        {
            Vector3 fixedGrip = grip.position, fixedLeft = leftAnchor.position, fixedRight = rightAnchor.position;
            foreach (float angle in new[] { -55f, 55f })
            {
                Vector3 feet = pole.HangingPosition(angle);
                Vector3 release = pole.ReleaseVelocity(angle, 120);
                pole.PresentSwing(angle);
                Near(bar.position, feet + Vector3.up * 1.5f);
                Vector3 pivot = fixedGrip + Vector3.up * 1.5f;
                Near(bar.position, pivot + PoleSwingMotion.Offset(angle, 1.5f, pole.Forward));
                Assert.That(Vector3.Distance(pivot, bar.position), Is.EqualTo(1.5f).Within(0.001f));
                Near(grip.position, fixedGrip);
                Near(leftAnchor.position, fixedLeft); Near(rightAnchor.position, fixedRight);
                Near(pole.HangingPosition(angle), feet);
                Near(pole.ReleaseVelocity(angle, 120), release);
                AssertCable(leftCable, leftAnchor);
                AssertCable(rightCable, rightAnchor);
                Assert.That(leftCable.localScale.y, Is.GreaterThan(0f));
                Assert.That(rightCable.localScale.y, Is.GreaterThan(0f));
            }
        }

        [Test]
        public void RepeatedPresentation_DoesNotFeedBackIntoPhysics_AndZeroReturnsBarAndCables()
        {
            Vector3 expected = pole.HangingPosition(70) + Vector3.up * 1.5f;
            for (int frame = 0; frame < 100; frame++) pole.PresentSwing(70);
            Near(bar.position, expected);
            pole.PresentSwing(0);
            Near(bar.position, grip.position);
            AssertCable(leftCable, leftAnchor); AssertCable(rightCable, rightAnchor);
            Assert.That(leftCable.localScale.y, Is.EqualTo(8).Within(0.001f));
            Assert.That(rightCable.localScale.y, Is.EqualTo(8).Within(0.001f));
            Assert.That(leftCable.localScale.x, Is.EqualTo(0.1f));
            Assert.That(rightCable.localScale.z, Is.EqualTo(0.1f));
        }

        [TestCase(-35f, -70f)]
        [TestCase(35f, 70f)]
        public void ReleaseVelocity_IsDerivativeOfActualBodyArcPlusUpwardBoost(float angle, float speed)
        {
            const float deltaTime = 0.0001f;
            Vector3 finiteDifference = (pole.HangingPosition(angle + speed * deltaTime)
                - pole.HangingPosition(angle - speed * deltaTime)) / (2 * deltaTime);
            Vector3 tangent = pole.ReleaseVelocity(angle, speed) - Vector3.up * 6;
            Assert.That(Vector3.Distance(tangent, finiteDifference), Is.LessThan(0.02f));
            Vector3 pivot = grip.position + Vector3.up * 1.5f;
            Assert.That(Vector3.Dot(tangent, pole.BarPosition(angle) - pivot), Is.EqualTo(0).Within(0.001f));
            Assert.That(tangent.magnitude, Is.EqualTo(1.5f * Mathf.Abs(speed) * Mathf.Deg2Rad).Within(0.001f));
        }

        [Test]
        public void MissingCeilingAnchor_PreservesLegacyShortPendulum()
        {
            Set("rightAnchor", null);
            Assert.That(pole.SwingLength, Is.EqualTo(1.5f));
            Near(pole.HangingPosition(35), grip.position + PoleSwingMotion.Offset(35, 1.5f, pole.Forward));
            Near(pole.ReleaseVelocity(35, 70), PoleSwingMotion.Velocity(35, 70, 1.5f, pole.Forward) + Vector3.up * 6);
            pole.PresentSwing(35);
            Near(bar.position, pole.HangingPosition(35) + Vector3.up * 1.5f);
        }

        [Test]
        public void CeilingGeometryCannotChangeSharedGravityAndVelocity()
        {
            Assert.That(pole.SwingLength, Is.EqualTo(1.5f).Within(0.001f));
            float expectedAngle = 30, expectedSpeed = 0;
            PoleSwingMotion.Step(ref expectedAngle, ref expectedSpeed, 0, 0.1f, 9.81f, 1.5f, 300, 80, 180, 0.4f);
            float angle = 30, speed = 0;
            pole.Step(ref angle, ref speed, 0, 0.1f, 9.81f);
            Assert.That(angle, Is.EqualTo(expectedAngle).Within(0.0001f));
            Assert.That(speed, Is.EqualTo(expectedSpeed).Within(0.0001f));
            Near(pole.HangingPosition(0), grip.position - Vector3.up * 1.5f);
            Near(pole.ReleaseVelocity(0, 100), pole.Forward * (1.5f * 100 * Mathf.Deg2Rad) + Vector3.up * 6);
        }

        [TestCase(false, false)] [TestCase(false, true)]
        [TestCase(true, false)] [TestCase(true, true)]
        [Category("TutorialCommonRulesRegression")]
        public void SharedPrefab_KeepsConfiguredWidthAndCenteredGripUnderScaledParents(bool isSwitch, bool nonUniform)
        {
            JsonUtility.FromJsonOverwrite("{\"regularBarWidth\":1.8,\"switchBarWidth\":1.8}", GameSettings.Poles);
            var parent = new GameObject("Scaled scene placement");
            try
            {
                parent.transform.localScale = nonUniform ? new Vector3(3f, .4f, 2f) : Vector3.one * 2f;
                parent.transform.rotation = Quaternion.Euler(13, 31, 7);
                root.transform.SetParent(parent.transform, false);
                root.transform.localRotation = Quaternion.Euler(0, 53, 0);
                root.transform.localScale = nonUniform ? new Vector3(.7f, 1.9f, 1.1f) : Vector3.one;
                root.SetActive(true);
                pole.IsSwitch = isSwitch;
                RefreshSharedVisual();
                var visual = pole.CommonVisual;
                Assert.IsNull(visual.parent, "표시 루트는 배치 부모의 크기·전단을 물려받지 않는다.");
                Assert.AreEqual(root.scene, visual.gameObject.scene);
                foreach (float angle in new[] { 0f, -55f, 55f })
                {
                    pole.PresentSwing(angle);
                    Assert.That(ProjectedSize(pole.VisualBar, pole.VisualBar.right), Is.EqualTo(1.8f).Within(.001f));
                    Near(WorldMeshBounds(pole.VisualBar).center, pole.BarPosition(angle));
                    Assert.That(Quaternion.Angle(pole.VisualBar.rotation, grip.rotation), Is.LessThan(.001f));
                }
                // 배치 부모가 생성 후 변경되어도 외형 길이는 공통값이다.
                parent.transform.localScale = new Vector3(.3f, 4f, 1.7f);
                pole.PresentSwing(0);
                Assert.That(ProjectedSize(pole.VisualBar, pole.VisualBar.right), Is.EqualTo(1.8f).Within(.001f));
                AssertCommonCable(isSwitch ? "LeftSwingCable" : "LeftCable", "LeftAnchor");
                AssertCommonCable(isSwitch ? "RightSwingCable" : "RightCable", "RightAnchor");
                if (isSwitch)
                {
                    Assert.IsFalse(visual.Find("LeftCable").gameObject.activeSelf);
                    Assert.IsFalse(visual.Find("RightCable").gameObject.activeSelf);
                }
            }
            finally
            {
                root.transform.SetParent(null, true);
                Object.DestroyImmediate(parent);
            }
        }

        [TestCase(false)] [TestCase(true)]
        [Category("TutorialCommonRulesRegression")]
        public void SharedWidth_ResizesBarAndCableSpacingWithoutChangingCableHeightOrPhysics(bool isSwitch)
        {
            root.SetActive(true);
            pole.IsSwitch = isSwitch;
            RefreshSharedVisual();
            pole.PresentSwing(0);
            var visual = pole.CommonVisual;
            var anchor = visual.Find("LeftAnchor");
            var source = GameSettings.Poles.Visual(isSwitch).transform;
            float authoredWidth = ProjectedSize(source.Find("VisualBar"), source.Find("VisualBar").right);
            Assert.That(authoredWidth, Is.EqualTo(isSwitch ? 4.4f : 1.8f).Within(.001f), "기존 정본 외형의 실측 길이");
            float height = anchor.position.y - grip.position.y;
            var feet = pole.HangingPosition(35);
            var target = pole.GrabPoint(35, 0);
            var release = pole.ReleaseVelocity(35, 120);
            float reach = GameSettings.Poles.GrabRadius;
            string field = isSwitch ? "switchBarWidth" : "regularBarWidth";
            JsonUtility.FromJsonOverwrite("{\"" + field + "\":2.5}", GameSettings.Poles);
            pole.PresentSwing(0);
            Assert.That(ProjectedSize(pole.VisualBar, pole.VisualBar.right), Is.EqualTo(2.5f).Within(.001f));
            Assert.That(anchor.position.y - grip.position.y, Is.EqualTo(height).Within(.001f));
            float sourceSpacing = Vector3.Distance(source.Find("LeftAnchor").position, source.Find("RightAnchor").position);
            Assert.That(Vector3.Distance(anchor.position, visual.Find("RightAnchor").position),
                Is.EqualTo(sourceSpacing * 2.5f / authoredWidth).Within(.001f));
            Near(pole.HangingPosition(35), feet);
            Near(pole.GrabPoint(35, 0), target);
            Near(pole.ReleaseVelocity(35, 120), release);
            Assert.AreEqual(reach, GameSettings.Poles.GrabRadius);
            AssertCommonCable(isSwitch ? "LeftSwingCable" : "LeftCable", "LeftAnchor");
        }

        [Test]
        [Category("TutorialCommonRulesRegression")]
        public void SharedPrefab_PreservesAuthoredRootAndBarRotation()
        {
            var source = Object.Instantiate(GameSettings.Poles.Visual(false));
            try
            {
                source.transform.localRotation = Quaternion.Euler(0, 17, 0);
                var sourceBar = source.transform.Find("VisualBar");
                sourceBar.localRotation = Quaternion.Euler(0, 26, 0);
                typeof(PoleSettings).GetField("regularVisual", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(GameSettings.Poles, source);
                root.SetActive(true);
                RefreshSharedVisual();
                pole.PresentSwing(42);
                var expected = grip.rotation * source.transform.localRotation * sourceBar.localRotation;
                Assert.That(Quaternion.Angle(pole.VisualBar.rotation, expected), Is.LessThan(.001f),
                    "매 프레임 Grip.rotation으로 정본의 축 보정을 덮어쓰지 않는다.");
                Assert.That(ProjectedSize(pole.VisualBar, pole.VisualBar.right),
                    Is.EqualTo(GameSettings.Poles.BarWidth(false)).Within(.001f));
            }
            finally { Object.DestroyImmediate(source); }
        }

        [Test]
        [Category("TutorialCommonRulesRegression")]
        public void SharedVisual_HidesLegacyRenderersAndReusesReplacesAndCleansItsRoot()
        {
            var legacyBar = bar.gameObject.AddComponent<MeshRenderer>();
            var legacyCable = leftCable.gameObject.AddComponent<MeshRenderer>();
            root.SetActive(true);
            RefreshSharedVisual();
            var first = pole.CommonVisual;
            Assert.IsFalse(legacyBar.enabled);
            Assert.IsFalse(legacyCable.enabled);
            RefreshSharedVisual();
            Assert.AreSame(first, pole.CommonVisual, "같은 정본으로 외형을 중복 생성하지 않는다.");
            root.SetActive(false);
            pole.PresentSwing(0);
            Assert.IsFalse(first.gameObject.activeSelf);
            root.SetActive(true);
            pole.PresentSwing(0);
            Assert.IsTrue(first.gameObject.activeSelf);
            pole.IsSwitch = true;
            RefreshSharedVisual();
            Assert.IsTrue(first == null, "외형 종류 변경 시 이전 표시 루트를 정리한다.");
            var replacement = pole.CommonVisual;
            Assert.IsNotNull(replacement);
            DestroySharedVisual();
            Assert.IsTrue(replacement == null, "공통 정리 경로가 분리된 표시 루트를 제거한다.");
        }

        void RefreshSharedVisual() => typeof(HumanCannonPole)
            .GetMethod("RefreshCommonVisual", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pole, null);

        void DestroySharedVisual() => typeof(HumanCannonPole)
            .GetMethod("DestroyCommonVisual", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pole, null);

        void AssertCommonCable(string cableName, string anchorName)
        {
            var visual = pole.CommonVisual;
            var cable = visual.Find(cableName);
            var anchor = visual.Find(anchorName);
            Assert.IsTrue(cable.gameObject.activeInHierarchy, cableName);
            Near(cable.TransformPoint(Vector3.up * .5f), anchor.position);
            var midpoint = (visual.Find("LeftAnchor").position + visual.Find("RightAnchor").position) * .5f;
            Near(cable.TransformPoint(Vector3.down * .5f), pole.VisualBar.position + anchor.position - midpoint);
        }

        static float ProjectedSize(Transform visual, Vector3 axis)
        {
            float min = float.PositiveInfinity, max = float.NegativeInfinity;
            foreach (var filter in visual.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                var bounds = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    float projected = Vector3.Dot(filter.transform.TransformPoint(corner), axis);
                    min = Mathf.Min(min, projected); max = Mathf.Max(max, projected);
                }
            }
            return max - min;
        }

        static Bounds WorldMeshBounds(Transform visual)
        {
            var bounds = new Bounds(visual.position, Vector3.zero);
            bool found = false;
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return bounds;
        }

        void AssertCable(Transform cable, Transform anchor)
        {
            Near(cable.TransformPoint(Vector3.up * 0.5f), anchor.position);
            Near(cable.TransformPoint(Vector3.down * 0.5f),
                bar.position + Vector3.ProjectOnPlane(anchor.position - grip.position, Vector3.up));
        }
        Transform Child(string name, Vector3 position)
        {
            var child = new GameObject(name).transform;
            child.SetParent(root.transform, false);
            child.localPosition = position;
            return child;
        }
        void Set(string field, Transform value) => typeof(HumanCannonPole)
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(pole, value);
        static void Near(Vector3 actual, Vector3 expected) => Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.001f));
    }
}
