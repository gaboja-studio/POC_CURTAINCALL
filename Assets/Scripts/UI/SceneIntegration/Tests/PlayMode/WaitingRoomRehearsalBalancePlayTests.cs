using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    /// <summary>2026-11-04 사용자: 리허설 균형바가 공연과 다르다는 보고. 공연·리허설에서 줄 위 균형바 화면을 같은 조건으로 남긴다.</summary>
    public sealed partial class DevStartLauncherPlayTests
    {
        static IEnumerator CaptureBalance(string name)
        {
            System.IO.Directory.CreateDirectory(".harness/local/rehearsal/balance");
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath(".harness/local/rehearsal/balance/" + name + ".png"));
            yield return null;
        }

        static string DescribeBalanceViews()
        {
            var text = new System.Text.StringBuilder();
            foreach (var view in Object.FindObjectsByType(T("CurtainCall.UI.PerformanceHudV1.TightropeBalanceView"), FindObjectsSortMode.None))
            {
                var component = (Component)view;
                var canvas = component.GetComponentInParent<Canvas>();
                var scaler = canvas != null ? canvas.rootCanvas.GetComponent<UnityEngine.UI.CanvasScaler>() : null;
                var rect = (RectTransform)component.transform;
                text.Append(component.transform.root.name).Append('/').Append(component.name)
                    .Append(" active=").Append(component.gameObject.activeInHierarchy)
                    .Append(" match=").Append(scaler != null ? scaler.screenMatchMode + ":" + scaler.matchWidthOrHeight : "-")
                    .Append(" pos=").Append(rect.anchoredPosition).Append(" size=").Append(rect.rect.size).Append('\n');
            }
            return text.ToString();
        }

        static Component VisibleBalance()
        {
            var visible = new List<Component>();
            foreach (var item in Object.FindObjectsByType(T("CurtainCall.UI.PerformanceHudV1.TightropeBalanceView"), FindObjectsSortMode.None))
            {
                var view = (Component)item;
                if (((CanvasGroup)Priv(view, "visibility")).alpha > .99f && (bool)Get(view, "HasValidData")) visible.Add(view);
            }
            Assert.AreEqual(1, visible.Count, "줄 위의 유효한 균형 UI는 정확히 하나여야 한다. 존재만 확인하거나 중복 표시를 놓치지 않는다.");
            foreach (var item in Object.FindObjectsByType(T("CurtainCall.Player.BalanceGaugeView"), FindObjectsSortMode.None))
            {
                var legacy = (Behaviour)item;
                var canvas = legacy.GetComponent<Canvas>();
                Assert.IsFalse(legacy.isActiveAndEnabled && canvas != null && canvas.enabled, "옛 균형 게이지가 함께 표시됨");
            }
            return visible[0];
        }

        static void SetBalanceSample(Component view, float risk = 0f)
        {
            Call(view, "SetContext", true, false);
            Assert.IsTrue((bool)Call(view, "SetBalance", .25f, -.3f, .3f, -.5f, .5f, risk, "A", "D", true, false));
            Canvas.ForceUpdateCanvases();
        }

        static string BalanceSignature(Component view)
        {
            var rows = new List<string>();
            var canvas = view.GetComponentInParent<Canvas>().rootCanvas;
            var scaler = canvas.GetComponent<CanvasScaler>();
            rows.Add($"canvas={canvas.renderMode}/{canvas.scaleFactor:F3}/{canvas.additionalShaderChannels}/{canvas.sortingOrder};scaler={scaler.uiScaleMode}/{scaler.screenMatchMode}/{scaler.referenceResolution}");
            foreach (var rect in view.GetComponentsInChildren<RectTransform>(true))
            {
                string path = "";
                for (var node = rect.transform; node != view.transform; node = node.parent) path = node.name + "/" + path;
                rows.Add($"{path}rect={rect.anchoredPosition.ToString("F3")}/{rect.sizeDelta.ToString("F3")}/{rect.anchorMin}/{rect.anchorMax}/{rect.pivot}/{rect.localScale};active={rect.gameObject.activeSelf}");
                if (rect.TryGetComponent<Graphic>(out var graphic)) rows.Add($"{path}graphic={graphic.GetType().Name}/{graphic.color}");
                if (rect.TryGetComponent<Image>(out var image)) rows.Add($"{path}image={image.type}/{image.fillMethod}/{image.fillOrigin}/{image.fillAmount:F3}/{image.sprite?.name}");
                if (rect.TryGetComponent<TMP_Text>(out var text))
                {
                    string face = text.font != null ? text.font.faceInfo.familyName + "/" + text.font.faceInfo.styleName : "none";
                    rows.Add($"{path}text={text.text}/{face}/{text.fontSize}/{text.fontSizeMin}/{text.fontSizeMax}/{text.enableAutoSizing}/{text.textWrappingMode}/{text.overflowMode}");
                }
            }
            foreach (string type in new[] { "BalanceRiskBlink", "BalanceZoneColors" })
            {
                var behaviour = view.GetComponent(T("CurtainCall.UI.SceneIntegration." + type));
                Assert.IsNotNull(behaviour, "공연 런타임 균형 스타일 누락: " + type);
                rows.Add(type + "=" + JsonUtility.ToJson(behaviour));
            }
            rows.Sort(System.StringComparer.Ordinal);
            return string.Join("\n", rows);
        }

        static string[] OriginalCourseComponents(Component course)
        {
            var types = new List<string>();
            foreach (var component in course.GetComponents<MonoBehaviour>())
            {
                string type = component.GetType().FullName;
                // 공연 진행/보상 연결과 Dev 봇은 리허설 진행 관리자가 대체한다.
                if (type.StartsWith("CurtainCall.Tightrope.") && !type.EndsWith("TightropeCampaignBridge") && !type.EndsWith("TightropePracticeBot")) types.Add(type);
            }
            return types.ToArray();
        }

        static string CourseRulesSignature(object shape)
        {
            var copy = Call(shape, "Clone");
            // 문서가 지정한 배치/발판 크기/활성 기물만 제외한다. 물리·균형·나머지 설정은 공연과 같아야 한다.
            JsonUtility.FromJsonOverwrite("{\"laneCount\":4,\"laneEndDistances\":[],\"startPlatformLength\":0,\"finishPlatformLength\":0,\"trapezeLane\":0,\"trapezeLength\":0,\"finishDistance\":0,\"laneChangeZones\":[],\"swingGapStart\":0,\"trapezeStart\":0,\"swingGapEnabled\":false,\"relayGapEnabled\":false,\"trapezeEnabled\":false}", copy);
            return JsonUtility.ToJson(copy);
        }

        static string NativeDeviceSignature(Component course, bool relay)
        {
            var rows = new List<string>();
            var device = course.GetComponent(T("CurtainCall.Tightrope." + (relay ? "TightropeRelayGate" : "TightropeTrapeze")));
            rows.Add(JsonUtility.ToJson(device));
            int count = (int)Get(device, "PoleCount");
            if (!relay) count = Mathf.Min(count, 2); // 연습하는 첫 두 원본 봉의 상대 배치/표시를 비교한다.
            rows.Add("count=" + count);
            if (relay) rows.Add("required=" + Get(device, "RequiredHolders"));
            else rows.Add("lane=" + Get(device, "LaneIndex"));
            float from = (float)Get(course, relay ? "SwingGapFrom" : "TrapezeGapFrom");
            Vector3 forward = (Vector3)Get(course, "Forward"), right = (Vector3)Get(course, "Right");
            for (int i = 0; i < count; i++)
            {
                var feet = (Vector3)(relay ? Call(device, "PoleFeet", i) : Call(device, "PoleFeet", i, false)) - course.transform.position;
                int lane = relay ? (int)Call(device, "PoleLane", i) : (int)Get(device, "LaneIndex");
                var laneOrigin = (Vector3)Call(course, "GetLanePosition", lane, 0f) - course.transform.position;
                rows.Add($"{i}:feet={Vector3.Dot(feet - laneOrigin, right):F3}/{feet.y:F3}/{Vector3.Dot(feet, forward) - from:F3}");
                if (relay) rows.Add($"{i}:far={Call(device, "IsFarPole", i)}");
                var view = (Transform)Call(device, "PoleVisual", i);
                Assert.IsNotNull(view, "실제로 표시된 봉이 있어야 한다");
                Assert.IsTrue(view.gameObject.activeInHierarchy);
                rows.Add($"{i}:scale={view.localScale}");
                if (view.TryGetComponent<LineRenderer>(out var line)) rows.Add($"{i}:line={line.startWidth}/{line.endWidth}/{line.startColor}/{line.endColor}");
                foreach (var renderer in view.GetComponentsInChildren<Renderer>())
                {
                    foreach (var material in renderer.sharedMaterials) rows.Add($"{i}:material={material?.name}/{material?.shader.name}");
                    if (renderer.TryGetComponent<MeshFilter>(out var filter)) rows.Add($"{i}:mesh={filter.sharedMesh?.name}");
                }
            }
            return string.Join("\n", rows);
        }

        [UnityTest]
        public IEnumerator W6_Rehearsal_BalanceBarMatchesPerformanceCapture()
        {
            T(RehearsalType).GetField("savedStage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).SetValue(null, 0);
            try
            {
                yield return Launch(1, "Performance", 0, "Tightrope", "Tightrope");
                float until = Time.realtimeSinceStartup + 30f;
                while ((bool)Static("CurtainCall.UI.SceneIntegration.SceneCurtainTransition", "IsActive") && Time.realtimeSinceStartup < until) yield return null;
                yield return new WaitForSecondsRealtime(6f);
                var course = (Component)Find("CurtainCall.Tightrope.TightropeCourse");
                var localPlayer = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
                var mover = localPlayer.GetComponent(T("CurtainCall.Player.PlayerMover"));
                var at = (Vector3)Call(course, "GetLanePosition", 0, 4f);
                Call(mover, "Relocate", at, Quaternion.LookRotation((Vector3)Get(course, "Forward")));
                yield return new WaitForSecondsRealtime(1.5f);
                System.IO.Directory.CreateDirectory(".harness/local/rehearsal/balance");
                System.IO.File.WriteAllText(".harness/local/rehearsal/balance/performance.txt", DescribeBalanceViews());
                Assert.AreEqual("Assets/Scenes/Tests/MainStage/Tightrope/Tightrope.unity", course.gameObject.scene.path, "Start 경로의 실제 공연 씬을 기준으로 검사한다");
                Assert.IsFalse((bool)Get(course, "HasSwingGap"));
                Assert.IsTrue((bool)Get(course, "HasRelayGap"));
                Assert.IsTrue((bool)Get(course, "HasTrapezeGap"));
                Assert.IsFalse((bool)Priv(course.GetComponent(T("CurtainCall.Tightrope.TightropeRelayGate")), "UnlimitedHold"), "공연의 유지 제한은 보존한다");
                Assert.IsTrue((bool)Priv(course.GetComponent(T("CurtainCall.Tightrope.TightropeTrapeze")), "HoldTimeLimited"));
                string rules = CourseRulesSignature(Get(course, "Shape"));
                string relaySettings = JsonUtility.ToJson(course.GetComponent(T("CurtainCall.Tightrope.TightropeRelayGate"))), trapezePresentation = NativeDeviceSignature(course, false);
                var originalTypes = OriginalCourseComponents(course);
                System.IO.File.WriteAllLines(".harness/local/rehearsal/balance/performance-components.txt", originalTypes);
                var originalArt = Get(course, "ArtProfile");
                var originalPrefab = Field(Field(Get(Static("CurtainCall.Network.Session.NetworkSessionManager", "Instance"), "NetworkManager"), "NetworkConfig"), "PlayerPrefab");
                var originalScheme = Get(localPlayer.GetComponent(T("CurtainCall.Player.PlayerController")), "Scheme").GetType();
                var originalPlayerBalance = localPlayer.GetComponent(T("CurtainCall.Player.PlayerBalance"));
                float green = (float)Get(originalPlayerBalance, "GreenLimit"), warning = (float)Get(originalPlayerBalance, "WarningLimit");
                var originalReader = localPlayer.GetComponent(T("CurtainCall.Player.PlayerInputReader"));
                var bindings = new[] { (string)Call(originalReader, "BindingLabel", "Interact", "F"), (string)Call(originalReader, "BindingLabel", "Action", "Space"),
                    (string)Call(originalReader, "BindingPartLabel", "Posture", "negative", "A"), (string)Call(originalReader, "BindingPartLabel", "Posture", "positive", "D") };
                var originalView = VisibleBalance();
                ((Behaviour)Find("CurtainCall.UI.SceneIntegration.PerformanceSceneBinding")).enabled = false;
                SetBalanceSample(originalView);
                string expected = BalanceSignature(originalView);
                System.IO.File.WriteAllText(".harness/local/rehearsal/balance/performance-signature.txt", expected);
                yield return CaptureBalance("performance");
                SetBalanceSample(originalView, .85f);
                yield return null; yield return null;
                Assert.IsTrue((bool)Get(originalView.GetComponent(T("CurtainCall.UI.SceneIntegration.BalanceRiskBlink")), "IsBlinking"));

                yield return Launch(1, "Rehearsal", 0, "Tightrope", "WaitingRoomBase");
                yield return WaitStage(0, 10f);
                for (int stage = 0; stage < 5; stage++)
                {
                    if (stage > 0) yield return JumpStage(stage);
                    course = (Component)Priv(Rehearsal, "course");
                    foreach (string type in originalTypes) Assert.IsNotNull(course.GetComponent(T(type)), $"소구간 {stage}: 공연 컴포넌트 누락 {type}");
                    foreach (string type in new[] { "PiggybackSystem", "PiggybackFollow" })
                        Assert.IsNotNull(course.GetComponent(T("CurtainCall.Tightrope." + type)), $"소구간 {stage}: 공연 협동 컴포넌트 누락 {type}");
                    Assert.AreEqual(rules, CourseRulesSignature(Get(course, "Shape")), "코드 기본값이나 임의 수치 대신 실제 공연 설정을 복사해야 한다");
                    Assert.IsFalse((bool)Get(course, "HasSwingGap"));
                    var ordinary = course.GetComponent(T("CurtainCall.Tightrope.TightropeSwingPole"));
                    Assert.IsFalse((bool)Priv(ordinary, "HasGap"));
                    Assert.IsNull(Get(ordinary, "PoleVisual"), "공연에서 휴면인 그네의 예측 착지/해제 표시를 만들지 않는다");
                    Assert.AreEqual(stage == 3, Priv(course.GetComponent(T("CurtainCall.Tightrope.TightropeRelayGate")), "UsesSpots"));
                    Assert.IsFalse((bool)Get(course, "HasRelayGap"), "2-3은 기획서의 단일 트리거 배치");
                    Assert.AreEqual(stage == 1 || stage == 2 || stage == 4, Get(course, "HasTrapezeGap"));
                    if (stage == 3)
                    {
                        Assert.AreEqual(relaySettings, JsonUtility.ToJson(course.GetComponent(T("CurtainCall.Tightrope.TightropeRelayGate"))), "원본 봉 컴포넌트 설정 보존");
                        AssertRehearsalRelay(course);
                        var views = (System.Collections.IList)Priv(course.GetComponent(T("CurtainCall.Tightrope.TightropeRelayGate")), "relayPoleViews");
                        foreach (var poleView in views) Assert.IsFalse(((Transform)Priv(Priv(poleView, "Clock"), "Root")).gameObject.activeSelf, "리허설의 유지 시간 LED는 숨긴다");
                    }
                    if (stage == 4) Assert.AreEqual(trapezePresentation, NativeDeviceSignature(course, false), "실제 공연 공중그네 간격/높이/아트/표시 설정을 복사해야 한다");
                    Assert.IsFalse((bool)Static("CurtainCall.Tightrope.PiggybackSystem", "Suppressed"));
                    Assert.AreSame(originalArt, Get(course, "ArtProfile"));
                    Assert.AreSame(originalPrefab, Field(Field(Get(Static("CurtainCall.Network.Session.NetworkSessionManager", "Instance"), "NetworkManager"), "NetworkConfig"), "PlayerPrefab"));
                    localPlayer = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
                    mover = localPlayer.GetComponent(T("CurtainCall.Player.PlayerMover"));
                    at = (Vector3)Call(course, "GetLanePosition", 0, 1f);
                    Call(mover, "Relocate", at, Quaternion.LookRotation((Vector3)Get(course, "Forward")));
                    yield return new WaitForSecondsRealtime(1.5f);
                    Assert.AreEqual(originalScheme, Get(localPlayer.GetComponent(T("CurtainCall.Player.PlayerController")), "Scheme").GetType());
                    var playerBalance = localPlayer.GetComponent(T("CurtainCall.Player.PlayerBalance"));
                    Assert.AreEqual(green, (float)Get(playerBalance, "GreenLimit"), .0001f, "공연과 다른 기본 안전 균형 경계");
                    Assert.AreEqual(warning, (float)Get(playerBalance, "WarningLimit"), .0001f, "공연과 다른 기본 경고 균형 경계");
                    var reader = localPlayer.GetComponent(T("CurtainCall.Player.PlayerInputReader"));
                    CollectionAssert.AreEqual(bindings, new[] { (string)Call(reader, "BindingLabel", "Interact", "F"), (string)Call(reader, "BindingLabel", "Action", "Space"),
                        (string)Call(reader, "BindingPartLabel", "Posture", "negative", "A"), (string)Call(reader, "BindingPartLabel", "Posture", "positive", "D") });
                    var view = VisibleBalance();
                    ((Behaviour)Find("CurtainCall.UI.SceneIntegration.PerformanceSceneBinding")).enabled = false;
                    SetBalanceSample(view);
                    string actual = BalanceSignature(view);
                    System.IO.File.WriteAllText($".harness/local/rehearsal/balance/rehearsal-{stage}-signature.txt", actual);
                    yield return CaptureBalance("rehearsal-" + stage);
                    Assert.AreEqual(expected, actual, $"소구간 {stage}: 공연의 실제 균형 UI와 다름(전체 Rect/색상/폰트/키/렌더/스타일 비교)");
                    SetBalanceSample(view, .85f);
                    yield return null; yield return null;
                    Assert.IsTrue((bool)Get(view.GetComponent(T("CurtainCall.UI.SceneIntegration.BalanceRiskBlink")), "IsBlinking"), "원본 70% 위험 경고가 동작해야 한다");
                }
            }
            finally
            {
                T(RehearsalType).GetField("savedStage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).SetValue(null, 0);
            }
        }
    }
}
