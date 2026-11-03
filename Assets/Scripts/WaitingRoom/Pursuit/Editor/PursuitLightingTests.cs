using CurtainCall.Settings;
using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    public sealed class PursuitLightingTests
    {
        [Test]
        public void DefaultStagesAccumulateDimCrackleRoomFlickerAndRedTint()
        {
            var s = new PursuitLightSettings();
            Assert.AreEqual(4, s.Stages.Length);
            foreach (var stage in s.Stages) Assert.Less(stage.Brightness, 1, "1단계부터 밝기를 낮춘다.");
            for (int i = 1; i < 4; i++) { Assert.Greater(s.Stages[i].CracklePerMinute, 0); Assert.Greater(s.Stages[i].Sparks, 0); }
            Assert.AreEqual(0, s.Stages[0].CracklePerMinute);
            Assert.AreEqual(3, s.Stages[2].EntryFlickers, "3단계 시작 시 2~3회 깜빡인다.");
            Assert.AreEqual(0, s.Stages[3].EntryFlickers, "진입 깜빡임은 3단계 시작 1회만.");
            Assert.Greater(s.Stages[2].RoomFlickerInterval, 0); Assert.Greater(s.Stages[3].RoomFlickerInterval, 0);
            Assert.AreEqual(0, s.Stages[1].RoomFlickerInterval);
            Assert.That(s.Stages[3].TintAmount, Is.InRange(.1f, .6f), "4단계는 붉지만 과하지 않게.");
            Assert.AreEqual(0, s.Stages[2].TintAmount);
            Assert.AreSame(s.Stages[3], s.StageFor(9), "단계가 더 많으면 마지막 조명 단계를 쓴다.");
            Assert.IsNull(s.StageFor(-1));
            Assert.AreEqual(0, s.CaptureOffIntensity, "포획 암전은 완전히 꺼진다.");
        }

        [Test]
        public void FlickerPatternsAreDeterministicAndBounded()
        {
            Assert.AreEqual(PursuitLightSettings.Hash(3, 9, 1), PursuitLightSettings.Hash(3, 9, 1));
            Assert.AreNotEqual(PursuitLightSettings.Hash(3, 9, 1), PursuitLightSettings.Hash(4, 9, 1));
            for (int i = 0; i < 200; i++) Assert.That(PursuitLightSettings.Hash(i, i * 7, 2), Is.InRange(0f, 1f));
            Assert.IsTrue(PursuitLightSettings.IsDark(.01f, 6, .5f, 1), "암전은 꺼진 상태로 시작한다.");
            Assert.IsFalse(PursuitLightSettings.IsDark(-1, 6, .5f, 1));
            int dark = 0;
            for (int i = 0; i < 600; i++) if (PursuitLightSettings.IsDark(i / 6f + .01f, 6, .5f, 7)) dark++;
            Assert.That(dark, Is.InRange(220, 380), "꺼짐 비율이 설정값 근처.");

            int blinks = 0; bool was = false;
            for (float t = 0; t < 3; t += .01f) { bool d = PursuitLightSettings.EntryDark(t, 3, .42f); if (d && !was) blinks++; was = d; }
            Assert.AreEqual(3, blinks);
            Assert.IsFalse(PursuitLightSettings.EntryDark(3 * .42f + .01f, 3, .42f));

            int crackles = 0;
            for (float t = 0; t < 60; t += .02f)
                if (PursuitLightSettings.Crackle(t, 5, 5, .32f, out _, out float local) && local < .02f) crackles++;
            Assert.That(crackles, Is.InRange(4, 6), "조명당 분당 횟수만큼 지직인다.");
            Assert.IsFalse(PursuitLightSettings.Crackle(10, 5, 0, .32f, out _, out _));
        }

        [Test]
        public void LightingRestoresOriginalLightAndAmbientAfterCaptureBlackout()
        {
            var root = new GameObject("PursuitLightingTest");
            var lamp = new GameObject("Lamp", typeof(Light));
            var sky = RenderSettings.ambientSkyColor;
            try {
                root.SetActive(false);
                var area = root.AddComponent<BoxCollider>(); area.size = new Vector3(10, 4, 10); area.center = new Vector3(0, 2, 0);
                var room = new GameObject("Room").AddComponent<PursuitRoom>();
                room.transform.SetParent(root.transform);
                room.Area = area;
                var light = lamp.GetComponent<Light>(); light.type = LightType.Point; light.intensity = 5; light.color = Color.white;
                room.Lights = new[] { light };
                var director = root.AddComponent<PursuitDirector>();
                director.Rooms = new[] { room };
                var lighting = root.AddComponent<PursuitLighting>();
                root.SetActive(true);
                Assert.IsTrue(director.DebugCaptureBlackout(0, 1) == false, "Dev 권한 없이는 미리보기를 막는다.");
                room.Flicker(Time.time + 5);
                var update = typeof(PursuitLighting).GetMethod("LateUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                update.Invoke(lighting, null);
                Assert.IsTrue(lighting.Engaged);
                Assert.GreaterOrEqual(lighting.LightCount, 1, "열린 씬의 방 미지정 조명도 포함될 수 있다.");
                Assert.AreEqual(0, light.intensity, 1e-4f, "포획 암전의 첫 순간은 완전히 꺼진다.");
                room.ClearFlicker();
                update.Invoke(lighting, null);
                Assert.IsFalse(lighting.Engaged);
                Assert.AreEqual(5, light.intensity, 1e-4f);
                Assert.AreEqual(sky, RenderSettings.ambientSkyColor);
            } finally {
                Object.DestroyImmediate(root); Object.DestroyImmediate(lamp);
                RenderSettings.ambientSkyColor = sky;
            }
        }
    }
}
