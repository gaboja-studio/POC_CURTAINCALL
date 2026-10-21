using System.Reflection;
using CurtainCall.Player;
using CurtainCall.Settings;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CurtainCall.Tightrope.EditorTools.Tests
{
    public sealed class TightropeBalanceFeedbackTests
    {
        const BindingFlags Members = BindingFlags.Instance | BindingFlags.NonPublic;
        GameObject player, gauge;
        PlayerBalance balance;
        string previousRules, previousRope;
        Random.State previousRandom;

        [SetUp]
        public void SetUp()
        {
            previousRandom = Random.state;
            previousRules = JsonUtility.ToJson(GameSettings.Base.Balance);
            previousRope = JsonUtility.ToJson(GameSettings.Tightrope.RopeMovement);
            JsonUtility.FromJsonOverwrite("{\"laneLandingDangerRatio\":0.05}", GameSettings.Tightrope.RopeMovement);
            JsonUtility.FromJsonOverwrite("{\"greenLimit\":30,\"warningLimit\":50,\"fallTime\":2,\"warningFallRate\":0.33333334,\"greenRecoveryRate\":0.25,\"idleSway\":0,\"movingSway\":0,\"tiltAcceleration\":0}", GameSettings.Base.Balance);
            player = new GameObject("Balance feedback test");
            balance = player.AddComponent<PlayerBalance>();
            balance.SetBalanceActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            if (gauge != null) Object.DestroyImmediate(gauge);
            Object.DestroyImmediate(player);
            JsonUtility.FromJsonOverwrite(previousRules, GameSettings.Base.Balance);
            JsonUtility.FromJsonOverwrite(previousRope, GameSettings.Tightrope.RopeMovement);
            Random.state = previousRandom;
        }

        [TestCase(0f, BalanceZone.Green)]
        [TestCase(30f, BalanceZone.Green)]
        [TestCase(-30f, BalanceZone.Green)]
        [TestCase(30.01f, BalanceZone.Yellow)]
        [TestCase(-30.01f, BalanceZone.Yellow)]
        [TestCase(50f, BalanceZone.Yellow)]
        [TestCase(-50f, BalanceZone.Yellow)]
        [TestCase(50.01f, BalanceZone.Red)]
        [TestCase(-50.01f, BalanceZone.Red)]
        public void ZoneBoundariesMatchBothSides(float value, BalanceZone zone) =>
            Assert.That(balance.ZoneOf(value), Is.EqualTo(zone));

        [TestCase(40f)]
        [TestCase(-40f)]
        public void YellowAccumulatesSlowlyAndOnlyGreenRecoversTheSharedDangerTimer(float value)
        {
            balance.SetDisplayedState(true, value);
            Step(3f);
            Assert.That(balance.RedTime, Is.EqualTo(1f).Within(.0001f));
            Assert.That(balance.HasFallen, Is.False);
            balance.SetDisplayedState(true, 60f);
            Step(.5f);
            Assert.That(balance.RedTime, Is.EqualTo(1.5f).Within(.0001f));
            Assert.That(balance.HasFallen, Is.False);
            balance.SetDisplayedState(true, 50f);
            Step(.3f);
            Assert.That(balance.RedTime, Is.EqualTo(1.6f).Within(.0001f), "빨강에서 노랑으로 와도 누적 시간은 유지된다.");
            balance.SetDisplayedState(true, 30f);
            Step(.1f);
            Assert.That(balance.RedTime, Is.EqualTo(1.55f).Within(.0001f));
            Step(4f);
            Assert.That(balance.RedTime, Is.Zero);
            balance.SetDisplayedState(true, value);
            int falls = 0;
            balance.Fell += () => falls++;
            Step(6.1f);
            Step(3f);
            Assert.That(balance.HasFallen, Is.True);
            Assert.That(falls, Is.EqualTo(1));
        }

        [Test]
        public void AirbornePausesDangerAndResetRemovesIt()
        {
            balance.SetDisplayedState(true, -80f);
            Step(0.5f);
            balance.SetAirborne(true);
            Step(3f);
            Assert.That(balance.Value, Is.EqualTo(-80f));
            Assert.That(balance.RedTime, Is.EqualTo(0.5f));
            balance.SetBalanceActive(false);
            Step(3f);
            Assert.That(balance.RedTime, Is.Zero);
            Assert.That(balance.Zone, Is.EqualTo(BalanceZone.Green));
            Assert.That(balance.HasFallen, Is.False);
        }

        [Test]
        public void RepeatedLaneLandingsAddFivePercentagePointsEach()
        {
            balance.SetDisplayedState(true, 60f);
            Step(.5f);
            for (int i = 0; i < 10; i++) balance.ApplyLaneLanding(0f);
            Assert.That(balance.LastShock, Is.Not.Zero, "착지의 바늘 충격은 유지한다.");
            Assert.That(balance.RedTimeRatio, Is.EqualTo(.75f).Within(.0001f), "기존 25%에 착지 10회의 50%포인트를 더한다.");
            Assert.That(balance.HasFallen, Is.False);
        }

        [TestCase(1f, 1, 0f)]
        [TestCase(2f, 4, .9f)]
        [TestCase(4f, 4, 1f)]
        public void LaneLandingDangerIsFivePercentOfFullBarRegardlessOfShockModifiers(float fallTime, int stack, float reduction)
        {
            JsonUtility.FromJsonOverwrite("{\"fallTime\":" + fallTime.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}", GameSettings.Base.Balance);
            balance.SetStackSize(stack);
            balance.LaneLandingShockMultiplier = 2f;
            balance.ApplyLaneLanding(reduction);
            Assert.That(balance.RedTimeRatio, Is.EqualTo(.05f).Within(.0001f));
        }

        [Test]
        public void LaneLandingCapsFullDangerAndFallsOnlyOnce()
        {
            balance.SetDisplayedState(true, 70f);
            Step(1.96f);
            int falls = 0;
            balance.Fell += () => falls++;
            balance.ApplyLaneLanding(0f);
            balance.ApplyLaneLanding(0f);
            Assert.That(balance.RedTime, Is.EqualTo(balance.FallTime));
            Assert.That(balance.HasFallen, Is.True);
            Assert.That(falls, Is.EqualTo(1));
            balance.SetBalanceActive(false);
            balance.ApplyLaneLanding(0f);
            Assert.That(balance.RedTime, Is.Zero);
        }

        [Test]
        public void ReturningToGreenStartsGradualRecoveryAfterLaneLanding()
        {
            balance.SetDisplayedState(true, 70f);
            Step(.5f);
            balance.ApplyLaneLanding(0f);
            balance.SetDisplayedState(true, 0f);
            Step(1f / 60f);
            Assert.That(balance.RedTime, Is.EqualTo(.6f - .5f / 60f).Within(.0001f));
            Step(2f);
            Assert.That(balance.RedTime, Is.Zero);
            Assert.That(balance.HasFallen, Is.False);
        }

        [Test]
        public void HalfFullDangerBarTakesTwoGroundedGreenSecondsToRecover()
        {
            balance.SetDisplayedState(true, 70f);
            Step(1f);
            Assert.That(balance.RedTimeRatio, Is.EqualTo(.5f).Within(.0001f));
            balance.SetDisplayedState(true, 0f);
            Step(1f);
            Assert.That(balance.RedTimeRatio, Is.EqualTo(.25f).Within(.0001f));
            Step(1f);
            Assert.That(balance.RedTime, Is.Zero);
        }

        [Test]
        public void BriefGreenVisitAndAirborneGreenCannotEraseAccumulatedDanger()
        {
            balance.SetDisplayedState(true, 70f);
            Step(1f);
            balance.SetDisplayedState(true, 0f);
            Step(.1f);
            Assert.That(balance.RedTimeRatio, Is.EqualTo(.475f).Within(.0001f));
            balance.SetAirborne(true);
            Step(10f);
            Assert.That(balance.RedTimeRatio, Is.EqualTo(.475f).Within(.0001f));
            balance.SetAirborne(false);
            balance.SetDisplayedState(true, 40f);
            Step(.3f);
            Assert.That(balance.RedTime, Is.EqualTo(1.05f).Within(.0001f));
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(144)]
        public void GreenRecoveryRateIsIndependentOfFrameRate(int frameRate)
        {
            balance.SetDisplayedState(true, 70f);
            Step(1f);
            balance.SetDisplayedState(true, 0f);
            for (int i = 0; i < frameRate; i++) Step(1f / frameRate);
            Assert.That(balance.RedTimeRatio, Is.EqualTo(.25f).Within(.0001f));
        }

        [TestCase(.6f, 10)]
        [TestCase(.85f, 14)]
        [TestCase(1f, 16)]
        public void DensityKeepsSawCountsAcrossTheWholeCourse(float density, int expected)
        {
            int kept = 0;
            for (int i = 0; i < 16; i++) if (TrickSettings.IncludesSaw(i, 16, density)) kept++;
            Assert.That(kept, Is.EqualTo(expected));
            Assert.That(TrickSettings.IncludesSaw(15, 16, density), Is.True, "도착 쪽을 통째로 비우지 않는다.");
            Assert.That(TrickSettings.IncludesSaw(-1, 16, density), Is.False);
            Assert.That(TrickSettings.IncludesSaw(0, 0, density), Is.False);
        }

        [TestCase(0, 30f)]
        [TestCase(1, -30f)]
        public void DiagonalPathsAlternateApprovedThirtyDegreeAngles(int index, float angle)
        {
            float slope = TrickSettings.DiagonalSlope(index, 30f);
            Assert.That(Mathf.Atan(slope) * Mathf.Rad2Deg, Is.EqualTo(angle).Within(.001f));
        }

        [Test]
        public void HeldCounterInputCanRecoverFromDeepRedDespiteOutwardSway()
        {
            JsonUtility.FromJsonOverwrite("{\"idleSway\":22.5,\"tiltAcceleration\":0.5}", GameSettings.Base.Balance);
            balance.SetDisplayedState(true, 90f);
            Set(balance, "swayDirection", 1f);
            Set(balance, "nextDirectionChange", float.MaxValue);
            for (int i = 0; i < 119 && balance.Zone == BalanceZone.Red; i++)
            {
                balance.SetCorrectionInput(-1f);
                Step(1f / 60f);
            }
            Assert.That(balance.HasFallen, Is.False);
            Assert.That(balance.Zone, Is.EqualTo(BalanceZone.Yellow), "정확한 반대 입력으로 2초 이내에 경고 구간까지 회복할 수 있어야 한다.");
            Assert.That(balance.RedTime, Is.GreaterThan(0f), "초록까지 돌아와야 위험 시간이 줄어들기 시작한다.");
        }

        [Test]
        public void InvalidWarningLimitCannotOverlapGreenOrExceedGauge()
        {
            JsonUtility.FromJsonOverwrite("{\"greenLimit\":30,\"warningLimit\":10}", GameSettings.Base.Balance);
            Assert.That(balance.WarningLimit, Is.EqualTo(30f));
            Assert.That(balance.ZoneOf(31), Is.EqualTo(BalanceZone.Red));
            JsonUtility.FromJsonOverwrite("{\"warningLimit\":120}", GameSettings.Base.Balance);
            Assert.That(balance.WarningLimit, Is.EqualTo(100f));
        }

        [Test]
        public void ExistingGaugeBuildsOneWarningBandBehindGreenAndHidesOffRope()
        {
            gauge = new GameObject("Gauge test", typeof(Canvas));
            var bar = new GameObject("Bar", typeof(RectTransform));
            bar.transform.SetParent(gauge.transform, false);
            var green = new GameObject("Green", typeof(RectTransform), typeof(Image));
            green.transform.SetParent(bar.transform, false);
            var view = gauge.AddComponent<BalanceGaugeView>();
            Set(view, "greenZone", green.GetComponent<RectTransform>());
            view.Target = balance;
            view.AutoFindTarget = false;
            Invoke(view, "Update");
            Invoke(view, "Update");
            var warning = bar.transform.Find("WarningZone") as RectTransform;
            Assert.That(warning, Is.Not.Null);
            Assert.That(bar.transform.childCount, Is.EqualTo(2), "매 프레임 경고 구간을 중복 생성하면 안 된다.");
            Assert.That(warning.GetSiblingIndex(), Is.LessThan(green.transform.GetSiblingIndex()));
            Assert.That(warning.anchorMin.x, Is.EqualTo(0.25f));
            Assert.That(warning.anchorMax.x, Is.EqualTo(0.75f));
            Assert.That(warning.GetComponent<Image>().raycastTarget, Is.False);
            balance.SetBalanceActive(false);
            Invoke(view, "Update");
            Assert.That(gauge.GetComponent<Canvas>().enabled, Is.False);
            Invoke(view, "OnDestroy"); // EditMode에서 ExecuteAlways가 아닌 컴포넌트의 수명 주기를 명시적으로 구동한다.
            Object.DestroyImmediate(view);
            Assert.That(bar.transform.childCount, Is.EqualTo(1), "생성한 경고 구간도 함께 정리한다.");
        }

        void Step(float deltaTime) => Invoke(balance, "Step", deltaTime);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Members).SetValue(target, value);
        static object Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, Members).Invoke(target, args);
    }
}
