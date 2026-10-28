using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed class BalanceRiskBlinkPlayTests
    {
        GameObject root;

        static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(name)).First(t => t != null);

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.Destroy(root);
        }

        [UnityTest]
        public IEnumerator TrackBorderBlinksOnlyFromSeventyPercentRisk()
        {
            root = new GameObject("P1_Balance", typeof(RectTransform));
            var track = new GameObject("60_DwellTrack", typeof(RectTransform), typeof(Image));
            track.transform.SetParent(root.transform, false);
            var fill = new GameObject("61_DwellFill", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            fill.transform.SetParent(root.transform, false);
            fill.type = Image.Type.Filled;
            var blink = root.AddComponent(GameType("CurtainCall.UI.SceneIntegration.BalanceRiskBlink"));
            bool Blinking() => (bool)blink.GetType().GetProperty("IsBlinking").GetValue(blink);

            fill.fillAmount = 0.69f;
            yield return null;
            Assert.IsFalse(Blinking(), "70% 미만에서는 테두리를 표시하지 않는다.");

            fill.fillAmount = 0.7f;
            yield return null;
            Assert.IsTrue(Blinking(), "70% 이상이면 테두리가 깜빡인다.");
            var image = track.GetComponent<Image>();
            Assert.That(image.color.g, Is.LessThan(0.5f), "깜빡임은 같은 테두리 이미지를 빨강으로 바꾸며 시작한다.");
            Assert.IsNull(track.GetComponent<Outline>(), "테두리 바깥에 별도 선을 그리지 않는다.");
            var ring = track.transform.Find("60_DwellTrackWarning");
            Assert.IsNotNull(ring, "두꺼운 경고 테두리를 트랙과 같은 자리에 겹친다.");
            Assert.IsTrue(ring.gameObject.activeSelf);
            var ringRect = (RectTransform)ring;
            Assert.That(ringRect.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(ringRect.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(ringRect.offsetMin, Is.EqualTo(Vector2.zero));
            Assert.That(ringRect.offsetMax, Is.EqualTo(Vector2.zero));
            float first = image.color.g;
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.That(image.color.g, Is.Not.EqualTo(first).Within(0.01f), "테두리 색이 시간에 따라 바뀐다.");

            fill.fillAmount = 0.3f;
            yield return null;
            Assert.IsFalse(Blinking(), "위험이 내려가면 테두리를 끈다.");
            Assert.That(image.color, Is.EqualTo(Color.white), "원래 흰 테두리로 돌아간다.");
            Assert.IsFalse(ring.gameObject.activeSelf, "경고 테두리도 숨긴다.");
        }

        [UnityTest]
        public IEnumerator ZoneFillsDeepenWhileFrameStaysWhite()
        {
            root = new GameObject("P1_Balance", typeof(RectTransform));
            var frame = new GameObject("40_Frame", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            frame.transform.SetParent(root.transform, false);
            var original = frame.color = new Color(0.78f, 0.85f, 0.85f, 1f);
            var zones = new GameObject("10_Zones", typeof(RectTransform));
            zones.transform.SetParent(root.transform, false);
            var pastelSafe = new Color(0.45f, 0.78f, 0.63f, 1f);
            var safe = new GameObject("Safe", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            safe.transform.SetParent(zones.transform, false);
            safe.color = pastelSafe;
            root.AddComponent(GameType("CurtainCall.UI.SceneIntegration.BalanceZoneColors"));
            yield return null;

            Assert.That(safe.color.r + safe.color.g + safe.color.b, Is.LessThan(pastelSafe.r + pastelSafe.g + pastelSafe.b),
                "바 안의 초록 구간을 원래보다 짙게 칠한다.");
            Assert.That(frame.color, Is.EqualTo(original), "균형바 테두리는 원래 흰색 그대로 둔다.");
            Assert.That(frame.transform.childCount, Is.Zero, "테두리 위에 별도 선을 겹치지 않는다.");
        }
    }
}
