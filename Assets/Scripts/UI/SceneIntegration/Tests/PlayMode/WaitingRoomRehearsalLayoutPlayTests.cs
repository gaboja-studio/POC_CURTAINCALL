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
            // Task-097: 기획서 5~7장 구조도대로 줄·발판·그네·트리거 그네·끊긴 구간을 놓고, 연습실 옛 소품은 숨긴다.
            FreshTutorial();
            T(RehearsalType).GetField("savedStage", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, 0);
            var gameView = UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
            bool wasMaximized = gameView.maximized;
            gameView.maximized = true; gameView.Focus();
            System.IO.Directory.CreateDirectory(AuditDirectory);
            try
            {
                yield return Launch(1, "Rehearsal", 0, "Tightrope", "WaitingRoomBase");
                yield return WaitStage(0, 10f);
                yield return new WaitForSecondsRealtime(.6f);
                var room = GameObject.Find("S09_PracticeRoom").transform;
                Assert.IsFalse(room.Find("LegacyRing/PracticeLanes").gameObject.activeSelf, "리허설 중 연습실 옛 소품은 숨긴다");

                yield return AuditCapture("01-stage1-start");
                var initial = LocalPlayer.transform.position;
                var initialRotation = LocalPlayer.transform.rotation;
                yield return AuditClosedGates();
                Call(LocalPlayer.GetComponent(T("CurtainCall.Player.PlayerMover")), "Relocate", initial, initialRotation);
                // 추락 상태 요청 → 호스트 복구 → 같은 구간 출발대.
                Call(LocalPlayer, "RequestState", Enum.Parse(Get(LocalPlayer, "State").GetType(), "Fallen"));
                yield return new WaitForSecondsRealtime(.3f);
                yield return AuditCapture("02-fall");
                float recoveryUntil = Time.realtimeSinceStartup + 6f;
                while (Get(LocalPlayer, "State").ToString() != "Normal" && Time.realtimeSinceStartup < recoveryUntil) yield return null;
                Assert.AreEqual("Normal", Get(LocalPlayer, "State").ToString());
                Assert.AreEqual(0, Static(RehearsalType, "CurrentStage"));
                yield return AuditCapture("03-recovered");

                // 1단계: 위쪽 줄은 중간 지점 발판에서 끝나고, 아래쪽 줄만 도착 장소로 간다.
                var course = (Component)Find("CurtainCall.Tightrope.TightropeCourse");
                var layout = Get(course, "Layout");
                Assert.AreEqual(2, Get(course, "LaneCount"));
                Assert.AreEqual(22f, (float)Call(layout, "GetLaneEnd", 0), .01f);
                Assert.AreEqual(38f, (float)Call(layout, "GetLaneEnd", 1), .01f);
                Assert.IsNull(course.transform.Find("RehearsalPlatform"), "줄 끝에 임의 발판을 추가하지 않는다");

                // 2-1: 그네 1개.
                var firstFinish = (Pose)Call(course, "GetFinishPose", 1);
                Call(LocalPlayer.GetComponent(T("CurtainCall.Player.PlayerMover")), "Relocate", firstFinish.position, firstFinish.rotation);
                yield return WaitStage(1, 5f);
                yield return new WaitForSecondsRealtime(.6f);
                yield return AuditCapture("04-stage2-1");
                course = (Component)Find("CurtainCall.Tightrope.TightropeCourse");
                Assert.IsTrue(Blocked(course, 0, 13f) && !Blocked(course, 0, 6f) && !Blocked(course, 0, 18f), "2-1 그네 자리(공연과 같은 8m)만 끊김");

                // 2-2: 그네 2개가 이어져 있고, 첫 그네를 놓으면 날아가며 두 번째 그네를 잡는다.
                yield return JumpStage(2);
                course = (Component)Find("CurtainCall.Tightrope.TightropeCourse");
                var root = course.GetComponent(T("CurtainCall.Tightrope.TightropeSwingPole"));
                var poles = (IList)Priv(root, "poles");
                Assert.AreEqual(2, poles.Count, "2-2 그네 2개");
                Assert.IsTrue(Blocked(course, 0, 11f) && Blocked(course, 0, 17f) && !Blocked(course, 0, 22f));
                var first = poles[0]; var second = poles[1];
                Assert.AreSame(second, Priv(first, "next"));
                yield return AuditCapture("05-stage2-2-start");
                yield return SwingAcross(course, first, second);
                yield return AuditCapture("06-stage2-2-second-grab");
                // 기능 확인을 마친 봉에서 이탈한 뒤 다음 구간을 배치한다(매달린 상태를 강제로 이월하지 않는다).
                Invoke(second, "OnTeleported"); yield return null; yield return null;

                // 2-3: 위쪽 줄 끝 트리거 그네 1을 잡는 동안만 아래쪽 줄이 이어진다.
                yield return JumpStage(3);
                course = (Component)Find("CurtainCall.Tightrope.TightropeCourse");
                Assert.AreEqual(12f, (float)Call(Get(course, "Layout"), "GetLaneEnd", 0), .01f, "위쪽 줄은 트리거 그네에서 끝");
                Assert.IsTrue(Blocked(course, 1, 15f), "아래쪽 줄 가운데가 끊김");
                yield return HoldTrigger(course, 0, 0, 11.4f);
                Assert.IsFalse(Blocked(course, 1, 15f), "트리거를 잡는 동안 이어짐");
                yield return AuditCapture("07-stage2-3-trigger-held");
                ReleaseTrigger(0);
                yield return null; yield return null;
                Assert.IsTrue(Blocked(course, 1, 15f), "놓으면 다시 끊김");
                yield return AuditCapture("08-stage2-3-released");

                // 3단계: 줄마다 그네, 1줄 트리거 → 2줄 앞쪽 끊김, 2줄 트리거 → 1줄 뒤쪽 끊김(3·4줄 같은 짝).
                yield return JumpStage(4);
                course = (Component)Find("CurtainCall.Tightrope.TightropeCourse");
                root = course.GetComponent(T("CurtainCall.Tightrope.TightropeSwingPole"));
                Assert.AreEqual(4, ((IList)Priv(root, "poles")).Count, "줄마다 인간 그네");
                for (int lane = 0; lane < 4; lane++) Assert.IsTrue(Blocked(course, lane, 7f), $"{lane + 1}줄 그네 끊김");
                Assert.IsTrue(Blocked(course, 0, 20f) && Blocked(course, 1, 18f) && !Blocked(course, 0, 15f) && !Blocked(course, 1, 23f), "엇갈린 끊김");
                yield return HoldTrigger(course, 0, 0, 16.6f);
                Assert.IsFalse(Blocked(course, 1, 18f), "1줄 트리거 → 2줄 연결");
                Assert.IsTrue(Blocked(course, 0, 20f) && Blocked(course, 3, 18f), "다른 줄은 그대로");
                ReleaseTrigger(0);
                yield return HoldTrigger(course, 3, 3, 23.1f);
                Assert.IsFalse(Blocked(course, 2, 20f), "4줄 트리거 → 3줄 연결");
                Assert.IsTrue(Blocked(course, 1, 18f));
                yield return AuditCapture("09-stage3-trigger-held");
                ReleaseTrigger(3);
                yield return null; yield return null;
                var finalPose = (Pose)Call(course, "GetFinishPose", 0);
                Call(LocalPlayer.GetComponent(T("CurtainCall.Player.PlayerMover")), "Relocate", finalPose.position, finalPose.rotation);
                float until = Time.realtimeSinceStartup + 5f;
                while (!(bool)Static(RehearsalType, "IsCompleted") && Time.realtimeSinceStartup < until) yield return null;
                Assert.IsTrue((bool)Static(RehearsalType, "IsCompleted"));
                yield return new WaitForSecondsRealtime(4f);
                yield return AuditCapture("10-complete-route");
                yield return AuditWalkToStage();
            }
            finally
            {
                T(RehearsalType).GetField("savedStage", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, 0);
                FlowKeys();
                gameView.maximized = wasMaximized;
                EndTutorial();
            }
        }

        [UnityTest]
        public IEnumerator W6_Rehearsal_TriggerAndExitSmoke()
        {
            FreshTutorial();
            T(RehearsalType).GetField("savedStage", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, 0);
            System.IO.Directory.CreateDirectory(AuditDirectory);
            var gameView = UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
            bool maximized = gameView.maximized; gameView.maximized = true; gameView.Focus();
            try
            {
                yield return Launch(1, "Rehearsal", 0, "Tightrope", "WaitingRoomBase");
                yield return JumpStage(3);
                var course = (Component)Find("CurtainCall.Tightrope.TightropeCourse");
                yield return HoldTrigger(course, 0, 0, 11.4f);
                Assert.IsFalse(Blocked(course, 1, 15f));
                yield return AuditCapture("07-stage2-3-trigger-held");
                ReleaseTrigger(0); yield return null; yield return null;
                Assert.IsTrue(Blocked(course, 1, 15f));
                yield return AuditCapture("08-stage2-3-released");
                yield return JumpStage(4);
                course = (Component)Find("CurtainCall.Tightrope.TightropeCourse");
                yield return HoldTrigger(course, 3, 3, 23.1f);
                Assert.IsFalse(Blocked(course, 2, 20f));
                yield return AuditCapture("09-stage3-trigger-held");
                ReleaseTrigger(3); yield return null; yield return null;
                var finish = (Pose)Call(course, "GetFinishPose", 0);
                Call(LocalPlayer.GetComponent(T("CurtainCall.Player.PlayerMover")), "Relocate", finish.position, finish.rotation);
                float until = Time.realtimeSinceStartup + 5f;
                while (!(bool)Static(RehearsalType, "IsCompleted") && Time.realtimeSinceStartup < until) yield return null;
                Assert.IsTrue((bool)Static(RehearsalType, "IsCompleted"));
                yield return new WaitForSecondsRealtime(4f);
                yield return AuditCapture("10-complete-route");
                yield return AuditWalkToStage();
            }
            finally { FlowKeys(); gameView.maximized = maximized; EndTutorial(); }
        }

        [UnityTest]
        public IEnumerator W6_Rehearsal_ExitRouteSmoke()
        {
            FreshTutorial();
            T(RehearsalType).GetField("savedStage", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, 0);
            System.IO.Directory.CreateDirectory(AuditDirectory);
            var gameView = UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
            bool maximized = gameView.maximized; gameView.maximized = true; gameView.Focus();
            try
            {
                yield return Launch(1, "Rehearsal", 0, "Tightrope", "WaitingRoomBase");
                yield return JumpStage(4);
                var course = (Component)Find("CurtainCall.Tightrope.TightropeCourse");
                var finish = (Pose)Call(course, "GetFinishPose", 0);
                Call(LocalPlayer.GetComponent(T("CurtainCall.Player.PlayerMover")), "Relocate", finish.position, finish.rotation);
                float until = Time.realtimeSinceStartup + 5f;
                while (!(bool)Static(RehearsalType, "IsCompleted") && Time.realtimeSinceStartup < until) yield return null;
                Assert.IsTrue((bool)Static(RehearsalType, "IsCompleted"));
                yield return new WaitForSecondsRealtime(4f);
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

        /// <summary>공연 해제봉과 같은 F 잡기 요청(호스트 처리)으로 트리거 그네를 잡는다.</summary>
        IEnumerator HoldTrigger(Component course, int index, int lane, float distance)
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
            Assert.AreEqual(LocalId, (ulong)Call(gate, "HolderAt", index), $"트리거 그네 {index + 1}을 잡아야 한다: " + diagnostic);
            Assert.IsTrue((bool)Call(gate, "IsHeld", LocalPlayer), "공연 해제봉처럼 봉에 매달린다");
        }

        static void ReleaseTrigger(int index)
        {
            var gate = RelayGate;
            Invoke(gate, "ServerHandle", LocalId, NestedEnum(gate, "Operation", "Release"), index, PoleRevision(gate, index));
        }

        IEnumerator SwingAcross(Component course, object first, object second)
        {
            float entry = (float)Priv(first, "EntryDistance");
            PlaceOnRope(course, 0, entry);
            yield return new WaitForSecondsRealtime(.5f);
            Invoke(first, "ServerHandle", LocalId, NestedEnum(first, "Operation", "Grab"), 0f);
            yield return null; yield return null;
            Assert.AreEqual("Holding", Priv(first, "mode").ToString(), "첫 그네를 잡는다");
            // W를 누른 것처럼 반동을 키우다가 놓을 수 있게 되면 놓는다.
            float until = Time.realtimeSinceStartup + 12f;
            float target = (float)Priv(second, "EntryDistance");
            // 테스트는 공중 W 조절이 없으므로, 조절 없이도 두 번째 그네 매달림 위치에 닿는 순간 놓는다.
            while ((float)Call(course, "GetDistance", (Vector3)Invoke(first, "PredictLanding", false)) < target - .2f && Time.realtimeSinceStartup < until)
            {
                float speed = (float)Priv(first, "angularSpeed");
                Invoke(first, "ServerHandle", LocalId, NestedEnum(first, "Operation", "Swing"), speed >= 0f ? 1f : -1f);
                yield return null;
            }
            Assert.IsTrue((bool)Priv(first, "OwnCanLetGo"), "반동을 키우면 놓을 수 있어야 한다");
            Debug.Log($"[Test] 놓기 예상 착지 {(float)Call(course, "GetDistance", (Vector3)Invoke(first, "PredictLanding", false)):0.00} / 두 번째 그네 {target:0.00}");
            Invoke(first, "ServerHandle", LocalId, NestedEnum(first, "Operation", "LetGo"), 0f);
            // 공연 공중그네처럼 날아가는 동안 F를 눌러야 잡는다(누르지 않으면 잡지 않는다).
            yield return new WaitForSecondsRealtime(.2f);
            Assert.AreEqual("Idle", Priv(second, "mode").ToString(), "F 없이 자동으로 잡지 않는다");
            until = Time.realtimeSinceStartup + 4f;
            while (Priv(second, "mode").ToString() != "Holding" && Time.realtimeSinceStartup < until)
            {
                SetPriv(first, "airGrabRequestedUntil", (double)Priv(first, "Clock") + .3d);
                yield return null;
            }
            until = Time.realtimeSinceStartup + 4f;
            while (Priv(second, "mode").ToString() != "Holding" && Time.realtimeSinceStartup < until) yield return null;
            Assert.AreEqual("Holding", Priv(second, "mode").ToString(), "날아가는 동안 두 번째 그네를 잡는다");
            Assert.AreEqual(LocalId, (ulong)Priv(second, "holder"));
            Assert.AreEqual("Normal", Get(LocalPlayer, "State").ToString(), "떨어지지 않는다");
        }
    }
}
