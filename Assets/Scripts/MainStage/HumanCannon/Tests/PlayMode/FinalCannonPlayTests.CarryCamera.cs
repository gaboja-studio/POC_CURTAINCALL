using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.HumanCannon.Tests
{
    public sealed partial class FinalCannonPlayTests
    {
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
