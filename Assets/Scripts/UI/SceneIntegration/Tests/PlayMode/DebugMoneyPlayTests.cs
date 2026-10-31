using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed partial class DevStartLauncherPlayTests
    {
        [UnityTest]
        public IEnumerator W2_DebugLobbyMoney_GrantsThousandOncePerPress()
        {
            yield return Launch(2, "Night", 0, "", "WaitingRoomBase");
            var prop = Find("CurtainCall.WaitingRoom.DebugMoneyProp");
            Assert.IsNotNull(prop, "로비에 디버그 돈 기물이 있어야 한다");
            Assert.AreEqual("S01_Lobby", prop.transform.parent.name);
            var rewards = Find("CurtainCall.Reward.PerformanceRewards");
            Assert.AreEqual(true, Get(rewards, "HasAuthority"));
            long before = (long)Get(rewards, "Balance");

            Call(prop, "Interact");
            yield return new WaitForSecondsRealtime(.3f);
            Assert.AreEqual(before + 1000, (long)Get(rewards, "Balance"), "F 한 번에 1000이 한 번만 들어간다");
            var last = ((IEnumerable)Get(Get(rewards, "State"), "Ledger")).Cast<object>().Last();
            Assert.AreEqual("디버그 지급", Get(last, "Description"));
            Assert.AreEqual(before + 1000, (long)Get(last, "Balance"), "참가자 화면 검사: 장부 마지막 줄 = 잔액");

            Call(prop, "Interact");
            yield return new WaitForSecondsRealtime(.3f);
            Assert.AreEqual(before + 2000, (long)Get(rewards, "Balance"), "다시 누르면 다시 1000");
        }
    }
}
