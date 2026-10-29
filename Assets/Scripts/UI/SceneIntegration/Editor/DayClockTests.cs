using System.Linq;
using CurtainCall.Campaign;
using CurtainCall.UI.DayClockV2;
using NUnit.Framework;
using UnityEngine;
using PackageView = CurtainCall.UI.DayClockV2.DayClockView;

namespace CurtainCall.UI.SceneIntegration.EditorTools.Tests
{
    public sealed class DayClockTests
    {
        DayClockScheduleProfile profile;
        DayClockCompiledSchedule Compile()
        {
            Assert.IsTrue(DayClockScheduleMath.TryCompile(profile, out var result, out string error), error);
            return result;
        }
        [SetUp] public void Open() => profile = ScriptableObject.CreateInstance<DayClockScheduleProfile>();
        [TearDown] public void Close() => Object.DestroyImmediate(profile);

        [Test]
        public void ChangedScheduleDrivesClockAndRingTogether()
        {
            var entry = profile.entries.Single(x => x.phase == DayClockPhase.MorningBreak);
            entry.start = new DayClockTime { hour = 7, minute = 30 };
            entry.end = new DayClockTime { hour = 9, minute = 30 };
            var schedule = Compile();
            var state = new CampaignSnapshot { Phase = CampaignPhase.RestTime, Rest = RestKind.Morning,
                Day = 2, Timer = RestTimerState.Running, TimeRemaining = 150 };
            Assert.IsTrue(DayClock.TryRead(state, 300, schedule, out var frame));
            Assert.AreEqual(510, frame.minuteOfDay, .001);
            Assert.AreEqual(150, frame.remainingSeconds);
            var ring = schedule.intervals.Single(x => x.phase == DayClockPhase.MorningBreak);
            Assert.AreEqual(450, ring.startMinute);
            Assert.AreEqual(570, ring.endMinute);
            state.TimeRemaining = 0;
            DayClock.TryRead(state, 300, schedule, out frame);
            Assert.Less(frame.minuteOfDay, 570);
        }

        [Test]
        public void CrossMidnightIntervalInterpolatesFromConfiguredStart()
        {
            profile.entries.Clear();
            profile.entries.Add(new DayClockScheduleEntry(DayClockPhase.Maintenance, "밤 정비", 22, 2, false, Color.blue));
            var schedule = Compile();
            var state = new CampaignSnapshot { Phase = CampaignPhase.Night, Timer = RestTimerState.Running, TimeRemaining = 75 };
            Assert.IsTrue(DayClock.TryRead(state, 300, schedule, out var frame));
            Assert.AreEqual(60, frame.minuteOfDay, .001);
            Assert.AreEqual(2, schedule.intervals.Count(x => x.phase == DayClockPhase.Maintenance));
        }

        [Test]
        public void UnlimitedStaysAtStartAndDoesNotInventRemainingSeconds()
        {
            var state = new CampaignSnapshot { Phase = CampaignPhase.Night, Timer = RestTimerState.Unlimited };
            Assert.IsTrue(DayClock.TryRead(state, 300, Compile(), out var frame));
            Assert.IsTrue(frame.isUnlimited);
            Assert.IsFalse(frame.hasRemaining);
            Assert.AreEqual(960, frame.minuteOfDay);
            Assert.IsFalse(DayClock.TryRead(new CampaignSnapshot { Phase = CampaignPhase.Escape }, 300, Compile(), out _));
        }

        [Test]
        public void InvalidOverlapIsRejectedAndSleepIsComplement()
        {
            var schedule = Compile();
            Assert.AreEqual(1440, schedule.intervals.Sum(x => x.endMinute - x.startMinute));
            profile.entries[0].end = new DayClockTime { hour = 11 };
            Assert.IsFalse(DayClockScheduleMath.TryCompile(profile, out _, out _));
        }

        [Test]
        public void PrefabUsesPackageArtworkAndExplicitUnlimitedDisplay()
        {
            var prefab = Resources.Load<DayClockView>(DayClockView.PrefabPath);
            Assert.IsNotNull(prefab);
            var instance = Object.Instantiate(prefab);
            try
            {
                var view = instance.View;
                var state = new CampaignSnapshot { Phase = CampaignPhase.Night, Day = 3, Timer = RestTimerState.Unlimited };
                instance.Show(state, 300);
                Assert.IsTrue(instance.IsShown);
                Assert.AreEqual("3일차", instance.DayText);
                Assert.AreEqual("제한 없음", instance.RemainingText);
                state.Timer = RestTimerState.Running; state.TimeRemaining = 90;
                instance.Show(state, 300);
                Assert.AreEqual("1:30", instance.RemainingText, "밤 제한을 켜면 프로필 기본값과 무관하게 실제 카운트다운 표시");
                Assert.IsFalse(instance.Snapshot.isUnlimited);
                Assert.AreEqual(new Vector2(224, 248), ((RectTransform)instance.transform).sizeDelta);
                Assert.IsTrue(instance.GetComponentsInChildren<UnityEngine.UI.Image>(true)
                    .All(x => !x.raycastTarget && (x.sprite != null || x.name.EndsWith("Divider"))));
                var group = instance.GetComponent<CanvasGroup>();
                Assert.IsFalse(group.blocksRaycasts); Assert.IsFalse(group.interactable);
                instance.Hide(); Assert.IsFalse(instance.IsShown);
                instance.Show(state, 300); Assert.IsTrue(instance.IsShown);
            }
            finally { Object.DestroyImmediate(instance.gameObject); }
        }

        [Test]
        public void MinuteHandStaysDisabledAcrossUpdatesAndCanBeRestored()
        {
            var instance = Object.Instantiate(Resources.Load<DayClockView>(DayClockView.PrefabPath));
            try
            {
                var rects = instance.GetComponentsInChildren<RectTransform>(true);
                var minute = rects.Single(x => x.name == "71_MinuteHand");
                var hour = rects.Single(x => x.name == "70_HourHand");
                Assert.IsFalse(minute.gameObject.activeSelf);
                foreach (double time in new[] { 540d, 785d, 1080d })
                {
                    instance.View.SetSnapshot(new DayClockSnapshot(true, 1, DayClockPhase.NoonBreak, time, true, 135, true));
                    Assert.IsFalse(minute.gameObject.activeSelf);
                    Assert.IsTrue(hour.gameObject.activeSelf);
                    Assert.AreEqual(Mathf.Repeat(-(float)time * .5f, 360), hour.localEulerAngles.z, .001);
                }
                var settings = new UnityEditor.SerializedObject(instance.View);
                settings.FindProperty("showMinuteHand").boolValue = true;
                settings.ApplyModifiedPropertiesWithoutUndo();
                instance.View.SetSnapshot(new DayClockSnapshot(true, 1, DayClockPhase.NoonBreak, 785, true, 135, true));
                Assert.IsTrue(minute.gameObject.activeSelf, "오브젝트·참조·계산을 남겨 설정으로 복원한다");
                Assert.AreEqual(330, minute.localEulerAngles.z, .001);
                var missing = instance.Snapshot; missing.hasTime = false;
                instance.View.SetSnapshot(missing);
                Assert.IsFalse(minute.gameObject.activeSelf);
                Assert.IsFalse(hour.gameObject.activeSelf);
            }
            finally { Object.DestroyImmediate(instance.gameObject); }
        }

        [Test]
        public void LiveScheduleEditUpdatesActualRadialGeometryAndHands()
        {
            var instance = Object.Instantiate(Resources.Load<DayClockView>(DayClockView.PrefabPath));
            try
            {
                Assert.IsTrue(instance.View.TryApplySchedule(profile, out _));
                var entry = profile.entries.Single(x => x.phase == DayClockPhase.NoonBreak);
                entry.start = new DayClockTime { hour = 12, minute = 30 };
                entry.end = new DayClockTime { hour = 13, minute = 30 };
                instance.Show(new CampaignSnapshot { Phase = CampaignPhase.RestTime, Rest = RestKind.Between,
                    Timer = RestTimerState.Running, TimeRemaining = 150 }, 300);
                Assert.AreEqual(780, instance.Snapshot.minuteOfDay, .001);
                var segment = instance.GetComponentsInChildren<UnityEngine.UI.Image>(true)
                    .Single(x => x.name == "Segment_NoonBreak_30");
                Assert.AreEqual(60f / 720, segment.fillAmount, .0001);
                Assert.AreEqual(345, segment.rectTransform.localEulerAngles.z, .001);
                Assert.IsTrue(segment.gameObject.activeSelf);
                var minuteHand = instance.GetComponentsInChildren<RectTransform>(true).Single(x => x.name == "71_MinuteHand");
                Assert.AreEqual(0, minuteHand.localEulerAngles.z, .001);
                var missing = instance.Snapshot; missing.hasRemaining = false; missing.isUnlimited = false;
                instance.View.SetSnapshot(missing);
                Assert.AreEqual("--:--", instance.RemainingText, "미수신과 무제한을 구분한다");
            }
            finally { Object.DestroyImmediate(instance.gameObject); }
        }

        [Test]
        public void OrdinaryClockUses12HourGeometryAndSwitchesScheduleAtNoon()
        {
            var instance = Object.Instantiate(Resources.Load<DayClockView>(DayClockView.PrefabPath));
            try
            {
                var view = instance.View;
                var schedule = view.AcceptedSchedule;
                var expected = new[] { (DayClockPhase.MorningBreak, 480, 600),
                    (DayClockPhase.MorningShow, 600, 720), (DayClockPhase.NoonBreak, 720, 840),
                    (DayClockPhase.AfternoonShow, 840, 960), (DayClockPhase.Maintenance, 960, 1200) };
                foreach (var (phase, start, end) in expected)
                {
                    var interval = schedule.intervals.Single(x => x.phase == phase);
                    Assert.AreEqual(start, interval.startMinute);
                    Assert.AreEqual(end, interval.endMinute);
                }
                foreach (int minute in new[] { 0, 540, 660, 719, 720, 780, 900, 1080, 1380 })
                {
                    var phase = schedule.intervals.Single(x => x.startMinute <= minute && minute < x.endMinute).phase;
                    view.SetSnapshot(new DayClockSnapshot(true, 1, phase, minute, true, 150, true));
                    var images = instance.GetComponentsInChildren<UnityEngine.UI.Image>(true);
                    var segments = images.Where(x => x.name.StartsWith("Segment_") && x.gameObject.activeSelf).ToArray();
                    var parts = DayClockScheduleMath.ClipToWindow(schedule, DayClockScheduleMath.WindowStart(minute));
                    Assert.AreEqual(minute < 720 ? 3 : 4, segments.Length, "현재 오전/오후 구간만 일반 12시간 눈금에 표시한다");
                    foreach (var interval in parts)
                    {
                        var segment = segments.Single(x => x.name == $"Segment_{interval.phase}_{interval.startMinute}");
                        Assert.AreEqual((interval.endMinute - interval.startMinute) / 720f, segment.fillAmount, .0001);
                        Assert.AreEqual(Mathf.Repeat(-interval.startMinute * .5f, 360), segment.rectTransform.localEulerAngles.z, .001);
                        int localMinute = minute - DayClockScheduleMath.WindowStart(minute);
                        bool current = interval.phase == phase && localMinute >= interval.startMinute && localMinute < interval.endMinute;
                        Color expectedColor = current ? interval.color : Color.Lerp(interval.color, new Color32(0x34, 0x45, 0x4A, 0xFF), .12f);
                        expectedColor.a = 1;
                        Assert.AreEqual(expectedColor, segment.color);
                    }
                    Assert.AreEqual(minute < 720 ? 3 : 4, images.Count(x => x.name.StartsWith("Boundary_") && x.gameObject.activeSelf));
                    var hour = instance.GetComponentsInChildren<RectTransform>(true).Single(x => x.name == "70_HourHand");
                    Assert.AreEqual(Mathf.Repeat(-minute * .5f, 360), hour.localEulerAngles.z, .001);
                }
                var labels = instance.GetComponentsInChildren<TMPro.TMP_Text>(true);
                Assert.AreEqual("12", labels.Single(x => x.name == "Hour12TMP").text);
                Assert.AreEqual("3", labels.Single(x => x.name == "Hour03TMP").text);
                Assert.AreEqual("6", labels.Single(x => x.name == "Hour06TMP").text);
                Assert.AreEqual("9", labels.Single(x => x.name == "Hour09TMP").text);
            }
            finally { Object.DestroyImmediate(instance.gameObject); }
        }

        [TestCase(0d, true, "오전")]
        [TestCase(719.9d, true, "오전")]
        [TestCase(720d, true, "오후")]
        [TestCase(1439.9d, true, "오후")]
        [TestCase(1440d, true, "—")]
        [TestCase(540d, false, "—")]
        [TestCase(double.NaN, true, "—")]
        public void V27HalfDayUsesClockNotPhaseAndKeepsIndependentCountdown(double minute, bool hasTime, string expected)
        {
            var instance = Object.Instantiate(Resources.Load<DayClockView>(DayClockView.PrefabPath));
            try
            {
                instance.View.SetSnapshot(new DayClockSnapshot(true, 2, DayClockPhase.Maintenance,
                    minute, hasTime, 135, true));
                var labels = instance.GetComponentsInChildren<TMPro.TMP_Text>(true);
                Assert.AreEqual(expected, labels.Single(x => x.name == "90_HalfDayTMP").text);
                Assert.AreEqual("2일차", instance.DayText);
                Assert.AreEqual("2:15", instance.RemainingText);
                Assert.AreEqual(expected != "—", instance.GetComponentsInChildren<RectTransform>(true)
                    .Single(x => x.name == "70_HourHand").gameObject.activeSelf);
            }
            finally { Object.DestroyImmediate(instance.gameObject); }
        }

        [Test]
        public void V27DesignUsesNewArtworkWithoutReplacingScheduleOrWarningHeader()
        {
            var prefab = Resources.Load<DayClockView>(DayClockView.PrefabPath);
            var instance = Object.Instantiate(prefab);
            try
            {
                Assert.AreSame(UnityEditor.AssetDatabase.LoadAssetAtPath<DayClockScheduleProfile>(
                    "Assets/Resources/Prefabs/UIs/DayClock/W1_DayClock_v2_2_Schedule.asset"), instance.View.ScheduleProfile);
                var rects = instance.GetComponentsInChildren<RectTransform>(true);
                Assert.AreEqual(new Vector2(176, 176), rects.Single(x => x.name == "DialRoot").sizeDelta);
                var images = instance.GetComponentsInChildren<UnityEngine.UI.Image>(true);
                var plate = images.Single(x => x.name == "86_DateHeaderPlate");
                Assert.AreEqual(UnityEngine.UI.Image.Type.Sliced, plate.type);
                Assert.AreEqual(new Vector4(12, 12, 12, 12), plate.sprite.border);
                StringAssert.Contains("curtaincall_dayclock_v2.7/", UnityEditor.AssetDatabase.GetAssetPath(plate.sprite));
                Assert.AreEqual(new Vector2(160, 32), plate.rectTransform.sizeDelta);
                Assert.AreEqual(new Vector2(112, -232), plate.rectTransform.anchoredPosition);
                Assert.LessOrEqual(plate.rectTransform.anchoredPosition.y + 16,
                    rects.Single(x => x.name == "Hour06TMP").anchoredPosition.y - 12,
                    "명판은 6시 눈금 아래에 겹치지 않게 둔다");
                var labels = instance.GetComponentsInChildren<TMPro.TMP_Text>(true);
                Assert.AreEqual(22, labels.Single(x => x.name == "90_HeaderTMP").fontSize);
                Assert.AreEqual(18, labels.Single(x => x.name == "90_HalfDayTMP").fontSize);
                Assert.AreEqual(.85f, labels.Single(x => x.name == "90_HeaderTMP").alpha, .001f);
                Assert.AreEqual(.85f, labels.Single(x => x.name == "90_HalfDayTMP").alpha, .001f);
                Assert.IsTrue(labels.All(x => x.font.HasCharacters(x.name == "90_HalfDayTMP" ? "오전오후—" : "제한 없음0123456789")));
                foreach (double seconds in new[] { 135d, 30d, 10d, 0d, 90d })
                {
                    instance.View.SetSnapshot(new DayClockSnapshot(true, 1, DayClockPhase.MorningBreak, 560, true, seconds, true));
                    Assert.AreEqual(Color.white, plate.color, "경고는 외부 명판을 물들이지 않는다");
                    Assert.AreEqual("오전", labels.Single(x => x.name == "90_HalfDayTMP").text);
                    Assert.AreEqual(1f, labels.Single(x => x.name == "91_CountdownTMP").color.a);
                }
                var paused = instance.Snapshot; paused.isCountdownPaused = true;
                instance.View.SetSnapshot(paused);
                Assert.AreEqual("일시정지", labels.Single(x => x.name == "92_PhaseTMP").text);
                Assert.IsFalse(images.Single(x => x.name == "92_PhaseIcon").gameObject.activeSelf);
                paused.isCountdownPaused = false; instance.View.SetSnapshot(paused);
                Assert.IsTrue(images.Single(x => x.name == "92_PhaseIcon").gameObject.activeSelf);
            }
            finally { Object.DestroyImmediate(instance.gameObject); }
        }

        [Test]
        public void V27EmphasizesOnlyActualFragmentWithoutRebuildingSameWindow()
        {
            profile.entries.Clear();
            profile.entries.Add(new DayClockScheduleEntry(DayClockPhase.AfternoonShow, "공연", 14, 16, false, Color.yellow));
            var instance = Object.Instantiate(Resources.Load<DayClockView>(DayClockView.PrefabPath));
            try
            {
                Assert.IsTrue(instance.View.TryApplySchedule(profile, out _));
                instance.View.SetSnapshot(new DayClockSnapshot(true, 1, DayClockPhase.Sleep, 780, true, 0, false));
                var images = instance.GetComponentsInChildren<UnityEngine.UI.Image>(true);
                var outlines = images.Where(x => x.name.StartsWith("Outline_") && x.gameObject.activeSelf).ToArray();
                var symbols = images.Where(x => x.name.StartsWith("Activity_") && x.gameObject.activeSelf).ToArray();
                Assert.AreEqual(3, outlines.Length);
                Assert.AreEqual(3, symbols.Length);
                var ids = outlines.Select(x => x.GetInstanceID()).ToArray();
                foreach (var symbol in symbols)
                {
                    Assert.AreEqual(new Vector2(14, 14), symbol.rectTransform.sizeDelta);
                    Assert.AreEqual(67.2222222f, symbol.rectTransform.anchoredPosition.magnitude, .001f);
                    Assert.AreEqual(Quaternion.identity, symbol.rectTransform.localRotation);
                    Assert.AreEqual(0, symbol.transform.childCount, "기호에 원판/외곽선이 없다");
                }
                foreach (var (minute, phase, selected) in new[] {
                    (780d, DayClockPhase.Sleep, "Outline_Sleep_0"),
                    (1080d, DayClockPhase.Sleep, "Outline_Sleep_240"),
                    (840d, DayClockPhase.Sleep, ""),
                    (900d, DayClockPhase.AfternoonShow, "Outline_AfternoonShow_120") })
                {
                    instance.View.SetSnapshot(new DayClockSnapshot(true, 1, phase, minute, true, 0, false));
                    CollectionAssert.AreEqual(ids, outlines.Select(x => x.GetInstanceID()).ToArray());
                    foreach (var outline in outlines)
                        Assert.AreEqual(outline.name == selected ? .82f : .10f, outline.color.a, .001f);
                    foreach (var symbol in symbols)
                        Assert.AreEqual(symbol.name.Replace("Activity_", "Outline_") == selected ? 1 : .6f, symbol.color.a, .001f);
                }
                instance.View.SetSnapshot(new DayClockSnapshot(true, 1, DayClockPhase.AfternoonShow, double.NaN, true, 135, true));
                Assert.IsFalse(outlines.Any(x => x.gameObject.activeSelf));
                Assert.IsFalse(symbols.Any(x => x.gameObject.activeSelf));
                Assert.AreEqual("2:15", instance.RemainingText);
            }
            finally { Object.DestroyImmediate(instance.gameObject); }
        }

        [TestCase(47, false)]
        [TestCase(48, true)]
        public void V27HidesOnlySymbolForShortClippedIntervals(int minutes, bool visible)
        {
            profile.entries.Clear();
            profile.entries.Add(new DayClockScheduleEntry(DayClockPhase.MorningShow, "공연", 11, 13, false, Color.yellow));
            profile.entries[0].start = new DayClockTime { hour = 11, minute = 60 - minutes };
            var instance = Object.Instantiate(Resources.Load<DayClockView>(DayClockView.PrefabPath));
            try
            {
                Assert.IsTrue(instance.View.TryApplySchedule(profile, out _));
                instance.View.SetSnapshot(new DayClockSnapshot(true, 1, DayClockPhase.MorningShow, 700, true, 0, false));
                var images = instance.GetComponentsInChildren<UnityEngine.UI.Image>(true);
                Assert.AreEqual(visible, images.Any(x => x.name.StartsWith("Activity_MorningShow_") && x.gameObject.activeSelf));
                Assert.AreEqual(minutes / 720f, images.Single(x => x.name.StartsWith("Segment_MorningShow_") && x.gameObject.activeSelf).fillAmount, .0001f);
                Assert.AreEqual("ui_icon_dayclock_show_normal@2x", images.Single(x => x.name == "92_CurrentActivityIcon").sprite.name);
            }
            finally { Object.DestroyImmediate(instance.gameObject); }
        }

        [Test]
        public void V27CentralSymbolsKeepUnlimitedAndExternalPhaseIndependentOfClock()
        {
            var instance = Object.Instantiate(Resources.Load<DayClockView>(DayClockView.PrefabPath));
            try
            {
                var images = instance.GetComponentsInChildren<UnityEngine.UI.Image>(true);
                var activity = images.Single(x => x.name == "92_CurrentActivityIcon");
                var divider = images.Single(x => x.name == "92_ActivityDivider");
                var time = images.Single(x => x.name == "92_PhaseIcon");
                instance.View.SetSnapshot(new DayClockSnapshot(true, 1, DayClockPhase.NoonBreak, 690, true, 135, true));
                Assert.AreEqual("ui_icon_dayclock_rest_normal@2x", activity.sprite.name);
                Assert.AreEqual("ui_icon_dayclock_day_normal@2x", time.sprite.name);
                Assert.AreEqual("오전", instance.GetComponentsInChildren<TMPro.TMP_Text>(true).Single(x => x.name == "90_HalfDayTMP").text);
                Assert.IsTrue(activity.gameObject.activeSelf);
                Assert.IsTrue(divider.gameObject.activeSelf);
                Assert.AreEqual(.45f, divider.color.a, .001f);
                var unlimited = new DayClockSnapshot(true, 3, DayClockPhase.Maintenance, 960, true, 0, false);
                unlimited.isUnlimited = true;
                instance.View.SetSnapshot(unlimited);
                Assert.AreEqual("제한 없음", instance.RemainingText);
                Assert.IsTrue(activity.gameObject.activeSelf);
                Assert.IsTrue(time.gameObject.activeSelf);
                Assert.IsFalse(images.Single(x => x.name == "85_WarningRim").gameObject.activeSelf);
                unlimited.isUnlimited = false;
                instance.View.SetSnapshot(unlimited);
                Assert.IsTrue(activity.gameObject.activeSelf);
                Assert.IsTrue(divider.gameObject.activeSelf);
                Assert.IsFalse(images.Single(x => x.name == "93_StatusIcon").gameObject.activeSelf);
            }
            finally { Object.DestroyImmediate(instance.gameObject); }
        }

        [TestCase(DayClockPhase.MorningBreak, 540d)]
        [TestCase(DayClockPhase.NoonBreak, 780d)]
        [TestCase(DayClockPhase.Maintenance, 960d)]
        [TestCase(DayClockPhase.AfternoonShow, 900d)]
        public void UnlimitedHidesTextAndCentersIconPairThenRestoresFiniteLayout(DayClockPhase phase, double minute)
        {
            var instance = Object.Instantiate(Resources.Load<DayClockView>(DayClockView.PrefabPath));
            try
            {
                var images = instance.GetComponentsInChildren<UnityEngine.UI.Image>(true);
                var activity = images.Single(x => x.name == "92_CurrentActivityIcon");
                var time = images.Single(x => x.name == "92_PhaseIcon");
                var divider = images.Single(x => x.name == "92_ActivityDivider");
                var countdown = instance.GetComponentsInChildren<TMPro.TMP_Text>(true).Single(x => x.name == "91_CountdownTMP");
                var state = new DayClockSnapshot(true, 3, phase, minute, true, 0, true, true);
                state.isUnlimited = true;
                instance.View.SetSnapshot(state);
                Assert.IsFalse(countdown.gameObject.activeSelf, "무제한이면 남은 초/일시정지 잔여값보다 우선하여 텍스트를 숨긴다");
                Assert.IsTrue(activity.gameObject.activeSelf);
                Assert.IsTrue(time.gameObject.activeSelf);
                Assert.IsTrue(divider.gameObject.activeSelf);
                Assert.AreEqual(new Vector2(94, -108), activity.rectTransform.anchoredPosition);
                Assert.AreEqual(new Vector2(130, -108), time.rectTransform.anchoredPosition);
                Assert.AreEqual(new Vector2(112, -108), divider.rectTransform.anchoredPosition);
                Assert.IsFalse(images.Single(x => x.name == "85_WarningRim").gameObject.activeSelf);
                Assert.IsFalse(images.Single(x => x.name == "93_StatusIcon").gameObject.activeSelf);
                state.isUnlimited = false; state.isCountdownPaused = false; state.remainingSeconds = 135;
                instance.View.SetSnapshot(state);
                Assert.IsTrue(countdown.gameObject.activeSelf);
                Assert.AreEqual("2:15", countdown.text);
                Assert.AreEqual(new Vector2(94, -138), activity.rectTransform.anchoredPosition);
                Assert.AreEqual(new Vector2(130, -138), time.rectTransform.anchoredPosition);
                Assert.AreEqual(new Vector2(112, -138), divider.rectTransform.anchoredPosition);
                state.hasRemaining = false;
                if (phase == DayClockPhase.MorningBreak || phase == DayClockPhase.NoonBreak)
                {
                    instance.View.SetSnapshot(state);
                    Assert.IsTrue(countdown.gameObject.activeSelf, "무제한이 아닌 미수신은 숨기지 않는다");
                    Assert.AreEqual("--:--", countdown.text);
                }
            }
            finally { Object.DestroyImmediate(instance.gameObject); }
        }

        [Test]
        public void WaitingRoomSceneReferencesV27PrefabAndOriginalSchedule()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene("Assets/Scenes/UI/CommonWaitingRoomUI.unity");
            try
            {
                var ui = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<CommonWaitingRoomUI>(true)).Single();
                var clock = new UnityEditor.SerializedObject(ui).FindProperty("dayClock").objectReferenceValue as DayClockView;
                Assert.IsNotNull(clock);
                Assert.AreEqual(DayClockProjectBuilder.Prefab, UnityEditor.PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(clock));
                Assert.AreEqual(new Vector2(32, -48), ((RectTransform)clock.transform).anchoredPosition);
                Assert.AreEqual(new Vector2(224, 248), ((RectTransform)clock.transform).sizeDelta);
                Assert.AreSame(Resources.Load<DayClockView>(DayClockView.PrefabPath).View.ScheduleProfile, clock.View.ScheduleProfile);
                var performance = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene("Assets/Scenes/UI/CommonPerformanceUI.unity");
                try
                {
                    var cheer = performance.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<RectTransform>(true))
                        .Single(x => x.name == "C1CheerGauge");
                    var ring = cheer.GetComponentsInChildren<RectTransform>(true).Single(x => x.name == "RingRoot");
                    var dial = clock.GetComponentsInChildren<RectTransform>(true).Single(x => x.name == "DialRoot");
                    var clockRect = (RectTransform)clock.transform;
                    Assert.AreEqual(new Vector2(0, 1), cheer.anchorMin);
                    Assert.AreEqual(cheer.anchorMin, cheer.anchorMax);
                    Assert.AreEqual(cheer.anchorMin, cheer.pivot);
                    Assert.AreEqual(new Vector2(.5f, .5f), ring.anchorMin);
                    Assert.AreEqual(ring.anchorMin, ring.anchorMax);
                    Assert.AreEqual(ring.anchorMin, ring.pivot);
                    Assert.AreEqual(new Vector2(0, 1), clockRect.pivot);
                    Assert.AreEqual(clockRect.pivot, clockRect.anchorMin);
                    Assert.AreEqual(clockRect.anchorMin, clockRect.anchorMax);
                    var cheerCenter = cheer.anchoredPosition + new Vector2(cheer.sizeDelta.x / 2, -cheer.sizeDelta.y / 2) + ring.anchoredPosition;
                    var clockCenter = clockRect.anchoredPosition + dial.anchoredPosition;
                    Assert.AreEqual(new Vector2(144, -156), cheerCenter);
                    Assert.AreEqual(cheerCenter, clockCenter, "공연·대기실 원 중심은 같은 좌상단 기준 좌표를 사용한다");
                }
                finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(performance); }
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void InvalidLiveEditKeepsHandsAndRingOnLastAcceptedSchedule()
        {
            var instance = Object.Instantiate(Resources.Load<DayClockView>(DayClockView.PrefabPath));
            try
            {
                Assert.IsTrue(instance.View.TryApplySchedule(profile, out _));
                var accepted = instance.View.AcceptedSchedule;
                profile.entries[0].end = new DayClockTime { hour = 11 };
                UnityEngine.TestTools.LogAssert.Expect(LogType.Warning,
                    new System.Text.RegularExpressions.Regex("W1 schedule was not applied"));
                Assert.AreSame(accepted, instance.View.AcceptedSchedule);
                instance.Show(new CampaignSnapshot { Phase = CampaignPhase.RestTime, Rest = RestKind.Morning,
                    Timer = RestTimerState.Running, TimeRemaining = 150 }, 300);
                Assert.AreEqual(540, instance.Snapshot.minuteOfDay, .001);
            }
            finally { Object.DestroyImmediate(instance.gameObject); }
        }
    }
}
