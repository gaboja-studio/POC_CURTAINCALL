using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed partial class DevStartLauncherPlayTests
    {
        // Task-079 기존 판자 상자→머리핀 회귀 + 실제 배치의 잠긴상자1→계단열쇠, 로비UV→화장실 표식.
        // 성공(개방·발견) 때만 사용 횟수가 1 준다.
        [UnityTest]
        public IEnumerator M2_ExplorationToolsChain_PryLockAndUv()
        {
            yield return Launch(1, "Night", 0, "", "WaitingRoomBase");
            var state = Static("CurtainCall.WaitingRoom.ExplorationState", "Instance");
            var carry = Static("CurtainCall.WaitingRoom.PropCarry", "Instance");
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            var mover = player.GetComponent(T("CurtainCall.Player.PlayerMover"));
            ulong me = (ulong)Get(carry, "LocalClientId");
            var targets = Object.FindObjectsByType(T("CurtainCall.WaitingRoom.ToolTarget"), FindObjectsSortMode.None).Cast<Component>().ToArray();
            var crate = targets.Single(t => (string)Get(t, "TargetId") == "trial-boarded-crate");
            var trunk = targets.Single(t => (string)Get(t, "TargetId") == "structure-a:locked-box-1");
            var mark = Object.FindObjectsByType(T("CurtainCall.WaitingRoom.UvMark"), FindObjectsSortMode.None).Cast<Component>().Single(t => (string)Get(t, "TargetId") == "structure-a:uv-1");
            mover.GetType().GetProperty("Simulated").SetValue(mover, false);

            IEnumerator PickUp(string id)
            {
                object prop = null; float until = Time.realtimeSinceStartup + 5;
                while (prop == null && Time.realtimeSinceStartup < until)
                {
                    prop = ((IEnumerable)Get(carry, "Floor")).Cast<object>().FirstOrDefault(p => (string)Get(Get(p, "Item"), "ItemId") == id && (id != "magician-hairpin" || (int)Get(Get(p, "Item"), "Uid") == (int)T("CurtainCall.WaitingRoom.ItemUses").GetField("FoundUidBase").GetRawConstantValue() + (int)Field(State, "Generation") * 100 + (int)Get(crate, "RewardUidOffset")));
                    if (prop == null) yield return null;
                }
                Assert.IsNotNull(prop, id + " 이(가) 바닥에 놓여야 한다");
                Stand(mover, new Vector3((float)Get(prop, "X"), 0, (float)Get(prop, "Z")), Vector3.forward);
                yield return new WaitForSecondsRealtime(.25f);
                Call(carry, "RequestPickUp", (int)Get(Get(prop, "Item"), "Uid"));
                yield return null;
                Assert.AreEqual(id, Get(Call(carry, "Selected", me), "ItemId"), id + " 을(를) 손에 든다");
            }
            IEnumerator Approach(Component target)
            {
                var p = target.transform.position + target.transform.forward * 1.1f;
                Stand(mover, p, -target.transform.forward);
                yield return new WaitForSecondsRealtime(.3f);
            }

            // 1) 빠루 → 판자 상자. 한 번에 열리고 그때 1 소모, 머리핀이 나온다.
            yield return PickUp("stake-puller");
            int crowbarUses = (int)Get(Call(carry, "Selected", me), "Uses");
            yield return Approach(crate);
            Call(crate, "Interact"); yield return new WaitForSecondsRealtime(.3f);
            Assert.AreEqual(true, Call(state, "IsOpen", "trial-boarded-crate"));
            bool crowbarLeft = ((IEnumerable)Call(carry, "Held", me)).Cast<object>().Any(o => (string)Get(o, "ItemId") == "stake-puller");
            Assert.AreEqual(crowbarUses > 1, crowbarLeft, "개방 때 1 소모(마지막이면 사라짐)");

            // 2) 머리핀 → 트렁크. 실패는 소모 없이 시도 수만 쌓이고, 세 번째 시도는 반드시 열린다.
            yield return PickUp("magician-hairpin");
            int pinUses = (int)Get(Call(carry, "Selected", me), "Uses");
            yield return Approach(trunk);
            int tries = 0;
            while (!(bool)Call(state, "IsOpen", "structure-a:locked-box-1") && tries < 3)
            {
                Call(trunk, "Interact"); tries++;
                yield return new WaitForSecondsRealtime(.15f);
                if (!(bool)Call(state, "IsOpen", "structure-a:locked-box-1"))
                    Assert.AreEqual(pinUses, Get(Call(carry, "Selected", me), "Uses"), "실패한 시도는 소모하지 않는다");
            }
            Assert.AreEqual(true, Call(state, "IsOpen", "structure-a:locked-box-1"), "세 번째 시도 안에 반드시 열린다");
            Assert.AreEqual(tries, Call(state, "Hits", "structure-a:locked-box-1"));

            // 3) UV 등 → 숨은 표식. 처음 발견 때만 1 소모, 표식이 켜진다.
            yield return PickUp("uv-lamp");
            int uvUses = (int)Get(Call(carry, "Selected", me), "Uses");
            var revealedObjects = (GameObject[])mark.GetType().GetField("marks", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(mark);
            var marks = revealedObjects.SelectMany(g => g.GetComponentsInChildren<Renderer>(true)).ToArray();
            Assert.IsTrue(mark.GetComponentsInChildren<Renderer>(true).Any(r => r.gameObject.activeInHierarchy), "UV를 쓰기 전에도 표면 외곽선으로 식별 가능");
            Assert.IsTrue(marks.All(r => !r.gameObject.activeInHierarchy), "발견 전에는 보이지 않는다");
            Stand(mover, mark.transform.position + Vector3.forward * 1.2f, Vector3.back);
            yield return new WaitForSecondsRealtime(.3f);
            Call(mark, "Interact"); yield return new WaitForSecondsRealtime(.3f);
            Assert.AreEqual(true, Call(state, "IsOpen", "structure-a:uv-1"));
            Assert.IsTrue(marks.All(r => r.gameObject.activeInHierarchy), "발견하면 표식이 드러난다");
            bool uvLeft = ((IEnumerable)Call(carry, "Held", me)).Cast<object>().Any(o => (string)Get(o, "ItemId") == "uv-lamp");
            Assert.AreEqual(uvUses > 1, uvLeft);
        }
    }
}
