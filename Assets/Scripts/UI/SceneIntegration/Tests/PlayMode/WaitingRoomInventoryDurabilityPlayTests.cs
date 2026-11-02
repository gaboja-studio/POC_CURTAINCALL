using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    /// <summary>
    /// Task-094 소지 칸 내구도 바·사용 연출(실제 대기실, 호스트 판정 화면).
    /// 2026-11-02 사용자: 칸을 키우고, 남은 내구도를 칸 아래 1단위로 나뉜 바로 보이며, 내구도를 쓰면 그 칸에 사용 연출을 띄운다.
    /// </summary>
    public sealed partial class DevStartLauncherPlayTests
    {
        [UnityTest]
        public IEnumerator W2_InventoryDurabilityBarAndUseEffect()
        {
            yield return Launch(2, "Night", 0, "", "WaitingRoomBase");
            var carry = Find("CurtainCall.WaitingRoom.PropCarry");
            var player = Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            ulong me = (ulong)Get(player, "OwnerClientId");
            var itemType = T("CurtainCall.WaitingRoom.PropItem");
            object Item(int uid, string id, int uses) => Activator.CreateInstance(itemType, uid, id, id, uses);
            var inventory = (Component)Find("CurtainCall.UI.SceneIntegration.CommonInventoryUI");
            var slots = (Component)inventory.GetType().GetField("slots").GetValue(inventory);
            var barType = T("CurtainCall.UI.SceneIntegration.InventorySlotDurabilityBar");
            var fxType = T("CurtainCall.UI.SceneIntegration.InventorySlotUseEffect");
            Component Slot(int i) => (Component)Call(slots, "GetSlot", i);
            Assert.AreEqual(1.5f, slots.transform.localScale.x, .001f, "소지 칸 1.5배(96px)");

            Component Bar(int slot) => Slot(slot).GetComponentInChildren(barType, true);
            int Segments(int slot) => Bar(slot).GetComponentsInChildren<UnityEngine.UI.Image>(false).Count(i => i.name.StartsWith("Segment_"));
            int Shown(int slot) => (int)barType.GetField("shownUses", AnyInstance).GetValue(Bar(slot));

            Assert.AreEqual("None", Call(carry, "Receive", me, Item(9501, "magician-hairpin", 3)).ToString());
            Assert.AreEqual("None", Call(carry, "Receive", me, Item(9502, "rescue-gloves", 2)).ToString());
            yield return null; yield return null;
            Assert.IsNotNull(Slot(0).GetComponent(fxType), "칸마다 사용 연출");
            Assert.AreEqual(5, Segments(0), "머리핀 새 제품 내구도 5칸");
            Assert.AreEqual(3, Shown(0), "남은 3칸만 채움");
            Assert.AreEqual(5, Segments(1), "구조 장갑 새 제품 내구도 5칸");
            Assert.AreEqual(2, Shown(1));
            Assert.AreEqual("", Slot(0).GetComponentsInChildren<TMPro.TMP_Text>(true).First(t => t.name == "60_QuantityTMP").text, "숫자 대신 바");

            int played = 0, lastSlot = -2; bool lastBroken = false;
            Action<int, bool> onPlayed = (slot, broken) => { played++; lastSlot = slot; lastBroken = broken; };
            var evt = inventory.GetType().GetEvent("UseEffectPlayed");
            evt.AddEventHandler(inventory, onPlayed);
            try
            {
                var args = new object[] { me, 9501, null, null };
                Assert.AreEqual("None", carry.GetType().GetMethod("UseItem").Invoke(carry, args).ToString());
                yield return null;
                Assert.AreEqual(1, played, "도구를 쓰면 그 칸에 사용 연출");
                Assert.AreEqual(0, lastSlot); Assert.IsFalse(lastBroken);
                Assert.AreEqual(2, Shown(0), "한 칸 줄어든다");

                Assert.AreEqual(true, Call(carry, "UseEquipment", me, "rescue-gloves", true, true));
                yield return null;
                Assert.AreEqual(2, played, "장비를 쓰면 그 칸에 사용 연출");
                Assert.AreEqual(1, lastSlot); Assert.IsFalse(lastBroken);
                Assert.AreEqual(1, Shown(1));

                Assert.AreEqual(true, Call(carry, "UseEquipment", me, "rescue-gloves", true, true), "쿨타임 없이 바로 다시 쓴다");
                yield return null;
                Assert.AreEqual(3, played);
                Assert.AreEqual(1, lastSlot, "다 써서 사라진 기물이 있던 칸");
                Assert.IsTrue(lastBroken, "마지막 회차는 파괴 연출");

                yield return new WaitForSecondsRealtime(1f);
                Assert.AreEqual(3, played, "같은 사용으로 연출을 다시 띄우지 않는다");
            }
            finally { evt.RemoveEventHandler(inventory, onPlayed); }
        }
    }
}
