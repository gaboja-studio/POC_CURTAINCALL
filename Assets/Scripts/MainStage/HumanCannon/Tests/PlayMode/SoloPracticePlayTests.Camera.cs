using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace CurtainCall.HumanCannon.Tests
{
    public sealed partial class SoloPracticePlayTests
    {
        Component CameraDirector => StaticGet("CurtainCall.Cameras.CameraViewDirector", "Current") as Component;
        bool IsFirstPerson => (bool)Get(CameraDirector, "IsFirstPerson");
        bool IsFreeThirdPerson => (bool)Get(CameraDirector, "IsFreeThirdPerson");

        [UnityTest]
        public IEnumerator OriginalCourse_StartsInFirstPersonWithoutPractice_AndReleasesViewWhenDisabled()
        {
            // F8 연습 진입 없이 원본 씬에서 실제 호스트를 시작한다.
            var host = (System.Threading.Tasks.Task<bool>)Call(session, "HostLanAsync", Type.Missing);
            yield return Until(() => host.IsCompleted, 10, "원본 코스 호스트 시작");
            Assert.IsTrue(host.GetAwaiter().GetResult());
            yield return Until(() =>
            {
                player = StaticGet("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local") as Component;
                return player != null && CameraDirector != null && IsFirstPerson;
            }, 5, "최초 생성부터 기본 1인칭");
            Assert.IsFalse((bool)Get(practice, "IsPracticing"));
            AssertDefaultCourseCamera("아무 대포에도 타지 않은 최초 시작");
            var cannon = (Behaviour)Current("HumanCannonSystem");
            Assert.IsFalse((bool)Call(cannon, "IsSeated", Actor));
            Assert.IsFalse((bool)Call(cannon, "IsFlying", Actor));
            yield return CameraFrames(2);
            float eyeHeight = (float)Get(Get(CameraDirector, "CurrentView"), "EyeHeight");
            if (eyeHeight <= 0) eyeHeight = (float)Get(player.GetComponent(GameType("CurtainCall.Player.PlayerMover")), "BodyHeight") * 0.9f;
            Assert.That(Vector3.Distance(player.transform.position + Vector3.up * eyeHeight, Camera.main.transform.position),
                Is.LessThan(0.08f), "첫 시작 카메라가 캐릭터 뒤가 아닌 눈 위치에 있어야 한다.");

            cannon.enabled = false;
            yield return CameraFrames(3);
            Assert.AreEqual("ThirdPerson", Get(Get(CameraDirector, "CurrentView"), "Kind").ToString(),
                "인간 대포가 비활성화되면 콘텐츠 요청을 해제하고 공용 기본값을 보존한다.");
            cannon.enabled = true;
            yield return Until(() => IsFirstPerson, 3, "원본 코스 재활성화 후 기본 1인칭 복원");
        }

        [UnityTest]
        public IEnumerator FirstCarrierCamera_GrabAndDropSwitchViews_StationaryLookKeepsTrampolinePose()
        {
            yield return Enter(1);
            yield return VerifyCarryCameraAndStationaryPose();
        }

        [UnityTest]
        public IEnumerator OriginalCourse_CarryAndDropSwitchViews_StationaryLookKeepsTrampolinePose()
        {
            yield return Enter(0);
            Call(practice, "ReturnToCourse");
            yield return Until(() => !(bool)Get(practice, "IsBusy"), 5, "원본 코스 준비");
            yield return Until(() => !(bool)StaticGet("CurtainCall.Player.InputGate", "IsBlocked"), 5, "원본 입력 해제");
            Assert.IsFalse((bool)Get(practice, "IsPracticing"));
            var trampoline = Current("HumanCannonTrampoline");
            var mover = player.GetComponent(GameType("CurtainCall.Player.PlayerMover"));
            Call(mover, "SetStartPose", ((Transform)Get(trampoline, "Handle")).position, Quaternion.identity, true);
            yield return CameraFrames(5);
            Assert.IsTrue((bool)Call(trampoline, "CanGrab", player), "원본 손잡이에 실제 접근");
            yield return VerifyCarryCameraAndStationaryPose();
        }

        IEnumerator VerifyCarryCameraAndStationaryPose()
        {
            yield return Until(() => IsFirstPerson, 3, "운반 전 기본 1인칭");
            var director = CameraDirector;
            var trampoline = Current("HumanCannonTrampoline");
            var slot = player.GetComponent(GameType("CurtainCall.Player.PlayerModelSlot"));
            var model = (GameObject)Get(slot, "CurrentModel");
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            Assert.IsNotEmpty(renderers);
            bool[] visibility = renderers.Select(r => r.forceRenderingOff).ToArray();

            // 카메라 공용 테스트와 같이 시선 적분만 대체한다. 실제 마우스/포커스 확인은 사람이 한다.
            // 연습의 InputGate 연결은 계속 실행되므로 Escape와 같은 커서 해제 상태로 마우스 적분만 막는다.
            bool cursorWasReleased = (bool)Get(director, "cursorReleased");
            Set(director, "cursorReleased", true);
            try
            {
                Set(director, "yaw", 35f);
                Set(director, "pitch", -10f);
                yield return CameraFrames(2);
                Assert.IsTrue((bool)Get(director, "HideLocalModel"));
                yield return InspectMainCameraRender(() => renderers.All(r => r.forceRenderingOff), "1인칭 렌더 중 로컬 몸 가림");
                CollectionAssert.AreEqual(visibility, renderers.Select(r => r.forceRenderingOff).ToArray(), "렌더 밖 가림 상태 보존");

                Call(trampoline, "RequestGrab");
                Assert.AreEqual(Actor, Get(trampoline, "Carrier"), "호스트가 실제 손잡이 잡기를 승인");
                yield return Until(() => IsFreeThirdPerson, 3, "잡은 뒤 자유 3인칭");
                Assert.IsFalse(IsFirstPerson);
                Assert.IsFalse((bool)Get(director, "HideLocalModel"));
                Assert.That(Mathf.DeltaAngle(35f, (float)Get(director, "yaw")), Is.EqualTo(0).Within(0.001f), "잡을 때 시선 yaw 유지");
                Assert.That((float)Get(director, "pitch"), Is.EqualTo(-10f).Within(0.001f), "잡을 때 시선 pitch 유지");
                yield return InspectMainCameraRender(() => visibility.SequenceEqual(renderers.Select(r => r.forceRenderingOff)), "3인칭 렌더 중 몸 표시");

                // 입력 리더는 비활성/빈 입력, 실제 운반 조작 전략과 이동 부품은 계속 tick한다.
                controller.enabled = true;
                yield return CameraFrames(3);
                Vector3 bodyPosition = player.transform.position;
                Quaternion bodyRotation = player.transform.rotation;
                Vector3 devicePosition = trampoline.transform.position;
                Quaternion deviceRotation = trampoline.transform.rotation;
                Vector3 beforeView = (Vector3)Get(director, "ViewForward");
                Set(director, "yaw", 125f);
                Set(director, "pitch", -25f);
                yield return CameraFrames(10);
                Assert.That(Vector3.Angle(beforeView, (Vector3)Get(director, "ViewForward")), Is.GreaterThan(80f), "카메라만 주변으로 회전");
                Assert.That(Vector3.Distance(bodyPosition, player.transform.position), Is.LessThan(0.02f), "정지 둘러보기 중 몸 위치 유지");
                Assert.That(Quaternion.Angle(bodyRotation, player.transform.rotation), Is.LessThan(0.01f), "정지 둘러보기 중 몸 방향 유지");
                Assert.That(Vector3.Distance(devicePosition, trampoline.transform.position), Is.LessThan(0.02f), "정지 둘러보기 중 트램폴린 위치 유지");
                Assert.That(Quaternion.Angle(deviceRotation, trampoline.transform.rotation), Is.LessThan(0.01f), "정지 둘러보기 중 트램폴린 방향 유지");

                Call(trampoline, "RequestDrop");
                Assert.AreNotEqual(Actor, Get(trampoline, "Carrier"));
                yield return Until(() => IsFirstPerson, 3, "놓은 뒤 1인칭 복귀");
                Assert.IsFalse(IsFreeThirdPerson);
                Assert.IsTrue((bool)Get(director, "HideLocalModel"));
                Assert.That(Mathf.DeltaAngle(125f, (float)Get(director, "yaw")), Is.EqualTo(0).Within(0.001f), "놓을 때 시선 yaw 유지");
                Assert.That((float)Get(director, "pitch"), Is.EqualTo(-25f).Within(0.001f), "놓을 때 시선 pitch 유지");
            }
            finally
            {
                if (director != null) Set(director, "cursorReleased", cursorWasReleased);
                if (controller != null) controller.enabled = false;
            }
        }

        [UnityTest]
        public IEnumerator CarryCamera_RetryOtherCarrierAndOriginalCourseClearPreviousOwnership()
        {
            yield return Enter(1);
            yield return Until(() => IsFirstPerson, 3, "첫 구간 1인칭");
            var oldTrampoline = Current("HumanCannonTrampoline");
            Call(oldTrampoline, "RequestGrab");
            yield return Until(() => IsFreeThirdPerson, 3, "재시작 전 운반 시점");
            yield return Enter(1);
            yield return Until(() => IsFirstPerson, 3, "잡은 채 재시작 시 기본 1인칭");
            Assert.IsTrue(oldTrampoline == null, "이전 장치 제거");
            Assert.AreNotEqual(Actor, Get(Current("HumanCannonTrampoline"), "Carrier"));

            Call(Current("HumanCannonTrampoline"), "RequestGrab");
            yield return Until(() => IsFreeThirdPerson, 3, "구간 전환 전 운반 시점");
            yield return Enter(6);
            yield return CameraFrames(3);
            AssertDefaultCourseCamera("두 번째 운반 연습은 코스 기본 1인칭");
            Call(Current("HumanCannonTrampoline"), "RequestGrab");
            Assert.AreEqual(Actor, Get(Current("HumanCannonTrampoline"), "Carrier"));
            yield return Until(() => IsFreeThirdPerson, 3, "두 번째 구간도 잡으면 자유 3인칭");
            Call(Current("HumanCannonTrampoline"), "RequestDrop");
            yield return Until(() => IsFirstPerson, 3, "두 번째 구간도 놓으면 1인칭");

            yield return Enter(1);
            Call(Current("HumanCannonTrampoline"), "RequestGrab");
            yield return Until(() => IsFreeThirdPerson, 3, "원본 복귀 전 운반 시점");
            Call(practice, "ReturnToCourse");
            yield return Until(() => !(bool)Get(practice, "IsBusy"), 5, "잡은 채 원본 코스 복귀");
            yield return CameraFrames(3);
            AssertDefaultCourseCamera("원본 코스 시점 복구");
            Assert.IsFalse((bool)Get(practice, "IsPracticing"));
        }

        [UnityTest]
        public IEnumerator FirstCarrierCamera_ForcedDropUsesSpectatorUntilRetry()
        {
            yield return Enter(1);
            Call(Current("HumanCannonTrampoline"), "RequestGrab");
            yield return Until(() => IsFreeThirdPerson, 3, "사망 전 운반 시점");
            Call(player, "ServerSetState", Enum.Parse(GameType("CurtainCall.Network.PlayerSync.PlayerState"), "Fallen"));
            yield return Until(() => (bool)StaticGet("CurtainCall.Cameras.CameraSpectator", "IsSpectating"), 3, "사망 후 관전");
            yield return CameraFrames(3);
            Assert.AreNotEqual(Actor, Get(Current("HumanCannonTrampoline"), "Carrier"), "사망 시 손잡이 강제 해제");
            Assert.IsFalse(IsFirstPerson, "강제 해제 후 사망자는 관전이 우선");
            Assert.IsFalse(IsFreeThirdPerson);
            Assert.IsFalse((bool)Get(CameraDirector, "HideLocalModel"));

            yield return Enter(1);
            yield return Until(() => IsFirstPerson, 3, "재시작 후 기본 1인칭 복구");
            Assert.IsFalse((bool)StaticGet("CurtainCall.Cameras.CameraSpectator", "IsSpectating"));
            AssertBody(0);
        }

        [UnityTest]
        public IEnumerator CourseCamera_CancelCannonKeepsOpenPracticePanelInputBlocked()
        {
            yield return Enter(1);
            var cannon = Current("HumanCannonSystem");
            yield return BoardForCamera(cannon, 0);
            var director = CameraDirector;
            try
            {
                RequestCannon(cannon, 0, "Cancel");
                Assert.IsFalse((bool)Call(cannon, "IsSeated", Actor));
                // 탑승 취소와 같은 프레임에 메뉴를 열어도 시점 전환이 입력 차단을 풀면 안 된다.
                Call(practice, "SetPanel", true);
                Assert.IsTrue((bool)StaticGet("CurtainCall.Player.InputGate", "IsBlocked"));
                Call(director, "PrepareView");
                Assert.IsTrue(IsFirstPerson, "대포 취소 후 기본 1인칭 요청");
                Assert.IsTrue((bool)Get(director, "inputBlocked"), "대포 요청 해제가 연습 메뉴의 마우스 입력 차단을 풀면 안 된다");
                yield return null;
                Assert.IsTrue((bool)Get(director, "inputBlocked"), "다음 프레임에도 메뉴 차단 유지");

                Call(practice, "SetPanel", false);
                yield return Until(() => !(bool)StaticGet("CurtainCall.Player.InputGate", "IsBlocked"), 5, "메뉴 닫기 후 입력 놓기");
                yield return null;
                Assert.IsFalse((bool)Get(director, "inputBlocked"), "메뉴를 닫고 입력을 놓으면 카메라 둘러보기 복구");
                Assert.IsTrue(IsFirstPerson);
            }
            finally
            {
                if (practice != null) Call(practice, "SetPanel", false);
            }
        }

        [UnityTest]
        public IEnumerator OriginalTenCannons_BoardInThirdPersonAndCancelRestoresCourseView()
        {
            yield return Enter(0); // 실제 1인 호스트를 만든 뒤 원본으로 돌아간다.
            Call(practice, "ReturnToCourse");
            yield return Until(() => !(bool)Get(practice, "IsBusy"), 5, "원본 코스 준비");
            yield return Until(() => !(bool)StaticGet("CurtainCall.Player.InputGate", "IsBlocked"), 5, "원본 입력 해제");
            var cannon = Current("HumanCannonSystem");
            Assert.AreEqual(10, Get(cannon, "StationCount"));
            for (int index = 0; index < 10; index++)
            {
                yield return BoardForCamera(cannon, index);
                Assert.IsFalse(IsFirstPerson, "원본 대포 탑승 시 몸을 볼 수 있어야 한다: " + index);
                Assert.IsFalse((bool)Get(CameraDirector, "HideLocalModel"));
                RequestCannon(cannon, index, "Cancel");
                Assert.IsFalse((bool)Call(cannon, "IsSeated", Actor));
                yield return CameraFrames(2);
                AssertDefaultCourseCamera("원본 대포 " + index + " 취소 후 기존 시점 복구");
                AssertBody(0);
            }
        }

        [UnityTest]
        public IEnumerator PracticeCannons_LaunchImmediatelyAtEyes_LandingAndCourseChangesClearFlightView()
        {
            foreach (int sectionIndex in new[] { 0, 9 })
            {
                yield return Enter(sectionIndex);
                var cannon = Current("HumanCannonSystem");
                var pilot = player.GetComponent(GameType(Prefix + "HumanCannonPilot"));
                int station = sectionIndex == 0 ? 0 : 6;
                yield return BoardForCamera(cannon, station);
                RequestCannon(cannon, station, "Launch");
                Assert.IsTrue((bool)Get(pilot, "IsFlying"), "호스트가 실제 발사를 승인");
                yield return null; // 발사 직후 첫 렌더 프레임. 전환 보간이 남아 있으면 눈 위치와 다르다.
                Assert.IsTrue(IsFirstPerson, "발사 즉시 1인칭: " + sectionIndex);
                Assert.IsFalse(IsFreeThirdPerson);
                Assert.IsTrue((bool)Get(CameraDirector, "HideLocalModel"));
                float eyeHeight = (float)Get(Get(CameraDirector, "CurrentView"), "EyeHeight");
                if (eyeHeight <= 0) eyeHeight = (float)Get(player.GetComponent(GameType("CurtainCall.Player.PlayerMover")), "BodyHeight") * 0.9f;
                Assert.That(Vector3.Distance(player.transform.position + Vector3.up * eyeHeight, Camera.main.transform.position),
                    Is.LessThan(0.08f), "첫 비행 화면은 이전 3인칭 위치에 남지 않는다");
                var model = (GameObject)Get(player.GetComponent(GameType("CurtainCall.Player.PlayerModelSlot")), "CurrentModel");
                var renderers = model.GetComponentsInChildren<Renderer>(true);
                yield return InspectMainCameraRender(() => renderers.All(r => r.forceRenderingOff), "비행 1인칭 렌더 중 로컬 몸 가림");

                if (sectionIndex == 0)
                {
                    yield return Enter(1); // 비행 중 복사본 교체와 기본 시점 복구.
                    Assert.IsTrue(pilot == null, "비행 중이던 이전 pilot 제거");
                    yield return Until(() => IsFirstPerson, 3, "첫 운반 연습 기본 시점");
                    Call(Current("HumanCannonTrampoline"), "RequestGrab");
                    yield return Until(() => IsFreeThirdPerson, 3, "이전 비행 priority 200 요청이 새 운반 시점을 덮지 않음");
                }
                else
                {
                    yield return Until(() => !(bool)Get(pilot, "IsFlying"), 8, "최종 대포 실제 안전 착지");
                    yield return CameraFrames(2);
                    AssertBody(0);
                    Assert.AreEqual(1, Get(cannon, "LocalSafeLandingCount"));
                    AssertDefaultCourseCamera("안전 착지 후 기존 시점 복구");

                    yield return BoardForCamera(cannon, station);
                    RequestCannon(cannon, station, "Launch");
                    Assert.IsTrue((bool)Get(pilot, "IsFlying"));
                    yield return Until(() => IsFirstPerson, 3, "원본 복귀 전 비행 시점");
                    Call(practice, "ReturnToCourse");
                    yield return Until(() => !(bool)Get(practice, "IsBusy"), 5, "비행 중 원본 코스 복귀");
                    yield return CameraFrames(2);
                    Assert.IsTrue(pilot == null);
                    AssertDefaultCourseCamera("비행 중 원본 복귀 시 카메라 요청 해제");
                }
            }
        }

        IEnumerator BoardForCamera(Component cannon, int index)
        {
            var station = Call(cannon, "Station", index);
            var boarding = (Transform)Get(station, "BoardingPoint");
            var mover = player.GetComponent(GameType("CurtainCall.Player.PlayerMover"));
            var body = (CharacterController)Get(mover, "Body");
            Call(mover, "SetStartPose", boarding.position, Quaternion.identity, true);
            yield return null;
            // 원본 코스 재활성화/순간이동은 CharacterController 접지를 다시 계산한다.
            // 첫 1프레임을 접지로 가정하지 않고, 실제 탑승에 필요한 지상 상태를 기다린다.
            float deadline = Time.realtimeSinceStartup + 3;
            while ((!body.isGrounded || (bool)Get(player, "IsAirborne")) && Time.realtimeSinceStartup < deadline) yield return null;
            string context = "station=" + index + " state=" + Get(player, "State")
                + " grounded=" + body.isGrounded + " jump=" + Get(player, "CurrentJump")
                + " position=" + player.transform.position + " boarding=" + boarding.position
                + " active=" + ((Behaviour)station).isActiveAndEnabled + " flying=" + Call(cannon, "IsFlying", Actor);
            Assert.IsTrue(body.isGrounded && !(bool)Get(player, "IsAirborne"), "실제 탑승 준비 접지: " + context);
            Assert.AreEqual(index, Call(cannon, "FindBoardableStation", player), "실제 대포 탑승 거리: " + context);
            RequestCannon(cannon, index, "Board");
            Assert.AreEqual(Actor, Call(cannon, "Occupant", index), "실제 호스트 탑승 승인");
            yield return Until(() => IsFreeThirdPerson, 3, "대포 탑승 3인칭: " + index);
        }

        static void RequestCannon(Component cannon, int index, string action) =>
            Call(cannon, "Request", index, Enum.Parse(GameType(Prefix + "CannonAction"), action));

        void AssertDefaultCourseCamera(string reason)
        {
            Assert.IsTrue(IsFirstPerson, reason);
            Assert.IsFalse(IsFreeThirdPerson, reason);
            Assert.IsTrue((bool)Get(CameraDirector, "HideLocalModel"), reason);
            Assert.AreEqual("FirstPerson", Get(Get(CameraDirector, "CurrentView"), "Kind").ToString(), reason);
        }

        static IEnumerator CameraFrames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        static IEnumerator InspectMainCameraRender(Func<bool> inspect, string reason)
        {
            bool rendered = false, matched = false;
            void OnBegin(ScriptableRenderContext context, Camera camera)
            {
                if (rendered || camera != Camera.main) return;
                rendered = true;
                matched = inspect();
            }
            RenderPipelineManager.beginCameraRendering += OnBegin;
            try
            {
                for (int i = 0; i < 30 && !rendered; i++) yield return null;
                if (!rendered) Camera.main.Render();
            }
            finally { RenderPipelineManager.beginCameraRendering -= OnBegin; }
            Assert.IsTrue(rendered, "게임 카메라 실제 렌더 이벤트");
            Assert.IsTrue(matched, reason);
        }
    }
}
