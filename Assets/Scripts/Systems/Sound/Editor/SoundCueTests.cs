using System.Collections.Generic;
using CurtainCall.EscapeRoute;
using CurtainCall.Network.PlayerSync;
using CurtainCall.UI.RestTimerV28;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.Sound.Editor
{
    public sealed class SoundCueTests
    {
        static RestTimerSnapshot Limited(double remaining, bool paused = false) =>
            new(true, 1, RestTimeOfDay.Day, RestTimerMode.Limited, remaining, 180d, paused);

        static List<string> Run(RestTimerSoundCues cues, params double[] remaining)
        {
            var played = new List<string>();
            foreach (double value in remaining)
            {
                string cue = cues.Step(Limited(value));
                if (cue != null) played.Add(cue);
            }
            return played;
        }

        [Test]
        public void TimerWarning_PlaysOnceWhenCrossingThirtySeconds()
        {
            var played = Run(new RestTimerSoundCues(), 31.2, 30.4, 29.9, 29.5, 20d, 11d);
            CollectionAssert.AreEqual(new[] { SoundSituations.RestTimerWarning }, played);
        }

        [Test]
        public void FinalTicks_PlayOncePerSecondFromTenToOne()
        {
            var values = new List<double> { 10.4 };
            for (double t = 10.2; t > -0.5; t -= 0.25) values.Add(t);
            var played = Run(new RestTimerSoundCues(), values.ToArray());
            Assert.AreEqual(10, played.Count);
            Assert.That(played, Is.All.EqualTo(SoundSituations.RestTimerFinalTick));
        }

        [Test]
        public void FirstVisibleFrame_DoesNotReplayWarningOrTick()
        {
            var cues = new RestTimerSoundCues();
            Assert.IsNull(cues.Step(Limited(25d)));
            var late = new RestTimerSoundCues();
            Assert.IsNull(late.Step(Limited(7.5)));
            Assert.AreEqual(SoundSituations.RestTimerFinalTick, late.Step(Limited(6.9)));
        }

        [Test]
        public void PausedOrHiddenTimer_IsSilentAndHiddenResets()
        {
            var cues = new RestTimerSoundCues();
            Assert.IsNull(cues.Step(Limited(31d)));
            Assert.IsNull(cues.Step(Limited(29d, paused: true)));
            var hidden = Limited(29d); hidden.visible = false;
            Assert.IsNull(cues.Step(hidden));
            Assert.IsNull(cues.Step(Limited(28d)), "다시 보인 첫 프레임은 소리를 내지 않는다");
        }

        [Test]
        public void Cue_RangeAndPitchShapePlayLength()
        {
            var clip = AudioClip.Create("Test", 44100 * 2, 1, 44100, false);
            try
            {
                var cue = new SoundCue { clips = new[] { clip }, startOffset = 0.5f };
                Assert.AreEqual(1.5f, cue.PlayLength(clip, 1f), 0.01f);
                cue.maxDuration = 0.4f;
                Assert.AreEqual(0.4f, cue.PlayLength(clip, 1f), 0.01f);
                Assert.AreEqual(0.2f, cue.PlayLength(clip, 2f), 0.01f);
                cue.startOffset = 5f;
                Assert.Less(cue.StartTime(clip), clip.length);
                Assert.AreSame(clip, new SoundCue { clips = new AudioClip[] { null, clip } }.PickClip());
                Assert.IsNull(new SoundCue().PickClip());
            }
            finally { Object.DestroyImmediate(clip); }
        }

        [Test]
        public void Catalog_ResolvesSituationToItsSheetId()
        {
            var catalog = ScriptableObject.CreateInstance<SoundCatalog>();
            try
            {
                catalog.cues.Add(new SoundCue { id = "UI-1" });
                catalog.cues.Add(new SoundCue { id = "UI-2" });
                catalog.situations.Add(new SoundSituation { key = "a.b", cueId = "UI-2" });
                Assert.AreEqual("UI-2", catalog.Resolve("a.b", out var situation).id);
                Assert.IsNotNull(situation);
                Assert.AreEqual("UI-1", catalog.Resolve("ui-1", out situation).id, "시트 ID 직접(대소문자 무시)");
                Assert.IsNull(situation);
                catalog.situations[0].cueId = "";
                Assert.IsNull(catalog.Resolve("a.b", out _), "상황에 사운드를 비우면 소리 없음");
            }
            finally { Object.DestroyImmediate(catalog); }
        }

        [Test]
        public void EscapeLocalDeath_OnlyWhenAliveBecomesDeadInEscape()
        {
            Assert.IsTrue(EscapeLocalDeathSound.IsLocalDeath(PlayerState.Normal, PlayerState.Fallen, true));
            Assert.IsTrue(EscapeLocalDeathSound.IsLocalDeath(PlayerState.EscapeCaptured, PlayerState.Retired, true));
            Assert.IsFalse(EscapeLocalDeathSound.IsLocalDeath(PlayerState.Normal, PlayerState.Fallen, false), "탈출로 밖은 카드 경로가 담당");
            Assert.IsFalse(EscapeLocalDeathSound.IsLocalDeath(PlayerState.Fallen, PlayerState.Retired, true), "이미 죽은 뒤 상태 변화");
            Assert.IsFalse(EscapeLocalDeathSound.IsLocalDeath(PlayerState.Normal, PlayerState.Arrived, true));
        }

        [Test]
        public void Catalog_HasEveryCodeSituationWithPlayableSound()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<SoundCatalog>(SoundCatalog.AssetPath);
            Assert.IsNotNull(catalog, SoundCatalog.AssetPath);
            foreach (var (key, _, _, _) in SoundSituations.Defaults)
            {
                var cue = catalog.Resolve(key, out var situation);
                Assert.IsNotNull(situation, key);
                Assert.IsNotNull(cue, key + " 사운드");
                Assert.IsTrue(cue.HasClip, key + " 클립");
            }
        }

        [Test]
        public void Catalog_HasEveryCodeConnectedCueWithClip()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<SoundCatalog>(SoundCatalog.AssetPath);
            Assert.IsNotNull(catalog, SoundCatalog.AssetPath);
            foreach (var (id, _, _) in SoundCueIds.Defaults)
            {
                var cue = catalog.Find(id);
                Assert.IsNotNull(cue, id);
                Assert.IsTrue(cue.HasClip, id + " 클립");
            }
        }
    }
}
