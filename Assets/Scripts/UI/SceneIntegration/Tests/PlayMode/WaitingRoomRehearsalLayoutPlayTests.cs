using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed partial class DevStartLauncherPlayTests
    {
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        static object Priv(object target, string name)
        {
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(name, Any);
                if (field != null) return field.GetValue(target);
                var prop = type.GetProperty(name, Any);
                if (prop != null) return prop.GetValue(target);
            }
            throw new MissingMemberException(target.GetType().Name, name);
        }
        static void SetPriv(object target, string name, object value) => target.GetType().GetField(name, Any).SetValue(target, value);
        static object Invoke(object target, string name, params object[] args)
        {
            foreach (var method in target.GetType().GetMethods(Any))
                if (method.Name == name && method.GetParameters().Length == args.Length) return method.Invoke(target, args);
            throw new MissingMethodException(target.GetType().Name, name);
        }
        static object NestedEnum(object owner, string nested, string value) =>
            System.Enum.Parse(owner.GetType().GetNestedType(nested, BindingFlags.NonPublic), value);

        static object Rehearsal => T(RehearsalType).GetField("instance", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        static Component LocalPlayer => (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
        static ulong LocalId => (ulong)Get(LocalPlayer, "OwnerClientId");

        /// <summary>호스트 리허설을 그 소구간으로 바로 넘긴다(새 회차).</summary>
        static IEnumerator JumpStage(int stage)
        {
            var r = Rehearsal;
            SetPriv(r, "stage", stage);
            SetPriv(r, "round", (int)Priv(r, "round") + 1);
            SetPriv(r, "version", (int)Priv(r, "version") + 1);
            yield return WaitStage(stage, 5f);
            yield return new WaitForSecondsRealtime(.6f);
        }

        static void PlaceOnRope(Component course, int lane, float distance)
        {
            var mover = LocalPlayer.GetComponent(T("CurtainCall.Player.PlayerMover"));
            var position = (Vector3)Call(course, "GetLanePosition", lane, distance);
            Call(mover, "Relocate", position, Quaternion.LookRotation((Vector3)Get(course, "Forward")));
        }

        static bool Blocked(Component course, int lane, float distance) => (bool)Call(course, "IsSegmentBlocked", lane, distance);

        static void AssertRehearsalTrapeze(Component course)
        {
            var source = Get(Static("CurtainCall.Settings.GameSettings", "Tightrope"), "Course");
            Assert.AreEqual(4, Get(course, "LaneCount"));
            Assert.AreEqual(Get(source, "TrapezeLength"), Get(Get(course, "Shape"), "TrapezeLength"));
            Assert.IsTrue((bool)Get(course, "HasTrapezeGap"));
            Assert.IsFalse((bool)Get(course, "HasSwingGap"));
            Assert.IsFalse((bool)Get(course, "HasRelayGap"));
            var trapeze = course.GetComponent(T("CurtainCall.Tightrope.TightropeTrapeze"));
            Assert.IsTrue((bool)Get(trapeze, "IsActive"));
            Assert.AreEqual(2, Get(trapeze, "PoleCount"));
            Assert.AreEqual((int)Get(source, "TrapezeLane") - 1, Get(trapeze, "LaneIndex"));
            Assert.Greater(((Vector3)Call(trapeze, "PoleFeet", 1, false)).y, ((Vector3)Call(trapeze, "PoleFeet", 0, false)).y);
            Assert.IsNull(Call(trapeze, "PoleVisual", 2));
            Assert.IsFalse((bool)Priv(course.GetComponent(T("CurtainCall.Tightrope.TightropeSwingPole")), "HasGap"));
            Assert.AreSame(Resources.Load("Prefabs/Objects/ETCs/TightropeArtKit/TightropeArtProfile"), Get(course, "ArtProfile"));
        }

        static void AssertRehearsalRelay(Component course)
        {
            Assert.AreEqual(2, Get(course, "LaneCount"));
            Assert.IsFalse((bool)Get(course, "HasSwingGap"));
            Assert.IsFalse((bool)Get(course, "HasTrapezeGap"));
            Assert.IsTrue((bool)Get(RelayGate, "IsActive"));
            Assert.IsTrue((bool)Priv(RelayGate, "UsesSpots"));
            Assert.AreEqual(1, Get(RelayGate, "PoleCount"));
            Assert.AreEqual(0, Call(RelayGate, "PoleLane", 0));
            Assert.IsFalse((bool)Call(RelayGate, "IsFarPole", 0));
            Assert.AreEqual(12f, Call(Get(course, "Layout"), "GetLaneEnd", 0));

        }

        [UnityTest]
        public IEnumerator W6_Rehearsal_WholeCourseCaptures()
        {
            FreshTutorial();
            T(RehearsalType).GetField("savedStage", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, 0);
            System.IO.Directory.CreateDirectory(".harness/local/rehearsal/overview");
            try
            {
                yield return Launch(1, "Rehearsal", 0, "Tightrope", "WaitingRoomBase");
                for (int stage = 0; stage < 5; stage++)
                {
                    if (stage > 0) yield return JumpStage(stage);
                    var course = (Component)Priv(Rehearsal, "course");
                    Assert.IsNotNull(course);
                    yield return CaptureWholeCourse(course, stage);
                }
            }
            finally { EndTutorial(); }
        }

        // 실제 생성된 코스만 검토 카메라로 촬영한다. 배치·메시·재질은 바꾸지 않는다.
        static IEnumerator CaptureWholeCourse(Component course, int stage)
        {
            System.IO.Directory.CreateDirectory(".harness/local/rehearsal/overview");
            // 생성/아트 모듈의 다음 프레임 준비가 끝난 뒤 레이어를 분리한다.
            yield return null;
            var transforms = course.GetComponentsInChildren<Transform>(true);
            var layers = new int[transforms.Length];
            for (int i = 0; i < transforms.Length; i++) { layers[i] = transforms[i].gameObject.layer; transforms[i].gameObject.layer = 31; }
            var renderers = course.GetComponentsInChildren<Renderer>(true);
            var hidden = new bool[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) { hidden[i] = renderers[i].forceRenderingOff; renderers[i].forceRenderingOff = false; }
            var go = new GameObject("Course overview capture camera");
            var camera = go.AddComponent<Camera>();
            var lamp = new GameObject("Course overview light");
            var light = lamp.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 2.5f;
            light.cullingMask = 1 << 31; light.shadows = LightShadows.None;
            lamp.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var texture = new RenderTexture(1600, 1200, 24);
            var image = new Texture2D(1600, 1200, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                float finish = (float)Get(course, "FinishDistance");
                Vector3 centre = course.transform.position + Vector3.forward * (finish * .5f) + Vector3.up * .7f;
                camera.transform.position = centre + new Vector3(10f, 36f, -15f);
                camera.transform.LookAt(centre);
                camera.orthographic = true;
                camera.orthographicSize = 24f;
                camera.nearClipPlane = .1f; camera.farClipPlane = 150f;
                camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.12f, .15f, .19f);
                camera.targetTexture = texture;
                camera.Render();
                RenderTexture.active = texture;
                image.ReadPixels(new Rect(0, 0, 1600, 1200), 0, 0); image.Apply();
                System.IO.File.WriteAllBytes(".harness/local/rehearsal/overview/" + new[] { "01-stage1", "02-stage2-1", "03-stage2-2", "04-stage2-3", "05-stage3" }[stage] + ".png", image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                for (int i = 0; i < transforms.Length; i++) if (transforms[i] != null) transforms[i].gameObject.layer = layers[i];
                camera.targetTexture = null;
                for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].forceRenderingOff = hidden[i];
                Object.Destroy(lamp); Object.Destroy(go); Object.Destroy(texture); Object.Destroy(image);
            }
        }

        [UnityTest]
        public IEnumerator W6_Rehearsal_CoursesFollowStageDiagrams()
        {
            FreshTutorial();
            T(RehearsalType).GetField("savedStage", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, 0);
            System.IO.Directory.CreateDirectory(AuditDirectory);
            try
            {
                yield return Launch(1, "Rehearsal", 0, "Tightrope", "WaitingRoomBase");
                yield return WaitStage(0, 10f);
                var ids = new[] { "1", "2-1", "2-2", "2-3", "3" };
                var kinds = new[] { "LaneChange", "SinglePole", "ConsecutivePoles", "Relay", "Trapeze" };
                for (int stage = 0; stage < ids.Length; stage++)
                {
                    if (stage > 0) yield return JumpStage(stage);
                    var course = (Component)Priv(Rehearsal, "course");
                    Assert.AreEqual(ids[stage], Priv(Priv(Rehearsal, "Stage"), "Id"), "원래 다섯 단계 순서를 보존한다");
                    Assert.AreEqual(kinds[stage], Priv(Priv(Rehearsal, "Stage"), "Kind").ToString());
                    Assert.AreEqual(stage == 3 ? "Anyone" : "EveryoneOrMajority", Priv(Priv(Rehearsal, "Stage"), "Rule").ToString());
                    Assert.IsFalse((bool)Static("CurtainCall.Tightrope.PiggybackSystem", "Suppressed"), "공연 목마 연결 규칙 유지");
                    Assert.IsFalse((bool)Priv(course.GetComponent(T("CurtainCall.Tightrope.TightropeSwingPole")), "HasGap"), "실제 공연의 휴면 일반 그네를 켜지 않는다");
                    Assert.AreEqual(stage == 0, course.transform.Find("Generated/IntermediatePlatform_Lane0") != null);
                    if (stage == 3) AssertRehearsalRelay(course);
                    else if (stage == 4) AssertRehearsalTrapeze(course);
                    else if (stage is 1 or 2)
                    {
                        Assert.AreEqual(1, Get(course, "LaneCount"));
                        Assert.IsTrue((bool)Get(course, "HasTrapezeGap"));
                        Assert.AreEqual(stage, Get(course.GetComponent(T("CurtainCall.Tightrope.TightropeTrapeze")), "PoleCount"));
                    }
                    else
                    {
                        Assert.AreEqual(2, Get(course, "LaneCount"));
                        Assert.AreEqual(22f, Call(Get(course, "Layout"), "GetLaneEnd", 0));
                        Assert.IsTrue((bool)Call(course, "IsLaneUsable", 1, 30f));
                        Assert.IsFalse((bool)Call(course, "IsLaneUsable", 0, 30f));
                    }
                    yield return CaptureWholeCourse(course, stage);
                }
                // 배치 검사 종료 준비. 실제 학습/완주 판정은 Learning 및 4인 입력 검사로 검증한다.
                Assert.IsTrue((bool)Call(Rehearsal, "TryClearCurrentStage"));
                float until = Time.realtimeSinceStartup + 5f;
                while (!(bool)Static(RehearsalType, "IsCompleted") && Time.realtimeSinceStartup < until) yield return null;
                Assert.IsTrue((bool)Static(RehearsalType, "IsCompleted"));
            }
            finally { FlowKeys(); EndTutorial(); }
        }

        [UnityTest]
        public IEnumerator W6_Rehearsal_RelayAndExitSmoke()
        {
            FreshTutorial();
            T(RehearsalType).GetField("savedStage", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, 0);
            System.IO.Directory.CreateDirectory(AuditDirectory);
            try
            {
                yield return Launch(1, "Rehearsal", 0, "Tightrope", "WaitingRoomBase");
                yield return JumpStage(3);
                var course = (Component)Priv(Rehearsal, "course");
                AssertRehearsalRelay(course);
                yield return HoldRelayPole(course, 0, (int)Call(RelayGate, "PoleLane", 0), 11f);
                int opened = (int)Call(RelayGate, "OpenedLane", 0);
                Assert.AreNotEqual(Call(RelayGate, "PoleLane", 0), opened);
                Assert.AreEqual(1, opened);
                Assert.IsFalse(Blocked(course, 1, 16f), "점유 복제 상태로 실제 줄이 열림");
                Assert.IsTrue((bool)Call(RelayGate, "IsLaneConnected", opened));
                Assert.IsTrue((bool)Call(RelayGate, "IsUnlimitedHold", 0), "30초 제한만 제외한다");
                var state = Priv(((Array)Priv(RelayGate, "stations")).GetValue(0), "State");
                Invoke(state, "Tick", (double)Priv(RelayGate, "Clock") + 31d, 3f);
                Assert.AreEqual(LocalId, Call(RelayGate, "HolderAt", 0), "30초 뒤에도 점유 유지");
                ReleaseRelayPole(0); yield return null; yield return null;
                Assert.IsFalse((bool)Call(RelayGate, "IsLaneConnected", opened), "놓으면 공연처럼 즉시 연결이 끊긴다");
                Assert.IsTrue(Blocked(course, 1, 16f));
                yield return JumpStage(4);
                AssertRehearsalTrapeze((Component)Priv(Rehearsal, "course"));
                Assert.IsTrue((bool)Call(Rehearsal, "TryClearCurrentStage"));
                yield return null; yield return null;
                Assert.IsTrue((bool)Static(RehearsalType, "IsCompleted"));
                yield return new WaitForSecondsRealtime(4f);
                yield return AuditWalkToStage();
            }
            finally { FlowKeys(); EndTutorial(); }
        }

        [UnityTest]
        public IEnumerator W6_Rehearsal_ExitRouteSmoke()
        {
            FreshTutorial();
            WaitingVariantField.SetValue(WaitingSpaces, Enum.Parse(WaitingVariantField.FieldType, "Base"));
            T(RehearsalType).GetField("savedStage", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, 0);
            System.IO.Directory.CreateDirectory(AuditDirectory);
            var gameView = UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
            bool maximized = gameView.maximized; gameView.maximized = true; gameView.Focus();
            try
            {
                yield return Launch(1, "Rehearsal", 0, "Tightrope", "WaitingRoomBase");
                yield return JumpStage(4);
                // 출구 길안내 검사를 위한 완료 준비. 이후 이동은 실제 입력이다.
                Assert.IsTrue((bool)Call(Rehearsal, "TryClearCurrentStage"));
                float until = Time.realtimeSinceStartup + 5f;
                while (!(bool)Static(RehearsalType, "IsCompleted") && Time.realtimeSinceStartup < until) yield return null;
                Assert.IsTrue((bool)Static(RehearsalType, "IsCompleted"));
                yield return new WaitForSecondsRealtime(4f);
                // 사용자 2026-11-03: 완료 뒤 길안내가 나올 때 카메라가 돌지 않았다. 코스가 사라진 뒤 카메라 입력 잠금이 남지 않아야 한다.
                var director = Find("CurtainCall.Cameras.CameraViewDirector");
                Assert.IsFalse((bool)Priv(director, "inputBlocked"), "완료 뒤 카메라 입력 잠금이 남아 있다");
                Assert.IsTrue((bool)Get(director, "UsesMouseLook"), "완료 뒤 마우스 시점이어야 한다");
                yield return AuditCapture("10-complete-route");
                yield return AuditWalkToStage();
            }
            finally { FlowKeys(); gameView.maximized = maximized; EndTutorial(); }
        }

        const string AuditDirectory = ".harness/local/rehearsal/verification";
        static IEnumerator AuditCapture(string name)
        {
            yield return null; yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath(AuditDirectory + "/" + name + ".png"));
            yield return null; yield return null;
        }

        // 각 시도의 시작 위치만 배치하고 닫힌 철창은 보통 이동 입력으로 밀어 본다.
        IEnumerator AuditClosedGates()
        {
            var mover = LocalPlayer.GetComponent(T("CurtainCall.Player.PlayerMover"));
            var report = new List<string>();
            var failures = new List<string>();
            var gates = Object.FindObjectsByType(T("CurtainCall.WaitingRoom.RehearsalLockdownGate"), FindObjectsSortMode.None);
            foreach (Component gate in gates)
            {
                var box = gate.GetComponent<BoxCollider>();
                foreach (float offset in new[] { 0f, -box.size.x * .5f - .4f, box.size.x * .5f + .4f })
                {
                    var middle = gate.transform.position + gate.transform.right * offset;
                    var direction = gate.transform.forward;
                    var start = middle - direction * 1.5f + Vector3.up * .1f;
                    var hits = Physics.OverlapCapsule(start + Vector3.up * .35f, start + Vector3.up * 1.4f, .3f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    bool occupied = false;
                    foreach (var hit in hits) if (hit.GetComponentInParent(T("CurtainCall.Player.PlayerMover")) == null) occupied = true;
                    if (occupied) { report.Add(gate.name + " offset=" + offset + " start already blocked by wall"); continue; }
                    if (!Physics.Raycast(start + Vector3.up, Vector3.down, 2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    { report.Add(gate.name + " offset=" + offset + " no floor at probe start"); continue; }
                    Call(mover, "Relocate", start, Quaternion.LookRotation(direction));
                    float end = Time.realtimeSinceStartup + 1.2f;
                    while (Time.realtimeSinceStartup < end)
                    {
                        var director = Find("CurtainCall.Cameras.CameraViewDirector");
                        var basis = director != null && (bool)Get(director, "UsesMouseLook") ? (Vector3)Get(director, "ViewForward") : (Vector3)Get(mover, "CourseForward");
                        basis.y = 0; basis.Normalize();
                        float f = Vector3.Dot(direction, basis), r = Vector3.Dot(direction, Vector3.Cross(Vector3.up, basis));
                        var keys = new List<string>();
                        if (f > .3f) keys.Add("W"); else if (f < -.3f) keys.Add("S");
                        if (r > .3f) keys.Add("D"); else if (r < -.3f) keys.Add("A");
                        FlowKeys(keys.ToArray()); yield return null;
                    }
                    FlowKeys(); yield return null;
                    float side = Vector3.Dot(LocalPlayer.transform.position - middle, direction);
                    string row = gate.name + " offset=" + offset.ToString("F2") + " end=" + LocalPlayer.transform.position.ToString("F2") + " crossed=" + (side > .4f);
                    report.Add(row);
                    if (side > .4f) failures.Add(row);
                }
                yield return AuditCapture("gate-" + gate.name);
            }
            System.IO.File.WriteAllLines(AuditDirectory + "/closed-gate-probes.txt", report);
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        IEnumerator AuditWalkToStage()
        {
            var guide = (Component)Priv(Rehearsal, "guide");
            float until = Time.realtimeSinceStartup + 30f;
            while (!(bool)Priv(guide, "navReady") && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue((bool)Priv(guide, "navReady"), "완료 뒤 길안내 NavMesh 생성");
            yield return new WaitForSecondsRealtime(.6f);
            var path = (UnityEngine.AI.NavMeshPath)Priv(guide, "path");
            Assert.AreEqual(UnityEngine.AI.NavMeshPathStatus.PathComplete, path.status, "연습실부터 무대 입구까지 완전한 경로");
            var corners = path.corners;
            System.IO.File.WriteAllLines(AuditDirectory + "/exit-route.txt", Array.ConvertAll(corners, v => v.ToString("F2")));
            Assert.Greater(corners.Length, 1);
            var mover = LocalPlayer.GetComponent(T("CurtainCall.Player.PlayerMover"));
            for (int i = 1; i < corners.Length; i++)
            {
                var walking = FlowWalk(LocalPlayer, mover, corners[i]);
                while (Field(State, "Phase").ToString() == "Rehearsal" && walking.MoveNext()) yield return walking.Current;
                FlowKeys();
                if (Field(State, "Phase").ToString() != "Rehearsal") break;
                yield return AuditCapture("route-" + i.ToString("D2"));
                if (Field(State, "Phase").ToString() != "Rehearsal") break;
            }
            until = Time.realtimeSinceStartup + 10f;
            while (Field(State, "Phase").ToString() == "Rehearsal" && Time.realtimeSinceStartup < until) yield return null;
            Assert.AreEqual("Performance", Field(State, "Phase").ToString(), "실제 이동 집결 뒤 첫 공연");
            yield return new WaitForSecondsRealtime(3f);
            yield return AuditCapture("11-first-performance");
        }

        static object RelayGate => Static("CurtainCall.Tightrope.TightropeRelayGate", "Current");
        static uint PoleRevision(object gate, int index) =>
            (uint)Priv(Priv(((Array)Priv(gate, "stations")).GetValue(index), "State"), "Revision");

        /// <summary>공연 해제봉과 같은 F 잡기 요청(호스트 처리)으로 해제봉을 잡는다.</summary>
        IEnumerator HoldRelayPole(Component course, int index, int lane, float distance)
        {
            PlaceOnRope(course, lane, distance);
            yield return new WaitForSecondsRealtime(.4f);
            var gate = RelayGate;
            var mover = LocalPlayer.GetComponent(T("CurtainCall.Player.PlayerMover"));
            string diagnostic = "active=" + Get(gate, "IsActive") + " available=" + Call(gate, "IsPoleAvailable", index)
                + " canUse=" + Call(gate, "CanUsePole", index, LocalPlayer) + " state=" + Get(LocalPlayer, "State")
                + " simulated=" + Get(mover, "Simulated") + " body=" + mover.GetComponent<CharacterController>().enabled
                + " airborne=" + Get(mover, "IsAirborne") + " position=" + LocalPlayer.transform.position.ToString("F2");
            System.IO.File.WriteAllText(AuditDirectory + "/trigger-diagnostic.txt", diagnostic);
            yield return AuditCapture("trigger-before-grab");
            Invoke(gate, "SubmitGrab", LocalId, index, PoleRevision(gate, index));
            yield return null; yield return null;
            Assert.AreEqual(LocalId, (ulong)Call(gate, "HolderAt", index), $"해제봉 {index + 1}을 잡아야 한다: " + diagnostic);
            Assert.IsTrue((bool)Call(gate, "IsHeld", LocalPlayer), "공연 해제봉처럼 봉에 매달린다");
        }

        static void ReleaseRelayPole(int index)
        {
            var gate = RelayGate;
            Invoke(gate, "ServerHandle", LocalId, NestedEnum(gate, "Operation", "Release"), index, PoleRevision(gate, index));
        }

    }
}
