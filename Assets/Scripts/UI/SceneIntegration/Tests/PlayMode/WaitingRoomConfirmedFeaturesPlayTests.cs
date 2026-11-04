using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    // 2026-10-30 확정 기능: 구역별 탐색 타이머, 장비형 UV 손전등, 거울·연습실 모니터, 배관함 철창, 배치물 외형.
    public sealed partial class DevStartLauncherPlayTests
    {
        object testMouse;
        void MouseRight(bool held)
        {
            var input = T("UnityEngine.InputSystem.InputSystem");
            testMouse ??= input.GetMethod("AddDevice", new[] { typeof(string), typeof(string), typeof(string) }).Invoke(null, new object[] { "Mouse", null, null });
            var stateType = T("UnityEngine.InputSystem.LowLevel.MouseState");
            object state = Activator.CreateInstance(stateType);
            state = stateType.GetMethod("WithButton").Invoke(state, new[] { Enum.Parse(T("UnityEngine.InputSystem.LowLevel.MouseButton"), "Right"), held });
            input.GetMethods().Single(m => m.Name == "QueueStateEvent" && m.IsGenericMethodDefinition).MakeGenericMethod(stateType).Invoke(null, new[] { testMouse, state, -1d });
        }

        [UnityTest]
        public IEnumerator M2_Confirmed_TimerRunsOnlyWhileSomeoneIsInRunningZone()
        {
            yield return Launch(2, "Morning", 0, "Tightrope", "WaitingRoomBase");
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            var mover = player.GetComponent(T("CurtainCall.Player.PlayerMover"));
            var zones = Find("CurtainCall.WaitingRoom.ExplorationTimerZones"); Assert.IsNotNull(zones);
            Assert.Greater((int)Get(zones, "RunningZoneCount"), 0); Assert.Greater((int)Get(zones, "StoppingZoneCount"), 0);
            yield return null; yield return null;
            Assert.AreEqual("Stopped", Field(State, "Timer").ToString(), "로비(멈춤 구역)에서는 멈춘다");
            float before = (float)Field(State, "TimeRemaining");
            yield return new WaitForSecondsRealtime(.4f);
            Assert.AreEqual(before, (float)Field(State, "TimeRemaining"), .001f, "멈춘 동안 줄지 않는다");

            Call(mover, "SetStartPose", new Vector3(23.5f, .05f, 56f), Quaternion.identity); // 배관 구역(재생)
            yield return null; yield return null;
            Assert.AreEqual("Running", Field(State, "Timer").ToString(), "재생 구역에 한 명이라도 있으면 흐른다");
            yield return new WaitForSecondsRealtime(.4f);
            Assert.Less((float)Field(State, "TimeRemaining"), before - .2f, "흐르는 동안 줄어든다");
            float paused = (float)Field(State, "TimeRemaining");

            Call(mover, "SetStartPose", new Vector3(-5f, .05f, 74.4f), Quaternion.identity); // 로비로 복귀
            yield return null; yield return null;
            Assert.AreEqual("Stopped", Field(State, "Timer").ToString(), "전원이 멈춤 구역으로 돌아오면 멈춘다");
            yield return new WaitForSecondsRealtime(.3f);
            Assert.AreEqual(paused, (float)Field(State, "TimeRemaining"), .05f, "줄어든 수치에서 멈춰 있다");
            Assert.AreEqual("RestTime", Field(State, "Phase").ToString());
        }

        [UnityTest]
        public IEnumerator M2_Confirmed_UvFlashlightRevealsMarkWhileHeldAndDiscoversOnce()
        {
            yield return Launch(2, "Morning", 0, "Tightrope", "WaitingRoomBase");
            var state = Static("CurtainCall.WaitingRoom.ExplorationState", "Instance");
            var carry = Static("CurtainCall.WaitingRoom.PropCarry", "Instance");
            var flashlight = Find("CurtainCall.WaitingRoom.UvFlashlight"); Assert.IsNotNull(flashlight);
            var mark = Object.FindObjectsByType(T("CurtainCall.WaitingRoom.UvMark"), FindObjectsSortMode.None).Cast<Component>().Single(m => (string)Get(m, "TargetId") == "structure-a:uv-1");
            var marks = ((GameObject[])mark.GetType().GetField("marks", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(mark)).SelectMany(g => g.GetComponentsInChildren<Renderer>(true)).ToArray();
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            var mover = player.GetComponent(T("CurtainCall.Player.PlayerMover"));

            // 벽 1.1m 앞에 서서 마우스 시선을 벽(-z) 쪽으로 돌린다.
            var director = Find("CurtainCall.Cameras.CameraViewDirector");
            IEnumerator FaceWall()
            {
                Call(mover, "SetStartPose", mark.transform.position + Vector3.forward * 1.1f + Vector3.down * 1.3f, Quaternion.LookRotation(Vector3.back));
                yield return null;
                if (director != null) Call(director, "SetLookRotation", Quaternion.LookRotation(Vector3.back));
                yield return new WaitForSecondsRealtime(.3f);
            }
            // 장비 없이 우클릭: 켜지지 않는다.
            yield return FaceWall();
            MouseRight(true); yield return new WaitForSecondsRealtime(.3f);
            Assert.IsFalse((bool)Get(flashlight, "IsOn"), "UV 등이 없으면 켜지지 않는다");
            MouseRight(false); yield return null;

            yield return PickPlaced("uv-lamp", "S02_Dormitory");
            Assert.AreEqual(-1, Get(((IEnumerable)Call(carry, "Held", (ulong)Get(carry, "LocalClientId"))).Cast<object>().Single(o => (string)Get(o, "ItemId") == "uv-lamp"), "Uses"), "장비형: 내구도 없음");
            yield return FaceWall();
            Assert.IsTrue(marks.All(r => !r.gameObject.activeInHierarchy), "비추기 전에는 안 보인다");
            MouseRight(true);
            float until = Time.realtimeSinceStartup + 3f;
            // 비추는 동안의 켜짐·표시를 기록한다(발견 직후 안내가 입력을 잠깐 막을 수 있다).
            bool sawOn = false, sawLit = false;
            while (Time.realtimeSinceStartup < until && !(sawLit && (bool)Call(state, "IsOpen", "structure-a:uv-1")))
            {
                sawOn |= (bool)Get(flashlight, "IsOn"); sawLit |= (bool)Get(mark, "Lit");
                if (sawLit) Assert.IsTrue(marks.All(r => r.gameObject.activeInHierarchy), "비추는 동안 노란 형광 낙서가 보인다");
                yield return null;
            }
            var reader = player.GetComponent(T("CurtainCall.Player.PlayerInputReader"));
            string inputWhy = $"uvHeld {Field(Get(reader, "Current"), "UvLightHeld")} blocked {Static("CurtainCall.Player.InputGate", "IsBlocked")} held {string.Join(",", ((IEnumerable)Call(carry, "Held", (ulong)Get(carry, "LocalClientId"))).Cast<object>().Select(o => Get(o, "ItemId")))} open {Call(state, "IsOpen", "structure-a:uv-1")} lit {Get(mark, "Lit")} readerEnabled {((Behaviour)reader).isActiveAndEnabled}";
            Assert.IsTrue(sawOn, "장비를 들고 우클릭을 누르면 켜진다 — " + inputWhy);
            var origin = (Vector3)Get(flashlight, "LastOrigin"); var forward = (Vector3)Get(flashlight, "LastForward"); var point = (Vector3)Get(mark, "Point");
            string why = $"origin {origin} forward {forward} aim {Get(flashlight, "LastAim")} mark {point} angle {Vector3.Angle(forward, point - origin):F1} dist {Vector3.Distance(origin, point):F2}"
                + $" cam {(Camera.main != null ? Camera.main.transform.position.ToString() + " fwd " + Camera.main.transform.forward.ToString() : "none")} player {player.transform.position} blocker {Static("CurtainCall.WaitingRoom.UvFlashlight", "LastBlocker")} open {Call(state, "IsOpen", "structure-a:uv-1")}";
            Assert.IsTrue(sawLit, "원뿔 안의 낙서를 비춘다 — " + why);
            Assert.AreEqual(true, Call(state, "IsOpen", "structure-a:uv-1"), "처음 비춘 낙서는 발견으로 기록된다");
            Assert.IsTrue(marks.All(r => r.sharedMaterial != null && r.sharedMaterial.name.StartsWith("UvGlow")), "형광 재질");
            MouseRight(false); yield return null; yield return null; yield return null;
            Assert.IsFalse((bool)Get(flashlight, "IsOn"), "입력을 놓으면 꺼진다");
            Assert.IsTrue(marks.All(r => r.gameObject.activeInHierarchy), "빛이 떠나도 5초 동안 남는다");
            Assert.IsTrue(mark.GetComponentsInChildren<Renderer>(true).Where(r => !marks.Contains(r)).All(r => !r.enabled), "낙서 자리 겉판은 보이지 않는다(2026-11-01)");
            // 오버뷰로 보고 있으면 빛이 꺼져도 닫을 때까지 남는다(2026-11-01).
            var clue = Object.FindObjectsByType(T("CurtainCall.WaitingRoom.ClueInspectable"), FindObjectsInactive.Include, FindObjectsSortMode.None).Cast<Component>()
                .Single(c => (string)Get(c, "TargetId") == "structure-a:uv-1-clue");
            Call(clue, "SetOpen", true); yield return null;
            Assert.IsTrue((bool)Get(clue, "IsOpen"), "드러난 단서를 오버뷰로 연다");
            yield return new WaitForSeconds(5.3f);
            Assert.IsTrue((bool)Get(clue, "IsOpen") && marks.All(r => r.gameObject.activeInHierarchy), "오버뷰를 보는 동안에는 남는다");
            Call(clue, "SetOpen", false); yield return null; yield return null;
            Assert.IsTrue(marks.All(r => !r.gameObject.activeInHierarchy), "오버뷰를 닫으면 다시 숨는다");
            Assert.IsTrue(((IEnumerable)Call(carry, "Held", (ulong)Get(carry, "LocalClientId"))).Cast<object>().Any(o => (string)Get(o, "ItemId") == "uv-lamp"), "발견해도 장비는 남는다");
        }

        [UnityTest]
        public IEnumerator M2_Confirmed_MirrorsMonitorGratingAndPropShapesArePresent()
        {
            yield return Launch(2, "Morning", 0, "Tightrope", "WaitingRoomBase");
            var layout = GameObject.Find("WaitingRoom_LayoutA").transform;
            Assert.AreEqual(6, Object.FindObjectsByType(T("CurtainCall.WaitingRoom.PlanarMirror"), FindObjectsSortMode.None).Length, "화장실 2·탈의 방 1·소품실 3");
            var monitor = layout.Find("S09_PracticeRoom/PracticeMonitor"); Assert.IsNotNull(monitor);
            Assert.IsNotNull(monitor.GetComponent(T("CurtainCall.WaitingRoom.WaitingRoomCctvMonitor")));
            var box = layout.Find("B1_MachineRoom/Exploration_A/PipeBox");
            // 2026-11-01: 철창 대신 스테인리스 문 달린 배관함. 부서지기 전에는 문이 키패드를 가린다.
            Assert.IsNull(box.Find("BoxLid")); Assert.AreEqual(0, box.Cast<Transform>().Count(t => t.name == "GratingBar"), "철창 없음");
            var door = box.Find("Door"); Assert.IsNotNull(door, "배관함 문");
            Bounds db = door.GetComponent<Renderer>().bounds, kb = box.Find("Keypad").GetComponent<Renderer>().bounds;
            Assert.IsTrue(db.min.y < kb.min.y && db.max.y > kb.max.y && db.min.z < kb.min.z && db.max.z > kb.max.z && db.min.x > kb.max.x, "문이 키패드 앞을 가린다");
            var pieces = (Transform[])box.GetComponent(T("CurtainCall.WaitingRoom.BreakableWall")).GetType().GetField("pieces", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(box.GetComponent(T("CurtainCall.WaitingRoom.BreakableWall")));
            Assert.IsTrue(box.Cast<Transform>().Where(t => t.name.StartsWith("Door")).All(t => pieces.Contains(t)), "문은 배관과 함께 무너지는 조각");
            Assert.GreaterOrEqual(layout.GetComponentsInChildren<Transform>(true).Count(t => t.name == "PropShape"), 30, "탐색 배치물 외형");
            var dormitoryUv = layout.Find("S02_Dormitory").GetComponentsInChildren(T("CurtainCall.WaitingRoom.FoundItemSpot"), true).Cast<Component>().Where(s => ((string)Get(s, "SpotId")).Contains("uv-lamp")).ToArray();
            Assert.AreEqual(4, dormitoryUv.Length); Assert.IsTrue(dormitoryUv.All(s => ((string)Get(s, "SpotId")).StartsWith("dormitory-uv-lamp")), "더미 UV 없음");
            Assert.IsFalse(layout.Find("S01_Lobby").GetComponentsInChildren(T("CurtainCall.WaitingRoom.FoundItemSpot"), true).Cast<Component>().Any(s => ((string)Get(s, "SpotId")).Contains("uv-lamp")), "로비에 UV 없음");
        }

        // 2026-11-01: 압수된 노트 뒷면의 새김 "34"를 오버뷰에서 뒤집어 마주 보면 강조되고 설명이 더해진다(찾은 사람 화면에만).
        [UnityTest]
        public IEnumerator M2_Confirmed_NotebookBackEngravingRevealsHintWhenFaced()
        {
            yield return Launch(2, "Morning", 0, "Tightrope", "WaitingRoomBase");
            var state = Static("CurtainCall.WaitingRoom.ExplorationState", "Instance");
            var clue = Object.FindObjectsByType(T("CurtainCall.WaitingRoom.ClueInspectable"), FindObjectsInactive.Include, FindObjectsSortMode.None).Cast<Component>()
                .Single(c => (string)Get(c, "TargetId") == "structure-a:confiscated-notebook");
            Assert.IsTrue((bool)Get(clue, "HasSecret"), "뒷면 새김 글자");
            Assert.IsFalse(((string)Get(clue, "Description")).Contains("34"), "설명에는 34가 없다");
            var unlock = (string)Get(clue, "UnlockTarget");
            if (!(bool)Get(clue, "Revealed") && !string.IsNullOrEmpty(unlock)) { Call(state, "RequestUse", unlock); yield return null; yield return null; }
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            Call(player.GetComponent(T("CurtainCall.Player.PlayerMover")), "SetStartPose", clue.transform.position + Vector3.forward * .9f + Vector3.down * .9f, Quaternion.LookRotation(Vector3.back));
            yield return null; yield return null;
            Call(clue, "SetOpen", true); yield return null;
            Assert.IsTrue((bool)Get(clue, "IsOpen"), "오버뷰를 연다");
            yield return new WaitForSeconds(.6f);
            Assert.IsFalse((bool)Get(clue, "SecretSeen"), "처음 앞면에서는 보이지 않는다");
            var pivot = Camera.main.transform.Find("ClueOverview/Pivot"); Assert.IsNotNull(pivot);
            var face = pivot.GetComponentsInChildren<Transform>().Single(t => t.name == "SecretFace");
            // 새김 면이 카메라를 향하도록 돌린다.
            var cam = Camera.main.transform;
            pivot.rotation = Quaternion.FromToRotation(face.up, -cam.forward) * pivot.rotation;
            yield return new WaitForSeconds(.7f);
            Assert.IsTrue((bool)Get(clue, "SecretSeen"), "새김 면을 마주 보면 찾는다 — facing " + Vector3.Dot(face.up, -cam.forward).ToString("F2"));
            var message = clue.GetComponentsInChildren<TMPro.TMP_Text>(true).Concat(Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None)).FirstOrDefault(t => t.name == "Message");
            Assert.IsNotNull(message); StringAssert.Contains("노트 구석에 \"34\"라는 숫자가 새겨져 있다.", message.text);
            ScreenCapture.CaptureScreenshot(".harness/local/visual/10-notebook-secret.png");
            yield return null; yield return null;
            Call(clue, "SetOpen", false); yield return null;
            Call(clue, "SetOpen", true); yield return null;
            Assert.IsTrue((bool)Get(clue, "SecretSeen"), "다시 열어도 찾은 상태가 남는다");
            Call(clue, "SetOpen", false); yield return null;
        }
    }
}
