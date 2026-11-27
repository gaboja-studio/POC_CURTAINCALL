using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CurtainCall.Tests
{
    public sealed class LocalizationDisplayTests
    {
        readonly Type language = Type.GetType("CurtainCall.Localization.GameLanguage, Assembly-CSharp", true);
        void Switch(string code) => language.GetMethod("SetPreference").Invoke(null, new object[] { code });

        [UnityTest]
        public IEnumerator QualifiedWorldSignRefreshesAfterRebinding()
        {
            const string key = "CurtainCall.Input.CommonBindings.v1";
            var preferences = Type.GetType("CurtainCall.Player.InputBindingPreferences, Assembly-CSharp", true);
            bool hadSaved = PlayerPrefs.HasKey(key);
            string saved = PlayerPrefs.GetString(key);
            var previous = (InputActionAsset)preferences.GetMethod("CreateDraft").Invoke(null, null);
            var draft = (InputActionAsset)preferences.GetMethod("CreateDefaultDraft").Invoke(null, null);
            var world = new GameObject("Turn control sign");
            try
            {
                preferences.GetMethod("ResetDefaults").Invoke(null, null);
                var sign = world.AddComponent<TextMesh>();
                sign.text = "{SETTINGS_INPUT_TURN-AROUND}";
                var binder = Type.GetType("CurtainCall.Localization.LocalizedDisplays, Assembly-CSharp", true);
                binder.GetMethod("BindLegacy").MakeGenericMethod(typeof(TextMesh)).Invoke(null, new object[] { sign });
                Assert.That(sign.text, Is.EqualTo("휠 클릭").Or.EqualTo("Wheel click"));
                draft.FindAction("Common/TurnAround", true).ApplyBindingOverride(0, "<Keyboard>/f10");
                var args = new object[] { draft, null };
                Assert.That(preferences.GetMethod("TryApply").Invoke(null, args), Is.EqualTo(true), args[1] as string);
                yield return null;
                yield return null;
                Assert.That(sign.text, Is.EqualTo("F10").IgnoreCase);
            }
            finally
            {
                if (hadSaved)
                {
                    preferences.GetMethod("TryApply").Invoke(null, new object[] { previous, null });
                    PlayerPrefs.SetString(key, saved);
                    PlayerPrefs.Save();
                }
                else preferences.GetMethod("ResetDefaults").Invoke(null, null);
                UnityEngine.Object.Destroy(previous);
                UnityEngine.Object.Destroy(draft);
                UnityEngine.Object.Destroy(world);
            }
        }

        [UnityTest]
        public IEnumerator StartMenuAndWorldTextFollowTheLocalLanguage()
        {
            const string pref = "CurtainCall.Language";
            bool hadPref = PlayerPrefs.HasKey(pref);
            string before = PlayerPrefs.GetString(pref, "auto");
            GameObject world = null;
            TMP_FontAsset worldFont = null;
            try
            {
                Switch("ko");
                yield return SceneManager.LoadSceneAsync("Start");
                yield return new WaitForSecondsRealtime(1f);
                var labels = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                var settings = labels.First(x => x.isActiveAndEnabled && x.text == "설정");
                Assert.That(Rendered(settings), Is.EqualTo("설정"));
                Capture("ko");
                yield return null;
                world = new GameObject("Localization test world sign");
                var sign = world.AddComponent<TextMesh>();
                sign.text = "설정";
                var binder = Type.GetType("CurtainCall.Localization.LocalizedDisplays, Assembly-CSharp", true);
                binder.GetMethod("BindLegacy").MakeGenericMethod(typeof(TextMesh)).Invoke(null, new object[] { sign });
                var legacy = new GameObject("Legacy UI", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                legacy.transform.SetParent(world.transform);
                var ui = legacy.GetComponent<Text>();
                ui.text = "종료";
                binder.GetMethod("BindLegacy").MakeGenericMethod(typeof(Text)).Invoke(null, new object[] { ui });
                var dynamic = new GameObject("New world TMP", typeof(TextMeshPro));
                dynamic.transform.SetParent(world.transform);
                var tmp = dynamic.GetComponent<TextMeshPro>();
                // The menu button's static atlas only contains menu glyphs. A new world
                // label uses the project's Korean typeface for its changing prompts.
                worldFont = TMP_FontAsset.CreateFontAsset(Resources.Load<Font>("Fonts/Pretendard-Medium/Pretendard-Medium 1"));
                tmp.font = worldFont;
                tmp.text = "준비 중";
                tmp.ForceMeshUpdate();
                yield return null;
                yield return null;
                Switch("en");
                yield return null;
                Assert.That(Rendered(settings), Is.EqualTo("Settings"));
                Assert.That(sign.text, Is.EqualTo("Settings"));
                Assert.That(ui.text, Is.EqualTo("Quit"));
                Assert.That(Rendered(tmp), Is.EqualTo("Preparing"));
                var hud = Type.GetType("CurtainCall.UI.SceneIntegration.PerformanceHudProfile, Assembly-CSharp", true);
                tmp.text = (string)hud.GetMethod("FormatKeys").Invoke(null,
                    new object[] { "[{side}]로 옆 줄 건너기", null, "F", "Space" });
                Assert.That(Rendered(tmp), Does.Contain("Left click/Right-click"));
                Switch("ko");
                Assert.That(Rendered(tmp), Does.Contain("좌클릭/우클릭"));
                Switch("en");
                Assert.That(settings.text, Is.EqualTo("설정"));
                sign.text = "구조 장갑 줍기";
                yield return null;
                yield return null;
                Assert.That(sign.text, Is.EqualTo("Pick up Rescue Gloves"));
                world.SetActive(false);
                Capture("en");
                yield return null;
                Switch("ko");
                Assert.That(Rendered(settings), Is.EqualTo("설정"));
            }
            finally
            {
                if (world) UnityEngine.Object.Destroy(world);
                if (worldFont) UnityEngine.Object.Destroy(worldFont);
                Switch(before);
                if (!hadPref) { PlayerPrefs.DeleteKey(pref); PlayerPrefs.Save(); }
            }
        }

        static string Rendered(TMP_Text label)
        {
            label.ForceMeshUpdate(true, true);
            return label.GetParsedText();
        }

        static void Capture(string suffix)
        {
            if (!Environment.GetCommandLineArgs().Contains("-captureLocalization")) return;
            var camera = Camera.main;
            if (!camera) camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
            Assert.That(camera, Is.Not.Null);
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                .Where(x => x.isRootCanvas && x.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
            var previousCameras = canvases.Select(x => x.worldCamera).ToArray();
            var distances = canvases.Select(x => x.planeDistance).ToArray();
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            var target = RenderTexture.GetTemporary(1920, 1080, 24);
            var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                foreach (var canvas in canvases)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = camera.nearClipPlane + 1;
                }
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.GetFullPath(".harness/local/localization-start-" + suffix + ".png"), image.EncodeToPNG());
            }
            finally
            {
                for (int i = 0; i < canvases.Length; i++)
                {
                    canvases[i].worldCamera = previousCameras[i];
                    canvases[i].planeDistance = distances[i];
                    canvases[i].renderMode = RenderMode.ScreenSpaceOverlay;
                }
                camera.targetTexture = oldTarget;
                RenderTexture.active = oldActive;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.Destroy(image);
            }
        }
    }
}

