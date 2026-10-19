using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.HumanCannon.Tests
{
    public sealed partial class FinalCannonPlayTests
    {
        [UnityTest]
        public IEnumerator CarryMovement_StrafeBackpedalMatchesAirSpeed_AndRestoresAfterDropRestartAndExit()
        {
            DisableEnhancementThreats();
            var trampoline = StaticGet("CurtainCall.HumanCannon.HumanCannonTrampoline", "Current") as Component;
            var handle = (Transform)Get(trampoline, "Handle");
            var character = Get(StaticGet("CurtainCall.Settings.GameSettings", "Base"), "Character");
            float walkSpeed = (float)Get(character, "FreeMoveSpeed");
            float airSpeed = (float)Get(trampoline, "groundBounceAirControlSpeed");
            Assert.AreEqual(4.5f, airSpeed);
            for (int i = 0; i < (int)Get(system, "StationCount"); i++)
                Assert.AreEqual(airSpeed, Get(Call(system, "Station", i), "AirControlSpeed"));

            Call(mover, "SetStartPose", handle.position, Quaternion.Euler(0, 35, 0), true);
            yield return null;
            Input(tapped: true);
            yield return WaitUntil(() => (ulong)Get(trampoline, "Carrier") == Actor, 2, "F 운반 시작");
            var director = StaticGet("CurtainCall.Cameras.CameraViewDirector", "Current") as Component;
            yield return WaitUntil(() => (bool)Get(director, "IsFreeThirdPerson"), 2, "운반 3인칭");
            SetCarryView(director, 35, 0);
            yield return new WaitForEndOfFrame();
            Quaternion facing = player.transform.rotation, deviceFacing = trampoline.transform.rotation;
            Call(mover, "SetStartPose", new Vector3(0, 0.01f, 57), facing, true);
            var body = (CharacterController)Get(mover, "Body");
            yield return WaitUntil(() => body.isGrounded && !(bool)Get(mover, "IsAirborne"), 3, "운반 측정 전 실제 바닥 접촉");
            yield return new WaitForEndOfFrame();
            Vector3 offset = (Vector3)Get(trampoline, "Surface") - player.transform.position;

            for (int lost = 0; lost <= 2; lost++)
            {
                if (lost > 0) Call(player, "ServerCut", 1);
                yield return null; yield return null;
                float injury = (float)Call(Get(system, "Injuries"), "AirMultiplier", lost);
                Assert.That(walkSpeed * (float)Get(mover, "SpeedMultiplier"), Is.EqualTo(airSpeed * injury).Within(.001f));
                foreach (Vector3 direction in new[] { Vector3.right, Vector3.left, Vector3.back,
                    Vector3.forward, new Vector3(1, 0, -1).normalized })
                {
                    // 같은 실제 조작 규칙·CharacterController를 한 프레임씩 측정한다. 화면/키보드는 조작하지 않는다.
                    yield return new WaitForEndOfFrame();
                    Vector3 start = player.transform.position;
                    CarryMovementInput(direction);
                    Call(mover, "Update");
                    Call(trampoline, "LateUpdate");
                    Vector3 moved = Vector3.ProjectOnPlane(player.transform.position - start, Vector3.up);
                    Assert.That(Vector3.Distance(moved, direction * (airSpeed * injury * Time.deltaTime)), Is.LessThan(.003f),
                        "직선/대각선 실제 이동 속도가 같은 부상 상태의 공중 조작 속도와 같아야 한다.");
                    Assert.That(Quaternion.Angle(player.transform.rotation, facing), Is.LessThan(.05f), "옆걸음/뒷걸음은 몸 방향 유지");
                    Assert.That(Quaternion.Angle(trampoline.transform.rotation, deviceFacing), Is.LessThan(.05f));
                    Assert.That(Vector3.Distance((Vector3)Get(trampoline, "Surface") - player.transform.position, offset), Is.LessThan(.003f),
                        "입력 방향이 바뀌어도 받침대 중심은 캐릭터 주위를 돌아가면 안 된다.");
                }
                yield return new WaitForEndOfFrame();
                Vector3 stopped = player.transform.position;
                CarryMovementInput(Vector3.zero);
                Call(mover, "Update");
                Call(trampoline, "LateUpdate");
                Assert.That(Vector3.ProjectOnPlane(player.transform.position - stopped, Vector3.up).magnitude, Is.LessThan(.003f));
            }

            // 운반 중 일반 점프도 손잡이를 발 위치에 붙인 채 같은 방향을 유지한다.
            Call(mover, "RequestJump");
            yield return WaitUntil(() => (bool)Get(mover, "IsAirborne"), 2, "운반자 일반 점프");
            yield return new WaitForEndOfFrame();
            Assert.That(Quaternion.Angle(player.transform.rotation, facing), Is.LessThan(.05f));
            Assert.That(Vector3.Distance((Vector3)Get(trampoline, "Surface") - player.transform.position, offset), Is.LessThan(.003f));
            yield return WaitUntil(() => body.isGrounded && !(bool)Get(mover, "IsAirborne"), 3, "운반자 점프 착지");

            yield return ReleaseThenArm();
            Input(held: true, progress: 1);
            yield return WaitUntil(() => (ulong)Get(trampoline, "Carrier") == ulong.MaxValue, 2, "긴 F로 내려놓기");
            yield return null; yield return null;
            Assert.That((float)Get(mover, "SpeedMultiplier"), Is.EqualTo(.8f).Within(.001f), "내려놓으면 운반 배율 제거");
            yield return new WaitForEndOfFrame();
            Vector3 dropPosition = trampoline.transform.position;
            CarryMovementInput(Vector3.back);
            Call(mover, "Update");
            Call(trampoline, "LateUpdate");
            Assert.That(Quaternion.Angle(player.transform.rotation, facing), Is.GreaterThan(.01f), "일반 이동 회전 복구");
            Assert.AreEqual(dropPosition, trampoline.transform.position, "놓은 트램폴린은 따라오지 않는다.");

            Input(); yield return null;
            Call(mover, "SetStartPose", handle.position, Quaternion.Euler(0, -70, 0), true);
            SetCarryView(director, -70, 0);
            Input(tapped: true);
            yield return WaitUntil(() => (ulong)Get(trampoline, "Carrier") == Actor, 2, "다른 방향으로 다시 잡기");
            yield return new WaitForEndOfFrame();
            Assert.That(Quaternion.Angle(player.transform.rotation, Quaternion.Euler(0, -70, 0)), Is.LessThan(.05f));
            Call(player, "RequestRestartAll");
            yield return WaitUntil(() => (ulong)Get(trampoline, "Carrier") == ulong.MaxValue, 3, "R 운반 점유 초기화");
            yield return null; yield return null;
            Assert.That((float)Get(mover, "SpeedMultiplier"), Is.EqualTo(.8f).Within(.001f), "R은 운반 배율만 해제하고 기존 부상은 보존");

            Call(mover, "SetStartPose", handle.position, Quaternion.identity, true);
            Input(tapped: true);
            yield return WaitUntil(() => (ulong)Get(trampoline, "Carrier") == Actor, 2, "종료 전 운반");
            yield return null; yield return null;
            Assert.That(walkSpeed * (float)Get(mover, "SpeedMultiplier"), Is.EqualTo(airSpeed * .8f).Within(.001f));
            ((Behaviour)trampoline).enabled = false;
            yield return null; yield return null;
            Assert.That((float)Get(mover, "SpeedMultiplier"), Is.EqualTo(.8f).Within(.001f), "장치 비활성화 후 운반 속도 해제");
        }

        void CarryMovementInput(Vector3 worldDirection)
        {
            var director = StaticGet("CurtainCall.Cameras.CameraViewDirector", "Current");
            Vector3 forward = (bool)Get(director, "UsesMouseLook") ? (Vector3)Get(director, "ViewForward")
                : (Vector3)Get(mover, "CourseForward");
            forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            var frame = System.Activator.CreateInstance(GameType("CurtainCall.Player.PlayerInputFrame"));
            frame.GetType().GetField("Move").SetValue(frame, Vector3.Dot(worldDirection, forward));
            frame.GetType().GetField("Posture").SetValue(frame, Vector3.Dot(worldDirection, Vector3.Cross(Vector3.up, forward)));
            Call(Get(controller, "Scheme"), "Tick", Get(controller, "context"), frame);
        }

        [UnityTest]
        public IEnumerator CarryView_YawTurnsBodyAndDevice_WsadMovesRelativeToCamera_WithoutPitchingSurface()
        {
            DisableEnhancementThreats();
            var trampoline = StaticGet("CurtainCall.HumanCannon.HumanCannonTrampoline", "Current") as Component;
            var director = StaticGet("CurtainCall.Cameras.CameraViewDirector", "Current") as Component;
            Call(mover, "SetStartPose", ((Transform)Get(trampoline, "Handle")).position, Quaternion.identity, true);
            yield return null;
            Input(tapped: true);
            yield return WaitUntil(() => (ulong)Get(trampoline, "Carrier") == Actor && (bool)Get(director, "IsFreeThirdPerson"),
                2, "트램폴린 운반과 3인칭 시점");
            Call(mover, "SetStartPose", new Vector3(0, .01f, 57), Quaternion.identity, true);
            yield return WaitUntil(() => ((CharacterController)Get(mover, "Body")).isGrounded && !(bool)Get(mover, "IsAirborne"),
                3, "회전 검사 바닥 접촉");
            yield return new WaitForEndOfFrame();
            float handleDistance = Vector3.ProjectOnPlane(trampoline.transform.position - ((Transform)Get(trampoline, "Handle")).position,
                Vector3.up).magnitude;
            foreach (float yaw in new[] { 0f, 90f, 180f, 270f })
            {
                Vector3 start = player.transform.position;
                SetCarryView(director, yaw, yaw % 180 == 0 ? 30 : -30);
                yield return null;
                yield return new WaitForEndOfFrame();
                Vector3 forward = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
                Assert.That(Vector3.Angle(player.transform.forward, forward), Is.LessThan(.05f), "정지 상태에서도 카메라 yaw로 몸 회전");
                Assert.That(Vector3.ProjectOnPlane(player.transform.position - start, Vector3.up).magnitude, Is.LessThan(.003f),
                    "마우스 회전만으로 운반자 발 위치가 이동하지 않는다.");
                Assert.That(Vector3.Distance(Vector3.ProjectOnPlane(trampoline.transform.position - player.transform.position, Vector3.up),
                    forward * handleDistance), Is.LessThan(.003f), "트램폴린 중심은 항상 시선 앞에 있다.");
                Assert.That(Vector3.Angle(trampoline.transform.up, Vector3.up), Is.LessThan(.01f), "위를 봐도 반동면은 수평 유지");

                foreach (Vector2 input in new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right, new Vector2(1, -1) })
                {
                    yield return new WaitForEndOfFrame();
                    start = player.transform.position;
                    var frame = System.Activator.CreateInstance(GameType("CurtainCall.Player.PlayerInputFrame"));
                    frame.GetType().GetField("Move").SetValue(frame, input.y);
                    frame.GetType().GetField("Posture").SetValue(frame, input.x);
                    Call(Get(controller, "Scheme"), "Tick", Get(controller, "context"), frame);
                    Call(mover, "Update");
                    Call(trampoline, "LateUpdate");
                    Vector3 expected = Vector3.ClampMagnitude(forward * input.y + Vector3.Cross(Vector3.up, forward) * input.x, 1)
                        * (4.5f * Time.deltaTime);
                    Assert.That(Vector3.Distance(Vector3.ProjectOnPlane(player.transform.position - start, Vector3.up), expected),
                        Is.LessThan(.003f), "W/S 앞뒤, A/D 옆걸음, 대각선 모두 시선 기준 같은 속도");
                    Assert.That(Vector3.Angle(player.transform.forward, forward), Is.LessThan(.05f), "S/A/D 입력이 몸을 뒤/옆으로 돌리지 않는다.");
                }
            }
        }

        static void SetCarryView(Component director, float yaw, float pitch)
        {
            director.GetType().GetField("cursorReleased", InstanceMembers).SetValue(director, true);
            director.GetType().GetField("yaw", InstanceMembers).SetValue(director, yaw);
            director.GetType().GetField("pitch", InstanceMembers).SetValue(director, pitch);
        }

        /// <summary>실제 LAN 호스트 1명의 운반 카메라·해제 회귀. 원격 동료나 다중 클라이언트를 가장하지 않는다.</summary>
        [UnityTest]
        public IEnumerator OriginalCourse_CarryCameraFramesActualDevice_AndRestoresAfterDropAndRestart()
        {
            var director = StaticGet("CurtainCall.Cameras.CameraViewDirector", "Current") as Component;
            var trampoline = StaticGet("CurtainCall.HumanCannon.HumanCannonTrampoline", "Current") as Component;
            Assert.IsNotNull(director);
            Assert.IsNotNull(trampoline);
            yield return WaitUntil(() => (bool)Get(director, "IsFirstPerson"), 2, "평상시 인간대포 1인칭");
            var firstView = Get(director, "CurrentView");
            var handle = (Transform)Get(trampoline, "Handle");
            Call(mover, "SetStartPose", handle.position, Quaternion.identity, true);
            yield return null;
            Assert.IsTrue((bool)Call(trampoline, "CanGrab", player));
            Input(tapped: true);
            yield return WaitUntil(() => (ulong)Get(trampoline, "Carrier") == Actor, 2, "실제 운반 소유권 확정");
            yield return WaitUntil(() => (bool)Get(director, "IsFreeThirdPerson"), 2, "운반 전용 3인칭 요청");
            var carryView = Get(pilot, "carryingView");
            Assert.AreSame(carryView, Get(director, "CurrentView"));
            Assert.AreNotSame(Get(pilot, "seatedView"), carryView, "탑승 구도를 바꾸지 않는 독립 운반 프리셋");
            Assert.AreNotSame(Resources.Load("Settings/Camera/FreeThirdPerson"), carryView, "공유 에셋 대신 런타임 사본");
            Assert.IsFalse((bool)Get(director, "HideLocalModel"), "운반자는 자기 몸을 볼 수 있어야 한다.");
            yield return new WaitForSeconds(0.25f);

            // 실제 마우스 대신 내부 시선 값만 지정한다. 최종 출력은 실제 Cinemachine 프레임을 사용한다.
            director.GetType().GetField("cursorReleased", InstanceMembers).SetValue(director, true);
            foreach (float yaw in new[] { 0f, 90f, 180f, 270f })
            {
                foreach (float side in new[] { -1f, 0f, 1f })
                {
                    float pitch = side * (float)Get(carryView, "PitchLimit");
                    director.GetType().GetField("yaw", InstanceMembers).SetValue(director, yaw);
                    director.GetType().GetField("pitch", InstanceMembers).SetValue(director, pitch);
                    yield return null;
                    yield return null;
                    var view = Camera.main;
                    Assert.IsNotNull(view);
                    Assert.That(view.transform.position.y, Is.GreaterThan(player.transform.position.y + 0.05f),
                        "허용된 운반 시선 끝에서도 카메라가 바닥 아래로 들어가면 안 된다.");
                    var body = (CharacterController)Get(mover, "Body");
                    AssertCarryBoundsVisible(view, body.bounds, "운반자 몸");
                    foreach (var renderer in trampoline.GetComponentsInChildren<MeshRenderer>())
                        if (renderer.enabled) AssertCarryBoundsVisible(view, renderer.bounds, "실제 장치: " + renderer.name);
                }
            }

            Call(trampoline, "RequestDrop");
            yield return WaitUntil(() => (ulong)Get(trampoline, "Carrier") != Actor && (bool)Get(director, "IsFirstPerson"),
                2, "놓기 후 1인칭 복귀");
            Assert.AreSame(firstView, Get(director, "CurrentView"));
            Assert.IsTrue((bool)Get(director, "HideLocalModel"));

            Call(mover, "SetStartPose", handle.position, Quaternion.identity, true);
            yield return null;
            Call(trampoline, "RequestGrab");
            yield return WaitUntil(() => (ulong)Get(trampoline, "Carrier") == Actor && (bool)Get(director, "IsFreeThirdPerson"),
                2, "다시 운반");
            Call(player, "RequestRestartAll");
            yield return WaitUntil(() => (ulong)Get(trampoline, "Carrier") != Actor && (bool)Get(director, "IsFirstPerson"),
                3, "모두 재시작 후 소유권과 1인칭 복귀");
            Assert.AreSame(firstView, Get(director, "CurrentView"));
            Assert.IsTrue((bool)Get(director, "HideLocalModel"));
        }

        static void AssertCarryBoundsVisible(Camera view, Bounds bounds, string reason)
        {
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                        var screen = view.WorldToViewportPoint(corner);
                        Assert.That(screen.z, Is.GreaterThan(view.nearClipPlane), reason + " 카메라 앞");
                        Assert.That(screen.x, Is.InRange(0f, 1f), reason + " 화면 가로 범위");
                        Assert.That(screen.y, Is.InRange(0f, 1f), reason + " 화면 세로 범위");
                    }
        }
    }
}
