using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    /// <summary>
    /// Task-092 기물 손·칸 규칙과 공연용 장비 공통 소모(실제 대기실, 호스트 판정 화면).
    /// 2026-11-01 사용자: 도구만 1·2로 손에 들고 같은 칸을 다시 누르면 집어넣는다. 장비 칸은 손에 들지 않고 고르기만 한다.
    /// 빈손이면 새로 받은 도구를 자동으로 든다. 고른 칸이 없으면 F 길게로 버리지 않는다.
    /// </summary>
    public sealed partial class DevStartLauncherPlayTests
    {
        [UnityTest]
        public IEnumerator W2_HandRules_ToolsHoldEquipmentSelectsAndEquipmentUseCools()
        {
            yield return Launch(2, "Night", 0, "", "WaitingRoomBase");
            var carry = Find("CurtainCall.WaitingRoom.PropCarry");
            var player = Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            ulong me = (ulong)Get(player, "OwnerClientId");
            var itemType = T("CurtainCall.WaitingRoom.PropItem");
            object Item(int uid, string id, int uses) => Activator.CreateInstance(itemType, uid, id, id, uses);
            int InHand() => (int)Get(Call(carry, "InHand", me), "Uid");
            int Chosen() => (int)Get(Call(carry, "Selected", me), "Uid");
            int Held() => ((IEnumerable)Call(carry, "Held", me)).Cast<object>().Count();
            var inventory = Find("CurtainCall.UI.SceneIntegration.CommonInventoryUI");
            string Label() => ((TMP_Text)inventory.GetType().GetField("selectedLabel").GetValue(inventory)).text;
            bool HandIcon() => ((UnityEngine.UI.Image)inventory.GetType().GetField("heldIndicator", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public).GetValue(inventory)).enabled;

            Assert.AreEqual(0, InHand(), "처음에는 빈손");
            // 씬에 저장한 편집 미리보기(예시 이름·손 아이콘·보이는 슬롯 템플릿)는 게임 시작 때 사라진다.
            yield return null;
            Assert.AreEqual("", Label(), "빈손이면 이름 칸이 비어 있다");
            Assert.IsFalse(HandIcon(), "빈손이면 손 아이콘 없음");
            var slotsView = (Component)inventory.GetType().GetField("slots").GetValue(inventory);
            var template = (Component)slotsView.GetType().GetField("slotTemplate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(slotsView);
            Assert.IsFalse(template.gameObject.activeSelf, "슬롯 템플릿은 게임에서 숨는다");
            Assert.AreEqual("None", Call(carry, "Receive", me, Item(9101, "magician-hairpin", 5)).ToString());
            yield return null;
            Assert.AreEqual(9101, InHand(), "빈손이면 받은 도구를 자동으로 든다");
            Assert.AreEqual("None", Call(carry, "Receive", me, Item(9102, "rescue-gloves", 2)).ToString());
            yield return null;
            Assert.AreEqual(9101, InHand(), "장비는 손에 들지 않고 슬롯에만 들어간다");

            Call(carry, "RequestSelectSlot", 1); yield return null;
            Assert.AreEqual(9102, Chosen(), "장비 칸도 고를 수는 있다(버리기·가판대 대상)");
            Assert.AreEqual(0, InHand(), "장비 칸을 고르면 손은 빈다");
            yield return null;
            StringAssert.Contains("구조 장갑", Label());
            Assert.IsFalse(HandIcon(), "장비 칸을 골라도 손 아이콘은 없다");
            Call(carry, "RequestSelectSlot", 1); yield return null;
            Assert.AreEqual(0, Chosen(), "같은 칸을 다시 누르면 선택을 푼다");
            Call(carry, "RequestDrop"); yield return null;
            Assert.AreEqual(2, Held(), "고른 칸이 없으면 버리지 않는다");
            Call(carry, "RequestSelectSlot", 0); yield return null;
            Assert.AreEqual(9101, InHand());
            yield return null;
            StringAssert.Contains("마술사의 머리핀", Label());
            Assert.IsTrue(HandIcon(), "도구를 들면 손 아이콘");
            Call(carry, "RequestSelectSlot", 0); yield return null;
            Assert.AreEqual(0, InHand(), "도구도 같은 칸을 다시 누르면 집어넣는다");

            // 공연용 장비 공통 소모: 실제 효과 때 1 소모, 쿨타임 없음, 마지막 회차 뒤 슬롯에서 사라짐.
            Assert.AreEqual(true, Call(carry, "EquipmentReady", me, "rescue-gloves"));
            Assert.AreEqual(true, Call(carry, "UseEquipment", me, "rescue-gloves", true, true));
            Assert.AreEqual(0f, (float)Call(carry, "CooldownLeft", me, "rescue-gloves"), "공연 기물 쿨타임 없음(2026-11-02)");
            var gloves = ((IEnumerable)Call(carry, "Held", me)).Cast<object>().Single(i => (string)Get(i, "ItemId") == "rescue-gloves");
            Assert.AreEqual(1, Get(gloves, "Uses"));
            Assert.AreEqual(true, Call(carry, "UseEquipment", me, "rescue-gloves", true, true));
            yield return null;
            Assert.AreEqual(1, Held(), "마지막 회차를 쓰면 파괴·슬롯 제거");
            Assert.AreEqual(false, Call(carry, "EquipmentReady", me, "rescue-gloves"), "0회·없는 장비는 새 효과를 내지 못한다");

            // 끈끈이 장갑: 장갑이 있을 때만 제한 있는 매달림이 1.5배로 늘어난다.
            var sticky = T("CurtainCall.WaitingRoom.StickyGloves");
            double Extra() => (double)sticky.GetMethod("ExtraSeconds").Invoke(null, new object[] { me, 7d });
            Assert.AreEqual(0d, Extra(), 1e-6);
            Assert.AreEqual("None", Call(carry, "Receive", me, Item(9103, "sticky-gloves", 3)).ToString());
            yield return null;
            Assert.AreEqual(3.5d, Extra(), 1e-6);
        }
    }
}
