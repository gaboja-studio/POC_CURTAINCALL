using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed partial class DevStartLauncherPlayTests
    {
        // Task-079 2단계: 단서 하나를 들고 돌려 본 뒤 플레이로 정상 복귀한다.
        [UnityTest]
        public IEnumerator M2_ClueOverview_OpensLocallyAndReturnsToPlay()
        {
            yield return Launch(1, "Night", 0, "", "WaitingRoomBase");
            var clue = Find("CurtainCall.WaitingRoom.ClueInspectable");
            Assert.IsNotNull(clue, "구조 A에 조사 가능한 단서가 있어야 한다");
            var original = clue.transform.position; var originalRotation = clue.transform.rotation;
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            var mover = player.GetComponent(T("CurtainCall.Player.PlayerMover"));
            // 단서 주변(책상·벽을 피한) 빈 바닥에 세운다.
            Vector3 stand = default;
            for (int a = 0; a < 360 && stand == default; a += 30)
            {
                var p = new Vector3(original.x, 0, original.z) + Quaternion.Euler(0, a, 0) * Vector3.forward * 1f;
                if (!Physics.CheckCapsule(p + Vector3.up * .4f, p + Vector3.up * 1.4f, .3f, ~0, QueryTriggerInteraction.Ignore)) stand = p;
            }
            Assert.AreNotEqual(default(Vector3), stand, "단서 앞에 설 자리가 있어야 한다");
            var face = new Vector3(original.x, 0, original.z) - stand;
            Call(mover, "SetStartPose", stand + Vector3.up * .1f, Quaternion.LookRotation(face));
            yield return new WaitForSecondsRealtime(.3f);

            Assert.IsTrue((float)Call(clue, "Reach", player.transform.position) >= 0f, "단서 앞에서 F가 닿아야 한다");
            Call(clue, "Interact");
            yield return null;
            Assert.AreEqual(true, Get(clue, "IsOpen"));
            Assert.AreEqual(true, Get(clue, "Discovered"));
            Assert.AreEqual(true, Static("CurtainCall.Player.InputGate", "IsRequested"), "보는 동안 이동·다른 입력을 막는다");
            var view = Camera.main.transform.Find("ClueOverview");
            Assert.IsNotNull(view, "카메라 앞에 단서 사본을 띄운다");
            Assert.Greater(view.GetComponentsInChildren<MeshRenderer>().Length, 1, "사본(메시)과 가림막이 있어야 한다");
            Assert.AreEqual(0, view.GetComponentsInChildren<Collider>().Length, "사본은 충돌하지 않는다");

            // 돌려 보기: 회전축을 직접 돌려 앞뒤가 바뀌는지만 본다(마우스 입력은 수동 확인).
            var pivot = view.Find("Pivot"); pivot.Rotate(Vector3.up, 180f, Space.Self);
            yield return null;
            Assert.AreEqual(original, clue.transform.position, "원본은 움직이지 않는다");
            Assert.AreEqual(originalRotation, clue.transform.rotation);

            Call(clue, "SetOpen", false);
            yield return null;
            Assert.AreEqual(false, Get(clue, "IsOpen"));
            Assert.IsNull(Camera.main.transform.Find("ClueOverview"), "닫으면 사본을 치운다");
            Assert.AreEqual(false, Static("CurtainCall.Player.InputGate", "IsRequested"), "닫으면 입력이 돌아온다");

            // 열어 둔 채 밀려나면 저절로 닫힌다.
            Call(clue, "Interact");
            yield return null;
            Call(mover, "SetStartPose", new Vector3(original.x - 4f, .1f, original.z), Quaternion.identity);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.AreEqual(false, Get(clue, "IsOpen"), "멀어지면 닫힌다");
            Assert.AreEqual(false, Static("CurtainCall.Player.InputGate", "IsRequested"));
        }
    }
}
